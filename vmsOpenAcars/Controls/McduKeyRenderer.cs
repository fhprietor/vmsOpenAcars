using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace vmsOpenAcars.Controls
{
    /// <summary>Estado visual de una tecla. El cuerpo es negro en los cuatro: lo que cambia es el bisel.</summary>
    internal enum McduKeyState
    {
        Normal,
        Hover,
        Pressed,
        Disabled
    }

    /// <summary>
    /// Dibuja **una tecla retroiluminada estilo MCDU A320**: cuerpo negro con bisel de dos tonos,
    /// esquinas redondeadas y la leyenda **encendida por detrás** en el color del botón.
    ///
    /// Es `static` y recibe el `Graphics` y el rectángulo: no depende de ninguna ventana, así que se puede
    /// previsualizar a cualquier escala y probar sin abrir un formulario. Dos reglas se respetan aquí:
    ///
    /// 1. **El color nunca vive en el cuerpo**: el capuchón es negro en los cuatro estados y lo único que
    ///    se mueve es el bisel (luz arriba, sombra abajo al soltar; al revés al pulsar).
    /// 2. **La luz viene de detrás**, que es lo que hace que la tecla parezca retroiluminada y no pintada:
    ///    un **lavado** del propio color dentro del capuchón —la luz que atraviesa el plástico— y un
    ///    **halo** alrededor de las letras —la que sangra por la serigrafía—. El halo es un desenfoque de
    ///    la propia leyenda (se reduce a un cuarto y se vuelve a ampliar con bicúbica), así que pega con la
    ///    forma de cada rótulo y no hay que dibujar ninguna aureola a mano.
    /// </summary>
    internal static class McduKeyRenderer
    {
        /// <summary>Radio de la esquina del capuchón, en px.</summary>
        internal const float CornerRadius = 4f;

        /// <summary>Banderas con las que se mide y se dibuja la leyenda: sin relleno y en una sola línea.</summary>
        private const TextFormatFlags LegendFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                                                    TextFormatFlags.SingleLine;

        /// <summary>Tamaño de sondeo para medir: GDI necesita un rectángulo, y aquí no se recorta nada.</summary>
        private static readonly Size MaxSize = new Size(int.MaxValue, int.MaxValue);

        /// <summary>Cuánto se apaga la leyenda de un botón deshabilitado (1 = intacta).</summary>
        internal const float DisabledFactor = 0.45f;

        /// <summary>
        /// Aire que se deja a cada lado de la leyenda, en px. Es lo que separa las letras del borde del
        /// capuchón: sin él, un rótulo que «cabe» lo hace tocando el bisel.
        /// </summary>
        internal const float LegendInset = 4f;

        /// <summary>
        /// Cuerpo mínimo al que se puede encoger una leyenda para que quepa. Por debajo no se lee, así que
        /// un rótulo imposible se queda aquí y **se recorta** contra el capuchón en vez de salirse.
        /// </summary>
        internal const float MinLegendSize = 6f;

        /// <summary>
        /// Opacidad del lavado de color dentro del capuchón (0-255). Es la luz que viene de detrás del
        /// plástico: sube el brillo del fondo justo donde está la leyenda, así que **no puede crecer sin
        /// medir el contraste** — de ahí que `McduKeyRenderer.BacklitBody` exista y que el test lo use.
        /// </summary>
        internal const int WashAlpha = 26;

        /// <summary>Punto medio del degradado del capuchón: donde se apoya la leyenda.</summary>
        private static readonly Color BodyCenter = Color.FromArgb(33, 33, 37);

        // Cuerpo: SIEMPRE negro. Entre estados solo se mueve el brillo del degradado.
        private static readonly Color BodyTop      = Color.FromArgb(56, 56, 62);
        private static readonly Color BodyBottom   = Color.FromArgb(12, 12, 15);
        private static readonly Color BodyTopHot   = Color.FromArgb(72, 72, 80);
        private static readonly Color BodyBotHot   = Color.FromArgb(20, 20, 24);
        private static readonly Color BodyTopDown  = Color.FromArgb(9, 9, 11);
        private static readonly Color BodyBotDown  = Color.FromArgb(21, 21, 25);
        private static readonly Color BodyTopOff   = Color.FromArgb(32, 32, 37);
        private static readonly Color BodyBotOff   = Color.FromArgb(12, 12, 15);

        // Borde del capuchón.
        private static readonly Color Edge        = Color.FromArgb(96, 96, 106);
        private static readonly Color EdgeHot     = Color.FromArgb(138, 138, 150);
        private static readonly Color EdgeDown    = Color.FromArgb(44, 44, 50);
        private static readonly Color EdgeOff     = Color.FromArgb(52, 52, 58);

        /// <summary>Filo superior iluminado: es el detalle que hace que la tecla parezca tener volumen.</summary>
        private static readonly Color RimLight   = Color.FromArgb(152, 152, 165);

        /// <summary>Leyenda apagada: el color del botón, pero hundido en el negro (no gris neutro).</summary>
        internal static Color DisabledLegend(Color legend)
        {
            return Dim(legend, DisabledFactor);
        }

        /// <summary>Mezcla un color hacia el negro del cuerpo. `factor` = 0 → negro, 1 → el color intacto.</summary>
        internal static Color Dim(Color color, float factor)
        {
            if (factor < 0f) factor = 0f;
            if (factor > 1f) factor = 1f;
            return Color.FromArgb(
                (int)Math.Round(color.R * factor),
                (int)Math.Round(color.G * factor),
                (int)Math.Round(color.B * factor));
        }

        /// <summary>Aclara un color hacia el blanco. Solo lo usa la leyenda de la tecla pulsada.</summary>
        internal static Color Brighten(Color color, float factor)
        {
            if (factor <= 1f) return color;
            float t = Math.Min(1f, factor - 1f);
            return Color.FromArgb(
                (int)Math.Round(color.R + (255 - color.R) * t),
                (int)Math.Round(color.G + (255 - color.G) * t),
                (int)Math.Round(color.B + (255 - color.B) * t));
        }

        /// <summary>Pone `overlay` encima de `background` con la opacidad dada (0 = nada, 1 = todo).</summary>
        internal static Color Blend(Color background, Color overlay, float alpha)
        {
            if (alpha < 0f) alpha = 0f;
            if (alpha > 1f) alpha = 1f;
            return Color.FromArgb(
                (int)Math.Round(background.R + (overlay.R - background.R) * alpha),
                (int)Math.Round(background.G + (overlay.G - background.G) * alpha),
                (int)Math.Round(background.B + (overlay.B - background.B) * alpha));
        }

        /// <summary>
        /// El fondo que hay **realmente detrás de la leyenda**: el capuchón con su lavado de color encima.
        /// Es contra esto —y no contra el negro puro— contra lo que hay que medir el contraste de la
        /// leyenda, porque la luz de detrás sube el brillo del fondo. Lo usa `MainButtonBarTests`.
        /// </summary>
        internal static Color BacklitBody(Color legend)
        {
            return Blend(BodyCenter, legend, WashAlpha / 255f);
        }

        /// <param name="legendSize">
        /// Cuerpo de letra ya decidido para el grupo de teclas (0 = que lo decida el renderizador por su
        /// cuenta). Lo usa `McduKeyButton` para que todas las teclas hermanas compartan tamaño: la armonía
        /// del grupo manda sobre el tamaño individual de cada rótulo.
        /// </param>
        internal static void Paint(Graphics g, Rectangle bounds, string text, Color legend,
                                   Font font, McduKeyState state, float legendSize = 0f)
        {
            if (g == null || bounds.Width < 8 || bounds.Height < 8) return;

            SmoothingMode prevSmoothing = g.SmoothingMode;
            TextRenderingHint prevHint  = g.TextRenderingHint;
            PixelOffsetMode prevOffset  = g.PixelOffsetMode;
            InterpolationMode prevInterp = g.InterpolationMode;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            try
            {
                Color top, bottom, edge;
                int rimTopAlpha, shadeAlpha;
                float legendShiftY, legendBoost, bloom;

                switch (state)
                {
                    case McduKeyState.Hover:
                        top = BodyTopHot; bottom = BodyBotHot; edge = EdgeHot;
                        rimTopAlpha = 210; shadeAlpha = 190;
                        legendShiftY = 0f; legendBoost = 1f; bloom = 0.95f;
                        break;
                    case McduKeyState.Pressed:
                        // Hundida: el cuerpo se apaga del todo, el bisel se invierte (sombra arriba, sin
                        // filo) y la tecla **se enciende**: leyenda más brillante y más halo. Es lo que lee
                        // el ojo como «ha cedido y ha respondido».
                        top = BodyTopDown; bottom = BodyBotDown; edge = EdgeDown;
                        rimTopAlpha = 0; shadeAlpha = 0;
                        legendShiftY = 1f; legendBoost = 1.18f; bloom = 1.25f;
                        break;
                    case McduKeyState.Disabled:
                        top = BodyTopOff; bottom = BodyBotOff; edge = EdgeOff;
                        rimTopAlpha = 60; shadeAlpha = 120;
                        legendShiftY = 0f; legendBoost = 1f; bloom = 0.22f;
                        break;
                    default:
                        top = BodyTop; bottom = BodyBottom; edge = Edge;
                        rimTopAlpha = 165; shadeAlpha = 185;
                        legendShiftY = 0f; legendBoost = 1f; bloom = 0.8f;
                        break;
                }

                float radius = Math.Min(CornerRadius, Math.Min(bounds.Width, bounds.Height) / 3f);
                RectangleF cap = new RectangleF(bounds.X + 0.5f, bounds.Y + 0.5f,
                                                bounds.Width - 1f, bounds.Height - 1f);

                // El color con el que se ilumina esta tecla: apagado si está deshabilitada, más brillante
                // si está pulsada. Todo lo que brilla —lavado, halo y leyenda— sale de aquí.
                Color glow = state == McduKeyState.Disabled
                    ? DisabledLegend(legend)
                    : Brighten(legend, legendBoost);

                using (GraphicsPath path = RoundedRect(cap, radius))
                {
                    using (LinearGradientBrush fill = new LinearGradientBrush(
                               new RectangleF(cap.X, cap.Y, cap.Width, cap.Height + 1f),
                               top, bottom, LinearGradientMode.Vertical))
                    {
                        g.FillPath(fill, path);
                    }

                    // La luz que viene de detrás: un lavado del propio color, más denso en el centro
                    // —donde está la leyenda— y desvanecido hacia el borde del capuchón.
                    using (PathGradientBrush wash = new PathGradientBrush(path))
                    {
                        wash.CenterColor = Color.FromArgb(WashAlpha, glow);
                        wash.SurroundColors = new Color[] { Color.FromArgb(0, glow) };
                        wash.SetSigmaBellShape(0.4f);
                        g.FillPath(wash, path);
                    }

                    // Filo iluminado arriba (el detalle que da volumen al capuchón) y sombra abajo.
                    if (rimTopAlpha > 0)
                    {
                        using (Pen rim = new Pen(Color.FromArgb(rimTopAlpha, RimLight), 1f))
                            g.DrawLine(rim, cap.X + radius, cap.Y + 1.0f, cap.Right - radius, cap.Y + 1.0f);
                    }
                    if (state == McduKeyState.Pressed)
                    {
                        // Sombra interior pegada al borde de arriba: el hueco que deja el capuchón hundido.
                        using (Pen inner = new Pen(Color.FromArgb(150, 0, 0, 0), 2f))
                            g.DrawLine(inner, cap.X + radius, cap.Y + 2.0f, cap.Right - radius, cap.Y + 2.0f);
                    }
                    if (shadeAlpha > 0)
                    {
                        using (Pen shade = new Pen(Color.FromArgb(shadeAlpha, 0, 0, 0), 1f))
                            g.DrawLine(shade, cap.X + radius, cap.Bottom - 1.0f, cap.Right - radius, cap.Bottom - 1.0f);
                    }

                    // La luz que sangra por la junta del capuchón.
                    using (Pen leak = new Pen(Color.FromArgb((int)(55 * bloom), glow), 1f))
                        g.DrawLine(leak, cap.X + radius, cap.Bottom - 2.0f, cap.Right - radius, cap.Bottom - 2.0f);

                    using (Pen border = new Pen(edge, 1f))
                        g.DrawPath(border, path);
                }

                // La leyenda (y su halo) no pueden salirse de la tecla: si ni encogida al mínimo cabe, se
                // recorta contra el capuchón en vez de invadir la tecla de al lado.
                GraphicsState clip = g.Save();
                g.SetClip(new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2));
                try
                {
                    DrawLegendBloom(g, bounds, text, glow, font, legendShiftY, bloom, legendSize);
                    DrawLegend(g, bounds, text, glow, font, legendShiftY, legendSize);
                }
                finally
                {
                    g.Restore(clip);
                }
            }
            finally
            {
                g.SmoothingMode = prevSmoothing;
                g.TextRenderingHint = prevHint;
                g.PixelOffsetMode = prevOffset;
                g.InterpolationMode = prevInterp;
            }
        }

        /// <summary>
        /// El halo de la leyenda: la luz que sale por la serigrafía. Es la propia leyenda desenfocada, así
        /// que pega con la forma de cada rótulo sin dibujar ninguna aureola a mano — se pinta el texto en una
        /// capa aparte, se reduce a un cuarto y se vuelve a ampliar con bicúbica (un desenfoque barato, que
        /// es todo lo que hace falta para un halo de 3-4 px) y se echa encima con la opacidad del estado.
        /// </summary>
        private static void DrawLegendBloom(Graphics g, Rectangle bounds, string text, Color color,
                                            Font font, float shiftY, float strength, float legendSize)
        {
            if (string.IsNullOrEmpty(text) || font == null || strength <= 0.01f) return;
            if (bounds.Width < 16 || bounds.Height < 10) return;

            int w = bounds.Width;
            int h = bounds.Height;

            // La capa va **opaca** (32bppRgb) por dos motivos: el texto lo pinta GDI, que sobre un mapa de
            // bits con alfa deja el alfa a cero y desaparece, y así el halo se puede construir por
            // luminancia —blanco sobre negro— y teñir después al componer, sin volver a dibujar el texto.
            using (Bitmap layer = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
            {
                using (Graphics lg = Graphics.FromImage(layer))
                {
                    lg.Clear(Color.Black);
                    DrawLegend(lg, new Rectangle(0, 0, w, h), text, Color.White, font, shiftY, legendSize);
                }

                int sw = Math.Max(2, w / 4);
                int sh = Math.Max(2, h / 4);

                using (Bitmap small = new Bitmap(sw, sh, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                {
                    using (Graphics sg = Graphics.FromImage(small))
                    {
                        sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        sg.DrawImage(layer, new Rectangle(0, 0, sw, sh));
                    }

                    using (System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes())
                    {
                        attributes.SetColorMatrix(LuminanceTint(color, strength));

                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(small, new Rectangle(bounds.X, bounds.Y, w, h),
                                    0, 0, sw, sh, GraphicsUnit.Pixel, attributes);
                    }
                }
            }
        }

        /// <summary>
        /// La matriz que convierte el halo en blanco sobre negro en **luz del color del botón**: tiñe con
        /// el color y saca el alfa de la luminancia, así que el negro de la capa desaparece y solo queda el
        /// resplandor de las letras, con la opacidad del estado.
        /// </summary>
        private static System.Drawing.Imaging.ColorMatrix LuminanceTint(Color color, float strength)
        {
            const float lr = 0.2126f, lg = 0.7152f, lb = 0.0722f;   // luminancia relativa (WCAG)

            float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;

            System.Drawing.Imaging.ColorMatrix matrix = new System.Drawing.Imaging.ColorMatrix();
            matrix.Matrix00 = lr * r; matrix.Matrix10 = lg * r; matrix.Matrix20 = lb * r;
            matrix.Matrix01 = lr * g; matrix.Matrix11 = lg * g; matrix.Matrix21 = lb * g;
            matrix.Matrix02 = lr * b; matrix.Matrix12 = lg * b; matrix.Matrix22 = lb * b;
            matrix.Matrix03 = lr * strength; matrix.Matrix13 = lg * strength; matrix.Matrix23 = lb * strength;
            matrix.Matrix33 = 0f;
            matrix.Matrix44 = 1f;
            return matrix;
        }

        /// <summary>
        /// Leyenda centrada, **de una sola tirada**.
        ///
        /// Va por **GDI** (`TextRenderer`) y no por GDI+, y no es un detalle: GDI+ **no hace enlace de
        /// fuentes**, así que un carácter que Consolas no tenga sale como un cuadro vacío. Los botones de
        /// la aplicación usan unos cuantos —`✕` de cerrar, `▸`/`▾` de los desplegables, y los emoji de
        /// `💾 PNG`, `🗑 DELETE BID`, `📋 PLAN IN SIMBRIEF` y `🔄 FETCH OFP`—, y con `DrawString` el
        /// engranaje de la cabecera o el disquete salían en blanco (medido: `Tools/McduPreview/probe`).
        /// GDI los resuelve contra las fuentes del sistema y además es más afilado en pantalla.
        ///
        /// **Y se dibuja la cadena entera, no letra a letra.** Dibujarla por caracteres era el efecto MCDU
        /// que se buscaba (el aire entre letras), pero se medía cada carácter por separado y esa medida
        /// **se infla con el DPI**: medido en un proceso al 200 %, cada letra de `DESPACHO` salía a 24 px
        /// cuando el ancho real de la cadena es de 16 px por letra. El resultado era una leyenda más ancha
        /// de lo que es —y por tanto encogida antes de tiempo, que es justo lo que se vio en la barra—.
        /// De una tirada, GDI mide y coloca como dibuja, y los pares surogados y los selectores de variación
        /// (`📋` U+1F4CB, `⚙️` U+FE0F) dejan de ser un caso aparte porque no hay nada que partir.
        /// </summary>
        private static void DrawLegend(Graphics g, Rectangle bounds, string text, Color color, Font font,
                                       float shiftY, float legendSize)
        {
            if (string.IsNullOrEmpty(text) || font == null) return;

            // El cuerpo de letra lo decide el grupo cuando lo trae decidido (`legendSize`), y si no, se
            // encoge lo justo para caber: la tecla tiene un tamaño fijo y la traducción puede ser más larga
            // que el inglés («ACEPTAR Y CERRAR» son 128 px en un botón de 100). Se crea una fuente nueva
            // solo cuando hace falta, que es el caso raro.
            float size = legendSize > 0f ? legendSize : FittedSize(text, font, bounds.Width - 2f * LegendInset);
            Font used = font;
            bool propia = size < font.Size;
            if (propia) used = new Font(font.FontFamily, size, font.Style);

            try
            {
                float x = bounds.Left + (bounds.Width - MeasureLegend(text, used)) / 2f;
                float y = bounds.Top + (bounds.Height - LegendHeight(g, used)) / 2f + shiftY;

                TextRenderer.DrawText(g, text, used, new Point((int)Math.Round(x), (int)Math.Round(y)),
                                      color, LegendFlags);
            }
            finally
            {
                if (propia) used.Dispose();
            }
        }

        /// <summary>
        /// **El cuerpo de letra con el que la leyenda cabe en la tecla.** Si ya cabe devuelve el tamaño de
        /// la fuente que se le pasa —y entonces no hay que crear ninguna—; si no, baja en pasos de 0,5 pt
        /// hasta <see cref="MinLegendSize"/>.
        ///
        /// Existe porque el tamaño de la tecla es fijo y **el rótulo no**: la misma clave tiene el texto en
        /// inglés y en español, y la traducción puede ser bastante más larga. Medido: «ACEPTAR Y CERRAR»
        /// ocupa 128 px en el botón de aceptar del planificador de vuelo, que mide 100.
        ///
        /// Se mide **sin `Graphics`** (`TextRenderer.MeasureText` con el contexto de pantalla) para que la
        /// regla se pueda probar sin abrir una ventana, como los demás helpers puros del proyecto.
        /// </summary>
        internal static float FittedSize(string text, Font font, float maxWidth)
        {
            if (font == null) return 0f;
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f) return font.Size;

            if (MeasureLegend(text, font) <= maxWidth) return font.Size;

            float size = font.Size;
            while (size > MinLegendSize)
            {
                size = Math.Max(MinLegendSize, size - 0.5f);

                using (Font probe = new Font(font.FontFamily, size, font.Style))
                    if (MeasureLegend(text, probe) <= maxWidth) return size;

                if (size <= MinLegendSize) break;
            }
            return MinLegendSize;
        }

        /// <summary>
        /// Ancho de la leyenda para un cuerpo de letra dado: **la cadena entera de una vez**, con las mismas
        /// banderas con las que se dibuja. Medirla por caracteres y sumar daba un ancho inflado en pantallas
        /// de DPI alto (24 px por letra donde el ancho real era 16), y de ahí salían rótulos encogidos que no
        /// hacía falta encoger.
        /// </summary>
        internal static float MeasureLegend(string text, Font font)
        {
            if (string.IsNullOrEmpty(text) || font == null) return 0f;

            return TextRenderer.MeasureText(text, font, MaxSize, LegendFlags).Width;
        }

        /// <summary>Alto de línea de la leyenda, para centrarla en la tecla.</summary>
        private static float LegendHeight(IDeviceContext dc, Font font)
        {
            return TextRenderer.MeasureText(dc, "Wg", font, MaxSize, LegendFlags).Height;
        }

        internal static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0.1f)
            {
                path.AddRectangle(r);
                return path;
            }

            float d = radius * 2f;
            path.AddArc(r.X, r.Y, d, d, 180f, 90f);
            path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }
    }
}
