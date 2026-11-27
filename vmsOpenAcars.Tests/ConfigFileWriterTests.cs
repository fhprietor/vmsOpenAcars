using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **Escribir el `.config` sin `ConfigurationManager.Save`** (1.0.1), con el defecto real como caso:
    /// al guardar Ajustes en la app en marcha, `Configuration.Save` lanzaba «Colección modificada; puede
    /// que no se ejecute la operación de enumeración» —lo que el piloto veía como «Error:»— y, aunque no
    /// lo hiciera, **reescribía el fichero entero**: la plantilla pasaba de **7 692 a 3 516 bytes**
    /// porque se comía los comentarios. Los binding redirects y las secciones sí se conservaban.
    ///
    /// Lo que se fija aquí: solo cambian **las claves pedidas**, todo lo demás del fichero sobrevive
    /// **literalmente** —comentarios incluidos— y un fichero imposible devuelve `false` en vez de
    /// lanzar, porque guardar unos ajustes no puede tumbar la aplicación.
    /// </summary>
    [TestClass]
    public class ConfigFileWriterTests
    {
        private string _dir;

        [TestInitialize]
        public void CrearCarpeta()
        {
            _dir = Path.Combine(Path.GetTempPath(), "vmsacars-cfgw-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void BorrarCarpeta()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { /* temporal */ }
        }

        private string Escribir(string contenido)
        {
            string path = Path.Combine(_dir, "vmsOpenACars.exe.config");
            File.WriteAllText(path, contenido);
            return path;
        }

        private static IEnumerable<KeyValuePair<string, string>> Par(string key, string value)
        {
            return new[] { new KeyValuePair<string, string>(key, value) };
        }

        private static Dictionary<string, string> LeerClaves(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            var claves = new Dictionary<string, string>();
            XmlElement appSettings = doc.DocumentElement != null ? doc.DocumentElement["appSettings"] : null;
            if (appSettings == null) return claves;
            foreach (XmlNode nodo in appSettings.ChildNodes)
            {
                var elemento = nodo as XmlElement;
                if (elemento != null && elemento.Name == "add")
                    claves[elemento.GetAttribute("key")] = elemento.GetAttribute("value");
            }
            return claves;
        }

        private static int Contar(string texto, string buscado)
        {
            int veces = 0, idx = 0;
            while ((idx = texto.IndexOf(buscado, idx, StringComparison.Ordinal)) >= 0)
            {
                veces++;
                idx += buscado.Length;
            }
            return veces;
        }

        [TestMethod]
        public void CambiaLaClavePedidaYNoTocaLasDemas()
        {
            string path = Escribir(
                "<configuration><appSettings>" +
                "<add key=\"language\" value=\"es\" />" +
                "<add key=\"vms_api_url\" value=\"your_phpvms_url\" />" +
                "</appSettings></configuration>");

            string error;
            Assert.IsTrue(ConfigFileWriter.SetKeys(path, Par("vms_api_url", "https://vholar.co"), out error), error);

            Dictionary<string, string> claves = LeerClaves(path);
            Assert.AreEqual("https://vholar.co", claves["vms_api_url"]);
            Assert.AreEqual("es", claves["language"], "las demás claves no se tocan");
        }

        [TestMethod]
        public void AnadeLaClaveSiNoExisteYNoDuplicaLaQueYaEsta()
        {
            string path = Escribir("<configuration><appSettings><add key=\"language\" value=\"es\" /></appSettings></configuration>");

            string error;
            Assert.IsTrue(ConfigFileWriter.SetKeys(path, Par("osd_enabled", "true"), out error), error);
            Assert.IsTrue(ConfigFileWriter.SetKeys(path, Par("osd_enabled", "false"), out error), error);

            string texto = File.ReadAllText(path);
            Assert.AreEqual(1, Contar(texto, "key=\"osd_enabled\""), "una clave, una entrada");
            Assert.AreEqual("false", LeerClaves(path)["osd_enabled"], "gana el último valor");
        }

        [TestMethod]
        public void SobrevivenLosComentariosYLasDemasSecciones()
        {
            // Recorte de la plantilla real: el comentario que documenta la clave y la sección <runtime>
            // con su binding redirect. `Configuration.Save` devolvía el fichero sin el comentario.
            string path = Escribir(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n" +
                "  <appSettings>\r\n" +
                "    <!-- Plantilla distribuible: sin credenciales. -->\r\n" +
                "    <add key=\"vms_api_url\" value=\"your_phpvms_url\" />\r\n" +
                "  </appSettings>\r\n" +
                "  <runtime><assemblyBinding><dependentAssembly>" +
                "<assemblyIdentity name=\"System.Data.SQLite\" publicKeyToken=\"db937bc2d44ff139\" />" +
                "<bindingRedirect oldVersion=\"0.0.0.0-1.0.119.0\" newVersion=\"1.0.119.0\" />" +
                "</dependentAssembly></assemblyBinding></runtime>\r\n" +
                "</configuration>");

            string error;
            Assert.IsTrue(ConfigFileWriter.SetKeys(path, Par("vms_api_url", "https://vholar.co"), out error), error);

            string texto = File.ReadAllText(path);
            Assert.IsTrue(texto.Contains("Plantilla distribuible"), "el comentario sobrevive");
            Assert.IsTrue(texto.Contains("bindingRedirect"), "el redirect sobrevive");
            Assert.IsTrue(texto.Contains("https://vholar.co"), "y la clave cambió");
            Assert.IsFalse(texto.Contains("your_phpvms_url"), "el marcador ya no está");
        }

        [TestMethod]
        public void UnaRutaImposibleDevuelveFalseYNoLanza()
        {
            string error;
            Assert.IsFalse(ConfigFileWriter.SetKeys(null, Par("language", "es"), out error));
            Assert.IsFalse(ConfigFileWriter.SetKeys("   ", Par("language", "es"), out error));
            Assert.IsFalse(string.IsNullOrEmpty(error));

            // Un fichero que no es XML: se dice y no se pisa.
            string roto = Escribir("esto no es un config");
            Assert.IsFalse(ConfigFileWriter.SetKeys(roto, Par("language", "es"), out error));
            Assert.IsFalse(string.IsNullOrEmpty(error));
            Assert.AreEqual("esto no es un config", File.ReadAllText(roto));
        }

        [TestMethod]
        public void SinFicheroPrevioLoCreaConLaClave()
        {
            string path = Path.Combine(_dir, "nuevo.exe.config");
            string error;
            Assert.IsTrue(ConfigFileWriter.SetKeys(path, Par("language", "en"), out error), error);

            Dictionary<string, string> claves = LeerClaves(path);
            Assert.AreEqual("en", claves["language"]);
        }
    }
}
