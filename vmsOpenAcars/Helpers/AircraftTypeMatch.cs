using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// ¿El avión que vuela el simulador es el del OFP?
    ///
    /// La comparación **no puede ser de igualdad a secas**, y tampoco puede quedarse en la familia:
    ///
    /// - El simulador publica el **modelo ATC** —`atc_model` del `aircraft.cfg` por el offset
    ///   `0x0618` de FSUIPC, con `ExtractIcaoFromTitle` como respaldo—, que es un código de
    ///   **familia**: un PMDG 777 reporta `B777`, un 737-800 reporta `B737`, un 747-400 reporta
    ///   `B747`.
    /// - Pero también publica el **modelo completo** —`AircraftModel` (`0x0B26`) y el *title*
    ///   (`0x3D00`, el que se pinta como «✈️ Aeronave»)—, y ahí sí está la variante: el mantenedor
    ///   lo vio en su log con un PMDG 777-200LR (`✈️ Aeronave: 777-200LR` / `📋 ICAO: B777`).
    /// - SimBrief publica en `aircraft.icao_code` el **designador ICAO de tipo**, de variante:
    ///   `B77L`, `B77W`, `B738`, `B744`, `B789`.
    ///
    /// Así que la regla es **usar el dato más preciso que haya**, y solo degradar cuando falte:
    ///
    /// 1. Si del modelo/título sale un **designador de variante** y el OFP también trae uno, se
    ///    comparan **exactos**: un plan de 777-300ER (`B77W`) en un 777-200LR (`B77L`) **sí** avisa
    ///    —son aviones distintos, con consumo distinto—, que es lo que pidió el mantenedor.
    /// 2. Si no se puede resolver la variante (un addon que solo dice «777», o un modelo que no
    ///    está en la tabla), se cae a la **familia**: coinciden los tres primeros caracteres. Es lo
    ///    que evita el falso positivo que originó todo esto (simulador `B777` contra OFP `B77L`).
    /// 3. Sin dato (`????`, vacío) no se bloquea nada.
    ///
    /// La tabla de modelos es **best-effort**: si un avión no está, el resultado no es un aviso
    /// equivocado sino la comparación por familia. Y el aviso nunca bloquea: el diálogo pregunta.
    /// </summary>
    internal static class AircraftTypeMatch
    {
        /// <summary>Lo que devuelve el cliente cuando el simulador no publica el dato.</summary>
        internal const string Unknown = "????";

        /// <summary>¿Hay dato de verdad, o es vacío / el centinela «????»?</summary>
        internal static bool IsKnown(string type)
            => !string.IsNullOrWhiteSpace(type)
               && !string.Equals(type.Trim(), Unknown, StringComparison.Ordinal);

        /// <summary>
        /// Misma familia = coinciden los tres primeros caracteres del designador: `B77` cubre
        /// B772/B773/B77L/B77W/B778/B779 y también el `B777` de familia que reporta el simulador;
        /// `B73` cubre 737-700/800/900; `A32` cubre A319/A320/A321. Un avión de otra familia
        /// (A320 contra B738, B77L contra A333) no pasa: ahí el aviso sigue siendo el correcto.
        /// </summary>
        internal static bool IsSameFamily(string simType, string planType)
        {
            if (!IsKnown(simType) || !IsKnown(planType)) return false;

            string a = simType.Trim().ToUpperInvariant();
            string b = planType.Trim().ToUpperInvariant();
            if (a == b) return true;

            // Menos de tres caracteres no identifica familia: se exige igualdad exacta (arriba).
            if (a.Length < 3 || b.Length < 3) return false;
            return string.Equals(a.Substring(0, 3), b.Substring(0, 3), StringComparison.Ordinal);
        }

        /// <summary>
        /// ¿Es el mismo avión? Exacto cuando las dos partes tienen la variante, por familia cuando
        /// a alguna le falta.
        /// </summary>
        internal static bool IsSameAircraft(string simAtcModel, string simModel, string simTitle,
                                            string planType)
        {
            if (!IsKnown(simAtcModel) || !IsKnown(planType)) return false;

            string variant = ResolveVariant(simModel, simTitle, simAtcModel);
            if (variant.Length > 0 && IsVariantDesignator(planType))
                return string.Equals(variant, planType.Trim().ToUpperInvariant(), StringComparison.Ordinal);

            return IsSameFamily(simAtcModel, planType);
        }

        /// <summary>
        /// La variante del avión del simulador, o "" si no se puede resolver. Mira primero el
        /// modelo y luego el título —cualquiera de los dos puede traer «777-200LR»— y, si ninguno
        /// lo dice, acepta el propio modelo ATC cuando ya es un designador de variante (`B77L`).
        /// </summary>
        internal static string ResolveVariant(string simModel, string simTitle, string simAtcModel)
        {
            string fromModel = VariantFromText(simModel);
            if (fromModel.Length > 0) return fromModel;

            string fromTitle = VariantFromText(simTitle);
            if (fromTitle.Length > 0) return fromTitle;

            return IsVariantDesignator(simAtcModel)
                ? simAtcModel.Trim().ToUpperInvariant()
                : "";
        }

        /// <summary>
        /// Designador ICAO de la variante a partir de un texto libre del simulador («777-200LR»,
        /// «PMDG 777-200LR British Airways», «737 MAX 8»). "" si no reconoce ninguno.
        /// </summary>
        internal static string VariantFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string upper = text.ToUpperInvariant();
            foreach (var kv in ModelPatterns)          // de más específico a más genérico
                if (upper.Contains(kv.Key)) return kv.Value;

            return "";
        }

        /// <summary>¿Es un designador ICAO de variante conocido, y no un código de familia?</summary>
        internal static bool IsVariantDesignator(string type)
            => IsKnown(type) && VariantDesignators.Contains(type.Trim().ToUpperInvariant());

        // ── Tabla modelo → designador ICAO ────────────────────────────────────────
        // Se ordena por longitud del patrón descendente al construirla: «777-200LR» tiene que
        // ganarle a «777-200», y «737-800» a «737-8» (que es el MAX 8). Añadir un modelo que no
        // esté aquí no rompe nada: cae a la comparación por familia.
        private static readonly List<KeyValuePair<string, string>> ModelPatterns = BuildPatterns();
        private static readonly HashSet<string> VariantDesignators = BuildDesignators();

        private static List<KeyValuePair<string, string>> BuildPatterns()
        {
            var list = new List<KeyValuePair<string, string>>
            {
                // Boeing 777
                P("777-200LR", "B77L"), P("777-200ER", "B772"), P("777-200", "B772"),
                P("777-300ER", "B77W"), P("777-300", "B773"), P("777F", "B77F"),
                P("777-8", "B778"), P("777-9", "B779"),
                // Boeing 737
                P("737 MAX 10", "B3XM"), P("737 MAX 9", "B39M"), P("737 MAX 8", "B38M"),
                P("737 MAX 7", "B37M"), P("737-900", "B739"), P("737-800", "B738"),
                P("737-700", "B73G"), P("737-600", "B736"),
                // Boeing 747 / 767 / 787 / 757
                P("747-400", "B744"), P("747-8", "B748"),
                P("767-200", "B762"), P("767-300", "B763"), P("767-400", "B764"),
                P("787-10", "B78X"), P("787-9", "B789"), P("787-8", "B788"),
                P("757-200", "B752"), P("757-300", "B753"),
                // Airbus
                P("A320NEO", "A20N"), P("A321NEO", "A21N"), P("A319NEO", "A19N"),
                P("A318", "A318"), P("A319", "A319"), P("A320", "A320"), P("A321", "A321"),
                P("A330-200", "A332"), P("A330-300", "A333"),
                P("A330-800", "A338"), P("A330-900", "A339"),
                P("A340-300", "A343"), P("A340-600", "A346"),
                P("A350-1000", "A35K"), P("A350-900", "A359"),
                P("A380", "A388"),
                P("A220-100", "BCS1"), P("A220-300", "BCS3"),
                // Regionales
                P("ATR 72", "AT76"), P("ATR 42", "AT46"), P("AT72", "AT76"), P("AT76", "AT76"),
                P("E170", "E170"), P("E175", "E175"), P("E190", "E190"), P("E195", "E195"),
                P("CRJ-700", "CRJ7"), P("CRJ-900", "CRJ9"), P("CRJ-1000", "CRJX"),
                P("MD-11", "MD11"), P("Q400", "DH8D"), P("DASH 8", "DH8D")
            };
            list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            return list;
        }

        private static KeyValuePair<string, string> P(string pattern, string designator)
            => new KeyValuePair<string, string>(pattern, designator);

        private static HashSet<string> BuildDesignators()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in ModelPatterns) set.Add(kv.Value);
            return set;
        }
    }
}
