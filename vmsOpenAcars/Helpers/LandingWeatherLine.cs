using System;
using System.Collections.Generic;
using System.Globalization;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La meteo del aterrizaje, en texto**, en los dos formatos que hacen falta:
    ///
    /// - <see cref="Build"/>: la **línea compacta que viaja al PIREP**, al `notes` de `/file`, que es
    ///   texto libre y ya se usaba en el prefile. No se inventa un campo de `pirep_fields` porque un
    ///   campo lo tiene que crear phpVMS primero y una clave desconocida no da error: la ignoraría en
    ///   silencio. Ejemplo real (SKBO 14R, METAR del 25/09/2026 del logbook del piloto):
    ///   `WX LANDING: METAR SKBO 250100Z 01003KT 350V110 9999 BKN080 15/11 Q1028 NOSIG | WIND 010/03 | HW -1 XW -3`
    /// - <see cref="Display"/>: la línea legible del logbook, con el lado del viento escrito.
    ///
    /// Las dos **degradan sin datos**: sin METAR se publica solo el viento, sin viento se publica
    /// solo el METAR, y sin rumbo de pista se publica el viento en crudo pero **no** se inventan las
    /// componentes. Si no hay nada, <see cref="Build"/> devuelve `null` y no se añade nada al `notes`.
    /// </summary>
    internal static class LandingWeatherLine
    {
        /// <summary>Prefijo con el que la línea viaja en el `notes` del PIREP. Es lo que permite a la
        /// aerolínea distinguirla del resto del texto de `notes` sin ambigüedad.</summary>
        internal const string Prefix = "WX LANDING:";

        /// <summary>
        /// Línea compacta del PIREP, o `null` si no hay ni METAR ni viento. El METAR va **en crudo y
        /// entero** (ya contiene su hora de observación, `250100Z`): recortarlo o reformatearlo
        /// rompería la comparación con la fuente.
        /// </summary>
        internal static string Build(string metarRaw, DateTime? observedAtUtc, WindComponentResult wind)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(metarRaw))
                parts.Add(metarRaw.Trim());

            string raw = WindComponents.FormatRaw(wind);
            if (raw != "—")
            {
                string windPart = "WIND " + raw;
                string components = WindComponents.Format(wind);
                if (components != "—") windPart += " | " + components;
                parts.Add(windPart);
            }

            if (parts.Count == 0) return null;
            return Prefix + " " + string.Join(" | ", parts);
        }

        /// <summary>
        /// Línea legible del logbook. El METAR no se repite aquí (el formulario ya lo pinta en su
        /// propia franja); esta línea es la del **viento y sus componentes**, con el lado escrito en
        /// palabras porque el signo solo no se lee de un vistazo.
        /// </summary>
        internal static string Display(string runwayName, WindComponentResult wind)
        {
            string rwy = string.IsNullOrWhiteSpace(runwayName) ? "—" : runwayName.Trim().ToUpperInvariant();

            string raw = WindComponents.FormatRaw(wind);
            if (raw == "—")
                return "WIND —   (no wind recorded at touchdown)";

            string heading = wind.RunwayHeadingTrueDeg.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "RWY {0} TRUE {1:0}°",
                                rwy, wind.RunwayHeadingTrueDeg.Value)
                : string.Format(CultureInfo.InvariantCulture, "RWY {0} TRUE —", rwy);

            if (wind.Calm)
                return string.Format(CultureInfo.InvariantCulture,
                                     "WIND {0} kt   ·   {1}   ·   CALM", raw, heading);

            if (!wind.Available)
                return string.Format(CultureInfo.InvariantCulture,
                                     "WIND {0} kt   ·   {1}   ·   components not computable " +
                                     "(no runway heading)", raw, heading);

            string side = wind.CrosswindKt > 0 ? "from the right"
                        : wind.CrosswindKt < 0 ? "from the left"
                        : "along the runway";
            string head = wind.HeadwindKt >= 0
                ? string.Format(CultureInfo.InvariantCulture, "HEADWIND {0:0.0} kt", wind.HeadwindKt)
                : string.Format(CultureInfo.InvariantCulture, "TAILWIND {0:0.0} kt", -wind.HeadwindKt);

            return string.Format(CultureInfo.InvariantCulture,
                                 "WIND {0} kt   ·   {1}   ·   {2}   ·   CROSSWIND {3:0.0} kt ({4})",
                                 raw, heading, head, Math.Abs(wind.CrosswindKt), side);
        }
    }
}
