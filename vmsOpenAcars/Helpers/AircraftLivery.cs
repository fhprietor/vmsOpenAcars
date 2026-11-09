using System;
using System.Collections.Generic;

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
        /// **El fabricante nunca es la pintura.** La regla de «sin dígitos» de la v0.9.16 dejaba
        /// pasar los nombres de fabricante de 3–4 letras en mayúsculas: en el PIREP real
        /// `nROPdk4GxYY5AOy1` el título del ATR es `ATR 72-600 Passenger` y el log imprimía
        /// «🎨 Pintura: ATR» —el fabricante disfrazado de pintura, exactamente la misma clase de
        /// fallo que el «Pintura: 777» de la v0.9.16—. La lista se compara sin distinguir
        /// mayúsculas porque el token candidato ya viene en mayúsculas, y crece aquí, no en el
        /// consumidor.
        /// </summary>
        private static readonly HashSet<string> Manufacturers =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ATR", "BOEING", "AIRBUS", "EMBRAER", "CESSNA", "BEECHCRAFT",
            "BOMBARDIER", "DEHAVILLAND", "PIPER", "CIRRUS", "DASSAULT",
            "FOKKER", "SAAB", "SUKHOI", "TUPOLEV", "ANTONOV", "COMAC",
            "MITSUBISHI", "LEARJET", "GULFSTREAM", "HAWKER", "PIAGGIO",
            "PILATUS", "ROBINSON", "MOONEY", "SOCATA", "AVIAT", "EXTRA"
        };

        /// <summary>
        /// El nombre de la aerolínea si está en el título; si no, un código de pintura —3 o 4
        /// caracteres, en mayúsculas, **sin dígitos**, que no sea el fabricante ni el modelo—; y si
        /// no hay nada de eso, `Unknown`.
        ///
        /// La regla de «sin dígitos» es la que separa una pintura de un modelo: `AAL`, `SAS` o
        /// `VHR` son pinturas; `777`, `200LR`, `B738` o `A20N` son el avión. Y **el modelo y el
        /// designador ICAO del propio avión tampoco son pintura** aunque el título los escriba
        /// sueltos (`72`, `600`, `738`, `AT76`): llegan por parámetro desde `FsuipcService`, que es
        /// quien los tiene. Un título que solo dice el avión devuelve `Unknown` y el log no pinta
        /// esa línea.
        /// </summary>
        internal static string FromTitle(string title, string model = null, string icao = null)
        {
            if (string.IsNullOrWhiteSpace(title)) return Unknown;

            foreach (string airline in Airlines)
                if (title.IndexOf(airline, StringComparison.OrdinalIgnoreCase) >= 0) return airline;

            var modelTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddTokens(modelTokens, model);
            AddTokens(modelTokens, icao);

            string[] parts = title.Split(new[] { ' ', '-', '_', '(', ')' },
                                         StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                if (part.Length < 3 || part.Length > 4) continue;
                if (part != part.ToUpperInvariant()) continue;
                if (HasDigit(part)) continue;            // «777», «200LR»: el modelo, no la pintura
                if (Manufacturers.Contains(part)) continue;  // «ATR», «BOEING»: el fabricante
                if (modelTokens.Contains(part)) continue;    // el modelo/ICAO suelto en el título
                return part;
            }

            return Unknown;
        }

        private static void AddTokens(HashSet<string> target, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (string part in text.Split(new[] { ' ', '-', '_', '(', ')' },
                                                StringSplitOptions.RemoveEmptyEntries))
                target.Add(part);
        }

        private static bool HasDigit(string text)
        {
            foreach (char c in text)
                if (c >= '0' && c <= '9') return true;
            return false;
        }
    }
}
