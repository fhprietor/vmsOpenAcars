using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Cuándo se cortó la potencia.** El criterio es el pico de N1 y la primera caída que se
    /// sostiene por debajo de él, no un umbral absoluto: el mismo 40 % de N1 es empuje en un 737
    /// ligero y ralentí en un 777 cargado, así que un umbral fijo no valdría para todos los aviones.
    ///
    /// **La traza es sintética**, y hay que decirlo: `flare_track` está **vacía (0 filas)** en la base
    /// local, así que no hay un aterrizaje real con N1 guardado contra el que validar ni el criterio
    /// ni el valor de `PowerCut.DropPct`. Lo que estos tests fijan es el **comportamiento del
    /// helper** —dónde sitúa el corte, cuándo se calla y con qué se conforma—, no una medición de
    /// vuelo.
    ///
    /// **Por qué la cadencia es 0,12 s y no 0,1 s**: el contacto se interpola como el **punto medio**
    /// del salto a tierra, así que con muestras cada 0,1 s la distancia al corte acaba siempre en
    /// `x,05` —exactamente el empate de la décima— y el redondeo cae a un lado u otro según el bit,
    /// no según el criterio. Con 0,12 s la diferencia es `4,14 s`, inequívocamente 4,1, y el test mide
    /// el helper en vez de la aritmética del punto flotante. La geometría es la del caso real que usa
    /// todo el análisis del flare (vuelo 41, SKBG → SKCG): el umbral en 0 y 150 kt.
    /// </summary>
    [TestClass]
    public class PowerCutTests
    {
        private const double StepFt      = 25.0;    // 25 ft por muestra
        private const double DtSec       = 0.12;    // ≈ 10 Hz
        private const double TouchdownFt = -1125.0; // la toma, 1 125 ft pasada la pista

        /// <summary>
        /// Una traza de +1 500 ft a −1 750 ft con el toque en <paramref name="touchdownFt"/>.
        /// `n1` es una función del índice de la muestra: cada test describe su propio perfil de
        /// potencia sin repetir la geometría.
        /// </summary>
        private static List<FlareTrackPoint> Track(Func<int, double?> n1,
                                                   double stepFt = StepFt, double dtSec = DtSec,
                                                   double touchdownFt = TouchdownFt,
                                                   bool bothEngines = true)
        {
            var list = new List<FlareTrackPoint>();
            var t = new DateTime(2026, 10, 7, 3, 8, 0, DateTimeKind.Utc);
            int i = 0;
            for (double d = 1500.0; d >= -1750.0; d -= stepFt, i++)
            {
                double? p = n1(i);
                list.Add(new FlareTrackPoint
                {
                    SeqNo        = i,
                    TimestampUtc = t.AddSeconds(dtSec * i),
                    DistFt       = d,
                    AglFt        = Math.Max(0.0, 60.0 + d / 40.0),
                    Eng1Pct      = p,
                    Eng2Pct      = bothEngines ? p : null,
                    OnGround     = d <= touchdownFt,
                });
            }
            return list;
        }

        /// <summary>
        /// El perfil de referencia: 58 % de empuje hasta la muestra **69** y 30 % (ralentí) desde la
        /// **70**. Con el toque en la transición de la muestra 104 a la 105, el corte queda a
        /// **4,14 s** del contacto y a **1,2 s** del cruce del umbral (muestra 60, dist 0).
        /// </summary>
        private static List<FlareTrackPoint> Reference() => Track(i => i <= 69 ? 58.0 : 30.0);

        // ── Dónde cae el corte ────────────────────────────────────────────────────

        [TestMethod]
        public void ElCorteDelPerfilDeReferencia_CaeDondeSeRetardoLaPotencia()
        {
            var r = PowerCut.Compute(Reference());

            Assert.IsTrue(r.HasValue, $"{r.Status}");
            Assert.AreEqual(PowerCutStatus.Ok, r.Status);

            Assert.AreEqual(4.1, r.SecondsBeforeTouchdown, 1e-9);
            Assert.AreEqual(1.2, r.SecondsAfterThreshold, 1e-9,
                "el cruce del umbral está en la muestra 60 (dist = 0), o sea a 7,2 s");

            Assert.AreEqual(58.0, r.PeakPct, 1e-9);
            Assert.AreEqual(30.0, r.CutPct, 1e-9);
            Assert.AreEqual(28.0, r.DropPct, 1e-9);
            Assert.AreEqual(30.0, r.AtTouchdownPct, 1e-9);

            // Dónde estaba el avión: el pico al principio de la captura y el corte ya pasado el umbral.
            Assert.AreEqual(1500.0, r.PeakDistFt, 1e-9);
            Assert.AreEqual(-250.0, r.CutDistFt, 1e-9);
        }

        /// <summary>
        /// **La potencia es la media de los motores que publican dato**, y `EngineCount` dice si el
        /// análisis se hizo con los dos o con uno. Un avión que solo publica un N1 —o con un motor
        /// apagado— sigue teniendo corte: no publicar el dato por eso sería perder lo que sí se tiene.
        /// </summary>
        [TestMethod]
        public void UnSoloMotor_TambienDaCorteYSeDice()
        {
            var r = PowerCut.Compute(Track(i => i <= 69 ? 58.0 : 30.0, bothEngines: false));

            Assert.IsTrue(r.HasValue, $"{r.Status}");
            Assert.AreEqual(1, r.EngineCount);
            Assert.AreEqual(4.1, r.SecondsBeforeTouchdown, 1e-9);
        }

        /// <summary>
        /// Con los dos motores, la potencia es su **media**: una asimetría de empuje mueve el número
        /// pero no la marca temporal, que es lo que se publica.
        /// </summary>
        [TestMethod]
        public void ConDosMotores_LaPotenciaEsLaMedia()
        {
            var samples = Reference();
            foreach (var s in samples) if (s.Eng1Pct.HasValue) s.Eng2Pct = s.Eng1Pct.Value - 2.0;

            var r = PowerCut.Compute(samples);

            Assert.AreEqual(2, r.EngineCount);
            Assert.AreEqual(57.0, r.PeakPct, 1e-9, "58 y 56 promedian 57");
            Assert.AreEqual(4.1, r.SecondsBeforeTouchdown, 1e-9,
                "la media no mueve el instante del corte");
        }

        // ── La caída tiene que sostenerse ─────────────────────────────────────────

        /// <summary>
        /// **Un bache de una sola muestra no es un corte.** El ruido del addon o una corrección de
        /// medio segundo darían un instante que el piloto no reconoce, así que se exige que la caída
        /// se mantenga la ventana de <see cref="PowerCut.SustainSec"/>.
        /// </summary>
        [TestMethod]
        public void UnBacheDeUnaSolaMuestra_NoEsUnCorte()
        {
            var r = PowerCut.Compute(Track(i => i == 70 ? 30.0 : 58.0));

            Assert.IsFalse(r.HasValue);
            Assert.AreEqual(PowerCutStatus.NoDrop, r.Status,
                "la potencia vuelve al pico en la muestra siguiente: no hay caída sostenida");
        }

        /// <summary>
        /// **La caída tiene que ser antes de tocar.** Después del contacto el N1 sube por la reversa y
        /// baja por el ralentí de rodaje, y eso no es «cuándo corté»: el corte se busca entre el pico
        /// y el toque.
        /// </summary>
        [TestMethod]
        public void LaCaidaDespuesDelToque_NoEsElCorte()
        {
            var r = PowerCut.Compute(Track(i => i <= 104 ? 58.0 : 30.0));

            Assert.IsFalse(r.HasValue);
            Assert.AreEqual(PowerCutStatus.NoDrop, r.Status);
        }

        /// <summary>
        /// El corte tiene que ser una caída **relativa al pico**: si la potencia baja 3 puntos, no es
        /// un retardamiento, es una corrección de empuje. El umbral está en
        /// <see cref="PowerCut.DropPct"/> (5 puntos) y aquí se fija por los dos lados.
        /// </summary>
        [TestMethod]
        public void UnaCaidaMenorQueElUmbral_NoEsUnCorte()
        {
            // 3 puntos por debajo del pico (58 → 55) no llega al umbral de 5.
            Assert.IsFalse(PowerCut.Compute(Track(i => i <= 69 ? 58.0 : 55.0)).HasValue);

            // 6 puntos sí (58 → 52).
            var justo = PowerCut.Compute(Track(i => i <= 69 ? 58.0 : 52.0));
            Assert.IsTrue(justo.HasValue, $"{justo.Status}");
            Assert.AreEqual(6.0, justo.DropPct, 1e-9);
        }

        // ── Degradar sin datos ────────────────────────────────────────────────────

        [TestMethod]
        public void SinTraza_NoHayCorte()
        {
            foreach (var nada in new[] { null, new List<FlareTrackPoint>() })
            {
                var r = PowerCut.Compute(nada);
                Assert.IsFalse(r.HasValue);
                Assert.AreEqual(PowerCutStatus.NoTrack, r.Status);
            }
        }

        /// <summary>
        /// **Sin N1 no hay corte, y no se rellena con nada.** El avión que no publica `0x2000` —un
        /// turbohélice, o un addon que no lo escribe— deja la traza sin potencia, y la respuesta es
        /// decirlo.
        /// </summary>
        [TestMethod]
        public void SinN1_NoHayCorte()
        {
            var r = PowerCut.Compute(Track(i => null));

            Assert.IsFalse(r.HasValue);
            Assert.AreEqual(PowerCutStatus.NoPowerData, r.Status);
        }

        /// <summary>
        /// Por debajo de <see cref="PowerCut.MinSamples"/> no hay ni pico ni ventana de sostenido que
        /// valgan: se dice que no hay dato en vez de construir un corte sobre cuatro muestras.
        /// </summary>
        [TestMethod]
        public void PocasMuestrasConPotencia_NoHayCorte()
        {
            // Seis muestras (traza válida para el eje temporal) con potencia en cuatro.
            var samples = new List<FlareTrackPoint>();
            var t = new DateTime(2026, 10, 7, 3, 8, 0, DateTimeKind.Utc);
            var dist = new[] { 50.0, 25.0, 0.0, -25.0, -50.0, -75.0 };
            for (int i = 0; i < dist.Length; i++)
                samples.Add(new FlareTrackPoint
                {
                    SeqNo        = i,
                    TimestampUtc = t.AddMilliseconds(120 * i),
                    DistFt       = dist[i],
                    // La potencia solo en las cuatro primeras: las otras dos no publican el offset.
                    Eng1Pct      = i < 4 ? (double?)(i < 2 ? 58.0 : 30.0) : null,
                    OnGround     = dist[i] <= -50.0,
                });

            var r = PowerCut.Compute(samples);

            Assert.IsFalse(r.HasValue);
            Assert.AreEqual(PowerCutStatus.NoPowerData, r.Status);
        }

        /// <summary>
        /// **La traza de 2 s no publica tiempos finos**, exactamente como el tiempo umbral→toma: con
        /// ±1 s de error, un «4,1 s» sería una décima con dos segundos de mentira detrás. Se señala la
        /// traza como gruesa en vez de calcular.
        /// </summary>
        [TestMethod]
        public void UnaTrazaGruesa_NoPublicaElCorte()
        {
            var r = PowerCut.Compute(Track(i => i <= 3 ? 58.0 : 30.0, stepFt: 500.0, dtSec: 2.0));

            Assert.IsFalse(r.HasValue);
            Assert.AreEqual(PowerCutStatus.CoarseTrack, r.Status);
            Assert.AreEqual(2.0, r.MedianSampleIntervalSec, 1e-9);
        }

        /// <summary>
        /// Sin cruce del umbral no hay eje temporal respecto al cual decir «cuándo»: el helper
        /// comparte el cruce y el toque con `ThresholdToTouchdown` en vez de recalcularlos, así que
        /// hereda también sus motivos para callarse.
        /// </summary>
        [TestMethod]
        public void SinCruceDeUmbral_NoHayEjeTemporal()
        {
            var samples = new List<FlareTrackPoint>();
            var t = new DateTime(2026, 10, 7, 3, 8, 0, DateTimeKind.Utc);
            for (int i = 0; i < 30; i++)
                samples.Add(new FlareTrackPoint
                {
                    SeqNo        = i,
                    TimestampUtc = t.AddMilliseconds(100 * i),
                    DistFt       = -100.0 - i * 50.0,     // toda la traza pasada la pista
                    Eng1Pct      = i < 10 ? 58.0 : 30.0,
                    OnGround     = i >= 20,
                });

            var r = PowerCut.Compute(samples);

            Assert.IsFalse(r.HasValue);
            Assert.AreEqual(PowerCutStatus.NoTimeline, r.Status);
        }

        /// <summary>Si la potencia nunca cae de forma sostenida —un avión que llega con empuje— no hay
        /// corte, y tampoco se devuelve un cero: se dice que no lo hubo.</summary>
        [TestMethod]
        public void LaPotenciaNuncaCae_NoHayCorte()
        {
            var r = PowerCut.Compute(Track(i => 58.0));

            Assert.IsFalse(r.HasValue);
            Assert.AreEqual(PowerCutStatus.NoDrop, r.Status);
        }

        // ── El dato que viaja al PIREP ────────────────────────────────────────────

        [TestMethod]
        public void ElSufijoDelPirep_VaEnAsciiYConLaUnidad()
        {
            Assert.AreEqual("PWR-CUT 4.1s", PowerCut.PirepSuffix(PowerCut.Compute(Reference())));
            Assert.IsNull(PowerCut.PirepSuffix(PowerCut.Compute(null)),
                "sin dato no se concatena nada en el notes");

            foreach (char c in PowerCut.PirepSuffix(PowerCut.Compute(Reference())))
                Assert.IsTrue(c <= 127, "el notes es texto para la aerolínea: en ASCII");
        }
    }
}
