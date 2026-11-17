using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Qué tipos de espacio aéreo acotado son una violación** y cuáles solo una advertencia.
    ///
    /// Regla (phpVMS, mensaje del 16/11/2026): cuentan **`Prohibited` y `Restricted`**; **`Danger` se
    /// registra pero no avisa** —ni en el servidor ni aquí—, porque es una zona de peligro y no una
    /// prohibición de vuelo.
    ///
    /// El porqué de que esto sea un helper con test y no dos comparaciones sueltas: la auditoría de
    /// los **10 PIREPs** que el cliente mandó a revisión encontró que **2 eran `Danger`**, o sea que
    /// el cliente marcó como invasión un vuelo que no había invadido nada. El aviso en pantalla y en
    /// el OSD de una `Danger` sigue saliendo: lo que no se hace es mandar el PIREP a revisión.
    /// </summary>
    internal static class AirspaceViolationPolicy
    {
        /// <summary>
        /// ¿Entrar en este tipo de zona es una violación? Sin dato (`null`, vacío) se responde que
        /// **no**: los filtros de este cliente degradan sin datos, y marcar un vuelo por un tipo que
        /// no llegó sería inventarse la falta.
        /// </summary>
        internal static bool IsViolation(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return false;
            string value = type.Trim();

            return string.Equals(value, "Prohibited", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Restricted", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// ¿El tipo merece un aviso en pantalla/OSD? `Danger` sí: es útil saber que se entra en una
        /// zona de peligro, aunque no marque el PIREP.
        /// </summary>
        internal static bool IsAdvisory(string type)
            => string.Equals((type ?? "").Trim(), "Danger", StringComparison.OrdinalIgnoreCase);
    }
}
