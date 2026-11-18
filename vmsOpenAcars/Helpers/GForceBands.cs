using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Las bandas del **G medido en el toque**: cuánto se deduce por cada una y cómo se llama.
    ///
    /// Existe porque el rótulo del registro y el scoring **tenían dos tablas distintas** y podían
    /// decir cosas opuestas del mismo número. Medido en el PIREP real **`ez9OqzRnYxbnGLR8`**
    /// (B738 PMDG, MSFS 2024, SKBO→SKBG, **−236 fpm**): el registro pintó
    /// `Fuerza G: 1.72g (Normal)` con el umbral viejo del rótulo (`< 1.8`) mientras el scoring
    /// cobraba **los 15 puntos de la banda máxima** (`> 1.7`). El mismo valor no puede ser «Normal»
    /// y estar en la banda que más resta.
    ///
    /// **Cuál de las dos tablas era la buena lo confirmó el mantenedor: 1,7.** Las bandas del
    /// scoring (`≤ 1,5` / `≤ 1,7` / `> 1,7`) **no se mueven**; lo que cambia es que ahora son las
    /// únicas, y el rótulo se deriva de ellas en vez de tener su propia copia.
    ///
    /// **La banda de arriba se parte en dos rótulos que comparten la misma deducción**: decir
    /// «Dura» a un toque de **3 g** perdería el aviso que el piloto sí necesita, pero un segundo
    /// umbral de puntuación sería justo la divergencia que esto viene a quitar. Así que el corte de
    /// 2,5 g **solo elige la palabra**; la deducción de la banda es la misma (15) para las dos.
    /// </summary>
    internal enum GForceBand
    {
        /// <summary>`≤ 1,5 g`: sin deducción. Un toque suave de verdad.</summary>
        Smooth,

        /// <summary>`> 1,5 y ≤ 1,7 g`: 7 puntos. Firme pero normal.</summary>
        Firm,

        /// <summary>`> 1,7 y ≤ 2,5 g`: 15 puntos, la banda máxima. Dura.</summary>
        Hard,

        /// <summary>`> 2,5 g`: 15 puntos, los mismos que <see cref="Hard"/>. Solo cambia el rótulo.</summary>
        Severe,
    }

    /// <summary>La tabla única de bandas del G del toque (ver <see cref="GForceBand"/>).</summary>
    internal static class GForceBands
    {
        /// <summary>Borde superior de «sin deducción». Es el umbral del scoring, no se mueve.</summary>
        internal const double SmoothMax = 1.5;

        /// <summary>Borde superior de «firme». **Es el 1,7 que confirmó el mantenedor.**</summary>
        internal const double FirmMax = 1.7;

        /// <summary>
        /// Borde superior de «dura». **No es un umbral de puntuación**: solo separa el rótulo
        /// `Hard` del rótulo `Crash` dentro de la banda máxima.
        /// </summary>
        internal const double HardMax = 2.5;

        /// <summary>La banda en la que cae un G medido.</summary>
        internal static GForceBand Classify(double gForce)
        {
            if (gForce <= SmoothMax) return GForceBand.Smooth;
            if (gForce <= FirmMax)   return GForceBand.Firm;
            if (gForce <= HardMax)   return GForceBand.Hard;
            return GForceBand.Severe;
        }

        /// <summary>Lo que resta esta banda. `0 / 7 / 15 / 15`.</summary>
        internal static int Deduction(GForceBand band)
        {
            switch (band)
            {
                case GForceBand.Smooth: return 0;
                case GForceBand.Firm:   return 7;
                default:                return 15;
            }
        }

        /// <summary>La deducción del G medido, sin pasar por el rótulo.</summary>
        internal static int Deduction(double gForce) => Deduction(Classify(gForce));

        /// <summary>
        /// La clave del rótulo que se pinta en el registro. Las cuatro claves ya existen en los dos
        /// `.json` (`Score_Perfect`, `Score_Normal`, `Score_Hard`, `Score_Crash`).
        /// </summary>
        internal static string LabelKey(GForceBand band)
        {
            switch (band)
            {
                case GForceBand.Smooth: return "Score_Perfect";
                case GForceBand.Firm:   return "Score_Normal";
                case GForceBand.Hard:   return "Score_Hard";
                default:                return "Score_Crash";
            }
        }

        internal static string LabelKey(double gForce) => LabelKey(Classify(gForce));
    }
}
