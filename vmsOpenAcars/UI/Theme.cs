using System.Drawing;

namespace vmsOpenAcars.UI
{
    public static class Theme
    {
        // Fondo principal del ACARS / cockpit
        public static readonly Color CockpitBackground = Color.FromArgb(18, 18, 18); // casi negro

        // Texto principal
        public static readonly Color MainText = Color.FromArgb(0, 255, 255); // cyan

        // Texto de información secundaria
        public static readonly Color SecondaryText = Color.FromArgb(128, 255, 255); // cyan más tenue

        // Indicadores de fase de vuelo
        public static readonly Color Taxi = Color.FromArgb(0, 200, 0); // verde
        public static readonly Color Takeoff = Color.FromArgb(255, 255, 0); // amarillo
        public static readonly Color Enroute = Color.FromArgb(0, 200, 0); // verde
        public static readonly Color Approach = Color.FromArgb(255, 128, 0); // ámbar
        public static readonly Color Landing = Color.FromArgb(255, 0, 0); // rojo
        public static readonly Color Arrived = Color.FromArgb(128, 128, 128); // gris apagado

        // Alertas y warnings
        public static readonly Color Warning = Color.FromArgb(255, 165, 0); // ámbar
        public static readonly Color Caution = Color.FromArgb(255, 69, 0); // naranja rojizo
        public static readonly Color Danger = Color.FromArgb(255, 0, 0); // rojo brillante

        // Paneles / frames
        public static readonly Color PanelBackground = Color.FromArgb(28, 28, 28); // gris muy oscuro
        public static readonly Color PanelBorder = Color.FromArgb(50, 50, 50); // gris borde

        // Nuevos paneles específicos estilo cockpit
        public static readonly Color FMAPanelBackground = Color.FromArgb(20, 20, 25);   // panel superior FMA
        public static readonly Color NDBackground = Color.FromArgb(10, 10, 20);         // panel central ND / Map
        public static readonly Color ACARSBackground = Color.FromArgb(25, 25, 30);      // panel inferior ACARS

        // Botones
        public static readonly Color ButtonBackground = Color.FromArgb(40, 40, 40);
        public static readonly Color ButtonHover = Color.FromArgb(60, 60, 60);
        public static readonly Color ButtonText = MainText;

        // Líneas de separación
        public static readonly Color Separator = Color.FromArgb(70, 70, 70);

        // Fuentes
        public static readonly Font MainFont = new Font("Consolas", 10, FontStyle.Bold);
        public static readonly Font SmallFont = new Font("Consolas", 8, FontStyle.Regular);
        public static readonly Font LargeFont = new Font("Consolas", 14, FontStyle.Bold);

        // Gráficos (LiveCharts) colores de series
        public static readonly Color GraphPrimary = MainText;
        public static readonly Color GraphSecondary = SecondaryText;
        public static readonly Color GraphWarning = Warning;
        public static readonly Color GraphDanger = Danger;

        // Mapas (GMap.NET)
        public static readonly Color MapBackground = Color.FromArgb(15, 15, 15);
        public static readonly Color MapLine = MainText;
        public static readonly Color MapAirport = Color.FromArgb(255, 128, 0);
    }
}