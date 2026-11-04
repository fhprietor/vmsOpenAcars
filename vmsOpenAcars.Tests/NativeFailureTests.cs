using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El reporte real de la 0.9.63: el manejador global capturó
    ///
    ///     System.BadImageFormatException: … (Excepción de HRESULT: 0x8007000B)
    ///        en System.Data.SQLite.UnsafeNativeMethods.sqlite3_libversion()
    ///        en GMap.NET.CacheProviders.SQLitePureImageCache.Ping()
    ///        en GMap.NET.WindowsForms.GMapControl..cctor()
    ///
    /// y el aviso decía «pdfium expected» porque `PdfNativeFailure` **asumía** pdfium. El módulo que
    /// falló era `SQLite.Interop.dll`, el disparador el constructor estático de `GMapControl`, y el
    /// paquete solo lleva la nativa en `x86\` (1.592.504 B) y `x64\` (2.005.688 B). Lo que fijan estos
    /// tests es la decisión: el módulo sale de la traza, no de una suposición; las cuatro nativas se
    /// describen con tamaño y machine del PE; y los textos dicen qué hacer sin tocar el vuelo.
    /// Sin abrir ventanas: el helper es puro en sus decisiones.
    /// </summary>
    [TestClass]
    public class NativeFailureTests
    {
        [TestMethod]
        public void LaTrazaDeSqliteSeDetectaComoSqliteInterop()
        {
            // La pila del caso real: `sqlite3_libversion` y `SQLitePureImageCache.Ping`. El nombre del
            // método que lanza reproduce esas marcas en el StackTrace, igual que en el reporte.
            Exception ex = Capture(ThrowSqliteFrame);

            Assert.AreEqual(NativeModule.SqliteInterop, NativeFailure.DetectModule(ex));
            Assert.AreEqual(NativeFailure.SqliteMessageKey,
                NativeFailure.MessageKeyFor(ex, NativeFailure.GlobalGenericMessageKey));

            string detail = NativeFailure.BuildDiagnosticDetail(ex, MissingBaseDir(), false, true);
            StringAssert.Contains(detail, "Módulo: SQLite.Interop.dll");
            Assert.IsFalse(detail.Contains("Módulo: pdfium"),
                "el 0x8007000B de la 0.9.63 era SQLite.Interop, no pdfium");
        }

        [TestMethod]
        public void LaTrazaDePdfiumSeDetectaComoPdfium()
        {
            // El caso de la 0.9.60/0.9.63 en el visor del OFP: el resolvedor de PdfiumViewer carga
            // `pdfium.dll` desde su constructor estático.
            Exception ex = Capture(ThrowPdfiumFrame);

            Assert.AreEqual(NativeModule.Pdfium, NativeFailure.DetectModule(ex));
            Assert.AreEqual(NativeFailure.PdfiumMessageKey,
                NativeFailure.MessageKeyFor(ex, NativeFailure.OfpGenericMessageKey));

            string detail = NativeFailure.BuildDiagnosticDetail(ex, MissingBaseDir(), true, true);
            StringAssert.Contains(detail, "Módulo: pdfium.dll");
        }

        [TestMethod]
        public void SinModuloNoSeInventaPdfium()
        {
            // Un 0x8007000B sin una sola marca de módulo en la traza: antes se decía pdfium por
            // defecto. Ahora se dice «no identificado» y el formulario decide su texto genérico.
            var ex = new BadImageFormatException("0x8007000B");

            Assert.AreEqual(NativeModule.Unknown, NativeFailure.DetectModule(ex));
            Assert.AreEqual(NativeFailure.OfpGenericMessageKey,
                NativeFailure.MessageKeyFor(ex, NativeFailure.OfpGenericMessageKey));

            string detail = NativeFailure.BuildDiagnosticDetail(ex, MissingBaseDir(), true, true);
            StringAssert.Contains(detail, "Módulo: no identificado");
            Assert.IsFalse(detail.Contains("Módulo: pdfium"),
                "sin dato no se puede afirmar que el módulo sea pdfium");
        }

        [TestMethod]
        public void UnBadImageFormatAnidadoTambienCuenta()
        {
            // Una inicialización estática llega envuelta en TypeInitializationException; cuenta si
            // dentro está uno de los tres tipos del cargador, no por el tipo exterior.
            var ex = new TypeInitializationException(
                "GMap.NET.WindowsForms.GMapControl",
                new BadImageFormatException("0x8007000B"));

            Assert.IsTrue(NativeFailure.IsNativeLoadFailure(ex));

            var doble = new TypeInitializationException("GMap.NET.WindowsForms.GMapControl", ex);
            Assert.IsTrue(NativeFailure.IsNativeLoadFailure(doble));

            Assert.IsFalse(NativeFailure.IsNativeLoadFailure(
                new TypeInitializationException("GMap.NET.WindowsForms.GMapControl",
                    new InvalidOperationException("sin permisos"))),
                "una inicialización que falla por otra cosa no es un fallo de carga nativa");
        }

        [TestMethod]
        public void UnaNativaQueNoEstaOTieneOtroExportTambienEsFalloDeLaNativa()
        {
            Assert.IsTrue(NativeFailure.IsNativeLoadFailure(
                new DllNotFoundException("No se puede encontrar el módulo 'SQLite.Interop.dll'.")));
            Assert.IsTrue(NativeFailure.IsNativeLoadFailure(
                new EntryPointNotFoundException("No se encuentra el punto de entrada FPDF_AddRef.")));
        }

        [TestMethod]
        public void OtroFalloNoSeConfundeConLaNativa()
        {
            Assert.IsFalse(NativeFailure.IsNativeLoadFailure(
                new InvalidOperationException("El archivo PDF está dañado")));
            Assert.IsFalse(NativeFailure.IsNativeLoadFailure(null));

            // Un mensaje sobre `landing_log.sqlite` no convierte un error de aplicación en un fallo
            // de `SQLite.Interop.dll`: la detección solo busca dentro de fallos de carga nativa.
            var errorDeAplicacion = new InvalidOperationException("no se pudo abrir landing_log.sqlite");
            Assert.AreEqual(NativeModule.Unknown, NativeFailure.DetectModule(errorDeAplicacion));
            Assert.AreEqual(NativeFailure.OfpGenericMessageKey,
                NativeFailure.MessageKeyFor(errorDeAplicacion, NativeFailure.OfpGenericMessageKey));
        }

        [TestMethod]
        public void UnArchivoDeBitnessCruzadaSeDelata()
        {
            string dir = NewTempDir();
            try
            {
                string folder = Path.Combine(dir, "x86");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "pdfium.dll"), PeImage(NativeFailure.MachineX64));

                var state = NativeFailure.Inspect(dir, "x86", "pdfium.dll");

                Assert.IsTrue(state.Exists);
                Assert.AreEqual(NativeFailure.MachineX64, state.Machine.Value);
                Assert.AreEqual("bitness cruzada", NativeFailure.StatusFor(state));
                StringAssert.Contains(NativeFailure.DescribeState(state), "0x8664");
            }
            finally { DeleteQuietly(dir); }
        }

        [TestMethod]
        public void UnArchivoAusenteSeInformaComoFalta()
        {
            string dir = NewTempDir();
            try
            {
                var state = NativeFailure.Inspect(dir, "x64", "SQLite.Interop.dll");

                Assert.IsFalse(state.Exists);
                Assert.AreEqual(0L, state.Length);
                Assert.IsNull(state.Machine);
                Assert.AreEqual("falta", NativeFailure.StatusFor(state));
            }
            finally { DeleteQuietly(dir); }
        }

        [TestMethod]
        public void UnArchivoDeTamanoRaroNoRompeElDiagnostico()
        {
            // Un archivo truncado (12 B) existe pero no es un PE legible: el diagnóstico tiene que
            // decir su tamaño real y «ilegible», no lanzar desde el manejador global.
            string dir = NewTempDir();
            try
            {
                string folder = Path.Combine(dir, "x86");
                Directory.CreateDirectory(folder);
                byte[] truncated = { (byte)'M', (byte)'Z', 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                File.WriteAllBytes(Path.Combine(folder, "SQLite.Interop.dll"), truncated);

                var state = NativeFailure.Inspect(dir, "x86", "SQLite.Interop.dll");

                Assert.IsTrue(state.Exists);
                Assert.AreEqual(12L, state.Length);
                Assert.IsNull(state.Machine);
                Assert.AreEqual("ilegible", NativeFailure.StatusFor(state));
                StringAssert.Contains(NativeFailure.DescribeState(state), "12 B (?)");
            }
            finally { DeleteQuietly(dir); }
        }

        [TestMethod]
        public void ElDetalleLlevaElModuloLaBitnessYLasCuatroNativas()
        {
            string dir = NewTempDir();
            try
            {
                WriteNative(dir, "x86", "pdfium.dll", NativeFailure.MachineX86);
                WriteNative(dir, "x86", "SQLite.Interop.dll", NativeFailure.MachineX64); // cruzada
                // x64\pdfium.dll ausente a propósito.
                WriteNative(dir, "x64", "SQLite.Interop.dll", NativeFailure.MachineX64);

                string detail = NativeFailure.BuildDiagnosticDetail(
                    new BadImageFormatException("0x8007000B"), dir, is64BitProcess: true, is64BitOs: true);

                StringAssert.Contains(detail, "BadImageFormatException");
                StringAssert.Contains(detail, "Módulo: no identificado");
                StringAssert.Contains(detail, "proceso 64 bits");
                StringAssert.Contains(detail, "Windows 64 bits");
                StringAssert.Contains(detail, @"Nativas:");
                StringAssert.Contains(detail, @"x86\pdfium.dll: OK 256 B (0x14c)");
                StringAssert.Contains(detail, @"x86\SQLite.Interop.dll: bitness cruzada 256 B (0x8664)");
                StringAssert.Contains(detail, @"x64\pdfium.dll: falta");
                StringAssert.Contains(detail, @"x64\SQLite.Interop.dll: OK 256 B (0x8664)");
            }
            finally { DeleteQuietly(dir); }
        }

        [TestMethod]
        public void LaLineaDeNativasResumeLasCuatro()
        {
            string dir = NewTempDir();
            try
            {
                WriteNative(dir, "x86", "pdfium.dll", NativeFailure.MachineX86);
                WriteNative(dir, "x64", "SQLite.Interop.dll", NativeFailure.MachineX64);

                string line = NativeFailure.BuildInventory(dir);

                StringAssert.Contains(line, "x86 pdfium.dll OK");
                StringAssert.Contains(line, "x86 SQLite.Interop.dll falta");
                StringAssert.Contains(line, "x64 pdfium.dll falta");
                StringAssert.Contains(line, "x64 SQLite.Interop.dll OK");
                Assert.AreEqual(3, line.Count(c => c == '·'),
                    "la línea resume exactamente las cuatro nativas");
            }
            finally { DeleteQuietly(dir); }
        }

        [TestMethod]
        public void LaRutaDeLaNativaEsperadaDependeDeLaBitnessYNoDeLaBarraFinal()
        {
            string baseSinBarra = @"C:\juegos\vmsOpenAcars";

            Assert.AreEqual(@"C:\juegos\vmsOpenAcars\x86\pdfium.dll",
                NativeFailure.ExpectedNativePath(baseSinBarra, false, "pdfium.dll"));
            Assert.AreEqual(@"C:\juegos\vmsOpenAcars\x64\SQLite.Interop.dll",
                NativeFailure.ExpectedNativePath(baseSinBarra, true, "SQLite.Interop.dll"));

            Assert.AreEqual(
                NativeFailure.ExpectedNativePath(baseSinBarra, false, "pdfium.dll"),
                NativeFailure.ExpectedNativePath(baseSinBarra + @"\", false, "pdfium.dll"));
            Assert.AreEqual(
                NativeFailure.ExpectedNativePath(baseSinBarra, true, "SQLite.Interop.dll"),
                NativeFailure.ExpectedNativePath(baseSinBarra + @"\", true, "SQLite.Interop.dll"));
        }

        /// <summary>
        /// Las claves del aviso existen en **los dos** ficheros de idioma, con texto y sin caer al
        /// marcador `[[Clave]]`. Se leen del repositorio porque el proyecto de tests no despliega toda
        /// la carpeta `Languages\` (misma búsqueda de rutas que `CloseupTrackSourceTests`).
        /// </summary>
        [TestMethod]
        public void LosTextosNuevosExistenEnLosDosIdiomasYNoSeConfunden()
        {
            var es = LoadLanguage("es.json");
            var en = LoadLanguage("en.json");
            if (es.Count == 0 || en.Count == 0) Assert.Inconclusive("no se encontraron los archivos de idioma");

            CollectionAssert.AreEquivalent(es.Keys.ToList(), en.Keys.ToList(),
                "es.json y en.json tienen que seguir siendo simétricos");

            foreach (var file in new[] { es, en })
            {
                foreach (string key in new[]
                {
                    NativeFailure.PdfiumMessageKey,
                    NativeFailure.OfpGenericMessageKey,
                    NativeFailure.SqliteMessageKey,
                    NativeFailure.MapMessageKey,
                    NativeFailure.GlobalGenericMessageKey,
                    "Log_Bitness",
                    "Log_Natives",
                    "Err_UnhandledTitle",
                })
                {
                    Assert.IsTrue(file.ContainsKey(key), $"falta «{key}»");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(file[key]), $"«{key}» está vacía");
                }

                // El aviso de pdfium sigue diciendo de qué librería se trata y que el vuelo NO se ve
                // afectado; el de SQLite, además, manda reinstalar en limpio y borrar la copia suelta.
                string pdfium = file[NativeFailure.PdfiumMessageKey];
                StringAssert.Contains(pdfium, "pdfium");
                StringAssert.Contains(pdfium, "ACARS");
                StringAssert.Contains(pdfium, "SimBrief");

                string sqlite = file[NativeFailure.SqliteMessageKey];
                StringAssert.Contains(sqlite, "SQLite.Interop.dll");
                StringAssert.Contains(sqlite, "ACARS");
                StringAssert.Contains(sqlite, "x86");
                StringAssert.Contains(sqlite, "x64");
                Assert.IsTrue(sqlite.IndexOf("reinstala", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              sqlite.IndexOf("reinstall", StringComparison.OrdinalIgnoreCase) >= 0,
                    "el texto de SQLite tiene que decir «reinstala»/«reinstall»");
                Assert.IsTrue(sqlite.IndexOf("raíz", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              sqlite.IndexOf("root", StringComparison.OrdinalIgnoreCase) >= 0,
                    "el texto de SQLite tiene que señalar la raíz de la instalación");

                Assert.AreNotEqual(file[NativeFailure.PdfiumMessageKey],
                    file[NativeFailure.SqliteMessageKey],
                    "pdfium y SQLite no pueden compartir texto: son módulos distintos");

                // El mapa tiene su propio aviso genérico, y la línea de arranque su hueco.
                StringAssert.Contains(file[NativeFailure.MapMessageKey], "ACARS");
                StringAssert.Contains(file["Log_Natives"], "{0}");
                // El manejador global compone la genérica con `{0}`: si perdiera el hueco, el detalle
                // técnico dejaría de verse en el diálogo.
                StringAssert.Contains(file[NativeFailure.GlobalGenericMessageKey], "{0}");
            }
        }

        // ── Utilidades ────────────────────────────────────────────────────────────

        private static Exception Capture(Action action)
        {
            try { action(); }
            catch (Exception ex) { return ex; }

            Assert.Fail("se esperaba una excepción");
            return null;
        }

        // El nombre del método aparece en el StackTrace: es la marca «SQLite» de la traza real.
        private static void ThrowSqliteFrame()
        {
            throw new BadImageFormatException(
                "Se ha intentado cargar un programa con un formato incorrecto. (Excepción de HRESULT: 0x8007000B)");
        }

        private static void ThrowPdfiumFrame()
        {
            throw new BadImageFormatException(
                "No se puede cargar pdfium.dll: formato incorrecto. (Excepción de HRESULT: 0x8007000B)");
        }

        private static byte[] PeImage(ushort machine)
        {
            var bytes = new byte[256];
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'Z';
            BitConverter.GetBytes(0x80).CopyTo(bytes, 0x3C);
            bytes[0x80] = (byte)'P';
            bytes[0x81] = (byte)'E';
            bytes[0x82] = 0;
            bytes[0x83] = 0;
            BitConverter.GetBytes(machine).CopyTo(bytes, 0x84);
            return bytes;
        }

        private static void WriteNative(string dir, string folder, string fileName, ushort machine)
        {
            string path = Path.Combine(dir, folder);
            Directory.CreateDirectory(path);
            File.WriteAllBytes(Path.Combine(path, fileName), PeImage(machine));
        }

        private static string MissingBaseDir()
        {
            // No hace falta tocar el disco para el detalle: una carpeta que no existe deja las cuatro
            // nativas en «falta», que es justo lo que se comprueba en estos tres casos.
            return Path.Combine(Path.GetTempPath(), "vmsOpenACars-native-missing-" + Guid.NewGuid().ToString("N"));
        }

        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "vmsOpenACars-native-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteQuietly(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch { /* el temporal no puede hacer fallar el test */ }
        }

        /// <summary>Los archivos de idioma, con la misma búsqueda de rutas que otros tests del proyecto.</summary>
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
