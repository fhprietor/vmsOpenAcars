using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using vmsOpenAcars.Controls;
using vmsOpenAcars.UI;

namespace McduPreview
{
    /// <summary>
    /// Lámina de la propuesta «carcasa MCDU»: la ventana entera pintada como el aparato del A320 —
    /// plástico azuloso sucio y viejo, con los grupos hundidos como pantallas LCD.
    ///
    /// **Los colores no son inventados: están muestreados de la foto de referencia** (413×638) que pasó
    /// el mantenedor, con el histograma de cada zona. Lo que se midió y lo que no:
    ///
    /// · Pantalla: fondo #1B1D20 (55.122 px, el color dominante), blanco #F8F4ED (604 px),
    ///   verde #08DA09 (519 px) y cian #09CFCF (374 px).
    /// · Carcasa: una **escala** de plástico iluminado que va de #93A6BD a #6A7C92, con el desgaste
    ///   en #5A6A7E / #505F71 / #45505E y las sombras duras en #393839 / #323233.
    /// · Tecla: cara #323233 y #393839 (11.900 px entre las dos) — **gris oscuro cálido, no negro**.
    ///
    /// Lo que **no** se pudo medir y va por convención MCDU: el ámbar y el magenta. En esta foto la
    /// pantalla solo tiene blanco, verde y cian: no hay un solo píxel cálido (R&gt;B+25) ni rosado.
    ///
    /// Esta lámina **no toca la aplicación**: es el mismo dibujo que después irá a `McduChassis` y
    /// `McduDisplayPanel`, aquí suelto para poder aprobarlo o corregirlo antes de escribir nada.
    /// </summary>
    internal static class WindowPreview
    {
        // ── Paleta medida en la foto ──────────────────────────────────────────────────────────────────

        private static readonly Color ChassisLight = Color.FromArgb(0x93, 0xA6, 0xBD);
        private static readonly Color ChassisHigh  = Color.FromArgb(0x8B, 0x9F, 0xB7);
        private static readonly Color ChassisMid   = Color.FromArgb(0x7D, 0x90, 0xA8);
        private static readonly Color ChassisLow   = Color.FromArgb(0x6A, 0x7C, 0x92);
        private static readonly Color ChassisDirt  = Color.FromArgb(0x5A, 0x6A, 0x7E);
        private static readonly Color ChassisWorn  = Color.FromArgb(0x50, 0x5F, 0x71);
        private static readonly Color Seam         = Color.FromArgb(0x45, 0x50, 0x5E);
        private static readonly Color SeamDark     = Color.FromArgb(0x39, 0x42, 0x4D);
        private static readonly Color ScrewLight   = Color.FromArgb(0xF8, 0xF4, 0xED);
        private static readonly Color ScrewDark    = Color.FromArgb(0x81, 0x95, 0xAD);

        private static readonly Color LcdBg        = Color.FromArgb(0x1B, 0x1D, 0x20);
        private static readonly Color LcdBg2       = Color.FromArgb(0x22, 0x25, 0x27);
        private static readonly Color LcdWhite     = Color.FromArgb(0xF8, 0xF4, 0xED);
        private static readonly Color LcdGreen     = Color.FromArgb(0x08, 0xDA, 0x09);   // medido
        private static readonly Color LcdCyan      = Color.FromArgb(0x09, 0xCF, 0xCF);   // medido
        private static readonly Color LcdGreenSoft = Color.FromArgb(0x3F, 0xCB, 0x55);   // lectura larga
        private static readonly Color LcdAmber     = Color.FromArgb(0xFF, 0xB0, 0x20);   // convención

        /// <summary>Tinta grabada sobre el plástico. #2F3A45 se quedaba en 4,0:1 sobre el claro; esta da 4,6:1.</summary>
        private static readonly Color Ink = Color.FromArgb(0x1F, 0x29, 0x33);

        // Colores de HOY (los de la aplicación), solo para la comparación.
        private static readonly Color TodayPanel   = Color.FromArgb(15, 15, 25);
        private static readonly Color TodayCard    = Color.FromArgb(20, 25, 35);
        private static readonly Color TodayLog     = Color.FromArgb(25, 25, 30);
        private static readonly Color TodayTitle   = Color.Yellow;
        private static readonly Color TodayLogText = Color.FromArgb(144, 238, 144);

        private const int WindowW = 1024;
        private const int WindowH = 768;

        // ── Entrada de la lámina ─────────────────────────────────────────────────────────────────────

