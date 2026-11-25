using System;
using System.Drawing;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Dónde y de qué tamaño se pinta la tarjeta del toque**, en píxeles reales del monitor.
    ///
    /// El tamaño base es 720×200 a 96 DPI. A 4K con escalado de Windows al 200 % la tarjeta mide
    /// 1440×400 y sigue cabiendo en 3840×2160; el margen superior de 40 px también crece con el
    /// DPI para que la tarjeta no se pegue al borde en pantallas grandes. Si por cualquier motivo
    /// la tarjeta no cupiera, el anclaje la pega al borde en vez de dejar media tarjeta fuera.
    /// </summary>
    internal static class OsdCardPlacement
    {
        internal const int LogicalWidth  = 720;
        internal const int LogicalHeight = 200;
        internal const int TopMargin     = 40;

        /// <summary>Tamaño de la tarjeta para el DPI real (`96`, `120`, `144`, `192`…).</summary>
        internal static Size CardSize(int dpi)
        {
            double scale = Scale(dpi);
            return new Size((int)Math.Round(LogicalWidth  * scale),
                            (int)Math.Round(LogicalHeight * scale));
        }

        /// <summary>Posición centrada arriba y siempre dentro de la pantalla indicada.</summary>
        internal static Point CenterTop(Size card, Size screen, int dpi)
        {
            int margin = (int)Math.Round(TopMargin * Scale(dpi));
            int x = (screen.Width - card.Width) / 2;
            int y = margin;

            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x + card.Width > screen.Width)  x = Math.Max(0, screen.Width  - card.Width);
            if (y + card.Height > screen.Height) y = Math.Max(0, screen.Height - card.Height);
            return new Point(x, y);
        }

        private static double Scale(int dpi) => dpi <= 0 ? 1.0 : dpi / 96.0;
    }
}
