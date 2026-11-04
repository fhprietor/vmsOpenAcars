using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace vmsOpenAcars.Helpers
{
    /// <summary>Módulo nativo que el cargador de Windows no pudo enlazar.</summary>
    internal enum NativeModule
    {
        Unknown = 0,
        Pdfium,
        SqliteInterop
    }

    /// <summary>Estado de una de las nativas del paquete en el disco del piloto.</summary>
    internal sealed class NativeFileState
    {
        public string Folder   { get; set; }
        public string FileName { get; set; }
        public string Path     { get; set; }
        public bool   Expected64 { get; set; }
        public bool   Exists   { get; set; }
        public long   Length   { get; set; }

        /// <summary>Machine del PE (`0x14c` x86, `0x8664` x64); `null` si el archivo no es un PE legible.</summary>
        public ushort? Machine  { get; set; }
    }

    /// <summary>
    /// Diagnóstico de un fallo al cargar una **nativa** del paquete, y el texto que se le enseña al
    /// piloto. Nació como `PdfNativeFailure` en la 0.9.63 para el `0x8007000B` del visor del OFP,
    /// pero el reporte de la 0.9.63 lo desmintió: el módulo que falló era **`SQLite.Interop.dll`**,
    /// no pdfium. El aviso seguía diciendo «pdfium expected» porque el helper **asumía** pdfium; eso
    /// indujo a error al diagnosticar y es el defecto que corrige la 0.9.64.
    ///
    /// La traza real, capturada por el manejador global:
    ///
    ///     System.BadImageFormatException: … (Excepción de HRESULT: 0x8007000B)
    ///        en System.Data.SQLite.UnsafeNativeMethods.sqlite3_libversion()
    ///        en GMap.NET.CacheProviders.SQLitePureImageCache.Ping()
    ///        en GMap.NET.WindowsForms.GMapControl..cctor()
    ///
    /// O sea: el disparador es el **constructor estático de `GMapControl`**, que llama a
    /// `SQLitePureImageCache.Ping()` (GMap usa SQLite para su caché de teselas). El paquete no lleva
    /// `SQLite.Interop.dll` en la raíz —solo `x86\` (1.592.504 B) y `x64\` (2.005.688 B)—, así que
    /// una copia de 32 bits suelta en la raíz explicaría el `0x8007000B` en un proceso de 64 bits.
    ///
    /// El helper es puro en sus decisiones (no abre ventanas ni toca WinForms): se prueba con su test
    /// y los formularios solo pintan. La lectura del PE y del tamaño sí toca el disco, y va en
    /// `try/catch` porque este código corre también en el manejador global, que es la última red.
    /// </summary>
    internal static class NativeFailure
    {
        /// <summary>Clave del texto de pdfium: falta la nativa de PDF para este sistema.</summary>
        internal const string PdfiumMessageKey = "Ofp_PdfNativeMissing";

        /// <summary>Clave del texto cuando la nativa que falló es `SQLite.Interop.dll`.</summary>
        internal const string SqliteMessageKey = "Err_SqliteNativeMissing";

        /// <summary>Clave genérica del visor del OFP: el PDF no se pudo abrir por otra razón.</summary>
        internal const string OfpGenericMessageKey = "Ofp_PdfOpenFailed";

        /// <summary>Clave genérica del mapa: el control no se pudo crear por otra razón.</summary>
        internal const string MapMessageKey = "Map_MapUnavailable";

        /// <summary>Clave genérica del manejador global de excepciones.</summary>
        internal const string GlobalGenericMessageKey = "Err_UnhandledMsg";

        /// <summary>Machine esperada en un PE de 32 bits.</summary>
        internal const ushort MachineX86 = 0x14c;

        /// <summary>Machine esperada en un PE de 64 bits.</summary>
        internal const ushort MachineX64 = 0x8664;

        private static readonly string[] NativeFileNames = { "pdfium.dll", "SQLite.Interop.dll" };

        /// <summary>
        /// ¿El fallo es que no se pudo **cargar** una nativa? Son los tres que produce el cargador al
        /// enlazar una DLL nativa: bitness equivocada (<see cref="BadImageFormatException"/>), archivo
        /// ausente (<see cref="DllNotFoundException"/>) o export que no está
        /// (<see cref="EntryPointNotFoundException"/>).
        ///
        /// Se mira también el `InnerException` porque una inicialización estática puede llegar envuelta
        /// en <see cref="TypeInitializationException"/>; **solo cuenta si dentro hay uno de esos tres**,
        /// no por ser de tipo `TypeInitializationException` (una inicialización que falla por otra cosa
        /// no es esto).
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
        /// El módulo que de verdad falló, sacado de `ex.StackTrace` / `ex.InnerException` —nunca de
        /// una suposición—. Se busca `SQLite` (que en la traza aparece como
        /// `System.Data.SQLite.UnsafeNativeMethods`, `SQLitePureImageCache.Ping` o
        /// `sqlite3_libversion`) y `pdfium`. Si el texto no nombra a ninguno, se devuelve
        /// <see cref="NativeModule.Unknown"/> y el aviso dirá «no identificado» en vez de inventar
        /// pdfium, que es exactamente el error de la 0.9.63.
        ///
        /// Solo se busca cuando la excepción **es** un fallo de carga nativa: un mensaje sobre
        /// `landing_log.sqlite` no convierte un error de aplicación en un fallo de `SQLite.Interop.dll`.
        /// </summary>
        internal static NativeModule DetectModule(Exception ex)
        {
            if (!IsNativeLoadFailure(ex)) return NativeModule.Unknown;

            for (Exception current = ex; current != null; current = current.InnerException)
            {
                NativeModule fromStack = ModuleInText(current.StackTrace);
                if (fromStack != NativeModule.Unknown) return fromStack;

                NativeModule fromMessage = ModuleInText(current.Message);
                if (fromMessage != NativeModule.Unknown) return fromMessage;
            }
            return NativeModule.Unknown;
        }

        private static NativeModule ModuleInText(string text)
        {
            if (string.IsNullOrEmpty(text)) return NativeModule.Unknown;

            if (text.IndexOf("SQLite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("sqlite3_", StringComparison.OrdinalIgnoreCase) >= 0)
                return NativeModule.SqliteInterop;

            if (text.IndexOf("pdfium", StringComparison.OrdinalIgnoreCase) >= 0)
                return NativeModule.Pdfium;

            return NativeModule.Unknown;
        }

        /// <summary>Nombre del archivo que se le enseña al piloto, o «no identificado».</summary>
        internal static string ModuleFileName(NativeModule module)
        {
            switch (module)
            {
                case NativeModule.SqliteInterop: return "SQLite.Interop.dll";
                case NativeModule.Pdfium:        return "pdfium.dll";
                default:                         return "no identificado";
            }
        }

        /// <summary>
        /// Clave de idioma que le toca al fallo: la del módulo detectado cuando se pudo identificar, y
        /// la genérica del formulario que llama cuando no. El visor del OFP pasa
        /// <see cref="OfpGenericMessageKey"/>, el mapa <see cref="MapMessageKey"/> y el manejador
        /// global <see cref="GlobalGenericMessageKey"/>, así el aviso nunca dice «pdfium» por defecto.
        /// </summary>
        internal static string MessageKeyFor(Exception ex, string fallbackKey)
        {
            switch (DetectModule(ex))
            {
                case NativeModule.SqliteInterop: return SqliteMessageKey;
                case NativeModule.Pdfium:        return PdfiumMessageKey;
                default:                         return fallbackKey;
            }
        }

        /// <summary>Ruta de la nativa que se esperaba, según la bitness del **proceso**.</summary>
        internal static string ExpectedNativePath(string baseDir, bool is64Bit, string fileName)
        {
            return Path.Combine(baseDir ?? string.Empty, is64Bit ? "x64" : "x86", fileName);
        }

        /// <summary>
        /// Machine del PE de un buffer. Devuelve `null` si el archivo no es un PE legible (por ejemplo
        /// un archivo truncado o de 12 B): el diagnóstico tiene que seguir sirviendo aunque el archivo
        /// esté corrupto, y **no puede lanzar** desde el manejador global.
        /// </summary>
        internal static ushort? ReadPeMachine(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 0x40) return null;
            if (bytes[0] != (byte)'M' || bytes[1] != (byte)'Z') return null;

            try
            {
                int peOffset = BitConverter.ToInt32(bytes, 0x3C);
                if (peOffset <= 0 || peOffset > bytes.Length - 6) return null;
                if (bytes[peOffset] != (byte)'P' || bytes[peOffset + 1] != (byte)'E' ||
                    bytes[peOffset + 2] != 0 || bytes[peOffset + 3] != 0)
                    return null;

                return BitConverter.ToUInt16(bytes, peOffset + 4);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Lee la cabecera del archivo (4 KB) y devuelve su machine, o `null` si no se pudo.</summary>
        internal static ushort? ReadPeMachineFromFile(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int count = (int)Math.Min(4096L, stream.Length);
                    if (count < 0x40) return null;

                    var buffer = new byte[count];
                    int read = 0;
                    while (read < count)
                    {
                        int n = stream.Read(buffer, read, count - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    return ReadPeMachine(buffer);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Inspecciona una nativa: existe, tamaño real y machine del PE.</summary>
        internal static NativeFileState Inspect(string baseDir, string folder, string fileName)
        {
            bool expected64 = string.Equals(folder, "x64", StringComparison.OrdinalIgnoreCase);
            var state = new NativeFileState
            {
                Folder     = folder,
                FileName   = fileName,
                Path       = Path.Combine(baseDir ?? string.Empty, folder, fileName),
                Expected64 = expected64
            };

            try
            {
                var info = new FileInfo(state.Path);
                state.Exists = info.Exists;
                if (state.Exists)
                {
                    state.Length  = info.Length;
                    state.Machine = ReadPeMachineFromFile(state.Path);
                }
            }
            catch
            {
                // Un directorio sin permisos no puede tumbar el diagnóstico: se queda como «falta»,
                // que es lo que se verifica a mano en el próximo reporte.
            }

            return state;
        }

        /// <summary>Las cuatro nativas del paquete: x86/x64 × pdfium/SQLite.Interop.</summary>
        internal static IList<NativeFileState> InspectAll(string baseDir)
        {
            var states = new List<NativeFileState>();
            foreach (string folder in new[] { "x86", "x64" })
                foreach (string fileName in NativeFileNames)
                    states.Add(Inspect(baseDir, folder, fileName));
            return states;
        }

        /// <summary>OK / falta / bitness cruzada / ilegible, según el archivo y la carpeta que le toca.</summary>
        internal static string StatusFor(NativeFileState state)
        {
            if (state == null || !state.Exists) return "falta";
            if (!state.Machine.HasValue) return "ilegible";

            ushort expected = state.Expected64 ? MachineX64 : MachineX86;
            return state.Machine.Value == expected ? "OK" : "bitness cruzada";
        }

        /// <summary>
        /// Una entrada del inventario de arranque: carpeta, archivo, estado y —cuando existe— su tamaño
        /// real y su machine. El tamaño se escribe sin separadores para que la línea no dependa de la
        /// cultura del equipo.
        /// </summary>
        internal static string DescribeState(NativeFileState state)
        {
            string status = StatusFor(state);
            if (state == null || !state.Exists) return status;

            string machine = state.Machine.HasValue
                ? "0x" + state.Machine.Value.ToString("x")
                : "?";
            return status + " " + state.Length + " B (" + machine + ")";
        }

        /// <summary>
        /// La línea de una sola tirada que va al log al arrancar: el resultado de las cuatro nativas.
        /// Es informativa: no bloquea nada y si el disco no se puede leer devuelve «falta».
        /// </summary>
        internal static string BuildInventory(string baseDir)
        {
            var sb = new StringBuilder();
            foreach (var state in InspectAll(baseDir))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.AppendFormat("{0} {1} {2}", state.Folder, state.FileName, StatusFor(state));
            }
            return sb.ToString();
        }

        /// <summary>
        /// El bloque de diagnóstico que va debajo del aviso: excepción, **módulo detectado** (o «no
        /// identificado»), bitness del proceso y de Windows, y el estado de las cuatro nativas con
        /// tamaño real y machine del PE. Es lo que convierte «no se pudo abrir» en un dato accionable
        /// sin otra ida y vuelta.
        ///
        /// Va sin pasar por `L` a propósito: lo lee quien mantiene el cliente, no el piloto, y así no
        /// depende del servicio de idiomas, que en el manejador global puede no estar listo.
        /// </summary>
        internal static string BuildDiagnosticDetail(
            Exception ex, string baseDir, bool is64BitProcess, bool is64BitOs)
        {
            var sb = new StringBuilder();
            if (ex != null)
                sb.AppendFormat("{0}: {1}", ex.GetType().Name, ex.Message).AppendLine();

            sb.AppendFormat("Módulo: {0}", ModuleFileName(DetectModule(ex))).AppendLine();
            sb.AppendFormat("Bitness: proceso {0} bits · Windows {1} bits",
                is64BitProcess ? 64 : 32, is64BitOs ? 64 : 32).AppendLine();
            sb.AppendLine("Nativas:");

            foreach (var state in InspectAll(baseDir))
                sb.AppendFormat("  {0}\\{1}: {2}", state.Folder, state.FileName, DescribeState(state))
                  .AppendLine();

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// El diagnóstico listo para pintar: resuelve la bitness del proceso y de Windows y compone el
        /// detalle. Lo usan el visor del OFP, el mapa y el manejador global, para que un reporte de los
        /// tres caminos traiga los mismos datos.
        /// </summary>
        internal static string DescribeFailure(Exception ex, string baseDir)
        {
            return BuildDiagnosticDetail(
                ex, baseDir, Environment.Is64BitProcess, Environment.Is64BitOperatingSystem);
        }
    }
}
