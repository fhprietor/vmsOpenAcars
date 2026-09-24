using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Geometría flat-earth compartida.
    ///
    /// Estas fórmulas estaban triplicadas en el código (mapa, espacios aéreos y carta de
    /// aproximación). Consolidarlas no basta: si la fórmula se equivoca, los tres módulos
    /// se equivocan juntos y en el mismo sentido, que es precisamente el fallo que no se
    /// detecta a ojo. De ahí que se comprueben contra valores calculados a mano.
    /// </summary>
    [TestClass]
    public class GeoMathTests
    {
        // ── CosLat: la guarda que evita el desbordamiento ────────────────────────

        [TestMethod]
        public void CosLat_IsCosineAtNormalLatitudes()
        {
            Assert.AreEqual(1.0, GeoMath.CosLat(0.0), 1e-9);
            Assert.AreEqual(Math.Cos(60 * Math.PI / 180), GeoMath.CosLat(60.0), 1e-9);
        }

        [TestMethod]
        public void CosLat_IsClampedAtThePoles_SoProjectionDoesNotExplode()
        {
            // Sin la guarda, dividir por cos(90°)≈0 producía incrementos de longitud
            // desmesurados.
            double atPole = GeoMath.CosLat(90.0);
            Assert.IsTrue(atPole > 0, "el coseno acotado debe ser estrictamente positivo");
            Assert.AreEqual(1e-6, atPole, 1e-12);
        }

        // ── Project: desplazamiento por rumbo y distancia ────────────────────────

        [TestMethod]
        public void Project_Northward_IncreasesLatitudeByTheProjectedDegrees()
        {
            // 60 NM al norte NO es exactamente 1°: la constante de la proyección
            // (111320 m/grado) equivale a ~60.108 NM por grado, así que 60 NM son
            // 60/60.108 ≈ 0.9982°. Se comprueba contra el valor derivado, no contra una
            // regla de tres mental.
            GeoMath.Project(4.0, -74.0, 0.0, 60.0, out double lat, out double lon);

            double expectedDeg = 60.0 * GeoMath.MetersPerNm / GeoMath.MetersPerDegLat;
            Assert.AreEqual(4.0 + expectedDeg, lat, 1e-6);
            Assert.AreEqual(-74.0, lon, 1e-9, "un rumbo norte puro no cambia la longitud");
        }

        [TestMethod]
        public void Project_Eastward_ScalesLongitudeByCosLat()
        {
            // A 60° N un grado de longitud mide la mitad que uno de latitud, así que las
            // mismas 60 NM son ~2° de longitud en lugar de ~1°.
            GeoMath.Project(60.0, 10.0, 90.0, 60.0, out double lat, out double lon);

            Assert.AreEqual(60.0, lat, 1e-9, "un rumbo este puro no cambia la latitud");
            double expectedDeg = 60.0 * GeoMath.MetersPerNm
                                 / (GeoMath.MetersPerDegLat * GeoMath.CosLat(60.0));
            Assert.AreEqual(10.0 + expectedDeg, lon, 1e-6);
        }

        [TestMethod]
        public void Project_ZeroDistance_ReturnsTheSamePoint()
        {
            GeoMath.Project(4.7011, -74.1469, 123.0, 0.0, out double lat, out double lon);

            Assert.AreEqual(4.7011, lat, 1e-12);
            Assert.AreEqual(-74.1469, lon, 1e-12);
        }

        [TestMethod]
        public void Project_IsReversible_ByOppositeBearing()
        {
            // Proyectar 10 NM y volver por el rumbo recíproco devuelve el origen.
            // Tolerancia en grados: el error de redondeo de la proyección flat-earth es
            // del orden de 1e-5°, no de 1e-6.
            GeoMath.Project(4.0, -74.0, 45.0, 10.0, out double lat, out double lon);
            GeoMath.Project(lat, lon, 225.0, 10.0, out double backLat, out double backLon);

            Assert.AreEqual(4.0, backLat, 1e-4);
            Assert.AreEqual(-74.0, backLon, 1e-4);
        }

        // ── DistanceNm / DistanceKm ─────────────────────────────────────────────

        [TestMethod]
        public void DistanceNm_OneDegreeOfLatitude_IsTheDerivedValue()
        {
            // 1° de latitud son 111320 m ≈ 60.108 NM con la constante de esta clase.
            // El valor "60 NM por grado" es una aproximación marinera, no la de flat-earth.
            double expected = GeoMath.MetersPerDegLat / GeoMath.MetersPerNm;
            Assert.AreEqual(expected, GeoMath.DistanceNm(4.0, -74.0, 5.0, -74.0), 1e-6);
        }

        [TestMethod]
        public void DistanceNm_OneDegreeOfLongitudeAtEquator_MatchesLatitudeScale()
        {
            // En el ecuador cos(0)=1, así que un grado de longitud debe medir lo mismo que
            // uno de latitud.
            double atEquator = GeoMath.DistanceNm(0.0, 0.0, 0.0, 1.0);
            double latitude   = GeoMath.DistanceNm(0.0, 0.0, 1.0, 0.0);
            Assert.AreEqual(latitude, atEquator, 1e-6);
        }

        [TestMethod]
        public void DistanceNm_IsSymmetric()
        {
            double ab = GeoMath.DistanceNm(4.0, -74.0, 6.17, -75.42);
            double ba = GeoMath.DistanceNm(6.17, -75.42, 4.0, -74.0);
            Assert.AreEqual(ab, ba, 0.5, "la distancia no debe depender del orden");
        }

        [TestMethod]
        public void DistanceKm_MatchesDistanceNmConverted()
        {
            double nm = GeoMath.DistanceNm(4.0, -74.0, 5.0, -74.0);
            double km = GeoMath.DistanceKm(4.0, -74.0, 5.0, -74.0);
            Assert.AreEqual(nm * 1.852, km, 0.05);
        }

        [TestMethod]
        public void DistanceNm_SamePoint_IsZero()
        {
            Assert.AreEqual(0.0, GeoMath.DistanceNm(4.7, -74.1, 4.7, -74.1), 1e-9);
        }

        // ── BearingDeg ──────────────────────────────────────────────────────────

        // Origen fijo en (0,0); solo varía el destino. Firma:
        //   BearingDeg(lat1, lon1, lat2, lon2)  ->  rumbo de (lat1,lon1) a (lat2,lon2)
        [DataTestMethod]
        [DataRow( 1.0,  0.0,   0.0, "norte")]
        [DataRow( 0.0,  1.0,  90.0, "este")]
        [DataRow(-1.0,  0.0, 180.0, "sur")]
        [DataRow( 0.0, -1.0, 270.0, "oeste")]
        public void BearingDeg_CardinalDirections(double destLat, double destLon, double expected, string because)
        {
            double b = GeoMath.BearingDeg(0.0, 0.0, destLat, destLon);
            Assert.AreEqual(expected, b, 0.5, because);
        }

        [TestMethod]
        public void BearingDeg_MatchesProjectionRoundTrip()
        {
            // Coherencia interna: proyectar en un rumbo y medir el rumbo al punto
            // proyectado debe devolver el mismo valor.
            foreach (double bearing in new[] { 0.0, 45.0, 120.0, 200.0, 315.0 })
            {
                GeoMath.Project(4.0, -74.0, bearing, 25.0, out double lat, out double lon);
                double measured = GeoMath.BearingDeg(4.0, -74.0, lat, lon);
                Assert.AreEqual(bearing, measured, 0.5, $"rumbo {bearing}° no se recupera");
            }
        }

        [TestMethod]
        public void BearingDeg_IdenticalPoints_IsZeroInsteadOfNaN()
        {
            double b = GeoMath.BearingDeg(4.0, -74.0, 4.0, -74.0);
            Assert.AreEqual(0.0, b, 1e-9);
            Assert.IsFalse(double.IsNaN(b));
        }

        // ── BearingDiffDeg ──────────────────────────────────────────────────────

        [DataTestMethod]
        [DataRow(350.0, 10.0,  20.0, "cruce del norte")]
        [DataRow(10.0, 350.0,  20.0, "cruce del norte, orden inverso")]
        [DataRow(0.0,  180.0, 180.0, "opuestos")]
        [DataRow(90.0, 90.0,    0.0, "iguales")]
        [DataRow(0.0,  90.0,   90.0, "perpendiculares")]
        public void BearingDiffDeg_IsAlwaysTheShortestArc(double a, double b, double expected, string because)
        {
            Assert.AreEqual(expected, GeoMath.BearingDiffDeg(a, b), 1e-9, because);
        }

        [TestMethod]
        public void BearingDiffDeg_NeverExceeds180()
        {
            var rng = new Random(7);
            for (int i = 0; i < 500; i++)
            {
                double d = GeoMath.BearingDiffDeg(rng.NextDouble() * 720 - 180,
                                                  rng.NextDouble() * 720 - 180);
                Assert.IsTrue(d >= 0 && d <= 180, $"diferencia fuera de rango: {d}");
            }
        }

        // ── ToMeters ────────────────────────────────────────────────────────────

        [TestMethod]
        public void ToMeters_NorthwardDeltaYieldsNorthComponentOnly()
        {
            GeoMath.ToMeters(4.0, -74.0, 5.0, -74.0, out double n, out double e);
            Assert.AreEqual(111320.0, n, 1.0);
            Assert.AreEqual(0.0, e, 1e-6);
        }

        [TestMethod]
        public void ToMeters_EastwardDeltaIsScaledByCosLat()
        {
            GeoMath.ToMeters(60.0, 10.0, 60.0, 11.0, out double n, out double e);
            Assert.AreEqual(0.0, n, 1e-6);
            Assert.AreEqual(111320.0 * Math.Cos(60 * Math.PI / 180), e, 1.0);
        }
    }

    /// <summary>
    /// Respaldo regional de altitud/nivel de transición.
    ///
    /// Antes, cuando NavData no publicaba TA/TL, el cliente no hacía nada: sin aviso de
    /// TA/TL, sin comprobación de 1013 en subida y sin gate de QNH por TL. El respaldo
    /// cubre ese hueco, pero debe ser explícito cuando no sabe (no inventar valores) y
    /// mantener el orden TA &lt; TL, que es lo que hace coherentes los avisos.
    /// </summary>
    [TestClass]
    public class TransitionDefaultsTests
    {
        private static NavAirportInfo Info(string iso) => new NavAirportInfo { IsoCountry = iso };

        [DataTestMethod]
        [DataRow("US", 18000)]
        [DataRow("CA", 18000)]
        [DataRow("CO", 18000)]
        [DataRow("GB", 5000)]
        [DataRow("DE", 5000)]
        [DataRow("FR", 5000)]
        [DataRow("AU", 10000)]
        [DataRow("JP", 14000)]
        public void GetTransitionAltitudeFt_KnownCountries(string iso, double expected)
        {
            Assert.AreEqual(expected, TransitionDefaults.GetTransitionAltitudeFt(Info(iso)), 1e-9);
        }

        [TestMethod]
        public void GetTransitionAltitudeFt_IsCaseInsensitive()
        {
            Assert.AreEqual(TransitionDefaults.GetTransitionAltitudeFt(Info("us")),
                            TransitionDefaults.GetTransitionAltitudeFt(Info("US")));
        }

        [TestMethod]
        public void GetTransitionAltitudeFt_AcceptsThreeLetterCodes()
        {
            // El campo puede venir como "USA"; se usan los dos primeros caracteres.
            Assert.AreEqual(18000, TransitionDefaults.GetTransitionAltitudeFt(Info("USA")), 1e-9);
        }

        [TestMethod]
        public void GetTransitionAltitudeFt_UnknownCountry_ReturnsZero()
        {
            Assert.AreEqual(0, TransitionDefaults.GetTransitionAltitudeFt(Info("ZZ")), 1e-9,
                "con un país desconocido es mejor no inventar un valor");
        }

        [TestMethod]
        public void GetTransitionAltitudeFt_MissingOrEmptyCountry_ReturnsZero()
        {
            Assert.AreEqual(0, TransitionDefaults.GetTransitionAltitudeFt(null), 1e-9);
            Assert.AreEqual(0, TransitionDefaults.GetTransitionAltitudeFt(Info(null)), 1e-9);
            Assert.AreEqual(0, TransitionDefaults.GetTransitionAltitudeFt(Info("")), 1e-9);
            Assert.AreEqual(0, TransitionDefaults.GetTransitionAltitudeFt(Info("   ")), 1e-9);
        }

        [TestMethod]
        public void GetTransitionLevelFt_IsAlwaysAboveTheTransitionAltitude()
        {
            // TA < TL es lo que hace coherente la secuencia de avisos: primero "SET STD"
            // al subir, luego "SET QNH" al bajar.
            foreach (string iso in new[] { "US", "CO", "GB", "DE", "AU", "JP", "BR", "AR" })
            {
                double ta = TransitionDefaults.GetTransitionAltitudeFt(Info(iso));
                double tl = TransitionDefaults.GetTransitionLevelFt(Info(iso));
                Assert.IsTrue(tl > ta, $"{iso}: TL ({tl}) debe superar la TA ({ta})");
                Assert.AreEqual(ta + 1000.0, tl, 1e-9);
            }
        }

        [TestMethod]
        public void GetTransitionLevelFt_UnknownCountry_IsZero()
        {
            Assert.AreEqual(0, TransitionDefaults.GetTransitionLevelFt(Info("ZZ")), 1e-9);
            Assert.AreEqual(0, TransitionDefaults.GetTransitionLevelFt(null), 1e-9);
        }

        // ── DistanceToSegmentNm: la recta recortada, no la infinita ─────────────

        /// <summary>
        /// La diferencia con <c>Project</c> es el recorte a [0,1]: un punto más allá del final
        /// del tramo se mide contra el extremo, no contra la recta prolongada. Es lo que hace
        /// falta para medir desviación de traza (un avión pasado el destino no está "sobre la
        /// ruta"), y es justo lo que no se puede obtener proyectando sobre la recta infinita.
        /// </summary>
        [TestMethod]
        public void DistanceToSegmentNm_ClampsToTheEndpoints()
        {
            // Tramo de 1° de latitud sobre el meridiano 0 (≈60 NM).
            const double lat1 = 0.0, lon1 = 0.0;
            const double lat2 = 1.0, lon2 = 0.0;

            // Sobre el tramo (punto medio y extremos).
            Assert.AreEqual(0.0, GeoMath.DistanceToSegmentNm(0.5, 0.0, lat1, lon1, lat2, lon2), 0.01);
            Assert.AreEqual(0.0, GeoMath.DistanceToSegmentNm(0.0, 0.0, lat1, lon1, lat2, lon2), 0.01);
            Assert.AreEqual(0.0, GeoMath.DistanceToSegmentNm(1.0, 0.0, lat1, lon1, lat2, lon2), 0.01);

            // Perpendicular por el punto medio: 0.1° de longitud en el ecuador ≈ 6 NM.
            Assert.AreEqual(6.0, GeoMath.DistanceToSegmentNm(0.5, 0.1, lat1, lon1, lat2, lon2), 0.1);

            // Más allá del extremo final: la distancia es la del extremo, no la de la recta
            // (que seguiría siendo 0 porque el punto está sobre el meridiano).
            double beyondNm = GeoMath.DistanceToSegmentNm(1.5, 0.0, lat1, lon1, lat2, lon2);
            Assert.AreEqual(0.5 * GeoMath.MetersPerDegLat / GeoMath.MetersPerNm, beyondNm, 0.1);
            Assert.IsTrue(beyondNm > 0.0, "un punto pasado el final NO está sobre la traza");

            // Y antes del extremo inicial, igual.
            Assert.AreEqual(beyondNm, GeoMath.DistanceToSegmentNm(-0.5, 0.0, lat1, lon1, lat2, lon2), 0.1);
        }

        [TestMethod]
        public void DistanceToSegmentNm_DegenerateSegment_IsDistanceToThePoint()
        {
            // Un tramo de longitud cero (dos fixes en el mismo sitio) no debe dividir por cero.
            double d = GeoMath.DistanceToSegmentNm(0.0, 0.1, 0.0, 0.0, 0.0, 0.0);
            Assert.AreEqual(6.0, d, 0.1);
        }
    }
}
