using System;
using System.Collections.Generic;
using System.Drawing;

namespace vmsOpenAcars.UI
{
    /// <summary>
    /// La paleta del MCDU del A320, **muestreada de la foto de referencia** (413×638) que pasó el
    /// mantenedor, no elegida a ojo. Cada valor lleva de dónde sale:
    ///
    /// · **Pantalla**: fondo #1B1D20 (55.122 px, el color dominante de la foto), blanco #F8F4ED
    ///   (604 px), verde #08DA09 (519 px) y cian #09CFCF (374 px).
    /// · **Carcasa**: una escala de plástico iluminado de #93A6BD (solo el reflejo del canto de arriba)
    ///   a #5A6A7E, con el desgaste en #505F71 y las sombras duras en #39424D.
    /// · **Tecla**: cara #323233 (11.900 px contando las dos) — **gris oscuro cálido, no negro**.
    ///
    /// **Lo que NO está medido**: el ámbar y el rojo. En esa foto la pantalla solo tiene blanco, verde y
    /// cian: no hay un solo píxel cálido (R&gt;B+25) ni rosado. Van por convención MCDU y así está dicho
    /// en `Docs/PROPUESTA-MCDU-CARCASA.md`.
    ///
    /// Y la regla que manda sobre la estética: **todo texto tiene que pasar 4,5:1** contra el fondo en el
    /// que se pinta (WCAG AA), que aquí son dos fondos distintos —el LCD #1B1D20 y el plástico—. Lo
    /// comprueba `McduPaletteTests`. Por eso la cabecera y las etiquetas sobre el plástico van **grabadas
    /// en oscuro** (`Ink`): el blanco sobre #859AB2 se queda en 2,9:1 y el cian de la app en 1,5:1.
    /// </summary>
    internal static class McduPalette
    {
        // ── Carcasa: el plástico azuloso, gastado ────────────────────────────────────────────────────

        /// <summary>Reflejo del canto de arriba. En la foto es un brillo, no el color del panel.</summary>
        internal static readonly Color ChassisLight = Color.FromArgb(0x93, 0xA6, 0xBD);
        internal static readonly Color ChassisHigh  = Color.FromArgb(0x85, 0x9A, 0xB2);
        internal static readonly Color ChassisMid   = Color.FromArgb(0x7D, 0x90, 0xA8);
        internal static readonly Color ChassisLow   = Color.FromArgb(0x6A, 0x7C, 0x92);
        internal static readonly Color ChassisDirt  = Color.FromArgb(0x5A, 0x6A, 0x7E);
        internal static readonly Color ChassisWorn  = Color.FromArgb(0x50, 0x5F, 0x71);
        internal static readonly Color Seam         = Color.FromArgb(0x45, 0x50, 0x5E);
        internal static readonly Color SeamDark     = Color.FromArgb(0x39, 0x42, 0x4D);

        /// <summary>
        /// Tinta grabada para el texto que va **sobre el plástico**. No es un capricho: #1F2933 sobre el
        /// plástico claro da 5,1:1 y el blanco 2,9:1, así que la cabecera va grabada como las leyendas
        /// serigrafiadas de un panel de verdad.
        /// </summary>
        internal static readonly Color Ink = Color.FromArgb(0x1F, 0x29, 0x33);

        internal static readonly Color KeyFace    = Color.FromArgb(0x32, 0x32, 0x33);
        internal static readonly Color KeyFaceLow = Color.FromArgb(0x39, 0x38, 0x39);
        internal static readonly Color ScrewLight = Color.FromArgb(0xF8, 0xF4, 0xED);
        internal static readonly Color ScrewDark  = Color.FromArgb(0x81, 0x95, 0xAD);

        // ── Pantalla ─────────────────────────────────────────────────────────────────────────────────

        internal static readonly Color LcdBackground  = Color.FromArgb(0x1B, 0x1D, 0x20);
        internal static readonly Color LcdBackground2 = Color.FromArgb(0x22, 0x25, 0x27);
        internal static readonly Color LcdWhite       = Color.FromArgb(0xF8, 0xF4, 0xED);
        internal static readonly Color LcdGreen       = Color.FromArgb(0x08, 0xDA, 0x09);
        internal static readonly Color LcdCyan        = Color.FromArgb(0x09, 0xCF, 0xCF);

        /// <summary>
        /// Verde de lectura para el cuerpo del log. El medido (#08DA09) es el del aparato y se queda para
        /// los valores cortos; éste baja la saturación —que es lo que cansa en un párrafo— y sigue dando
        /// 8,0:1 sobre el fondo del LCD.
        /// </summary>
        internal static readonly Color LcdGreenSoft = Color.FromArgb(0x3F, 0xCB, 0x55);

