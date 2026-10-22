using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El filtro de los rebotes de tren y spoilers.
    ///
    /// El caso es el PIREP real **`5ow69vzDNW5ZMayB`** (B77W en MSFS, 21/10/2026), con el avión
    /// limpio en el ascenso y el offset de spoilers rebotando: `EXTENDIDOS` 13:17:46, `RETRAÍDOS`
    /// 13:17:49, `RETRAÍDOS` 13:17:50 —dos seguidos, o sea un EXT/RET en medio—, `EXTENDIDOS`
    /// 13:17:50… Cada rebote era una línea del registro.
    ///
    /// La regla: el cambio solo pasa si el valor nuevo se sostiene 2,5 s, el mismo criterio que las
    /// luces. La primera muestra siembra sin publicar, para que un vuelo que empieza con el tren
    /// abajo no anuncie «tren abajo» al pulsar START.
    /// </summary>
    [TestClass]
    public class StateChangeGateTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 21, 13, 17, 40, DateTimeKind.Utc);

        private static DateTime At(double seconds) => T0.AddSeconds(seconds);

        [TestMethod]
        public void LaAproximacionYLaTomaTampocoSeLlenanDeRebotes()
        {
            // El mismo vuelo, en la final y la toma (20:24:54 → 20:25:38): **26 líneas** de spoilers en
            // 44 s, con pares partidos por menos de un segundo. El estado alterna tan rápido que el
            // registro escribe dos «RETRAÍDOS» seguidos porque hubo un EXT/RET en medio.
            var gate = new StateChangeGate<bool>(2.5);
            bool previous;
            int events = 0;

            gate.Update(T0, false, out previous);          // llegando limpio

            double[] times =
            {
                0, 5, 11, 12, 26, 28, 28.4, 30, 30.4, 31, 31.4, 31.4, 31.6,
                32, 32.6, 33, 36, 36.4, 36.6, 37, 39, 39.4, 41, 41.4, 43, 44
            };

            bool value = false;
            for (int i = 0; i < times.Length; i++)
            {
                value = i == 0 ? false : !value;           // cada línea del registro es un cambio
                if (gate.Update(T0.AddSeconds(times[i]), value, out previous)) events++;
            }

            Assert.IsTrue(events <= 3,
                $"26 líneas no pueden quedar en {events}: las rachas de 1-2 s no se publican");
        }

        [TestMethod]
        public void LaPrimeraMuestraSiembraYNoPublica()
        {
            var gate = new StateChangeGate<bool>(2.5);
            bool previous;

            Assert.IsFalse(gate.Update(T0, true, out previous),
                "El primer valor es el estado de partida, no un cambio");
            Assert.IsFalse(gate.Update(At(1), true, out previous));
        }

        [TestMethod]
        public void UnReboteDeDosSegundosNoSePublica()
        {
            // La racha real: EXTENDIDOS → RETRAÍDOS (3 s) → vuelve a EXTENDIDOS a los 1 s.
            var gate = new StateChangeGate<bool>(2.5);
            bool previous;

            gate.Update(T0, false, out previous);          // sembrado: retraídos
            Assert.IsFalse(gate.Update(At(6), true, out previous), "empieza a desplegarse");
            Assert.IsFalse(gate.Update(At(7), true, out previous), "1 s, todavía no");
            Assert.IsFalse(gate.Update(At(7.5), false, out previous),
                "vuelve atrás antes de los 2,5 s: no pasó nada");
            Assert.IsFalse(gate.Update(At(20), false, out previous));
        }

        [TestMethod]
        public void LaRachaDelPirepNoPublicaNada()
        {
            // Los tiempos del registro: 13:17:46 EXT, :49 RET, :50 RET, :50 EXT, 13:20:30 RET,
            // :31 EXT, :32 EXT, :35 RET, :35 EXT. Se recorren 20 Hz hacia adelante.
            var gate = new StateChangeGate<bool>(2.5);
            bool previous;
            int events = 0;

            double[] times  = { 6.0,   9.0,   10.0,  10.4,  170.0, 171.0, 172.0, 175.0, 175.4 };
            bool[]   values = { true,  false, false, true,  false, true,  true,  false, true  };

            gate.Update(T0, false, out previous);          // sembrado antes de la racha

            for (double t = 0; t <= 176; t += 0.05)
            {
                bool value = false;                        // antes de la racha, retraídos
                for (int k = 0; k < times.Length; k++)
                    if (t >= times[k]) value = values[k];

                if (gate.Update(T0.AddSeconds(t), value, out previous)) events++;
            }

            // Lo único que se sostiene más de 2,5 s es el tramo EXTENDIDOS de 6 s a 9 s: un aviso.
            // Los rebotes de 0,4-1,4 s (9→10,4 · 170→171 · 175→175,4) no llegan.
            Assert.AreEqual(1, events,
                "Los rebotes de 1-2 s no se publican; el estado que sí se sostuvo, una vez");
        }

        [TestMethod]
        public void UnCambioSostenidoSePublicaUnaVezConElValorAnterior()
        {
            var gate = new StateChangeGate<int>(2.5);
            int previous;

            gate.Update(T0, 0, out previous);              // tren arriba
            Assert.IsFalse(gate.Update(At(10), 1, out previous));
            Assert.IsFalse(gate.Update(At(12.0), 1, out previous), "a 2 s todavía no");

            Assert.IsTrue(gate.Update(At(12.5), 1, out previous), "a 2,5 s sí");
            Assert.AreEqual(0, previous, "y dice de dónde venía");

            Assert.IsFalse(gate.Update(At(13), 1, out previous), "no se repite");
        }

        /// <summary>
        /// Muestrea el gate como lo hace el servicio: 20 Hz entre dos instantes, con el mismo valor.
        /// Devuelve cuántos cambios se publicaron y deja el último «valor anterior» en
        /// <paramref name="previous"/>.
        /// </summary>
        private static int Run(StateChangeGate<bool> gate, double fromSec, double toSec, bool value,
                               out bool previous)
        {
            int events = 0;
            bool last = false;
            for (double t = fromSec; t <= toSec + 1e-9; t += 0.05)
                if (gate.Update(T0.AddSeconds(t), value, out last)) events++;

            previous = last;
            return events;
        }

        [TestMethod]
        public void VariosRebotesSeguidosSoloPublicanElQueSeSostiene()
        {
            var gate = new StateChangeGate<int>(2.5);
            int previous;
            int events = 0;

            gate.Update(T0, 0, out previous);

            // Alternando cada 0,5 s durante 5 s, a 20 Hz.
            for (double t = 0.5; t <= 5.0; t += 0.5)
            {
                int value = ((int)(t * 2)) % 2;
                for (int i = 0; i < 10; i++)
                    if (gate.Update(T0.AddSeconds(t + i * 0.05), value, out previous)) events++;
            }
            Assert.AreEqual(0, events, "alternar cada medio segundo no es un cambio de tren");

            // Ahora sí, sostenido 5 s: un único cambio, y dice de dónde venía. `previous` se toma
            // en la llamada que publica, no al final del bucle: el out se rellena en todas.
            int previousOnEmit = -1;
            for (int i = 0; i < 100; i++)
            {
                int prev;
                if (gate.Update(T0.AddSeconds(6.0 + i * 0.05), 1, out prev))
                {
                    events++;
                    previousOnEmit = prev;
                }
            }

            Assert.AreEqual(1, events);
            Assert.AreEqual(0, previousOnEmit);
        }

        [TestMethod]
        public void ElSostenidoEsElQueSeConfigura()
        {
            var corto = new StateChangeGate<bool>(1.0);
            var largo = new StateChangeGate<bool>(5.0);
            bool previous = false;

            corto.Update(T0, false, out previous);
            largo.Update(T0, false, out previous);

            // El mismo cambio, sostenido 3 s: con 1 s de exigencia pasa; con 5 s, todavía no.
            Assert.AreEqual(1, Run(corto, 1.2, 4.2, true, out previous), "con 1 s de sostenido, pasa");
            Assert.AreEqual(0, Run(largo,  1.2, 4.2, true, out previous), "con 5 s, todavía no");

            // Y cuando se cumple el plazo del largo, pasa.
            Assert.AreEqual(1, Run(largo, 4.25, 7.0, true, out previous));
        }
    }
}
