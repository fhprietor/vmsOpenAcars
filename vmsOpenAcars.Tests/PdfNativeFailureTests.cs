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
    /// El fallo que reportó un piloto con la 0.9.60: al cargar el OFP salía el diálogo genérico de .NET
    /// —«Se ha intentado cargar un programa con un formato incorrecto. (Excepción de HRESULT:
    /// 0x8007000B)»— justo después de «Plan cargado. Destino: SKAR (elevación: 3976 ft)».
    ///
    /// `0x8007000B` es `ERROR_BAD_FORMAT`: una DLL **nativa** con la bitness equivocada, y aquí es
    /// `BadImageFormatException`. El paquete que viajaba traía solo `pdfium.dll` **x64** (4.294.144 B,
    /// PE 0x8664) y ni `packages\` ni `bin\Release` tenían `x86\pdfium.dll`; el `.exe` es `AnyCPU` con
    /// `Prefer32Bit=false`, así que en un Windows de 32 bits arranca a 32 bits y no hay nativa que
    /// cargar. Lo que estos tests fijan es **la decisión**: qué excepción es un fallo de carga nativa
    /// (para enseñar el texto que dice qué pasa y que el vuelo sigue) y cuál no (para no mentir con la
    /// causa). Los textos, además, existen en los dos idiomas y son simétricos.
    /// </summary>
    [TestClass]
    public class PdfNativeFailureTests
    {
        [TestMethod]
        public void UnBadImageFormatEsUnFalloDeLaNativa()
        {
            // El tipo exacto que produce el cargador al enlazar una DLL de otra arquitectura.
            var ex = new BadImageFormatException(
                "Se ha intentado cargar un programa con un formato incorrecto. (Excepción de HRESULT: 0x8007000B)");

            Assert.IsTrue(PdfNativeFailure.IsNativeLoadFailure(ex),
                "BadImageFormatException es el 0x8007000B del reporte");
            Assert.AreEqual(PdfNativeFailure.NativeMessageKey, PdfNativeFailure.MessageKeyFor(ex));
        }

        [TestMethod]
        public void UnBadImageFormatAnidadoTambienCuenta()
        {
            // El resolvedor de PdfiumViewer inicializa la nativa desde un constructor estático, así que
            // el fallo puede llegar envuelto en TypeInitializationException.
            var ex = new TypeInitializationException(
                "PdfiumViewer.NativeMethods",
                new BadImageFormatException("0x8007000B"));

            Assert.IsTrue(PdfNativeFailure.IsNativeLoadFailure(ex),
                "un TypeInitializationException cuyo InnerException es de carga nativa sí es el fallo");
            Assert.AreEqual(PdfNativeFailure.NativeMessageKey, PdfNativeFailure.MessageKeyFor(ex));

            // Y anidado dos veces tampoco se escapa.
            var doble = new TypeInitializationException("PdfiumViewer.PdfLibrary", ex);
            Assert.IsTrue(PdfNativeFailure.IsNativeLoadFailure(doble));
        }

        [TestMethod]
        public void UnaNativaQueNoEstaOTieneOtroExportTambienEsFalloDeLaNativa()
        {
            Assert.IsTrue(PdfNativeFailure.IsNativeLoadFailure(
                new DllNotFoundException("No se puede encontrar el módulo 'pdfium.dll'.")));
            Assert.IsTrue(PdfNativeFailure.IsNativeLoadFailure(
                new EntryPointNotFoundException("No se encuentra el punto de entrada FPDF_AddRef.")));

            Assert.AreEqual(PdfNativeFailure.NativeMessageKey,
                PdfNativeFailure.MessageKeyFor(new DllNotFoundException()));
        }

        [TestMethod]
        public void OtroFalloNoSeConfundeConLaNativa()
        {
            // Un PDF dañado, un archivo bloqueado o un fallo de la inicialización por otra razón **no**
            // son «falta la librería de PDF»: se dice lo que pasó, no una causa inventada.
            Assert.IsFalse(PdfNativeFailure.IsNativeLoadFailure(
                new InvalidOperationException("El archivo PDF está dañado")));
            Assert.AreEqual(PdfNativeFailure.OtherMessageKey,
                PdfNativeFailure.MessageKeyFor(new InvalidOperationException("El archivo PDF está dañado")));

            Assert.IsFalse(PdfNativeFailure.IsNativeLoadFailure(
                new TypeInitializationException("PdfiumViewer.NativeMethods",
                    new InvalidOperationException("sin permisos"))),
                "un TypeInitializationException por otra causa no es un fallo de carga nativa");

            Assert.IsFalse(PdfNativeFailure.IsNativeLoadFailure(null));
        }

        /// <summary>
        /// La ruta de la nativa es `<base>\x86\pdfium.dll` o `<base>\x64\pdfium.dll` según la bitness
        /// del **proceso**, que es como la busca PdfiumViewer. Y la barra final de la carpeta base no
        /// puede cambiar el resultado: `AppDomain.CurrentDomain.BaseDirectory` la lleva y otras
        /// llamadas no, así que el aviso no puede depender de por dónde entró.
        /// </summary>
        [TestMethod]
        public void LaNativaEsperadaDependeDeLaBitnessYNoDeLaBarraFinal()
        {
            string baseSinBarra = @"C:\juegos\vmsOpenAcars";

            // Proceso de 32 bits: la carpeta `x86`, que es la que faltaba en el equipo del reporte.
            Assert.AreEqual(@"C:\juegos\vmsOpenAcars\x86\pdfium.dll",
                PdfNativeFailure.ExpectedNativePath(baseSinBarra, false));

            // Proceso de 64 bits: `x64`.
            Assert.AreEqual(@"C:\juegos\vmsOpenAcars\x64\pdfium.dll",
                PdfNativeFailure.ExpectedNativePath(baseSinBarra, true));

            // Con barra final, exactamente la misma ruta.
            Assert.AreEqual(
                PdfNativeFailure.ExpectedNativePath(baseSinBarra, false),
                PdfNativeFailure.ExpectedNativePath(baseSinBarra + @"\", false));
            Assert.AreEqual(
                PdfNativeFailure.ExpectedNativePath(baseSinBarra, true),
                PdfNativeFailure.ExpectedNativePath(baseSinBarra + @"\", true));

            // Las dos bitness nunca apuntan al mismo fichero.
            Assert.AreNotEqual(
                PdfNativeFailure.ExpectedNativePath(baseSinBarra, false),
                PdfNativeFailure.ExpectedNativePath(baseSinBarra, true));
        }

        /// <summary>
        /// El detalle técnico tiene que permitir responder sin otra ida y vuelta: qué excepción fue,
        /// con qué bitness corría el proceso, qué nativa se esperaba y si estaba. Es el dato que
        /// faltaba en el reporte del 0x8007000B, donde solo llegó la captura del diálogo.
        /// </summary>
        [TestMethod]
        public void ElDetalleLlevaLaBitnessLaRutaYSiElFicheroExiste()
        {
            string baseDir = @"C:\Program Files (x86)\vmsOpenAcars\";
            var ex = new BadImageFormatException("0x8007000B");

            string de32SinNativa = PdfNativeFailure.BuildDiagnosticDetail(
                ex, baseDir, is64BitProcess: false, is64BitOs: false, nativeExists: false);

            StringAssert.Contains(de32SinNativa, "BadImageFormatException");
            StringAssert.Contains(de32SinNativa, "0x8007000B");
            StringAssert.Contains(de32SinNativa, "process 32 bits");
            StringAssert.Contains(de32SinNativa, "Windows 32 bits");
            StringAssert.Contains(de32SinNativa, @"\x86\pdfium.dll");
            StringAssert.Contains(de32SinNativa, "missing");

            // El mismo proceso de 32 bits en un Windows de 64 bits: la línea distingue las dos cosas,
            // que es justo la confusión que hubo que deshacer a mano en el reporte.
            string mixto = PdfNativeFailure.BuildDiagnosticDetail(
                ex, baseDir, is64BitProcess: false, is64BitOs: true, nativeExists: false);
            StringAssert.Contains(mixto, "process 32 bits");
            StringAssert.Contains(mixto, "Windows 64 bits");

            // Con la nativa de la bitness correcta presente, dice `exists` y la ruta del x64.
            string de64ConNativa = PdfNativeFailure.BuildDiagnosticDetail(
                ex, baseDir, is64BitProcess: true, is64BitOs: true, nativeExists: true);
            StringAssert.Contains(de64ConNativa, @"\x64\pdfium.dll");
            StringAssert.Contains(de64ConNativa, "exists");
            Assert.IsFalse(de64ConNativa.Contains("missing"),
                "si la nativa existe no puede decir «missing»");
        }

        /// <summary>
        /// Las claves del aviso y de la línea de bitness existen en **los dos** ficheros de idioma, con
        /// texto y sin caer al marcador `[[Clave]]`. Se leen del repositorio porque el proyecto de tests
        /// no despliega toda la carpeta `Languages\` (misma búsqueda de rutas que
        /// `CloseupTrackSourceTests`).
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
                    PdfNativeFailure.NativeMessageKey,
                    PdfNativeFailure.OtherMessageKey,
                    "Log_Bitness",
                    "Err_UnhandledTitle",
                    "Err_UnhandledMsg",
                })
                {
                    Assert.IsTrue(file.ContainsKey(key), $"falta «{key}»");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(file[key]), $"«{key}» está vacía");
                }

                // El aviso tiene que decir las tres cosas: de qué librería se trata, que el vuelo NO se
                // ve afectado y dónde puede ver el OFP.
                string nativa = file[PdfNativeFailure.NativeMessageKey];
                StringAssert.Contains(nativa, "pdfium");
                StringAssert.Contains(nativa, "ACARS");
                StringAssert.Contains(nativa, "SimBrief");

                Assert.AreNotEqual(file[PdfNativeFailure.NativeMessageKey],
                    file[PdfNativeFailure.OtherMessageKey],
                    "el texto de la nativa y el genérico no pueden ser el mismo");

                // La línea de bitness lleva los dos huecos: proceso y Windows.
                StringAssert.Contains(file["Log_Bitness"], "{0}");
                StringAssert.Contains(file["Log_Bitness"], "{1}");

                // El aviso global remite al detalle técnico y dice que el vuelo sigue: es lo que
                // impide que el piloto cierre la aplicación por miedo a perder el vuelo.
                string avisoGlobal = file["Err_UnhandledMsg"];
                StringAssert.Contains(avisoGlobal, "{0}");
                StringAssert.Contains(avisoGlobal, "ACARS");
                Assert.IsFalse(string.IsNullOrWhiteSpace(file["Err_UnhandledTitle"]));
            }
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