        internal static Bitmap Sheet()
        {
            const int margin = 20;
            const int detalleAncho = 360;
            const int detalleAlto = 170;

            // Cada bloque se pinta en SU PROPIO bitmap y luego se compone. Con un solo `Graphics` para
            // todo, un `Graphics` compartido arrastra estado (recortes, transformaciones) de un bloque al
            // siguiente, y eso fue exactamente lo que mezcló las dos ventanas en la primera versión.
            using (Bitmap hoy = Ventana(false))
            using (Bitmap propuesta = Ventana(true))
            using (Bitmap detalle = Detalle(detalleAncho, detalleAlto))
            {
                int width = WindowW + margin * 2;
                int height = margin + 22 + 22 + 18 + WindowH + 40 + 18 + WindowH + 34 + 22 + detalleAlto + margin;

                Bitmap canvas = NewCanvas(width, height);
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    int y = margin;
                    Print(g, "La ventana como un MCDU: carcasa de plástico y grupos como pantallas LCD (1:1)",
                          margin, y, Color.FromArgb(230, 235, 245), 12f, true);
                    y += 20;
                    Print(g, "Los colores están muestreados de tu foto del A320, no elegidos a ojo. Todo va a 1:1: lo que ves es lo que se vería en pantalla.",
                          margin, y, Color.FromArgb(150, 160, 175), 8.5f, false);
                    y += 44;

                    Print(g, "HOY — paneles negros, títulos amarillos, cabecera azul oscuro",
                          margin, y, Color.FromArgb(200, 208, 220), 9.5f, true);
                    y += 18;
                    g.DrawImageUnscaled(hoy, margin, y);
                    y += WindowH + 40;

                    Print(g, "PROPUESTA — carcasa azulosa gastada, cada grupo hundido como una pantalla LCD",
                          margin, y, Color.FromArgb(200, 208, 220), 9.5f, true);
                    y += 18;
                    g.DrawImageUnscaled(propuesta, margin, y);
                    y += WindowH + 34;

                    Print(g, "Detalle 2:1 — el grano, el desgaste y el bisel de la pantalla",
                          margin, y, Color.FromArgb(230, 235, 245), 10f, true);
                    y += 22;
                    g.DrawImageUnscaled(detalle, margin, y);
                    Palette(g, margin + detalleAncho + 40, y);
                }
                return canvas;
            }
        }

