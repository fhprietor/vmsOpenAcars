using System;
using System.Collections.Generic;
using System.Linq;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La regla del radioaltímetro, en un solo sitio**: el cero del addon y la altura de ruedas.
    ///
    /// El porqué de que exista: el radar de FSUIPC mide desde el **punto de referencia del modelo**,
    /// no desde las ruedas. Ese cero va de **0,1–0,7 ft** en el ToLiss A320 a **13,1 ft** en el iFly
    /// B38M, y **el mismo A320** da 0,2 en el ToLiss y 12,15 en el Fenix. Sin restarlo, la misma
    /// «puerta de 10 ft» significa 0,5 ft de ruedas en un 737 y 9,8 en un A320; con él, 10 ft son
    /// 10 ft en cualquier avión (decisión de phpVMS, 18/11/2026).
    ///
    /// Se mide como la **mediana** de las muestras en tierra del principio del vuelo, no la media:
    /// el radar en tierra da saltos de varios pies y una media los reparte por todo el vuelo. Si el
    /// vuelo empieza en el aire (retomado) no hay muestras y el cero es `null`; entonces la altura
    /// de ruedas también es `null`, **nunca** la indicación cruda disfrazada.
    /// </summary>
    internal static class RadioAltPolicy
    {
        /// <summary>
        /// La mediana de las muestras en tierra, o `null` si no hay ninguna válida. Se descartan las
        /// no finitas y las ≤ 0: un 0 es «el addon no publica» (el centinela de FSUIPC), no un cero
        /// físico medido.
        /// </summary>
        internal static double? ZeroFrom(IEnumerable<double> groundSamples)
        {
            if (groundSamples == null) return null;

            var valid = groundSamples
                .Where(v => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0.0)
                .ToList();
            if (valid.Count == 0) return null;

            valid.Sort();
            return valid[valid.Count / 2];
        }

        /// <summary>
        /// La altura de ruedas: `max(0, radar − cero)`. Sin radar o sin cero, `null`; un radar de 0
        /// (el centinela de «no lo publica») no es una altura de cero pies.
        ///
        /// El `max(0, …)` no es cosmético: en el contacto la indicación baja de 9,5 a 9,3 ft con el
        /// cero en 9,46 y la resta da negativa por el ruido. Las ruedas no están bajo tierra.
        /// </summary>
        internal static double? WheelHeight(double? radarRaw, double? zero)
        {
            if (!radarRaw.HasValue || !zero.HasValue) return null;
            if (radarRaw.Value <= 0.0) return null;   // 0 = «no lo publica», no un avión en el suelo
            if (double.IsNaN(radarRaw.Value) || double.IsNaN(zero.Value) ||
                double.IsInfinity(radarRaw.Value) || double.IsInfinity(zero.Value))
                return null;

            return Math.Max(0.0, radarRaw.Value - zero.Value);
        }
    }
}
