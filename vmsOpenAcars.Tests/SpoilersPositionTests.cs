using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Los spoilers se leían como un booleano y son una **posición**. La tabla oficial de FSUIPC7 dice:
    ///
    /// ```
    /// 0x0BD8  4  SPOILERS RIGHT POSITION   "Spoiler Right position indicator (0-16383)"
    /// 0x0BCC  4  SPOILERS ARMED            "Spoilers arm (0=off, 1=arm for auto deployment)"
    /// ```
    ///
    /// y el cliente hacía `0x0BD8 > 0 || 0x0BCC > 0`. El resultado, en el PIREP real
    /// **`5ow69vzDNW5ZMayB`**: **26 líneas de spoilers en 44 s** de final y rachas en el ascenso con el
    /// avión limpio, porque el indicador temblaba unas cuentas y porque **estar armado** contaba como
    /// desplegado. Los valores de estos tests son los del rango documentado; el «ruido» es el que se
    /// deduce de ese parpadeo (una o dos cuentas) y el despliegue bueno, el del 100 % de la toma.
    /// </summary>
    [TestClass]
    public class SpoilersPositionTests
    {
        [TestMethod]
        public void ElRuidoDelIndicadorNoEsUnDespliegue()
        {
            // Una o dos cuentas de 16 383 son el 0,006 % / 0,01 %: con `> 0` eran «EXTENDIDOS».
            foreach (int raw in new[] { 0, 1, 2, 5, 20, 100, 400 })
            {
                double fraction = SpoilersPosition.FromRaw(raw);
                Assert.IsFalse(SpoilersPosition.IsDeployed(fraction),
                    $"una posición de {raw}/16383 no es un despliegue");
            }
        }

        [TestMethod]
        public void ElDespliegueDeLaTomaSiLoEs()
        {
            Assert.IsTrue(SpoilersPosition.IsDeployed(SpoilersPosition.FromRaw(16383)),
                "el despliegue automático de la toma va al 100 %");
            Assert.IsTrue(SpoilersPosition.IsDeployed(SpoilersPosition.FromRaw(16000)));
        }

        [TestMethod]
        public void ElUmbralEsElDiezPorCientoDelRecorrido()
        {
            // El 10 % de 16 383 son 1 638,3, así que el primer valor que cuenta es **1 639**: 1 638 es
            // el 9,998 % y no llega. Se calcula desde la constante para no dejar números mágicos.
            int firstDeployed = (int)System.Math.Ceiling(
                SpoilersPosition.MinDeployedFraction * SpoilersPosition.RawMax);

            Assert.AreEqual(1639, firstDeployed, "el umbral del 10 % cae en 1 639 de 16 383");
            Assert.IsFalse(SpoilersPosition.IsDeployed(SpoilersPosition.FromRaw(firstDeployed - 1)));
            Assert.IsTrue(SpoilersPosition.IsDeployed(SpoilersPosition.FromRaw(firstDeployed)));
        }

        [TestMethod]
        public void LaFraccionSeRecortaAlRango()
        {
            Assert.AreEqual(0.0, SpoilersPosition.FromRaw(-5), 1e-9);
            Assert.AreEqual(0.0, SpoilersPosition.FromRaw(0), 1e-9);
            Assert.AreEqual(1.0, SpoilersPosition.FromRaw(16383), 1e-9);
            Assert.AreEqual(1.0, SpoilersPosition.FromRaw(40000), 1e-9, "fuera de rango no puede pasar de 1");

            // Y el 100 % del bloque de aterrizaje ya es un dato de verdad, no un `1.0` de conveniencia.
            Assert.AreEqual(1.0, SpoilersPosition.FromRaw(16383), 1e-9);
            Assert.AreEqual(0.5, SpoilersPosition.FromRaw(8191), 0.001);
        }
    }
}
