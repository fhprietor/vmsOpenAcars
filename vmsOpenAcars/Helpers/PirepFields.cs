using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Los campos personalizados que phpVMS declaró en `pirep_fields` (29/09/2026) para que el PIREP
    /// lleve la pista y la ruta de rodaje: `Departure Runway`, `Arrival Runway` y `Taxi Route`.
    ///
    /// **La clave que se envía es el NOMBRE, no el slug.** La API guarda la clave que mandamos como
    /// nombre del campo y deriva el slug con `str_slug()`; si se manda `departure-runway` casa igual
    /// —lo encuentra por slug— pero el PIREP acaba pintando el slug en lugar del nombre. Por eso los
    /// nombres viven aquí como constantes y no se escriben a mano en cada sitio que envía.
    ///
    /// Un campo **sin valor se omite** en vez de mandarse vacío: no aporta nada y evita que un envío
    /// posterior borre con `""` algo que ya estaba escrito.
    /// </summary>
    internal static class PirepFields
    {
        internal const string DepartureRunwayName = "Departure Runway";
        internal const string ArrivalRunwayName   = "Arrival Runway";
        internal const string TaxiRouteName       = "Taxi Route";

        /// <summary>
        /// **Nombre del campo que habría que crear en phpVMS** para llevar la meteo del aterrizaje
        /// estructurada en vez de dentro de `notes`. Hoy **no se usa por defecto**: un campo de
        /// `pirep_fields` lo tiene que crear la aerolínea y mandar una clave desconocida **no da
        /// error**, se ignora en silencio. El envío va detrás de
        /// `AppConfig.PirepLandingWeatherFieldEnabled`, apagado.
        ///
        /// Mientras no exista, el mismo texto viaja en el `notes` del PIREP (texto libre),
        /// compuesto por <see cref="LandingWeatherLine.Build"/>.
        /// </summary>
        internal const string LandingWeatherName  = "Landing Weather";

        /// <summary>
        /// Arma el diccionario de `fields` con lo que haya. Cualquiera de los tres puede venir nulo:
        /// la pista de salida se conoce al prefilear, la de llegada a veces también (la del OFP) y la
        /// ruta de rodaje solo cuando el piloto responde al popup.
        /// </summary>
        internal static Dictionary<string, string> Build(string departureRunway, string arrivalRunway,
                                                         string taxiRoute)
            => Build(departureRunway, arrivalRunway, taxiRoute, null);

        /// <summary>
        /// La misma construcción con la meteo del aterrizaje, que **solo se rellena si phpVMS ya ha
        /// creado el campo** (<see cref="LandingWeatherName"/>): es el llamante quien decide si tiene
        /// derecho a mandarlo (ver `AppConfig.PirepLandingWeatherFieldEnabled`). Un cuarto valor nulo
        /// deja el diccionario exactamente igual que antes, así que el camino apagado no cambia
        /// ningún envío.
        /// </summary>
        internal static Dictionary<string, string> Build(string departureRunway, string arrivalRunway,
                                                         string taxiRoute, string landingWeather)
        {
            var fields = new Dictionary<string, string>();
            Add(fields, DepartureRunwayName, NormalizeRunway(departureRunway));
            Add(fields, ArrivalRunwayName,   NormalizeRunway(arrivalRunway));
            Add(fields, TaxiRouteName,       NormalizeRoute(taxiRoute));
            Add(fields, LandingWeatherName,  NormalizeLandingWeather(landingWeather));
            return fields;
        }

        /// <summary>
        /// La línea de meteo es una sola línea ya compuesta (`LandingWeatherLine.Build`): solo se
        /// colapsan los espacios de más y los saltos de línea, que en un campo de texto libre
        /// romperían la lectura. No se reformatea ni se recorta el METAR: el crudo es el dato.
        /// </summary>
        private static string NormalizeLandingWeather(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            var tokens = line.Split(new[] { ' ', '\t', '\r', '\n' },
                                    System.StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length == 0 ? null : string.Join(" ", tokens);
        }

        private static void Add(IDictionary<string, string> fields, string name, string value)
        {
            if (!string.IsNullOrEmpty(value)) fields[name] = value;
        }

        /// <summary>
        /// La pista se publica como «14R» / «32L»: sin espacios y en mayúsculas, porque ni el OFP ni
        /// el dataset la escriben siempre igual («14r», « 14R »). No se toca nada más: no se inventa
        /// un designador que no viniera.
        /// </summary>
        private static string NormalizeRunway(string runway)
            => string.IsNullOrWhiteSpace(runway) ? null : runway.Trim().ToUpperInvariant();

        /// <summary>
        /// La ruta de rodaje es **la autorización de ATC tal como la tecleó el piloto**: se conservan
        /// las calles y su ORDEN —es la fuente buena de la observación para NavData— y solo se
        /// colapsan los espacios de más (el popup admite pegar texto con saltos de línea y dobles
        /// espacios). No se pasa a mayúsculas ni se reordena: eso sería interpretar su autorización.
        /// </summary>
        private static string NormalizeRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route)) return null;

            var streets = route.Split(new[] { ' ', '\t', '\r', '\n' },
                                      System.StringSplitOptions.RemoveEmptyEntries);
            return streets.Length == 0 ? null : string.Join(" ", streets);
        }
    }
}
