using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// RAAS: grafo de calles, ruta editable y avisos.
    ///
    /// El fixture son **106 segmentos reales de SKBO** descargados del NavData
    /// (`/airport/SKBO/taxiways/`, AIRAC 2609) más las posiciones y rumbos reales del rodaje del
    /// PIREP `MNjR664PBAr25RbD` (SKBO→MMGL): las rutas de prueba son las que el piloto hizo de
    /// verdad, no geometría inventada.
    /// </summary>
    [TestClass]
    public class RaasTests
    {
        private static TaxiGraph.Segment S(string name, double lat1, double lon1, double lat2, double lon2)
            => new TaxiGraph.Segment { Name = name, Lat1 = lat1, Lon1 = lon1, Lat2 = lat2, Lon2 = lon2 };

        private static List<TaxiGraph.Segment> Skbo() => new List<TaxiGraph.Segment>
        {
            S("B", 4.690934, -74.126732, 4.690434, -74.126068),
            S("B", 4.691404, -74.127357, 4.690934, -74.126732),
            S("B", 4.691777, -74.127853, 4.691404, -74.127357),
            S("B", 4.692027, -74.128181, 4.691777, -74.127853),
            S("B", 4.692247, -74.128479, 4.692027, -74.128181),
            S("B", 4.693332, -74.129906, 4.692247, -74.128479),
            S("B", 4.693617, -74.130287, 4.693332, -74.129906),
            S("B", 4.693827, -74.130577, 4.693617, -74.130287),
            S("B", 4.694089, -74.130920, 4.693827, -74.130577),
            S("B", 4.694346, -74.131271, 4.694089, -74.130920),
            S("B", 4.694539, -74.131531, 4.694346, -74.131271),
            S("B", 4.694855, -74.131943, 4.694539, -74.131531),
            S("B", 4.695103, -74.132271, 4.694855, -74.131943),
            S("B", 4.695529, -74.132843, 4.695103, -74.132271),
            S("B", 4.695645, -74.133011, 4.695529, -74.132843),
            S("B", 4.696244, -74.133820, 4.695645, -74.133011),
            S("B", 4.697641, -74.135658, 4.696244, -74.133820),
            S("B", 4.698036, -74.136177, 4.697641, -74.135658),
            S("B", 4.698842, -74.137253, 4.698036, -74.136177),
            S("B", 4.699740, -74.138451, 4.698842, -74.137253),
            S("B", 4.700963, -74.140068, 4.699740, -74.138451),
            S("B", 4.702394, -74.141968, 4.700963, -74.140068),
            S("B", 4.703614, -74.143593, 4.702394, -74.141968),
            S("B", 4.704093, -74.144234, 4.703614, -74.143593),
            S("B", 4.704591, -74.144890, 4.704093, -74.144234),
            S("B9", 4.698252, -74.137650, 4.698842, -74.137253),
            S("B9", 4.698791, -74.137115, 4.698842, -74.137253),
            S("B9", 4.698829, -74.136841, 4.698791, -74.137115),
            S("B9", 4.698927, -74.136208, 4.698829, -74.136841),
            S("C", 4.696697, -74.135590, 4.696454, -74.135269),
            S("C", 4.697074, -74.136635, 4.696768, -74.136932),
            S("C", 4.697137, -74.136177, 4.696865, -74.135811),
            S("C", 4.697309, -74.136406, 4.697137, -74.136177),
            S("C", 4.697309, -74.136406, 4.697074, -74.136635),
            S("C", 4.697319, -74.136955, 4.696816, -74.137337),
            S("C", 4.697453, -74.136597, 4.697581, -74.136765),
            S("C", 4.697453, -74.136597, 4.697309, -74.136406),
            S("C", 4.697581, -74.136765, 4.697319, -74.136955),
            S("C", 4.697674, -74.137428, 4.697264, -74.137741),
            S("C", 4.697748, -74.136986, 4.697581, -74.136765),
            S("C", 4.697748, -74.136986, 4.697931, -74.137230),
            S("C", 4.697931, -74.137230, 4.698008, -74.137329),
            S("C", 4.697931, -74.137230, 4.697674, -74.137428),
            S("C", 4.698008, -74.137329, 4.698252, -74.137650),
            S("C", 4.698152, -74.138069, 4.697745, -74.138382),
            S("C", 4.698252, -74.137650, 4.698290, -74.137703),
            S("C", 4.698290, -74.137703, 4.698413, -74.137878),
            S("C", 4.698413, -74.137878, 4.698152, -74.138069),
            S("C", 4.698413, -74.137878, 4.698528, -74.138023),
            S("C", 4.698528, -74.138023, 4.698747, -74.138313),
            S("C", 4.698747, -74.138313, 4.698820, -74.138420),
            S("C", 4.698820, -74.138420, 4.699059, -74.138733),
            S("C", 4.699059, -74.138733, 4.699183, -74.138901),
            S("C", 4.699183, -74.138901, 4.699379, -74.139168),
            S("C", 4.699379, -74.139168, 4.699490, -74.139305),
            S("C", 4.699490, -74.139305, 4.699754, -74.139664),
            S("C", 4.699754, -74.139664, 4.700096, -74.140129),
            S("C", 4.700096, -74.140129, 4.700196, -74.140259),
            S("C", 4.700556, -74.140739, 4.700420, -74.140556),
            S("C", 4.700710, -74.140945, 4.700556, -74.140739),
            S("C", 4.700937, -74.141243, 4.700710, -74.140945),
            S("C", 4.701037, -74.141373, 4.700937, -74.141243),
            S("C", 4.701316, -74.141747, 4.701037, -74.141373),
            S("C", 4.701562, -74.142677, 4.701635, -74.142532),
            S("C", 4.701600, -74.142860, 4.701562, -74.142677),
            S("C", 4.701625, -74.142151, 4.701376, -74.141823),
            S("C", 4.701635, -74.142532, 4.701701, -74.142326),
            S("C", 4.701701, -74.142326, 4.701625, -74.142151),
            S("C", 4.701890, -74.143257, 4.701600, -74.142860),
            S("C", 4.702137, -74.143570, 4.701890, -74.143257),
            S("C", 4.702351, -74.144554, 4.702831, -74.145195),
            S("C", 4.702490, -74.138924, 4.701514, -74.139641),
            S("C", 4.702675, -74.144302, 4.702137, -74.143570),
            S("C", 4.702831, -74.145195, 4.703326, -74.145844),
            S("C", 4.703073, -74.138458, 4.702490, -74.138924),
            S("K", 4.688949, -74.137314, 4.688943, -74.137192),
            S("K", 4.688986, -74.137421, 4.688949, -74.137314),
            S("K", 4.689999, -74.138763, 4.688986, -74.137421),
            S("K", 4.689999, -74.138763, 4.690287, -74.139137),
            S("K", 4.690287, -74.139137, 4.690559, -74.139442),
            S("K", 4.690559, -74.139442, 4.691197, -74.140030),
            S("K", 4.691247, -74.139885, 4.691197, -74.140030),
            S("K", 4.691331, -74.140129, 4.691247, -74.139885),
            S("K", 4.691479, -74.140327, 4.691895, -74.140007),
            S("K", 4.691479, -74.140327, 4.691331, -74.140129),
            S("K", 4.691530, -74.140396, 4.691479, -74.140327),
            S("K", 4.691722, -74.140656, 4.691530, -74.140396),
            S("K", 4.691803, -74.140770, 4.692214, -74.140434),
            S("K", 4.691803, -74.140770, 4.691722, -74.140656),
            S("K", 4.691895, -74.140007, 4.691936, -74.139969),
            S("K", 4.692003, -74.141029, 4.691803, -74.140770),
            S("K", 4.692119, -74.141182, 4.692003, -74.141029),
            S("K", 4.692214, -74.140434, 4.692225, -74.140419),
            S("K", 4.692441, -74.141609, 4.692119, -74.141182),
            S("K", 4.692758, -74.142029, 4.692441, -74.141609),
            S("K", 4.693081, -74.142456, 4.692758, -74.142029),
            S("K", 4.693178, -74.142578, 4.693081, -74.142456),
            S("K", 4.693436, -74.142921, 4.693178, -74.142578),
            S("K", 4.695133, -74.145164, 4.693436, -74.142921),
            S("K", 4.696675, -74.147232, 4.695133, -74.145164),
            S("K", 4.697452, -74.148262, 4.696675, -74.147232),
            S("K", 4.697954, -74.148926, 4.698438, -74.149574),
            S("K", 4.697954, -74.148926, 4.697452, -74.148262),
            S("K", 4.701940, -74.154236, 4.698438, -74.149574),
            S("K", 4.703229, -74.155960, 4.701940, -74.154236),
            S("K", 4.705357, -74.158798, 4.703229, -74.155960),
            S("K", 4.710250, -74.165283, 4.705357, -74.158798),
            S("K", 4.711980, -74.167587, 4.712306, -74.168022),
            S("K", 4.711980, -74.167587, 4.710250, -74.165283),
            S("K", 4.712306, -74.168022, 4.712507, -74.168327),
            S("K1", 4.710662, -74.168892, 4.711710, -74.169029),
            S("K1", 4.711710, -74.169029, 4.711996, -74.168938),
            S("K1", 4.711996, -74.168938, 4.712327, -74.168686),
            S("K1", 4.712327, -74.168686, 4.712507, -74.168327),
            S("K1", 4.712568, -74.168030, 4.712507, -74.168327),
            S("K1", 4.712678, -74.167740, 4.712568, -74.168030),
            S("M", 4.693081, -74.142456, 4.693501, -74.142136),
            S("M", 4.693501, -74.142136, 4.693571, -74.142082),
            S("M", 4.699078, -74.149071, 4.698438, -74.149574),
            S("M", 4.699641, -74.148636, 4.699078, -74.149071),
            S("M", 4.699863, -74.148468, 4.699641, -74.148636),
            S("M", 4.701816, -74.146996, 4.699863, -74.148468),
            S("M", 4.703326, -74.145844, 4.701816, -74.146996),
            S("M", 4.704591, -74.144890, 4.703326, -74.145844),
            S("M", 4.705154, -74.144478, 4.704591, -74.144890),
            S("M", 4.712453, -74.163712, 4.712614, -74.163757),
            S("M", 4.712614, -74.163757, 4.712734, -74.163864),
            S("M", 4.712734, -74.163864, 4.713062, -74.164299),
            S("M", 4.713062, -74.164299, 4.712293, -74.164932),
            S("M", 4.713062, -74.164299, 4.714015, -74.165565),
            S("M", 4.714015, -74.165565, 4.714335, -74.165993),
            S("M", 4.714335, -74.165993, 4.714400, -74.166222),
            S("M", 4.714400, -74.166222, 4.714354, -74.166397),
            S("V", 4.710191, -74.168777, 4.710662, -74.168892),
        };

        // Puesto G49 (donde el log dice «SALIENDO DESDE G49») y umbral real de la 14R.
        private const double StandLat = 4.697740, StandLon = -74.138390;
        private const double Rwy14RLat = 4.710490, Rwy14RLon = -74.169160;

        [TestMethod]
        public void SuggestRoute_FromTheStandToRunway14R_FollowsTheRealTaxiways()
        {
            var r = TaxiGraph.Suggest(Skbo(), StandLat, StandLon, Rwy14RLat, Rwy14RLon);

            Assert.IsTrue(r.Found, "debe encontrar ruta por el grafo de calles");
            Assert.IsTrue(r.DistanceM > 1500, $"la ruta real es larga (fue ~{r.DistanceM:F0} m)");
            Assert.AreEqual("C", r.Names.First(), "el puesto está sobre la calle C");
            CollectionAssert.Contains(r.Names, "B");
            CollectionAssert.Contains(r.Names, "M");
            CollectionAssert.Contains(r.Names, "K");
            Assert.IsTrue(r.Names.Last() == "V" || r.Names.Last() == "K1",
                $"la última calle debe ser la de entrada a la 14R, no {r.Names.Last()}");
            Assert.IsFalse(r.Text.Contains("  "), "la ruta se escribe con un solo espacio entre calles");
        }

        [TestMethod]
        public void SuggestRoute_WithoutSegments_DoesNotInventARoute()
        {
            var r = TaxiGraph.Suggest(new List<TaxiGraph.Segment>(), StandLat, StandLon,
                                      Rwy14RLat, Rwy14RLon);
            Assert.IsFalse(r.Found);
            Assert.AreEqual("", r.Text);
        }

        [TestMethod]
        public void ParseRoute_AcceptsWhatPilotsActuallyType()
        {
            CollectionAssert.AreEqual(new[] { "B", "M", "K1", "V" },
                TaxiRoutePlan.Parse("B M K1 V").Names);
            CollectionAssert.AreEqual(new[] { "B", "M", "K1", "V" },
                TaxiRoutePlan.Parse("  b,m,k1 , v ").Names.ToArray());
            CollectionAssert.AreEqual(new[] { "B", "M", "K1", "V" },
                TaxiRoutePlan.Parse("B -> M -> K1 → V").Names.ToArray());
            CollectionAssert.AreEqual(new[] { "B", "M", "K1" },
                TaxiRoutePlan.Parse("B  M   M  K1").Names.ToArray(),
                "un nombre repetido seguido se colapsa: es la misma calle");
            CollectionAssert.AreEqual(new[] { "B", "M", "B" },
                TaxiRoutePlan.Parse("B M B").Names.ToArray(),
                "volver a una calle ya usada es una ruta válida y no se colapsa");
            Assert.AreEqual("B M K1 V", TaxiRoutePlan.Parse("B M K1 V").ToText());
        }

        [TestMethod]
        public void Guidance_OnTheRoute_PointsAtTheNextTaxiway()
        {
            // Punto real del rodaje sobre la calle C, camino del cruce con B.
            var g = RaasAdvisor.ResolveGuidance(Skbo(),
                TaxiRoutePlan.Parse("C B M K V"), 4.698152, -74.138069, "C");

            Assert.IsTrue(g.HasRoute);
            Assert.IsTrue(g.OnRoute);
            Assert.IsFalse(g.Done);
            Assert.AreEqual("B", g.NextTaxiway);
            Assert.IsTrue(g.DistanceToTurnM > 0 && g.DistanceToTurnM < 400,
                $"el cruce C/B está cerca de ese punto (dio {g.DistanceToTurnM:F0} m)");
        }

        [TestMethod]
        public void Guidance_OnTheLastTaxiway_IsDone()
        {
            var g = RaasAdvisor.ResolveGuidance(Skbo(),
                TaxiRoutePlan.Parse("C B M K V"), 4.710191, -74.168777, "V");
            Assert.IsTrue(g.Done, "en la última calle de la ruta no queda giro que anunciar");
        }

        [TestMethod]
        public void Guidance_OffTheRoute_SaysSo()
        {
            var g = RaasAdvisor.ResolveGuidance(Skbo(),
                TaxiRoutePlan.Parse("B M K V"), 4.697740, -74.138390, "C");
            Assert.IsTrue(g.HasRoute);
            Assert.IsFalse(g.OnRoute, "C no está en la ruta B M K V");
            Assert.AreEqual("B", g.NextTaxiway, "se apunta a la primera calle del plan");
        }

        [TestMethod]
        public void SideOfTurn_Right_Left_Straight()
        {
            Assert.AreEqual(TurnSide.Right,    RaasAdvisor.SideOfTurn(270, 0));
            Assert.AreEqual(TurnSide.Right,    RaasAdvisor.SideOfTurn(270, 300));
            Assert.AreEqual(TurnSide.Left,     RaasAdvisor.SideOfTurn(0, 270));
            Assert.AreEqual(TurnSide.Straight, RaasAdvisor.SideOfTurn(270, 275));
            Assert.AreEqual(TurnSide.Straight, RaasAdvisor.SideOfTurn(double.NaN, 10));
        }

        // ── Los cinco puntos reales del rodaje frente al hold-short real de la 14R ──────────
        // (4.71251,-74.16833) (4.71233,-74.16869) (4.71231,-74.16802) (4.71200,-74.16894)
        // (4.71171,-74.16903), todos con heading 136° = el eje de la pista.
        [TestMethod]
        public void HoldShort_RealPointsOfThePirep_ProduceApproachingAndStop()
        {
            var advisor = new RaasAdvisor();
            var t0 = new DateTime(2026, 9, 24, 21, 50, 0, DateTimeKind.Utc);

            // 22 m del hold-short, rodando a 8 kt y yendo hacia él (punto donde el piloto frenó).
            var stop = advisor.Evaluate(new RaasAdvisor.Inputs
            {
                GroundSpeedKt = 8,
                HoldShortRunway = "14R",
                HoldShortDistanceM = 22,
                HeadingTowardHoldShort = true
            }, t0);
            Assert.AreEqual(RaasCalloutType.HoldShortStop, stop.Type);
            Assert.AreEqual("14R", stop.Runway);

            // 17 m, misma situación 20 s después: ya se dijo, no se repite.
            var repeated = advisor.Evaluate(new RaasAdvisor.Inputs
            {
                GroundSpeedKt = 8, HoldShortRunway = "14R",
                HoldShortDistanceM = 17, HeadingTowardHoldShort = true
            }, t0.AddSeconds(5));
            Assert.AreEqual(RaasCalloutType.None, repeated.Type, "no repite el mismo aviso cada sondeo");

            // Se aleja del hold-short (la situación termina) y vuelve a acercarse: avisa otra vez.
            advisor.Evaluate(new RaasAdvisor.Inputs { GroundSpeedKt = 8 }, t0.AddSeconds(10));
            var again = advisor.Evaluate(new RaasAdvisor.Inputs
            {
                GroundSpeedKt = 8, HoldShortRunway = "14R",
                HoldShortDistanceM = 120, HeadingTowardHoldShort = true
            }, t0.AddSeconds(60));
            Assert.AreEqual(RaasCalloutType.HoldShortApproaching, again.Type);
        }

        [TestMethod]
        public void HoldShort_ParallelToIt_DoesNotFire()
        {
            var advisor = new RaasAdvisor();
            var c = advisor.Evaluate(new RaasAdvisor.Inputs
            {
                GroundSpeedKt = 12,
                HoldShortRunway = "14R",
                HoldShortDistanceM = 30,          // cerca…
                HeadingTowardHoldShort = false    // …pero rodando en paralelo, no hacia él
            }, DateTime.UtcNow);
            Assert.AreEqual(RaasCalloutType.None, c.Type);
        }

        [TestMethod]
        public void HoldShort_TheOldAxisFilter_WouldHaveRejectedTheRealApproach()
        {
            // Causa raíz del aviso que nunca sonaba: el heading del hold-short es el de la pista
            // (136°) y el avión rodaba a 267–270°, así que el filtro de ±45° del eje descartaba
            // justo los hold-shorts sobre los que estaba pasando (5–22 m).
            double deltaAxis = GeoMath.BearingDiffDeg(136, 270);
            Assert.IsTrue(deltaAxis > 45.0, $"el filtro viejo lo descartaba (dio {deltaAxis:F0}°)");

            // Lo que sí discrimina: ir HACIA el punto. En el rodaje real, el hold-short estaba
            // delante (bearing al punto ~315°, rumbo 270° → 45° de diferencia).
            double bearingToPoint = 315.0, track = 270.0;
            Assert.IsTrue(GeoMath.BearingDiffDeg(bearingToPoint, track) <= 90.0);
        }

        [TestMethod]
        public void Advisor_GuidesTheTurnAndThenTheNextOne()
        {
            var advisor = new RaasAdvisor();
            var t = new DateTime(2026, 9, 24, 22, 0, 0, DateTimeKind.Utc);

            var ahead = advisor.Evaluate(new RaasAdvisor.Inputs
            {
                GroundSpeedKt = 12,
                ActiveTaxiway = "B",
                Guidance = new RouteGuidance
                {
                    HasRoute = true, OnRoute = true, NextTaxiway = "M",
                    DistanceToTurnM = 200, Side = TurnSide.Left
                }
            }, t);
            Assert.AreEqual(RaasCalloutType.TurnAhead, ahead.Type);
            Assert.AreEqual("M", ahead.Taxiway);
            Assert.AreEqual(TurnSide.Left, ahead.Side);

            var now = advisor.Evaluate(new RaasAdvisor.Inputs
            {
                GroundSpeedKt = 10,
                ActiveTaxiway = "B",
                Guidance = new RouteGuidance
                {
                    HasRoute = true, OnRoute = true, NextTaxiway = "M",
                    DistanceToTurnM = 40, Side = TurnSide.Left
                }
            }, t.AddSeconds(25));
            Assert.AreEqual(RaasCalloutType.TurnNow, now.Type,
                "al acercarse al cruce cambia de banda y vuelve a avisar");
        }

        [TestMethod]
        public void Advisor_OffRoute_And_RouteComplete()
        {
            var advisor = new RaasAdvisor();
            var t = DateTime.UtcNow;

            var off = advisor.Evaluate(new RaasAdvisor.Inputs
            {
                GroundSpeedKt = 10,
                ActiveTaxiway = "D",
                Guidance = new RouteGuidance { HasRoute = true, OnRoute = false, NextTaxiway = "B" }
            }, t);
            Assert.AreEqual(RaasCalloutType.OffRoute, off.Type);

            var done = advisor.Evaluate(new RaasAdvisor.Inputs
            {
                GroundSpeedKt = 10,
                ActiveTaxiway = "V",
                Guidance = new RouteGuidance { HasRoute = true, OnRoute = true, Done = true }
            }, t.AddSeconds(30));
            Assert.AreEqual(RaasCalloutType.RouteComplete, done.Type);
        }

        [TestMethod]
        public void Advisor_OnTheRunway_StaysSilent()
        {
            var advisor = new RaasAdvisor();
            var c = advisor.Evaluate(new RaasAdvisor.Inputs
            {
                OnRunway = true,
                GroundSpeedKt = 60,
                HoldShortRunway = "14R",
                HoldShortDistanceM = 10,
                HeadingTowardHoldShort = true,
                ActiveTaxiway = "V",
                Guidance = new RouteGuidance { HasRoute = true, OnRoute = true, Done = true }
            }, DateTime.UtcNow);
            Assert.AreEqual(RaasCalloutType.None, c.Type, "en pista manda el RAAS de pista, no este");
        }
    }
}