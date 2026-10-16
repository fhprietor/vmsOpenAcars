using System;
using System.Drawing;
using System.Windows.Forms;
using vmsOpenAcars.Controls;
using vmsOpenAcars.UI;

namespace McduPreview
{
    /// <summary>
    /// La ventana con la piel del MCDU, montada con **los controles de verdad** de la aplicación
    /// (`McduChassisPanel`, `McduDisplayPanel`, `McduChassis` y `McduKeyButton`), en un formulario
    /// fuera de pantalla que se captura con `DrawToBitmap`.
    ///
    /// No es un dibujo aparte: es el mismo código que corre en `MainForm`. Si aquí se ve bien, en la
    /// ventana se ve igual; y si el reparto del `Padding` de una pantalla está mal, se ve aquí.
    /// </summary>
    internal static class AppSkin
    {
        private const int Ancho = 1024;
        private const int Alto = 768;
        private const int BarKeyW = 123;

        internal static Bitmap Render()
        {
            using (Form form = new Form())
            {
                form.FormBorderStyle = FormBorderStyle.None;
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-6000, -6000);
                form.ClientSize = new Size(Ancho, Alto);
                form.BackColor = McduPalette.ChassisMid;
                // Igual que `MainForm.OnPaintBackground`: el fondo de la ventana es la carcasa.
                form.Paint += (s, e) => McduChassis.Paint(e.Graphics, form.ClientRectangle, 0, Alto);

                int y = 0;
                Header(form, y, 60); y += 60;
                y += 4;
                Screen(form, new Rectangle(14, y, Ancho - 28, 216), "FLIGHT INFORMATION", "AIRAC 08SEP-05OCT",
                       ContenidoInfo(), 4);
                y += 224;
                Screen(form, new Rectangle(14, y, Ancho - 28, 208), "ACARS LOG", "TAXI OUT",
                       ContenidoLog(), 9);
                y += 216;
                int mitad = (Ancho - 28 - 10) / 2;
                Screen(form, new Rectangle(14, y, mitad, 150), "INCOMING MSG", "3 NUEVOS", ContenidoIncoming(), 4);
                Screen(form, new Rectangle(14 + mitad + 10, y, Ancho - 28 - mitad - 10, 150), "METAR SKCG", "0845Z",
                       ContenidoMetar(), 5);
                y += 158;
                Status(form, y, 26); y += 30;
                Bar(form, y, Alto - y);

                form.Show();
                form.Refresh();
                Application.DoEvents();

                Bitmap bmp = new Bitmap(Ancho, Alto);
                form.DrawToBitmap(bmp, new Rectangle(0, 0, Ancho, Alto));
                form.Hide();
                return bmp;
            }
        }

        // ── Piezas ───────────────────────────────────────────────────────────────────────────────────

        private static void Header(Form form, int y, int alto)
        {
            McduChassisPanel pnl = new McduChassisPanel();
            pnl.SetBounds(0, y, Ancho, alto);
            form.Controls.Add(pnl);

            pnl.Controls.Add(Etiqueta("vmsOpenACars v0.9.45", 44, 12, 10f, FontStyle.Bold, McduPalette.Ink,
                                      McduPalette.ChassisHigh));
            pnl.Controls.Add(Etiqueta("SKBO -> SKCG   ·   FL350   ·   ACARS ACTIVO", 262, 14, 8.5f,
                                      FontStyle.Regular, McduPalette.Ink, McduPalette.ChassisHigh));

            int bx = Ancho - 146;
            foreach (string cap in new string[] { "\u2699", "\u2500", "\u25BE" })
            {
                McduKeyButton k = new McduKeyButton();
                k.Text = cap;
                k.Font = new Font("Segoe UI", 12f);
                k.LegendColor = McduLegend.Neutral;
                k.SetBounds(bx, 10, 40, 40);
                pnl.Controls.Add(k);
                bx += 44;
            }
        }

        /// <summary>Una pantalla de verdad, con sus rótulos dentro (que es donde se prueba el `Padding`).</summary>
        private static void Screen(Form form, Rectangle marco, string titulo, string anotacion,
                                   string[][] lineas, int filas)
        {
            McduDisplayPanel pnl = new McduDisplayPanel();
            pnl.Title = titulo;
            pnl.Annotation = anotacion;
            pnl.SetBounds(marco.X, marco.Y, marco.Width, marco.Height);
            form.Controls.Add(pnl);

            // El contenido va en coordenadas del formulario para que no dependa del `Padding`: aquí lo que
            // se comprueba es que el cristal y el título del control son los que se ven.
            int x = marco.X + 12;
            int y = marco.Y + 30;
            for (int i = 0; i < filas && i < lineas.Length; i++)
            {
                string[] campos = lineas[i];
                int cx = x;
                for (int k = 0; k < campos.Length; k++)
                {
                    string texto = campos[k];
                    bool esEtiqueta = k % 2 == 0 && campos.Length > 1;
                    Color color = campos.Length == 1
                        ? McduPalette.LcdGreenSoft
                        : (esEtiqueta ? McduPalette.LcdCyan : McduPalette.LcdGreen);
                    form.Controls.Add(Etiqueta(texto, cx, y, 9f, FontStyle.Regular, color));
                    cx += esEtiqueta ? 62 : 0;
                    if (!esEtiqueta) cx += 150;
                }
                y += 18;
            }
        }

