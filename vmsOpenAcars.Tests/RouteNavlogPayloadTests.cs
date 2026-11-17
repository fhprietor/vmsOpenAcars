using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **El `navlog` del OFP que viaja en el prefile** (`route_navlog`), para que el servidor pueda
    /// verificar la ruta sin bajar nada de SimBrief.
    ///
    /// Lo motiva una medición, no una idea: el 16/11/2026 se prefiró el OFP real **`193868184`**
    /// (SKBO/14L→KATL/09R, 44 tramos, que cruza SKR11 en **KAGEM→EVPIP** a FL340) y phpVMS devolvió
    /// `route_warning: null`, porque resuelve `simbrief_id` contra **su propia tabla `simbriefs`**.
    /// Con el `navlog` en la mano no hay nada que resolver.
    ///
    /// Y lo que estas pruebas protegen, además del formato: que **nunca mande media ruta** (mejor
    /// `null` que una línea que describa un plan que no existe) y que los números salgan con **punto**
    /// decimal aunque el equipo del piloto esté en español.
    /// </summary>
    [TestClass]
    public class RouteNavlogPayloadTests
    {
        private const string FixtureFile = "route-airspace-2026-11-16.json";

        private static readonly Regex Point = new Regex(
            @"^(?<ident>[^,;]+),(?<lat>-?\d+(?:\.\d+)?),(?<lon>-?\d+(?:\.\d+)?),(?<alt>-?\d+)$",
            RegexOptions.Compiled);

        private static List<SimbriefWaypoint> Case863()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", FixtureFile);
            Assert.IsTrue(File.Exists(path), "falta el fixture de datos reales: " + path);

            var fixt = JObject.Parse(File.ReadAllText(path));
            return fixt["case_863"]["fixes"]
                .Select(f => new SimbriefWaypoint
                {
                    Ident = f["ident"].ToString(),
                    Lat   = f["lat"].Value<double>(),
                    Lon   = f["lon"].Value<double>(),
                    AltFt = f["alt"].Value<int>(),
                })
                .ToList();
        }

        [TestMethod]
        public void ElNavlogDelCaso863ViajaEnteroSinPerderPrecision()
        {
            var fixes = Case863();
            Assert.AreEqual(44, fixes.Count);

            string line = RouteNavlogPayload.Build(fixes);

            Assert.IsNotNull(line);
            var parts = line.Split(';');
            Assert.AreEqual(44, parts.Length, "un punto por cada fix del OFP");
            Assert.AreEqual("DE14L", parts[0].Split(',')[0]);
            Assert.AreEqual("KATL", parts[parts.Length - 1].Split(',')[0]);

            // Ida y vuelta: cada punto se lee como un `SimbriefWaypoint` y sus números coinciden
            // dentro de la precisión que se manda (5 decimales, ~1,1 m).
            for (int i = 0; i < parts.Length; i++)
            {
                Match m = Point.Match(parts[i]);
                Assert.IsTrue(m.Success, "punto mal formado: " + parts[i]);

                Assert.AreEqual(fixes[i].Ident, m.Groups["ident"].Value);
                Assert.AreEqual(fixes[i].Lat, double.Parse(m.Groups["lat"].Value, CultureInfo.InvariantCulture), 1e-5,
                    "latitud de " + fixes[i].Ident);
                Assert.AreEqual(fixes[i].Lon, double.Parse(m.Groups["lon"].Value, CultureInfo.InvariantCulture), 1e-5,
                    "longitud de " + fixes[i].Ident);
                Assert.AreEqual(fixes[i].AltFt, int.Parse(m.Groups["alt"].Value, CultureInfo.InvariantCulture),
                    "altitud de " + fixes[i].Ident);
            }

            // El tramo que cruza SKR11 tiene que estar identificable en la línea: el servidor lo va a
            // nombrar en `route_warning.leg`.
            StringAssert.Contains(line, "KAGEM,9.423611,-73.904722,34000");
            StringAssert.Contains(line, "EVPIP,11.939444,-74.694167,34000");
        }

        /// <summary>
        /// **El punto decimal, con la cultura del equipo en español.** Si saliera una coma, el servidor
        /// leería cuatro campos donde hay seis y la ruta sería otra: es el mismo fallo que ya se
        /// corrigió en el resto del payload.
        /// </summary>
        [TestMethod]
        public void LosNumerosSalenConPuntoAunqueElEquipoEsteEnEspanol()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("es-CO");
                var fixes = Case863();

                string line = RouteNavlogPayload.Build(fixes);

                Assert.IsNotNull(line);
                StringAssert.Contains(line, "KAGEM,9.423611,-73.904722,34000");
                Assert.IsFalse(line.Contains("9,423611"), "una coma decimal partiría el punto en dos campos");
            }
            finally { CultureInfo.CurrentCulture = original; }
        }

        [TestMethod]
        public void UnTramoPorEncimaDelTopeNoSeMandaAMedias()
        {
            var fixes = Case863();

            Assert.IsNull(RouteNavlogPayload.Build(fixes, maxPoints: 10),
                "recortar la ruta describiría un plan que no existe: se prefiere no mandar nada");
            Assert.IsNotNull(RouteNavlogPayload.Build(fixes, maxPoints: 44));
            Assert.IsFalse(RouteNavlogPayload.ShouldSend(fixes) == false);
            Assert.AreEqual(RouteNavlogPayload.MaxPoints, 200, "el tope real deja fuera a un plan normal");
        }

        [TestMethod]
        public void SinPlanNoHayNadaQueMandar()
        {
            Assert.IsNull(RouteNavlogPayload.Build(null));
            Assert.IsNull(RouteNavlogPayload.Build(new List<SimbriefWaypoint>()));

            // Un vuelo retomado reconstruye el plan sin `navlog`: el prefile viaja igual, sin campo.
            Assert.IsNull(RouteNavlogPayload.Build(new List<SimbriefWaypoint>
            {
                new SimbriefWaypoint { Ident = "SKBO", Lat = 4.70159, Lon = -74.1469, AltFt = 8361 },
            }));

            Assert.IsFalse(RouteNavlogPayload.ShouldSend(null));
        }

        [TestMethod]
        public void UnPuntoSinCoordenadasInvalidaLaLinea()
        {
            // (0,0) es el hueco de un campo que no llegó. Saltarlo uniría dos puntos que el plan no
            // une, y eso es inventarse el tramo que el servidor tiene que juzgar.
            var fixes = Case863();
            fixes[20].Lat = 0; fixes[20].Lon = 0;

            Assert.IsNull(RouteNavlogPayload.Build(fixes));

            var outOfRange = Case863();
            outOfRange[5].Lat = 91;
            Assert.IsNull(RouteNavlogPayload.Build(outOfRange));
        }

        /// <summary>
        /// El formato es un contrato: `;` entre puntos y `,` dentro. Si alguien cambia el separador sin
        /// subir <see cref="RouteNavlogPayload.Version"/>, el servidor leería otra cosa.
        /// </summary>
        [TestMethod]
        public void ElFormatoYSuVersionSonLosAcordados()
        {
            Assert.AreEqual("route_navlog", RouteNavlogPayload.FieldName);
            Assert.AreEqual("route_navlog_version", RouteNavlogPayload.VersionFieldName);
            Assert.AreEqual(1, RouteNavlogPayload.Version);

            string line = RouteNavlogPayload.Build(Case863());

            Assert.AreEqual(43, line.Count(c => c == ';'), "44 puntos son 43 separadores");
            Assert.AreEqual(0, line.Count(c => c == ' '), "sin espacios: la línea no es un texto para leer");
            Assert.AreEqual(44 * 3, line.Count(c => c == ','), "tres comas por punto (lat, lon, alt)");
        }

        // ── El cuerpo del prefile, que es donde esto viaja de verdad ──────────────

        /// <summary>
        /// **El cuerpo del prefile con el plan del 863**: lleva el `navlog` y el `simbrief_id`, y los
        /// dos con su forma exacta. Es la prueba que une el helper con la petición real, sin red
        /// (`ApiService.BuildPrefileBody`).
        /// </summary>
        [TestMethod]
        public void ElCuerpoDelPrefileLlevaElNavlogYElId()
        {
            var plan = Plan863();
            plan.SimbriefId = "193868184";

            var body = ApiService.BuildPrefileBody(plan, new Pilot { AirlineId = 1 }, "0.9.73", "notas");

            Assert.AreEqual("193868184", body["simbrief_id"]?.ToString());
            Assert.AreEqual(RouteNavlogPayload.Version, body["route_navlog_version"]?.Value<int>());

            string navlog = body["route_navlog"]?.ToString();
            Assert.IsNotNull(navlog);
            Assert.AreEqual(44, navlog.Split(';').Length);
            StringAssert.StartsWith(navlog, "DE14L,");
            StringAssert.Contains(navlog, "KAGEM,9.423611,-73.904722,34000;EVPIP,");

            // Lo de siempre sigue en su sitio: el prefile no cambia por esto.
            Assert.AreEqual("SKBO", body["dpt_airport_id"]?.ToString());
            Assert.AreEqual("KATL", body["arr_airport_id"]?.ToString());
            Assert.AreEqual("in_progress", body["state"]?.ToString());
        }

        /// <summary>
        /// **Sin plan no se manda el campo, y el prefile sigue siendo válido.** Un vuelo retomado
        /// reconstruye el plan sin `navlog` y sin id: el cuerpo tiene que salir sin las dos claves,
        /// no con `null` (la ausencia es un dato, el `null` es otro).
        /// </summary>
        [TestMethod]
        public void ElCuerpoDelPrefileSinPlanNoLlevaEsosCampos()
        {
            var plan = new SimbriefPlan
            {
                Origin = "SKBO", Destination = "SKCG", FlightNumber = "8009",
                Route = "DCT", PlannedAltitude = "FL340",
                Waypoints = new List<SimbriefWaypoint>(),
            };

            var body = ApiService.BuildPrefileBody(plan, new Pilot { AirlineId = 1 }, "0.9.73", "notas");

            Assert.IsNull(body["route_navlog"], "sin navlog no se manda ni `null`");
            Assert.IsNull(body["route_navlog_version"]);
            Assert.IsNull(body["simbrief_id"], "sin id no se manda ni `null`");
            Assert.IsFalse(body.ContainsKey("route_navlog"));
            Assert.IsFalse(body.ContainsKey("simbrief_id"));
            Assert.AreEqual("SKBO", body["dpt_airport_id"]?.ToString());
        }

        /// <summary>El plan real del 863 tal como lo reconstruye el cliente desde el OFP.</summary>
        private static SimbriefPlan Plan863()
        {
            var fixt = JObject.Parse(File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", FixtureFile)));
            var caso = fixt["case_863"];

            return new SimbriefPlan
            {
                Origin          = caso["origin"].ToString(),
                Destination     = caso["dest"].ToString(),
                OriginRunway    = caso["origin_rwy"].ToString(),
                DestinationRunway = caso["dest_rwy"].ToString(),
                AircraftIcao    = caso["aircraft"].ToString(),
                Route           = caso["route"].ToString(),
                FlightNumber    = "863",
                PlannedAltitude = "FL340",
                Waypoints       = Case863(),
            };
        }
    }
}

