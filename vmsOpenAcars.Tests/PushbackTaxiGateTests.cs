using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La tercera puerta del `Pushback`**: que el avión esté rodando de verdad.
    ///
    /// Los tres vuelos reales que la fijan:
    ///
    /// | Vuelo | Remolque | Rodaje | Qué decide |
    /// |---|---|---|---|
    /// | `rMBVEKj5PZzx0pX9` (A321, 22/10) | **4 kt**, sin freno que avise | **17-24 kt** | la puerta cierra **95 s** antes que el rescate |
    /// | `XPNkY9jDneZ6ByGQ` (13/10) | **5 kt con una sola muestra**, motores apagados | — | no puede abrirse: es el caso que obligó a endurecerla |
    /// | `6aB60vzYbAKm7Z71` (A320, 20/10) | — (no hubo empuje) | **9-20 kt** | no aplica, ya salió a `TaxiOut` en 8 s |
    /// </summary>
    [TestClass]
    public class PushbackTaxiGateTests
    {
        private static DateTime T0 => new DateTime(2026, 10, 22, 18, 25, 32, DateTimeKind.Utc);

        /// <summary>Avanzar el reloj en pasos de un segundo con un estado dado.</summary>
        private static DateTime Run(PushbackTaxiGate gate, DateTime now, int seconds, double gs, bool engine, out bool fired)
        {
            fired = false;
            for (int i = 0; i < seconds; i++)
            {
                now = now.AddSeconds(1);
                if (gate.Confirm(now, gs, engine)) fired = true;
            }
            return now;
        }

        [TestMethod]
        public void ElCasoDelVueloRealHabriaCerradoNoventaYCincoSegundosAntes()
        {
            var gate = new PushbackTaxiGate();
            DateTime now = T0;

            // 18:25:40-18:26:11: remolcado a 4 kt, motores apagados. No es rodaje.
            now = Run(gate, now, 31, gs: 4.0, engine: false, out bool fired);
            Assert.IsFalse(fired, "un remolque a 4 kt sin motores no puede abrir la puerta");

            // 18:26:40: parado mientras terminan de arrancar.
            now = Run(gate, now, 60, gs: 0.0, engine: false, out fired);
            Assert.IsFalse(fired);

            // 18:28:41-18:28:56: rodando de verdad a 17 kt con motor. A los 15 s, puerta abierta.
            // El primer muestreo siembra la ventana; el umbral son 15 s de intervalo, no 15 ciclos.
            now = Run(gate, now, 15, gs: 17.0, engine: true, out fired);
            Assert.IsFalse(fired, "con 14 s de intervalo todavía no: el umbral es 15");

            now = now.AddSeconds(1);
            Assert.IsTrue(gate.Confirm(now, 17.0, true), "a los 15 s sí, y son 95 s antes del rescate de 300");
        }

        [TestMethod]
        public void ElRemolqueQueObligoAEndurecerLaPuertaNoLaAbre()
        {
            var gate = new PushbackTaxiGate();
            DateTime now = T0;

            // 13/10: 142 s de empuje que rozaron los 5 kt con una sola muestra, motores apagados.
            now = Run(gate, now, 142, gs: 5.0, engine: false, out bool fired);

            Assert.IsFalse(fired);
        }

        [TestMethod]
        public void UnRemolqueConLosMotoresEnMarchaTampocoLaAbre()
        {
            var gate = new PushbackTaxiGate();
            DateTime now = T0;

            // El caso incómodo: el piloto arranca los motores en el puesto y lo remolcan a 6 kt.
            now = Run(gate, now, 120, gs: 6.0, engine: true, out bool fired);

            Assert.IsFalse(fired, "6 kt son remolque, no rodaje: el umbral son 8");
        }

        [TestMethod]
        public void LaVentanaSeRearmaAlBajarDeVelocidad()
        {
            var gate = new PushbackTaxiGate();
            DateTime now = T0;

            now = Run(gate, now, 10, gs: 20.0, engine: true, out bool fired);
            Assert.IsFalse(fired);

            now = Run(gate, now, 2, gs: 3.0, engine: true, out fired);   // frenó
            Assert.IsFalse(fired);

            now = Run(gate, now, 15, gs: 20.0, engine: true, out fired);
            Assert.IsFalse(fired, "los 10 s de antes no se suman a los de ahora");

            now = now.AddSeconds(1);
            Assert.IsTrue(gate.Confirm(now, 20.0, true));
        }

        [TestMethod]
        public void SinDatoDeMotoresNoSeAbreNunca()
        {
            var gate = new PushbackTaxiGate();
            DateTime now = T0;

            now = Run(gate, now, 120, gs: 25.0, engine: false, out bool fired);

            Assert.IsFalse(fired,
                "un avión que no publica motores no puede perder las otras dos puertas por esta");
        }

        [TestMethod]
        public void ElResetVuelveAEmpezar()
        {
            var gate = new PushbackTaxiGate();
            DateTime now = T0;

            now = Run(gate, now, 20, gs: 20.0, engine: true, out bool fired);
            Assert.IsTrue(fired);

            gate.Reset();

            now = Run(gate, now, 15, gs: 20.0, engine: true, out fired);
            Assert.IsFalse(fired, "tras el reset hacen falta los 15 s otra vez");
        }
    }
}
