using System;
using System.Collections.Generic;
using System.Globalization;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El encuadre y las marcas del gráfico del flare**: qué tramo se ve, hasta dónde llega cada
    /// eje y dónde caen las marcas que se comparten con el closeup.
    ///
    /// Es un helper puro, sin WinForms, como <see cref="TouchdownCloseupGeometry"/>: el formulario
    /// solo traduce estos números a series y ejes. **No duplica la geometría de la pista, el umbral,
    /// las bandas de la zona de toma ni el punto de toque**: delega en
    /// <see cref="TouchdownCloseupGeometry.Compute"/> y expone el <see cref="TouchdownCloseup"/>
    /// resultante, que es el mismo objeto que pinta el closeup del perfil vertical. Si allí cambia un
    /// criterio, este gráfico cambia con él.
    ///
    /// **El encuadre sale de la traza del flare, no de una escala fija de la casa**: la captura se
    /// arma a 1 500 ft del umbral (o 1 000 ft AGL de respaldo), así que lo que hay que ver es
    /// exactamente lo que se capturó. Un encuadre fijo de 5 000 ft dejaría dos tercios del gráfico
    /// vacíos en todos los vuelos.
    ///
    /// **Sin muestras no hay gráfico**: `HasData` en falso y el formulario dice que no hay traza
    /// fina de ese aterrizaje. Los vuelos viejos no tienen `flare_track` y no se rellenan con
    /// `approach_track`, que sería vender 2 s como si fueran 0,1 s.
    /// </summary>
    internal sealed class FlareChartLayout
    {
        private FlareChartLayout() { }

        /// <summary>Hay muestras de flare: se puede pintar.</summary>
        internal bool HasData { get; private set; }

        /// <summary>Número de muestras de la traza.</summary>
        internal int SampleCount { get; private set; }

        /// <summary>La captura de alta frecuencia está armada (se ve si llegó a haber flare).</summary>
        internal bool EverStarted { get; private set; }

        /// <summary>X del borde izquierdo del encuadre, en pies (positivo = antes del umbral).</summary>
        internal double BeforeFt { get; private set; }

        /// <summary>Pies vistos pasados el umbral (borde derecho del encuadre).</summary>
        internal double AfterFt { get; private set; }

        // ── Los ejes ──────────────────────────────────────────────────────────────

        /// <summary>Suelo del eje de altitud: −50 ft, para que la banda de pista se vea entera.</summary>
        internal double AltitudeFloorFt { get; private set; }

        /// <summary>Techo del eje de altitud: la muestra más alta del encuadre con un 10 % de aire.</summary>
        internal double AltitudeTopFt { get; private set; }

        /// <summary>Fuente de altitud que se usa de verdad en el gráfico.</summary>
        internal FlareAltitudeSource AltitudeSource { get; private set; }

        /// <summary>Suelo y techo del eje de velocidad, redondeados a 10 kt.</summary>
        internal double SpeedFloorKt { get; private set; }

        /// <inheritdoc cref="SpeedFloorKt"/>
        internal double SpeedTopKt { get; private set; }

        /// <summary>Suelo y techo del eje de pitch, redondeados a 1°.</summary>
        internal double PitchFloorDeg { get; private set; }

        /// <inheritdoc cref="PitchFloorDeg"/>
        internal double PitchTopDeg { get; private set; }

        /// <summary>¿Hay algún pitch utilizable? Sin él, el gráfico se queda en altitud y velocidad.</summary>
        internal bool HasPitch { get; private set; }

        // ── Las marcas, heredadas del closeup ─────────────────────────────────────

        /// <summary>El mismo <see cref="TouchdownCloseup"/> que pinta el perfil vertical.</summary>
        internal TouchdownCloseup Closeup { get; private set; }

        /// <summary>Resumen de una línea para el panel del formulario.</summary>
        internal string Summary { get; private set; }

        internal static FlareChartLayout Build(IList<FlareTrackPoint> samples,
                                               double runwayLengthFt, bool everStarted)
        {
            var layout = new FlareChartLayout
            {
                AltitudeFloorFt = -50.0,
                AltitudeSource  = FlareAltitudeSource.None,
                SampleCount     = samples != null ? samples.Count : 0,
                EverStarted     = everStarted,
            };

            if (samples == null || samples.Count == 0)
            {
                layout.HasData = false;
                layout.BeforeFt = 0.0;
                layout.AfterFt  = 0.0;
                layout.Closeup  = TouchdownCloseupGeometry.Compute(0.0, runwayLengthFt, 0.0, 0.0);
                layout.Summary  = "No flare track for this landing";
                return layout;
            }

            double before = 0.0;
            double after  = 0.0;   // pies pasados el umbral que entran en el encuadre
            bool hasRadar = false, hasAgl = false, hasPitch = false;
            double tdDistFt = 0.0;
            bool hasTouchdown = false;

            double altTop = 0.0;
            double speedMin = double.MaxValue, speedMax = double.MinValue;
            double pitchMin = double.MaxValue, pitchMax = double.MinValue;

            foreach (var s in samples)
            {
                if (s == null) continue;

                if (s.DistFt > before) before = s.DistFt;
                // Pasado el umbral el gráfico solo llega hasta donde llegó la captura: 1–2 s de
                // frenada, no los 4 500 ft de pista que pinta el closeup.
                if (s.DistFt < 0.0 && -s.DistFt > after) after = -s.DistFt;

                // El toque es la última muestra en tierra: es el eje temporal del gráfico.
                if (s.OnGround)
                {
                    hasTouchdown = true;
                    tdDistFt     = s.DistFt;
                }

                double? alt = s.RadarAltFt ?? s.AglFt;
                if (alt.HasValue && alt.Value > altTop) altTop = alt.Value;
                if (s.RadarAltFt.HasValue) hasRadar = true;
                if (s.AglFt.HasValue)      hasAgl   = true;

                if (s.IasKt.HasValue)
                {
                    if (s.IasKt.Value < speedMin) speedMin = s.IasKt.Value;
                    if (s.IasKt.Value > speedMax) speedMax = s.IasKt.Value;
                }
                if (s.PitchDeg.HasValue)
                {
                    hasPitch = true;
                    if (s.PitchDeg.Value < pitchMin) pitchMin = s.PitchDeg.Value;
                    if (s.PitchDeg.Value > pitchMax) pitchMax = s.PitchDeg.Value;
                }
            }

            // ── El encuadre en X ──────────────────────────────────────────────────
            // El encuadre sale de la traza (1 500 ft capturados, más la frenada) y se redondea
            // **hacia fuera** a la centena (`CloseupAxis.RoundFrameOut`): hacia dentro recortaría
            // muestras, y la centena es el redondeo más fino que mantiene los rótulos en pies enteros.
            // El paso de las marcas lo elige luego `CloseupAxis.StepFor` sobre este encuadre.
            double beforeAligned = before, afterAligned = Math.Max(after, 0.0);
            CloseupAxis.RoundFrameOut(ref beforeAligned, ref afterAligned);
            layout.BeforeFt = beforeAligned;
            layout.AfterFt  = afterAligned;

            if (layout.BeforeFt <= 0.0)
            {
                // Toda la captura fue pasada el umbral (arma por AGL con el avión ya encima del
                // umbral): se enseña el tramo real y un poco de margen por delante.
                double fallbackBefore = 200.0, fallbackAfter = layout.AfterFt;
                CloseupAxis.RoundFrameOut(ref fallbackBefore, ref fallbackAfter);
                layout.BeforeFt = fallbackBefore <= 0.0 ? 200.0 : fallbackBefore;
                layout.AfterFt  = fallbackAfter;
            }

            // ── Las marcas: las mismas del closeup del perfil vertical ────────────
            // **El signo se da la vuelta aquí**: `flare_track.dist_ft` es positivo **antes** del
            // umbral y negativo después (el eje del gráfico), mientras que `TouchdownCloseupGeometry`
            // —y `flights.touchdown_dist_ft`— usan la distancia de toma **en positivo**, pasada la
            // pista. Pasarle el −1 650 tal cual lo tomaba por un valor no utilizable (exige `> 0`) y
            // el gráfico se quedaba sin punto de toque. Es el único punto del helper donde se cruzan
            // las dos convenciones, así que la conversión vive aquí y no en el modelo.
            double touchdownDistanceFt = hasTouchdown ? -tdDistFt : 0.0;
            layout.Closeup = TouchdownCloseupGeometry.Compute(
                touchdownDistanceFt,
                runwayLengthFt,
                layout.BeforeFt, layout.AfterFt);

            // ── Los ejes verticales ───────────────────────────────────────────────
            layout.AltitudeSource = hasRadar ? FlareAltitudeSource.RadarAltimeter
                                             : hasAgl ? FlareAltitudeSource.Agl
                                                      : FlareAltitudeSource.None;
            // AGL negativo después del toque no se pinta: el suelo es el suelo.
            layout.AltitudeTopFt = Math.Max(50.0, Math.Ceiling(altTop / 50.0) * 50.0);

            if (speedMax > speedMin)
            {
                layout.SpeedFloorKt = Math.Floor((speedMin - 5.0) / 10.0) * 10.0;
                layout.SpeedTopKt   = Math.Ceiling((speedMax + 5.0) / 10.0) * 10.0;
            }
            else
            {
                layout.SpeedFloorKt = 0.0;
                layout.SpeedTopKt   = 0.0;
            }

            layout.HasPitch = hasPitch;
            if (hasPitch)
            {
                layout.PitchFloorDeg = Math.Min(0.0, Math.Floor(pitchMin) - 1.0);
                layout.PitchTopDeg   = Math.Max(0.0, Math.Ceiling(pitchMax) + 1.0);
            }

            layout.HasData = true;
            layout.Summary = BuildSummary(layout, samples.Count);
            return layout;
        }

        private static string BuildSummary(FlareChartLayout l, int count)
        {
            string alt = l.AltitudeSource == FlareAltitudeSource.RadarAltimeter ? "RA"
                       : l.AltitudeSource == FlareAltitudeSource.Agl             ? "AGL"
                                                                                 : "sin altitud";
            return string.Format(CultureInfo.InvariantCulture,
                "{0} samples · {1} | {2:N0} ft → +{3:N0} ft | {4}",
                count, alt, l.BeforeFt, l.AfterFt, l.Closeup != null ? l.Closeup.Summary : "");
        }
    }

    /// <summary>De dónde sale la altitud que pinta el gráfico del flare.</summary>
    internal enum FlareAltitudeSource
    {
        /// <summary>Ni radioaltímetro ni AGL: no hay altitud que pintar.</summary>
        None,
        /// <summary>Radioaltímetro (FSUIPC `0x31E4`), el instrumento bueno para el flare.</summary>
        RadarAltimeter,
        /// <summary>AGL calculado del altímetro y la elevación del campo.</summary>
        Agl,
    }
}
