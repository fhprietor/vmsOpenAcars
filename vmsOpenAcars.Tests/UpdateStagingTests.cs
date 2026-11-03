using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **La carpeta de trabajo de la actualización** (`UpdateStaging`), que es la que arregla el
    /// diálogo «The file 'C:\Temp\vmsOpenAcars_update\pdfium.dll' already exists».
    ///
    /// La causa raíz: la carpeta era **fija** y se extraía con `ZipFile.ExtractToDirectory(zip,
    /// carpeta)`, la sobrecarga que **no sobrescribe**, así que el segundo intento —con la carpeta
    /// del primero todavía ahí, aunque fuera con un solo `pdfium.dll` dentro— lanzaba `IOException`
    /// siempre. Lo que fijan estos tests no es el nombre de la carpeta nueva, sino las dos
    /// propiedades que hacen imposible repetir el fallo:
    ///
    /// 1. una carpeta vieja **con ficheros dentro no impide** crear la de trabajo (nadie borra
    ///    ni limpia nada antes de extraer);
    /// 2. **dos llamadas seguidas dan carpetas distintas**, siempre vacías (no hay estado que
    ///    arrastrar entre intentos);
    /// 3. `Cleanup` **no lanza** si la carpeta ya no está ni si la ruta viene vacía.
    ///
    /// Se trabaja con un directorio temporal **de verdad** (`Path.GetTempPath()` + un `Guid`), no
    /// con una abstracción: lo que se prueba es justamente el sistema de ficheros. Cada test limpia
    /// la raíz que creó en un `try/finally`, porque las carpetas de trabajo se borran solas y la
    /// raíz no.
    /// </summary>
    [TestClass]
    public class UpdateStagingTests
    {
        // ── La carpeta vieja no impide crear la nueva ──────────────────────────────

        [TestMethod]
        public void UnaCarpetaViejaConFicherosDentro_NoImpideCrearLaCarpetaDeTrabajo()
        {
            string root = NewRoot();
            try
            {
                // El escenario exacto del fallo: la carpeta fija de un intento anterior, con el
                // pdfium.dll que chocaba, y también su copia en x64/ (va en el paquete dos veces).
                string stale = Path.Combine(root, "vmsOpenAcars_update");
                Directory.CreateDirectory(Path.Combine(stale, "x64"));
                File.WriteAllText(Path.Combine(stale, "pdfium.dll"), "pdfium viejo");
                File.WriteAllText(Path.Combine(stale, "x64", "pdfium.dll"), "pdfium viejo x64");

                string workspace = UpdateStaging.CreateWorkspace(root);

                Assert.IsTrue(Directory.Exists(workspace),
                              "la carpeta de trabajo tiene que existir aunque haya una vieja al lado");
                Assert.AreNotEqual(stale, workspace,
                              "la carpeta de trabajo no puede ser la vieja: ahí es donde chocaban los ficheros");
                Assert.AreEqual(0, Directory.GetFileSystemEntries(workspace).Length,
                              "la carpeta de trabajo tiene que estar vacía: se acaba de crear");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        // ── Cada intento estrena carpeta ──────────────────────────────────────────

        [TestMethod]
        public void DosLlamadasSeguidas_DanCarpetasDistintas()
        {
            string root = NewRoot();
            try
            {
                string first = UpdateStaging.CreateWorkspace(root);
                string second = UpdateStaging.CreateWorkspace(root);

                Assert.AreNotEqual(first, second,
                              "un reintento no puede reutilizar la carpeta del intento anterior");
                Assert.IsTrue(Directory.Exists(first) && Directory.Exists(second),
                              "las dos carpetas existen a la vez: no se borra la anterior por su cuenta");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void LaCarpetaDeTrabajo_SeCreaVacia()
        {
            string root = NewRoot();
            try
            {
                string workspace = UpdateStaging.CreateWorkspace(root);

                // «Vacía» es el requisito de la extracción: con un solo fichero dentro, extraer sin
                // sobrescribir volvería a reventar con el mismo IOException de pdfium.dll.
                Assert.IsTrue(Directory.Exists(workspace));
                Assert.AreEqual(0, Directory.GetFiles(workspace).Length, "sin ficheros");
                Assert.AreEqual(0, Directory.GetDirectories(workspace).Length, "sin subcarpetas");
                StringAssert.StartsWith(Path.GetFileName(workspace), UpdateStaging.WorkspacePrefix,
                              "el nombre lleva el prefijo del cliente para poder reconocerla en %TEMP%");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void SinBaseTemporal_UsaLaCarpetaTemporalDelSistema()
        {
            // El llamante puede pasar una base vacía (config rota o prueba): se cae a %TEMP% y la
            // actualización no depende de que ese dato llegue bien.
            string workspace = null;
            try
            {
                workspace = UpdateStaging.CreateWorkspace(null);

                Assert.IsTrue(Directory.Exists(workspace));
                Assert.AreEqual(Path.GetTempPath().TrimEnd('\\'),
                                Path.GetDirectoryName(workspace).TrimEnd('\\'));
            }
            finally
            {
                UpdateStaging.Cleanup(workspace);
            }
        }

        // ── Cleanup: nunca deja un error que el piloto no pueda resolver ──────────

        [TestMethod]
        public void Cleanup_NoLanza_SiLaCarpetaYaNoEsta()
        {
            string root = NewRoot();
            try
            {
                string workspace = UpdateStaging.CreateWorkspace(root);
                Directory.Delete(workspace, true);   // se la lleva otro proceso, o el propio Updater

                UpdateStaging.Cleanup(workspace);    // no debe lanzar
                UpdateStaging.Cleanup(null);         // ni con una ruta nula
                UpdateStaging.Cleanup(string.Empty); // ni vacía
                UpdateStaging.Cleanup("   ");

                Assert.IsFalse(Directory.Exists(workspace), "sigue sin existir, sin excepción de por medio");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Cleanup_BorraLaCarpetaConFicherosDentro()
        {
            // El caso normal de un intento fallido: la descarga se cortó, quedaron ficheros a medias
            // y el `finally` de `DownloadAndUpdate` los tira para no acumular basura en %TEMP%.
            string root = NewRoot();
            try
            {
                string workspace = UpdateStaging.CreateWorkspace(root);
                File.WriteAllText(Path.Combine(workspace, "pdfium.dll"), "a medias");
                Directory.CreateDirectory(Path.Combine(workspace, "x64"));
                File.WriteAllText(Path.Combine(workspace, "x64", "pdfium.dll"), "a medias");

                UpdateStaging.Cleanup(workspace);

                Assert.IsFalse(Directory.Exists(workspace), "la carpeta de trabajo se borra entera");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        // ── Utilidades del test ───────────────────────────────────────────────────

        /// <summary>Raíz temporal propia del test: `%TEMP%\vmsOpenAcars_update_tests_&lt;guid&gt;`.</summary>
        private static string NewRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(), "vmsOpenAcars_update_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        /// <summary>Borra la raíz con lo que haya; nunca lanza, para no tapar el fallo real.</summary>
        private static void DeleteRoot(string root)
        {
            try
            {
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch
            {
                // Un temporal que no se deja borrar no puede tumbar el resultado del test.
            }
        }
    }
}
