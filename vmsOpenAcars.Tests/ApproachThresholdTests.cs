using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Db;
using vmsOpenAcars.Models.NavData;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El test "¿está el avión en una final para esta pista?" — <c>SelectApproachThreshold</c>
    /// y el cono angular <c>IsWithinFinalCone</c>. Es lo que separa un desvío real de una
    /// alineación casual, así que se comprueba contra los datos reales de dos vuelos, no
    /// contra geometría inventada.
    ///
    /// <para><b>Caso 1 — falso desvío (SKTL).</b> PIREP <c>E7DK47e88XdabzoL</c>, vuelo SKRG→SKBQ
    /// del 20/09/2026, con un desvío simulado a SKCG. A las 15:12:47, descendiendo a ~17 000 ft
    /// en lat 9.22349, lon -75.43016, rumbo 348, el cliente anunció
    /// <i>"DESVÍO DETECTADO — aproximando a SKTL"</i>. El endpoint devolvió SKTL rwy 35 con
    /// <c>cross_track_nm</c> = 2.993 (el filtro de 3 NM de v0.9.1 lo dejó pasar por 0.007 NM,
    /// durante un solo sondeo: 5 s después ya era 3.06) y <c>hdg_diff</c> = 0.9° — SKTL
    /// simplemente comparte la alineación costera de la llegada.</para>
    ///
    /// <para><b>Caso 2 — desvío real (SKCG).</b> El mismo vuelo, 10 minutos después, a las
    /// 15:23:00 en lat 10.11381, lon -75.51447, rumbo 358: <i>"DESVÍO DETECTADO — aproximando a
    /// SKCG"</i>, que era correcto (el piloto sí iba a SKCG).</para>
    ///
    /// <para>Ambos matches están a ~19 NM del umbral, así que la distancia no los distingue;
    /// lo que los separa es el ángulo: 9.0° contra 2.1°.</para>
    ///
    /// <para><b>Datos reales de NavData (AIRAC 2609):</b> SKTL rwy 35 umbral
    /// 9.5037775,-75.5838089 / fin 9.5152272,-75.5879020, magnética 348.9 (verdadera 340.58);
    /// SKCG rwy 01 umbral 10.4308891,-75.5133667 / fin 10.4523649,-75.5124850, magnética 10.8
    /// (verdadera 2.31). La variación magnética local es de ~8.4°E.</para>
    /// </summary>
    [TestClass]
    public class ApproachThresholdTests
    {
        private const double MetersPerDegLat = 111320.0;
        private const double MetersPerNm     = 1852.0;

        // ── Datos reales de pista ────────────────────────────────────────────────

        private static List<NavRunway> SktlRunways() => new List<NavRunway>
        {
            new NavRunway
            {
                Name = "17", Heading = 168.9,
                ThresholdLat = 9.515277862548828, ThresholdLon = -75.5879135131836,
                EndLat = 9.503828216894354, EndLon = -75.58382034280582,
                LengthFt = 4429, WidthFt = 52, ElevationFt = 8,
            },
            new NavRunway
            {
                Name = "35", Heading = 348.9,
                ThresholdLat = 9.503777503967285, ThresholdLon = -75.58380889892578,
                EndLat = 9.515227172932203, EndLon = -75.58790200166118,
                LengthFt = 4429, WidthFt = 52, ElevationFt = 31,
            },
        };

        private static List<NavRunway> SkcgRunways() => new List<NavRunway>
        {
            new NavRunway
            {
                Name = "01", Heading = 10.8,
                ThresholdLat = 10.4308891296387, ThresholdLon = -75.5133666992188,
                EndLat = 10.4523648502728, EndLon = -75.5124850092804,
                LengthFt = 7841, WidthFt = 148, ElevationFt = 12, HasIls = true,
            },
            new NavRunway
            {
                Name = "19", Heading = 190.8,
                ThresholdLat = 10.4524745941162, ThresholdLon = -75.5124816894531,
                EndLat = 10.4309988710099, EndLon = -75.5133633174393,
                LengthFt = 7841, WidthFt = 148, ElevationFt = 8,
            },
        };

        // ── Helpers de geometría ─────────────────────────────────────────────────

        /// <summary>Bearing geográfico verdadero del eje de pista, igual que
        /// <c>NavDataService.TrueRunwayBearing</c>.</summary>
        private static double TrueBearing(NavRunway rwy)
        {
            double cosLat = Math.Cos(rwy.ThresholdLat * Math.PI / 180.0);
            double dN = (rwy.EndLat - rwy.ThresholdLat) * MetersPerDegLat;
            double dE = (rwy.EndLon - rwy.ThresholdLon) * MetersPerDegLat * cosLat;
            return (Math.Atan2(dE, dN) * 180.0 / Math.PI + 360.0) % 360.0;
        }

        /// <summary>
        /// Punto situado <paramref name="alongNm"/> NM por delante del umbral en el sentido del
        /// aterrizaje (negativo = antes del umbral, sobre la final) y <paramref name="crossNm"/>
        /// NM desplazado lateralmente. Se construye sobre el eje <b>verdadero</b> para que la
        /// geometría del test sea exacta y no arrastre la variación magnética.
        /// </summary>
        private static void OffsetFromThreshold(
            NavRunway rwy, double alongNm, double crossNm, out double lat, out double lon)
        {
            double rad = TrueBearing(rwy) * Math.PI / 180.0;
            double cos = Math.Cos(rwy.ThresholdLat * Math.PI / 180.0);
            double dNm = alongNm * MetersPerNm;
            double dCm = crossNm * MetersPerNm;

            double dN = dNm * Math.Cos(rad) + dCm * -Math.Sin(rad);
            double dE = dNm * Math.Sin(rad) + dCm *  Math.Cos(rad);

            lat = rwy.ThresholdLat + dN / MetersPerDegLat;
            lon = rwy.ThresholdLon + dE / (MetersPerDegLat * cos);
        }

        // ── Caso 1: el falso desvío real, en sus coordenadas exactas ─────────────

        /// <summary>
        /// El evento que reportó el piloto. Con la proyección sobre el eje verdadero el
        /// cross-track es 2.99 NM, por encima del 1% de tolerancia de 2 NM — y en ángulo,
        /// 9.0° contra los 4° del cono.
        /// </summary>
        [TestMethod]
        public void SktlFalseDiversion_FromRealFlight_IsRejected()
        {
            Assert.IsNull(
                NavDataService.SelectApproachThreshold(SktlRunways(), 9.22349, -75.43016, 348.0),
                "SKTL no puede resolverse como final: el avión pasa 19 NM al sur, fuera del eje");

            Assert.IsFalse(NavDataService.IsWithinFinalCone(2.993, 19.13),
                "2.99 NM de desviación a 19.13 NM del umbral son 9.0° — fuera del cono de 4°");
        }

        /// <summary>
        /// El sondeo siguiente, 5 s después (15:12:52). El filtro de 3 NM de v0.9.1 ya lo
        /// habría rechazado: el falso positivo vivió exactamente un sondeo.
        /// </summary>
        [TestMethod]
        public void SktlFalseDiversion_NextPoll_IsAlsoRejected()
        {
            Assert.IsNull(
                NavDataService.SelectApproachThreshold(SktlRunways(), 9.23272, -75.43225, 348.0));

            Assert.IsFalse(NavDataService.IsWithinFinalCone(3.06, 18.59));
        }

        // ── Caso 2: el desvío real a SKCG, en sus coordenadas exactas ────────────

        /// <summary>
        /// El desvío que el piloto sí estaba haciendo. Era un match legítimo y debe seguir
        /// resolviéndose: 0.70 NM del eje (2.1°), rumbo dentro de 15°, antes del umbral.
        ///
        /// Este test es el que falla si la proyección vuelve a usar el rumbo magnético de la
        /// pista (10.8° en vez de los 2.31° verdaderos): el error de eje de 8.4° a 19 NM son
        /// 2.8 NM, y el cross-track calculado salta a 3.51 NM — por encima de la tolerancia,
        /// dejando el desvío real sin detectar.
        /// </summary>
        [TestMethod]
        public void SkcgRealDiversion_FromRealFlight_IsAccepted()
        {
            RunwayTouchdownResult r =
                NavDataService.SelectApproachThreshold(SkcgRunways(), 10.11381, -75.51447, 358.0);

            Assert.IsNotNull(r, "el desvío real a SKCG debe resolverse como final");
            Assert.AreEqual("01", r.RunwayName);

            Assert.IsTrue(NavDataService.IsWithinFinalCone(0.703, 19.04),
                "0.70 NM a 19.04 NM del umbral son 2.1° — dentro del cono");
        }

        [TestMethod]
        public void SkcgRealDiversion_LaterPoll_IsAlsoAccepted()
        {
            RunwayTouchdownResult r =
                NavDataService.SelectApproachThreshold(SkcgRunways(), 10.14103, -75.51548, 358.0);

            Assert.IsNotNull(r);
            Assert.AreEqual("01", r.RunwayName);
            Assert.IsTrue(NavDataService.IsWithinFinalCone(0.577, 17.4));
        }

        // ── El eje de proyección debe ser el verdadero, no el magnético ─────────

        /// <summary>
        /// <c>ThresholdHeading</c> alimenta <c>ComputeApproachMetrics</c>, que proyecta: si se
        /// le pasa el rumbo magnético, la serie lateral del landing log arrastra el mismo error
        /// de variación (hasta ~600 ft con variación ≥ 13°).
        /// </summary>
        [TestMethod]
        public void ThresholdHeading_IsTheTrueBearing_NotTheMagneticOne()
        {
            RunwayTouchdownResult sktl =
                NavDataService.SelectApproachThreshold(SktlRunways(), 9.4, -75.56, 341.0);
            Assert.IsNotNull(sktl);
            Assert.AreEqual("35", sktl.RunwayName);
            Assert.AreEqual(340.58, sktl.ThresholdHeading, 0.1,
                "rwy 35 declara 348.9 magnética pero su eje geográfico es 340.58");
            Assert.AreNotEqual(348.9, sktl.ThresholdHeading, 1.0);

            RunwayTouchdownResult skcg =
                NavDataService.SelectApproachThreshold(SkcgRunways(), 10.30, -75.5143, 2.0);
            Assert.IsNotNull(skcg);
            Assert.AreEqual(2.31, skcg.ThresholdHeading, 0.1,
                "rwy 01 declara 10.8 magnética pero su eje geográfico es 2.31");
        }

        // ── El cono angular ─────────────────────────────────────────────────────

        [TestMethod]
        public void FinalCone_AllowsTheRealDiversionAndRejectsTheFalseOne()
        {
            // Real: 2.1° de 4° disponibles.
            Assert.IsTrue(NavDataService.IsWithinFinalCone(0.70, 19.0));
            // Falso: 9.0°, más del doble del límite.
            Assert.IsFalse(NavDataService.IsWithinFinalCone(2.99, 19.1));
        }

        [TestMethod]
        public void FinalCone_IsInclusiveAtTheLimit()
        {
            // A 10 NM, 4° son 0.6993 NM.
            double limit = 10.0 * Math.Tan(NavDataService.MaxFinalConeAngleDeg * Math.PI / 180.0);

            Assert.IsTrue(NavDataService.IsWithinFinalCone(limit, 10.0));
            Assert.IsFalse(NavDataService.IsWithinFinalCone(limit + 0.01, 10.0));
        }

        [TestMethod]
        public void FinalCone_HasAFloorNearTheThreshold()
        {
            // A 0.5 NM del umbral el cono daría 0.035 NM, absurdo: rige el suelo de 0.25 NM.
            Assert.IsTrue(NavDataService.IsWithinFinalCone(0.20, 0.5));
            Assert.IsFalse(NavDataService.IsWithinFinalCone(0.30, 0.5));
            Assert.AreEqual(0.25, NavDataService.FinalConeFloorNm);
        }

        [TestMethod]
        public void FinalCone_WithoutData_DoesNotBlock()
        {
            // Es un filtro *adicional*: sin datos no puede juzgar, y las tolerancias fija y
            // de umbral siguen aplicando.
            Assert.IsTrue(NavDataService.IsWithinFinalCone(null, 19.0));
            Assert.IsTrue(NavDataService.IsWithinFinalCone(2.99, null));
            Assert.IsTrue(NavDataService.IsWithinFinalCone(null, null));
        }

        // ── La definición de "en final" ──────────────────────────────────────────

        [TestMethod]
        public void OnFinal_FiveNmBeforeThreshold_ResolvesThatRunway()
        {
            NavRunway rwy35 = SktlRunways()[1];
            OffsetFromThreshold(rwy35, -5.0, 0.0, out double lat, out double lon);

            RunwayTouchdownResult r =
                NavDataService.SelectApproachThreshold(SktlRunways(), lat, lon, 341.0);

            Assert.IsNotNull(r);
            Assert.AreEqual("35", r.RunwayName);
            Assert.AreEqual(rwy35.ThresholdLat, r.ThresholdLat, 1e-9);
        }

        /// <summary>
        /// Margen cercano al umbral: 0.1 NM de desviación a 0.3 NM del umbral sí resuelve —
        /// estar ahí es estar aterrizando, no pasar de largo. Es el suelo del cono el que lo
        /// permite (a 0.3 NM el cono puro daría 0.02 NM).
        /// </summary>
        [TestMethod]
        public void JustBeforeTheThreshold_Resolves()
        {
            OffsetFromThreshold(SktlRunways()[1], -0.3, 0.1, out double lat, out double lon);

            RunwayTouchdownResult r =
                NavDataService.SelectApproachThreshold(SktlRunways(), lat, lon, 341.0);

            Assert.IsNotNull(r);
            Assert.AreEqual("35", r.RunwayName);
            Assert.IsTrue(NavDataService.IsWithinFinalCone(0.1, 0.3));
        }

        [TestMethod]
        public void PastThreshold_EvenOneNmLater_IsRejected()
        {
            // El avión acaba de cruzar el umbral volando en el sentido del aterrizaje: ya no
            // puede estar aproximando a esa pista.
            OffsetFromThreshold(SktlRunways()[1], 1.0, 0.0, out double lat, out double lon);

            Assert.IsNull(NavDataService.SelectApproachThreshold(SktlRunways(), lat, lon, 341.0));
        }

        [TestMethod]
        public void OnCenterlineButTooFarLateral_IsRejected()
        {
            // 3 NM de desviación lateral: el doble del ancho de cualquier pista de este
            // tamaño (SKTL: 52 ft).
            OffsetFromThreshold(SktlRunways()[1], -5.0, 3.0, out double lat, out double lon);

            Assert.IsNull(NavDataService.SelectApproachThreshold(SktlRunways(), lat, lon, 341.0));
        }

        [TestMethod]
        public void OnCenterlineButHeadingOffByMoreThan15Deg_IsRejected()
        {
            OffsetFromThreshold(SktlRunways()[1], -5.0, 0.0, out double lat, out double lon);

            // 340.58 ± 15 es el límite contra la magnética 348.9: 20° ya no es una final.
            Assert.IsNull(NavDataService.SelectApproachThreshold(SktlRunways(), lat, lon, 8.9));
        }

        [TestMethod]
        public void ApproachingTheReciprocalRunway_ResolvesRunway17()
        {
            NavRunway rwy17 = SktlRunways()[0];
            OffsetFromThreshold(rwy17, -5.0, 0.0, out double lat, out double lon);

            RunwayTouchdownResult r =
                NavDataService.SelectApproachThreshold(SktlRunways(), lat, lon, 169.0);

            Assert.IsNotNull(r);
            Assert.AreEqual("17", r.RunwayName);
        }

        /// <summary>Pista 04R de KBOS, la que el endpoint eligió en el vuelo real.</summary>
        private static List<NavRunway> KbosRwy04R() => new List<NavRunway>
        {
            new NavRunway
            {
                Name = "04R", Heading = 33.4,
                ThresholdLat = 42.35105895996094, ThresholdLon = -71.01179504394531,
                EndLat = 42.3768857171165, EndLon = -70.99929814791402,
                LengthFt = 10006, WidthFt = 150, ElevationFt = 20, HasIls = true,
            },
        };

        // ── Caso 3: tres falsos desvíos en la aproximación a KBOS ────────────────

        /// <summary>
        /// PIREP del 22-23/09/2026 (SKCG→KBOS, A320, v0.9.2). Durante el descenso y el
        /// viraje de encaje a la final de Logan, el cliente anunció tres desvíos seguidos —
        /// a **28M** (Cranland) a las 03:44:33, a **1B9** (Mansfield) a las 03:45:38 y a
        /// **KOWD** (Norwood) a las 03:46:09 — antes de revertir a KBOS a las 03:47:49.
        ///
        /// Mecanismo distinto al de SKTL: aquí el objetivo real **estaba dentro del radio de
        /// 20 NM** (a 6.5 NM en el primer evento), pero durante el viraje no tenía ninguna
        /// pista a menos de 15° del rumbo, así que el endpoint devolvía el aeródromo pequeño
        /// mejor alineado. Los tres `cross_track_nm` (1.244, 2.993, 1.203) están por debajo
        /// del corte de 3 NM de v0.9.1: los tres pasaban el filtro viejo.
        ///
        /// Los números son los que devolvió el endpoint en vivo con esas coordenadas.
        /// </summary>
        [TestMethod]
        public void Boston_28M_FirstFalseDiversion_IsRejected()
        {
            // 03:44:33 — el avión a 6.5 NM de KBOS, el "alterno" a 15.1 NM.
            Assert.IsFalse(NavDataService.IsWithinFinalCone(1.244, 14.99),
                "1.24 NM del eje a 14.99 NM del umbral son 4.7° — fuera del cono de 4°");
            Assert.IsFalse(NavDataService.IsPlausibleDiversionDistance(15.13, 6.49),
                "no se desvía uno a un aeropuerto 2.3 veces más lejos que el destino");
        }

        [TestMethod]
        public void Boston_1B9_SecondFalseDiversion_IsRejected()
        {
            // 03:45:38 — 1B9 rwy 22, durante el viraje.
            Assert.IsFalse(NavDataService.IsWithinFinalCone(2.993, 14.48),
                "11.7° del eje — el corte lateral fijo de 3 NM lo dejaba pasar por 0.007 NM");
            Assert.IsFalse(NavDataService.IsPlausibleDiversionDistance(14.64, 10.40));
        }

        /// <summary>
        /// 03:46:09 — KOWD. Este es el caso que **sólo** el cono detiene: Norwood estaba a
        /// 6.58 NM, más cerca que KBOS a 11.35 NM, así que la regla de distancia lo permite.
        /// Sirve de recordatorio de que las dos reglas cubren cosas distintas.
        /// </summary>
        [TestMethod]
        public void Boston_KOWD_ThirdFalseDiversion_IsStoppedByTheConeOnly()
        {
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDistance(6.58, 11.35),
                "Norwood estaba más cerca que Logan: la distancia no lo descarta");
            Assert.IsFalse(NavDataService.IsWithinFinalCone(1.203, 6.19),
                "11.0° del eje — lo detiene el cono");
        }

        /// <summary>
        /// La reversión real (03:47:49): la final correcta, a 8.29 NM y perfectamente alineada
        /// (0.002 NM del eje). Debe resolverse, y la app la habría tenido resuelta desde el
        /// sondeo de 03:47:16 — 33 s antes de la reversión que se observó en el log.
        /// </summary>
        [TestMethod]
        public void Boston_RealRunway04R_ResolvesFromFarOut()
        {
            RunwayTouchdownResult atRevert =
                NavDataService.SelectApproachThreshold(KbosRwy04R(), 42.22106, -71.07448, 26.0);
            Assert.IsNotNull(atRevert);
            Assert.AreEqual("04R", atRevert.RunwayName);

            RunwayTouchdownResult earlier =
                NavDataService.SelectApproachThreshold(KbosRwy04R(), 42.20242, -71.08181, 26.0);
            Assert.IsNotNull(earlier, "la final de 04R ya era válida a 9.45 NM");
            Assert.AreEqual("04R", earlier.RunwayName);

            Assert.IsTrue(NavDataService.IsWithinFinalCone(0.002, 8.29));
            Assert.IsTrue(NavDataService.IsWithinFinalCone(0.071, 9.45));
        }

        /// <summary>
        /// Y en el momento de los falsos positivos Logan **no** tenía final válida desde esa
        /// posición: el avión ya había pasado el umbral de la 22L (la única alineada con su
        /// rumbo). Por eso el endpoint proponía otros aeródromos, y por eso el arreglo correcto
        /// es rechazar esos matches, no "forzar" KBOS.
        /// </summary>
        [TestMethod]
        public void Boston_NoValidLoganFinalAtTheFalseDiversionPosition()
        {
            var rwy22L = new NavRunway
            {
                Name = "22L", Heading = 213.4,
                ThresholdLat = 42.37689971923828, ThresholdLon = -70.99929809570312,
                EndLat = 42.35107257243093, EndLon = -71.01179351936358,
                LengthFt = 10006, WidthFt = 150,
            };

            Assert.IsNull(
                NavDataService.SelectApproachThreshold(
                    new List<NavRunway> { rwy22L }, 42.26117, -70.95714, 193.0),
                "el avión estaba pasado el umbral de la 22L: no hay final de Logan en ese punto");
        }

        // ── El filtro vertical: no se aterriza desde 17 000 ft a 19 NM ───────────

        /// <summary>
        /// La comprobación que pidió el piloto: el AGL contra el aeródromo falso. La serie
        /// completa del falso SKTL, con las altitudes reales del log y la elevación real de
        /// SKTL (31 ft). El gradiente <i>empeora</i> al acercarse, porque el avión descendía
        /// pasando de largo junto a un campo en el que nunca iba a aterrizar.
        /// </summary>
        [TestMethod]
        public void SktlFalseDiversion_FailsTheDescentGradient_AtEveryPoll()
        {
            // (altitud MSL del log, distancia al umbral del endpoint)
            double[][] polls =
            {
                new[] { 16881.0, 18.59 },   // 906 ft/NM = 8.5°
                new[] { 16068.0, 15.67 },   // 1023 ft/NM
                new[] { 14541.0, 10.19 },   // 1424 ft/NM
                new[] { 13037.0,  5.96 },   // 2182 ft/NM = 19.8°
                new[] { 12307.0,  5.52 },   // 2224 ft/NM = 20.1°
                new[] { 10926.0,  8.48 },   // 1285 ft/NM
            };

            foreach (double[] p in polls)
            {
                Assert.IsFalse(
                    NavDataService.IsPlausibleDiversionDescent(p[0], 31.0, p[1]),
                    $"SKTL a {p[0]:F0} ft MSL y {p[1]:F2} NM no es una aproximación");
            }
        }

        /// <summary>
        /// Y los legítimos pasan con holgura: 2.4° el desvío real a SKCG, 2.9-3.3° la final de
        /// Logan. Son sendas de planeo de manual, no casualidades.
        /// </summary>
        [TestMethod]
        public void RealApproaches_PassTheDescentGradient()
        {
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(4959, 12, 19.04), "SKCG 260 ft/NM = 2.4°");
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(2949, 20, 9.45),  "KBOS 04R 310 ft/NM = 2.9°");
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(2900, 20, 8.29),  "KBOS 04R 347 ft/NM = 3.3°");
        }

        /// <summary>
        /// El filtro vertical <b>no</b> habría cazado los tres falsos de Boston: su perfil de
        /// descenso era perfectamente normal (2.8°–5.8°). A esos los paran el cono angular y
        /// la regla de distancia. Es importante que el test lo diga, para que nadie crea que
        /// un filtro sustituye a los otros.
        /// </summary>
        [TestMethod]
        public void BostonFalseDiversions_HadNormalDescentProfiles()
        {
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(5777, 71, 14.99),  "28M  381 ft/NM = 3.6°");
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(4395, 123, 14.48), "1B9  295 ft/NM = 2.8°");
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(3900, 49, 6.19),   "KOWD 622 ft/NM = 5.8°");
        }

        /// <summary>
        /// El AGL se mide contra la elevación del aeródromo <b>emparejado</b>, no la del destino
        /// planeado: así un destino en altiplano no enmascara un alterno a nivel del mar (ni al
        /// revés). El mismo avión a 17 000 ft es absurdo sobre SKTL (31 ft) pero razonable sobre
        /// un campo a 15 000 ft.
        /// </summary>
        [TestMethod]
        public void DescentGradient_IsMeasuredAgainstTheMatchedFieldElevation()
        {
            Assert.IsFalse(NavDataService.IsPlausibleDiversionDescent(17000, 31, 19.0));
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(17000, 15000, 19.0));
        }

        [TestMethod]
        public void DescentGradient_BoundaryAndFloor()
        {
            // 700 ft/NM exactos pasan; un pelo más no.
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(7000, 0, 10.0));
            Assert.IsFalse(NavDataService.IsPlausibleDiversionDescent(7001, 0, 10.0));
            Assert.AreEqual(700.0, NavDataService.MaxDiversionGradientFtPerNm);

            // A menos de 0.5 NM el gradiente degenera (se está sobre la pista): no bloquea.
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(3000, 0, 0.3));

            // Sin datos no bloquea: es un filtro adicional.
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(null, 0, 10.0));
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(17000, null, 10.0));
            Assert.IsTrue(NavDataService.IsPlausibleDiversionDescent(17000, 0, null));
        }

        // ── Robustez ─────────────────────────────────────────────────────────────

        [TestMethod]
        public void NoRunways_ReturnsNull()
        {
            Assert.IsNull(NavDataService.SelectApproachThreshold(new List<NavRunway>(), 9.5, -75.5, 349.0));
            Assert.IsNull(NavDataService.SelectApproachThreshold(null, 9.5, -75.5, 349.0));
        }

        [TestMethod]
        public void ParallelRunways_PicksTheOneWhoseCenterlineIsClosest()
        {
            // Dos pistas paralelas separadas 0.5 NM: el avión está sobre el eje de la "35",
            // así que debe elegir esa y no la primera de la lista.
            NavRunway rwy35 = SktlRunways()[1];
            OffsetFromThreshold(rwy35, -5.0, 0.0, out double lat, out double lon);
            OffsetFromThreshold(rwy35, 0.0, 0.5, out double twinLat, out double twinLon);

            var runways = new List<NavRunway>(SktlRunways())
            {
                new NavRunway
                {
                    Name = "35L", Heading = 348.9,
                    ThresholdLat = twinLat, ThresholdLon = twinLon,
                    LengthFt = 4429, WidthFt = 52,
                },
            };
            // La gemela va primero: una implementación que se quedara con la primera
            // candidata fallaría aquí.
            runways.Reverse();

            RunwayTouchdownResult r =
                NavDataService.SelectApproachThreshold(runways, lat, lon, 341.0);

            Assert.IsNotNull(r);
            Assert.AreEqual("35", r.RunwayName, "debe preferir el eje más cercano");
        }
    }
}
