using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using vmsOpenAcars.UI;
using vmsOpenAcars.Controls;

namespace McduPreview
{
    /// <summary>Un botón de la maqueta: rótulo y color (el que hoy es fondo y en la propuesta es leyenda).</summary>
    internal sealed class KeySpec
    {
        internal KeySpec(string text, Color color)
        {
            Text = text;
            Color = color;
            Enabled = true;
        }

        internal KeySpec(string text, Color color, bool enabled)
        {
            Text = text;
            Color = color;
            Enabled = enabled;
        }

        internal string Text;
        internal Color Color;
        internal bool Enabled;
    }

    /// <summary>
    /// Una tecla de la lámina de los demás formularios, con el tamaño y la tipografía **reales** que le da
    /// su formulario.
    /// </summary>
    internal sealed class KeyVisual
    {
        internal KeyVisual(string text, Color legend, int width, int height, string family, float size)
        {
            Text = text;
            Legend = legend;
            Width = width;
            Height = height;
            Family = family;
            Size = size;
        }

        internal string Text;
        internal Color Legend;
        internal int Width;
        internal int Height;
        internal string Family;
        internal float Size;
    }

    /// <summary>
    /// Arnés de previsualización de la propuesta MCDU. No forma parte de la aplicación: compila los dos
    /// archivos que se proponen (`McduKeyRenderer` + `McduKeyButton`) y pinta con ellos las láminas PNG.
    ///
    /// Las barras se dibujan con **controles reales** (`Button` para el «hoy», `McduKeyButton` para la
    /// propuesta) capturados con `DrawToBitmap`, así que lo que se ve es lo que pintaría la aplicación y no
    /// un dibujo aparte. Los estados que no se pueden forzar en un control vivo (hover, pulsado) sí van
    /// contra `McduKeyRenderer.Paint`, que es exactamente lo que llama el control.
    /// </summary>
    internal static class Program
    {
        // Geometría de `MainButtonBar` y `MainForm.InitializeButtons`: no se toca ni un píxel.
                /// <summary>Ancho de la tecla de hoy (v0.9.44): 90 px fijos, con 270 px muertos a la derecha.</summary>
        private const int LegacyKeyW = 90;

        /// <summary>Lo que le toca a cada tecla con el ancho repartido en la ventana de 1024: (1024-6-7x4)/8.</summary>
        private const int BarKeyW = 123;
        private const int KeyH = 34;
        private const int Gap = 4;
        private const int Pad = 6;

        private static readonly Color PanelColor = Color.FromArgb(30, 40, 50);   // pnlButtons
        private static readonly Color CanvasColor = Color.FromArgb(13, 15, 19);
        private static readonly Color TitleColor = Color.FromArgb(232, 238, 246);
        private static readonly Color NoteColor = Color.FromArgb(146, 158, 172);

        /// <summary>
        /// El rojo de CANCELAR/ABORTAR. No es el `Color.Red` de hoy: al encender la retroiluminación el
        /// fondo del capuchón sube de brillo y el rojo puro se quedaba en 4,39:1, por debajo del umbral.
        /// Este deja 5,2:1 con la luz encendida.
        /// </summary>
        private static readonly Color LegendRed = Color.FromArgb(255, 118, 110);

        // ── Paletas ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>Los ocho fondos de v0.9.44, tal cual están en `MainButtonBar`.</summary>
        private static List<KeySpec> BarToday()
        {
            return new List<KeySpec>
            {
                new KeySpec("ENTRAR",    Color.FromArgb(  0, 120, 200)),
                new KeySpec("OFP",      Color.FromArgb(  0, 100, 200), false),
                new KeySpec("MAPA",     Color.FromArgb( 60,  70,  80), false),
                new KeySpec("METEO",    Color.FromArgb( 60,  70,  80)),
                new KeySpec("DESPACHO", Color.FromArgb(  0, 150,   0)),
                new KeySpec("BITÁCORA", Color.FromArgb( 60,  90, 110)),
                new KeySpec("INICIAR",  Color.FromArgb(200, 100,   0)),
                new KeySpec("CANCELAR", Color.FromArgb(150,   0,   0)),
            };
        }

