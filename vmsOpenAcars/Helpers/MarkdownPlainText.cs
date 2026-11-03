using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Las notas de la Release, en texto plano para un diálogo.**
    ///
    /// El cuerpo de la Release de GitHub es el `.md` de `Docs/NOTAS-PILOTOS-&lt;versión&gt;.md` tal cual, y
    /// hasta v0.9.59 se interpolaba sin tratar en el aviso de «Actualización disponible». El diálogo
    /// enseñaba, literalmente:
    ///
    /// <code>
    /// Novedades:
    /// # Novedades de vmsOpenACARS — versión 0.9.59
    ///
    /// **En una frase:** la actualización automática deja de fallar en
    /// </code>
    ///
    /// —las almohadillas, los asteriscos y los acentos graves—. Un `MessageBox` no interpreta Markdown
    /// y no hay control enriquecido en un aviso que solo tiene que decir «hay versión nueva»: el
    /// arreglo es aplanar el texto, no meter un visor.
    ///
    /// **Lo que se descarta y por qué**: la URL de un enlace no aporta nada en un aviso que ya lleva su
    /// botón de descarga y, en cambio, ocupa el ancho del diálogo con una dirección interna. Las líneas
    /// de separación (`---`) solo tenían sentido en el `.md`. Las viñetas se conservan —son lo que hace
    /// legible una lista de novedades—, con `•` en vez de `-` porque el guion se lee como una raya.
    ///
    /// **El recorte es parte de la regla, no del que llama**: las notas completas de la 0.9.59 miden
    /// **1.520 caracteres** y en un `MessageBox` eso es un muro de texto. Se corta en el último espacio que quepa, de
    /// modo que nunca queda una palabra a medias, y se marca con `…` —quien quiera el detalle completo
    /// tiene el enlace de la Release—.
    ///
    /// **Degrada sin datos**: `null` o vacío devuelve cadena vacía y ningún camino lanza. Una Release
    /// sin cuerpo no puede tumbar el aviso de actualización, que es justo lo que el piloto necesita ver.
    /// </summary>
    internal static class MarkdownPlainText
    {
        /// <summary>Marca de recorte; un solo carácter, para no gastar dos del presupuesto.</summary>
        internal const string Ellipsis = "…";

        // `#`, `##`... al principio de línea: el texto del título se queda, la almohadilla no.
        private static readonly Regex HeadingMarker =
            new Regex(@"^\s{0,3}#{1,6}\s*", RegexOptions.Compiled);

        // Línea de separación: `---`, `***`, `___` (y `===`, que también cierra secciones en Markdown).
        private static readonly Regex HorizontalRule =
            new Regex(@"^\s{0,3}(?:-[ \t]*){3,}$|^\s{0,3}(?:\*[ \t]*){3,}$|^\s{0,3}(?:_[ \t]*){3,}$|^\s{0,3}(?:=[ \t]*){3,}$",
                      RegexOptions.Compiled);

        // Viñeta de lista al principio de línea. No se guarda la indentación: el `.md` sangra las
        // líneas de continuación de una lista y en el diálogo ese margen se ve como un diente roto.
        private static readonly Regex Bullet =
            new Regex(@"^[-*+][ \t]+", RegexOptions.Compiled);

        // `[texto](url)` → `texto`.
        private static readonly Regex Link =
            new Regex(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);

        private static readonly Regex BoldAsterisk =
            new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

        private static readonly Regex BoldUnderscore =
            new Regex(@"__(.+?)__", RegexOptions.Compiled);

        private static readonly Regex ItalicAsterisk =
            new Regex(@"\*(.+?)\*", RegexOptions.Compiled);

        // El `_` solo cuenta como énfasis si no está pegado a letras o números: así un `snake_case`
        // del texto no se convierte en `snakecase` sin querer.
        private static readonly Regex ItalicUnderscore =
            new Regex(@"(?<![A-Za-z0-9])_(.+?)_(?![A-Za-z0-9])", RegexOptions.Compiled);

        // Tres o más saltos seguidos → dos (un párrafo en blanco), la separación que cabe en un aviso.
        private static readonly Regex BlankRun =
            new Regex(@"\n{3,}", RegexOptions.Compiled);

        /// <summary>
        /// Convierte el Markdown de las notas en texto plano y lo recorta a <paramref name="maxChars"/>
        /// cuando hace falta. `maxChars &lt;= 0` no recorta.
        /// </summary>
        internal static string ToPlainText(string markdown, int maxChars)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            string normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            var blocks = new List<string>(lines.Length);

            // `canJoin` dice si la línea anterior era texto de un párrafo (o de una viñeta) y, por
            // tanto, la actual es su continuación. Las notas del piloto vienen cortadas a ~90 columnas
            // y el diálogo es más estrecho: unir las líneas de un mismo párrafo es lo que evita que el
            // aviso enseñe una columna con saltos a mitad de frase, y de paso deja que el énfasis que
            // cruza de una línea a otra (`**siempre con el` + `mismo nombre**`) se quite entero.
            bool canJoin = false;

            foreach (string raw in lines)
            {
                string line = raw.Trim();

                // La separación se decide antes que la viñeta: `- - -` es una línea de separación,
                // no una lista. Y una separación solo aporta el párrafo en blanco que la rodea.
                if (line.Length == 0 || HorizontalRule.IsMatch(line))
                {
                    blocks.Add(string.Empty);
                    canJoin = false;
                    continue;
                }

                bool isHeading = HeadingMarker.IsMatch(line);
                if (isHeading)
                {
                    line = HeadingMarker.Replace(line, string.Empty);
                }
                else
                {
                    bool isBullet = Bullet.IsMatch(line);
                    line = Bullet.Replace(line, "• ");

                    // Una viñeta siempre empieza bloque; su texto de continuación, en cambio, se une.
                    if (canJoin && !isBullet)
                    {
                        blocks[blocks.Count - 1] = blocks[blocks.Count - 1] + " " + line;
                        continue;
                    }
                }

                if (line.Length > 0)
                    blocks.Add(line);
                canJoin = !isHeading;
            }

            string text = string.Join("\n", blocks);

            // Los enlaces van primero: el texto del enlace puede llevar énfasis dentro.
            text = Link.Replace(text, "$1");
            text = BoldAsterisk.Replace(text, "$1");
            text = BoldUnderscore.Replace(text, "$1");
            text = ItalicAsterisk.Replace(text, "$1");
            text = ItalicUnderscore.Replace(text, "$1");

            // El código en línea se queda con su texto: el acento grave solo era la marca.
            text = text.Replace("`", string.Empty);
            text = BlankRun.Replace(text, "\n\n").Trim();

            if (maxChars > 0 && text.Length > maxChars)
                text = Truncate(text, maxChars);

            return text;
        }

        /// <summary>
        /// Corta en el último espacio que quepa para no partir una palabra, y añade `…`. Solo un texto
        /// sin ningún espacio en el presupuesto se corta a la fuerza: no hay frontera que respetar.
        /// </summary>
        private static string Truncate(string text, int maxChars)
        {
            int cut = Math.Min(maxChars, text.Length);
            int lastSpace = text.LastIndexOf(' ', cut - 1);
            if (lastSpace > 0)
                cut = lastSpace;

            return text.Substring(0, cut).TrimEnd() + Ellipsis;
        }
    }
}
