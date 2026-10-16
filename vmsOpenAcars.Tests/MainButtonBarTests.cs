using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Controls;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.UI;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// La barra de botones del ACARS: que **quepa** en la ventana, que cada botón tenga su rótulo en los
    /// dos idiomas y que **el rótulo más largo quepa al mismo cuerpo de letra que los demás**.
    ///
    /// El caso que la obliga: hasta v0.9.42 la barra tenía **diez** botones y pedía
    /// `6 + 10×90 + 9×4 = 942` px contra un `MinimumSize` de **800**. El panel no tiene `AutoScroll`
    /// y los botones se colocan con `Location` absoluta, así que `START` y `CANCEL` quedaban fuera de
    /// la ventana y **no se podían pulsar** en cuanto el piloto la encogía. Dos de aquellos diez no
    /// hacían nada —`MENU`, cuyo desplegable solo repetía «Settings» y «Test OSD», los dos ya
    /// existentes en el engranaje de la cabecera y en `SettingsForm`; y `ATIS`, que no tenía `case` y
    /// caía en un manejador genérico que solo escribía la pulsación en el log, mientras el ATIS se
    /// pinta en `AtcPanel`—, así que se retiraron: con ocho la barra pide **866** px.
    ///
    /// Los rótulos se leen de los dos `.json` del repositorio, con la misma búsqueda de rutas que
    /// `CloseupTrackSourceTests`.
    /// </summary>
    [TestClass]
    public class MainButtonBarTests
    {
        /// <summary>
        /// La restricción de verdad: si la barra no cabe, hay botones que el piloto no puede pulsar.
        /// `MainForm` toma su `MinimumSize` de <see cref="MainButtonBar.MinWindowWidth"/>, así que
        /// esta comprobación es sobre el ancho real de la ventana y no sobre una copia del número.
        /// </summary>
        [TestMethod]
        public void LaBarraCabeEnElAnchoMinimoDeLaVentana()
        {
            Assert.IsTrue(MainButtonBar.RequiredWidth <= MainButtonBar.MinWindowWidth,
                $"la barra pide {MainButtonBar.RequiredWidth} px y la ventana no baja de " +
                $"{MainButtonBar.MinWindowWidth}: los últimos botones quedarían fuera del panel");

            // Y está al límite: un noveno botón **no** cabe. Quien lo necesite tiene que quitar otro
            // o subir el mínimo de la ventana a propósito, no descubrirlo con un botón inalcanzable.
            Assert.IsTrue(MainButtonBar.RequiredWidthFor(MainButtonBar.Specs.Count + 1) > MainButtonBar.MinWindowWidth,
                "un botón más tendría que dejar de caber; si cabe, esta prueba ya no protege el ancho");
        }

        /// <summary>
        /// **Cada tecla reparte el ancho, y el rótulo más largo tiene que caber al 150 % de escalado.**
        ///
        /// Es el caso que reportó el mantenedor: con las teclas de 90 px fijos, en una pantalla al 150 %
        /// `DESPACHO`, `BITÁCORA` e `INICIAR` se encogían **solas** —el resto no— y la barra perdía la
        /// armonía. La prueba mide **los rótulos de verdad de los dos idiomas** con el mismo cuerpo de
        /// letra configurado pero al 150 % (10 pt a 144 ppp = 15 pt a 96 ppp, que es como medirlos en
        /// cualquier máquina) y exige que el ancho que le toca a cada tecla en la ventana mínima los
        /// cubra, con el aire de la leyenda. Si alguien añade una etiqueta más larga, esto falla y dice
        /// cuánto hay que ensanchar.
        /// </summary>
        [TestMethod]
        public void ElAnchoDeCadaTeclaCubreElRotuloMasLargoAl150PorCiento()
        {
            var es = LoadLanguage("es.json");
            var en = LoadLanguage("en.json");
            if (es.Count == 0 || en.Count == 0) Assert.Inconclusive("no se encontraron los archivos de idioma");

            // 10 pt en una pantalla al 150 % son 15 pt medidos a 96 ppp, así que se mide con esa fuente.
            // No vale medir a 10 pt y multiplicar por 1,5: la anchura de una letra se redondea a píxeles
            // enteros en cada cuerpo, y de 10 a 15 pt la relación real es 1,36, no 1,5 (medido: 72 px contra
            // 98 px). Multiplicar daría una exigencia falsa de 108 px cuando la de verdad es 98.
            using (var font = new System.Drawing.Font("Consolas", MainButtonBar.ButtonFontSize * 1.5f,
                                                      System.Drawing.FontStyle.Bold))
            {
                float peor = 0f;
                string culpable = "";

                foreach (var spec in MainButtonBar.Specs)
                {
                    foreach (var file in new[] { es, en })
                    {
                        string rotulo;
                        if (!file.TryGetValue(spec.LabelKey, out rotulo)) continue;

                        float ancho = McduKeyRenderer.MeasureLegend(rotulo, font);
                        if (ancho > peor) { peor = ancho; culpable = rotulo; }
                    }
                }

                int porTecla = MainButtonBar.ButtonWidthFor(MainButtonBar.MinWindowWidth, MainButtonBar.Specs.Count);
                float necesario = peor + 2f * McduKeyRenderer.LegendInset;

                Assert.IsTrue(porTecla >= necesario,
                    $"«{culpable}» ocupa {peor:F1} px al 150 % y necesita {necesario:F1}: con " +
                    $"{MainButtonBar.Specs.Count} botones en una ventana de {MainButtonBar.MinWindowWidth} px " +
                    $"solo le tocan {porTecla} px. Hay que subir `MinimumButtonWidth` o el mínimo de ventana");
            }
        }

        /// <summary>
        /// Un botón sin rótulo en el idioma activo no cae al inglés: `LocalizationService` devuelve
        /// `[[clave]]`, así que se vería literalmente eso en la barra. Y los dos idiomas tienen que
        /// seguir siendo simétricos, que es la regla del proyecto.
        /// </summary>
        [TestMethod]
        public void CadaBotonTieneSuRotuloEnLosDosIdiomas()
        {
            var es = LoadLanguage("es.json");
            var en = LoadLanguage("en.json");
            if (es.Count == 0 || en.Count == 0) Assert.Inconclusive("no se encontraron los archivos de idioma");

            CollectionAssert.AreEquivalent(es.Keys.ToList(), en.Keys.ToList(),
                "es.json y en.json tienen que seguir siendo simétricos");

            foreach (var spec in MainButtonBar.Specs)
            {
                foreach (var file in new[] { es, en })
                {
                    Assert.IsTrue(file.ContainsKey(spec.LabelKey),
                        $"el botón {spec.Name} se quedaría sin rótulo: falta «{spec.LabelKey}»");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(file[spec.LabelKey]),
                        $"«{spec.LabelKey}» está vacío");
                }
            }
        }

        /// <summary>
        /// Los dos botones que se retiraron por no hacer nada no pueden volver, y sus claves —más la
        /// del registro de pulsaciones, que solo existía para ellos— se fueron con ellos. Un nombre
        /// repetido, además, pisaría el manejador del anterior.
        /// </summary>
        [TestMethod]
        public void NingunBotonSeQuedaSinAccionYLasClavesRetiradasNoVuelven()
        {
            var es = LoadLanguage("es.json");
            if (es.Count == 0) Assert.Inconclusive("no se encontraron los archivos de idioma");

            foreach (string key in new[] { "BtnMenu", "BtnAtis", "BtnMsg", "Log_ButtonPress" })
                Assert.IsFalse(es.ContainsKey(key), $"«{key}» se retiró con su botón y no debe volver");

            foreach (var spec in MainButtonBar.Specs)
                Assert.IsFalse(spec.Name == "MENU" || spec.Name == "ATIS",
                    $"«{spec.Name}» es uno de los botones sin acción que se retiraron: no vuelve a la barra");

            CollectionAssert.AllItemsAreUnique(MainButtonBar.Specs.Select(s => s.Name).ToList(),
                "dos botones con el mismo nombre se pisarían el manejador");
        }

        /// <summary>
        /// **La leyenda de cada tecla tiene que leerse con la retroiluminación encendida.** Desde v0.9.45
        /// los botones son teclas MCDU: el cuerpo es negro y el color del botón enciende la leyenda, así
        /// que el color dejó de ser un relleno con letra blanca encima y pasó a ser texto.
        ///
        /// Se mide contra `McduKeyRenderer.BacklitBody`, que **no** es el negro puro: es el capuchón con su
        /// lavado de color, que es lo que hay de verdad detrás de las letras. Medir contra el negro daría
        /// margen de más. Los ocho colores de v0.9.44 tal cual —`rgb(60,70,80)` de MAPA, `rgb(150,0,0)` de
        /// CANCELAR— dan 1,75:1 y 1,84:1 y no llegarían; el rojo puro (`Color.Red`) tampoco, con 4,39:1.
        /// </summary>
        [TestMethod]
        public void CadaLeyendaSeLeeConLaRetroiluminacionEncendida()
        {
            foreach (var spec in MainButtonBar.Specs)
            {
                double ratio = ColorContrast.Ratio(spec.LegendColor,
                                                   McduKeyRenderer.BacklitBody(spec.LegendColor));

                Assert.IsTrue(ratio >= ColorContrast.MinimumForText,
                    $"«{spec.Name}» se quedaría sin leerse con la luz encendida: {ratio:F2}:1 " +
                    $"(mínimo {ColorContrast.MinimumForText:F1}:1)");
            }
        }

        /// <summary>
        /// El convenio compartido: los colores de `McduLegend` son los que usan todos los formularios, así
        /// que un valor que no se lea se propagaría a toda la aplicación. Y dos colores distintos tienen que
        /// seguir siendo distinguibles: el verde de confirmar no puede acabar siendo el rojo de cancelar.
        /// </summary>
        [TestMethod]
        public void LosColoresDelConvenioSeLeenYNoSeConfunden()
        {
            var leyendas = new Dictionary<string, System.Drawing.Color>
            {
                { "Neutral", McduLegend.Neutral },
                { "Info",    McduLegend.Info },
                { "Steel",   McduLegend.Steel },
                { "Confirm", McduLegend.Confirm },
                { "Warning", McduLegend.Warning },
                { "Danger",  McduLegend.Danger },
            };

            foreach (var par in leyendas)
            {
                double ratio = ColorContrast.Ratio(par.Value, McduKeyRenderer.BacklitBody(par.Value));
                Assert.IsTrue(ratio >= ColorContrast.MinimumForText,
                    $"`McduLegend.{par.Key}` no se lee con la luz encendida: {ratio:F2}:1");
            }

            CollectionAssert.AllItemsAreUnique(leyendas.Values.ToList(),
                "dos significados distintos no pueden compartir color");
        }

        /// <summary>Los archivos de idioma, con la misma búsqueda de rutas que `CloseupTrackSourceTests`.</summary>
        private static Dictionary<string, string> LoadLanguage(string fileName)
        {
            foreach (string rel in new[]
            {
                Path.Combine("Languages", fileName),
                Path.Combine(@"..\..\vmsOpenAcars\Languages", fileName),
                Path.Combine(@"..\..\..\vmsOpenAcars\bin\Debug\Languages", fileName),
                Path.Combine(@"..\vmsOpenAcars\bin\Debug\Languages", fileName),
            })
            {
                try
                {
                    if (!File.Exists(rel)) continue;

                    // Se lee con regex y no con Newtonsoft para no añadir esa dependencia al proyecto
                    // de tests: el archivo de idioma es plano, clave -> texto.
                    var dict = new Dictionary<string, string>();
                    foreach (Match m in Regex.Matches(File.ReadAllText(rel),
                                 "\"(?<k>[^\"]+)\"\\s*:\\s*\"(?<v>(?:[^\"\\\\]|\\\\.)*)\""))
                        dict[m.Groups["k"].Value] = Regex.Unescape(m.Groups["v"].Value);

                    if (dict.Count > 0) return dict;
                }
                catch { }
            }
            return new Dictionary<string, string>();
        }
    }
}
