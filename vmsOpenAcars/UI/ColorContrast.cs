using System;
using System.Drawing;

namespace vmsOpenAcars.UI
{
    /// <summary>
    /// Contraste entre dos colores, con la fórmula de la WCAG 2.1 (luminancia relativa y ratio
    /// `(L1+0.05)/(L2+0.05)`).
    ///
    /// Existe porque el cambio de convenio —el color deja de ser fondo y pasa a ser leyenda sobre un
    /// cuerpo negro— **invierte el problema de legibilidad**: un gris pizarra `(60,70,80)` que funcionaba
    /// como relleno con letra blanca encima es ilegible como letra sobre negro. Ese caso no lo ve el
    /// compilador y tampoco un ojo que mire el formulario en un monitor bueno, así que va a un test:
    /// `MainButtonBarTests` exige un ratio mínimo a **cada** color de la barra.
    ///
    /// Es puro (sin red ni WinForms) para poder probarlo, como los demás helpers del proyecto.
    /// </summary>
    internal static class ColorContrast
    {
        /// <summary>Mínimo de la WCAG para texto normal: 4.5:1. Por debajo, la leyenda no se lee.</summary>
        internal const double MinimumForText = 4.5;

        /// <summary>Luminancia relativa WCAG de un color (0 = negro, 1 = blanco).</summary>
        internal static double RelativeLuminance(Color color)
        {
            double r = Channel(color.R);
            double g = Channel(color.G);
            double b = Channel(color.B);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>Ratio de contraste entre dos colores, de 1:1 (idénticos) a 21:1 (negro contra blanco).</summary>
        internal static double Ratio(Color a, Color b)
        {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            double light = Math.Max(la, lb);
            double dark = Math.Min(la, lb);
            return (light + 0.05) / (dark + 0.05);
        }

        private static double Channel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
    }
}
