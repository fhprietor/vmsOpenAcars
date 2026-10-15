namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Cuánto tiempo se puede sumar de un ciclo de telemetría.** Los contadores que miden tiempo
    /// (el rodaje con un solo motor y el avión parado en una fase de vuelo) acumulan el hueco entre
    /// dos ciclos, y ese hueco **no siempre es de fiar**: si el portátil se suspende a mitad de vuelo,
    /// el salto entre dos muestras puede ser de horas, y contarlo regalaría minutos de rodaje —o de
    /// «fase atascada»— que no se volaron.
    ///
    /// El sondeo es de **20 Hz** (`polling_interval_ms = 50`), así que cinco segundos ya son cien
    /// ciclos perdidos: eso no es una muestra, es una interrupción. Un hueco así vale **cero**, y el
    /// mismo criterio para el reloj movido hacia atrás.
    ///
    /// Está aparte de las dos reglas que lo usan a propósito: «qué hueco es creíble» es una propiedad
    /// del bucle de telemetría, no del criterio de motor único ni del de la fase atascada.
    /// </summary>
    internal static class SampleClock
    {
        /// <summary>Hueco máximo entre dos ciclos que se puede sumar, en segundos.</summary>
        internal const double MaxGapSeconds = 5.0;

        /// <summary>
        /// Segundos válidos de un ciclo: el propio hueco, o **cero** si es negativo (reloj movido
        /// hacia atrás) o mayor que <see cref="MaxGapSeconds"/>.
        /// </summary>
        internal static double CountableSeconds(double deltaSeconds)
            => deltaSeconds > 0.0 && deltaSeconds <= MaxGapSeconds ? deltaSeconds : 0.0;
    }
}
