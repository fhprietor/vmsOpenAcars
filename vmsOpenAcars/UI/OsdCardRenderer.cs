using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;
using static vmsOpenAcars.Helpers.OsdCardText;

namespace vmsOpenAcars.UI
{
    /// <summary>
    /// **El pintado de las dos plantillas del OSD**, sin abrir ninguna ventana.
    ///
    /// Vive separado del formulario para que el banco de pintado pueda producir PNG y los tests
    /// puedan comprobar el resultado sobre un `Bitmap`: un test que instanciara el `Form` crearía
    /// una ventana real y no es lo que se quiere medir. El formulario solo aporta el lienzo y el
    /// DPI; aquí están las coordenadas, las fuentes y los colores.
    ///
    /// El **compromiso de las esquinas**: el formulario recorta con una `Region` construida con un
    /// `GraphicsPath` redondeado, porque es la única forma de que sigan funcionando la `Opacity`
    /// animada y el `HTTRANSPARENT`. La `Region` no tiene antialias, así que el borde de la esquina
    /// queda ligeramente escalonado; el relleno sí se pinta con GDI+ suavizado. La alternativa
    /// —`UpdateLayeredWindow` con alfa por píxel— daría esquinas perfectas pero rompería la
    /// animación de opacidad tal como está montada y obligaría a rehacer el click-through.
    /// </summary>
    internal static class OsdCardRenderer
    {
        private const float CardLogicalWidth  = OsdCardPlacement.LogicalWidth;
        private const float CardLogicalHeight = OsdCardPlacement.LogicalHeight;
        private const float SimpleLogicalMinWidth = 420f;
        private const float SimpleLogicalHeight   = 90f;

        private const float Pad            = 28f;
        private const float CornerRadius   = 14f;
        private const float HeaderBaseline = 38f;
        private const float DividerY       = 60f;
        private const float CaptionBaseline = 78f;
        private const float ValueBaseline   = 128f;
        private const float RightLineBaseline = 80f;
        private const float RightLineStep     = 21f;
        private const float BadgeTop        = 158f;
        private const float BadgeHeight     = 22f;

        // ── Formulario ───────────────────────────────────────────────────────────

