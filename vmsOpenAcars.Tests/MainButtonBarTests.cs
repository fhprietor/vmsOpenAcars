using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// La barra de botones del ACARS: que **quepa** en la ventana y que cada botón tenga su rótulo en
    /// los dos idiomas.
    ///
    /// El caso que la obliga: hasta v0.9.42 la barra tenía **diez** botones y pedía
    /// `6 + 10×90 + 9×4 = 942` px contra un `MinimumSize` de **800**. El panel no tiene `AutoScroll`
    /// y los botones se colocan con `Location` absoluta, así que `START` y `CANCEL` quedaban fuera de
    /// la ventana y **no se podían pulsar** en cuanto el piloto la encogía. Dos de aquellos diez no
    /// hacían nada —`MENU`, cuyo desplegable solo repetía «Settings» y «Test OSD», los dos ya
    /// existentes en el engranaje de la cabecera y en `SettingsForm`; y `ATIS`, que no tenía `case` y
    /// caía en un manejador genérico que solo escribía la pulsación en el log, mientras el ATIS se
    /// pinta en `AtcPanel`—, así que se retiraron: con ocho la barra pide **754** px.
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