        /// <summary>
        /// Variante A — «mismos tonos, aclarados»: cada botón conserva su familia de color de hoy
        /// (azul, pizarra, verde, ámbar, rojo) subida hasta que se lee sobre negro.
        /// </summary>
        private static List<KeySpec> BarVariantA()
        {
            return new List<KeySpec>
            {
                new KeySpec("ENTRAR",    Color.FromArgb( 74, 178, 255)),
                new KeySpec("OFP",      Color.FromArgb( 74, 178, 255), false),
                new KeySpec("MAPA",     Color.FromArgb(166, 180, 196), false),
                new KeySpec("METEO",    Color.FromArgb(166, 180, 196)),
                new KeySpec("DESPACHO", Color.FromArgb(  0, 214, 110)),
                new KeySpec("BITÁCORA", Color.FromArgb(138, 186, 226)),
                new KeySpec("INICIAR",  Color.FromArgb(255, 168,  32)),
                new KeySpec("CANCELAR", LegendRed),
            };
        }

        /// <summary>
        /// Variante B — «paleta MCDU»: solo los cinco colores del MCDU (blanco, cian, verde, ámbar, rojo)
        /// repartidos por significado, sin conservar el tono de cada botón.
        /// </summary>
        private static List<KeySpec> BarVariantB()
        {
            return new List<KeySpec>
            {
                new KeySpec("ENTRAR",    Color.FromArgb( 96, 224, 255)),
                new KeySpec("OFP",      Color.FromArgb( 96, 224, 255), false),
                new KeySpec("MAPA",     Color.FromArgb(214, 222, 232), false),
                new KeySpec("METEO",    Color.FromArgb(214, 222, 232)),
                new KeySpec("DESPACHO", Color.FromArgb(  0, 214, 110)),
                new KeySpec("BITÁCORA", Color.FromArgb(214, 222, 232)),
                new KeySpec("INICIAR",  Color.FromArgb(255, 168,  32)),
                new KeySpec("CANCELAR", LegendRed),
            };
        }

        // ── Entrada ───────────────────────────────────────────────────────────────────────────────────

        [STAThread]
        private static int Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "out");
            Directory.CreateDirectory(outDir);

            PrintContrastTable();

            Save(Path.Combine(outDir, "01-antes-y-despues.png"), SheetComparison(2));
            Save(Path.Combine(outDir, "02-estados.png"), SheetStates(2));
            Save(Path.Combine(outDir, "03-tamano-real.png"), SheetRealSize());
            Save(Path.Combine(outDir, "04-en-contexto.png"), SheetContext());
            Save(Path.Combine(outDir, "05-otros-formularios.png"), SheetOtherForms(2));
            Save(Path.Combine(outDir, "06-carcasa-mcdu.png"), WindowPreview.Sheet());