        internal static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
        {
            float d = Math.Max(0.1f, radius * 2f);
            var path = new GraphicsPath();
            path.AddArc(bounds.Left,          bounds.Top,           d, d, 180, 90);
            path.AddArc(bounds.Right - d,     bounds.Top,           d, d, 270, 90);
            path.AddArc(bounds.Right - d,     bounds.Bottom - d,    d, d,   0, 90);
            path.AddArc(bounds.Left,          bounds.Bottom - d,    d, d,  90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void PaintCard(Graphics g, Size size, OsdCardPayload payload, bool flashOn)
        {
            if (size.Width <= 0 || size.Height <= 0) return;

            Prepare(g);
            float s = size.Width / CardLogicalWidth;
            using (var path = CreateRoundedPath(
                       new RectangleF(0.5f * s, 0.5f * s, size.Width - s, size.Height - s),
                       CornerRadius * s))
            {
                var clip = g.Clip;
                g.SetClip(path, CombineMode.Replace);
                try
                {
                    using (var bg = new SolidBrush(flashOn
                               ? OsdStyle.CardBackgroundFlash : OsdStyle.CardBackground))
                        g.FillPath(bg, path);

                    using (var pen = new Pen(OsdStyle.CardBorder, Math.Max(1f, 1.2f * s)))
                        g.DrawPath(pen, path);

                    PaintCardContent(g, s, payload);
                }
                finally
                {
                    g.Clip = clip;
                }
            }
        }

        internal static void PaintSimple(Graphics g, Size size, string text,
                                         OsdSeverity severity, bool flashOn)
        {
            if (size.Width <= 0 || size.Height <= 0) return;

            Prepare(g);
            // La escala sale del alto, que es exactamente 90 × dpi/96: derivarla del ancho medido
            // con otra escala redondeaba de más y agrandaba el texto en pantallas HiDPI.
            float s = size.Height / SimpleLogicalHeight;
            using (var path = CreateRoundedPath(
                       new RectangleF(0.5f * s, 0.5f * s, size.Width - s, size.Height - s),
                       CornerRadius * s))
            {
                var clip = g.Clip;
                g.SetClip(path, CombineMode.Replace);
                try
                {
                    using (var bg = new SolidBrush(flashOn
                               ? OsdStyle.CardBackgroundFlash : OsdStyle.CardBackground))
                        g.FillPath(bg, path);

                    using (var pen = new Pen(OsdStyle.CardBorder, Math.Max(1f, 1.2f * s)))
                        g.DrawPath(pen, path);

                    string label, detail;
                    SplitSimple(text, severity, out label, out detail);

                    using (var labelFont  = OsdStyle.CreateFont(OsdStyle.SimpleLabelPoints, FontStyle.Bold, s))
                    using (var detailFont = OsdStyle.CreateFont(OsdStyle.SimpleMessagePoints, FontStyle.Bold, s))
                    using (var labelBrush = new SolidBrush(OsdStyle.ColorFor(severity)))
                    using (var detailBrush = new SolidBrush(OsdStyle.PrimaryText))
                    {
                        DrawTracked(g, label.ToUpperInvariant(), labelFont, labelBrush,
                                    Pad * s, 26f * s, OsdStyle.CaptionTrackingPx * s,
                                    Typographic());

                        var sf = Typographic();
                        g.DrawString(detail, detailFont, detailBrush,
                                     new RectangleF(Pad * s, 40f * s,
                                                    size.Width - 2f * Pad * s, 40f * s), sf);
                    }
                }
                finally
                {
                    g.Clip = clip;
                }
            }
        }

        // ── Medida y banco de pintado ────────────────────────────────────────────

        /// <summary>Tamaño físico de la plantilla simple para su texto y el DPI real.</summary>
        internal static Size MeasureSimple(string text, OsdSeverity severity, int dpi)
        {
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                // La medida se hace en unidades lógicas (escala 1) y se escala UNA vez con el DPI.
                float logicalWidth = MeasureSimpleLogicalWidth(g, text, severity);
                double scale = dpi <= 0 ? 1.0 : dpi / 96.0;
                return new Size((int)Math.Round(logicalWidth * scale),
                                (int)Math.Round(SimpleLogicalHeight * scale));
            }
        }

        internal static string[] GeneratePreviewImages()
        {
            string original = LocalizationService.Instance.CurrentLanguage;
            try
            {
                // El banco de pintado enseña la tarjeta con los textos de la referencia (inglés),
                // para poder compararla con la imagen que pasó el mantenedor. El idioma real de la
                // aplicación no se toca: se restaura al terminar.
                LocalizationService.Instance.LoadLanguage("en");

                string folder = Path.Combine(Path.GetTempPath(), "osd-preview");
                Directory.CreateDirectory(folder);

                var card = new OsdCardPayload
                {
                    // Los datos reales del toque del PIREP YxzZxO92Qnbxlqla (B738 SSW, MPTO→SKCG).
                    Severity       = OsdSeverity.Success,
                    Icao           = "SKCG",
                    AirportName    = "Rafael Núñez",
                    TouchdownUtc   = new DateTime(2026, 11, 24, 3, 39, 0, DateTimeKind.Utc),
                    LandingRateFpm = -219,
                    GForce         = 1.21,
                    IasKt          = 138,
                    GroundSpeedKt  = 141,
                    PitchDeg       = 3.5,
                    BankDeg        = 0.0,
                };

                string cardPath   = Path.Combine(folder, "osd-card-touchdown.png");
                string simplePath = Path.Combine(folder, "osd-simple-penalty.png");

                SaveBitmap(cardPath,   RenderCard(card, 96));
                SaveBitmap(simplePath, RenderSimple("PENALTY  NAV LIGHTS  −5 PTS",
                                                    OsdSeverity.Warning, 96));

                return new[] { cardPath, simplePath };
            }
            finally
            {
                LocalizationService.Instance.LoadLanguage(original);
            }
        }

        /// <summary>Pinta la tarjeta en un `Bitmap` con alfa; el test comprueba las esquinas fuera.</summary>
        internal static Bitmap RenderCard(OsdCardPayload payload, int dpi)
        {
            Size size = OsdCardPlacement.CardSize(dpi);
            var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            bmp.SetResolution(dpi, dpi);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                PaintCard(g, size, payload, false);
            }
            return bmp;
        }

