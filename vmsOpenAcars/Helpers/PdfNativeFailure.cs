using System;
using System.IO;
using System.Text;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Decide si un fallo al pintar el OFP es «no se pudo cargar la librería nativa de PDF» y qué
    /// texto enseñar al piloto.
    ///
    /// **El caso que lo motivó (v0.9.61, reporte de un piloto con la 0.9.60):** al cargar el OFP salía
    /// el diálogo genérico de .NET con «Se ha intentado cargar un programa con un formato incorrecto.
    /// (Excepción de HRESULT: 0x8007000B)». Es <see cref="BadImageFormatException"/>, o sea
    /// **ERROR_BAD_FORMAT**: una DLL nativa con la bitness equivocada. `OFPViewerForm` llama a
    /// `PdfDocument.Load` (PdfiumViewer), y `PdfiumViewer.NativeMethods` resuelve la nativa así:
    /// primero `pdfium.dll` de la carpeta `x86`/`x64` según `IntPtr.Size` y, si esa carpeta no
    /// existe, deja que el P/Invoke busque `pdfium.dll` en la carpeta del exe. El paquete que traía
    /// el cliente era **solo x64** (`PdfiumViewer.Native.x86_64…2018.4.8.256`, `pdfium.dll` de
    /// **4.294.144 B**, PE `0x8664`) y estaba copiado a la raíz de `bin\Release`, mientras que
    /// **`x86\pdfium.dll` no existía**. En un Windows de 64 bits todo funciona porque el proceso va a
    /// 64 bits; en un **Windows de 32 bits** el `AnyCPU` con `Prefer32Bit=false` arranca a 32 bits,
    /// la carpeta `x86` falta y el P/Invoke acaba cargando el x64 de la raíz → **0x8007000B**.
    /// Se reprodujo sin la interfaz: con el mismo layout, un host de PowerShell de 32 bits da
    /// exactamente `HRESULT: 0x8007000B`, y con `x86\pdfium.dll` abre el PDF. SQLite sí funcionaba en
    /// ese equipo porque `x86\SQLite.Interop.dll` sí viaja en el paquete.
    ///
    /// El helper es puro (sin WinForms ni red) porque la decisión —qué excepción es un fallo de
    /// carga nativa y qué clave de idioma le toca— se prueba con su test, y el formulario solo pinta.
    /// </summary>
    internal static class PdfNativeFailure
    {
        /// <summary>Clave del texto que explica que falta la nativa de PDF para este sistema.</summary>
        internal const string NativeMessageKey = "Ofp_PdfNativeMissing";

        /// <summary>Clave del texto genérico: el PDF no se pudo abrir por otra razón.</summary>
        internal const string OtherMessageKey = "Ofp_PdfOpenFailed";

        /// <summary>
        /// ¿El fallo es que no se pudo **cargar** la nativa? Son los tres que produce el cargador al
        /// enlazar una DLL nativa: bitness equivocada (<see cref="BadImageFormatException"/>), archivo
        /// ausente (<see cref="DllNotFoundException"/>) o export que no está
        /// (<see cref="EntryPointNotFoundException"/>).
        ///
        /// Se mira también el `InnerException` porque un `catch` de la inicialización estática del
        /// resolvedor de PdfiumViewer puede llegar envuelto en <see cref="TypeInitializationException"/>;
        /// **solo cuenta si dentro hay uno de esos tres**, no por ser de tipo
        /// `TypeInitializationException` (una inicialización que falla por otra cosa no es esto).
        /// </summary>
        internal static bool IsNativeLoadFailure(Exception ex)
        {
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                if (current is BadImageFormatException ||
                    current is DllNotFoundException ||
                    current is EntryPointNotFoundException)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Clave de idioma que le toca al fallo: la específica de la nativa cuando lo es, y la genérica
        /// cuando no —así el piloto nunca ve el diálogo de .NET y el mensaje dice lo que de verdad pasó—.
        /// </summary>
        internal static string MessageKeyFor(Exception ex)
        {
            return IsNativeLoadFailure(ex) ? NativeMessageKey : OtherMessageKey;
        }

        /// <summary>
        /// Ruta exacta de la nativa que PdfiumViewer habría cargado según la bitness del **proceso**:
        /// `<base>\x86\pdfium.dll` o `<base>\x64\pdfium.dll`.
        ///
        /// El piloto de la 0.9.60 reportó el 0x8007000B y no había forma de saber, sin otra ida y
        /// vuelta, si su equipo era de 32 bits y qué ruta se había intentado. La decisión es pura
        /// —bitness y ruta base, nada de IO— para poder fijarla con tests; la existencia la comprueba
        /// `DescribeFailure`, que sí toca el disco. `Path.Combine` tolera la barra final de
        /// `AppDomain.CurrentDomain.BaseDirectory`, así que la ruta sale igual con barra o sin ella.
        /// </summary>
        internal static string ExpectedNativePath(string baseDir, bool is64Bit)
        {
            return Path.Combine(baseDir ?? string.Empty, is64Bit ? "x64" : "x86", "pdfium.dll");
        }

        /// <summary>
        /// El bloque de diagnóstico que va detrás del aviso: tipo y mensaje de la excepción, la
        /// bitness del **proceso** y la de Windows, y la ruta de la nativa que se esperaba **con si
        /// existe o no**. Es lo que convierte un «no se pudo abrir el OFP» en un dato accionable.
        ///
        /// Va en el formato técnico de siempre (el mismo `Tipo: mensaje` que ya se pintaba en gris),
        /// sin pasar por `L` a propósito: lo lee quien mantiene el cliente, no el piloto, y así el
        /// diagnóstico no cambia con el idioma ni depende del servicio de idiomas, que en el manejador
        /// global puede no estar listo.
        /// </summary>
        internal static string BuildDiagnosticDetail(
            Exception ex, string baseDir, bool is64BitProcess, bool is64BitOs, bool nativeExists)
        {
            string path = ExpectedNativePath(baseDir, is64BitProcess);

            var sb = new StringBuilder();
            if (ex != null)
                sb.AppendFormat("{0}: {1}", ex.GetType().Name, ex.Message).AppendLine();

            sb.AppendFormat("bitness: process {0} bits · Windows {1} bits",
                is64BitProcess ? 64 : 32, is64BitOs ? 64 : 32).AppendLine();

            sb.AppendFormat("pdfium expected: {0} ({1})",
                path, nativeExists ? "exists" : "missing");

            return sb.ToString();
        }

        /// <summary>
        /// El diagnóstico listo para pintar: resuelve la bitness del proceso y de Windows, comprueba
        /// si la nativa de ESA bitness está en su carpeta y compone el detalle. Lo usan el visor del
        /// OFP y el manejador global de excepciones, para que un reporte de los dos caminos traiga
        /// los mismos datos. `File.Exists` va en su propio `try` porque un directorio sin permisos no
        /// puede tumbar el manejador que, por definición, es la última red.
        /// </summary>
        internal static string DescribeFailure(Exception ex, string baseDir)
        {
            bool is64BitProcess = Environment.Is64BitProcess;
            bool nativeExists = false;
            try
            {
                nativeExists = File.Exists(ExpectedNativePath(baseDir, is64BitProcess));
            }
            catch
            {
                // Sin poder mirar el disco el diagnóstico sigue sirviendo: dirá «missing», que es
                // justo lo que se verifica a mano en el próximo reporte.
            }

            return BuildDiagnosticDetail(
                ex, baseDir, is64BitProcess, Environment.Is64BitOperatingSystem, nativeExists);
        }
    }
}
