using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// La pintura (livery) de la aeronave, sacada del título que publica el simulador.
    ///
    /// Es el «🎨 Pintura: …» del log. Estaba dentro de `FsuipcService`, con un último recurso que
    /// devolvía **cualquier** token de 3–4 caracteres en mayúsculas del título, y eso convertía el
    /// modelo en pintura: con un PMDG 777-200LR el título es `777-200LR`, el token «777» no estaba
    /// en su lista de exclusión (`B777` sí, `777` no) y el log decía «Pintura: 777» —reportado por
    /// el mantenedor—. Vive aquí, puro y con test, porque la regla es una decisión y no un detalle
    /// de FSUIPC.
    /// </summary>
    internal static class AircraftLivery
    {
        internal const string Unknown = "Unknown";

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
        /// El nombre de la aerolínea si está en el título; si no, un código de pintura —3 o 4
        /// caracteres, en mayúsculas y **sin dígitos**—; y si no hay nada de eso, `Unknown`.
        ///
        /// La regla de «sin dígitos» es la que separa una pintura de un modelo: `AAL`, `SAS` o
        /// `VHR` son pinturas; `777`, `200LR`, `B738` o `A20N` son el avión. Y no se inventa nada:
        /// un título que solo dice el modelo devuelve `Unknown` y el log no pinta esa línea.
        /// </summary>
        internal static string FromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return Unknown;

            foreach (string airline in Airlines)
                if (title.IndexOf(airline, StringComparison.OrdinalIgnoreCase) >= 0) return airline;

            string[] parts = title.Split(new[] { ' ', '-', '_', '(', ')' },
                                         StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                if (part.Length < 3 || part.Length > 4) continue;
                if (part != part.ToUpperInvariant()) continue;
                if (HasDigit(part)) continue;       // «777», «200LR»: el modelo, no la pintura
                return part;
            }

            return Unknown;
        }

        private static bool HasDigit(string text)
        {
            foreach (char c in text)
                if (c >= '0' && c <= '9') return true;
            return false;
        }
    }
}
