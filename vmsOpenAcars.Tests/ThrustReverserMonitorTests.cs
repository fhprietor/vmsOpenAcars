using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Reversas: cuándo se dan por usadas y por qué el registro decía 0 %.
    ///
    /// El caso es el PIREP real **`5ow69vzDNW5ZMayB`** (SKRG, 21/10/2026). El bloque de datos de
    /// aterrizaje publicó `Reversas: 0% / 0%` a las **20:25:39** —el tick del toque, con las reversas
    /// todavía guardadas— y el aviso de verdad («detectada 95,2 %») llegó a las **20:26:23**, al entrar
    /// en `TaxiIn`, 44 s después. El dato existía: **95,2 % en los dos motores** durante el rodaje.
    ///
    /// Los tests fijan las tres cosas que se corrigieron: el 0 % del toque no es una medida; un ruido
    /// de décimas no es una reversa (el umbral era 0,6 %, o sea ninguno); y el cool-down se cuenta
    /// desde el toque, con reloj inyectado para no esperar 180 s.
    /// </summary>
    [TestClass]
    public class ThrustReverserMonitorTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 21, 20, 25, 39, DateTimeKind.Utc);

        private sealed class Clock
        {
            internal DateTime Now = T0;
            internal DateTime Read() => Now;
            internal void Advance(double seconds) => Now = Now.AddSeconds(seconds);
        }

        private static ThrustReverserMonitor Monitor(Clock clock) => new ThrustReverserMonitor(clock.Read);

        [TestMethod]
        public void ElCeroDelInstanteDelToqueNoEsUnaReversa()
        {
            var clock = new Clock();
            var monitor = Monitor(clock);
            monitor.OnTouchdown();

            for (int i = 0; i < 20; i++)                 // 2 s con el avión ya en la pista
            {
                monitor.Update(0, 0);
                clock.Advance(0.1);
            }

            Assert.IsFalse(monitor.ReversersUsed, "Antes de desplegarlas, 0 % es lo correcto: no hay reversa");
            Assert.IsFalse(monitor.ReverserDataAvailable, "Y tampoco prueba que el addon publique el dato");
        }

        [TestMethod]
        public void ElDespliegueRealSePublicaEnElMomento()
        {
            var clock = new Clock();
            var monitor = Monitor(clock);
            monitor.OnTouchdown();
            clock.Advance(2);                             // las reversas se despliegan ~2 s después

            monitor.Update(0.5, 0.5);
            Assert.IsFalse(monitor.FirstDetection, "0,5 % no es un despliegue");

            int detecciones = 0;
            for (int i = 0; i < 30; i++)                  // 3 s de despliegue en los dos motores
            {
                monitor.Update(95.2, 95.2);
                if (monitor.FirstDetection) detecciones++;
                clock.Advance(0.1);
            }

            Assert.AreEqual(1, detecciones, "Se avisa una vez, en el tick en que cruza el umbral");
            Assert.IsTrue(monitor.ReversersUsed);
            Assert.AreEqual(95.2, monitor.MaxEng1RevPct, 0.01);
            Assert.AreEqual(95.2, monitor.MaxEng2RevPct, 0.01);
        }

        [TestMethod]
        public void UnaSolaReversaBasta()
        {
            var clock = new Clock();
            var monitor = Monitor(clock);
            monitor.OnTouchdown();

            monitor.Update(12.0, 0.3);                    // asimétrico, como en un rodaje real

            Assert.IsTrue(monitor.ReversersUsed, "Con un motor por encima del umbral ya se usó");
            Assert.AreEqual(0.3, monitor.MaxEng2RevPct, 0.01);
        }

        [TestMethod]
        public void ElRuidoDeDecimasNoEsUnaReversa()
        {
            // El umbral era 0,6 %: cualquier lectura positiva contaba como «reversa usada», así que un
            // offset que se mueve unas décimas daba por hecho el despliegue y activaba el cool-down.
            var clock = new Clock();
            var monitor = Monitor(clock);
            monitor.OnTouchdown();

            for (int i = 0; i < 50; i++)
            {
                monitor.Update(0.6, 0.4);
                clock.Advance(0.1);
            }

            Assert.IsFalse(monitor.ReversersUsed, "0,6 % no es una reversa: el umbral son 10 %");
            Assert.IsTrue(monitor.ReverserDataAvailable, "Pero sí prueba que el addon publica el offset");
        }

        [TestMethod]
        public void ElCoolDownSeCuentaDesdeElToqueYSeCumple()
        {
            var clock = new Clock();
            var monitor = Monitor(clock);
            monitor.OnTouchdown();

            clock.Advance(2);
            monitor.Update(95.2, 95.2);                   // detectada a los 2 s

            clock.Advance(43);                            // 45 s después del toque
            monitor.Update(0, 0);
            string warning = monitor.CheckShutdown();

            Assert.IsNotNull(warning, "45 s no son los 180 s de cool-down");
            StringAssert.Contains(warning, "45s");

            clock.Advance(ThrustReverserMonitor.COOLDOWN_REQUIRED_SECONDS + 1);
            monitor.Update(0, 0);
            Assert.IsNull(monitor.CheckShutdown(), "Pasados los 180 s, el motor puede apagarse");
        }

        [TestMethod]
        public void SinReversasNoSeExigeCoolDown()
        {
            var clock = new Clock();
            var monitor = Monitor(clock);
            monitor.OnTouchdown();
            clock.Advance(10);
            monitor.Update(0, 0);

            Assert.IsNull(monitor.CheckShutdown(), "Sin reversas no hay nada que enfriar");
        }

        [TestMethod]
        public void SinDatosElAvisoDelAddonLlegaALosTreintaSegundos()
        {
            var clock = new Clock();
            var monitor = Monitor(clock);
            monitor.OnTouchdown();

            clock.Advance(29);
            monitor.Update(0, 0);
            Assert.IsNull(monitor.CheckAddonSupport(), "A los 29 s no se puede acusar al addon");

            clock.Advance(2);
            monitor.Update(0, 0);
            StringAssert.Contains(monitor.CheckAddonSupport(), "puede no soportar");
        }

        [TestMethod]
        public void ResetDejaElMonitorComoUnVueloNuevo()
        {
            var clock = new Clock();
            var monitor = Monitor(clock);
            monitor.OnTouchdown();
            monitor.Update(95.2, 95.2);
            Assert.IsTrue(monitor.ReversersUsed);

            monitor.Reset();

            Assert.IsFalse(monitor.ReversersUsed);
            Assert.AreEqual(0, monitor.MaxEng1RevPct, 0.01);
            Assert.AreEqual(0, monitor.SecondsSinceTouchdown);

            monitor.Update(95.2, 95.2);                   // sin touchdown no se alimenta
            Assert.IsFalse(monitor.ReversersUsed);
            Assert.IsFalse(monitor.FirstDetection);
        }
    }
}