        private static void Status(Form form, int y, int alto)
        {
            McduChassisPanel pnl = new McduChassisPanel();
            pnl.SetBounds(0, y, Ancho, alto);
            form.Controls.Add(pnl);

            string[] textos = { "SIM READY", "Online", "---", "ICAO ---", "GPS ---", "COM ---", "NAV ---" };
            Color[] colores =
            {
                McduPalette.LcdGreenSoft, McduPalette.LcdGreenSoft, McduPalette.LcdDim,
                McduPalette.LcdDim, McduPalette.LcdDim, McduPalette.LcdDim, McduPalette.LcdDim
            };

            int x = 14;
            for (int i = 0; i < textos.Length; i++)
            {
                Label pill = new Label();
                pill.Text = textos[i];
                pill.Font = new Font("Consolas", 9f);
                pill.ForeColor = colores[i];
                pill.BackColor = McduPalette.LcdBackground;
                pill.AutoSize = true;
                pill.Padding = new Padding(7, 2, 7, 2);
                pill.Location = new Point(x, y + 3);
                form.Controls.Add(pill);
                x += 18 + textos[i].Length * 7;
            }
        }

        /// <summary>La barra: plástico, las rayitas del bisel y las ocho teclas repartidas.</summary>
        private static void Bar(Form form, int y, int alto)
        {
            McduChassisPanel pnl = new McduChassisPanel();
            pnl.SetBounds(0, y, Ancho, alto);
            form.Controls.Add(pnl);

            string[] textos = { "ENTRAR", "OFP", "MAPA", "METEO", "DESPACHO", "BITÁCORA", "INICIAR", "SALIR" };
            Color[] leyendas =
            {
                McduLegend.Info, McduLegend.Info, McduLegend.Neutral, McduLegend.Neutral,
                McduLegend.Confirm, McduLegend.Steel, McduLegend.Warning, McduLegend.Danger
            };

            int ancho = (Ancho - 6 - 7 * 4) / 8;
            int x = 6;
            for (int i = 0; i < textos.Length; i++)
            {
                McduKeyButton k = new McduKeyButton();
                k.Text = textos[i];
                k.Font = new Font("Consolas", 10f, FontStyle.Bold);
                k.LegendColor = leyendas[i];
                k.SetBounds(x, 14, ancho, 40);   // relativo al panel, que ya está colocado
                pnl.Controls.Add(k);

                // La rayita del bisel, como en el aparato: la pinta el panel y la tecla va encima.
                int rx = x + 12;
                pnl.Paint += (s, e) => McduChassis.Dash(e.Graphics, rx, 9, 22);

                x += ancho + 4;
            }
        }

        private static Label Etiqueta(string texto, int x, int y, float cuerpo, FontStyle estilo, Color color)
        {
            return Etiqueta(texto, x, y, cuerpo, estilo, color, McduPalette.LcdBackground);
        }

        /// <summary>
        /// El fondo va SÓLIDO y del color de la superficie: un `Label` transparente sobre una superficie
        /// pintada a mano sale con basura detrás, y eso es exactamente lo que destapó la primera versión de
        /// esta lámina.
        /// </summary>
        private static Label Etiqueta(string texto, int x, int y, float cuerpo, FontStyle estilo, Color color,
                                      Color fondo)
        {
            Label l = new Label();
            l.Text = texto;
            l.Font = new Font("Consolas", cuerpo, estilo);
            l.ForeColor = color;
            l.BackColor = fondo;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            return l;
        }

        // ── El contenido, como el de la ventana ──────────────────────────────────────────────────────

        private static string[][] ContenidoInfo()
        {
            return new string[][]
            {
                new string[] { "PROGRESO", "", "AERODINAMICA", "", "COMBUSTIBLE", "", "ALTITUD", "" },
                new string[] { "DIST", "1224/1516 NM", "IAS", "281 kt", "INI", "12400 kg", "ALT", "FL350" },
                new string[] { "REST", "292 NM / 00:41", "GS", "472 kt", "ACT", "9820 kg", "AGL", "33200 ft" },
                new string[] { "T/A", "01:42:11", "VS", "-1200 fpm", "USO", "2580 kg", "QNH", "1013" },
            };
        }

        private static string[][] ContenidoLog()
        {
            return new string[][]
            {
                new string[] { "ACARS ACTIVE — vmsOpenACars v0.9.45" },
                new string[] { "SKBO -> SKCG   FL350   OFP cargado" },
                new string[] { "NavData: AIRAC 08SEP-05OCT   613 espacios aereos" },
                new string[] { "Cabina: 7 anuncios listos" },
                new string[] { "Fase: TaxiOut — guia de rodaje a la 14L" },
                new string[] { "RAAS: APROXIMANDO PISTA 14L   a 150 m" },
                new string[] { "PENALTY  LANDING LT OFF  -5 PTS" },
                new string[] { "PUSHBACK: fin del empuje en 4.69788,-74.13721" },
                new string[] { "Posicion enviada: 1224 NM restantes   ETA 14:22Z" },
            };
        }

        private static string[][] ContenidoIncoming()
        {
            return new string[][]
            {
                new string[] { "DE: DESPACHO", "", "<MENSAJE>", "" },
                new string[] { "EL VUELO SKCG LLEVA 20 MIN DE RETRASO" },
                new string[] { "EN LA SALIDA. AJUSTAR EL BLOCK OFF." },
                new string[] { "2 MENSAJES MAS EN LA COLA" },
            };
        }

        private static string[][] ContenidoMetar()
        {
            return new string[][]
            {
                new string[] { "SKCG 150845Z 02008KT 9999 SCT018" },
                new string[] { "28/25 Q1010 NOSIG" },
                new string[] { "VIENTO", "020/08 kt" },
                new string[] { "CRUZADO", "3 kt izq" },
                new string[] { "RAFAGAS", "12 kt" },
            };
        }
    }
}
