using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Windows.Forms;
using vmsOpenAcars.Controls;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Los rótulos que el piloto lee salen de `Languages/{es,en}.json`, no del código.**
    ///
    /// El caso que obliga a esta prueba: en v0.9.45 se cambiaron los 34 botones de la aplicación a teclas
    /// MCDU y, al revisarlos, resultó que buena parte de ellos llevaba el rótulo **escrito a mano** en
    /// inglés —`SELECT`, `CLOSE`, `ACCEPT`, `VIEW ANALYSIS`, `FOLLOW`, `TEST`, `REFRESH`— mientras los dos
    /// ficheros de idioma tenían **128 claves definidas y sin usar**, algunas escritas justo para esos
    /// botones (`Fp_BtnPlanSimbrief`, `Fp_BtnFetchOfp`, `Fp_BtnAccept`). No faltaban traducciones: faltaba
    /// **cablearlas**.
    ///
    /// La regla que se comprueba, y que es la que evita que vuelva a pasar: en el inicializador de un
    /// botón, una casilla o un botón de opción, `Text` es **o un símbolo** (`✕`, `─`, `▾`, `...`, `+`) **o
    /// una llamada a `_("Clave")`**. Un literal con letras ASCII es un rótulo que nadie va a traducir.
    ///
    /// No entra en esto —y por eso no lo vigila— lo que no es un control con `Text` propio: las píldoras
    /// de estado, los rótulos de sección, las cabeceras de rejilla y los mensajes de progreso, que se
    /// asignan a `.Text` de un control ya creado. Esos son otro bloque, listado en `Docs/CHANGELOG.md`.
    /// </summary>
    [TestClass]
    public class ButtonLabelsTests
    {
        /// <summary>El arranque de un control con rótulo propio.</summary>
        private static readonly Regex ControlStart =
            new Regex(@"new\s+(?:McduKeyButton|Button|CheckBox|RadioButton)\b", RegexOptions.Compiled);

        /// <summary>Los rótulos que se pasan por parámetro a una fábrica de botones.</summary>
        private static readonly Regex FactoryCall =
            new Regex(@"(?:CreateButton|MakeBtn|MakeTitleBtn|MakeLayerChk|MakeZoomBtn)\s*\(\s*""([^""]*)""",
                      RegexOptions.Compiled);

        /// <summary>`Text = "..."`, y **no** `HeaderText = "..."`: el `\b` de delante lo impide.</summary>
        private static readonly Regex LiteralLabel =
            new Regex(@"(?<![\w])Text\s*=\s*""([^""]*)""", RegexOptions.Compiled);

        /// <summary>Un uso del localizador: `_("Clave")`.</summary>
        private static readonly Regex KeyUse =
            new Regex(@"(?<![\w])_\(\s*""([^""]+)""\s*\)", RegexOptions.Compiled);

        [TestMethod]
        public void NingunBotonNiCasillaLlevaElRotuloEscritoAMano()
        {
            string app = FindAppProject();
            if (app == null) Assert.Inconclusive("no se encontró el proyecto vmsOpenAcars desde la carpeta de tests");

            var offenders = new List<string>();
            string ui = Path.Combine(app, "UI");

            foreach (string file in Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories))
                Scan(app, file, offenders);

            Assert.AreEqual(0, offenders.Count,
                "estos rótulos están escritos a mano y no se traducen (el piloto los lee en inglés):\n  " +
                string.Join("\n  ", offenders) +
                "\n\nSe arreglan con _(\"Clave\") y la clave en Languages/es.json y Languages/en.json.");
        }

        /// <summary>
        /// El otro lado de la regla: un rótulo de letras que se pasa por parámetro a una fábrica también
        /// tiene que venir traducido. Se comprueba aparte para que el mensaje diga **cuál** de las dos
        /// formas se ha usado mal.
        /// </summary>
        [TestMethod]
        public void LasFabricasDeBotonesTampocoRecibenElRotuloEscritoAMano()
        {
            string app = FindAppProject();
            if (app == null) Assert.Inconclusive("no se encontró el proyecto vmsOpenAcars desde la carpeta de tests");

            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(app, "UI"), "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in FactoryCall.Matches(text))
                    if (HasAsciiLetters(m.Groups[1].Value))
                        offenders.Add(Relative(app, file) + ":" + LineOf(text, m.Index) + "  \"" + m.Groups[1].Value + "\"");
            }

            Assert.AreEqual(0, offenders.Count,
                "estos rótulos se pasan a la fábrica del botón escritos a mano:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// La otra mitad del trato: **una clave que no exista no cae al inglés**, se pinta literalmente
        /// `[[Clave]]` en la pantalla (`LocalizationService.GetString`). Como el compilador no dice nada de
        /// una cadena, esta prueba recorre todos los `_("Clave")` de la interfaz y comprueba que la clave
        /// esté en **los dos** ficheros: es la red que evita el rótulo `[[Map_BtnAtc]]` en un botón.
        /// </summary>
        [TestMethod]
        public void TodaClaveUsadaEnLaInterfazExisteEnLosDosIdiomas()
        {
            string app = FindAppProject();
            if (app == null) Assert.Inconclusive("no se encontró el proyecto vmsOpenAcars desde la carpeta de tests");

            var es = LanguageKeys(Path.Combine(app, "Languages", "es.json"));
            var en = LanguageKeys(Path.Combine(app, "Languages", "en.json"));
            if (es.Count == 0 || en.Count == 0) Assert.Inconclusive("no se encontraron los archivos de idioma");

            var usadas = new SortedSet<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(app, "UI"), "*.cs", SearchOption.AllDirectories))
                foreach (Match m in KeyUse.Matches(File.ReadAllText(file)))
                    usadas.Add(m.Groups[1].Value);

            Assert.IsTrue(usadas.Count > 0, "no se encontró ningún _(\"Clave\") en la interfaz: ¿cambió la forma de traducir?");

            var faltan = new List<string>();
            foreach (string clave in usadas)
            {
                if (!es.Contains(clave)) faltan.Add(clave + "  (falta en es.json)");
                if (!en.Contains(clave)) faltan.Add(clave + "  (falta en en.json)");
            }

            Assert.AreEqual(0, faltan.Count,
                "estas claves se usan en la interfaz y no existen, así que se pintarían como [[Clave]]:\n  " +
                string.Join("\n  ", faltan));
        }

        /// <summary>
        /// **El rótulo tiene que caber en la tecla.** El tamaño de la tecla es fijo y el rótulo no: la misma
        /// clave lleva el texto en inglés y en español, y la traducción puede ser más larga. Medido con los
        /// botones de verdad: «ACEPTAR Y CERRAR» son **128 px** en el botón de aceptar del planificador de
        /// vuelo, que mide **100**; «📋 PLANIFICAR EN SIMBRIEF» son 199 px en uno de 200 —y con el espaciado
        /// de la leyenda, más—; y traducir el `LOGIN` de la barra a «INICIAR SESIÓN» serían 112 px en una
        /// tecla de 90.
        ///
        /// **Y hay un caso peor que la traducción larga: el DPI.** El ancho de un rótulo se mide en píxeles
        /// del dispositivo, así que en un monitor al 200 % de escalado «ENTRAR» pasa de 51 px a **99** en una
        /// tecla que sigue midiendo 90. Por eso la regla se calcula **en el momento de pintar, con el mismo
        /// contexto con el que se dibuja** (`FittedSize`), y por eso esta prueba no usa píxeles fijos: mide
        /// el rótulo en el proceso donde corre —el de tests es consciente del DPI, y ahí se ve el doble— y
        /// compara contra esa medida.
        ///
        /// Lo que se comprueba: un rótulo que cabe se queda con su cuerpo de letra; uno que no cabe se
        /// encoge en pasos de 0,5 pt **hasta caber de verdad**; y hay un suelo, para que un rótulo imposible
        /// no acabe siendo ilegible (se recorta contra el capuchón, que de eso se encarga el renderizador).
        /// </summary>
        [TestMethod]
        public void ElRotuloLargoSeEncogeParaCaberEnLaTecla()
        {
            using (var font = new Font("Consolas", 10f, FontStyle.Bold))
            {
                // Un rótulo que cabe no se toca: se devuelve el mismo cuerpo, sin crear ninguna fuente.
                float anchoEntrar = McduKeyRenderer.MeasureLegend("ENTRAR", font);
                Assert.AreEqual(font.Size, McduKeyRenderer.FittedSize("ENTRAR", font, anchoEntrar + 1f),
                    "un rótulo que ya cabe no debe encogerse");

                // Uno que no cabe, sí: y el resultado tiene que caber de verdad.
                foreach (string texto in new[] { "ACEPTAR Y CERRAR", "INICIAR SESIÓN", "📋 PLANIFICAR EN SIMBRIEF" })
                {
                    float holgado = McduKeyRenderer.MeasureLegend(texto, font);
                    float justo = holgado * 0.6f;      // el mismo rótulo en una tecla más estrecha

                    float size = McduKeyRenderer.FittedSize(texto, font, justo);
                    Assert.IsTrue(size < font.Size,
                        $"«{texto}» mide {holgado:F0} px, no cabe en {justo:F0} y no se encogió");

                    using (var ajustada = new Font(font.FontFamily, size, font.Style))
                        Assert.IsTrue(McduKeyRenderer.MeasureLegend(texto, ajustada) <= justo,
                            $"«{texto}» sigue sin caber a {size} pt");
                }

                // Y hay un suelo: un rótulo imposible se queda en el mínimo en vez de desaparecer.
                Assert.AreEqual(McduKeyRenderer.MinLegendSize,
                    McduKeyRenderer.FittedSize(new string('W', 60), font, 20f),
                    "sin suelo, un rótulo imposible se encogería hasta ser ilegible");
            }
        }

        /// <summary>
        /// **Las teclas hermanas comparten el cuerpo de letra.** Es la queja que lo obliga: con la barra de
        /// teclas iguales al 150 % de escalado, `DESPACHO`, `BITÁCORA` e `INICIAR` se encogían **solas** y el
        /// resto no, así que la barra perdía la armonía. La regla: el tamaño lo decide el grupo —el menor
        /// que necesita el rótulo más largo entre las teclas del **mismo padre, mismo ancho y misma
        /// fuente**—, nunca cada tecla por su cuenta.
        /// </summary>
        [TestMethod]
        public void LasTeclasHermanasCompartenElCuerpoDeLetra()
        {
            using (var panel = new Panel { Size = new Size(400, 60) })
            using (var corta = new McduKeyButton())
            using (var larga = new McduKeyButton())
            {
                foreach (var key in new[] { corta, larga })
                {
                    key.Font = new Font("Consolas", 10f, FontStyle.Bold);
                    key.Size = new Size(104, 34);
                    panel.Controls.Add(key);
                }

                corta.Text = "OFP";
                larga.Text = "BORRAR RESERVA";   // larga pero capaz de caber encogida: con 19 letras ni al mínimo de 6 pt entraría, y mediría el suelo en vez de la armonía

                float deLaCorta = corta.CommonLegendSize();
                float deLaLarga = larga.CommonLegendSize();

                Assert.AreEqual(deLaLarga, deLaCorta,
                    "las dos teclas del mismo grupo tienen que pintar la leyenda al mismo cuerpo de letra");
                Assert.IsTrue(deLaLarga > 0f && deLaLarga < 10f,
                    $"el grupo tiene que encogerse para que quepa la más larga (salió {deLaLarga})");

                // Y encogido «de verdad»: el rótulo más largo cabe en su tecla.
                using (var ajustada = new Font("Consolas", deLaLarga, FontStyle.Bold))
                    Assert.IsTrue(McduKeyRenderer.MeasureLegend(larga.Text, ajustada) <=
                                  larga.ClientSize.Width - 2f * McduKeyRenderer.LegendInset,
                        "el cuerpo común no basta para que el rótulo más largo quepa");
            }
        }

        /// <summary>Las claves de un `.json`, leídas con la misma regex plana que `MainButtonBarTests`.</summary>
        private static HashSet<string> LanguageKeys(string file)
        {
            var keys = new HashSet<string>();
            if (!File.Exists(file)) return keys;

            foreach (Match m in Regex.Matches(File.ReadAllText(file),
                         "\"(?<k>[^\"]+)\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\""))
                keys.Add(m.Groups["k"].Value);

            return keys;
        }

        private static void Scan(string app, string file, List<string> offenders)
        {
            string text = File.ReadAllText(file);

            foreach (Match control in ControlStart.Matches(text))
            {
                int brace = text.IndexOf('{', control.Index + control.Length);
                if (brace < 0) continue;

                string initializer = InitializerAt(text, brace);
                if (initializer == null) continue;

                foreach (Match literal in LiteralLabel.Matches(initializer))
                    if (HasAsciiLetters(literal.Groups[1].Value))
                        offenders.Add(Relative(app, file) + ":" + LineOf(text, brace) + "  \"" + literal.Groups[1].Value + "\"");
            }
        }

        /// <summary>
        /// El bloque `{ ... }` que empieza en `start`, contando llaves y saltándose las cadenas y los
        /// comentarios de línea (que también llevan llaves y comillas).
        /// </summary>
        private static string InitializerAt(string text, int start)
        {
            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"')
                {
                    i++;
                    while (i < text.Length && text[i] != '"')
                    {
                        if (text[i] == '\\') i++;
                        i++;
                    }
                    continue;
                }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') i++;
                    continue;
                }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return text.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        private static bool HasAsciiLetters(string value)
        {
            foreach (char c in value)
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return true;
            return false;
        }

        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        private static string Relative(string app, string file)
        {
            if (app == null) return Path.GetFileName(file);
            return file.StartsWith(app, StringComparison.OrdinalIgnoreCase)
                ? file.Substring(app.Length).TrimStart('\\')
                : Path.GetFileName(file);
        }

        /// <summary>
        /// La carpeta del proyecto de la aplicación, subiendo desde la carpeta de los tests hasta la
        /// solución (misma idea que la búsqueda de rutas de `MainButtonBarTests`, pero sin depender de que
        /// los tests se ejecuten desde un directorio concreto).
        /// </summary>
        private static string FindAppProject()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                if (!File.Exists(Path.Combine(dir.FullName, "vmsOpenAcars.sln"))) continue;

                string app = Path.Combine(dir.FullName, "vmsOpenAcars");
                if (Directory.Exists(Path.Combine(app, "UI"))) return app;
            }
            return null;
        }
    }
}
