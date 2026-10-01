using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El cuerpo del `POST /taxi-routes/observations` con la ruta **PROPUESTA** (`planned`).
    ///
    /// **Este test no llama al endpoint, y no puede hacerlo**: cada POST crea una observación real en
    /// la base de conocimiento de NavData y el proyecto tiene prohibido enviar observaciones
    /// sintéticas para cruzar el umbral de la agregación. Por eso la construcción del cuerpo es una
    /// función pura y aquí se fija su forma.
    ///
    /// Lo que se fija, y por qué: que viaje el `planned` con su geometría, que la polilínea se recorte
    /// a 500 puntos sin perder los extremos, que **no** se cuele `source: typed`/`traced` (esas son las
    /// categorías del flujo de observaciones de uso real, no de una propuesta) y que un
    /// `dataset_version` que no tenemos viaje como `null` en vez de inventarse.
    /// </summary>
    [TestClass]
    public class TaxiRouteObservationTests
    {
        private static List<(double Lat, double Lon)> Line(int count)
        {
            var list = new List<(double Lat, double Lon)>();
            for (int i = 0; i < count; i++) list.Add((4.69 + i / 10000.0, -74.14 - i / 10000.0));
            return list;
        }

        private static JObject Observation(string body)
        {
            // `DateParseHandling.None`: sin esto Newtonsoft convierte el `observed_at` en DateTime al
            // leer el JSON y la comprobación del texto de fecha fallaría por el formato de cultura,
            // no por el contenido.
            using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(body))
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None
            })
            {
                var root = (JObject)JObject.Load(reader);
                var list = root["observations"] as JArray;
                Assert.IsNotNull(list, "el lote viaja bajo la clave `observations`");
                Assert.AreEqual(1, list.Count, "un rodaje de salida = una observación");
                return (JObject)list[0];
            }
        }

        [TestMethod]
        public void TheBodyCarriesThePlannedRouteWithItsGeometry()
        {
            var body = TaxiRouteObservation.Build(
                "SKBO", "14L", "F E X A B5 A A3", Line(4), "a1b2c3d4e5f6",
                observedAtUtc: new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

            var obs     = Observation(body);
            var planned = obs["planned"] as JObject;

            Assert.AreEqual("SKBO", (string)obs["icao"]);
            Assert.AreEqual("14L",  (string)obs["runway"]);
            Assert.IsNotNull(planned, "la marca de que es una PROPUESTA es el campo `planned`");
            Assert.AreEqual("F E X A B5 A A3", (string)planned["text"]);
            Assert.AreEqual("14L", (string)planned["runway"]);
            Assert.AreEqual("a1b2c3d4e5f6", (string)planned["dataset_version"]);

            var poly = planned["polyline"] as JArray;
            Assert.IsNotNull(poly);
            Assert.AreEqual(4, poly.Count);
            Assert.AreEqual(4.69,  (double)poly[0]["lat"], 1e-6);
            Assert.AreEqual(-74.14, (double)poly[0]["lon"], 1e-6);
            Assert.AreEqual(4.6903, (double)poly[3]["lat"], 1e-6);

            Assert.AreEqual("vmsOpenAcars", (string)obs["client"]["name"]);
            Assert.IsFalse(string.IsNullOrWhiteSpace((string)obs["client"]["version"]));
            Assert.AreEqual("2026-10-01T12:00:00Z", (string)obs["observed_at"], body);
        }

        [TestMethod]
        public void TheObservationIsNotLabelledAsTypedOrTraced()
        {
            // Las dos categorías del flujo de observaciones reales describen la ruta que el piloto
            // escribió (`typed`) o la que se deduce de la traza (`traced`). Aquí lo que se manda es la
            // propuesta del cliente: si algún día aparece `source` en este cuerpo, es que se han
            // mezclado los dos flujos.
            var body = TaxiRouteObservation.Build("SKBO", "14L", "F E M A A3", Line(3), null);
            var obs  = Observation(body);

            Assert.IsNull(obs["source"], "una propuesta no lleva `source`");
            Assert.IsFalse(body.Contains("\"typed\""),  "ni `typed`");
            Assert.IsFalse(body.Contains("\"traced\""), "ni `traced`");
            Assert.IsNotNull(obs["planned"]);
        }

        [TestMethod]
        public void WithoutADatasetVersionTheFieldTravelsAsNull_NotInvented()
        {
            var body    = TaxiRouteObservation.Build("SKBO", "14L", "F E M A A3", Line(3), null);
            var planned = (JObject)Observation(body)["planned"];

            Assert.IsTrue(planned.ContainsKey("dataset_version"),
                "el campo tiene que viajar aunque no haya dato: «no lo sé» es información");
            Assert.AreEqual(JTokenType.Null, planned["dataset_version"].Type);
        }

        [TestMethod]
        public void NodeIdsAreOmittedWhenWeCannotVouchForThem()
        {
            // Hoy el grafo enruta fusionando nodos por proximidad (`useNodeIds = false`), así que no
            // podemos afirmar una identidad de nodo que no estamos usando. Se omite, no se inventa.
            var body    = TaxiRouteObservation.Build("SKBO", "14L", "F E M A A3", Line(3), "v1");
            var planned = (JObject)Observation(body)["planned"];
            Assert.IsNull(planned["node_ids"]);

            var withIds = (JObject)Observation(
                TaxiRouteObservation.Build("SKBO", "14L", "F E M A A3", Line(3), "v1",
                                           new List<long> { 1L, 2L, 3L }))["planned"];
            var ids = withIds["node_ids"] as JArray;
            Assert.IsNotNull(ids);
            Assert.AreEqual(3, ids.Count);
        }

        [TestMethod]
        public void AMaximumPolylineIsCappedAndStaysWellUnderTheBodyLimit()
        {
            // 1 200 puntos → 500 en la polilínea, y el cuerpo sigue muy por debajo de los 256 KB.
            var body    = TaxiRouteObservation.Build("SKBO", "14L", "F E M A A3", Line(1200), "v1");
            var planned = (JObject)Observation(body)["planned"];
            var poly    = planned["polyline"] as JArray;

            Assert.AreEqual(PolylineResampler.MaxPolylinePoints, poly.Count);

            int bytes = TaxiRouteObservation.BodyBytes(body);
            Assert.IsTrue(bytes < TaxiRouteObservation.MaxBodyBytes,
                $"el cuerpo mide {bytes} bytes y el tope son {TaxiRouteObservation.MaxBodyBytes}");
        }
    }
}
