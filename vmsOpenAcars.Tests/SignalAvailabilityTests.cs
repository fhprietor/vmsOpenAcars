using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// «¿Este addon publica el offset?» se contesta con **un cambio**, no con un valor.
    ///
    /// Los dos casos que lo motivan son ceros ambiguos: el **autobrake** (`0` es RTO, un ajuste válido)
    /// y los **carteles de cabina** (si el piloto no los toca, `0` puede ser «apagado» o «no
    /// publicado»). Y en los dos, un vuelo real acaba moviendo el valor: el selector se pone en RTO u
    /// OFF antes del despegue y en 1/2/3 antes de aterrizar; el letrero de cinturones se enciende y se
    /// apaga. Si nada se mueve en todo el vuelo, se publica «sin dato» en vez de suponer.
    /// </summary>
    [TestClass]
    public class SignalAvailabilityTests
    {
        [TestMethod]
        public void SinCambioNoHayDato()
        {
            var watch = new SignalAvailability<int>();

            for (int i = 0; i < 500; i++)
                Assert.IsFalse(watch.Update(0));

            Assert.IsFalse(watch.Available, "un cero constante no acredita nada");
        }

        [TestMethod]
        public void UnCambioAcreditaElOffsetParaElRestoDelVuelo()
        {
            var watch = new SignalAvailability<int>();

            watch.Update(0);
            Assert.IsTrue(watch.Update(2), "el selector pasó de RTO (0) a brake1 (2)");

            Assert.IsTrue(watch.Update(0), "y sigue acreditado aunque vuelva al valor de partida");
            Assert.IsTrue(watch.Available);
        }

        [TestMethod]
        public void ElPrimerValorEsElDePartidaYNoCuentaComoCambio()
        {
            var watch = new SignalAvailability<int>();

            Assert.IsFalse(watch.Update(3), "el primero solo siembra");
            Assert.AreEqual(3, watch.First);
            Assert.IsFalse(watch.Update(3));
            Assert.IsTrue(watch.Update(1));
        }

        [TestMethod]
        public void ElCasoDeLosCartelesDeCabina()
        {
            // Vuelo en el que nadie toca el letrero: no se puede acusar al addon, pero tampoco afirmar
            // que está apagado.
            var quieto = new SignalAvailability<int>();
            quieto.Update(0 * 2 + 0);
            Assert.IsFalse(quieto.Available);

            // Vuelo en el que se enciende: queda acreditado.
            var toca = new SignalAvailability<int>();
            toca.Update(0);
            Assert.IsTrue(toca.Update(1));
        }

        [TestMethod]
        public void ResetVuelveAExigirLaPrueba()
        {
            var watch = new SignalAvailability<int>();
            watch.Update(0);
            watch.Update(5);
            Assert.IsTrue(watch.Available);

            watch.Reset();

            Assert.IsFalse(watch.Available);
            Assert.IsFalse(watch.Update(0), "vuelo o avión nuevo: se vuelve a exigir el cambio");
        }
    }
}