            Console.WriteLine();
            Console.WriteLine("PNG en " + outDir);
            return 0;
        }

        private static void Save(string path, Bitmap bitmap)
        {
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Dispose();
            Console.WriteLine("escrito " + path);
        }

        // ── Medicion de contraste (numeros reales, no estimados) ──────────────────────────────────────

        private static void PrintContrastTable()
        {
            Console.WriteLine("Contraste de la leyenda contra el fondo del capuchón. «negro» mide contra el negro puro");
            Console.WriteLine("y «con luz» contra el mismo punto CON el lavado de color encendido, que es el que manda:");
            Console.WriteLine("la retroiluminación sube el brillo del fondo y se come margen.");
            Console.WriteLine();
            Console.WriteLine("boton        hoy (fondo)             hoy   A (leyenda)          negro  con luz   B (leyenda)          negro  con luz");
            Console.WriteLine("--------------------------------------------------------------------------------------------------------------------");

            List<KeySpec> today = BarToday();
            List<KeySpec> a = BarVariantA();
            List<KeySpec> b = BarVariantB();

            for (int i = 0; i < today.Count; i++)
            {
                Console.WriteLine(string.Format(
                    "{0,-12} {1,-21} {2,6:F2}   {3,-19} {4,6:F2} {5,7:F2}   {6,-19} {7,6:F2} {8,7:F2}",
                    today[i].Text,
                    ColorName(today[i].Color),
                    ColorContrast.Ratio(today[i].Color, McduKeyBody()),
                    ColorName(a[i].Color),
                    ColorContrast.Ratio(a[i].Color, McduKeyBody()),
                    ColorContrast.Ratio(a[i].Color, McduKeyRenderer.BacklitBody(a[i].Color)),
                    ColorName(b[i].Color),
                    ColorContrast.Ratio(b[i].Color, McduKeyBody()),
                    ColorContrast.Ratio(b[i].Color, McduKeyRenderer.BacklitBody(b[i].Color))));
            }

            Console.WriteLine();
            Console.WriteLine("Estados de INICIAR: normal " +
                ColorContrast.Ratio(BarVariantA()[6].Color, McduKeyBody()).ToString("F2") +
                "   deshabilitado " +
                ColorContrast.Ratio(McduKeyRenderer.DisabledLegend(BarVariantA()[6].Color), McduKeyBody()).ToString("F2"));
            Console.WriteLine("Umbral WCAG para texto normal: " + ColorContrast.MinimumForText.ToString("F1") + ":1");
            Console.WriteLine();
        }

        /// <summary>El negro medio del cuerpo de la tecla, que es contra lo que se mide la leyenda.</summary>
        private static Color McduKeyBody()
        {
            return Color.FromArgb(29, 29, 33);   // punto medio del degradado 44,44,49 → 14,14,17
        }

        private static string ColorName(Color c)
        {
            return "rgb(" + c.R + "," + c.G + "," + c.B + ")";
        }

        // ── Laminas ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>Lamina 1: la barra de hoy y las dos variantes, a la escala pedida.</summary>
        private static Bitmap SheetComparison(int scale)
        {
            List<KeySpec> today = BarToday();
            List<KeySpec> a = BarVariantA();
            List<KeySpec> b = BarVariantB();

            int margin = 22;
                        int barW = BarPixelWidth(today.Count, scale, BarKeyW);
            int barH = BarPixelHeight(scale);
            int rowH = 26 + barH + 20;
            int width = margin * 2 + barW;
            int height = margin + 40 + rowH * 3 + margin;

            Bitmap canvas = NewCanvas(width, height);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                Text(g, "vmsOpenACars · barra principal · " + scale + ":1", margin, margin, TitleColor, 12f, true);
                Text(g, "El cuerpo de la tecla es negro en las tres filas. Lo único que cambia es el color de la leyenda.",
                     margin, margin + 18, NoteColor, 9f, false);

                int y = margin + 40;
                DrawRow(g, margin, y, scale, "HOY (v0.9.44) — botón plano, color de fondo (90 px por tecla)", today, false, LegacyKeyW);
                y += rowH;
                DrawRow(g, margin, y, scale, "PROPUESTA A — los mismos tonos, aclarados (ancho repartido)", a, true, BarKeyW);
                y += rowH;
                DrawRow(g, margin, y, scale, "PROPUESTA B — paleta MCDU pura (blanco · cian · verde · ámbar · rojo)", b, true, BarKeyW);
            }
            return canvas;
        }

        /// <summary>Lamina 2: los cuatro estados de la tecla y los botones con significado.</summary>
        private static Bitmap SheetStates(int scale)
        {
            int margin = 22;
            int keyW = BarKeyW * scale;
            int keyH = KeyH * scale;
            int gap = 14;

            int width = margin * 2 + keyW * 6 + gap * 5;
            int height = margin + 40 + (18 + keyH + 14) + 30 + (18 + keyH + 14) + margin;

            Bitmap canvas = NewCanvas(width, height);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                Text(g, "Estados y significados (cuerpo negro siempre)", margin, margin, TitleColor, 12f, true);
                Text(g, "Normal · puntero encima · pulsada · deshabilitada — y los botones que cambian de color en vuelo.",
                     margin, margin + 18, NoteColor, 9f, false);

                int y = margin + 40;

                McduKeyState[] states =
                {
                    McduKeyState.Normal, McduKeyState.Hover, McduKeyState.Pressed, McduKeyState.Disabled
                };
                string[] stateNames = { "normal", "puntero encima", "pulsada", "deshabilitada" };
                Color amber = Color.FromArgb(255, 168, 32);

                for (int i = 0; i < states.Length; i++)
                {
                    int x = margin + i * (keyW + gap);
                    Text(g, stateNames[i], x, y, NoteColor, 8.5f, false);
                    DrawKey(g, new Rectangle(x, y + 16, keyW, keyH), "INICIAR", amber, scale, states[i]);
                }

                y += 18 + keyH + 30;

                List<KeySpec> live = new List<KeySpec>
                {
                    new KeySpec("INICIAR",  amber),
                    new KeySpec("ABORTAR",  LegendRed),
                    new KeySpec("ENVIAR",   Color.FromArgb(  0, 214, 110)),
                    new KeySpec("DESPACHO", Color.FromArgb(  0, 214, 110)),
                    new KeySpec("CANCELAR", LegendRed),
                    new KeySpec("ENTRAR",    Color.FromArgb( 74, 178, 255)),
                };
                string[] notes =
                {
                    "START", "ABORT", "SEND PIREP", "ruta SimBrief", "salir", "sesión"
                };

                for (int i = 0; i < live.Count; i++)
                {
                    int x = margin + i * (keyW + gap);
                    Text(g, notes[i], x, y, NoteColor, 8.5f, false);
                    DrawKey(g, new Rectangle(x, y + 16, keyW, keyH), live[i].Text, live[i].Color, scale, McduKeyState.Normal);
                }
            }
            return canvas;
        }

        /// <summary>Lamina 3: la barra a tamaño real (1:1), que es donde se juzga si se lee.</summary>
        private static Bitmap SheetRealSize()
        {
            List<KeySpec> today = BarToday();
            List<KeySpec> a = BarVariantA();
            List<KeySpec> b = BarVariantB();

            int margin = 16;
                        int barW = BarPixelWidth(today.Count, 1, BarKeyW);
            int barH = BarPixelHeight(1);
            int rowH = 20 + barH + 14;
            int width = margin * 2 + barW;
            int height = margin + 36 + rowH * 3 + margin;

            Bitmap canvas = NewCanvas(width, height);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                Text(g, "A tamaño real (1:1) — los 1018 px que ocupa la barra repartida en la ventana de 1024", margin, margin, TitleColor, 10f, true);

                int y = margin + 36;
                DrawRow(g, margin, y, 1, "HOY (90 px)", today, false, LegacyKeyW);
                y += rowH;
                DrawRow(g, margin, y, 1, "PROPUESTA A (repartido)", a, true, BarKeyW);
                y += rowH;
                DrawRow(g, margin, y, 1, "PROPUESTA B (repartido)", b, true, BarKeyW);
            }
            return canvas;
        }

        /// <summary>
        /// Lamina 4: la barra **dentro de la ventana** a 1:1 — el final del log encima, la tira de estado
        /// debajo — que es donde se juzga de verdad. Las tres bandas reproducen las filas 3, 4 y 5 de
        /// `MainForm` (mensajes al 100 %, botones 50 px, estado 26 px) con sus colores.
        /// </summary>
        private static Bitmap SheetContext()
        {
            const int width = 1024;
            const int margin = 18;
            const int titleH = 44;
            const int logH = 104;
            const int buttonsH = 50;
            const int statusH = 26;
            const int blockH = logH + buttonsH + statusH + 26;

            int height = margin + titleH + blockH * 2 + margin;
            Bitmap canvas = NewCanvas(width + margin * 2, height);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                Text(g, "La barra en la ventana (1:1, 1024 px de ancho)", margin, margin, TitleColor, 11f, true);
                Text(g, "Arriba, el final del log ACARS; debajo, la tira de estado. A la derecha se ve el hueco que sobra.",
                     margin, margin + 18, NoteColor, 8.5f, false);

                int y = margin + titleH;
                DrawWindowSlice(g, margin, y, width, logH, buttonsH, statusH, BarToday(), false, "HOY");
                y += blockH;
                DrawWindowSlice(g, margin, y, width, logH, buttonsH, statusH, BarVariantA(), true, "PROPUESTA A");
            }
            return canvas;
        }

        private static void DrawWindowSlice(Graphics g, int x, int y, int width, int logH, int buttonsH,
                                            int statusH, List<KeySpec> keys, bool mcdu, string caption)
        {
            Text(g, caption, x, y, TitleColor, 9.5f, true);
            int top = y + 16;

            // Fila 3 — el final del log ACARS (ACARSBackground).
            using (SolidBrush log = new SolidBrush(Color.FromArgb(25, 25, 30)))
                g.FillRectangle(log, new Rectangle(x, top, width, logH));
            string[] lines =
            {
                "ACARS ACTIVE — vmsOpenACars v0.9.44",
                "SKBO -> SKCG   FL350   OFP cargado",
                "NavData: AIRAC 08SEP-05OCT · 613 espacios aereos",
                "Cabina: 7 anuncios listos",
                "Fase: TaxiOut — guia de rodaje a la 14L",
            };
            using (Font font = new Font("Consolas", 9f))
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    using (SolidBrush brush = new SolidBrush(i == 0 ? Color.FromArgb(0, 220, 120) : NoteColor))
                        g.DrawString(lines[i], font, brush, x + 10, top + 8 + i * 18);
                }
            }

            // Fila 4 — la barra de botones (pnlButtons, 50 px).
            Rectangle buttons = new Rectangle(x, top + logH, width, buttonsH);
            using (SolidBrush panel = new SolidBrush(PanelColor))
                g.FillRectangle(panel, buttons);
            using (Bitmap bar = RenderBar(keys, 1, mcdu, mcdu ? BarKeyW : LegacyKeyW))
                g.DrawImageUnscaled(bar, x, top + logH);

            // Fila 5 — la tira de estado (10,12,20) con sus píldoras.
            int statusY = top + logH + buttonsH;
            using (SolidBrush status = new SolidBrush(Color.FromArgb(10, 12, 20)))
                g.FillRectangle(status, new Rectangle(x, statusY, width, statusH));

            int px = x + 3;
            string[] texts = { "SIM READY", "Offline", "---", "ICAO --- GPS ---", "---", "COM ---", "NAV ---" };
            Color[] colors =
            {
                Color.Lime, Color.Orange, Color.DimGray, Color.DimGray, Color.DimGray, Color.DimGray, Color.DimGray
            };

            for (int i = 0; i < texts.Length; i++)
                px += DrawPill(g, px, statusY + 3, texts[i], colors[i]);
        }

        /// <summary>Una píldora de estado como las de `MainForm.CreateStatusPill`. Devuelve el ancho ocupado.</summary>
        private static int DrawPill(Graphics g, int x, int y, string text, Color fore)
        {
            using (Font font = new Font("Consolas", 9f))
            {
                SizeF size = g.MeasureString(text, font);
                int w = (int)Math.Ceiling(size.Width) + 14;
                int h = 20;

                using (SolidBrush background = new SolidBrush(Color.FromArgb(28, 32, 48)))
                    g.FillRectangle(background, new Rectangle(x, y, w, h));
                using (SolidBrush brush = new SolidBrush(fore))
                    g.DrawString(text, font, brush, x + 7, y + 2);

                return w + 6;
            }
        }

        /// <summary>
        /// Lamina 5: los botones de los **demás formularios** a 1:1, con sus rótulos reales —incluidos los
        /// que llevan glifos que Consolas no tiene (`💾`, `🗑`, `📋`, `🔄`, `✕`, `▸`)—. Es la comprobación de
        /// que el enlace de fuentes de GDI los dibuja: con GDI+ salían como cuadros vacíos.
        /// </summary>
        private static Bitmap SheetOtherForms(int scale)
        {
            List<string> titulos = new List<string>();
            List<List<KeyVisual>> filas = new List<List<KeyVisual>>();

            titulos.Add("EcamDialog · aceptar y salir");
            filas.Add(new List<KeyVisual>
            {
                new KeyVisual("ACEPTAR",  McduLegend.Confirm, 90, 35, "Consolas", 10),
                new KeyVisual("CANCELAR", McduLegend.Danger,  90, 35, "Consolas", 10),
                new KeyVisual("SÍ",       McduLegend.Confirm, 90, 35, "Consolas", 10),
                new KeyVisual("NO",       McduLegend.Danger,  90, 35, "Consolas", 10),
                new KeyVisual("✕",        McduLegend.Danger,  30, 25, "Arial",    12),
            });

            titulos.Add("FlightPlanner · los emoji");
            filas.Add(new List<KeyVisual>
            {
                new KeyVisual("📋 SIMBRIEF", McduLegend.Info, 200, 40, "Consolas", 10),
                new KeyVisual("🔄 OBTENER OFP",   McduLegend.Confirm, 140, 40, "Consolas", 10),
                new KeyVisual("🗑 BORRAR RESERVA", McduLegend.Danger, 140, 32, "Consolas", 10),
                new KeyVisual("ACEPTAR",          McduLegend.Confirm, 100, 30, "Consolas", 10),
            });

            titulos.Add("FlareAnalysis · guardar imagen y cerrar");
            filas.Add(new List<KeyVisual>
            {
                new KeyVisual("💾 PNG", McduLegend.Info,   90, 25, "Consolas", 9),
                new KeyVisual("✕",      McduLegend.Danger, 30, 25, "Arial",    12),
            });

            titulos.Add("FlightHistory · barra inferior");
            filas.Add(new List<KeyVisual>
            {
                new KeyVisual("VER ANÁLISIS", McduLegend.Info,    140, 30, "Consolas", 9),
                new KeyVisual("COMPARAR",     McduLegend.Confirm, 140, 30, "Consolas", 9),
                new KeyVisual("BORRAR",       McduLegend.Danger,  140, 30, "Consolas", 9),
                new KeyVisual("CERRAR",       McduLegend.Neutral, 140, 30, "Consolas", 9),
            });

            titulos.Add("Settings · TEST / REFRESH y desplegables");
            filas.Add(new List<KeyVisual>
            {
                new KeyVisual("TEST",    McduLegend.Info,    46, 22, "Consolas", 8),
                new KeyVisual("RECARGAR", McduLegend.Confirm, 66, 22, "Consolas", 8),
                new KeyVisual("...",     McduLegend.Info,    28, 22, "Consolas", 9),
                new KeyVisual("TEST ▾",  McduLegend.Info,    72, 24, "Consolas", 9),
                new KeyVisual("Guardar", McduLegend.Confirm, 110, 30, "Consolas", 10),
            });

            titulos.Add("Mapa y barra lateral · título, zoom, ATC");
            filas.Add(new List<KeyVisual>
            {
                new KeyVisual("✕",     McduLegend.Danger,  30, 25, "Arial",    11),
                new KeyVisual("□",     McduLegend.Neutral, 30, 25, "Arial",    11),
                new KeyVisual("─",     McduLegend.Neutral, 30, 25, "Arial",    11),
                new KeyVisual("+",     McduLegend.Neutral, 32, 23, "Consolas", 13),
                new KeyVisual("−",     McduLegend.Neutral, 32, 23, "Consolas", 13),
                new KeyVisual("ATC ▸", McduLegend.Info,    62, 26, "Consolas", 8),
                new KeyVisual("◀",     McduLegend.Neutral, 20, 26, "Consolas", 7),
            });

            titulos.Add("Principal · cabecera (el engranaje es el caso que destapó el fallo)");
            filas.Add(new List<KeyVisual>
            {
                new KeyVisual("⚙️", McduLegend.Neutral, 40, 40, "Segoe UI", 14),
                new KeyVisual("─",  McduLegend.Neutral, 40, 40, "Consolas", 14),
                new KeyVisual("▾",  McduLegend.Neutral, 40, 40, "Segoe UI", 12),
            });

            int margin = 22;
            int anchoFilas = 660 * scale;
            int altoFila = 26 + 46 * scale + 16;
            int width = margin * 2 + anchoFilas;
            int height = margin + 42 + filas.Count * altoFila + margin;

            Bitmap canvas = NewCanvas(width, height);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                Text(g, "Los botones de los demás formularios · " + scale + ":1", margin, margin, TitleColor, 12f, true);
                Text(g, "Mismos rótulos, tamaños y tipografías que en la aplicación. Los emoji los dibuja GDI: con GDI+ eran cuadros.",
                     margin, margin + 18, NoteColor, 9f, false);

                int y = margin + 42;
                for (int f = 0; f < filas.Count; f++)
                {
                    Text(g, titulos[f], margin, y, NoteColor, 9f, false);

                    int x = margin;
                    foreach (KeyVisual key in filas[f])
                    {
                        Rectangle bounds = new Rectangle(x, y + 16, key.Width * scale, key.Height * scale);
                        DrawKey(g, bounds, key.Text, key.Legend, scale, McduKeyState.Normal, key.Family, key.Size);

                        int anchoTexto;
                        using (Font medida = new Font(key.Family, key.Size))
                            anchoTexto = TextRenderer.MeasureText(key.Text, medida).Width;

                        x += Math.Max(key.Width, anchoTexto + 16) * scale + 10 * scale;
                    }
                    y += altoFila;
                }
            }
            return canvas;
        }

        // ── Dibujo ────────────────────────────────────────────────────────────────────────────────────

                private static void DrawRow(Graphics g, int x, int y, int scale, string caption, List<KeySpec> keys,
                                    bool mcdu, int keyW)
        {
            Text(g, caption, x, y, TitleColor, 9.5f, true);

            int barW = BarPixelWidth(keys.Count, scale, keyW);
            int barH = BarPixelHeight(scale);
            Rectangle bar = new Rectangle(x, y + 20, barW, barH);

            using (SolidBrush panel = new SolidBrush(PanelColor))
                g.FillRectangle(panel, bar);
            using (Pen border = new Pen(Color.FromArgb(70, 84, 98), 1f))
                g.DrawRectangle(border, bar);

            using (Bitmap strip = RenderBar(keys, scale, mcdu, keyW))
                g.DrawImageUnscaled(strip, x, y + 20);
        }

        private static void DrawKey(Graphics g, Rectangle bounds, string text, Color legend, int scale,
                                    McduKeyState state, string family = "Consolas", float size = 10f)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush background = new SolidBrush(PanelColor))
                g.FillRectangle(background, bounds);
            using (Pen border = new Pen(Color.FromArgb(70, 84, 98), 1f))
                g.DrawRectangle(border, bounds);

            using (Font font = new Font(family, size * scale, FontStyle.Bold))
                McduKeyRenderer.Paint(g, bounds, text, legend, font, state);
        }

                private static int BarPixelWidth(int count, int scale, int keyW)
        {
            return (Pad + count * keyW + (count - 1) * Gap + Pad) * scale;
        }

        private static int BarPixelHeight(int scale)
        {
            return (Pad + KeyH + Pad) * scale;
        }

        /// <summary>
        /// Pinta la barra con **controles de verdad** en un formulario fuera de pantalla y la captura con
        /// `DrawToBitmap`: así lo que se enseña es exactamente lo que pintaría la aplicación.
        /// </summary>
                private static Bitmap RenderBar(List<KeySpec> keys, int scale, bool mcdu, int keyW)
        {
            int width = BarPixelWidth(keys.Count, scale, keyW);
            int height = BarPixelHeight(scale);

            using (Form form = new Form())
            {
                form.FormBorderStyle = FormBorderStyle.None;
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-5000, -5000);
                form.BackColor = PanelColor;
                form.ClientSize = new Size(width, height);

                int x = Pad * scale;
                foreach (KeySpec spec in keys)
                {
                    Control control;
                    if (mcdu)
                    {
                        McduKeyButton key = new McduKeyButton();
                        key.LegendColor = spec.Color;
                        control = key;
                    }
                    else
                    {
                        Button button = new Button();
                        button.BackColor = spec.Color;
                        button.ForeColor = Color.White;
                        button.FlatStyle = FlatStyle.Flat;   // como en `MainForm.InitializeButtons`
                        control = button;
                    }

                    control.Text = spec.Text;
                    control.Font = new Font("Consolas", 10f * scale, FontStyle.Bold);
                    control.Location = new Point(x, Pad * scale);
                    control.Size = new Size(keyW * scale, KeyH * scale);
                    control.Enabled = spec.Enabled;
                    form.Controls.Add(control);

                    x += (keyW + Gap) * scale;
                }

                form.Show();
                form.Refresh();
                Application.DoEvents();

                Bitmap bitmap = new Bitmap(width, height);
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));

                form.Hide();
                return bitmap;
            }
        }

        private static Bitmap NewCanvas(int width, int height)
        {
            Bitmap canvas = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(canvas))
            using (SolidBrush background = new SolidBrush(CanvasColor))
                g.FillRectangle(background, new Rectangle(0, 0, width, height));
            return canvas;
        }

        private static void Text(Graphics g, string text, int x, int y, Color color, float size, bool bold)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Font font = new Font("Consolas", size, bold ? FontStyle.Bold : FontStyle.Regular))
            using (SolidBrush brush = new SolidBrush(color))
                g.DrawString(text, font, brush, x, y);
        }
    }
}
