using System.Collections.Generic;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Altitud de transición (TA) y nivel de transición (TL) de respaldo, por país.
    ///
    /// La fuente preferente es siempre el NavData del aeropuerto
    /// (`NavAirportInfo.TransitionAltitudeFt` / `TransitionLevelFt`). Cuando ese dato no
    /// viene, el comportamiento anterior era simplemente no hacer nada: ni OSD de
    /// "SET STD 1013" al subir, ni aviso al bajar, ni comprobación de 1013 en el climb,
    /// ni el gate de QNH de llegada por TL — es decir, se perdían en silencio dos
    /// criterios de scoring y sus avisos al piloto.
    ///
    /// Estos valores son los publicados por la autoridad de cada país. Son un respaldo
    /// deliberadamente conservador:
    ///   · La TA se usa solo para avisar (OSD) y para el check de 1013 en subida.
    ///   · La TL solo se usa en llegada, y el gate de QNH tiene además su propio fallback
    ///     a 1 000 ft AGL, así que un valor aproximado no deja el check sin ejecutar.
    ///
    /// Un valor levemente equivocado produce un aviso algo antes o después, no una
    /// penalización injusta: el QNH de llegada se decide al filear contra el aeropuerto
    /// final.
    /// </summary>
    internal static class TransitionDefaults
    {
        // TA = altitud de transición en ft (se aplica al aeropuerto de SALIDA)
        private static readonly Dictionary<string, double> _taByCountry =
            new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Norteamérica: TA y TL unificadas en 18 000 ft
                ["US"] = 18000, ["CA"] = 18000, ["MX"] = 18000,

                // Sudamérica — Colombia 18 000 ft; el resto adopta 5 000/10 000 ft
                ["CO"] = 18000, ["VE"] = 18000, ["EC"] = 18000, ["PE"] = 18000,
                ["BO"] = 18000, ["PA"] = 18000, ["CR"] = 18000, ["GT"] = 18000,
                ["BR"] = 7000,  ["AR"] = 6000,  ["CL"] = 7000,  ["UY"] = 6000,
                ["PY"] = 6000,

                // Europa: la mayoría adopta 5 000 ft; el Reino Unido e Irlanda 3 000/5 000
                ["GB"] = 5000,  ["IE"] = 5000,
                ["DE"] = 5000,  ["FR"] = 5000,  ["ES"] = 5000,  ["IT"] = 5000,
                ["PT"] = 5000,  ["NL"] = 5000,  ["BE"] = 5000,  ["CH"] = 5000,
                ["AT"] = 5000,  ["DK"] = 5000,  ["NO"] = 5000,  ["SE"] = 5000,
                ["FI"] = 5000,  ["PL"] = 5000,  ["CZ"] = 5000,  ["HU"] = 5000,
                ["GR"] = 5000,  ["RO"] = 5000,  ["TR"] = 5000,

                // África y Oriente Medio
                ["ZA"] = 7500,  ["EG"] = 5000,  ["MA"] = 5000,  ["TN"] = 5000,
                ["DZ"] = 5000,  ["KE"] = 5000,  ["NG"] = 5000,  ["AE"] = 13000,
                ["SA"] = 13000, ["QA"] = 13000, ["OM"] = 13000, ["IL"] = 11000,

                // Asia-Pacífico
                ["JP"] = 14000, ["KR"] = 14000, ["CN"] = 10800, ["IN"] = 13000,
                ["AU"] = 10000, ["NZ"] = 11000, ["SG"] = 11000, ["MY"] = 11000,
                ["TH"] = 11000, ["ID"] = 11000, ["PH"] = 11000, ["HK"] = 11000,
            };

        /// <summary>
        /// Altitud de transición de respaldo para un aeropuerto, o 0 si el país no está
        /// en la tabla (en cuyo caso no se inventa nada y se mantiene el comportamiento
        /// de omitir el aviso, esta vez de forma explícita y trazable).
        /// </summary>
        internal static double GetTransitionAltitudeFt(NavAirportInfo info)
        {
            string iso = NormalizeCountry(info);
            if (iso == null) return 0;
            return _taByCountry.TryGetValue(iso, out double ta) ? ta : 0;
        }

        /// <summary>
        /// Nivel de transición de respaldo. Se deriva de la TA del país añadiendo un
        /// margen de 1 000 ft: en la práctica la TL es la TA redondeada al FL superior
        /// según la presión, y 1 000 ft es el margen mínimo habitual que mantiene el
        /// orden correcto TA &lt; TL.
        ///
        /// Si el país es desconocido devuelve 0, no 1 000: "no sé" no puede convertirse en
        /// un nivel que parezca válido, o el cliente acabaría avisando de un FL inventado.
        /// </summary>
        internal static double GetTransitionLevelFt(NavAirportInfo info)
        {
            double ta = GetTransitionAltitudeFt(info);
            return ta > 0 ? ta + 1000.0 : 0;
        }

        private static string NormalizeCountry(NavAirportInfo info)
        {
            string iso = info?.IsoCountry;
            if (string.IsNullOrWhiteSpace(iso)) return null;
            iso = iso.Trim().ToUpperInvariant();
            return iso.Length >= 2 ? iso.Substring(0, 2) : null;
        }
    }
}
