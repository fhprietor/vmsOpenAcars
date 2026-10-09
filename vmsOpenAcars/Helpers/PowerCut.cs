using System;
using System.Collections.Generic;
using System.Globalization;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Por qué no hay un corte de potencia que publicar.** Igual que en
    /// <see cref="ThresholdToTouchdownStatus"/>: cada valor es un caso en el que el dato no existe y
    /// la respuesta correcta es callarse, nunca devolver un cero.
    /// </summary>
    internal enum PowerCutStatus
    {
        /// <summary>Hay corte: interpolado y redondeado a 0,1 s.</summary>
        Ok,

        /// <summary>Sin traza utilizable (menos de dos muestras con instante y distancia).</summary>
        NoTrack,

        /// <summary>
        /// La traza no es fina. Es la **misma puerta** que la del tiempo umbral→toma
        /// (`ThresholdToTouchdown.MaxPublishableSampleIntervalSec`): con la traza de 2 s, ±1 s de
        /// error disfrazado de décimas es peor que no publicar nada.
        /// </summary>
        CoarseTrack,

        /// <summary>La traza no cruza el umbral o no tiene transición aire→tierra: sin eje temporal
        /// no se puede decir «cuándo».</summary>
        NoTimeline,

        /// <summary>Ninguna muestra trae N1: el avión no publica el offset, o no es un reactor.</summary>
        NoPowerData,

        /// <summary>Menos de <see cref="PowerCut.MinSamples"/> muestras con potencia, o el máximo de
        /// todas ellas está ya en tierra: no hay un pico del que medir la caída.</summary>
        NoPeak,

        /// <summary>La potencia nunca cae de forma sostenida por debajo del pico antes del toque.</summary>
        NoDrop,
    }

    /// <summary>En qué momento se cortó la potencia, o el motivo de no tenerlo.</summary>
    internal sealed class PowerCutResult
    {
        private PowerCutResult() { }

        /// <summary>Solo `true` con <see cref="PowerCutStatus.Ok"/>.</summary>
        internal bool HasValue { get; private set; }

        internal PowerCutStatus Status { get; private set; }

        /// <summary>
        /// **Segundos desde el corte hasta la toma**, redondeados a 0,1 s. Es la forma útil del dato:
        /// «corté 4,2 s antes de tocar». `NaN` sin dato.
        /// </summary>
        internal double SecondsBeforeTouchdown { get; private set; }

        /// <summary>
        /// Segundos del corte **respecto al cruce del umbral** (negativo = el corte fue antes de
        /// cruzar). Comparte el cruce con <see cref="ThresholdToTouchdown"/> en vez de recalcularlo.
        /// `NaN` sin dato.
        /// </summary>
        internal double SecondsAfterThreshold { get; private set; }

        /// <summary>El pico de N1 media de la traza, en porcentaje.</summary>
        internal double PeakPct { get; private set; }

        /// <summary>La potencia media en la muestra del corte, en porcentaje.</summary>
        internal double CutPct { get; private set; }

        /// <summary>Cuánto cayó en el corte respecto al pico, en puntos: `PeakPct − CutPct`.</summary>
        internal double DropPct { get; private set; }

        /// <summary>La potencia media en la última muestra en el aire, en porcentaje.</summary>
        internal double AtTouchdownPct { get; private set; }

        /// <summary>Cuántos motores entraron en la media: 2 con los dos, 1 si solo publica uno.</summary>
        internal int EngineCount { get; private set; }

        /// <summary>La distancia al umbral del pico, en pies (positiva = antes).</summary>
        internal double PeakDistFt { get; private set; }

        /// <summary>La distancia al umbral del corte, en pies (positiva = antes).</summary>
        internal double CutDistFt { get; private set; }

        /// <summary>Cadencia mediana de la traza, en segundos (`NaN` si no se pudo medir).</summary>
        internal double MedianSampleIntervalSec { get; private set; }

        /// <summary>Muestras de la traza que traían potencia.</summary>
        internal int SampleCount { get; private set; }

        internal static PowerCutResult No(PowerCutStatus status, int sampleCount, double medianSec)
            => new PowerCutResult
            {
                HasValue       = false,
                Status         = status,
                SecondsBeforeTouchdown = double.NaN,
                SecondsAfterThreshold  = double.NaN,
                PeakPct        = double.NaN,
                CutPct         = double.NaN,
                DropPct        = double.NaN,
                AtTouchdownPct = double.NaN,
                PeakDistFt     = double.NaN,
                CutDistFt      = double.NaN,
                SampleCount    = sampleCount,
                MedianSampleIntervalSec = medianSec,
            };

        /// <summary>El caso con dato. El constructor y los `set` son privados a propósito: un
        /// `PowerCutResult` solo nace de aquí o de <see cref="No"/>, nunca a medio construir.</summary>
        internal static PowerCutResult Ok(double secondsBeforeTouchdown, double secondsAfterThreshold,
                                          double peakPct, double cutPct, double atTouchdownPct,
                                          int engineCount, double peakDistFt, double cutDistFt,
                                          int sampleCount, double medianSec)
            => new PowerCutResult
            {
                HasValue               = true,
                Status                 = PowerCutStatus.Ok,
                SecondsBeforeTouchdown = secondsBeforeTouchdown,
                SecondsAfterThreshold  = secondsAfterThreshold,
                PeakPct                = peakPct,
                CutPct                 = cutPct,
                DropPct                = peakPct - cutPct,
                AtTouchdownPct         = atTouchdownPct,
                EngineCount            = engineCount,
                PeakDistFt             = peakDistFt,
                CutDistFt              = cutDistFt,
                SampleCount            = sampleCount,
                MedianSampleIntervalSec = medianSec,
            };
    }

    /// <summary>
    /// **Cuándo se cortó la potencia en el aterrizaje**, que es lo que explica un flotado largo:
    /// si el piloto retardó el ralentí hasta muy tarde, el avión llega al umbral con empuje y sigue
    /// volando.
    ///
    /// Función pura, sin WinForms y sin base de datos, como <see cref="ThresholdToTouchdown"/>.
    ///
    /// **El criterio: el pico y el inicio de la caída sostenida, no un umbral absoluto.** Un umbral
    /// fijo del tipo «N1 &lt; 40 %» no sirve: el N1 de aproximación depende del avión, del peso y del
    /// viento, así que el mismo 40 % es empuje en un 737 ligero y ralentí en un 777 cargado. La
    /// pregunta del piloto —«¿cuándo dejé de empujar?»— se contesta con dos hechos que la traza sí
    /// tiene: **el máximo de potencia** (el empuje con el que se venía) y **la primera caída que se
    /// sostiene** por debajo de ese máximo. Una caída **relativa al pico** es la misma pregunta para
    /// todos los aviones.
    ///
    /// **Y se exige que la caída se sostenga** (<see cref="SustainSec"/>), no solo que toque el
    /// umbral un ciclo: un bache suelto de una muestra —ruido del addon, una corrección de medio
    /// segundo— no es un corte, y marcarlo daría un instante que el piloto no reconoce. A 10 Hz la
    /// ventana son tres muestras, y si el corte cae a menos de esa ventana del toque se acepta lo que
    /// haya hasta el toque, pero **nunca una sola muestra**: con una no se puede afirmar «sostenido».
    ///
    /// **La potencia es la media de los motores que publican dato** (`eng1_pct`, `eng2_pct`). Con los
    /// dos, promediar evita que una asimetría de empuje mueva la marca; con uno solo —o con un motor
    /// apagado—, ese manda. `EngineCount` dice cuál de los dos casos fue, para que la pantalla pueda
    /// avisar de que el dato es de un motor.
    ///
    /// **El eje temporal no se recalcula**: se toma de <see cref="ThresholdToTouchdown"/>, que es
    /// quien interpola el cruce del umbral y el contacto. Dos interpolaciones distintas del mismo
    /// suceso darían dos números que no cuadrarían entre sí, y el tiempo umbral→toma ya se publica.
    ///
    /// **Degrada sin datos** en los seis casos del enumerado, y **no publica tiempos finos sobre la
    /// traza de 2 s**: ver <see cref="PowerCutStatus.CoarseTrack"/>.
    /// </summary>
    internal static class PowerCut
    {
        /// <summary>
        /// **Cuánto tiene que caer la potencia para contar como corte: 5 puntos porcentuales por
        /// debajo del pico.** En un reactor en final, del empuje de aproximación al ralentí hay
        /// **30–40 puntos** de N1, así que 5 son una fracción clara (≈15 %) y no una corrección de
        /// empuje; y la lectura es un `FLOAT64` con el valor exacto del simulador (offset `0x2000`),
        /// sin cuantizar, así que no hay un suelo de ruido por debajo. **No está medido contra una
        /// traza real** —`flare_track` está vacía en la base local—: es una magnitud de orden, no un
        /// número validado, y el test fija el valor para que un cambio sea deliberado.
        /// </summary>
        internal const double DropPct = 5.0;

        /// <summary>
        /// **Cuánto tiene que sostenerse la caída: 0,3 s.** A 10 Hz son **tres muestras**, que es lo
        /// que separa un corte de un bache de un solo ciclo. No se sube más porque el corte de verdad
        /// ocurre a menudo muy cerca del toque, y exigir medio segundo dejaría sin dato los
        /// retardamientos tardíos —justo los que explican el flotado que se viene a mirar—.
        /// </summary>
        internal const double SustainSec = 0.3;

        /// <summary>
        /// **Muestras con potencia que hacen falta como mínimo: 5.** Con cuatro no hay ni pico ni
        /// ventana de sostenido que valgan; por debajo se dice que no hay dato en vez de publicar un
        /// corte construido sobre nada.
        /// </summary>
        internal const int MinSamples = 5;

        /// <summary>Prefijo con el que el dato viaja en el `notes` del PIREP: `PWR-CUT 4.2s`.</summary>
        internal const string PirepPrefix = "PWR-CUT";

        internal static PowerCutResult Compute(IList<FlareTrackPoint> samples)
        {
            // El eje temporal sale del mismo helper que publica el tiempo umbral→toma: si él no tiene
            // cruce y contacto, aquí tampoco hay «cuándo».
            var timeline = ThresholdToTouchdown.Compute(samples);
            if (!timeline.HasValue)
                return PowerCutResult.No(TimelineStatus(timeline.Status), 0,
                                         timeline.MedianSampleIntervalSec);

            // Serie de potencia: media de los motores que publican dato en cada muestra. La lista se
            // recorre en el orden de captura, igual que el tiempo umbral→toma.
            var times  = new List<DateTime>();
            var dists  = new List<double>();
            var powers = new List<double>();
            int bothEngines = 0;

            if (samples != null)
            {
                foreach (var s in samples)
                {
                    if (s == null) continue;
                    if (s.TimestampUtc == default(DateTime)) continue;
                    if (double.IsNaN(s.DistFt) || double.IsInfinity(s.DistFt)) continue;

                    double sum = 0.0; int n = 0;
                    if (Finite(s.Eng1Pct)) { sum += s.Eng1Pct.Value; n++; }
                    if (Finite(s.Eng2Pct)) { sum += s.Eng2Pct.Value; n++; }
                    if (n == 0) continue;
                    if (n == 2) bothEngines++;

                    times.Add(s.TimestampUtc);
                    dists.Add(s.DistFt);
                    powers.Add(sum / n);
                }
            }

            double median = timeline.MedianSampleIntervalSec;

            if (powers.Count == 0)
                return PowerCutResult.No(PowerCutStatus.NoPowerData, 0, median);
            if (powers.Count < MinSamples)
                return PowerCutResult.No(PowerCutStatus.NoPowerData, powers.Count, median);

            DateTime touchdownUtc = timeline.TouchdownUtc.Value;

            // ── El pico, antes del toque ──────────────────────────────────────────
            // Después de tocar el N1 sube por la reversa, y eso no es «el empuje con el que se venía».
            int peakIndex = -1;
            for (int i = 0; i < powers.Count; i++)
            {
                if (times[i] > touchdownUtc) break;
                if (peakIndex < 0 || powers[i] > powers[peakIndex]) peakIndex = i;
            }
            if (peakIndex < 0 || peakIndex >= powers.Count - 1)
                return PowerCutResult.No(PowerCutStatus.NoPeak, powers.Count, median);

            double peak = powers[peakIndex];

            // ── La primera caída sostenida por debajo del pico ────────────────────
            int cutIndex = -1;
            for (int i = peakIndex + 1; i < powers.Count; i++)
            {
                if (times[i] > touchdownUtc) break;
                if (powers[i] > peak - DropPct) continue;

                if (IsSustained(times, powers, i, peak, touchdownUtc)) { cutIndex = i; break; }
            }
            if (cutIndex < 0)
                return PowerCutResult.No(PowerCutStatus.NoDrop, powers.Count, median);

            DateTime cutUtc = times[cutIndex];

            // La última muestra en el aire: la misma definición de toque que usa el tiempo
            // umbral→toma (el salto de `on_ground` falso a verdadero), leída aquí del reloj.
            double atTouchdown = powers[cutIndex];
            for (int i = cutIndex; i < powers.Count; i++)
            {
                if (times[i] > touchdownUtc) break;
                atTouchdown = powers[i];
            }

            double beforeTouchdown = (touchdownUtc - cutUtc).TotalSeconds;
            double afterThreshold  = timeline.ThresholdUtc.HasValue
                                         ? (cutUtc - timeline.ThresholdUtc.Value).TotalSeconds
                                         : double.NaN;

            return PowerCutResult.Ok(
                secondsBeforeTouchdown: Math.Round(beforeTouchdown, 1, MidpointRounding.AwayFromZero),
                secondsAfterThreshold:  double.IsNaN(afterThreshold)
                                            ? double.NaN
                                            : Math.Round(afterThreshold, 1, MidpointRounding.AwayFromZero),
                peakPct:        peak,
                cutPct:         powers[cutIndex],
                atTouchdownPct: atTouchdown,
                engineCount:    bothEngines > 0 ? 2 : 1,
                peakDistFt:     dists[peakIndex],
                cutDistFt:      dists[cutIndex],
                sampleCount:    powers.Count,
                medianSec:      median);
        }

        /// <summary>
        /// ¿La caída se sostiene? Se recorre la ventana de <see cref="SustainSec"/> desde el candidato
        /// —o hasta el toque, si el candidato está más cerca— y se exige que **ninguna** muestra de la
        /// ventana vuelva por encima del umbral. Y hacen falta **al menos dos muestras** en la
        /// ventana: una sola no sostiene nada.
        /// </summary>
        private static bool IsSustained(IList<DateTime> times, IList<double> powers, int from,
                                        double peak, DateTime touchdownUtc)
        {
            DateTime until = times[from].AddSeconds(SustainSec);
            if (until > touchdownUtc) until = touchdownUtc;

            int inWindow = 0;
            for (int j = from; j < times.Count && times[j] <= until; j++)
            {
                if (powers[j] > peak - DropPct) return false;
                inWindow++;
            }
            return inWindow >= 2;
        }

        /// <summary>Traduce el motivo del tiempo umbral→toma al de este helper.</summary>
        private static PowerCutStatus TimelineStatus(ThresholdToTouchdownStatus status)
        {
            switch (status)
            {
                case ThresholdToTouchdownStatus.NoTrack:     return PowerCutStatus.NoTrack;
                case ThresholdToTouchdownStatus.CoarseTrack: return PowerCutStatus.CoarseTrack;
                default:                                     return PowerCutStatus.NoTimeline;
            }
        }

        private static bool Finite(double? value)
            => value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);

        /// <summary>
        /// **El dato tal como viaja al `notes` del PIREP**: `PWR-CUT 4.2s`, en cultura invariante y en
        /// ASCII, como el `THR-TD`. `null` cuando no hay dato, para que el llamante no concatene nada.
        /// </summary>
        internal static string PirepSuffix(PowerCutResult result)
        {
            if (result == null || !result.HasValue) return null;
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:0.0}s",
                                 PirepPrefix, result.SecondsBeforeTouchdown);
        }

        /// <summary>El valor con su unidad, para la consola y los tests: `4.2 s` (invariante).</summary>
        internal static string FormatSeconds(double seconds)
            => string.Format(CultureInfo.InvariantCulture, "{0:0.0} s", seconds);
    }
}
