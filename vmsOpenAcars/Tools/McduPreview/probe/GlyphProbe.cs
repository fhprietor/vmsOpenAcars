using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace McduProbe
{
    /// <summary>
    /// Sonda: ¿quién dibuja los glifos que usan los botones de la aplicación? La leyenda de la tecla se
    /// pinta con GDI+ (`DrawString`) en la propuesta; GDI+ **no hace enlace de fuentes**, así que un
    /// carácter que Consolas no tenga sale como un cuadro vacío. `TextRenderer` (GDI) sí lo hace.
    ///
    /// Compara las dos rutas con los glifos reales de los botones para decidir con la imagen delante y no
    /// de memoria. Escribe `glifos-gdi+vs-gdi.png`.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string[] glyphs = { "⚙️", "─", "▾", "▸", "✕", "💾", "🗑", "◀", "A" };

            int width = 620;
            int rowH = 40;
            int height = 60 + rowH * 3 + 20;

            using (Bitmap canvas = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    g.Clear(Color.FromArgb(14, 16, 20));
                    Text(g, "Consolas 12 Bold — misma cadena por las dos rutas", 12, 10, Color.White);

                    Text(g, "GDI+ DrawString   :", 12, 44, Color.FromArgb(180, 190, 200));
                    Text(g, "TextRenderer GDI  :", 12, 44 + rowH, Color.FromArgb(180, 190, 200));
                    Text(g, "GDI+ sobre el capuchón negro (la ruta de la propuesta):", 12, 44 + rowH * 2, Color.FromArgb(180, 190, 200));

                    using (Font font = new Font("Consolas", 12f, FontStyle.Bold))
                    {
                        int x = 150;
                        foreach (string glyph in glyphs)
                        {
                            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                            g.DrawString(glyph, font, Brushes.White, x, 44);

                            TextRenderer.DrawText(g, glyph, font, new Point(x, 44 + rowH), Color.White,
                                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                            x += 46;
                        }
                    }

                    // La ruta real de la propuesta, con el halo, sobre una tecla.
                    using (Font font = new Font("Consolas", 10f, FontStyle.Bold))
                    {
                        int x = 12;
                        foreach (string glyph in glyphs)
                        {
                            Rectangle key = new Rectangle(x, 44 + rowH * 2 + 14, 42, 26);
                            using (SolidBrush body = new SolidBrush(Color.FromArgb(26, 26, 30)))
                                g.FillRectangle(body, key);
                            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                            g.DrawString(glyph, font, Brushes.Orange, key.X + 12, key.Y + 5);
                            x += 48;
                        }
                    }
                }

                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "glifos-gdi+vs-gdi.png");
                canvas.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("escrito " + path);
            }
            return 0;
        }

        private static void Text(Graphics g, string text, int x, int y, Color color)
        {
            using (Font font = new Font("Consolas", 9f))
                g.DrawString(text, font, new SolidBrush(color), x, y);
        }
    }
}
