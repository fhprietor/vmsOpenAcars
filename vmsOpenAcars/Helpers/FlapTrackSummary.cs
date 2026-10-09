using System;
using System.Collections.Generic;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Los flaps de un aterrizaje, leídos de la traza fina del flare**: qué ajuste había en el
    /// cruce del umbral, qué ajuste había en la toma y si cambió por el camino.
    ///
    /// Es la pregunta que explica un flotado largo —un piloto que baja el último punto de flap en
    /// corta final cambia el asiento del avión justo cuando ya no hay altura para volver a
    /// asentarlo—, y se contesta donde está el dato: la traza de 10 Hz tiene **60–80 muestras** para
    /// los últimos 1 500 ft, así que un movimiento del mando se ve.
    ///
    /// Función pura, sin WinForms y sin base de datos, como <see cref="ThresholdToTouchdown"/>.
    /// **Degrada sin datos**: sin muestras con `flaps_pct` no hay nada que decir (`HasTrack` en falso),
    /// y sin transición aire→tierra en la traza **no se inventa** el ajuste de la toma: se deja en
    /// `null` y la pantalla enseña solo el del umbral.
    ///
    /// **El umbral y el contacto se cogen de la muestra más cercana, no se interpolan**, al contrario
    /// que el tiempo umbral→toma: aquí lo que se busca es un **valor discreto** —la compuerta que
    /// estaba puesta— y promediar dos compuertas daría una tercera que no existía. A 10 Hz la muestra
    /// más cercana al cruce está a 25 ft, muy por debajo de la separación entre compuertas.
    /// </summary>
    internal sealed class FlapTrackSummary
    {
        private FlapTrackSummary() { }

        /// <summary>Hay al menos una muestra con `flaps_pct`.</summary>
        internal bool HasTrack { get; private set; }

        /// <summary>Cuántas muestras traían porcentaje de flaps.</summary>
        internal int SampleCount { get; private set; }

        /// <summary>El ajuste en el cruce del umbral. `null` solo si no hay traza de flaps.</summary>
        internal FlapSetting AtThreshold { get; private set; }

        /// <summary>
        /// El ajuste en la toma (la última muestra **en el aire**). `null` si la traza no llegó a
        /// tener una transición aire→tierra: sin toque no hay ajuste de toque.
        /// </summary>
        internal FlapSetting AtTouchdown { get; private set; }

        /// <summary>Cambió el ajuste entre el umbral y la toma (más de <see cref="ChangeHysteresisPct"/>).</summary>
        internal bool Changed { get; private set; }

        /// <summary>Cambió en algún punto de la traza capturada, umbral incluido.</summary>
        internal bool ChangedInCapture { get; private set; }

        /// <summary>Recorrido del mando en la captura, en puntos porcentuales. `NaN` sin datos.</summary>
        internal double MinPercent { get; private set; }

        /// <inheritdoc cref="MinPercent"/>
        internal double MaxPercent { get; private set; }

        /// <summary>
        /// **Hasta cuánto tiene que moverse el mando para contar como un cambio: 1 punto.** No es un
        /// número nuevo: es la **histéresis de 1 %** que ya usa el detector de cambios de flaps de
        /// `FsuipcService` (`DetectFlapsChange`, documentada en `Docs/architecture.md`), así que la
        /// traza fina dice «cambió» exactamente cuando el log de vuelo lo dijo.
        /// </summary>
        internal const double ChangeHysteresisPct = 1.0;

        internal static FlapTrackSummary Compute(IList<FlareTrackPoint> samples, string aircraftFamily)
        {
            var usable = new List<FlareTrackPoint>();
            if (samples != null)
            {
                foreach (var s in samples)
                {
                    if (s == null || !s.FlapsPct.HasValue) continue;
                    if (double.IsNaN(s.FlapsPct.Value) || double.IsInfinity(s.FlapsPct.Value)) continue;
                    usable.Add(s);
                }
            }

            var summary = new FlapTrackSummary
            {
                HasTrack    = usable.Count > 0,
                SampleCount = usable.Count,
                MinPercent  = double.NaN,
                MaxPercent  = double.NaN,
            };
            if (!summary.HasTrack) return summary;

            double min = double.MaxValue, max = double.MinValue;
            foreach (var s in usable)
            {
                if (s.FlapsPct.Value < min) min = s.FlapsPct.Value;
                if (s.FlapsPct.Value > max) max = s.FlapsPct.Value;
            }
            summary.MinPercent = min;
            summary.MaxPercent = max;
            summary.ChangedInCapture = (max - min) > ChangeHysteresisPct;

            // ── El umbral: la última muestra todavía delante de él ────────────────
            // El signo de `DistFt` es el de la traza (positivo antes, negativo después). Si la
            // captura se armó con el avión ya pasado el umbral no hay muestra «antes»: se toma la
            // primera, que es lo más cerca del umbral que se llegó, en vez de dejar el dato vacío.
            FlareTrackPoint atThreshold = usable[0];
            for (int i = 0; i < usable.Count; i++)
            {
                if (usable[i].DistFt >= 0.0) atThreshold = usable[i];
                else break;
            }
            summary.AtThreshold = FlapSetting.Interpret(atThreshold.FlapsPct, aircraftFamily,
                                                        atThreshold.FlapsIndex);

            // ── El contacto: la última muestra en el aire ─────────────────────────
            // Es la misma transición que usa `ThresholdToTouchdown` para el tiempo, leída igual: el
            // salto `on_ground` de falso a verdadero. La muestra del salto ya es «en tierra» y su
            // flaps puede ser el del rebote, así que el ajuste de la toma es el de la anterior.
            for (int i = 1; i < usable.Count; i++)
            {
                if (usable[i - 1].OnGround || !usable[i].OnGround) continue;
                summary.AtTouchdown = FlapSetting.Interpret(usable[i - 1].FlapsPct, aircraftFamily,
                                                            usable[i - 1].FlapsIndex);
                break;
            }

            if (summary.AtThreshold != null && summary.AtTouchdown != null)
                summary.Changed = Math.Abs(summary.AtTouchdown.Percent - summary.AtThreshold.Percent)
                                  > ChangeHysteresisPct;

            return summary;
        }
    }
}
