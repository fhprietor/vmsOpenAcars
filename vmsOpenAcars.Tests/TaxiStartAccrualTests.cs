using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El rodaje de salida cuenta **desde que el avión se mueve**, no desde que la fase lo dice.
    ///
    /// El caso real es el PIREP `rMBVEKj5PZzx0pX9` (SKRG→SKSM, A321 de Fenix, 22/10/2026): el avión
    /// empezó a rodar a las **18:28:41** y `TAXI OUT` se declaró a las **18:30:31** — **110 s** después—,
    /// porque en ese vuelo no hubo evento de freno de parqueo y la transición la hizo el rescate de
    /// 300 s (`PUSHBACK` entró a las 18:25:32 y `TAXI OUT` salió a los **299 s**). Esos 110 s de rodaje
    /// real no entraban en la métrica de motor único.
    /// </summary>
    [TestClass]
    public class TaxiStartAccrualTests
    {
        /// <summary>Simula `seconds` de un ciclo de 1 s con el estado dado.</summary>
        private static void Run(TaxiStartAccrual accrual, int seconds, bool moving, bool oneEngineOnly, double metresPerSecond = 5.0)
        {
            for (int i = 0; i < seconds; i++)
                accrual.Feed(1.0, moving ? metresPerSecond : 0.0, moving, oneEngineOnly);
        }

        [TestMethod]
        public void ElCasoDelVueloRealRecuperaLosCientoDiezSegundos()
        {
            var accrual = new TaxiStartAccrual();

            // 18:25:40–18:26:11: remolque, motores apagados → no cuenta como movimiento (exige motor).
            Run(accrual, 31, moving: false, oneEngineOnly: false);
            // 18:26:40: parado en el puesto.
            Run(accrual, 5, moving: false, oneEngineOnly: false);
            // 18:28:41–18:30:31: rodando de verdad, 110 s, con los dos motores.
            Run(accrual, 110, moving: true, oneEngineOnly: false);

            Assert.IsTrue(accrual.Take(out double moving, out double single, out double metres));
            Assert.AreEqual(110.0, moving, 0.001, "son los 110 s que el piloto reclamó");
            Assert.AreEqual(0.0, single, 0.001);
            Assert.AreEqual(550.0, metres, 0.001);
        }

        [TestMethod]
        public void CuentaDesdeLaUltimaParadaYNoDesdeElPrincipioDelEmpuje()
        {
            var accrual = new TaxiStartAccrual();

            Run(accrual, 40, moving: true, oneEngineOnly: false);  // remolcado con un motor en marcha
            Run(accrual, 10, moving: false, oneEngineOnly: false); // se detiene
            Run(accrual, 25, moving: true, oneEngineOnly: true);   // arranca el rodaje con uno

            Assert.IsTrue(accrual.Take(out double moving, out double single, out double metres));
            Assert.AreEqual(25.0, moving, 0.001, "lo anterior a la parada no entra");
            Assert.AreEqual(25.0, single, 0.001);
            Assert.AreEqual(125.0, metres, 0.001);
        }

        [TestMethod]
        public void UnaParadaEnMedioDejaSoloLaUltimaRacha()
        {
            var accrual = new TaxiStartAccrual();

            Run(accrual, 60, moving: true, oneEngineOnly: true);
            Run(accrual, 3, moving: false, oneEngineOnly: true);   // espera en el punto de espera
            Run(accrual, 20, moving: true, oneEngineOnly: false);

            Assert.IsTrue(accrual.Take(out double moving, out double single, out _));
            Assert.AreEqual(20.0, moving, 0.001);
            Assert.AreEqual(0.0, single, 0.001, "la racha con un motor quedó antes de la parada");
        }

        [TestMethod]
        public void SinMovimientoAntesDeLaTransicionNoHayNadaQueVolcar()
        {
            var accrual = new TaxiStartAccrual();

            Run(accrual, 30, moving: false, oneEngineOnly: false);

            Assert.IsFalse(accrual.Take(out _, out _, out _),
                "es el caso normal: el rodaje empezó ya con la fase declarada");
        }

        [TestMethod]
        public void NoSePagaDosVeces()
        {
            var accrual = new TaxiStartAccrual();

            Run(accrual, 15, moving: true, oneEngineOnly: true);

            Assert.IsTrue(accrual.Take(out _, out _, out _));
            Assert.IsFalse(accrual.Take(out _, out _, out _), "el volcado es una sola vez por vuelo");

            // Y después del volcado ya no acumula nada: a partir de ahí cuenta la fase.
            Run(accrual, 30, moving: true, oneEngineOnly: true);
            Assert.IsFalse(accrual.Take(out _, out _, out _));
        }

        [TestMethod]
        public void UnVueloNuevoVuelveAEmpezar()
        {
            var accrual = new TaxiStartAccrual();
            Run(accrual, 10, moving: true, oneEngineOnly: true);
            Assert.IsTrue(accrual.Take(out _, out _, out _));

            accrual.Reset();

            Assert.IsFalse(accrual.Consumed);
            Run(accrual, 12, moving: true, oneEngineOnly: true);
            Assert.IsTrue(accrual.Take(out double moving, out double single, out _));
            Assert.AreEqual(12.0, moving, 0.001);
            Assert.AreEqual(12.0, single, 0.001);
        }

        [TestMethod]
        public void UnHuecoDelRelojNoInventaSegundos()
        {
            var accrual = new TaxiStartAccrual();

            accrual.Feed(0.0, 0.0, moving: true, oneEngineOnly: true);   // delta no creíble
            accrual.Feed(-3.0, 0.0, moving: true, oneEngineOnly: true);  // reloj hacia atrás

            Assert.IsFalse(accrual.Take(out _, out _, out _),
                "`SampleClock` ya filtra los deltas imposibles; aquí no se suma nada negativo ni cero");
        }
    }
}
