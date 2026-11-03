using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Las notas de la Release convertidas en texto plano para el aviso de actualización.**
    ///
    /// El cuerpo de la Release de GitHub es el `.md` de `Docs/NOTAS-PILOTOS-&lt;versión&gt;.md` tal cual, y el
    /// aviso de «Actualización disponible» lo interpolaba sin tratar: el mantenedor vio en pantalla el
    /// `# Novedades de vmsOpenACARS — versión 0.9.59` y el `**En una frase:**` con sus marcas. Un
    /// `MessageBox` no interpreta Markdown, así que la regla es aplanar el texto, no añadir un visor.
    ///
    /// El caso central es <see cref="NotasRealesDeLa0959_NoDejanMarcasDeMarkdown"/>: el texto literal de
    /// las notas que provocaron el defecto. Los demás fijan una regla por test, para que el porqué de
    /// cada transformación quede escrito al lado de lo que decide. No se abre ninguna ventana.
    /// </summary>
    [TestClass]
    public class MarkdownPlainTextTests
    {
        // Copia literal de Docs/NOTAS-PILOTOS-0.9.59.md (la Release que se pintó en crudo en el aviso).
        private const string NotasReales0959 =
@"# Novedades de vmsOpenACARS — versión 0.9.59

**En una frase:** la actualización automática deja de fallar en el segundo intento y, si algo sale
mal, el aviso te dice **qué hacer** en vez de enseñarte un error en inglés con una ruta interna.

---

## Lo que hay que saber

- **Qué fallaba.** La actualización descomprimía el paquete en una carpeta temporal **siempre con el
  mismo nombre**, y al descomprimir no se permitía sobrescribir. Si quedaba algo de un intento
  anterior —una descarga que se cortó, un reintento—, la segunda vez fallaba **siempre**, con el
  mensaje `The file 'C:\Temp\vmsOpenAcars_update\pdfium.dll' already exists`.
- **Qué cambia.** Cada intento usa **su propia carpeta** y la borra al terminar, así que ya no hay
  nada de antes que pueda estorbar. No hay que borrar nada a mano ni limpiar `%TEMP%`: la carpeta
  vieja que te haya quedado se puede ignorar.
- **Si la actualización vuelve a fallar**, el aviso ahora lo dice en tu idioma: **cierra la
  aplicación y vuelve a intentarlo**; si se repite, cierra también cualquier ventana de actualización
  que se haya quedado abierta (un archivo en uso impide reemplazarlo). Como plan B, el mismo aviso
  trae el enlace para descargar la versión nueva a mano. Debajo, entre paréntesis, queda el detalle
  técnico del error: eso es para soporte, no hace falta que lo entiendas.
- **Nada que configurar.** No cambian ni el nombre del archivo que se descarga ni la dirección de
  descarga; solo cómo se prepara lo descargado antes de instalarlo.";

        [TestMethod]
        public void Titulo_QuitaLaAlmohadillaYDejaElTexto()
        {
            // El `#` no se pinta: el texto del título sí, porque es lo que dice de qué va la versión.
            Assert.AreEqual("Novedades de vmsOpenACARS — versión 0.9.59",
                MarkdownPlainText.ToPlainText("# Novedades de vmsOpenACARS — versión 0.9.59", 0));
            Assert.AreEqual("Lo que hay que saber",
                MarkdownPlainText.ToPlainText("## Lo que hay que saber", 0));
        }

        [TestMethod]
        public void NegritaYCursiva_SeQuedanSinMarcas()
        {
            // Los cuatro pares que aparecen en las notas: `**`, `__`, `*` y `_`.
            Assert.AreEqual("Qué fallaba. Esto es importante y esto y aquello.",
                MarkdownPlainText.ToPlainText(
                    "**Qué fallaba.** Esto es *importante* y __esto__ y _aquello_.", 0));
        }

        [TestMethod]
        public void CodigoEnLinea_QuitaLosAcentosGraves()
        {
            // El mensaje de error entre acentos graves es el que se veía con las comillas invertidas.
            Assert.AreEqual("con el mensaje The file 'x' already exists",
                MarkdownPlainText.ToPlainText("con el mensaje `The file 'x' already exists`", 0));
        }

        [TestMethod]
        public void Enlace_ConservaSoloElTexto()
        {
            // La URL de un enlace solo estorba en un diálogo: la descarga ya tiene su botón.
            Assert.AreEqual("Descarga desde la página de releases ahora.",
                MarkdownPlainText.ToPlainText(
                    "Descarga desde [la página de releases](https://github.com/x/y/releases) ahora.", 0));
        }

        [TestMethod]
        public void LineaDeSeparacion_DesapareceYNoDejaHuecosDeMas()
        {
            // El `---` del `.md` no aporta nada en texto plano; al quitarlo no pueden quedar tres saltos.
            Assert.AreEqual("Antes\n\nDespués",
                MarkdownPlainText.ToPlainText("Antes\n\n---\n\nDespués", 0));
            Assert.AreEqual("Antes\n\nDespués",
                MarkdownPlainText.ToPlainText("Antes\n\n***\n\nDespués", 0));
        }

        [TestMethod]
        public void Vineta_SeConvierteEnPuntoVolado()
        {
            // `-` y `*` al principio de línea son la misma lista; el guion se lee como una raya.
            Assert.AreEqual("• Qué cambia. Algo de la versión\n• Otra cosa",
                MarkdownPlainText.ToPlainText("- **Qué cambia.** Algo de la versión\n* Otra cosa", 0));
        }

        [TestMethod]
        public void SaltosRepetidos_SeColapsanYSeRecortanLosExtremos()
        {
            // Tres o más saltos son un párrafo en blanco; dos bastan para separar.
            Assert.AreEqual("Uno\n\nDos",
                MarkdownPlainText.ToPlainText("Uno\n\n\n\nDos", 0));
            Assert.AreEqual("Uno",
                MarkdownPlainText.ToPlainText("\n\n   Uno   \n\n", 0));
        }

        [TestMethod]
        public void SinNotas_DevuelveVacioSinLanzar()
        {
            // Una Release sin cuerpo no puede tumbar el aviso de actualización.
            Assert.AreEqual(string.Empty, MarkdownPlainText.ToPlainText(null, 600));
            Assert.AreEqual(string.Empty, MarkdownPlainText.ToPlainText(string.Empty, 600));
            Assert.AreEqual(string.Empty, MarkdownPlainText.ToPlainText("   ", 600));
        }

        [TestMethod]
        public void TextoQueCabe_NoSeRecortaNiAcabaEnPuntosSuspensivos()
        {
            // Con presupuesto de sobra el texto sale completo: el aviso no debe insinuar que falta algo.
            const string source = "La actualización usa una carpeta nueva por intento.";
            string result = MarkdownPlainText.ToPlainText(source, 600);

            Assert.AreEqual(source, result);
            Assert.IsFalse(result.EndsWith(MarkdownPlainText.Ellipsis, StringComparison.Ordinal));
        }

        [TestMethod]
        public void TextoQueNoCabe_AcabaEnPuntosSuspensivosSinCortarPalabra()
        {
            // 30 caracteres caen dentro de «descomprime»; el recorte tiene que retroceder al espacio
            // anterior, no dejar «descomp…».
            const string source = "La actualización descomprime el paquete en una carpeta temporal nueva.";
            string result = MarkdownPlainText.ToPlainText(source, 30);

            Assert.IsTrue(result.EndsWith(MarkdownPlainText.Ellipsis, StringComparison.Ordinal));

            string prefix = result.Substring(0, result.Length - MarkdownPlainText.Ellipsis.Length);
            Assert.IsTrue(source.StartsWith(prefix, StringComparison.Ordinal),
                $"«{prefix}» no es el principio del original");
            // La palabra siguiente en el original empieza tras un espacio: no se cortó a mitad.
            Assert.AreEqual(' ', source[prefix.Length],
                $"el recorte partió la palabra en «{prefix}»");
        }

        [TestMethod]
        public void NotasRealesDeLa0959_NoDejanMarcasDeMarkdown()
        {
            // El caso que vio el mantenedor: el texto literal de la 0.9.59. Sin límite, para revisar
            // todo el documento; con 600, el recorte que usa el diálogo.
            string plain = MarkdownPlainText.ToPlainText(NotasReales0959, 0);

            Assert.IsFalse(plain.Contains("**"), "quedaron asteriscos de negrita");
            Assert.IsFalse(plain.Contains("#"), "quedaron almohadillas de título");
            Assert.IsFalse(plain.Contains("]("), "quedó un enlace en Markdown");
            Assert.IsFalse(plain.Contains("`"), "quedaron acentos graves");
            Assert.IsTrue(plain.Contains("Novedades de vmsOpenACARS — versión 0.9.59"));
            Assert.IsTrue(plain.Contains("• Qué fallaba."), "la viñeta no se convirtió en «•»");

            string recortado = MarkdownPlainText.ToPlainText(NotasReales0959, 600);
            Assert.IsTrue(recortado.EndsWith(MarkdownPlainText.Ellipsis, StringComparison.Ordinal));
            Assert.IsTrue(recortado.Length <= 600, $"el recorte midió {recortado.Length} caracteres");
            Assert.IsFalse(recortado.Contains("**"));
        }
    }
}
