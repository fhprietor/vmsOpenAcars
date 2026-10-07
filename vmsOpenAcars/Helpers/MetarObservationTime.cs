using System;
using System.Globalization;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Hora de observación del METAR**, en UTC.
    ///
    /// El METAR la lleva dentro: el grupo `ddHHMMZ` (`SKBO 250100Z` = día 25 a las 01:00 UTC). Ni
    /// NavData (`Models/NavData.NavWeather`) ni el respaldo de aviationweather publican un campo
    /// aparte que usemos —`GetWeatherAsync` solo da `raw_metar` y los valores ya descompuestos—, así
    /// que la fuente fiable y común a los dos caminos es el propio texto. Guardarla permite ordenar
    /// y comparar aterrizajes por la hora de la observación y no por la del `INSERT`.
    ///
    /// **El año y el mes no están en el METAR.** Se anclan a la fecha de captura: el día del grupo
    /// se interpreta dentro de ese mes y, si sale en el futuro por más de un día, la observación es
    /// del **mes anterior** (el caso real: capturar a las 00:20 del 1 de octubre un METAR del
    /// `302350Z`). Es la única ambigüedad que el formato deja y se resuelve del lado conservador: un
    /// dato de hace minutos, no de dentro de un mes.
    ///
    /// Degrada sin datos: sin grupo válido devuelve `null`, y quien lo consuma guarda el METAR en
    /// crudo igual (el texto ya lleva la hora a la vista).
    /// </summary>
    internal static class MetarObservationTime
    {
        /// <summary>
        /// Hora de observación (UTC) del METAR en crudo, o `null` si no trae un grupo `ddHHMMZ`
        /// utilizable. <paramref name="referenceUtc"/> es el instante de captura, que aporta año y mes.
        /// </summary>
        internal static DateTime? Parse(string rawMetar, DateTime referenceUtc)
        {
            if (string.IsNullOrWhiteSpace(rawMetar)) return null;

            string[] parts = rawMetar.Split(new[] { ' ', '\t', '\r', '\n' },
                                            StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in parts)
            {
                string token = raw.TrimEnd('=');
                if (token.Length != 7 || token[6] != 'Z') continue;
                if (!IsAllDigits(token, 6)) continue;

                int day    = int.Parse(token.Substring(0, 2), CultureInfo.InvariantCulture);
                int hour   = int.Parse(token.Substring(2, 2), CultureInfo.InvariantCulture);
                int minute = int.Parse(token.Substring(4, 2), CultureInfo.InvariantCulture);

                if (day < 1 || day > 31 || hour > 23 || minute > 59) continue;

                var utc = new DateTime(referenceUtc.Year, referenceUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                if (day > DateTime.DaysInMonth(utc.Year, utc.Month)) continue;

                var observed = new DateTime(utc.Year, utc.Month, day, hour, minute, 0, DateTimeKind.Utc);

                // El grupo solo trae el día: una observación «en el futuro» por más de un día no
                // existe, es del mes anterior. (Margen de 1 día por desajustes de reloj.)
                if ((observed - referenceUtc).TotalDays > 1.0)
                {
                    observed = observed.AddMonths(-1);
                    // El día puede no existir en el mes anterior (31 → febrero): se degrada a null
                    // en vez de desplazarlo a otro día, que sería inventar la observación.
                    if (observed.Day != day) return null;
                }

                return observed;
            }

            return null;
        }

        private static bool IsAllDigits(string s, int count)
        {
            if (s.Length < count) return false;
            for (int i = 0; i < count; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }
    }
}
