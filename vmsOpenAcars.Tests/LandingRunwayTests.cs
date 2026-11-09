using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Qué pista se guarda en el registro del vuelo. El caso real es el PIREP
    /// `nROPdk4GxYY5AOy1`: la pista se conocía desde `02:56:46 APPROACH CAPTURE: RWY 08` y
    /// `02:58:27 FLARE TRACK START: RWY 08`, pero a las `03:04:58` se guardó
    /// `Landing log: flight #51 saved (55 points, RWY )` — vacía.
    /// </summary>
    [TestClass]
    public class LandingRunwayTests
    {
        [TestMethod]
        public void SinPistaDelToque_SeUsaLaQueYaConociaLaAproximacion()
        {
            // La geometría del toque no se resolvió (el `Task.Run` de `LookupRunwayData` no
            // encontró el rectángulo), pero la aproximación había resuelto la 08. No se pierde.
            Assert.AreEqual("08", LandingRunway.ForRecord("", "08"));
            Assert.AreEqual("08", LandingRunway.ForRecord(null, "08"));
        }

        [TestMethod]
        public void ConPistaDelToque_MandaLaDelToque()
        {
            // El toque resolvió la pista real: es el dato físico y no se sustituye por el de la
            // aproximación (que podría haberse quedado en una pista paralela).
            Assert.AreEqual("14R", LandingRunway.ForRecord("14R", "14L"));
        }

        [TestMethod]
        public void SinNingunaPista_SeDejaVacia_NoSeInventa()
        {
            Assert.AreEqual("", LandingRunway.ForRecord("", null));
            Assert.AreEqual("", LandingRunway.ForRecord(null, ""));
            Assert.AreEqual("", LandingRunway.ForRecord("   ", "  "));
        }
    }
}
