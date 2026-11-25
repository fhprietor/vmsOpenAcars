using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Las dos bandas de tolerancia de la compuerta, con las cifras del PIREP real
    /// `YxzZxO92Qnbxlqla`** (B738 SSW, MSFS 2024, MPTO→SKCG, v0.9.79).
    ///
    /// Los dos avisos del vuelo saltaron por resolución de instrumento, no por una violación:
    /// `03:38:53 ⚠️ BELOW MINIMUMS: 206 ft MSL < DA 207 ft (threshold 7 ft + 200)` —**1 ft**— y
    /// `03:24:14 ⚠️ APPROACH GATE: Excessive descent -1007 fpm (−5)` —**7 fpm** sobre el umbral
    /// nominal de −1.000, que costaron **5 puntos**—. Aquí se fijan los dos umbrales efectivos
    /// (**192 ft** y **−1.050 fpm**) y los bordes: lo que queda justo en la banda **no** se marca,
    /// porque el criterio es «más de» la tolerancia.
    /// </summary>
    [TestClass]
    public class ApproachToleranceTests
    {
        // ══ Mínimos ═════════════════════════════════════════════════════════════

        /// <summary>DA real del PIREP: umbral de MPTO (7 ft) + 200 ft CAT I = **207 ft MSL**.</summary>
        private const double DaFt = 7.0 + DecisionAltitude.CatIAboveThresholdFt;

        [TestMethod]
        public void Los206FtContraLaDaDe207_NoAvisan()
        {
            Assert.AreEqual(207.0, DaFt, 1e-9,
                "la DA del caso real es 7 ft de umbral + 200 ft CAT I");
            Assert.AreEqual(192.0, MinimumsTolerance.EffectiveDaFt(DaFt), 1e-9,
                "el umbral efectivo es DA − 15 ft = 192 ft");

            Assert.IsFalse(MinimumsTolerance.IsBelowMinimums(206.0, DaFt),
                "206 ft contra 207 ft es 1 ft: el escalón de un altímetro de ~10 ft, no una bajada");
        }

        [TestMethod]
        public void UnaBajadaDeVerdad_SiAvisa()
        {
            // 185 ft son 22 ft por debajo de la DA: más que la banda y más que dos pasos del
            // altímetro. Es el caso que el criterio tiene que seguir cobrando.
            Assert.IsTrue(MinimumsTolerance.IsBelowMinimums(185.0, DaFt),
                "22 ft por debajo de la DA es una bajada real, no ruido");
        }

        [TestMethod]
        public void ElBordeDe15Ft_NoCuenta()
        {
            // 192 ft es exactamente DA − 15: el criterio pide MÁS de 15 ft, así que el borde no
            // dispara. Un pelo más abajo sí.
            Assert.IsFalse(MinimumsTolerance.IsBelowMinimums(192.0, DaFt),
                "exactamente 15 ft por debajo no es «más de 15 ft»");
            Assert.IsTrue(MinimumsTolerance.IsBelowMinimums(191.9, DaFt),
                "pasado el borde de 15 ft ya es una bajada que marcar");
        }

        // ══ Descenso excesivo ═══════════════════════════════════════════════════

        [TestMethod]
        public void Los1007FpmDelPirep_NoCuentan()
        {
            Assert.AreEqual(-1000, ExcessiveDescentTolerance.ThresholdFpm,
                "el umbral nominal histórico no se mueve");
            Assert.AreEqual(-1050, ExcessiveDescentTolerance.EffectiveThresholdFpm,
                "el umbral efectivo es −1.000 − 50 = −1.050 fpm");

            Assert.IsFalse(ExcessiveDescentTolerance.IsExcessive(-1007),
                "−1.007 fpm son 7 sobre el nominal: resolución de la tasa vertical, no inestabilidad");
        }

        [TestMethod]
        public void UnDescensoDe1200Fpm_SiCuenta()
        {
            Assert.IsTrue(ExcessiveDescentTolerance.IsExcessive(-1200),
                "−1.200 fpm sigue siendo un descenso excesivo y sigue costando sus 5 puntos");
        }

        [TestMethod]
        public void ElBordeDe50Fpm_NoCuenta()
        {
            Assert.IsFalse(ExcessiveDescentTolerance.IsExcessive(-1050),
                "exactamente −1.050 fpm es el borde: el criterio pide MÁS de 50 fpm");
            Assert.IsTrue(ExcessiveDescentTolerance.IsExcessive(-1051),
                "un fpm pasado el borde ya es excesivo");
        }
    }
}