        /// <summary>La ventana entera en su propio lienzo, sin arrastrar estado de otros bloques.</summary>
        private static Bitmap Ventana(bool mcdu)
        {
            // La propuesta se monta con LOS CONTROLES DE LA APLICACIÓN (AppSkin), no con un dibujo aparte:
            // si aquí se ve bien, en la ventana se ve igual. La columna de «hoy» sigue dibujada a mano,
            // que es un aspecto que ya no existe en el código.
            if (mcdu) return AppSkin.Render();

            Bitmap bmp = new Bitmap(WindowW, WindowH);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Preparar(g);
                Window(g, new Rectangle(0, 0, WindowW, WindowH), false);
            }
            return bmp;
        }

        /// <summary>La esquina a 2:1, dibujada a esa escala (no es un reescalado: se pinta nítida).</summary>
        private static Bitmap Detalle(int ancho, int alto)
        {
            Bitmap bmp = new Bitmap(ancho, alto);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Preparar(g);
                using (SolidBrush fondo = new SolidBrush(Color.FromArgb(12, 14, 18)))
                    g.FillRectangle(fondo, 0, 0, ancho, alto);

                GraphicsState st = g.Save();
                g.SetClip(new Rectangle(0, 0, ancho, alto));
                g.ScaleTransform(2f, 2f);
                Window(g, new Rectangle(0, 0, WindowW, WindowH), true);
                g.Restore(st);

                using (Pen borde = new Pen(Color.FromArgb(90, 160, 175, 195), 1f))
                    g.DrawRectangle(borde, 0, 0, ancho - 1, alto - 1);
            }
            return bmp;
        }

        private static void Preparar(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
        internal static void Solo(Graphics g, bool mcdu)
        {
            Window(g, new Rectangle(6, 6, 1024, 768), mcdu);
        }

        /// <summary>La ventana entera, en la piel que se le pida. Todo se calcula desde el marco.</summary>
        private static void Window(Graphics g, Rectangle marco, bool mcdu)
        {
            int x = marco.X, y = marco.Y, w = marco.Width;

            // La carcasa: el cuerpo del aparato. En «hoy» es el fondo oscuro de siempre.
            if (mcdu) Chassis(g, marco, true);
            else using (SolidBrush b = new SolidBrush(Color.FromArgb(12, 12, 18))) g.FillRectangle(b, marco);

            int headerH = 35;
            Header(g, new Rectangle(x, y, w, headerH), mcdu);
            y += headerH;

            // FLIGHT INFORMATION: un grupo, una pantalla. Las tarjetas de dentro van separadas por una
            // línea, no por su propio bisel: bisel dentro de bisel es ruido, no parecido.
            int infoH = 232;
            Screen(g, new Rectangle(x + 14, y + 6, w - 28, infoH), mcdu, "FLIGHT INFORMATION", "AIRAC 08SEP-05OCT");
            if (mcdu)
                InfoCards(g, new Rectangle(x + 14, y + 6, w - 28, infoH));
            else
                InfoCardsToday(g, new Rectangle(x + 14, y + 6, w - 28, infoH));
            y += infoH + 8;

            // El log de ACARS — la pantalla que más se lee.
            int logH = 214;
            Screen(g, new Rectangle(x + 14, y, w - 28, logH), mcdu, "ACARS LOG", "TAXI OUT");
            LogText(g, new Rectangle(x + 14, y, w - 28, logH), mcdu);
            y += logH + 8;

            // INCOMING MSG y METAR, lado a lado.
            int rowH = 140;
            int mitad = (w - 28 - 10) / 2;
            Screen(g, new Rectangle(x + 14, y, mitad, rowH), mcdu, "INCOMING MSG", "3 NUEVOS");
            IncomingText(g, new Rectangle(x + 14, y, mitad, rowH), mcdu);
            Screen(g, new Rectangle(x + 14 + mitad + 10, y, w - 28 - mitad - 10, rowH), mcdu, "METAR SKCG", "0845Z");
            MetarText(g, new Rectangle(x + 14 + mitad + 10, y, w - 28 - mitad - 10, rowH), mcdu);
            y += rowH + 8;

            // La tira de estado y la barra de teclas, sobre el plástico.
            int statusH = 26;
            Status(g, new Rectangle(x + 14, y, w - 28, statusH), mcdu);
            y += statusH + 4;

            ButtonBar(g, new Rectangle(x, y, w, marco.Bottom - y), mcdu);
        }

        private static void Header(Graphics g, Rectangle r, bool mcdu)
        {
            if (!mcdu)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(20, 26, 36))) g.FillRectangle(b, r);

            if (!mcdu)
                using (Pen p = new Pen(Color.FromArgb(60, 80, 100))) g.DrawLine(p, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);

            // Sobre el plástico el texto va GRABADO en oscuro, como las leyendas serigrafiadas de un panel
            // de verdad: blanco sobre #93A6BD se queda en 1,9:1 y no se lee. #2F3A45 da 4,6:1.
            Color tinta = mcdu ? Ink : Color.FromArgb(220, 220, 220);
            Print(g, "vmsOpenACars v0.9.45", r.X + 44, r.Y + 10, tinta, 10f, true);
            Print(g, "SKBO -> SKCG   ·   FL350   ·   ACARS ACTIVO", r.X + 262, r.Y + 11,
                  mcdu ? Ink : Color.FromArgb(120, 200, 200), 8.5f, false);

            int bx = r.Right - 106;
            foreach (string cap in new string[] { "\u2699", "\u2500", "\u25BE" })
            {
                Key(g, new Rectangle(bx, r.Y + 5, 30, 25), cap, McduLegend.Neutral, 11f);
                bx += 34;
            }
        }

        // ── Pantallas ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// El grupo como pantalla hundida en el plástico: bisel, hueco con sombra, LCD con su degradado,
        /// viñeta, líneas de barrido y un reflejo tenue. Devuelve el rectángulo donde va el texto.
        /// </summary>
        private static Rectangle Screen(Graphics g, Rectangle marco, bool mcdu, string titulo, string anotacion)
        {
            if (!mcdu)
            {
                using (SolidBrush b = new SolidBrush(TodayPanel)) g.FillRectangle(b, marco);
                using (Pen p = new Pen(Color.FromArgb(70, 70, 70))) g.DrawRectangle(p, marco.X, marco.Y, marco.Width - 1, marco.Height - 1);
                Print(g, titulo, marco.X + 6, marco.Y + 4, TodayTitle, 10f, true);
                if (anotacion != null) Print(g, anotacion, marco.Right - 150, marco.Y + 4, Color.FromArgb(0, 200, 200), 9f, false);
                return marco;
            }

            // El bisel: plástico un poco más oscuro para que el hueco se lea como hueco.
            using (LinearGradientBrush bisel = new LinearGradientBrush(marco, ChassisMid, ChassisDirt, 90f))
                g.FillRectangle(bisel, marco);
            using (Pen labio = new Pen(Color.FromArgb(120, ChassisLight), 1f))
                g.DrawLine(labio, marco.Left, marco.Top, marco.Right - 1, marco.Top);
            using (Pen sombra = new Pen(Color.FromArgb(150, SeamDark), 1f))
                g.DrawLine(sombra, marco.Left, marco.Bottom - 1, marco.Right - 1, marco.Bottom - 1);

            // El hueco y la costura dura de dentro.
            Rectangle hueco = Rectangle.Inflate(marco, -3, -3);
            using (SolidBrush b = new SolidBrush(SeamDark)) g.FillRectangle(b, hueco);
            Rectangle lcd = Rectangle.Inflate(hueco, -1, -1);

            using (LinearGradientBrush fondo = new LinearGradientBrush(lcd, LcdBg, LcdBg2, 90f))
                g.FillRectangle(fondo, lcd);

            // Viñeta: en la foto las esquinas de la pantalla están más apagadas que el centro.
            Vignette(g, lcd, 72);
            Scanlines(g, lcd);

            Print(g, titulo, lcd.X + 8, lcd.Y + 5, LcdWhite, 9.5f, true);
            if (anotacion != null) Print(g, anotacion, lcd.Right - 110, lcd.Y + 6, LcdCyan, 8.5f, false);
            using (Pen linea = new Pen(Color.FromArgb(70, LcdCyan)))
                g.DrawLine(linea, lcd.X + 8, lcd.Y + 21, lcd.Right - 8, lcd.Y + 21);

            return lcd;
        }

        private static void InfoCards(Graphics g, Rectangle marco)
        {
            Rectangle lcd = Rectangle.Inflate(marco, -4, -4);
            int top = lcd.Y + 28;
            int w = (lcd.Width - 40) / 4;
            string[] titulos = { "PROGRESO", "AERODINAMICA", "COMBUSTIBLE", "ALTITUD" };
            string[][] filas =
            {
                new string[] { "DIST", "1224/1516 NM", "REST", "292 NM / 00:41", "T/A", "01:42:11" },
                new string[] { "IAS", "281 kt", "GS", "472 kt", "MACH", "0.84", "VS", "-1200 fpm" },
                new string[] { "INI", "12400 kg", "ACT", "9820 kg", "USO", "2580 kg" },
                new string[] { "ALT", "FL350", "AGL", "33200 ft", "CRZ", "FL350", "QNH", "1013" },
            };

            for (int i = 0; i < 4; i++)
            {
                int cx = lcd.X + 8 + i * (w + 8);
                Print(g, titulos[i], cx, top, LcdCyan, 8f, true);
                int fy = top + 18;
                string[] f = filas[i];
                for (int k = 0; k + 1 < f.Length; k += 2)
                {
                    Print(g, f[k], cx, fy, LcdWhite, 8f, false);
                    Print(g, f[k + 1], cx + 46, fy, LcdGreen, 8.5f, false);
                    fy += 17;
                }
                if (i < 3)
                    using (Pen sep = new Pen(Color.FromArgb(40, LcdCyan)))
                        g.DrawLine(sep, cx + w + 4, top, cx + w + 4, lcd.Bottom - 8);
            }
        }

        private static void InfoCardsToday(Graphics g, Rectangle marco)
        {
            int top = marco.Y + 26;
            int w = (marco.Width - 40) / 4;
            string[] titulos = { "PROGRESO", "AERODINAMICA", "COMBUSTIBLE", "ALTITUD" };
            string[][] filas =
            {
                new string[] { "DIST", "1224/1516 NM", "REST", "292 NM / 00:41" },
                new string[] { "IAS", "281 kt", "GS", "472 kt / M 0.84", "VS", "-1200 fpm" },
                new string[] { "INI", "12400 kg", "ACT", "9820 kg", "USO", "2580 kg" },
                new string[] { "ALT", "FL350", "AGL", "33200 ft", "QNH", "1013" },
            };
            for (int i = 0; i < 4; i++)
            {
                int cx = marco.X + 8 + i * (w + 8);
                using (SolidBrush b = new SolidBrush(TodayCard))
                    g.FillRectangle(b, new Rectangle(cx - 4, top - 4, w, marco.Height - 36));
                Print(g, titulos[i], cx, top, TodayTitle, 8f, true);
                int fy = top + 18;
                string[] f = filas[i];
                for (int k = 0; k + 1 < f.Length; k += 2)
                {
                    Print(g, f[k] + ": " + f[k + 1], cx, fy, TodayLogText, 8.5f, false);
                    fy += 17;
                }
            }
        }

        private static void LogText(Graphics g, Rectangle marco, bool mcdu)
        {
            if (!mcdu)
            {
                using (SolidBrush b = new SolidBrush(TodayLog))
                    g.FillRectangle(b, new Rectangle(marco.X + 2, marco.Y + 2, marco.Width - 4, marco.Height - 4));
            }

            Rectangle lcd = Rectangle.Inflate(marco, -4, -4);
            string[][] lineas =
            {
                new string[] { "ACARS ACTIVE — vmsOpenACars v0.9.45", "blanco" },
                new string[] { "SKBO -> SKCG   FL350   OFP cargado", "cuerpo" },
                new string[] { "NavData: AIRAC 08SEP-05OCT   613 espacios aereos", "cian" },
                new string[] { "Cabina: 7 anuncios listos", "cuerpo" },
                new string[] { "Fase: TaxiOut — guia de rodaje a la 14L", "cuerpo" },
                new string[] { "RAAS: APROXIMANDO PISTA 14L   a 150 m", "cuerpo" },
                new string[] { "PENALTY  LANDING LT OFF  -5 PTS", "ambar" },
                new string[] { "PUSHBACK: fin del empuje en 4.69788,-74.13721", "cuerpo" },
                new string[] { "Posicion enviada: 1224 NM restantes   ETA 14:22Z", "verde" },
            };

            int y = lcd.Y + 30;
            for (int i = 0; i < lineas.Length; i++)
            {
                Color color;
                if (!mcdu)
                {
                    color = i == 0 ? Color.FromArgb(0, 220, 120) : TodayLogText;
                    if (lineas[i][1] == "ambar") color = Color.FromArgb(255, 200, 0);
                }
                else if (lineas[i][1] == "blanco") color = LcdWhite;
                else if (lineas[i][1] == "cian") color = LcdCyan;
                else if (lineas[i][1] == "ambar") color = LcdAmber;
                else if (lineas[i][1] == "verde") color = LcdGreen;
                else color = LcdGreenSoft;

                Print(g, lineas[i][0], lcd.X + 10, y, color, 9f, false);
                y += 19;
            }
        }

        private static void IncomingText(Graphics g, Rectangle marco, bool mcdu)
        {
            Rectangle lcd = Rectangle.Inflate(marco, -4, -4);
            if (!mcdu)
                using (SolidBrush b = new SolidBrush(Color.Black))
                    g.FillRectangle(b, new Rectangle(marco.X + 2, marco.Y + 2, marco.Width - 4, marco.Height - 4));

            int y = lcd.Y + 30;
            Print(g, "<MENSAJE>", lcd.Right - 96, y, mcdu ? LcdCyan : Color.FromArgb(0, 200, 200), 8.5f, false);
            Print(g, "DE: DESPACHO", lcd.X + 10, y, mcdu ? LcdWhite : TodayTitle, 8.5f, false);
            y += 20;
            Print(g, "EL VUELO SKCG LLEVA 20 MIN DE RETRASO", lcd.X + 10, y, mcdu ? LcdGreenSoft : TodayLogText, 8.5f, false);
            y += 18;
            Print(g, "EN LA SALIDA. AJUSTAR EL BLOCK OFF.", lcd.X + 10, y, mcdu ? LcdGreenSoft : TodayLogText, 8.5f, false);
            y += 24;
            Print(g, "2 MENSAJES MAS EN LA COLA", lcd.X + 10, y, mcdu ? LcdCyan : Color.FromArgb(0, 200, 200), 8.5f, false);
        }

        private static void MetarText(Graphics g, Rectangle marco, bool mcdu)
        {
            Rectangle lcd = Rectangle.Inflate(marco, -4, -4);
            if (!mcdu)
                using (SolidBrush b = new SolidBrush(Color.Black))
                    g.FillRectangle(b, new Rectangle(marco.X + 2, marco.Y + 2, marco.Width - 4, marco.Height - 4));

            int y = lcd.Y + 30;
            Print(g, "SKCG 150845Z 02008KT 9999 SCT018", lcd.X + 10, y, mcdu ? LcdGreenSoft : TodayLogText, 8.5f, false);
            y += 18;
            Print(g, "28/25 Q1010 NOSIG", lcd.X + 10, y, mcdu ? LcdGreenSoft : TodayLogText, 8.5f, false);
            y += 26;
            Print(g, "VIENTO", lcd.X + 10, y, mcdu ? LcdWhite : TodayTitle, 8.5f, false);
            Print(g, "020/08 kt", lcd.X + 74, y, mcdu ? LcdGreen : TodayLogText, 8.5f, false);
            y += 18;
            Print(g, "CRUZADO", lcd.X + 10, y, mcdu ? LcdWhite : TodayTitle, 8.5f, false);
            Print(g, "3 kt izq", lcd.X + 74, y, mcdu ? LcdGreen : TodayLogText, 8.5f, false);
        }

        private static void Status(Graphics g, Rectangle r, bool mcdu)
        {
            string[] textos = { "SIM READY", "Online", "---", "ICAO ---", "GPS ---", "COM ---", "NAV ---" };
            Color[] colores =
            {
                Color.FromArgb(0, 214, 110), Color.FromArgb(0, 214, 110), Color.FromArgb(140, 150, 165),
                Color.FromArgb(140, 150, 165), Color.FromArgb(140, 150, 165), Color.FromArgb(140, 150, 165),
                Color.FromArgb(140, 150, 165)
            };
            int x = r.X;
            for (int i = 0; i < textos.Length; i++)
            {
                int w = 18 + textos[i].Length * 7;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 12, 14, 18)))
                    g.FillRectangle(b, new Rectangle(x, r.Y, w, r.Height));
                using (Pen p = new Pen(Color.FromArgb(90, SeamDark)))
                    g.DrawRectangle(p, x, r.Y, w - 1, r.Height - 1);
                Print(g, textos[i], x + 7, r.Y + 7, colores[i], 8f, false);
                x += w + 6;
            }
        }

        private static void ButtonBar(Graphics g, Rectangle r, bool mcdu)
        {
            if (!mcdu)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 40, 50))) g.FillRectangle(b, r);

            int[] anchos = { 123, 123, 123, 123, 123, 123, 123, 123 };
            string[] textos = { "ENTRAR", "OFP", "MAPA", "METEO", "DESPACHO", "BITÁCORA", "INICIAR", "SALIR" };
            Color[] leyendas =
            {
                McduLegend.Info, McduLegend.Info, McduLegend.Neutral, McduLegend.Neutral,
                McduLegend.Confirm, McduLegend.Steel, McduLegend.Warning, McduLegend.Danger
            };

            int x = r.X + 6;
            int ky = r.Y + 8;
            for (int i = 0; i < textos.Length; i++)
            {
                // La rayita del bisel: en el aparato real cada tecla de línea tiene la suya, apuntando a
                // su renglón. Aquí es lo que convierte la barra en la columna de LSK del MCDU.
                using (Pen raya = new Pen(Color.FromArgb(190, LcdWhite), 1f))
                    g.DrawLine(raya, x + 12, ky - 5, x + 34, ky - 5);

                Key(g, new Rectangle(x, ky, anchos[i], 34), textos[i], leyendas[i], 10f);
                x += anchos[i] + 4;
            }
        }

        // ── Las teclas (la leyenda la pinta el renderizador de verdad) ────────────────────────────────

        private static void Key(Graphics g, Rectangle r, string texto, Color leyenda, float cuerpo)
        {
            using (Font font = new Font("Consolas", cuerpo, FontStyle.Bold))
            {
                // Cuerpo de la tecla: la cara medida en la foto es un gris oscuro cálido, no negro.
                using (GraphicsPath camino = Redondeado(r, 4f))
                using (LinearGradientBrush cara = new LinearGradientBrush(r, Color.FromArgb(0x44, 0x44, 0x49), Color.FromArgb(0x14, 0x14, 0x17), 90f))
                {
                    g.FillPath(cara, camino);
                    using (Pen borde = new Pen(Color.FromArgb(0xA0, 0x10, 0x11, 0x14), 1f))
                        g.DrawPath(borde, camino);
                }
                using (Pen luz = new Pen(Color.FromArgb(90, 0x8B, 0x9F, 0xB7), 1f))
                    g.DrawLine(luz, r.Left + 3, r.Top + 1, r.Right - 4, r.Top + 1);

                McduKeyRenderer.Paint(g, r, texto, leyenda, font, McduKeyState.Normal, 0f);
            }
        }

        private static GraphicsPath Redondeado(Rectangle r, float radio)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radio * 2f;
            p.AddArc(r.Left, r.Top, d, d, 180f, 90f);
            p.AddArc(r.Right - d - 1, r.Top, d, d, 270f, 90f);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0f, 90f);
            p.AddArc(r.Left, r.Bottom - d - 1, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        // ── Texturas de la carcasa y de la pantalla ──────────────────────────────────────────────────

        /// <summary>
        /// El plástico: degradado vertical (la luz viene de arriba, como en la foto), grano fino y
        /// manchas de desgaste. Las manchas van donde la foto las tiene: cantos y alrededores de las
        /// teclas, que es donde se acumula la porquería.
        /// </summary>
        private static void Chassis(Graphics g, Rectangle r, bool wear)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, ChassisHigh, ChassisDirt, 90f))
            {
                // El plástico va de #859AB2 a #5A6A7E, no de #93A6BD: en la foto el tono claro es un
                // REFLEJO del canto de arriba, no el color del panel. Un panel entero a #93A6BD se ve
                // nuevo y limpio, justo lo contrario de lo que se busca.
                ColorBlend mezcla = new ColorBlend(5);
                mezcla.Colors = new Color[]
                {
                    ChassisHigh, ChassisMid, Color.FromArgb(0x6F, 0x82, 0x99),
                    Color.FromArgb(0x63, 0x74, 0x8A), ChassisDirt
                };
                mezcla.Positions = new float[] { 0f, 0.26f, 0.58f, 0.84f, 1f };
                b.InterpolationColors = mezcla;
                g.FillRectangle(b, r);
            }

            if (!wear) return;

            Random rnd = new Random(20251015);

            // Manchas grandes y suaves: el tono general, no la suciedad.
            for (int i = 0; i < 12; i++)
            {
                int w = 60 + rnd.Next(190);
                int h = 26 + rnd.Next(80);
                int px = r.Left + rnd.Next(r.Width) - w / 2;
                int py = rnd.Next(3) == 0 ? r.Top + rnd.Next(40) : r.Bottom - h - rnd.Next(90);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(px, py, w, h);
                    using (PathGradientBrush pg = new PathGradientBrush(p))
                    {
                        pg.CenterColor = Color.FromArgb(16 + rnd.Next(14), ChassisWorn);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, ChassisWorn) };
                        g.FillPath(pg, p);
                    }
                }
            }

            // Manchas FUERTES y pequeñas: las que se ven. Van donde se apoya la mano y junto a los cantos.
            for (int i = 0; i < 14; i++)
            {
                int w = 14 + rnd.Next(38);
                int h = 8 + rnd.Next(18);
                int px = rnd.Next(2) == 0 ? r.Left + 6 + rnd.Next(40) : r.Right - 46 - rnd.Next(40);
                if (rnd.Next(3) == 0) { px = r.Left + rnd.Next(r.Width / 2); }
                int py = rnd.Next(2) == 0 ? r.Top + 8 + rnd.Next(60) : r.Bottom - 30 - rnd.Next(150);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(px, py, w, h);
                    using (PathGradientBrush pg = new PathGradientBrush(p))
                    {
                        pg.CenterColor = Color.FromArgb(46 + rnd.Next(26), ChassisWorn);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, ChassisWorn) };
                        g.FillPath(pg, p);
                    }
                }
            }

            // Grano fino agrupado: en los cantos y alrededor del teclado, que es donde se acumula.
            for (int i = 0; i < 5200; i++)
            {
                int px, py;
                int donde = rnd.Next(4);
                if (donde == 0) { px = r.Left + rnd.Next(r.Width); py = r.Top + rnd.Next(26); }
                else if (donde == 1) { px = r.Left + rnd.Next(r.Width); py = r.Bottom - rnd.Next(80); }
                else if (donde == 2) { px = r.Left + rnd.Next(16); py = r.Top + rnd.Next(r.Height); }
                else { px = r.Right - rnd.Next(16); py = r.Top + rnd.Next(r.Height); }

                bool claro = rnd.Next(3) == 0;
                Color c = claro ? ChassisLight : SeamDark;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(claro ? 20 : 16, c)))
                    g.FillRectangle(b, px, py, 1, 1);
            }

            // Rayones de roce: en la foto los cantos tienen marcas, no solo manchas.
            for (int i = 0; i < 30; i++)
            {
                int sy = r.Top + rnd.Next(r.Height);
                int sx = r.Left + rnd.Next(r.Width / 2);
                int largo = 40 + rnd.Next(240);
                using (Pen roce = new Pen(Color.FromArgb(14 + rnd.Next(16), rnd.Next(2) == 0 ? ChassisLight : SeamDark), 1f))
                    g.DrawLine(roce, sx, sy, sx + largo, sy + rnd.Next(3) - 1);
            }

            // Viñeteado del propio plástico: los cantos del aparato están más sucios y apagados.
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddRectangle(r);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterColor = Color.FromArgb(0, ChassisWorn);
                    pg.SurroundColors = new Color[] { Color.FromArgb(34, ChassisWorn) };
                    g.FillRectangle(pg, r);
                }
            }

            using (Pen luz = new Pen(Color.FromArgb(120, 0xA8, 0xB8, 0xCC), 1f))
                g.DrawLine(luz, r.Left, r.Top, r.Right, r.Top);
            using (Pen bajo = new Pen(Color.FromArgb(160, SeamDark), 2f))
                g.DrawLine(bajo, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);

            Screw(g, r.Left + 18, r.Top + 17, 8f);
            Screw(g, r.Right - 18, r.Top + 17, 8f);
            Screw(g, r.Left + 18, r.Bottom - 26, 8f);
            Screw(g, r.Right - 18, r.Bottom - 26, 8f);
        }
        private static void Screw(Graphics g, float cx, float cy, float radio)
        {
            RectangleF caja = new RectangleF(cx - radio, cy - radio, radio * 2f, radio * 2f);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(caja);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterPoint = new PointF(cx - radio * 0.35f, cy - radio * 0.35f);
                    pg.CenterColor = ScrewLight;
                    pg.SurroundColors = new Color[] { ScrewDark };
                    g.FillPath(pg, p);
                }
                using (Pen borde = new Pen(Color.FromArgb(150, SeamDark), 1f))
                    g.DrawEllipse(borde, caja);
            }
            using (Pen ranura = new Pen(Color.FromArgb(170, ChassisWorn), Math.Max(1f, radio * 0.30f)))
                g.DrawLine(ranura, cx - radio * 0.52f, cy + radio * 0.34f, cx + radio * 0.52f, cy - radio * 0.34f);
        }

        private static void Vignette(Graphics g, Rectangle r, int fuerza)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddRectangle(r);
                using (PathGradientBrush pg = new PathGradientBrush(p))
                {
                    pg.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    pg.SurroundColors = new Color[] { Color.FromArgb(fuerza, 0, 0, 0) };
                    g.FillRectangle(pg, r);
                }
            }
        }

        private static void Scanlines(Graphics g, Rectangle r)
        {
            using (Pen linea = new Pen(Color.FromArgb(17, 0, 0, 0), 1f))
                for (int y = r.Top; y < r.Bottom; y += 3)
                    g.DrawLine(linea, r.Left, y, r.Right, y);
        }

        private static void Sheen(Graphics g, Rectangle r)
        {
            GraphicsState st = g.Save();
            g.SetClip(r);
            using (LinearGradientBrush b = new LinearGradientBrush(
                new Rectangle(r.X - r.Height / 2, r.Y, r.Height, r.Height), Color.FromArgb(9, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 20f))
                g.FillRectangle(b, r);
            g.Restore(st);
        }

        // ── La paleta, con los números de contraste medidos ──────────────────────────────────────────

        private static void Palette(Graphics g, int x, int y)
        {
            Print(g, "La paleta, muestreada de la foto", x, y, Color.FromArgb(230, 235, 245), 10f, true);
            int cy = y + 22;

            Muestra(g, x, cy, LcdBg, "LCD fondo", "medido, 55.122 px"); cy += 26;
            Muestra(g, x, cy, LcdWhite, "LCD blanco", "medido, 604 px"); cy += 26;
            Muestra(g, x, cy, LcdGreen, "LCD verde", "medido, 519 px"); cy += 26;
            Muestra(g, x, cy, LcdGreenSoft, "verde de lectura", "para el cuerpo del log"); cy += 26;
            Muestra(g, x, cy, LcdCyan, "LCD cian", "medido, 374 px"); cy += 26;
            Muestra(g, x, cy, LcdAmber, "LCD ambar", "convencion MCDU, NO medido"); cy += 30;

            Muestra(g, x, cy, ChassisLight, "carcasa clara", "medido"); cy += 26;
            Muestra(g, x, cy, ChassisMid, "carcasa media", "medido"); cy += 26;
            Muestra(g, x, cy, ChassisLow, "carcasa baja", "medido"); cy += 26;
            Muestra(g, x, cy, ChassisWorn, "desgaste", "medido"); cy += 36;

            Print(g, "Contraste sobre el fondo del LCD (#1B1D20), umbral WCAG 4,5:1", x, cy, Color.FromArgb(230, 235, 245), 9f, true);
            cy += 18;
            Fila(g, x, cy, "blanco medido", ColorContrast.Ratio(LcdWhite, LcdBg)); cy += 15;
            Fila(g, x, cy, "verde medido", ColorContrast.Ratio(LcdGreen, LcdBg)); cy += 15;
            Fila(g, x, cy, "verde de lectura", ColorContrast.Ratio(LcdGreenSoft, LcdBg)); cy += 15;
            Fila(g, x, cy, "cian medido", ColorContrast.Ratio(LcdCyan, LcdBg)); cy += 15;
            Fila(g, x, cy, "ambar (convencion)", ColorContrast.Ratio(LcdAmber, LcdBg)); cy += 15;
            Fila(g, x, cy, "amarillo de HOY", ColorContrast.Ratio(TodayTitle, LcdBg)); cy += 22;

            Print(g, "Sobre el plástico (el texto va grabado en oscuro, no en blanco):", x, cy, Color.FromArgb(230, 235, 245), 9f, true);
            cy += 18;
            Fila(g, x, cy, "blanco sobre plástico", ColorContrast.Ratio(Color.White, ChassisHigh)); cy += 15;
            Fila(g, x, cy, "tinta grabada", ColorContrast.Ratio(Ink, ChassisHigh)); cy += 15;
            Plain(g, x, cy, "El blanco no llega ni de lejos: por eso la cabecera va grabada."); cy += 14;

            Print(g, "El amarillo de hoy cumple de sobra, pero el MCDU no tiene amarillo:", x, cy, Color.FromArgb(150, 160, 175), 8f, false);
            cy += 14;
            Print(g, "usa blanco para el título de página y cian para las anotaciones.", x, cy, Color.FromArgb(150, 160, 175), 8f, false);
        }

        private static void Muestra(Graphics g, int x, int y, Color c, string nombre, string nota)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, new Rectangle(x, y, 26, 18));
            using (Pen p = new Pen(Color.FromArgb(120, 160, 175, 195))) g.DrawRectangle(p, x, y, 26, 18);
            Print(g, nombre + "   #" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"), x + 34, y + 3,
                  Color.FromArgb(215, 222, 232), 8.5f, true);
            Print(g, nota, x + 236, y + 3, Color.FromArgb(140, 150, 165), 8f, false);
        }

        private static void Plain(Graphics g, int x, int y, string texto)
        {
            Print(g, texto, x, y, Color.FromArgb(150, 160, 175), 8f, false);
        }

        private static void Fila(Graphics g, int x, int y, string nombre, double ratio)
        {
            Color c = ratio >= ColorContrast.MinimumForText ? Color.FromArgb(0, 214, 110) : Color.FromArgb(255, 118, 110);
            Print(g, nombre, x, y, Color.FromArgb(215, 222, 232), 8.5f, false);
            Print(g, ratio.ToString("F1") + ":1", x + 160, y, c, 8.5f, true);
        }

        // ── Utilidades ───────────────────────────────────────────────────────────────────────────────

        private static void Print(Graphics g, string texto, int x, int y, Color color, float cuerpo, bool negrita)
        {
            using (Font f = new Font("Consolas", cuerpo, negrita ? FontStyle.Bold : FontStyle.Regular))
                TextRenderer.DrawText(g, texto, f, new Point(x, y), color,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        private static Bitmap NewCanvas(int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 29, 34)))
                g.FillRectangle(b, 0, 0, width, height);
            return bmp;
        }
    }
}
