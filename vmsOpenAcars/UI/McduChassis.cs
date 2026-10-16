using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace vmsOpenAcars.UI
{
    /// <summary>
    /// Pinta la **carcasa**: el plástico azuloso gastado del MCDU. Es puro sobre `Graphics` —no depende de
    /// ningún control— para poder probarlo y para poder pintarlo desde la ventana, desde un panel o desde
    /// la lámina del arnés con el mismo resultado.
    ///
    /// Tres decisiones que vienen de mirar la foto de referencia:
    ///
    /// 1. **La rampa va de #859AB2 a #5A6A7E, no de #93A6BD.** El tono más claro de la foto es el reflejo
    ///    del canto de arriba, no el color del panel: un panel entero a #93A6BD se ve nuevo y limpio, que
    ///    es justo lo contrario de lo que se busca. El reflejo se pinta como una línea de 1 px.
    /// 2. **La porquería no está repartida por igual**: se acumula en los cantos y alrededor del teclado,
    ///    que es donde se apoya la mano. Por eso el grano se agrupa y las manchas fuertes caen abajo.
    /// 3. **Se pinta con un desplazamiento vertical** (`offsetY`): cada panel que enseña plástico pinta su
    ///    trozo de la misma rampa, así la ventana parece un solo aparato y no una colección de rectángulos
    ///    con su propio degradado. Por eso el degradado se define sobre un rectángulo *virtual* del alto de
    ///    la ventana y se recorta al del panel.
    /// </summary>
    internal static class McduChassis
    {
        /// <summary>Semilla fija: el grano es el mismo en cada repintado (si no, parpadearía).</summary>
        private const int Semilla = 20251015;

        /// <summary>
        /// Las paradas de la rampa, tal como salen de la foto: claro arriba (la luz viene de arriba) y
        /// sucio abajo. Están en `McduPalette` para que el color y su orden vivan en un solo sitio.
        /// </summary>
        private static readonly float[] Paradas = { 0f, 0.26f, 0.58f, 0.84f, 1f };

        /// <summary>
        /// **El color del plástico a una altura de la ventana.** Es una función pura —y por eso se prueba—
        /// y es lo que hace que la ventana se lea como un solo aparato: dos paneles contiguos piden el color
        /// en la misma Y y coinciden, así que no se ven escalones entre ellos.
        /// </summary>
        internal static Color RampAt(int y, int windowHeight)
        {
            if (windowHeight < 1) windowHeight = 1;

            float t = y / (float)windowHeight;
            if (t <= 0f) return McduPalette.ChassisHigh;
            if (t >= 1f) return McduPalette.ChassisDirt;

            for (int i = 1; i < Paradas.Length; i++)
            {
                if (t > Paradas[i] && i < Paradas.Length - 1) continue;

                float tramo = Paradas[i] - Paradas[i - 1];
                float f = tramo <= 0f ? 0f : (t - Paradas[i - 1]) / tramo;
                return Mezcla(ColorDeParada(i - 1), ColorDeParada(i), f);
            }
            return McduPalette.ChassisDirt;
        }

        private static Color ColorDeParada(int i)
        {
            switch (i)
            {
                case 0: return McduPalette.ChassisHigh;
                case 1: return McduPalette.ChassisMid;
                case 2: return Color.FromArgb(0x6F, 0x82, 0x99);
                case 3: return Color.FromArgb(0x63, 0x74, 0x8A);
                default: return McduPalette.ChassisDirt;
            }
        }

        private static Color Mezcla(Color a, Color b, float f)
        {
            if (f <= 0f) return a;
            if (f >= 1f) return b;
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * f),
                (int)Math.Round(a.G + (b.G - a.G) * f),
                (int)Math.Round(a.B + (b.B - a.B) * f));
        }

        /// <summary>
        /// Pinta el plástico en <paramref name="r"/>. <paramref name="offsetY"/> es dónde cae ese
        /// rectángulo dentro de la ventana y <paramref name="windowHeight"/> el alto de referencia de la
        /// rampa (el de la ventana), para que el degradado sea continuo entre paneles.
        /// </summary>
        internal static void Paint(Graphics g, Rectangle r, int offsetY, int windowHeight)
        {
            if (g == null || r.Width <= 0 || r.Height <= 0) return;
            if (windowHeight < 1) windowHeight = 1;

            // El degradado se define entre los DOS extremos que le tocan al panel dentro de la rampa de la
            // ventana, no con un rectángulo virtual: aquel truco no daba el color que tocaba (medido en la
            // captura de la ventana: la barra, al 96 % de la altura, salía con el color del 26 %), y así el
            // reparto es exacto y se puede probar como función pura.
            using (LinearGradientBrush b = new LinearGradientBrush(r, RampAt(offsetY, windowHeight),
                                                                   RampAt(offsetY + r.Height, windowHeight), 90f))
                g.FillRectangle(b, r);

            if (!McduPalette.Textured)
            {
                // Sin textura queda el plástico limpio con su reflejo y su sombra: se descansa la vista sin
                // perder ni los colores ni los biseles.
                Borde(g, r);
                return;
            }

            // Semilla distinta por altura: si no, todos los paneles repetirían el mismo dibujo de manchas.
            Random rnd = new Random(Semilla + offsetY * 7);

            ManchasSuaves(g, r, rnd);
            ManchasFuertes(g, r, rnd);
            Grano(g, r, rnd);
            Rayones(g, r, rnd);
            Vinetas(g, r);
            Borde(g, r);
        }

        private static void Borde(Graphics g, Rectangle r)
        {
            using (Pen luz = new Pen(Color.FromArgb(120, 0xA8, 0xB8, 0xCC), 1f))
                g.DrawLine(luz, r.Left, r.Top, r.Right, r.Top);
            using (Pen bajo = new Pen(Color.FromArgb(160, McduPalette.SeamDark), 2f))
                g.DrawLine(bajo, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
        }

        private static void ManchasSuaves(Graphics g, Rectangle r, Random rnd)
        {
            for (int i = 0; i < 12; i++)
            {
                int w = 60 + rnd.Next(190);
                int h = 26 + rnd.Next(80);
                int px = r.Left + rnd.Next(r.Width) - w / 2;
                int py = rnd.Next(3) == 0 ? r.Top + rnd.Next(40) : r.Bottom - h - rnd.Next(90);
                Mancha(g, new Rectangle(px, py, w, h), 16 + rnd.Next(14));
            }
        }

        private static void ManchasFuertes(Graphics g, Rectangle r, Random rnd)
        {
            for (int i = 0; i < 14; i++)
            {
                int w = 14 + rnd.Next(38);
                int h = 8 + rnd.Next(18);
                int px = rnd.Next(2) == 0 ? r.Left + 6 + rnd.Next(40) : r.Right - 46 - rnd.Next(40);
                if (rnd.Next(3) == 0) px = r.Left + rnd.Next(Math.Max(1, r.Width / 2));
                int py = rnd.Next(2) == 0 ? r.Top + 8 + rnd.Next(60) : r.Bottom - 30 - rnd.Next(150);
                Mancha(g, new Rectangle(px, py, w, h), 46 + rnd.Next(26));
            }
        }

        private static void Mancha(Graphics g, Rectangle caja, int alpha)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(caja);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterColor = Color.FromArgb(Math.Min(255, alpha), McduPalette.ChassisWorn);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, McduPalette.ChassisWorn) };
                    g.FillPath(pg, p);
                }
            }
        }

        private static void Grano(Graphics g, Rectangle r, Random rnd)
        {
            for (int i = 0; i < 2600; i++)
            {
                int px, py;
                int donde = rnd.Next(4);
                if (donde == 0) { px = r.Left + rnd.Next(r.Width); py = r.Top + rnd.Next(Math.Min(26, r.Height)); }
                else if (donde == 1) { px = r.Left + rnd.Next(r.Width); py = r.Bottom - rnd.Next(Math.Min(80, r.Height)); }
                else if (donde == 2) { px = r.Left + rnd.Next(Math.Min(16, r.Width)); py = r.Top + rnd.Next(r.Height); }
                else { px = r.Right - rnd.Next(Math.Min(16, r.Width)); py = r.Top + rnd.Next(r.Height); }

                bool claro = rnd.Next(3) == 0;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(claro ? 20 : 16,
                           claro ? McduPalette.ChassisLight : McduPalette.SeamDark)))
                    g.FillRectangle(b, px, py, 1, 1);
            }
        }

        private static void Rayones(Graphics g, Rectangle r, Random rnd)
        {
            for (int i = 0; i < 30; i++)
            {
                int sy = r.Top + rnd.Next(r.Height);
                int sx = r.Left + rnd.Next(Math.Max(1, r.Width / 2));
                int largo = 40 + rnd.Next(240);
                using (Pen roce = new Pen(Color.FromArgb(14 + rnd.Next(16),
                           rnd.Next(2) == 0 ? McduPalette.ChassisLight : McduPalette.SeamDark), 1f))
                    g.DrawLine(roce, sx, sy, sx + largo, sy + rnd.Next(3) - 1);
            }
        }

        private static void Vinetas(Graphics g, Rectangle r)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddRectangle(r);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterColor = Color.FromArgb(0, McduPalette.ChassisWorn);
                    pg.SurroundColors = new Color[] { Color.FromArgb(34, McduPalette.ChassisWorn) };
                    g.FillRectangle(pg, r);
                }
            }
        }

        /// <summary>
        /// El tornillo: una tapa redonda con su ranura. Es lo que más vende el parecido por lo que cuesta,
        /// así que va en las cuatro esquinas de la ventana.
        /// </summary>
        internal static void Screw(Graphics g, float cx, float cy, float radio)
        {
            RectangleF caja = new RectangleF(cx - radio, cy - radio, radio * 2f, radio * 2f);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(caja);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterPoint = new PointF(cx - radio * 0.35f, cy - radio * 0.35f);
                    pg.CenterColor = McduPalette.ScrewLight;
                    pg.SurroundColors = new Color[] { McduPalette.ScrewDark };
                    g.FillPath(pg, p);
                }
                using (Pen borde = new Pen(Color.FromArgb(150, McduPalette.SeamDark), 1f))
                    g.DrawEllipse(borde, caja);
            }
            using (Pen ranura = new Pen(Color.FromArgb(170, McduPalette.ChassisWorn), Math.Max(1f, radio * 0.30f)))
                g.DrawLine(ranura, cx - radio * 0.52f, cy + radio * 0.34f, cx + radio * 0.52f, cy - radio * 0.34f);
        }

        /// <summary>
        /// La rayita blanca del bisel. En el aparato real cada tecla de línea tiene la suya apuntando a su
        /// renglón, y aquí es lo que convierte la barra de botones en la columna de LSK del MCDU.
        /// </summary>
        internal static void Dash(Graphics g, int x, int y, int ancho)
        {
            using (Pen raya = new Pen(Color.FromArgb(190, McduPalette.LcdWhite), 1f))
                g.DrawLine(raya, x, y, x + ancho, y);
        }

        // ── Dónde cae un control dentro de la ventana ─────────────────────────────────────────────────

        /// <summary>
        /// La Y del control dentro de su formulario. Es lo que hace que el degradado sea continuo entre
        /// paneles: sin esto, cada panel pintaría la rampa entera y se verían los escalones.
        /// </summary>
        /// <summary>
        /// La Y del control dentro de su formulario. Es lo que hace que la rampa sea continua entre paneles:
        /// sin esto, cada panel pintaría la suya y se verían los escalones.
        ///
        /// Se suma el `Top` de la cadena de padres y **no** se usa `PointToScreen`, que era lo natural y
        /// devolvía valores imposibles (medido: −220 para un panel colocado en Y=40) y dejaba la ventana con
        /// el plástico **plano**, porque todos los paneles acababan pintando el mismo trozo de la rampa. La
        /// suma ignora el desplazamiento de un contenedor con scroll, y ninguno de los que enseña plástico
        /// lo tiene.
        /// </summary>
        internal static int OffsetY(Control control)
        {
            int y = 0;
            for (Control c = control; c != null && !(c is Form); c = c.Parent)
                y += c.Top;

            return y;
        }
        /// <summary>Alto de referencia de la rampa: el de la ventana que contiene al control.</summary>
        internal static int WindowHeight(Control control)
        {
            if (control == null) return 0;

            Form form = control.FindForm();
            return form == null || form.ClientSize.Height < 1 ? control.Height : form.ClientSize.Height;
        }
    }
}
