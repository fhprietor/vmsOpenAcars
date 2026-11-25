using System;
using System.Drawing;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La piel de la tarjeta del OSD**, separada del tema de consola de la aplicación.
    ///
    /// El OSD es una superficie de vuelo: se lee de un vistazo sobre el simulador y no comparte
    /// la estética cian-sobre-negro de la ventana principal. La referencia es la tarjeta de otro
    /// ACARS que pasó el mantenedor: fondo casi negro con tinte morado, texto blanco, secundarios
    /// grises, acento morado y un verde de píldora. No se distribuye ninguna fuente: **Segoe UI**
    /// es la sans geométrica que ya viene con Windows y la más cercana a la de la referencia.
    ///
    /// Los tamaños están en **puntos**, no en píxeles, para que el renderer los convierta con el
    /// DPI real del monitor. Los colores por severidad conservan la semántica de antes —información,
    /// éxito, aviso, crítico— pero con la paleta de la tarjeta: ni lima ni rojo puro.
    /// </summary>
    internal static class OsdStyle
    {
        // ── Tipografía (puntos) ──────────────────────────────────────────────────

        /// <summary>La familia que sustituye a Consolas en todo el OSD. Va con Windows.</summary>
        internal const string FontFamily = "Segoe UI";

        /// <summary>Rótulo de la tarjeta (`LANDING RATE`): pequeño, gris y en versalitas.</summary>
        internal const float CaptionPoints = 7.5f;

        /// <summary>El número enorme (`−145`, `1.18`): el dato que el piloto mira primero.</summary>
        internal const float ValuePoints = 28f;

        /// <summary>Unidad pequeña al lado del número (`fpm`, `G`).</summary>
        internal const float UnitPoints = 10f;

        /// <summary>Etiqueta de estado de la cabecera (`LANDED`).</summary>
        internal const float StatusPoints = 8.5f;

        /// <summary>ICAO de la cabecera (`EHAM`).</summary>
        internal const float IcaoPoints = 14f;

        /// <summary>Nombre del aeropuerto y estado vivo de la cabecera.</summary>
        internal const float MetaPoints = 8.5f;

        /// <summary>Las tres líneas de la columna derecha.</summary>
        internal const float DetailPoints = 9f;

        /// <summary>Texto de la píldora (`Normal`) y su nota (`by …`).</summary>
        internal const float BadgePoints = 8f;

        /// <summary>Etiqueta de la plantilla simple (severidad o primera parte del mensaje).</summary>
        internal const float SimpleLabelPoints = 8.5f;

        /// <summary>Mensaje grande de la plantilla simple.</summary>
        internal const float SimpleMessagePoints = 16f;

        /// <summary>Espaciado entre letras de los rótulos en versalitas, a 96 DPI.</summary>
        internal const float CaptionTrackingPx = 1.2f;

        /// <summary>Espaciado de la etiqueta de estado de la cabecera.</summary>
        internal const float StatusTrackingPx = 1.5f;

        // ── Paleta ───────────────────────────────────────────────────────────────

        /// <summary>Fondo de tarjeta: casi negro con el tinte morado de la referencia.</summary>
        internal static readonly Color CardBackground = Color.FromArgb(13, 10, 19);

        /// <summary>Fondo durante el parpadeo de `Critical`: rojo muy oscuro, no rojo puro.</summary>
        internal static readonly Color CardBackgroundFlash = Color.FromArgb(46, 14, 22);

        /// <summary>Borde sutil de la tarjeta.</summary>
        internal static readonly Color CardBorder = Color.FromArgb(46, 38, 60);

        /// <summary>Texto primario: blanco roto, nunca #FFFFFF puro sobre casi negro.</summary>
        internal static readonly Color PrimaryText = Color.FromArgb(247, 244, 251);

        /// <summary>Texto secundario: gris con el mismo tinte que el fondo.</summary>
        internal static readonly Color SecondaryText = Color.FromArgb(148, 141, 162);

        /// <summary>Acento morado de la etiqueta de estado.</summary>
        internal static readonly Color Accent = Color.FromArgb(184, 156, 255);

        /// <summary>Verde de la píldora `Perfect` (banda suave del G).</summary>
        internal static readonly Color BadgeGreen = Color.FromArgb(110, 228, 164);

        /// <summary>Verde suave de la píldora `Normal` (banda firme, sin llegar al ámbar).</summary>
        internal static readonly Color BadgeGreenSoft = Color.FromArgb(158, 214, 180);

        /// <summary>Borde de la píldora verde.</summary>
        internal static readonly Color BadgeBorder = Color.FromArgb(52, 126, 94);

        /// <summary>Relleno tenue de la píldora.</summary>
        internal static readonly Color BadgeBackground = Color.FromArgb(18, 36, 29);

        // ── Semántica de severidad ───────────────────────────────────────────────

        /// <summary>
        /// El color de cada severidad, conservando lo que significaba en la tabla anterior:
        /// `Info` informa, `Success` confirma, `Warning` avisa y `Critical` exige atención.
        /// La estética es la de la tarjeta: azul lavanda, menta, ámbar y coral.
        /// </summary>
        internal static Color ColorFor(OsdSeverity severity)
        {
            switch (severity)
            {
                case OsdSeverity.Success:  return Color.FromArgb(110, 228, 164); // menta, no lima
                case OsdSeverity.Warning:  return Color.FromArgb(235, 183,  96); // ámbar, no oro puro
                case OsdSeverity.Critical: return Color.FromArgb(228, 110, 124); // coral, no rojo puro
                default:                   return Color.FromArgb(150, 172, 255); // lavanda informativa
            }
        }

        /// <summary>
        /// El color de la píldora de la tarjeta, por **banda del G medido**: la misma tabla que
        /// decide la etiqueta. No mira la severidad del fpm; si no hay G, el renderer no pinta
        /// píldora y este método no se llama.
        /// </summary>
        internal static Color BadgeColor(GForceBand band)
        {
            switch (band)
            {
                case GForceBand.Smooth: return BadgeGreen;
                case GForceBand.Firm:   return BadgeGreenSoft;
                case GForceBand.Hard:   return ColorFor(OsdSeverity.Warning);
                default:                return ColorFor(OsdSeverity.Critical);
            }
        }

        /// <summary>
        /// Crea una fuente del OSD en **píxeles**, escalada por el DPI real. Se usa píxeles y no
        /// puntos en el renderer para que el tamaño no dependa del `Graphics.DpiY` del lienzo:
        /// el mismo `Bitmap` sale idéntico en el formulario y en el banco de pintado.
        /// </summary>
        internal static Font CreateFont(float points, FontStyle style, float scale)
        {
            float pixels = points * (96f / 72f) * Math.Max(0.1f, scale);
            return new Font(FontFamily, pixels, style, GraphicsUnit.Pixel);
        }
    }
}