        /// <summary>Ámbar de avisos. **Convención MCDU, no medido** (ver el comentario de la clase).</summary>
        internal static readonly Color LcdAmber = Color.FromArgb(0xFF, 0xB0, 0x20);

        /// <summary>Rojo de alarma que se lee sobre el LCD (el rojo puro de la app da 4,0:1 y no pasa).</summary>
        internal static readonly Color LcdRed = Color.FromArgb(0xFF, 0x7A, 0x6B);

        /// <summary>Gris apagado para lo que no es ni dato ni aviso, pero tiene que leerse.</summary>
        internal static readonly Color LcdDim = Color.FromArgb(0x8A, 0x93, 0xA0);

        // ── El interruptor de la textura ─────────────────────────────────────────────────────────────

        private static bool _textured = true;

        /// <summary>
        /// Si está apagado, la carcasa y las pantallas conservan sus colores y sus biseles pero pierden el
        /// grano, las manchas, los rayones y las líneas de barrido. Es la válvula para el piloto que lee el
        /// log durante horas: vuelve al aspecto descansado sin perder la estructura. Lo enciende y lo apaga
        /// `mcdu_look`, y `MainForm` lo pone al arrancar.
        /// </summary>
        internal static bool Textured
        {
            get { return _textured; }
            set { _textured = value; }
        }

        // ── El color del texto en el log ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Traduce los colores que la aplicación ya usa en el log (`Theme.*`) a la paleta del LCD. Va aquí,
        /// en un solo sitio, y no en los cientos de llamadas a `AddLog`: así el log entero cambia de piel de
        /// golpe y se puede probar que **ningún color de `Theme` acaba ilegible** sobre el fondo del LCD.
        /// Lo que no esté en la tabla se conserva si se lee, y si no, cae al verde de lectura.
        /// </summary>
        internal static Color ForLog(Color original)
        {
            Color elegido;

            if (_logMap.TryGetValue(original.ToArgb(), out elegido))
                return elegido;

            // Un color que ya se lee sobre el LCD se respeta: la tabla es para los de `Theme`, no para
            // prohibir colores.
            if (ColorContrast.Ratio(original, LcdBackground) >= ColorContrast.MinimumForText)
                return original;

            return LcdGreenSoft;
        }

        private static readonly Dictionary<int, Color> _logMap = CrearMapaDelLog();

        /// <summary>
        /// La tabla va por **valor de color**, no por miembro de `Theme`: así esta clase no depende de la
        /// paleta de la aplicación (la puede usar el arnés, que no compila `Theme.cs`) y sigue siendo el
        /// único sitio donde se decide qué color se lee en el LCD. Al lado de cada valor, a qué miembro de
        /// `Theme` corresponde hoy, para que se pueda seguir con la vista.
        /// </summary>
        private static Dictionary<int, Color> CrearMapaDelLog()
        {
            var mapa = new Dictionary<int, Color>();

            mapa[Color.FromArgb(0, 255, 255).ToArgb()] = LcdCyan;        // Theme.MainText
            mapa[Color.FromArgb(128, 255, 255).ToArgb()] = LcdCyan;      // Theme.SecondaryText

            mapa[Color.FromArgb(0, 200, 0).ToArgb()] = LcdGreenSoft;     // Theme.Success / Taxi / Enroute

            mapa[Color.FromArgb(255, 165, 0).ToArgb()] = LcdAmber;       // Theme.Warning
            mapa[Color.FromArgb(255, 69, 0).ToArgb()] = LcdAmber;        // Theme.Caution
            mapa[Color.FromArgb(255, 128, 0).ToArgb()] = LcdAmber;       // Theme.Approach
            mapa[Color.FromArgb(255, 255, 0).ToArgb()] = LcdAmber;       // Theme.Takeoff

            mapa[Color.FromArgb(255, 0, 0).ToArgb()] = LcdRed;           // Theme.Danger / Landing
            mapa[Color.FromArgb(128, 128, 128).ToArgb()] = LcdDim;       // Theme.Arrived

            mapa[Color.White.ToArgb()] = LcdWhite;
            mapa[Color.LightGreen.ToArgb()] = LcdGreenSoft;
            mapa[Color.Green.ToArgb()] = LcdGreenSoft;
            mapa[Color.Lime.ToArgb()] = LcdGreen;
            mapa[Color.Yellow.ToArgb()] = LcdAmber;
            mapa[Color.Orange.ToArgb()] = LcdAmber;
            mapa[Color.Cyan.ToArgb()] = LcdCyan;
            mapa[Color.Red.ToArgb()] = LcdRed;
            mapa[Color.Gray.ToArgb()] = LcdDim;
            mapa[Color.DimGray.ToArgb()] = LcdDim;
            mapa[Color.Black.ToArgb()] = LcdGreenSoft;   // tinta invisible en el LCD: se sustituye

            return mapa;
        }
    }
}
