using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// <c>RouteCorridor</c> — "¿está el avión donde su propio plan dice que debería?".
    ///
    /// Los datos son el **navlog real de SimBrief** del vuelo SKRG→SKBQ (ruta
    /// <c>BIVI1B BIVIG UW3 SERVO DCT LOLUD LOLU1A</c>, AIRAC 2609), que es exactamente el vuelo
    /// del falso desvío a SKTL: el OFP seguía siendo el último del usuario, así que se pudo
    /// descargar entero. Las coordenadas son las que SimBrief entregó.
    ///
    /// El resultado que importa: los puntos de la llegada planificada dan <b>0 NM</b> de
    /// desviación, y el avión estuvo a <b>25–33 NM</b> de esa traza durante todo el descenso
    /// (se había ido hacia SKCG), así que esta comprobación no habría tocado ese falso positivo
    /// — los seis filtros geométricos son los que lo resuelven.
    /// </summary>
    [TestClass]
    public class RouteCorridorTests
    {
        // ── El navlog real del vuelo SKRG→SKBQ ───────────────────────────────────

        private static SimbriefWaypoint Wp(string ident, double lat, double lon, string via, string stage, bool sidStar)
            => new SimbriefWaypoint { Ident = ident, Lat = lat, Lon = lon, Airway = via, Stage = stage, IsSidStar = sidStar };

        private static List<SimbriefWaypoint> RealPlan() => new List<SimbriefWaypoint>
        {
            Wp("KOXEV",  6.205367, -75.423225, "BIVI1B", "CLB", true),
            Wp("SIREV",  6.293042, -75.243467, "BIVI1B", "CLB", true),
            Wp("XONUD",  6.459708, -75.243467, "BIVI1B", "CLB", true),
            Wp("BIVIG",  7.020833, -75.195000, "BIVI1B", "CLB", false),
            Wp("TOC",    7.679651, -75.095171, "UW3",    "CLB", false),
            Wp("SERVO",  7.715556, -75.089722, "UW3",    "CRZ", false),
            Wp("TOD",    9.045253, -75.003463, "DCT",    "CRZ", false),
            Wp("LOLUD",  9.964550, -74.943458, "DCT",    "DSC", false),
            Wp("EROLA", 10.741239, -74.907936, "LOLU1A", "DSC", true),
            Wp("SKBQ",  10.889461, -74.780678, "LOLU1A", "DSC", true),
        };

        /// <summary>Punto a <paramref name="offsetNm"/> NM en perpendicular del punto medio de
        /// un tramo (lado derecho de la derrota).</summary>
        private static void PointBesideLeg(
            double lat1, double lon1, double lat2, double lon2, double offsetNm,
            out double lat, out double lon)
        {
            GeoMath.ToMeters(lat1, lon1, lat2, lon2, out double dy, out double dx);
            double len = Math.Sqrt(dx * dx + dy * dy);

            double midLat = (lat1 + lat2) / 2.0;
            double midLon = (lon1 + lon2) / 2.0;
            double offsetM = offsetNm * GeoMath.MetersPerNm;

            // Perpendicular a la derrota, normalizada.
            double perpN = -dy / len;
            double perpE =  dx / len;

            lat = midLat + (perpN * offsetM) / GeoMath.MetersPerDegLat;
            lon = midLon + (perpE * offsetM) / (GeoMath.MetersPerDegLat * GeoMath.CosLat(midLat));
        }

        // ── Qué parte del plan es "la llegada" ───────────────────────────────────

        /// <summary>
        /// Se define por distancia (los últimos NM de la traza), no por las banderas
        /// `IsSidStar`/`Stage`: en este navlog el fix de transición **LOLUD** llegó con
        /// `is_sid_star = 0` pese a pertenecer a la llegada, así que filtrar por bandera dejaba
        /// el corredor reducido al tramo EROLA→SKBQ de 11 NM.
        /// </summary>
        [TestMethod]
        public void ArrivalFixes_TakeTheLastLegs_NotTheFlaggedOnes()
        {
            List<SimbriefWaypoint> arrival = RouteCorridor.ArrivalFixes(RealPlan());

            Assert.AreEqual(3, arrival.Count);
            CollectionAssert.AreEqual(
                new[] { "LOLUD", "EROLA", "SKBQ" },
                arrival.ConvertAll(w => w.Ident).ToArray());
        }

        /// <summary>Y la ruta en crucero queda fuera: la regla es sobre la llegada, no sobre
        /// todo el plan.</summary>
        [TestMethod]
        public void EnroutePosition_IsNotOnTheArrival()
        {
            // SERVO y su entorno están a más de 100 NM de la llegada.
            Assert.IsFalse(RouteCorridor.IsOnArrival(RealPlan(), 7.715556, -75.089722));
            Assert.IsFalse(RouteCorridor.IsOnArrival(RealPlan(), 9.045253, -75.003463)); // TOD
        }

        // ── Sobre la llegada: 0 NM ───────────────────────────────────────────────

        [TestMethod]
        public void PointsOnThePlannedArrival_AreOnTheCorridor()
        {
            double? erola = RouteCorridor.DistanceFromArrivalNm(RealPlan(), 10.741239, -74.907936);
            double? skbq  = RouteCorridor.DistanceFromArrivalNm(RealPlan(), 10.889461, -74.780678);
            double? mid   = RouteCorridor.DistanceFromArrivalNm(RealPlan(), 10.815350, -74.844307);

            Assert.AreEqual(0.0, erola.Value, 0.2);
            Assert.AreEqual(0.0, skbq.Value, 0.2);
            Assert.AreEqual(0.0, mid.Value, 0.2);

            Assert.IsTrue(RouteCorridor.IsOnArrival(RealPlan(), 10.741239, -74.907936));
            Assert.IsTrue(RouteCorridor.IsOnArrival(RealPlan(), 10.815350, -74.844307));
        }

        [TestMethod]
        public void JustBesideTheArrivalCenterline_IsStillOnTheCorridor()
        {
            SimbriefWaypoint a = RealPlan()[8];   // EROLA
            SimbriefWaypoint b = RealPlan()[9];   // SKBQ
            PointBesideLeg(a.Lat, a.Lon, b.Lat, b.Lon, 3.0, out double lat, out double lon);

            double? d = RouteCorridor.DistanceFromArrivalNm(RealPlan(), lat, lon);
            Assert.AreEqual(3.0, d.Value, 0.3, "3 NM al lado de la final");
            Assert.IsTrue(RouteCorridor.IsOnArrival(RealPlan(), lat, lon));
        }

        /// <summary>
        /// El valor del corredor: a 7 NM de la llegada presentada ya no se suprime nada, y el
        /// caso vuelve a decidirse con los filtros geométricos.
        /// </summary>
        [TestMethod]
        public void WellBesideTheArrival_IsOutsideTheCorridor()
        {
            SimbriefWaypoint a = RealPlan()[8];
            SimbriefWaypoint b = RealPlan()[9];
            PointBesideLeg(a.Lat, a.Lon, b.Lat, b.Lon, 7.0, out double lat, out double lon);

            double? d = RouteCorridor.DistanceFromArrivalNm(RealPlan(), lat, lon);
            Assert.AreEqual(7.0, d.Value, 0.3);
            Assert.IsFalse(RouteCorridor.IsOnArrival(RealPlan(), lat, lon));
            Assert.AreEqual(5.0, RouteCorridor.ArrivalCorridorNm);
        }

        // ── El vuelo real del falso SKTL: fuera de su propio plan ────────────────

        /// <summary>
        /// Las posiciones exactas del descenso en que el cliente anunció el desvío a SKTL, y
        /// las de alrededor. El avión se había ido hacia SKCG: estaba a 25–33 NM de la llegada
        /// que él mismo había presentado, así que esta regla <b>no</b> lo habría silenciado.
        /// </summary>
        [TestMethod]
        public void RealFlightDuringTheFalseSktlDiversion_WasFarOffItsOwnPlan()
        {
            double[][] positions =
            {
                new[] {  9.130850, -75.409160 },   // 15:11:53,  24.3 NM fuera
                new[] {  9.223490, -75.430160 },   // 15:12:47  ¡evento!
                new[] {  9.232720, -75.432250 },   // 15:12:52
                new[] {  9.331890, -75.454740 },   // 15:13:52,  27.8 NM
                new[] {  9.522530, -75.492530 },   // 15:15:53,  30.8 NM
                new[] {  9.832650, -75.504000 },   // 15:19:23,  32.7 NM
            };

            foreach (double[] p in positions)
            {
                double? d = RouteCorridor.DistanceFromArrivalNm(RealPlan(), p[0], p[1]);

                Assert.IsTrue(d.HasValue);
                Assert.IsTrue(d.Value > 20.0,
                    $"a {p[0]}/{p[1]} el avión estaba a {d.Value:F1} NM de su llegada planificada");
                Assert.IsFalse(RouteCorridor.IsOnArrival(RealPlan(), p[0], p[1]));
            }
        }

        // ── Sin plan utilizable, la regla no opina ───────────────────────────────

        [TestMethod]
        public void WithoutAUsablePlan_NothingIsSuppressed()
        {
            Assert.IsFalse(RouteCorridor.IsOnArrival(null, 10.0, -75.0));
            Assert.IsFalse(RouteCorridor.IsOnArrival(new List<SimbriefWaypoint>(), 10.0, -75.0));

            // Un plan con un solo fix (o sin navlog) no permite medir nada.
            var single = new List<SimbriefWaypoint> { Wp("SKBQ", 10.889461, -74.780678, "", "", false) };
            Assert.IsNull(RouteCorridor.DistanceFromArrivalNm(single, 10.0, -75.0));
            Assert.IsFalse(RouteCorridor.IsOnArrival(single, 10.0, -75.0));
        }
    }
}