        internal static Bitmap RenderSimple(string text, OsdSeverity severity, int dpi)
        {
            Size size = MeasureSimple(text, severity, dpi);
            var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            bmp.SetResolution(dpi, dpi);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                PaintSimple(g, size, text, severity, false);
            }
            return bmp;
        }

        // ── Contenido de la tarjeta ──────────────────────────────────────────────

        private static void PaintCardContent(Graphics g, float s, OsdCardPayload p)
        {
            float pad   = Pad * s;
            float right = CardLogicalWidth * s - pad;

            // Cabecera: LANDED (morado, tracking), ICAO (blanco), nombre · hora (gris) y estado vivo.
            using (var statusFont = OsdStyle.CreateFont(OsdStyle.StatusPoints, FontStyle.Bold, s))
            using (var icaoFont   = OsdStyle.CreateFont(OsdStyle.IcaoPoints, FontStyle.Bold, s))
            using (var metaFont   = OsdStyle.CreateFont(OsdStyle.MetaPoints, FontStyle.Regular, s))
            using (var accent     = new SolidBrush(OsdStyle.Accent))
            using (var primary    = new SolidBrush(OsdStyle.PrimaryText))
            using (var secondary  = new SolidBrush(OsdStyle.SecondaryText))
            {
                float statusW = DrawTracked(g, StatusLabel.ToUpperInvariant(), statusFont, accent,
                                            pad, HeaderBaseline * s,
                                            OsdStyle.StatusTrackingPx * s, Typographic());

                float x = pad + statusW + 16f * s;
                string icao = (p?.Icao ?? string.Empty).Trim();
                if (icao.Length > 0)
                {
                    float icaoW = DrawAtBaseline(g, icao, icaoFont, primary, x, HeaderBaseline * s);
                    x += icaoW + 12f * s;
                }

                OsdTextLine live = LiveState(p?.GroundSpeedKt);
                float liveW = MeasureRuns(g, live,
                                          OsdStyle.MetaPoints, FontStyle.Regular, s);
                float liveX = right - liveW;
                DrawRuns(g, live, OsdStyle.MetaPoints, s, liveX, HeaderBaseline * s);

                string airportLine = AirportLine(p?.AirportName, p?.TouchdownUtc);
                if (!string.IsNullOrEmpty(airportLine))
                {
                    float maxW = Math.Max(0f, (live.IsEmpty ? right : liveX - 12f * s) - x);
                    if (maxW > 4f)
                    {
                        var sf = Typographic();
                        sf.Trimming = StringTrimming.EllipsisCharacter;
                        sf.FormatFlags |= StringFormatFlags.NoWrap;
                        g.DrawString(airportLine, metaFont, secondary,
                                     new RectangleF(x, HeaderBaseline * s - Ascent(metaFont),
                                                    maxW, 22f * s), sf);
                    }
                }
            }

            using (var divider = new Pen(OsdStyle.CardBorder, Math.Max(1f, s)))
                g.DrawLine(divider, pad, DividerY * s, right, DividerY * s);

            // Tres columnas de datos: rótulo pequeño con tracking y número enorme con su unidad.
            PaintDataColumn(g, s, pad,   LandingRateCaption, LandingRateValue(p?.LandingRateFpm),
                            _("Osd_Card_UnitFpm"));
            PaintDataColumn(g, s, 250f * s, TouchdownGCaption, GForceValue(p?.GForce),
                            _("Osd_Card_UnitG"));

            // Columna derecha: las líneas que quepan, sin huecos cuando falta un dato.
            var lines = new[]
            {
                SpeedLine(p?.IasKt, p?.GroundSpeedKt),
                AttitudeLine(p?.PitchDeg, p?.BankDeg),
                TouchdownLine(p?.TouchdownUtc),
            };
            float lineY = RightLineBaseline * s;
            foreach (var line in lines)
            {
                if (line == null || line.IsEmpty) continue;
                DrawRuns(g, line, OsdStyle.DetailPoints, s, 470f * s, lineY);
                lineY += RightLineStep * s;
            }

            PaintBadge(g, s, p, pad);
        }

