using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Cuándo se anuncia la llegada** — la orden de cabina «desarmar toboganes y verificar» y, tras
    /// ella, la bienvenida a destino. Hasta v0.9.56 el primero iba en la entrada de `AfterLanding`,
    /// que se declara **en el mismo tick del toque**: se oía con el avión a 145 kt en la pista.
    ///
    /// Medido en el PIREP real `r4JNk2b2bBMJd8lD`: toque **02:38:28** (145 kt), `── AFTER LANDING ──`
    /// 02:38:29, `🔊 PA slides_disarm` **02:38:37** y `── TAXI IN ──` (GS < 40 kt) 02:38:51 — todavía
    /// rodando por la pista, con las reversas al 100 %.
    ///
    /// La condición buena es **estar fuera de la pista**: el avión ya aterrizó, va a velocidad de
    /// rodaje y el detector de pista de NavData (`NavDataService.FindRunwayEntry`, el mismo que usa
    /// el RAAS) dice que no está sobre ninguna. Esa señal **degrada sin datos**: sin NavData o sin
    /// aeropuerto de llegada (`onRunway == null`) se anuncia igual al cumplirse las otras dos, que es
    /// lo que hacía la regla anterior — el piloto no se queda sin el anuncio por un fallo de red.
    /// </summary>
    internal static class CabinRunwayExitPolicy
    {
        /// <summary>
        /// La carrera de aterrizaje llega a 140-150 kt y la salida de pista ronda los 20-30: por
        /// encima de 40 kt no puede ser un rodaje, así que ni se pregunta por la pista.
        /// </summary>
        internal const double MaxRolloutKt = 40.0;

        /// <summary>
        /// ¿Es el momento de la llegada? <paramref name="onRunway"/> es <c>null</c> cuando no se pudo
        /// saber (sin NavData, sin aeropuerto de llegada): entonces no bloquea.
        /// </summary>
        internal static bool IsClearOfRunway(FlightPhase phase, double groundSpeedKt, bool? onRunway)
        {
            if (phase != FlightPhase.AfterLanding && phase != FlightPhase.TaxiIn) return false;
            if (groundSpeedKt > MaxRolloutKt) return false;

            // `!= true` y no `== false`: sin dato se anuncia (degradar hacia el piloto informado).
            return onRunway != true;
        }
    }
}
