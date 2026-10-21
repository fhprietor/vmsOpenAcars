using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Hora local del destino para el anuncio de llegada, y cómo se dice en voz alta.
    ///
    /// **No hay zona horaria en ningún dato que tengamos.** NavData publica `lat`/`lon` del
    /// aeropuerto pero ninguna base de tiempos; el METAR tampoco la lleva. La única fuente que sí
    /// está es la **longitud**, y de ahí sale un huso de 15° por hora. El error es real y está
    /// asumido por decisión del mantenedor («no importa si no es fiable»): un grado de longitud son
    /// 4 minutos, y los países con horario de verano van una hora por delante de lo que da esta
    /// cuenta. Se midió con las coordenadas reales de la caché: SKBO −74,1469 → −5 h (Colombia es
    /// UTC−5 todo el año, correcto) y SBGR −46,47 → −3 h (Brasilia, correcto). LEMD −3,59 → 0 h
    /// (en invierno acierta, en verano se queda una hora corta).
    ///
    /// La hora se dice **como la diría la tripulación**, no como un reloj de 24 horas: un pasajero
    /// no entiende «las catorce cuarenta y dos», entiende «las dos de la tarde con cuarenta y dos
    /// minutos». De ahí el reloj de 12 h con franja del día (madrugada, mañana, mediodía, tarde,
    /// noche) y la concordancia del verbo, que es lo que delata a una máquina cuando se escribe
    /// sola: **«Es la una»**, no «Son la una».
    ///
    /// Existe como helper puro, con el reloj por parámetro, para poder probarlo sin esperar a que
    /// sea una hora concreta en Bogotá.
    /// </summary>
    internal static class LocalTimeOfDay
    {
        /// <summary>
        /// Huso aproximado a partir de la longitud: 15° por hora, redondeado hacia fuera en el medio
        /// grado y acotado al rango real de husos (UTC−12 … UTC+14). Sin dato devuelve 0 (UTC).
        /// </summary>
        internal static int OffsetHours(double longitude)
        {
            if (double.IsNaN(longitude) || double.IsInfinity(longitude)) return 0;

            double clamped = Math.Max(-180.0, Math.Min(180.0, longitude));
            int hours = (int)Math.Round(clamped / 15.0, MidpointRounding.AwayFromZero);

            if (hours < -12) hours = -12;
            if (hours > 14)  hours = 14;
            return hours;
        }

        /// <summary>Hora local del destino para un instante UTC. Devuelve solo la hora del día.</summary>
        internal static TimeSpan At(DateTime utcNow, double longitude)
        {
            DateTime local = utcNow.AddHours(OffsetHours(longitude));
            return new TimeSpan(local.Hour, local.Minute, 0);
        }

        /// <summary>
        /// La hora en palabras: «Son las diez de la mañana con cinco minutos» / «Es la una de la
        /// tarde con veinticinco minutos». Devuelve la frase completa, con verbo, para que la
        /// plantilla solo tenga que añadir «, hora local».
        ///
        /// Franjas, por decisión del mantenedor: antes de las 05:00 es **madrugada**, de 05:00 a
        /// 11:59 **mañana**, las 12 **mediodía**, de 13:00 a 19:59 **tarde** y de 20:00 en adelante
        /// **noche**. Sin minutos se dice «en punto» en vez de «con cero minutos».
        /// </summary>
        internal static string Spell(TimeSpan timeOfDay, string language)
        {
            bool es = !string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);

            int hour24  = timeOfDay.Hours;
            int minutes = timeOfDay.Minutes;

            // Reloj de 12 h: 0 y 12 son «doce»; 13 → una, 14 → dos…
            int hour12 = hour24 % 12;
            if (hour12 == 0) hour12 = 12;

            if (es)
            {
                string article  = hour12 == 1 ? "la" : "las";
                string hourWord = hour12 == 1 ? "una" : Number(hour12, true);
                string tail     = minutes == 0
                    ? "en punto"
                    : "con " + MinutesEs(minutes);

                // «Es la una», no «Son la una»: la concordancia es lo que suena a persona.
                string verb = hour12 == 1 ? "Es" : "Son";

                return $"{verb} {article} {hourWord} {DayPartEs(hour24)} {tail}";
            }

            string englishTail = minutes == 0 ? "" : " " + MinutesEn(minutes);
            return $"The local time is {Number(hour12, false)}{englishTail} {DayPartEn(hour24)}";
        }

        /// <summary>Franja del día en español, ya con la contracción resuelta («del mediodía»).</summary>
        private static string DayPartEs(int hour24)
        {
            if (hour24 < 5)  return "de la madrugada";
            if (hour24 < 12) return "de la mañana";
            if (hour24 < 13) return "del mediodía";
            if (hour24 < 20) return "de la tarde";
            return "de la noche";
        }

        private static string DayPartEn(int hour24)
        {
            if (hour24 < 5)  return "at night";
            if (hour24 < 12) return "in the morning";
            if (hour24 < 13) return "at noon";
            if (hour24 < 20) return "in the afternoon";
            return "in the evening";
        }

        /// <summary>«un minuto» en singular; «cinco minutos» con palabra, nunca «5 minutos».</summary>
        private static string MinutesEs(int minutes) =>
            minutes == 1 ? "un minuto" : Number(minutes, true) + " minutos";

        /// <summary>Minutos en inglés: «oh-five» para una cifra, «twenty-five» para dos.</summary>
        private static string MinutesEn(int minutes) =>
            minutes < 10 ? "oh-" + Number(minutes, false) : Number(minutes, false);

        private static readonly string[] SpanishUnits =
        {
            "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve",
            "diez", "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete",
            "dieciocho", "diecinueve", "veinte"
        };

        private static readonly string[] SpanishTwenties =
        {
            "veinte", "veintiuno", "veintidós", "veintitrés", "veinticuatro", "veinticinco",
            "veintiséis", "veintisiete", "veintiocho", "veintinueve"
        };

        private static readonly string[] SpanishTens =
        {
            "", "", "veinte", "treinta", "cuarenta", "cincuenta"
        };

        private static readonly string[] EnglishUnits =
        {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
            "seventeen", "eighteen", "nineteen"
        };

        private static readonly string[] EnglishTens =
        {
            "", "", "twenty", "thirty", "forty", "fifty"
        };

        /// <summary>0…59 en palabras. Fuera de rango se devuelve el número tal cual, que al menos se lee.</summary>
        internal static string Number(int n, bool es)
        {
            if (n < 0 || n > 59) return n.ToString();
            if (es) return Spanish(n);
            return English(n);
        }

        private static string Spanish(int n)
        {
            if (n < SpanishUnits.Length) return SpanishUnits[n];
            if (n < 30) return SpanishTwenties[n - 20];

            int tens = n / 10;
            int unit = n % 10;
            if (unit == 0) return SpanishTens[tens];
            return SpanishTens[tens] + " y " + SpanishUnits[unit];
        }

        private static string English(int n)
        {
            if (n < EnglishUnits.Length) return EnglishUnits[n];

            int tens = n / 10;
            int unit = n % 10;
            if (unit == 0) return EnglishTens[tens];
            return EnglishTens[tens] + "-" + EnglishUnits[unit];
        }
    }
}
