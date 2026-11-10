using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **¿Sigue sirviendo la entrada de caché del METAR?** La respuesta la dan dos relojes, no uno.
    ///
    /// El caso medido el 10/11/2026 a las 03:36 UTC: `GET /weather/SKRG/` ya devolvía
    /// `METAR SKRG 100300Z VRB01KT 9999 NSC 14/12 Q1028` y el cliente seguía pintando el de las
    /// **0200Z**; el botón REFRESH no cambiaba nada. La caché de `NavDataClient` solo caducaba por
    /// reloj —TTL de 5 minutos, `DateTime.UtcNow - cached.FetchedAt < _weatherTtl`— y **en todo el
    /// proyecto no existía ningún `Clear`/`Invalidate`/`ForceRefresh` de esa caché**. Con el
    /// temporizador de `MetarService` también en 5 minutos, la ventana entera devolvía la observación
    /// vieja sin salir a la red.
    ///
    /// La regla que faltaba mira la **observación**, no solo la hora de la descarga: si el grupo
    /// `ddHHMMZ` del `raw_metar` es **anterior a la última hora en punto UTC**, la entrada está
    /// caducada aunque se trajera hace 20 segundos. Un METAR horario se publica a las :00, así que a
    /// las 03:00:20 el de las `100200Z` ya no es el vigente y el de las `100300Z` sí.
    ///
    /// **El cambio de día y de mes lo resuelve la comparación entre instantes completos**, no la
    /// hora suelta: `MetarObservationTime.Parse` ancla el `dd` al mes de la captura (y lo manda al
    /// mes anterior si saldría en el futuro), de manera que un `302350Z` capturado a las 00:20 del
    /// día 1 es `30/09 23:50` y queda por debajo de la última hora en punto —las 00:00 del día 1—.
    ///
    /// **Sin grupo de hora no se caduca por esta vía**: `Parse` devuelve `null` y decide el TTL.
    /// Nunca se descarta un dato por una suposición; el TTL de 5 minutos se conserva como red.
    /// </summary>
    internal static class WeatherCacheFreshness
    {
        /// <summary>
        /// Última hora en punto UTC en o por debajo de <paramref name="nowUtc"/>. Es el umbral de la
        /// regla: lo anterior a ese instante es una observación que ya fue reemplazada.
        /// </summary>
        internal static DateTime LastFullHourUtc(DateTime nowUtc)
        {
            DateTime utc = AsUtc(nowUtc);
            return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
        }

        /// <summary>
        /// `true` si el METAR en crudo lleva una observación anterior a la última hora en punto.
        /// Sin grupo de hora válido devuelve `false`: no hay dato con el que caducar.
        /// </summary>
        internal static bool IsObservationStale(string rawMetar, DateTime fetchedAtUtc, DateTime nowUtc)
        {
            // La referencia de `Parse` es la captura (aporta año y mes), no «ahora»: así el anclaje
            // del día no cambia si la entrada se consulta justo después de un cambio de mes.
            DateTime? observed = MetarObservationTime.Parse(rawMetar, AsUtc(fetchedAtUtc));
            if (!observed.HasValue) return false;

            return observed.Value < LastFullHourUtc(nowUtc);
        }

        /// <summary>
        /// Decisión completa de la caché de `NavDataClient`: `true` si se puede devolver la entrada
        /// sin salir a la red. `forceRefresh` la descarta siempre —es el refresco explícito del
        /// piloto y el del temporizador de `MetarService`, que piden red por definición—; si no, hay
        /// que estar dentro del TTL **y** no llevar una observación ya reemplazada.
        /// </summary>
        internal static bool CanUseCached(bool forceRefresh, string rawMetar,
                                          DateTime fetchedAtUtc, DateTime nowUtc, TimeSpan ttl)
        {
            if (forceRefresh) return false;

            DateTime fetched = AsUtc(fetchedAtUtc);
            if (AsUtc(nowUtc) - fetched >= ttl) return false;

            return !IsObservationStale(rawMetar, fetched, nowUtc);
        }

        /// <summary>
        /// Normaliza a UTC. Un `DateTimeKind.Local` se convierte (restaría la zona horaria dos veces
        /// si se comparara tal cual) y un `Unspecified` se interpreta como UTC, que es lo que
        /// publican las dos fuentes del METAR.
        /// </summary>
        private static DateTime AsUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            if (value.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return value;
        }
    }
}
