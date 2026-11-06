using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El chime de aviso.** En el vuelo real `3b0oBlXD5ZolKPM4` el chime duró **2,93 s** y hacía
    /// que la voz del pushback empezara 3,0 s después del freno (01:57:06 → 01:57:09). Ese es el
    /// único PA que se locuta directamente; los demás conservan el suyo.
    /// </summary>
    [TestClass]
    public class CabinChimePolicyTests
    {
        [TestMethod]
        public void ElPushbackSeLocutaSinChime()
        {
            Assert.IsFalse(CabinChimePolicy.ShouldPlayChime("pushback"));
        }

        [TestMethod]
        public void LosDemasAnunciosConservanElChime()
        {
            Assert.IsTrue(CabinChimePolicy.ShouldPlayChime("taxi_out"));
            Assert.IsTrue(CabinChimePolicy.ShouldPlayChime("slides_disarm"));
            Assert.IsTrue(CabinChimePolicy.ShouldPlayChime("taxi_in"));
            Assert.IsTrue(CabinChimePolicy.ShouldPlayChime("on_block"));
        }
    }
}
