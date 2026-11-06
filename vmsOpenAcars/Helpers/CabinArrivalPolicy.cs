using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Cuándo se anuncia «desarmar toboganes».** Hasta v0.9.65 se disparaba al despejar la pista
    /// (`Helpers/CabinRunwayExitPolicy`), y esa señal es **sensible al rumbo**: el detector
    /// `NavDataService.FindRunwayEntry` declara «fuera de pista» en cuanto el rectángulo de la pista
    /// no cubre la posición.
    ///
    /// Medido en el PIREP real `3b0oBlXD5ZolKPM4` (AT76, SKUC→SKBO, 05/11/2026): `TOUCHDOWN`
    /// 03:24:43 · `── TAXI IN ──` 03:25:29 · `↩️ BACKTRACK RWY 32R` **03:25:31** (el avión seguía en
    /// la pista, retrocediendo) · `🔊 PA slides_disarm` **03:25:37** · `↳ TWY A6` 03:25:39 (aquí sale
    /// de verdad). Al girar para el retroceso el rumbo pasó a 158° contra los 127° de la 14L y la
    /// posición salió del rectángulo: la pista quedaba por delante y el aviso sonó encima de ella.
    ///
    /// La señal buena no es geométrica sino operativa: **el freno de parqueo puesto con el avión ya
    /// rodando la llegada** (`TaxiIn`, que solo se alcanza después de aterrizar). En este vuelo fue
    /// **03:28:57**, con el avión ya en el puesto. `taxi_in` no cambia: sigue saliendo al despejar la
    /// pista con `CabinRunwayExitPolicy`.
    ///
    /// **El freno también se pone tras el pushback** (en este mismo vuelo, **01:57:51**, fase
    /// `Pushback`) y no puede anunciar la llegada: por eso la fase es condición, no un detalle.
    /// </summary>
    internal static class CabinArrivalPolicy
    {
        /// <summary>
        /// ¿Toca anunciar el desarme de toboganes? <paramref name="hasLanded"/> es «hubo aterrizaje
        /// en este vuelo» (`FlightManager.HasTouchedDownThisFlight`), que distingue la llegada real
        /// de un `TaxiIn` prematuro; <paramref name="parkingBrakeSet"/> es el suceso del freno.
        /// </summary>
        internal static bool ShouldAnnounceSlidesDisarm(
            FlightPhase phase, bool hasLanded, bool parkingBrakeSet, bool alreadySent)
        {
            if (alreadySent) return false;
            if (!hasLanded) return false;
            if (phase != FlightPhase.TaxiIn) return false;
            return parkingBrakeSet;
        }
    }
}
