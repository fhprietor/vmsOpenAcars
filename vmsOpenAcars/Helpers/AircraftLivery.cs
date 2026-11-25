using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// La pintura (livery) de la aeronave, sacada del título que publica el simulador.
    ///
    /// **Tercera vez que fallaba la misma heurística, y por eso ya no hay heurística.** El detector
    /// vivía en `FsuipcService` y devolvía «el primer token plausible» del título; cada vez que un
    /// addon nuevo lo rompía se añadía una lista de exclusión:
    /// - v0.9.16: `777-200LR` → «🎨 Pintura: 777» (el modelo disfrazado de pintura).
    /// - v0.9.67: `ATR 72-600 Passenger` → «🎨 Pintura: ATR» (el fabricante).
    /// - v0.9.79: `737-800 PAX SSW HD` → «🎨 Pintura: PAX» (una etiqueta de variante de cabina).
    ///
    /// Una cuarta lista de exclusiones volvería a fallar con el próximo addon. La regla que queda:
    /// **solo se pinta una aerolínea reconocible, escrita en el título** (o el código de aerolínea
    /// que trae el plan, si el título también lo escribe). No hay «token plausible»: si no hay
    /// evidencia, no hay línea. Es preferible no enseñar pintura a enseñar el modelo, el fabricante
    /// o una etiqueta de configuración como si fuera una librea.
    /// </summary>
    internal static class AircraftLivery
    {
        internal const string Unknown = "Unknown";

        /// <summary>
        /// Nombres de aerolínea que el cliente reconoce en el título. A diferencia de las listas de
        /// exclusión que se fueron acumulando, esta **no necesita crecer para no equivocarse**: un
        /// título que no nombre a ninguna de estas aerolíneas devuelve `Unknown` y no pinta nada,
        /// que es el fallo seguro. Crece solo cuando se quiere *reconocer* una aerolínea más.
        /// </summary>
        private static readonly string[] Airlines =
        {
            "United", "American", "Delta", "Iberia", "Lufthansa",
            "British", "Air France", "KLM", "Emirates", "Qatar",
            "Avianca", "LATAM", "Viva", "EasyJet", "Ryanair",
            "Southwest", "JetBlue", "Spirit", "Frontier", "Alaska",
            "Copa", "Aeromexico", "Air Canada", "WestJet",
            "Virgin", "Etihad", "Turkish", "Singapore", "Cathay", "VHR"
        };

        /// <summary>
        /// El nombre de la aerolínea si está en el título; si no, el código de aerolínea del plan
        /// —solo cuando el título también lo contiene—; y si no hay ninguna de las dos cosas,
        /// `Unknown` (el log no pinta la línea).
        ///
        /// `airlineHint` es el código ICAO que publica el plan (`SimbriefPlan.Airline`: `AVA`,
        /// `AAL`…). Se exige que aparezca **como token** en el título porque la pintura es lo que
        /// el avión dice llevar, no lo que el plan espera: usarlo sin esa comprobación volvería a
        /// inventar una librea. Y sin `hint`, un código de tres letras suelto (`AAL`, `SAS`) ya no
        /// es pintura: era exactamente el «primer token plausible» que falló tres veces.
        /// </summary>
        internal static string FromTitle(string title, string airlineHint = null)
        {
            if (string.IsNullOrWhiteSpace(title)) return Unknown;

            foreach (string airline in Airlines)
                if (title.IndexOf(airline, StringComparison.OrdinalIgnoreCase) >= 0) return airline;

            if (!string.IsNullOrWhiteSpace(airlineHint))
            {
                string hint = airlineHint.Trim();
                if (hint.Length > 0 && ContainsToken(title, hint)) return hint;
            }

            return Unknown;
        }

        /// <summary>
        /// Compara por **token** (separado por espacio, guion, guion bajo, paréntesis o barra), no
        /// por subcadena: `AVA` no puede casar dentro de `Avanti`.
        /// </summary>
        private static bool ContainsToken(string title, string token)
        {
            foreach (string part in title.Split(new[] { ' ', '-', '_', '(', ')', '/' },
                                                StringSplitOptions.RemoveEmptyEntries))
                if (string.Equals(part, token, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
