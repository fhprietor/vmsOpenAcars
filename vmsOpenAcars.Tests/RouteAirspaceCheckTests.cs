using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **La comprobación de ruta por intersección de segmentos**, con datos reales de NavData y del
    /// OFP del piloto (`Fixtures/route-airspace-2026-11-16.json`, capturado el 16/11/2026).
    ///
    /// El caso que obliga a esta prueba (phpVMS, mensaje del 16/11/2026): la comprobación anterior
    /// muestreaba la ruta —un punto cada ~130 nm en un SKBO–KATL— y **SKR11 / BARRANQUILLA** mide
    /// **72 × 53 nm** (24 puntos, restringida GND–FL999). El vuelo 863 se metió dentro y el ACARS solo
    /// lo vio en vuelo, a las 17:30:55. La pierna real que se usa aquí —`DIBA1A`, `DIBAM → EGOKU`,
    /// **153,7 nm** a 12.000–6.000 ft, publicada por NavData— tiene **los dos extremos fuera** de SKR11
    /// y **cruza la zona por dentro**: un muestreo de los extremos no la ve, un segmento sí.
    ///
    /// Los polígonos y las altitudes son los que publica NavData, sin retocar. Los datos vivos:
    /// `/airspaces/` (radio 200 nm), `/airport/{icao}/sids|stars/` y `/airway/{name}/`.
    /// </summary>
    [TestClass]
    public class RouteAirspaceCheckTests
    {
        private const string FixtureFile = "route-airspace-2026-11-16.json";

        private static JObject _fixture;

        [ClassInitialize]
        public static void LoadFixture(TestContext context)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", FixtureFile);
            Assert.IsTrue(File.Exists(path), "falta el fixture de datos reales: " + path);
            _fixture = JObject.Parse(File.ReadAllText(path));
        }

        // ── Lectura de los datos reales ───────────────────────────────────────────

        /// <summary>La zona tal como llega de NavData, deserializada con el modelo del cliente.</summary>
        private static NavAirspace Zone(string key)
            => JsonConvert.DeserializeObject<NavAirspace>(_fixture["zoned_areas"][key].ToString());

        private static SimbriefWaypoint Waypoint(JToken leg, bool from)
        {
            string prefix = from ? "from" : "to";
            return new SimbriefWaypoint
            {
                Ident = leg[prefix + "_ident"]?.ToString(),
                Lat   = leg[prefix + "_lat"].Value<double>(),
                Lon   = leg[prefix + "_lon"].Value<double>(),
                AltFt = (int)Math.Round(leg[prefix + "_alt_ft"].Value<double>()),
            };
        }

        private static List<SimbriefWaypoint> LegWaypoints(string key)
            => new List<SimbriefWaypoint> { Waypoint(_fixture["legs"][key], true),
                                            Waypoint(_fixture["legs"][key], false) };

        // ── El caso que lo motiva ────────────────────────────────────────────────

        [TestMethod]
        public void UnTramoQueCruzaLaZonaAvisa()
        {
            var hits = RouteAirspaceCheck.Find(LegWaypoints("crosses_skr11_both_outside"),
                                               new[] { Zone("skr11") });

            Assert.AreEqual(1, hits.Count, "la pierna DIBAM–EGOKU cruza SKR11");
            Assert.AreEqual("SKR11 / BARRANQUILLA", hits[0].Area);
            Assert.AreEqual("GND–FL999", hits[0].Limits, "los límites se publican como los da NavData");
            Assert.AreEqual("DIBAM–EGOKU", hits[0].Leg, "el tramo se nombra con los fix del navlog");
            Assert.AreEqual("Restricted", hits[0].Type);
            Assert.AreEqual("revisar_ruta", hits[0].Action);
            Assert.IsFalse(hits[0].FromServer, "este aviso lo calcula el cliente, no el servidor");
        }

        /// <summary>
        /// **La razón de ser del cambio**: con los dos extremos fuera de la zona, mirar los extremos
        /// —lo que hacía el muestreo— no dice nada. Y no es una zona pequeña: es de decenas de millas.
        /// </summary>
        [TestMethod]
        public void LosDosExtremosEstanFueraDeLaZona()
        {
            var zone = Zone("skr11");
            var ring = zone.Geometry.Coordinates[0];
            var leg  = _fixture["legs"]["crosses_skr11_both_outside"];

            var from = Waypoint(leg, true);
            var to   = Waypoint(leg, false);

            Assert.IsFalse(RouteAirspaceCheck.ContainsPoint(from.Lat, from.Lon, ring),
                $"{from.Ident} está FUERA de SKR11: el muestreo del fix no ve nada");
            Assert.IsFalse(RouteAirspaceCheck.ContainsPoint(to.Lat, to.Lon, ring),
                $"{to.Ident} está FUERA de SKR11: el muestreo del fix no ve nada");

            // Las cifras del caso: 153,7 nm de tramo contra una zona de ~72 × 53 nm.
            double legNm = GeoMath.DistanceNm(from.Lat, from.Lon, to.Lat, to.Lon);
            Assert.IsTrue(legNm > 150.0 && legNm < 160.0, $"el tramo mide {legNm:F1} NM");
        }

        /// <summary>
        /// **El caso del informe, tal cual**: el vuelo **863 (SKBO/14L → KATL/09R)** despachado en
        /// SimBrief el 16/11/2026 (`request_id` 193868184, 44 tramos, A21N). Su tramo
        /// **KAGEM → EVPIP**, a **FL340**, entra en **SKR11 / BARRANQUILLA** (GND–FL999). Es el vuelo
        /// que el ACARS solo detectó en vuelo, a las 17:30:55, cuando ya no había nada que corregir.
        ///
        /// Y es la prueba de que el aviso del prefile **no puede llegar hoy**: prefilar este mismo
        /// `simbrief_id` devuelve `route_warning: null` porque phpVMS resuelve el campo contra su
        /// propia tabla `simbriefs`. El cliente sí lo ve —esto—.
        /// </summary>
        [TestMethod]
        public void ElCasoDelVuelo863AvisaDelTramoKagemEvpip()
        {
            var case863 = _fixture["case_863"];
            Assert.AreEqual("193868184", case863["ofp_request_id"].ToString());

            var fixes = case863["fixes"]
                .Select(f => new SimbriefWaypoint
                {
                    Ident = f["ident"].ToString(),
                    Lat   = f["lat"].Value<double>(),
                    Lon   = f["lon"].Value<double>(),
                    AltFt = f["alt"].Value<int>(),
                })
                .ToList();

            Assert.AreEqual(44, fixes.Count);

            var hits = RouteAirspaceCheck.Find(fixes, new[] { Zone("skr11") });

            Assert.AreEqual(1, hits.Count, "el plan del 863 cruza SKR11 una vez");
            Assert.AreEqual("SKR11 / BARRANQUILLA", hits[0].Area);
            Assert.AreEqual("GND–FL999", hits[0].Limits);
            Assert.AreEqual("KAGEM–EVPIP", hits[0].Leg, "el tramo que entra es KAGEM–EVPIP, a FL340");
            Assert.AreEqual("Restricted", hits[0].Type);
        }

        [TestMethod]
        public void UnTramoQueEmpiezaDentroTambienAvisa()
        {
            // UMPUR y MULTU están los dos DENTRO de SKR11 (STAR UMPU1D de SKVP): salir de una zona
            // restringida es igual de malo que entrar, y el tramo tiene que contar.
            var hits = RouteAirspaceCheck.Find(LegWaypoints("starts_inside_skr11"),
                                               new[] { Zone("skr11") });

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual("UMPUR–MULTU", hits[0].Leg);
        }

        // ── La compuerta vertical ────────────────────────────────────────────────

        /// <summary>
        /// **Sobrevolar no es invadir.** Los límites son los reales de cada zona y las altitudes, las
        /// reales del tramo DIBA1A (12.000 → 6.000 ft); lo que se prueba es la compuerta, no que esa
        /// pierna pase por esas dos zonas.
        /// </summary>
        [TestMethod]
        public void LaAltitudDelTramoMandaSobreElCruceLateral()
        {
            var leg = _fixture["legs"]["crosses_skr11_both_outside"];
            int altFrom = (int)leg["from_alt_ft"].Value<double>();
            int altTo   = (int)leg["to_alt_ft"].Value<double>();

            Assert.IsTrue(RouteAirspaceCheck.IsWithinVerticalBand(altFrom, altTo, Zone("skr11")),
                "SKR11 llega a FL999: a 12.000 ft se entra");

            Assert.IsFalse(RouteAirspaceCheck.IsWithinVerticalBand(altFrom, altTo, Zone("skr17")),
                "SKR17 topa a 2.000 ft MSL y el tramo va por encima: no es una invasión");
            Assert.IsFalse(RouteAirspaceCheck.IsWithinVerticalBand(altFrom, altTo, Zone("svr2508")),
                "SVR2508 topa a 3.000 ft MSL: idem");
        }

        [TestMethod]
        public void SinAltitudNoSeDescartaElAviso()
        {
            // `AltFt == 0` es «no sé» en el navlog: se avisa igual, porque un aviso de más es mejor
            // que un vuelo dentro de una zona sin enterarse.
            Assert.IsTrue(RouteAirspaceCheck.IsWithinVerticalBand(0, 0, Zone("skr17")));
            Assert.IsTrue(RouteAirspaceCheck.IsWithinVerticalBand(12000, 0, Zone("skr17")));
        }

        [TestMethod]
        public void LosLimitesSeLeenComoLosPublicaNavData()
        {
            Assert.AreEqual(0.0,     RouteAirspaceCheck.LimitFt(Zone("skr11").LowerLimit));
            Assert.AreEqual(99900.0, RouteAirspaceCheck.LimitFt(Zone("skr11").UpperLimit));
            Assert.AreEqual(2000.0,  RouteAirspaceCheck.LimitFt(Zone("skr17").UpperLimit));
            Assert.AreEqual(3000.0,  RouteAirspaceCheck.LimitFt(Zone("svr2508").UpperLimit));

            Assert.AreEqual("GND–FL999", RouteAirspaceCheck.Limits(Zone("skr11")));
            Assert.AreEqual("GND–2000ft MSL", RouteAirspaceCheck.Limits(Zone("skr17")));
        }

        // ── Lo que NO debe avisar ────────────────────────────────────────────────

        /// <summary>
        /// La ruta real del OFP del piloto (SEQM→KMIA, 27 fix) contra las 30 zonas restringidas o
        /// prohibidas que NavData devolvió para ella: **ningún cruce**. Es el control que impide que
        /// la comprobación avise de todo.
        /// </summary>
        [TestMethod]
        public void UnaRutaRealLimpiaNoAvisa()
        {
            var fixes = _fixture["clean_route"]["fixes"]
                .Select(f => new SimbriefWaypoint
                {
                    Ident = f["ident"].ToString(),
                    Lat   = f["lat"].Value<double>(),
                    Lon   = f["lon"].Value<double>(),
                    AltFt = f["alt"] == null || f["alt"].Type == JTokenType.Null
                        ? 0 : (int)Math.Round(f["alt"].Value<double>()),
                })
                .ToList();

            var zones = _fixture["clean_route"]["airspaces"]
                .Select(z => JsonConvert.DeserializeObject<NavAirspace>(z.ToString()))
                .ToList();

            Assert.AreEqual(27, fixes.Count);
            Assert.AreEqual(30, zones.Count);

            var hits = RouteAirspaceCheck.Find(fixes, zones);

            Assert.AreEqual(0, hits.Count,
                "la ruta real está limpia: " + string.Join(" | ", hits.Select(h => h.Describe())));
        }

        /// <summary>
        /// **`Danger` es advertencia, no prohibición** (`AirspaceViolationPolicy`): el mismo polígono
        /// real de SKR11 marcado como `Danger` no avisa. Lo único que se cambia es el tipo, porque en
        /// el juego de datos real no hay una zona `Danger` cruzada por una pierna publicada.
        /// </summary>
        [TestMethod]
        public void UnaZonaDangerNoAvisa()
        {
            var danger = Zone("skr11");
            danger.Type = "Danger";

            var hits = RouteAirspaceCheck.Find(LegWaypoints("crosses_skr11_both_outside"),
                                               new[] { danger });

            Assert.AreEqual(0, hits.Count, "Danger se registra, pero no avisa");
        }

        [TestMethod]
        public void UnaZonaQueNoEsAcotadaNoParticipa()
        {
            var ctr = Zone("skr11");
            ctr.Type = "CTR";

            Assert.AreEqual(0, RouteAirspaceCheck.Find(LegWaypoints("crosses_skr11_both_outside"),
                                                       new[] { ctr }).Count);
        }

        // ── Una zona, un aviso ───────────────────────────────────────────────────

        [TestMethod]
        public void UnaZonaSeAvisaUnaSolaVezAunqueLaRutaLaCruceDosVeces()
        {
            // DIBAM → EGOKU → DIBAM cruza SKR11 dos veces (ida y vuelta). El piloto no necesita dos
            // avisos del mismo sitio: la zona se nombra una vez, en su primer tramo.
            var points = LegWaypoints("crosses_skr11_both_outside");
            points.Add(Waypoint(_fixture["legs"]["crosses_skr11_both_outside"], true));

            var hits = RouteAirspaceCheck.Find(points, new[] { Zone("skr11") });

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual("DIBAM–EGOKU", hits[0].Leg);
        }

        [TestMethod]
        public void SinRutaOSinZonasNoHayNadaQueDecir()
        {
            Assert.AreEqual(0, RouteAirspaceCheck.Find(null, new[] { Zone("skr11") }).Count);
            Assert.AreEqual(0, RouteAirspaceCheck.Find(new List<SimbriefWaypoint>(), new[] { Zone("skr11") }).Count);
            Assert.AreEqual(0, RouteAirspaceCheck.Find(LegWaypoints("crosses_skr11_both_outside"), null).Count);
            Assert.AreEqual(0, RouteAirspaceCheck.Find(LegWaypoints("crosses_skr11_both_outside"),
                                                       new NavAirspace[0]).Count);
        }

        /// <summary>
        /// Geometría pura: un anillo sin puntos suficientes no es un polígono, y el contacto en un
        /// vértice no cuenta como cruce (rozar la esquina de una zona no es meterse en ella).
        /// </summary>
        [TestMethod]
        public void LaGeometriaDegradaSinPoligonoValido()
        {
            var leg = _fixture["legs"]["crosses_skr11_both_outside"];
            var from = Waypoint(leg, true);
            var to   = Waypoint(leg, false);

            Assert.IsFalse(RouteAirspaceCheck.SegmentCrossesPolygon(from.Lat, from.Lon, to.Lat, to.Lon, null));
            Assert.IsFalse(RouteAirspaceCheck.SegmentCrossesPolygon(from.Lat, from.Lon, to.Lat, to.Lon,
                new List<double[]> { new[] { -73.7, 9.0 }, new[] { -73.6, 9.0 }, new[] { -73.6, 9.1 } }));

            // El vértice sur de SKR11 como "zona" de un solo punto: no hay polígono, no hay aviso.
            Assert.IsFalse(RouteAirspaceCheck.ContainsPoint(from.Lat, from.Lon, null));
        }
    }
}
