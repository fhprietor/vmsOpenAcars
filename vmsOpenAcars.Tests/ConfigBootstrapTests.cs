using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **El primer arranque de una instalación limpia** (v1.0.0): el piloto que descarga el cliente no
    /// tiene `vmsOpenACars.exe.config`, y sin él no hay URL ni clave que valga. La plantilla
    /// (`App.Release.config`) viaja en el zip y se copia **solo si falta**.
    ///
    /// Las dos reglas que se fijan aquí, y por qué:
    ///
    /// 1. **Un config existente no se toca nunca**, ni siquiera vacío: es lo que hace que el del piloto
    ///    sobreviva a las actualizaciones (el `Updater` copia sin borrar). Si esta regla se rompe, cada
    ///    actualización le borra la clave.
    /// 2. **Los marcadores `your_*` no son configuración**: la plantilla pone `your_phpvms_url`, que es
    ///    una cadena no vacía y falsa. Sin tratarla como «no configurado», el cliente pasaría de no
    ///    tener URL a tener una inválida, que es peor.
    ///
    /// Los ficheros son de verdad (carpeta temporal), no dobles: lo que se prueba es una copia byte a
    /// byte y su atomicidad, y eso no se puede simular.
    /// </summary>
    [TestClass]
    public class ConfigBootstrapTests
    {
        private string _dir;

        [TestInitialize]
        public void CrearCarpeta()
        {
            _dir = Path.Combine(Path.GetTempPath(), "vmsacars-cfg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void BorrarCarpeta()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { /* temporal */ }
        }

        private string Ruta(string nombre) { return Path.Combine(_dir, nombre); }

        private void EscribirPlantilla(string contenido = "<configuration><appSettings /></configuration>")
        {
            File.WriteAllText(Ruta(ConfigBootstrap.TemplateFileName), contenido, new UTF8Encoding(false));
        }

        [TestMethod]
        public void SinConfig_InstalaLaPlantillaTalCual()
        {
            EscribirPlantilla("<configuration><appSettings><add key=\"vms_api_url\" value=\"your_phpvms_url\" /></appSettings></configuration>");

            string detail;
            ConfigInstallOutcome outcome = ConfigBootstrap.InstallIfMissing(_dir, out detail);

            Assert.AreEqual(ConfigInstallOutcome.Installed, outcome);
            Assert.IsNull(detail, "sin fallo no debe haber motivo");
            string config = Ruta(ConfigBootstrap.ConfigFileName);
            Assert.IsTrue(File.Exists(config), "el config tiene que existir tras instalarlo");
            CollectionAssert.AreEqual(
                File.ReadAllBytes(Ruta(ConfigBootstrap.TemplateFileName)),
                File.ReadAllBytes(config),
                "la copia es byte a byte: ni se reescribe ni se cambia la codificación");
            Assert.IsFalse(File.Exists(config + ".tmp"), "el temporal se limpia al moverlo");
        }

        [TestMethod]
        public void ConConfigDelPiloto_NoSeToca_NiAunqueEsteVacio()
        {
            EscribirPlantilla();
            string config = Ruta(ConfigBootstrap.ConfigFileName);
            byte[] delPiloto = Encoding.UTF8.GetBytes("<configuration><!-- mio --></configuration>");
            File.WriteAllBytes(config, delPiloto);

            string detail;
            ConfigInstallOutcome outcome = ConfigBootstrap.InstallIfMissing(_dir, out detail);

            Assert.AreEqual(ConfigInstallOutcome.AlreadyPresent, outcome);
            CollectionAssert.AreEqual(delPiloto, File.ReadAllBytes(config),
                "el config del piloto no se toca: es lo que lo hace sobrevivir a las actualizaciones");

            // Y el caso límite: un config de 0 bytes sigue siendo suyo.
            File.WriteAllBytes(config, new byte[0]);
            Assert.AreEqual(ConfigInstallOutcome.AlreadyPresent, ConfigBootstrap.InstallIfMissing(_dir, out detail));
            Assert.AreEqual(0, new FileInfo(config).Length);
        }

        [TestMethod]
        public void SinPlantilla_NoEscribeNadaYLoDice()
        {
            string detail;
            ConfigInstallOutcome outcome = ConfigBootstrap.InstallIfMissing(_dir, out detail);

            Assert.AreEqual(ConfigInstallOutcome.TemplateMissing, outcome);
            Assert.IsFalse(string.IsNullOrEmpty(detail), "el motivo se cuenta en el log");
            Assert.IsFalse(File.Exists(Ruta(ConfigBootstrap.ConfigFileName)),
                "sin plantilla no se inventa un config a medias");
        }

        [TestMethod]
        public void SinDirectorioBase_FallaEnVezDeLanzar()
        {
            string detail;
            Assert.AreEqual(ConfigInstallOutcome.Failed, ConfigBootstrap.InstallIfMissing(null, out detail));
            Assert.AreEqual(ConfigInstallOutcome.Failed, ConfigBootstrap.InstallIfMissing("   ", out detail));
            Assert.IsFalse(string.IsNullOrEmpty(detail));
        }

        [TestMethod]
        public void UnConfigExistenteNoImpideLeerLaPlantilla_SiSeBorraDespues()
        {
            // El orden real de una actualización: el config está, la plantilla llega nueva con el zip.
            EscribirPlantilla("<configuration />");
            File.WriteAllText(Ruta(ConfigBootstrap.ConfigFileName), "<configuration />");

            string detail;
            Assert.AreEqual(ConfigInstallOutcome.AlreadyPresent, ConfigBootstrap.InstallIfMissing(_dir, out detail));
            Assert.IsTrue(File.Exists(Ruta(ConfigBootstrap.TemplateFileName)));
        }
    }

    /// <summary>
    /// **Los marcadores de la plantilla no son datos.** `your_phpvms_url` es una cadena no vacía: si
    /// <see cref="AppConfig"/> la devolviera tal cual, el cliente intentaría resolver un host que no
    /// existe y `NavDataKeyProvider` construiría la petición sobre basura.
    /// </summary>
    [TestClass]
    public class ConfigPlaceholdersTests
    {
        [TestMethod]
        public void LosMarcadoresDeLaPlantilla_NoSonConfiguracion()
        {
            foreach (string marcador in new[]
                     {
                         "your_phpvms_url", "your_phpvms_apikey", "your_simbrief_user",
                         "your_navdata_api_url", " YOUR_PHPVMS_URL "
                     })
            {
                Assert.IsTrue(ConfigPlaceholders.IsPlaceholder(marcador), "es marcador: " + marcador);
                Assert.IsTrue(ConfigPlaceholders.IsMissing(marcador), "no es configuración: " + marcador);
                Assert.AreEqual("", ConfigPlaceholders.OrEmpty(marcador));
            }
        }

        [TestMethod]
        public void UnaUrlDeVerdad_NoEsMarcador()
        {
            const string url = "https://vms.miaerolinea.com";
            Assert.IsFalse(ConfigPlaceholders.IsPlaceholder(url));
            Assert.IsFalse(ConfigPlaceholders.IsMissing(url));
            Assert.AreEqual(url, ConfigPlaceholders.OrEmpty(url));
        }

        [TestMethod]
        public void VacioYNull_TambienSonNoConfigurado()
        {
            Assert.IsTrue(ConfigPlaceholders.IsMissing(null));
            Assert.IsTrue(ConfigPlaceholders.IsMissing(""));
            Assert.IsTrue(ConfigPlaceholders.IsMissing("   "));
            Assert.AreEqual("", ConfigPlaceholders.OrEmpty(null));
            Assert.IsFalse(ConfigPlaceholders.IsPlaceholder(null), "null no es un marcador, es ausencia");
            Assert.IsFalse(ConfigPlaceholders.IsPlaceholder(""));
        }

        [TestMethod]
        public void UnNombreQueEmpiezaPorYour_PeroNoEsMarcador_SigueValiendo()
        {
            // El prefijo es `your_` con guion bajo: `yourserver.com` no es un marcador.
            const string host = "yourserver.example.com";
            Assert.IsFalse(ConfigPlaceholders.IsPlaceholder(host));
            Assert.AreEqual(host, ConfigPlaceholders.OrEmpty(host));
        }
    }
}