        private static void PaintDataColumn(Graphics g, float s, float x, string caption,
                                            string value, string unit)
        {
            if (string.IsNullOrEmpty(value)) return;

            using (var captionFont = OsdStyle.CreateFont(OsdStyle.CaptionPoints, FontStyle.Bold, s))
            using (var valueFont   = OsdStyle.CreateFont(OsdStyle.ValuePoints, FontStyle.Bold, s))
            using (var unitFont    = OsdStyle.CreateFont(OsdStyle.UnitPoints, FontStyle.Regular, s))
            using (var gray        = new SolidBrush(OsdStyle.SecondaryText))
            using (var white       = new SolidBrush(OsdStyle.PrimaryText))
            {
                DrawTracked(g, (caption ?? string.Empty).ToUpperInvariant(), captionFont, gray,
                            x, CaptionBaseline * s, OsdStyle.CaptionTrackingPx * s, Typographic());

                float valueW = DrawAtBaseline(g, value, valueFont, white, x, ValueBaseline * s);
                if (!string.IsNullOrEmpty(unit))
                    g.DrawString(unit, unitFont, gray,
                                 new PointF(x + valueW + 5f * s,
                                            ValueBaseline * s - Ascent(unitFont)));
            }
        }

        private static void PaintBadge(Graphics g, float s, OsdCardPayload p, float x)
        {
            string badge = LandingBadge(p?.GForce);
            if (string.IsNullOrEmpty(badge)) return;

            Color color = OsdStyle.BadgeColor(GForceBands.Classify(p.GForce ?? 0.0));
            using (var font = OsdStyle.CreateFont(OsdStyle.BadgePoints, FontStyle.Bold, s))
            {
                float textW = g.MeasureString(badge, font, new PointF(0, 0), Typographic()).Width;
                float w     = textW + 22f * s;
                float h     = BadgeHeight * s;
                float top   = BadgeTop * s;

                using (var path = CreateRoundedPath(new RectangleF(x, top, w, h), h / 2f))
                using (var fill = new SolidBrush(Color.FromArgb(26, color.R, color.G, color.B)))
                using (var pen  = new Pen(Color.FromArgb(150, color.R, color.G, color.B), Math.Max(1f, s)))
                using (var text = new SolidBrush(color))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                    g.DrawString(badge, font, text,
                                 new PointF(x + (w - textW) / 2f,
                                            top + (h - font.GetHeight(g)) / 2f + 1f * s));
                }

                using (var noteFont = OsdStyle.CreateFont(OsdStyle.BadgePoints, FontStyle.Regular, s))
                using (var note     = new SolidBrush(OsdStyle.SecondaryText))
                {
                    g.DrawString(ScoringNote, noteFont, note,
                                 new PointF(x + w + 8f * s,
                                            top + (h - noteFont.GetHeight(g)) / 2f + 1f * s));
                }
            }
        }

        // ── Utilidades GDI+ ──────────────────────────────────────────────────────

        private static void Prepare(Graphics g)
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        private static StringFormat Typographic()
        {
            var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            // Sin `MeasureTrailingSpaces`, GDI+ mide « · » sin los espacios de los extremos y
            // las líneas de la columna derecha salían pegadas: `Pitch3.5°·Bank0.0°`.
            sf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
            return sf;
        }

        private static float Ascent(Font font)
        {
            FontFamily family = font.FontFamily;
            return family.GetCellAscent(font.Style) * font.Size / family.GetEmHeight(font.Style);
        }

        private static float DrawAtBaseline(Graphics g, string text, Font font, Brush brush,
                                            float x, float baseline)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var sf = Typographic();
            float width = g.MeasureString(text, font, new PointF(0, 0), sf).Width;
            g.DrawString(text, font, brush, new PointF(x, baseline - Ascent(font)), sf);
            return width;
        }

        private static float MeasureTracked(Graphics g, string text, Font font, float tracking)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var sf = Typographic();
            float width = 0f;
            for (int i = 0; i < text.Length; i++)
                width += g.MeasureString(text[i].ToString(), font, new PointF(0, 0), sf).Width;
            return width + tracking * (text.Length - 1);
        }

        private static float DrawTracked(Graphics g, string text, Font font, Brush brush,
                                         float x, float baseline, float tracking, StringFormat sf)
        {
            float width = 0f;
            float y = baseline - Ascent(font);
            for (int i = 0; i < text.Length; i++)
            {
                string ch = text[i].ToString();
                float w = g.MeasureString(ch, font, new PointF(0, 0), sf).Width;
                g.DrawString(ch, font, brush, new PointF(x + width, y), sf);
                width += w + tracking;
            }
            return text.Length == 0 ? 0f : width - tracking;
        }

        private static float MeasureRuns(Graphics g, OsdTextLine line, float points,
                                         FontStyle style, float s)
        {
            if (line == null || line.IsEmpty) return 0f;
            float width = 0f;
            using (var regular = OsdStyle.CreateFont(points, style, s))
            using (var bold    = OsdStyle.CreateFont(points, FontStyle.Bold, s))
            {
                var sf = Typographic();
                foreach (var run in line.Runs)
                {
                    Font font = run.Emphasized ? bold : regular;
                    width += g.MeasureString(run.Text, font, new PointF(0, 0), sf).Width;
                }
            }
            return width;
        }

        private static void DrawRuns(Graphics g, OsdTextLine line, float points, float s,
                                     float x, float baseline)
        {
            if (line == null || line.IsEmpty) return;

            using (var regular = OsdStyle.CreateFont(points, FontStyle.Regular, s))
            using (var bold    = OsdStyle.CreateFont(points, FontStyle.Bold, s))
            using (var primary = new SolidBrush(OsdStyle.PrimaryText))
            using (var secondary = new SolidBrush(OsdStyle.SecondaryText))
            {
                var sf = Typographic();
                float cursor = x;
                foreach (var run in line.Runs)
                {
                    Font font = run.Emphasized ? bold : regular;
                    Brush brush = run.Emphasized ? primary : secondary;
                    g.DrawString(run.Text, font, brush,
                                 new PointF(cursor, baseline - Ascent(font)), sf);
                    cursor += g.MeasureString(run.Text, font, new PointF(0, 0), sf).Width;
                }
            }
        }

        private static float MeasureSimpleLogicalWidth(Graphics g, string text,
                                                       OsdSeverity severity)
        {
            string label, detail;
            SplitSimple(text, severity, out label, out detail);

            using (var labelFont  = OsdStyle.CreateFont(OsdStyle.SimpleLabelPoints, FontStyle.Bold, 1f))
            using (var detailFont = OsdStyle.CreateFont(OsdStyle.SimpleMessagePoints, FontStyle.Bold, 1f))
            {
                float labelW  = MeasureTracked(g, label.ToUpperInvariant(), labelFont,
                                               OsdStyle.CaptionTrackingPx);
                float detailW = g.MeasureString(detail, detailFont, new PointF(0, 0),
                                                Typographic()).Width;
                return Math.Max(SimpleLogicalMinWidth, Math.Max(labelW, detailW) + 2f * Pad);
            }
        }

        private static void SaveBitmap(string path, Bitmap bitmap)
        {
            using (bitmap) bitmap.Save(path, ImageFormat.Png);
        }
    }
}
