using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Los aviones que no tienen spoilers**, para que su indicador fantasma no escriba avisos.
    ///
    /// El ATR no lleva spoilers, y su addon no deja el offset `0x0BD8` en cero: lo publica oscilando
    /// alrededor del 10 % que <see cref="SpoilersPosition"/> exige para decir «desplegado». En el
    /// PIREP real `D0Wy9Pa7GNB5pYEo` (AT76, MSFS 2024) eso dejó **seis pares `DEPLOYED`/`RETRACTED`**
    /// en el descenso, entre 14 500 y 8 300 ft y con el avión limpio:
    ///
    /// ```
    /// 01:27:23 → 01:27:39   01:27:53 → 01:28:13   01:29:11 → 01:30:23
    /// 01:31:18 → 01:31:28   01:31:37 → 01:32:09   01:32:28 → 01:32:36
    /// ```
    ///
    /// y en el propio toque el bloque de datos decía `Spoilers: 0%`.
    ///
    /// **Un tipo que no esté en la lista se comporta como hoy** —esta regla degrada sin datos, no
    /// adivina—: no se toca ninguna señal de un avión que sí los tiene. Es el mismo patrón que
    /// `FlightManager.BeaconStrobeSharedAircraft` con el DH8D: una lista de excepciones por tipo, no
    /// un detector genérico.
    ///
    /// La familia ATR en ICAO son cuatro caracteres que empiezan por `AT4` (ATR 42: AT43/AT45/AT46) o
    /// `AT7` (ATR 72: AT72/AT75/AT76). Se aceptan las dos formas explícitas y la familia, porque el
    /// modelo ATC del simulador no siempre es el designador de variante.
    /// </summary>
    internal static class SpoilerlessAircraft
    {
        /// <summary>Los designadores de tipo conocidos sin spoilers.</summary>
        private static readonly HashSet<string> Types = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AT43",  // ATR 42-300/320
            "AT45",  // ATR 42-500
            "AT46",  // ATR 42-600
            "AT72",  // ATR 72-200
            "AT75",  // ATR 72-500
            "AT76",  // ATR 72-600  ← el del PIREP D0Wy9Pa7GNB5pYEo
        };

        /// <summary>
        /// ¿Este avión no tiene spoilers? **Sin dato devuelve `false`**: se comporta como siempre y
        /// deciden las otras reglas.
        /// </summary>
        internal static bool HasNoSpoilers(string aircraftIcao)
        {
            if (string.IsNullOrEmpty(aircraftIcao)) return false;

            string type = aircraftIcao.Trim().ToUpperInvariant();
            if (type.Length < 4) return false;
            if (Types.Contains(type)) return true;

            // La familia, por si el simulador publica el modelo ATC (AT72 en un 72-600).
            return type.StartsWith("AT4", StringComparison.Ordinal)
                || type.StartsWith("AT7", StringComparison.Ordinal);
        }

        /// <summary>
        /// La fracción publicable: **0 en un avión sin spoilers**, la leída en cualquier otro. La
        /// posición se fuerza a cero en lugar de ignorar el evento para que todo lo que se pinta y se
        /// guarda —el bloque de datos del toque, el `SpoilersDeployed` de la fase de rodaje— diga lo
        /// mismo.
        /// </summary>
        internal static double EffectiveFraction(double fraction, string aircraftIcao)
            => HasNoSpoilers(aircraftIcao) ? 0.0 : fraction;
    }
}
