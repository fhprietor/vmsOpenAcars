using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Core.Helpers;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Arma el cuerpo del `POST /taxi-routes/observations` con la ruta de rodaje **PROPUESTA** por el
    /// grafo —el objeto `planned` que aprobó NavData (su mensaje nº 10, §3)—.
    ///
    /// **Qué es esto y qué no.** El `planned` es la línea base contra la que NavData quiere medir su
    /// regla «no empeora»: la ruta que el cliente propuso, con su geometría, **en el momento en que se
    /// enseñó el popup**. No es la ruta que el piloto acaba tecleando —esa sigue yendo, como siempre,
    /// al campo `Taxi Route` del PIREP—, ni la traza rodada, ni una observación de uso real: por eso
    /// **no se manda `source: typed` ni `source: traced`**, que son las dos categorías acordadas para
    /// el flujo de observaciones reales. Aquí la marca de que es una propuesta es el propio campo
    /// `planned`.
    ///
    /// Puro a propósito —recibe la fecha en vez de leer el reloj— para poder fijar el cuerpo con un
    /// test **sin salir a la red**: el endpoint no se llama desde un test, porque cada POST crearía
    /// una observación sintética en su base y el proyecto lo tiene prohibido explícitamente.
    /// </summary>
    internal static class TaxiRouteObservation
    {
        /// <summary>Tope de cuerpo que acepta NavData (256 KB; su mensaje nº 10).</summary>
        internal const int MaxBodyBytes = 256 * 1024;

        /// <summary>Precisión de las coordenadas: 6 decimales son ~0,11 m, muy por debajo del error de
        /// la red del escenario, y evitan arrastrar 15 dígitos de doble por punto.</summary>
        internal const int CoordinateDecimals = 6;

        /// <summary>
        /// Devuelve el JSON del lote (siempre una sola observación) con el `planned`. La polilínea se
        /// recorta con <see cref="PolylineResampler.Cap"/> para no pasarse nunca del tope de puntos.
        /// <paramref name="datasetVersion"/> puede ser <c>null</c> y entonces el campo viaja como
        /// `null`: **no se fabrica** una versión del dataset que no tengamos.
        /// <paramref name="nodeIds"/> es opcional; si es <c>null</c> el campo se **omite**.
        /// <paramref name="observedAtUtc"/> es opcional; si es <c>null</c> también se omite.
        /// </summary>
        internal static string Build(
            string icao, string runway, string text,
            IList<(double Lat, double Lon)> polyline,
            string datasetVersion,
            IList<long> nodeIds = null,
            DateTime? observedAtUtc = null)
        {
            var capped = PolylineResampler.Cap(polyline);

            var planned = new JObject
            {
                ["text"]   = text   ?? "",
                ["runway"] = runway ?? "",
            };

            var points = new JArray();
            foreach (var p in capped)
            {
                points.Add(new JObject
                {
                    ["lat"] = Math.Round(p.Lat, CoordinateDecimals),
                    ["lon"] = Math.Round(p.Lon, CoordinateDecimals),
                });
            }
            planned["polyline"] = points;

            // `null` explícito cuando no hay dato: NavData pidió `dataset_version` para poder auditar
            // la polilínea junto al dataset con el que se generó, y «no lo sé» tiene que verse como
            // tal, no desaparecer del cuerpo.
            planned["dataset_version"] = datasetVersion == null
                ? JValue.CreateNull()
                : (JToken)datasetVersion;

            if (nodeIds != null)
            {
                var ids = new JArray();
                foreach (long id in nodeIds) ids.Add(id);
                planned["node_ids"] = ids;
            }

            var observation = new JObject
            {
                ["icao"]    = icao    ?? "",
                ["runway"]  = runway  ?? "",
                ["planned"] = planned,
                // El nombre y la versión del cliente, como en el resto del contrato de observaciones
                // (`client: {name, version}`): es lo que deja reproducir un caso concreto.
                ["client"]  = new JObject
                {
                    ["name"]    = "vmsOpenAcars",
                    ["version"] = AppInfo.Version,
                },
            };
            if (observedAtUtc.HasValue)
                observation["observed_at"] = observedAtUtc.Value.ToUniversalTime()
                                                          .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

            var body = new JObject { ["observations"] = new JArray { observation } };
            return body.ToString(Formatting.None);
        }

        /// <summary>
        /// Tamaño real del cuerpo en bytes UTF-8. Se usa para no enviar algo que el servidor va a
        /// rechazar por tamaño: con el tope de 500 puntos el caso normal ronda los 20 KB, así que
        /// esto es una red de seguridad, no una restricción que recorte datos de verdad.
        /// </summary>
        internal static int BodyBytes(string body)
            => body == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(body);
    }
}
