using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// La DA de la aproximación ILS, con el caso real del PIREP `l5QVxBEmJ4yOaY2O` (B738 SSW,
    /// MSFS 2024, SKRG→MPTO): umbral a **134 ft** y TCH **50 ft**. La 0.9.78 usaba el TCH como DA y
    /// escribía `⚠️ BELOW MINIMUMS: 49 ft MSL < DA 50 ft`, una comparación entre una altitud MSL y
    /// una altura sobre el umbral en un campo cuya elevación es 134 ft: a 49 ft MSL el avión estaría
    /// bajo tierra. La DA CAT I es **134 + 200 = 334 ft MSL**.
    /// </summary>
    [TestClass]
    public class DecisionAltitudeTests
    {
        [TestMethod]
        public void ElTchNoEsLaDa()
        {
            // 50 ft es el TCH; la DA del umbral de 134 ft es 334 ft MSL.
            double? da = DecisionAltitude.FromThresholdElevation(134.0);

            Assert.IsTrue(da.HasValue);
            Assert.AreEqual(334.0, da.Value, 1e-9, "DA = elevación del umbral + 200 ft (CAT I)");
            Assert.AreNotEqual(50.0, da.Value, "el TCH (50 ft) no puede ser la altitud de decisión");
        }

        [TestMethod]
        public void SinElevacionDelUmbral_NoHayDa()
        {
            // Sin dato no se compara: no marcar y no penalizar. Un 0 inventado convertiría cada
            // aproximación a un aeródromo sin elevación publicada en un «bajo mínimos».
            Assert.IsNull(DecisionAltitude.FromThresholdElevation(0.0));
            Assert.IsNull(DecisionAltitude.FromThresholdElevation(-5.0));
            Assert.IsNull(DecisionAltitude.FromThresholdElevation(double.NaN));
        }

        [TestMethod]
        public void LaCatIESLaDelRespaldoHistorico()
        {
            // El 200 ft no se mueve: es la rama de respaldo que ya usaba el cliente y la que
            // sobrevive ahora como única fuente (NavData no publica mínimos de carta).
            Assert.AreEqual(200.0, DecisionAltitude.CatIAboveThresholdFt, 1e-9);
            Assert.AreEqual(220.0, DecisionAltitude.FromThresholdElevation(20.0).Value, 1e-9,
                "la 04R de KBOS (umbral 20 ft) mantiene su DA de 220 ft");
        }
    }
}
