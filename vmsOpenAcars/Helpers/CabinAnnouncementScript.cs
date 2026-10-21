using System;
using System.Collections.Generic;
using System.Text;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Datos de vuelo disponibles para locutar un anuncio de cabina.
    ///
    /// Todos son opcionales **por diseño**: la librería de frases no puede depender de que el
    /// aeropuerto esté en la caché de NavData ni de que el METAR haya respondido. Cuando un dato
    /// falta, la frase que lo usa desaparece y el anuncio sigue teniendo sentido — nunca se lee un
    /// hueco ni un «con destino .».
    /// </summary>
    internal sealed class CabinAnnouncementContext
    {
        /// <summary>Nombre de la aerolínea tal como lo publica phpVMS (`Pilot.AirlineName`).</summary>
        public string AirlineName { get; set; }

        /// <summary>Número de vuelo del OFP (p. ej. «55CH»).</summary>
        public string FlightNumber { get; set; }

        /// <summary>Nombre del aeropuerto de destino (`NavAirportInfo.Name`). Llega **abreviado y sin tildes**: «El Dorado Intl».</summary>
        public string AirportName { get; set; }

        /// <summary>Ciudad del destino (`NavAirportInfo.City`): «Bogota», también sin tilde.</summary>
        public string City { get; set; }

        /// <summary>Código del destino (IATA preferido), último recurso cuando no hay nombre ni ciudad.</summary>
        public string DestinationCode { get; set; }

        /// <summary>Longitud del destino en grados. Es lo único que hace falta para la hora local.</summary>
        public double? DestinationLongitude { get; set; }

        /// <summary>
        /// Hora local del destino, ya calculada por el llamante (`Helpers/LocalTimeOfDay`). Se pasa
        /// en vez de calcularla aquí para que la redacción siga siendo pura y probable sin reloj.
        /// </summary>
        public TimeSpan? LocalTime { get; set; }

        /// <summary>Temperatura del METAR del destino en °C.</summary>
        public int? TemperatureC { get; set; }

        /// <summary>Condición del METAR del destino, en texto libre del servidor.</summary>
        public string Condition { get; set; }

        internal CabinAnnouncementContext Clone() => (CabinAnnouncementContext)MemberwiseClone();
    }

    /// <summary>
    /// Librería de frases de los anuncios de cabina, en español e inglés, generada en el cliente.
    ///
    /// Existe porque **NavData no va a publicar más clips** que los siete de `/briefing/`: los
    /// momentos que faltaban en una operación real —puerta cerrada, después del despegue,
    /// desarmar toboganes, llegada a puesto— no tienen grabación y no la van a tener. La salida es
    /// SAPI (`Services/CabinVoice`), que ya viaja con .NET Framework y no añade nada que distribuir.
    ///
    /// Es puro a propósito (sin red, sin WinForms, sin reloj): el texto se decide con datos que
    /// entran por parámetro, así que se puede probar la redacción sin tarjeta de sonido. Los clips
    /// pregrabados **siguen teniendo prioridad** cuando existen; esto es el respaldo y lo nuevo.
    ///
    /// El texto no vive en `Languages/*.json` porque no es interfaz: son dos locuciones completas
    /// por fase, y la que suena la decide la voz instalada en el equipo, no el idioma de la UI.
    /// </summary>
    internal static class CabinAnnouncementScript
    {
        internal const string Spanish = "es";
        internal const string English = "en";

        /// <summary>
        /// Las trece fases con texto propio, en orden de vuelo. Es el catálogo único: la lista del
        /// botón de prueba de Settings y la validación de simetría de los tests leen de aquí, así
        /// que añadir una fase sin texto rompe el test en vez de sonar en silencio.
        ///
        /// Las dos últimas —`turbulence_on` y `turbulence_off`— no son momentos del vuelo sino
        /// **eventos**: pueden ocurrir en cualquier punto en el aire y las decide
        /// `Helpers/TurbulenceAdvisor`, no la máquina de fases.
        /// </summary>
        internal static readonly string[] Phases =
        {
            "boarding", "pushback", "taxi_out", "after_takeoff",
            "cruise", "top_of_descent", "approach", "on_runway",
            "slides_disarm", "taxi_in", "on_block",
            "turbulence_on", "turbulence_off"
        };

        /// <summary>Fases cuyo texto habla del tiempo en el destino (se refresca el METAR antes).</summary>
        internal static bool UsesWeather(string phase) =>
            phase == "top_of_descent" || phase == "taxi_in";

        internal static bool IsKnownPhase(string phase) =>
            phase != null && Array.IndexOf(Phases, phase) >= 0;

        /// <summary>
        /// Redacta el anuncio. Devuelve <c>null</c> para una fase desconocida — el llamante calla en
        /// vez de inventar, que es como se detecta una fase nueva sin plantilla.
        /// </summary>
        internal static string Build(string phase, string language, CabinAnnouncementContext ctx)
        {
            if (!IsKnownPhase(phase)) return null;

            bool es = !IsEnglish(language);
            ctx = ctx ?? new CabinAnnouncementContext();

            switch (phase)
            {
                case "boarding":       return es ? BoardingEs(ctx)      : BoardingEn(ctx);
                case "pushback":       return es ? PushbackEs()         : PushbackEn();
                case "taxi_out":       return es ? TaxiOutEs()          : TaxiOutEn();
                case "after_takeoff":  return es ? AfterTakeoffEs()     : AfterTakeoffEn();
                case "cruise":         return es ? CruiseEs(ctx)        : CruiseEn(ctx);
                case "top_of_descent": return es ? TopOfDescentEs(ctx)  : TopOfDescentEn(ctx);
                case "approach":       return es ? ApproachEs()         : ApproachEn();
                case "on_runway":      return es ? OnRunwayEs()         : OnRunwayEn();
                case "slides_disarm":  return es ? SlidesDisarmEs()     : SlidesDisarmEn();
                case "taxi_in":        return es ? TaxiInEs(ctx)        : TaxiInEn(ctx);
                case "on_block":       return es ? OnBlockEs(ctx)       : OnBlockEn(ctx);
                case "turbulence_on":  return es ? TurbulenceOnEs()     : TurbulenceOnEn();
                case "turbulence_off": return es ? TurbulenceOffEs()    : TurbulenceOffEn();
                default:               return null;
            }
        }

        // ── Plantillas ────────────────────────────────────────────────────────────
        //
        // Cada frase se arma con trozos opcionales y se cierra con su propio punto: así el mismo
        // molde sirve con y sin datos, y no hay que mantener dos redacciones por fase.

        private static string BoardingEs(CabinAnnouncementContext c) => JoinSentences(
            Join(" ",
                "Señores pasajeros,",
                c.AirlineName != null ? $"en nombre de {Clean(c.AirlineName)}" : null,
                "les damos la bienvenida a bordo",
                c.FlightNumber != null ? $"del vuelo {SpellFlightNumber(c.FlightNumber)}" : "de este vuelo",
                AirportPhrase(c, Spanish) != null ? $"con destino al {AirportPhrase(c, Spanish)}" : null) + ".",
            "Les rogamos que ocupen sus asientos y coloquen el equipaje de mano en los compartimentos superiores.",
            "Gracias por volar con nosotros.");

        private static string BoardingEn(CabinAnnouncementContext c) => JoinSentences(
            Join(" ",
                "Ladies and gentlemen,",
                c.AirlineName != null ? $"on behalf of {Clean(c.AirlineName)}," : null,
                "welcome on board",
                c.FlightNumber != null ? $"flight {SpellFlightNumber(c.FlightNumber)}" : "this flight",
                AirportPhrase(c, English) != null ? $"to {AirportPhrase(c, English)}" : null) + ".",
            "Please take your seats and stow your hand luggage in the overhead bins.",
            "Thank you for flying with us.");

        private static string PushbackEs() => JoinSentences(
            "Señores pasajeros, la tripulación de cabina va a cerrar las puertas.",
            "Por favor, abróchense los cinturones y apaguen los dispositivos electrónicos.",
            "Vamos a iniciar el retroceso.");

        private static string PushbackEn() => JoinSentences(
            "Ladies and gentlemen, the cabin crew will now close the doors.",
            "Please fasten your seat belts and switch off electronic devices.",
            "We are about to push back.");

        private static string TaxiOutEs() => JoinSentences(
            "Señores pasajeros, hemos iniciado el rodaje hacia la pista de salida.",
            "Permanezcan sentados, con el cinturón abrochado y los dispositivos en modo avión.");

        private static string TaxiOutEn() => JoinSentences(
            "Ladies and gentlemen, we have started our taxi to the departure runway.",
            "Please remain seated with your seat belt fastened and your devices in flight mode.");

        private static string AfterTakeoffEs() => JoinSentences(
            "Señores pasajeros, hemos alcanzado los diez mil pies de altitud.",
            "Ya pueden utilizar sus dispositivos electrónicos.",
            "La tripulación iniciará el servicio de a bordo en breve.");

        private static string AfterTakeoffEn() => JoinSentences(
            "Ladies and gentlemen, we have reached ten thousand feet.",
            "You may now use your electronic devices.",
            "The cabin crew will begin the on-board service shortly.");

        private static string CruiseEs(CabinAnnouncementContext c) => JoinSentences(
            "Señores pasajeros, nos encontramos en ruta a la altitud de crucero.",
            AirportPhrase(c, Spanish) != null ? $"Nuestro destino es el {AirportPhrase(c, Spanish)}." : null,
            "La tripulación está a su disposición para cualquier necesidad.");

        private static string CruiseEn(CabinAnnouncementContext c) => JoinSentences(
            "Ladies and gentlemen, we are now cruising at our cruise altitude.",
            AirportPhrase(c, English) != null ? $"Our destination is {AirportPhrase(c, English)}." : null,
            "The crew is available for anything you may need.");

        private static string TopOfDescentEs(CabinAnnouncementContext c) => JoinSentences(
            "Señores pasajeros, hemos iniciado el descenso"
                + (AirportPhrase(c, Spanish) != null ? $" hacia el {AirportPhrase(c, Spanish)}" : "") + ".",
            WeatherEs(c),
            "Les rogamos que regresen a sus asientos y abrochen sus cinturones.");

        private static string TopOfDescentEn(CabinAnnouncementContext c) => JoinSentences(
            "Ladies and gentlemen, we have started our descent"
                + (AirportPhrase(c, English) != null ? $" to {AirportPhrase(c, English)}" : "") + ".",
            WeatherEn(c),
            "Please return to your seats and fasten your seat belts.");

        private static string ApproachEs() => JoinSentences(
            "Señores pasajeros, nos encontramos en aproximación final.",
            "La tripulación de cabina debe asegurar la cabina.",
            "Permanezcan sentados con el cinturón abrochado hasta que el avión se detenga por completo.");

        private static string ApproachEn() => JoinSentences(
            "Ladies and gentlemen, we are on final approach.",
            "The cabin crew must secure the cabin.",
            "Please remain seated with your seat belt fastened until the aircraft comes to a complete stop.");

        private static string OnRunwayEs() => JoinSentences(
            "Señores pasajeros, vamos a alinearnos en la pista.",
            "La tripulación de cabina debe ocupar sus asientos para el despegue.");

        private static string OnRunwayEn() => JoinSentences(
            "Ladies and gentlemen, we are lining up on the runway.",
            "Cabin crew, take your seats for departure.");

        /// <summary>
        /// La orden del comandante a la tripulación al dejar la pista, no un PA a los pasajeros.
        /// Es el «disarm slides and cross-check» de cualquier manual: se dice **después de aterrizar**
        /// y antes de llegar al puesto, y faltaba por completo.
        /// </summary>
        private static string SlidesDisarmEs() => JoinSentences(
            "Tripulación de cabina, desarmen los toboganes y realicen el chequeo cruzado.");

        private static string SlidesDisarmEn() => JoinSentences(
            "Cabin crew, disarm slides and cross-check.");

        /// <summary>
        /// Turbulencia: **no dice que el comandante haya encendido el letrero** —ese offset no se lee
        /// y afirmarlo sería inventar— sino que se está atravesando la zona, que es lo que percibe el
        /// pasajero y lo que el avión acaba de medir (`Helpers/TurbulenceAdvisor`).
        /// </summary>
        private static string TurbulenceOnEs() => JoinSentences(
            "Señores pasajeros, estamos atravesando una zona de turbulencia.",
            "Por favor, regresen a sus asientos y abróchense los cinturones.");

        private static string TurbulenceOnEn() => JoinSentences(
            "Ladies and gentlemen, we are experiencing turbulence.",
            "Please return to your seats and fasten your seat belts.");

        /// <summary>
        /// El final se anuncia con el cinturón puesto, no con barra libre: es lo que dice la
        /// tripulación al quitar el letrero y lo que evita que el pasajero se levante en falso.
        /// </summary>
        private static string TurbulenceOffEs() => JoinSentences(
            "Señores pasajeros, hemos salido de la zona de turbulencia.",
            "Pueden moverse por la cabina; mantengan el cinturón abrochado mientras estén sentados.");

        private static string TurbulenceOffEn() => JoinSentences(
            "Ladies and gentlemen, we have left the area of turbulence.",
            "You may move around the cabin; please keep your seat belt fastened while you are seated.");

        private static string TaxiInEs(CabinAnnouncementContext c) => JoinSentences(
            "Señores pasajeros, hemos aterrizado"
                + (AirportPhrase(c, Spanish) != null ? $" en el {AirportPhrase(c, Spanish)}" : "") + ".",
            c.LocalTime.HasValue
                ? $"{LocalTimeOfDay.Spell(c.LocalTime.Value, Spanish)}, hora local."
                : null,
            c.TemperatureC.HasValue
                ? $"La temperatura exterior es de {SpellTemperature(c.TemperatureC.Value, Spanish)}."
                : null,
            "Permanezcan sentados hasta que se apague la señal de cinturones y el comandante autorice el descenso.");

        private static string TaxiInEn(CabinAnnouncementContext c) => JoinSentences(
            "Ladies and gentlemen, we have landed"
                + (AirportPhrase(c, English) != null ? $" at {AirportPhrase(c, English)}" : "") + ".",
            c.LocalTime.HasValue
                ? $"{LocalTimeOfDay.Spell(c.LocalTime.Value, English)}."
                : null,
            c.TemperatureC.HasValue
                ? $"The outside temperature is {SpellTemperature(c.TemperatureC.Value, English)}."
                : null,
            "Please remain seated until the seat belt sign is switched off and the captain authorises disembarkation.");

        private static string OnBlockEs(CabinAnnouncementContext c) => JoinSentences(
            "Señores pasajeros, hemos llegado a nuestro destino.",
            "Pueden desembarcar; por favor, comprueben que no olvidan ningún objeto personal.",
            c.AirlineName != null
                ? $"{Clean(c.AirlineName)} les desea un buen día."
                : "Les deseamos un buen día.");

        private static string OnBlockEn(CabinAnnouncementContext c) => JoinSentences(
            "Ladies and gentlemen, we have arrived at our destination.",
            "You may now disembark; please make sure you do not leave any personal belongings behind.",
            c.AirlineName != null
                ? $"{Clean(c.AirlineName)} wishes you a pleasant day."
                : "We wish you a pleasant day.");

        // ── El aeropuerto por su nombre ───────────────────────────────────────────

        /// <summary>
        /// Cómo se nombra el destino en voz alta. NavData publica `name` **abreviado y sin tildes**
        /// —medido en la caché real: «El Dorado Intl», «Alfonso Bonilla Aragon Intl», «Palonegro»—
        /// junto a `city`, así que la frase se arma con los dos: se expande la abreviatura, se
        /// reordena a la construcción española («Aeropuerto Internacional El Dorado») y se le añade
        /// la ciudad si el nombre no la lleva ya.
        ///
        /// Devuelve la frase **sin artículo** y con mayúscula inicial («Aeropuerto Internacional El
        /// Dorado de Bogota»), porque el artículo depende de la plantilla: «con destino al …»,
        /// «hemos aterrizado en el …», «hacia el …». Null cuando no hay ni nombre, ni ciudad, ni
        /// código: entonces la cláusula entera desaparece.
        /// </summary>
        internal static string AirportPhrase(CabinAnnouncementContext c, string language)
        {
            if (c == null) return null;

            bool es = !IsEnglish(language);
            string name = Clean(c.AirportName ?? "");
            string city = Clean(c.City ?? "");
            string code = string.IsNullOrWhiteSpace(c.DestinationCode) ? "" : SpellCode(c.DestinationCode);

            string phrase;
            if (name.Length > 0)
            {
                string proper;
                AirportKind kind = SplitAirportName(name, out proper);
                if (proper.Length == 0) proper = name;

                phrase = es ? SpanishAirport(kind, proper) : EnglishAirport(kind, proper);
            }
            else if (city.Length > 0)
            {
                phrase = es ? $"Aeropuerto de {city}" : $"the airport in {city}";
            }
            else if (code.Length > 0)
            {
                phrase = es ? $"Aeropuerto {code}" : $"the airport {code}";
            }
            else
            {
                return null;
            }

            // La ciudad solo se añade si no está ya dicha: «Aeropuerto de Cali» no necesita «de Cali».
            if (city.Length > 0 && phrase.IndexOf(city, StringComparison.OrdinalIgnoreCase) < 0)
                phrase += es ? $" de {city}" : $" in {city}";

            return phrase;
        }

        private enum AirportKind { Plain, International, Regional, Municipal }

        /// <summary>
        /// Separa la abreviatura del nombre propio. Se prueba de sufijo más largo a más corto y se
        /// ignoran el punto y los espacios sobrantes («Intl.»). Un sufijo desconocido no se toca:
        /// mejor decir el nombre tal cual que inventarse una categoría.
        /// </summary>
        private static AirportKind SplitAirportName(string name, out string proper)
        {
            proper = name;

            var suffixes = new[]
            {
                new { Token = "International Airport", Kind = AirportKind.International },
                new { Token = "Regional Airport",      Kind = AirportKind.Regional },
                new { Token = "Municipal Airport",     Kind = AirportKind.Municipal },
                new { Token = "Airport",               Kind = AirportKind.Plain },
                new { Token = "Intl",                  Kind = AirportKind.International },
                new { Token = "Rgnl",                  Kind = AirportKind.Regional },
                new { Token = "Muni",                  Kind = AirportKind.Municipal },
            };

            string trimmed = name.TrimEnd('.', ' ');
            foreach (var suffix in suffixes)
            {
                if (!trimmed.EndsWith(suffix.Token, StringComparison.OrdinalIgnoreCase)) continue;
                if (trimmed.Length == suffix.Token.Length) break;   // el nombre ES la categoría: no se toca

                string candidate = Clean(trimmed.Substring(0, trimmed.Length - suffix.Token.Length).TrimEnd('.', ' '));
                if (candidate.Length == 0) break;

                proper = candidate;
                return suffix.Kind;
            }

            return AirportKind.Plain;
        }

        private static string SpanishAirport(AirportKind kind, string proper)
        {
            switch (kind)
            {
                case AirportKind.International: return $"Aeropuerto Internacional {proper}";
                case AirportKind.Regional:      return $"Aeropuerto Regional {proper}";
                case AirportKind.Municipal:     return $"Aeropuerto Municipal {proper}";
                default:                        return $"Aeropuerto {proper}";
            }
        }

        private static string EnglishAirport(AirportKind kind, string proper)
        {
            switch (kind)
            {
                case AirportKind.International: return $"{proper} International Airport";
                case AirportKind.Regional:      return $"{proper} Regional Airport";
                case AirportKind.Municipal:     return $"{proper} Municipal Airport";
                default:                        return $"{proper} Airport";
            }
        }

        // ── Frases con datos ──────────────────────────────────────────────────────

        private static string WeatherEs(CabinAnnouncementContext c)
        {
            if (!c.TemperatureC.HasValue) return null;
            string condition = DescribeCondition(c.Condition, Spanish);
            return "La temperatura prevista a nuestra llegada es de "
                 + SpellTemperature(c.TemperatureC.Value, Spanish)
                 + (condition != null ? $", con {condition}." : ".");
        }

        private static string WeatherEn(CabinAnnouncementContext c)
        {
            if (!c.TemperatureC.HasValue) return null;
            string condition = DescribeCondition(c.Condition, English);
            return "The expected temperature on arrival is "
                 + SpellTemperature(c.TemperatureC.Value, English)
                 + (condition != null ? $", with {condition}." : ".");
        }

        // ── Piezas puras, probadas aparte ─────────────────────────────────────────

        /// <summary>
        /// Número de vuelo como se lee por megafonía: si es todo dígitos se deletrea dígito a
        /// dígito («1234» → «1 2 3 4»), porque «mil doscientos treinta y cuatro» no es un vuelo.
        /// Si lleva letras se deja tal cual: las voces de es-ES y en-US leen bien una secuencia
        /// corta en mayúsculas («55CH») y separarla a mano la empeora.
        /// </summary>
        internal static string SpellFlightNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string v = value.Trim();
            bool allDigits = true;
            foreach (char ch in v)
                if (ch < '0' || ch > '9') { allDigits = false; break; }
            if (!allDigits) return v;

            var sb = new StringBuilder(v.Length * 2);
            for (int i = 0; i < v.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(v[i]);
            }
            return sb.ToString();
        }

        /// <summary>Código OACI/IATA deletreado letra a letra, para que la voz no lo lea como una palabra.</summary>
        internal static string SpellCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            string v = code.Trim().ToUpperInvariant();
            char[] chars = v.ToCharArray();
            return string.Join(" ", Array.ConvertAll(chars, ch => ch.ToString()));
        }

        /// <summary>
        /// Temperatura en palabras. «1» es singular y el signo se dice «menos»: leer «1 grados» o
        /// «-3 grados» delata que el texto lo escribió una máquina.
        /// </summary>
        internal static string SpellTemperature(int celsius, string language)
        {
            bool es = !IsEnglish(language);

            if (celsius == 0)
                return es ? "cero grados centígrados" : "zero degrees Celsius";

            int magnitude = Math.Abs(celsius);
            string sign = celsius < 0 ? (es ? "menos " : "minus ") : "";

            if (es)
                return magnitude == 1
                    ? $"{sign}un grado centígrado"
                    : $"{sign}{magnitude} grados centígrados";

            return magnitude == 1
                ? $"{sign}one degree Celsius"
                : $"{sign}{magnitude} degrees Celsius";
        }

        /// <summary>
        /// Traduce la condición del METAR a una frase hablable. El campo llega como texto libre del
        /// servidor, así que se busca por token y **lo que no se reconoce se omite**: es preferible
        /// no decir el tiempo que decir «con volcanic ash».
        /// </summary>
        internal static string DescribeCondition(string raw, string language)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string s = raw.Trim().ToLowerInvariant();
            bool es = !IsEnglish(language);

            if (HasAny(s, "thunder", "storm", "tormenta", "trueno"))            return es ? "tormenta"      : "thunderstorms";
            if (HasAny(s, "snow", "sleet", "nieve", "aguanieve"))               return es ? "nieve"          : "snow";
            if (HasAny(s, "fog", "mist", "haze", "niebla", "bruma", "neblina")) return es ? "niebla"         : "fog";
            if (HasAny(s, "rain", "drizzle", "shower", "lluvia", "chubasco"))   return es ? "lluvia"         : "rain";
            if (HasAny(s, "cloud", "overcast", "broken", "nublado"))            return es ? "cielo nublado"  : "cloudy skies";
            if (HasAny(s, "clear", "sunny", "despejado", "soleado"))            return es ? "cielo despejado" : "clear skies";

            return null;
        }

        // ── Utilidades de composición ─────────────────────────────────────────────

        private static bool IsEnglish(string language) =>
            string.Equals(language, English, StringComparison.OrdinalIgnoreCase);

        private static bool HasAny(string haystack, params string[] tokens)
        {
            foreach (string token in tokens)
                if (haystack.IndexOf(token, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>
        /// Une trozos descartando los vacíos. Evita el defecto clásico de estas plantillas: dos
        /// espacios, «con destino .» o una coma huérfana cuando falta un dato.
        /// </summary>
        private static string Join(string separator, params string[] parts)
        {
            var sb = new StringBuilder();
            foreach (string part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                string p = part.Trim();
                if (sb.Length > 0) sb.Append(separator);
                sb.Append(p);
            }
            return sb.ToString();
        }

        private static string JoinSentences(params string[] sentences) => Join(" ", sentences);

        /// <summary>Colapsa espacios repetidos y quita el espacio antes de la puntuación.</summary>
        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string v = value.Trim();
            var sb = new StringBuilder(v.Length);
            bool lastWasSpace = false;
            foreach (char ch in v)
            {
                bool isSpace = ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n';
                if (isSpace)
                {
                    if (lastWasSpace) continue;
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(ch);
                }
                lastWasSpace = isSpace;
            }
            return sb.ToString();
        }
    }
}
