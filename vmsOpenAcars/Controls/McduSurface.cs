using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using vmsOpenAcars.UI;

namespace vmsOpenAcars.Controls
{
    /// <summary>
    /// El plástico de la carcasa como control: se pone donde antes había un panel de color plano.
    ///
    /// Pinta **su trozo de la rampa** (con el desplazamiento que le da <see cref="McduChassis.OffsetY"/>),
    /// así que varios de estos paneles en la misma ventana se leen como un solo aparato y no como una
    /// colección de rectángulos. Los rótulos que van encima tienen que llevar
    /// `BackColor = Color.Transparent` y tinta oscura: sobre el plástico el blanco se queda en 2,9:1.
    /// </summary>
    internal class McduChassisPanel : Panel
    {
        internal McduChassisPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            McduChassis.Paint(e.Graphics, ClientRectangle, McduChassis.OffsetY(this), McduChassis.WindowHeight(this));
            base.OnPaint(e);
        }
    }

    /// <summary>
    /// El layout de la ventana como superficie de carcasa. Es lo que hace que **los huecos entre paneles**
    /// —que son la mayor superficie de plástico visible— muestren la rampa y no un color plano: medido en la
    /// ventana, con el layout de color liso la ventana entera se veía plana aunque los paneles pintaran bien.
    /// </summary>
    internal class McduChassisTable : TableLayoutPanel
    {
        internal McduChassisTable()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            McduChassis.Paint(e.Graphics, ClientRectangle, McduChassis.OffsetY(this), McduChassis.WindowHeight(this));
            base.OnPaint(e);
        }
    }

    /// <summary>La tira de estado: el flujo de las píldoras, sobre plástico y no sobre un color plano.</summary>
    internal class McduChassisFlow : FlowLayoutPanel
    {
        internal McduChassisFlow()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            McduChassis.Paint(e.Graphics, ClientRectangle, McduChassis.OffsetY(this), McduChassis.WindowHeight(this));
            base.OnPaint(e);
        }
    }

    /// <summary>
    /// Un grupo de la ventana **hundido como la pantalla de un MCDU**: bisel de plástico, hueco con su
    /// costura, y dentro el LCD con su degradado, su viñeta y sus líneas de barrido.
    ///
    /// El control pinta el título de página (blanco) y la anotación (cian) él mismo, y reserva el `Padding`
    /// para que los hijos caigan **dentro** de la pantalla sin tapar el marco. Es la traducción directa de lo
    /// que hace el aparato real: el contenido vive en el cristal, no en el plástico.
    /// </summary>
    internal class McduDisplayPanel : Panel
    {
        private const int Bisel = 3;      // plástico alrededor del hueco
        private const int Margen = 8;     // aire entre el borde del LCD y el contenido
        private const int AltoTitulo = 22; // la línea del título de página, con su subrayado

        private string _title = string.Empty;
        private string _annotation = string.Empty;

        internal McduDisplayPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            BackColor = McduPalette.LcdBackground;
            RecalcularPadding();
        }

        /// <summary>Título de página, como el `ENG` o el `NAV DATA BASE` del aparato: va en blanco.</summary>
        internal string Title
        {
            get { return _title; }
            set { _title = value ?? string.Empty; Invalidate(); }
        }

        /// <summary>Anotación de la esquina, como el `AIRAC` de la foto: va en cian.</summary>
        internal string Annotation
        {
            get { return _annotation; }
            set { _annotation = value ?? string.Empty; Invalidate(); }
        }

        /// <summary>Rectángulo del cristal, sin el bisel. Es donde se pinta el contenido.</summary>
        internal Rectangle ScreenBounds
        {
            get { return Rectangle.Inflate(ClientRectangle, -Bisel - 1, -Bisel - 1); }
        }

        private void RecalcularPadding()
        {
            Padding = new Padding(Margen, AltoTitulo + Margen / 2, Margen, Margen);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle marco = ClientRectangle;
            if (marco.Width < 12 || marco.Height < 12) return;

            // 1. El bisel: plástico un poco más oscuro que el resto, para que el hueco se lea como hueco.
            using (LinearGradientBrush bisel = new LinearGradientBrush(marco, McduPalette.ChassisMid,
                                                                      McduPalette.ChassisDirt, 90f))
                g.FillRectangle(bisel, marco);
            using (Pen labio = new Pen(Color.FromArgb(120, McduPalette.ChassisLight), 1f))
                g.DrawLine(labio, marco.Left, marco.Top, marco.Right - 1, marco.Top);
            using (Pen sombra = new Pen(Color.FromArgb(150, McduPalette.SeamDark), 1f))
                g.DrawLine(sombra, marco.Left, marco.Bottom - 1, marco.Right - 1, marco.Bottom - 1);

            // 2. El hueco y su costura dura.
            Rectangle hueco = Rectangle.Inflate(marco, -Bisel, -Bisel);
            using (SolidBrush b = new SolidBrush(McduPalette.SeamDark))
                g.FillRectangle(b, hueco);

            // 3. El cristal.
            Rectangle lcd = ScreenBounds;
            using (LinearGradientBrush fondo = new LinearGradientBrush(lcd, McduPalette.LcdBackground,
                                                                      McduPalette.LcdBackground2, 90f))
                g.FillRectangle(fondo, lcd);

            if (McduPalette.Textured)
            {
                VinettaLCD(g, lcd);
                LineasDeBarrido(g, lcd);
            }

            // El área de contenido se deja PLANA. Los rótulos que van dentro llevan fondo sólido —un
            // `Label` con `BackColor` transparente sobre una superficie pintada a mano sale con basura
            // detrás, porque WinForms no sabe qué hay debajo— y sobre un degradado con líneas de barrido
            // se verían sus cajas. La textura queda en la banda del título y en los márgenes del cristal,
            // que es donde el ojo la lee como pantalla.
            using (SolidBrush plano = new SolidBrush(McduPalette.LcdBackground))
                g.FillRectangle(plano, DisplayRectangle);

            // 4. La línea del título, como el renglón de página del MCDU.
            if (!string.IsNullOrEmpty(_title))
            {
                using (Font f = new Font("Consolas", 9.5f, FontStyle.Bold))
                    TextRenderer.DrawText(g, _title, f, new Point(lcd.X + 8, lcd.Y + 5), McduPalette.LcdWhite,
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }

            if (!string.IsNullOrEmpty(_annotation))
            {
                using (Font f = new Font("Consolas", 8.5f, FontStyle.Regular))
                {
                    Size tam = TextRenderer.MeasureText(_annotation, f);
                    TextRenderer.DrawText(g, _annotation, f,
                        new Point(lcd.Right - 8 - tam.Width, lcd.Y + 6), McduPalette.LcdCyan,
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                }
            }

            if (!string.IsNullOrEmpty(_title) || !string.IsNullOrEmpty(_annotation))
                using (Pen linea = new Pen(Color.FromArgb(70, McduPalette.LcdCyan)))
                    g.DrawLine(linea, lcd.X + 8, lcd.Y + 21, lcd.Right - 8, lcd.Y + 21);

            base.OnPaint(e);
        }

        /// <summary>
        /// La viñeta: en la foto las esquinas del cristal están más apagadas que el centro. Es sutil a
        /// propósito —el log se lee durante minutos— y se apaga con el interruptor de la textura.
        /// </summary>
        private static void VinettaLCD(Graphics g, Rectangle lcd)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddRectangle(lcd);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    pg.SurroundColors = new Color[] { Color.FromArgb(72, 0, 0, 0) };
                    g.FillRectangle(pg, lcd);
                }
            }
        }

        private static void LineasDeBarrido(Graphics g, Rectangle lcd)
        {
            using (Pen linea = new Pen(Color.FromArgb(17, 0, 0, 0), 1f))
                for (int y = lcd.Top; y < lcd.Bottom; y += 3)
                    g.DrawLine(linea, lcd.Left, y, lcd.Right, y);
        }
    }
}
