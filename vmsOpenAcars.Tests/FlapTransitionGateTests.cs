using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El portero de flaps del PIREP real `l5QVxBEmJ4yOaY2O` (B738 SSW, MSFS 2024). Las dos
    /// secuencias de abajo son **literales** del log de ese vuelo: siete transiciones en seis
    /// segundos en la aproximación y doce en quince en el despegue, con un solo movimiento de la
    /// palanca. Lo que se fija: la aproximación deja **una** línea y el despegue **una o dos**; un
    /// cambio real de detente sostenido (38→50) sí se anuncia, y un temblor dentro del mismo
    /// detente no.
    /// </summary>
    [TestClass]
    public class FlapTransitionGateTests
    {
        private struct Sample
        {
            internal double Sec;
            internal double Percent;

            internal Sample(double sec, double percent)
            {
                Sec     = sec;
                Percent = percent;
            }
        }

        private static readonly DateTime BaseUtc =
            new DateTime(2026, 11, 24, 1, 49, 26, DateTimeKind.Utc);

        /// <summary>
        /// Alimenta el portero a 10 Hz (bastante más fino que los 20 Hz reales del ciclo de
        /// telemetría) para que el asentamiento se evalúe entre transiciones, como en vuelo. La
        /// primera muestra es el valor **anterior** a la primera transición del log: es la siembra.
        /// </summary>
        private static List<FlapTransition> Run(params Sample[] samples)
        {
            var gate  = new FlapTransitionGate();
            var lines = new List<FlapTransition>();
            const double step = 0.1;

            for (int i = 0; i < samples.Length; i++)
            {
                double end = i + 1 < samples.Length
                    ? samples[i + 1].Sec
                    : samples[i].Sec + 4.0;   // cola: el último detente se queda quieto

                for (double sec = samples[i].Sec; sec < end - 1e-9; sec += step)
                {
                    var transition = gate.Update(samples[i].Percent, BaseUtc.AddSeconds(sec));
                    if (transition.HasValue) lines.Add(transition.Value);
                }
            }
            return lines;
        }

        [TestMethod]
        public void Aproximacion_SieteTransicionesEnSeisSegundos_DejaUnaLinea()
        {
            // Literal del 01:49:27 al 01:49:33, con el 62 % previo (el «62 → 87» del primer renglón).
            // El 75 % se sostiene ~1,8 s (01:49:29–01:49:31), pero es la MISMA banda que el 62 %
            // ya anunciado (banda 6), así que no escribe línea. El único detente nuevo y asentado es
            // el 87 % final (banda 7).
            var lines = Run(
                new Sample(0.0, 62),   // antes del 01:49:27
                new Sample(1.0, 87),   // 01:49:27  62% → 87%
                new Sample(2.0, 62),   // 01:49:28  87% → 62%
                new Sample(3.0, 87),   // 01:49:29  62% → 87%
                new Sample(3.2, 75),   // 01:49:29  87% → 75%
                new Sample(5.0, 87),   // 01:49:31  75% → 87%
                new Sample(6.0, 75),   // 01:49:32  87% → 75%
                new Sample(7.0, 87));  // 01:49:33  75% → 87%

            Assert.AreEqual(1, lines.Count,
                "siete transiciones del temblor tienen que quedar en una sola línea asentada");
            Assert.AreEqual(87.0, lines[0].Percent, 0.5,
                "la línea es el detente final, el 87 %");
        }

        [TestMethod]
        public void Despegue_DoceTransicionesEnQuinceSegundos_DejaUnaODosLineas()
        {
            // Literal del 00:59:43 al 00:59:57. Aquí el 13 % sí se sostiene más de 1,5 s
            // (00:59:49–00:59:53), así que puede dejar una línea intermedia; el 38 % final deja la
            // última. Lo que no puede pasar es volver a las doce.
            var lines = Run(
                new Sample(0.0,  0),   // antes del 00:59:43
                new Sample(1.0,  25),  // 00:59:43  0% → 25%
                new Sample(2.0,  38),  // 00:59:44  25% → 38%
                new Sample(2.2,  13),  // 00:59:44  38% → 13%
                new Sample(3.0,  0),   // 00:59:45  13% → 0%
                new Sample(3.2,  38),  // 00:59:45  0% → 38%
                new Sample(4.0,  13),  // 00:59:46  38% → 13%
                new Sample(5.0,  38),  // 00:59:47  13% → 38%
                new Sample(5.2,  13),  // 00:59:47  38% → 13%
                new Sample(7.0,  38),  // 00:59:49  13% → 38%
                new Sample(7.2,  13),  // 00:59:49  38% → 13%
                new Sample(11.0, 25),  // 00:59:53  13% → 25%
                new Sample(15.0, 38)); // 00:59:57  25% → 38%

            Assert.IsTrue(lines.Count >= 1 && lines.Count <= 2,
                $"doce transiciones tienen que quedar en una o dos líneas, no en {lines.Count}");
            Assert.AreEqual(38.0, lines[lines.Count - 1].Percent, 0.5,
                "la última línea es el detente final del despegue, el 38 %");
        }

        [TestMethod]
        public void CambioRealDeDetente_Sostenido_SiSeAnuncia()
        {
            // El caso que no se puede perder: 38 % → 50 % sostenido. 1,5 s después del cambio ya
            // es una configuración asentada y la línea sale con el «desde» real.
            var gate = new FlapTransitionGate();
            Assert.IsNull(gate.Update(38.0, BaseUtc), "la primera lectura siembra, no anuncia");
            Assert.IsNull(gate.Update(50.0, BaseUtc.AddSeconds(1.0)), "el cambio todavía no se asentó");
            Assert.IsNull(gate.Update(50.0, BaseUtc.AddSeconds(2.4)), "1,4 s no llega al umbral");

            var settled = gate.Update(50.0, BaseUtc.AddSeconds(2.5));
            Assert.IsTrue(settled.HasValue, "1,5 s en el mismo detente es una configuración asentada");
            Assert.AreEqual(38.0, settled.Value.PreviousPercent, 1e-9);
            Assert.AreEqual(50.0, settled.Value.Percent, 1e-9);
        }

        [TestMethod]
        public void TemblorDentroDeLaMismaBanda_NoSeAnuncia()
        {
            // 40 y 44 caen en la misma banda (`FlapsScale.BandKey` = 4): es el mismo detente. El
            // portero no compara porcentajes, así que este movimiento no escribe nada.
            var gate = new FlapTransitionGate();
            gate.Update(40.0, BaseUtc);

            for (int i = 1; i <= 30; i++)
                Assert.IsNull(gate.Update(44.0, BaseUtc.AddSeconds(i * 0.1)),
                    "un cambio dentro del mismo detente no es una transición que anunciar");
        }

        [TestMethod]
        public void UnPicoQueNoSeAsienta_NoSeAnuncia()
        {
            // El pico de un frame (87 % durante 0,2 s) tampoco pasa: el reloj se reinicia cuando el
            // valor vuelve a 62 %. Es el mecanismo que convierte las siete líneas en una.
            var gate = new FlapTransitionGate();
            gate.Update(62.0, BaseUtc);
            Assert.IsNull(gate.Update(87.0, BaseUtc.AddSeconds(1.0)), "el pico aún no se asentó");
            Assert.IsNull(gate.Update(62.0, BaseUtc.AddSeconds(1.2)), "vuelve al detente anterior");

            for (int i = 13; i <= 40; i++)
                Assert.IsNull(gate.Update(62.0, BaseUtc.AddSeconds(i * 0.1)),
                    "el 62 % ya estaba anunciado: no repite línea");
        }
    }
}
