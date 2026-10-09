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

        /// <summary>
        /// ¿Hay alguna IAS en la traza? **Sin ella el eje de velocidad no se fija** y se queda en
        /// automático: fijarlo en un rango inventado sería dibujar una escala que la traza no sostiene.
        /// </summary>
        internal bool HasSpeed { get; private set; }

        /// <summary>Suelo y techo del eje de pitch, redondeados a 1°.</summary>
        internal double PitchFloorDeg { get; private set; }

        /// <inheritdoc cref="PitchFloorDeg"/>
        internal double PitchTopDeg { get; private set; }

        /// <summary>¿Hay algún pitch utilizable? Sin él, el gráfico se queda en altitud y velocidad.</summary>
        internal bool HasPitch { get; private set; }

        /// <summary>
        /// ¿Hay algún porcentaje de flaps en la traza? **Sin él no se añade un área vacía**: la
        /// ventana del flare se queda como estaba y no se le enseña al piloto una fila plana que no
        /// dice nada.
        /// </summary>
        internal bool HasFlaps { get; private set; }

        /// <summary>Suelo del eje de flaps, en porcentaje del mando.</summary>
        internal double FlapFloorPct { get; private set; }

        /// <inheritdoc cref="FlapFloorPct"/>
        internal double FlapTopPct { get; private set; }

        /// <summary>
        /// **Los flaps del aterrizaje**: el ajuste en el umbral, el de la toma y si cambió por el
        /// camino. Es lo que explica un flotado largo —bajar el último punto de flap en corta final
        /// cambia el asiento del avión cuando ya no hay altura para volver a asentarlo—, así que viaja
        /// con el gráfico y no solo con la línea de datos: el resumen del panel lo pinta.
        /// </summary>
        internal FlapTrackSummary Flaps { get; private set; }

        /// <summary>
        /// ¿Hay algún N1 en la traza? Igual que con los flaps: sin potencia no se añade un área vacía.
        /// </summary>
        internal bool HasPower { get; private set; }

        /// <summary>Suelo y techo del eje de N1, redondeados a 5 puntos.</summary>
        internal double PowerFloorPct { get; private set; }

        /// <inheritdoc cref="PowerFloorPct"/>
        internal double PowerTopPct { get; private set; }

        /// <summary>
        /// **Dónde se cortó la potencia.** El gráfico marca ese instante sobre la traza y el resumen
        /// publica los segundos antes de la toma: es la otra mitad de la explicación de un flotado
        /// largo —llegar al umbral todavía con empuje—.
        /// </summary>
        internal PowerCutResult Power { get; private set; }

        // ── Las marcas, heredadas del closeup ─────────────────────────────────────

        /// <summary>El mismo <see cref="TouchdownCloseup"/> que pinta el perfil vertical.</summary>
        internal TouchdownCloseup Closeup { get; private set; }

        /// <summary>Resumen de una línea para el panel del formulario.</summary>
        internal string Summary { get; private set; }

        internal static FlareChartLayout Build(IList<FlareTrackPoint> samples,
                                               double runwayLengthFt, bool everStarted,
                                               string aircraftFamily = null)
        {
            var layout = new FlareChartLayout
            {
                AltitudeFloorFt = -50.0,
                AltitudeSource  = FlareAltitudeSource.None,
                SampleCount     = samples != null ? samples.Count : 0,
                EverStarted     = everStarted,
                // Los dos resúmenes se calculan siempre, incluso sin muestras: devuelven «no hay
                // traza» y el formulario no tiene que comprobar nada antes de preguntarles.
                Flaps           = FlapTrackSummary.Compute(samples, aircraftFamily),
                Power           = PowerCut.Compute(samples),
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
            double flapMin = double.MaxValue, flapMax = double.MinValue;
            bool hasFlaps = false;
            double powerMin = double.MaxValue, powerMax = double.MinValue;
            bool hasPower = false;

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

                // Los flaps y la potencia solo cuentan cuando el dato existe de verdad: un `null` no
                // es un cero, y meterlo en el eje pintaría una fila en el suelo que el avión no tuvo.
                if (Finite(s.FlapsPct))
                {
                    hasFlaps = true;
                    if (s.FlapsPct.Value < flapMin) flapMin = s.FlapsPct.Value;
                    if (s.FlapsPct.Value > flapMax) flapMax = s.FlapsPct.Value;
                }

                // La potencia del eje es la **media de los motores que publican dato**, la misma que
                // usa `PowerCut` para decidir el corte: si el gráfico pintara otra cosa que la que
                // decide la marca, el piloto vería la raya del corte fuera de la curva.
                double? power = MeanPower(s);
                if (power.HasValue)
                {
                    hasPower = true;
                    if (power.Value < powerMin) powerMin = power.Value;
                    if (power.Value > powerMax) powerMax = power.Value;
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

            // ── El eje de velocidad ───────────────────────────────────────────────
            // **Con la velocidad constante no hay rango, y un eje de 0 a 0 deja el área sin escala.**
            // No es cosmético: el motor de gráficos no puede pintar un área cuyo `AxisY` tiene el
            // mínimo igual al máximo, y el resultado es que **todo el gráfico sale en blanco** —lo
            // destapó el volcado a PNG del caso con flaps y N1, cuya traza lleva la IAS fija del
            // tramo—. Cuando la serie no se mueve (o solo hay una muestra) se abre el eje ±20 kt
            // alrededor, que es lo que mide la maniobra en un flare; y **sin ninguna IAS** se deja el
            // eje en automático en vez de inventar un rango.
            if (speedMax > speedMin)
            {
                layout.HasSpeed     = true;
                layout.SpeedFloorKt = Math.Floor((speedMin - 5.0) / 10.0) * 10.0;
                layout.SpeedTopKt   = Math.Ceiling((speedMax + 5.0) / 10.0) * 10.0;
            }
            else if (speedMax > double.MinValue)
            {
                layout.HasSpeed     = true;
                layout.SpeedFloorKt = Math.Floor((speedMax - 20.0) / 10.0) * 10.0;
                layout.SpeedTopKt   = Math.Ceiling((speedMax + 20.0) / 10.0) * 10.0;
            }
            else
            {
                layout.HasSpeed     = false;
                layout.SpeedFloorKt = 0.0;
                layout.SpeedTopKt   = 0.0;
            }

            layout.HasPitch = hasPitch;
            if (hasPitch)
            {
                layout.PitchFloorDeg = Math.Min(0.0, Math.Floor(pitchMin) - 1.0);
                layout.PitchTopDeg   = Math.Max(0.0, Math.Ceiling(pitchMax) + 1.0);
            }

            // El eje de flaps va de 0 a 100 **siempre**: la escala del mando es absoluta y conocida
            // (`0x0BDC` → 0–16383 → 0–100 %), así que autoescalarlo mentiría sobre cuánto flap llevaba
            // el avión —un eje 80–82 % haría parecer que iba a la mitad—. Una fila plana en el 30 % no
            // es un gráfico mal escalado: es que los flaps no se movieron, que es el dato.
            layout.HasFlaps     = hasFlaps;
            layout.FlapFloorPct = 0.0;
            layout.FlapTopPct   = 100.0;

            // La potencia sí se aprieta a lo que se ve —en el flare el N1 se mueve 20–30 puntos—,
            // redondeado a 5 y con el cero **fuera** salvo que la traza llegue de verdad ahí: un eje
            // desde 0 aplastaría la caída que se viene a mirar.
            layout.HasPower = hasPower;
            if (hasPower)
            {
                layout.PowerFloorPct = Math.Max(0.0, Math.Floor(powerMin / 5.0) * 5.0);
                layout.PowerTopPct   = Math.Min(100.0, Math.Ceiling(powerMax / 5.0) * 5.0);
                if (layout.PowerTopPct - layout.PowerFloorPct < 10.0)
                {
                    // Una traza plana (un motor a ralentí constante) necesita un eje con algo de aire:
                    // si no, la línea cae justo en el borde y parece cortada.
                    layout.PowerFloorPct = Math.Max(0.0, layout.PowerFloorPct - 5.0);
                    layout.PowerTopPct   = Math.Min(100.0, layout.PowerTopPct + 5.0);
                }
            }

            layout.HasData = true;
            layout.Summary = BuildSummary(layout, samples.Count);
            return layout;
        }

        /// <summary>`null` no es un número: solo lo finito entra en un eje.</summary>
        private static bool Finite(double? value)
            => value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);

        /// <summary>
        /// La potencia de una muestra: la **media de los motores que publican dato**, exactamente el
        /// mismo cálculo que hace <see cref="PowerCut"/> para decidir dónde está el corte. Está aquí
        /// duplicado a propósito y no es duplicación de regla: es la misma definición, y si el gráfico
        /// pintara una sola de las dos series el eje no describiría la curva sobre la que se marca el
        /// corte. `null` cuando no hay ningún motor.
        /// </summary>
        private static double? MeanPower(FlareTrackPoint s)
        {
            double sum = 0.0; int n = 0;
            if (Finite(s.Eng1Pct)) { sum += s.Eng1Pct.Value; n++; }
            if (Finite(s.Eng2Pct)) { sum += s.Eng2Pct.Value; n++; }
            return n == 0 ? (double?)null : sum / n;
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
