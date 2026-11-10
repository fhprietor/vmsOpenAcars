using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Updater
{
    class Program
    {
        // Presupuesto total para que la aplicación cierre. No es «30 s por proceso»: se comprueba
        // el estado cada WaitPollMs y, si al agotarse el plazo queda alguno vivo, se aborta sin
        // copiar. Con dos instancias abiertas el plazo es el mismo, no el doble.
        private const int WaitTotalMs = 30000;
        private const int WaitPollMs = 500;

        // Reintentos de la copia normal. Se conservan los 500 ms de antes porque resuelven el
        // bloqueo momentáneo; para la DLL mapeada no sirven (el bloqueo no caduca) y de eso se
        // encarga el renombrado.
        private const int CopyAttempts = 3;
        private const int CopyRetryMs = 500;

        // Tras renombrar, Windows tarda un instante en soltar el nombre antiguo.
        private const int RenameAttempts = 3;
        private const int RenameRetryMs = 200;
        private const int PostRenameSettleMs = 150;

        // Margen después de que todos los procesos hayan cerrado: el sistema termina de soltar
        // los handles de los módulos que el proceso tenía mapeados.
        private const int ExitGraceMs = 500;

        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Uso: Updater.exe <origen> <destino> <app.exe>");
                return 2;
            }

            // El orden de los argumentos lo fija el cliente (MainForm.DownloadAndUpdate):
            //   <carpeta extraída del zip> <carpeta de instalación> <exe a relanzar>
            string source = args[0];
            string dest = args[1];
            string relaunch = args[2];

            string exeName = Path.GetFileNameWithoutExtension(relaunch);

            // Los .old los deja una ejecución anterior que no pudo borrarlos: la DLL seguía mapeada
            // en la app. Se borran ahora, que la app está cerrada. Si alguno no se deja, se ignora
            // en silencio: la actualización no puede depender de una limpieza cosmética.
            CleanupOldFiles(dest);

            Console.WriteLine("Esperando que la aplicación cierre...");

            if (!WaitForAppToClose(exeName, dest))
            {
                Console.WriteLine();
                Console.WriteLine("Presiona cualquier tecla para salir...");
                try { Console.ReadKey(); } catch { }
                return 1;
            }

            Thread.Sleep(ExitGraceMs);

            // Segunda pasada: si la app tenía bloqueado un .old, la primera limpieza no pudo con él.
            CleanupOldFiles(dest);

            var pendingOld = new List<string>();

            try
            {
                CopyTree(source, dest, exeName, pendingOld);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error durante la actualización: " + ex.Message);
                Console.WriteLine("Presiona cualquier tecla para salir...");
                try { Console.ReadKey(); } catch { }
                return 1;
            }

            ReportPendingOldFiles(dest, pendingOld);

            Console.WriteLine("Actualización completada. Relanzando...");

            try
            {
                Process.Start(relaunch);
            }
            catch (Exception ex)
            {
                Console.WriteLine("La actualización se copió, pero no se pudo relanzar " +
                                  Path.GetFileName(relaunch) + ": " + ex.Message);
                Console.WriteLine("Abre la aplicación a mano.");
                Console.WriteLine("Presiona cualquier tecla para salir...");
                try { Console.ReadKey(); } catch { }
                return 1;
            }

            return 0;
        }

        // ------------------------------------------------------------------------------------
        // 1. Esperar a que la aplicación cierre de verdad
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Espera a que no quede ninguna instancia de la aplicación abierta. Devuelve false si
        /// alguna sigue viva al agotarse el plazo; en ese caso NO se ha copiado nada.
        /// </summary>
        private static bool WaitForAppToClose(string exeName, string dest)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(WaitTotalMs);

            while (true)
            {
                List<string> alive = FindAppProcesses(exeName, dest);

                if (alive.Count == 0)
                    return true;

                if (DateTime.UtcNow >= deadline)
                {
                    // Nunca copiar a ciegas con la app viva: antes se seguía adelante aunque
                    // WaitForExit hubiera agotado su plazo y el resultado era el error del piloto
                    // («...fsuipcClient.dll ... being used by another process»).
                    Console.WriteLine();
                    Console.WriteLine("La aplicación no se ha cerrado después de " +
                                      (WaitTotalMs / 1000) + " s. No se ha copiado ningún archivo.");
                    Console.WriteLine("Cierra desde el Administrador de tareas (Ctrl+Mayús+Esc → " +
                                      "pestaña Detalles → clic derecho → Finalizar tarea):");
                    foreach (string line in alive)
                        Console.WriteLine("  • " + line);


                    Console.WriteLine();
                    Console.WriteLine("Vuelve a ejecutar la actualización cuando estén cerrados.");
                    return false;
                }

                Thread.Sleep(WaitPollMs);
            }
        }

        /// <summary>
        /// Procesos vivos que pueden estar usando los archivos del destino. No basta con buscar
        /// por nombre: una copia renombrada del cliente (o cualquier proceso lanzado desde la
        /// carpeta de instalación) también mantiene mapeadas sus DLL nativas, así que se mira
        /// además la ruta del ejecutable. Un proceso con el nombre esperado pero sin ruta legible
        /// (acceso denegado) también bloquea, porque no se puede descartar que sea la app; los de
        /// otro nombre cuya ruta no se puede leer no son atribuibles a ella y se ignoran.
        /// </summary>
        private static List<string> FindAppProcesses(string exeName, string dest)
        {
            var result = new List<string>();

            string destRoot = Path.GetFullPath(dest)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            int selfPid = Process.GetCurrentProcess().Id;

            Process[] all;
            try
            {
                all = Process.GetProcesses();
            }
            catch
            {
                return result;
            }

            foreach (Process p in all)
            {
                if (p.Id == selfPid)
                {
                    p.Dispose();
                    continue;
                }

                try
                {
                    string name = p.ProcessName;
                    bool sameName = string.Equals(name, exeName, StringComparison.OrdinalIgnoreCase);

                    // La ruta se pide con QueryFullProcessImageName y no con Process.MainModule:
                    // este exe es de 32 bits (AnyCPU con Prefer32Bit por defecto) y MainModule
                    // falla al leer CUALQUIER proceso de 64 bits, que es la mitad de la máquina
                    // y, en particular, el propio cliente. Así se puede reconocer la app por su
                    // carpeta aunque se haya renombrado el .exe.
                    string path = GetProcessImagePath(p.Id);

                    bool pathKnown = !string.IsNullOrEmpty(path);
                    bool underDest = pathKnown &&
                                     Path.GetFullPath(path).StartsWith(destRoot,
                                         StringComparison.OrdinalIgnoreCase);

                    if (pathKnown && underDest)
                    {
                        // Corre desde la carpeta de instalación aunque se llame de otra forma.
                        result.Add(name + " (PID " + p.Id + ")");
                    }
                    else if (pathKnown)
                    {
                        // Mismo nombre desde otra carpeta: es otra instalación y no bloquea estos
                        // archivos; no se espera por ella. Un proceso ajeno que no sale de la
                        // carpeta tampoco puede tener mapeadas sus DLL, así que se ignora igual.
                    }
                    else if (sameName)
                    {
                        // Sin ruta y con el nombre esperado: no se puede descartar que sea la app
                        // (p. ej. sin permiso para leer su módulo principal), así que bloquea.
                        result.Add(name + " (PID " + p.Id + ", ruta no comprobable por permisos)");
                    }
                    // Sin ruta y con otro nombre: no atribuible a la app; no se espera por ella.
                }
                catch
                {
                    // Proceso desaparecido entre GetProcesses y la lectura: irrelevante.
                }
                finally
                {
                    p.Dispose();
                }
            }

            return result;
        }

        /// <summary>Procesos sospechosos para el mensaje de error de la copia.</summary>
        private static string DescribeAppProcesses(string exeName, string dest)
        {
            List<string> alive = FindAppProcesses(exeName, dest);
            if (alive.Count > 0)
                return string.Join(", ", alive.ToArray());

            return "no se ha identificado ninguno (puede ser el antivirus, el Explorador de " +
                   "Windows o un proceso de otro usuario)";
        }

        /// <summary>
        /// Ruta del ejecutable de un proceso, sin depender de la arquitectura: Process.MainModule
        /// devuelve acceso denegado al leer un proceso de 64 bits desde uno de 32. Devuelve null
        /// si no hay permisos (p. ej. procesos del sistema) o el proceso ya ha muerto.
        /// </summary>
        private static string GetProcessImagePath(int pid)
        {
            IntPtr handle = IntPtr.Zero;

            try
            {
                handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (handle == IntPtr.Zero)
                    return null;

                var buffer = new StringBuilder(1024);
                int size = buffer.Capacity;

                if (!QueryFullProcessImageName(handle, 0, buffer, ref size))
                    return null;

                return size > 0 ? buffer.ToString(0, size) : null;
            }
            catch
            {
                // Sin la API (sistemas muy antiguos) o con el proceso desapareciendo.
                return null;
            }
            finally
            {
                if (handle != IntPtr.Zero)
                    CloseHandle(handle);
            }
        }

        private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags,
                                                             StringBuilder exeName, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        // ------------------------------------------------------------------------------------
        // 2. Limpieza de los .old de la ejecución anterior
        // ------------------------------------------------------------------------------------

        private static void CleanupOldFiles(string dest)
        {
            try
            {
                if (!Directory.Exists(dest))
                    return;

                foreach (string old in Directory.GetFiles(dest, "*.old", SearchOption.AllDirectories))
                {
                    try { File.Delete(old); }
                    catch { } // bloqueado por la app: se queda y el renombrado usará un contador
                }
            }
            catch
            {
                // Un .old que no se puede borrar no puede impedir la actualización.
            }
        }

        // ------------------------------------------------------------------------------------
        // 3. Copia, con renombrado cuando el destino está bloqueado
        // ------------------------------------------------------------------------------------

        private static void CopyTree(string source, string dest, string exeName,
                                     List<string> pendingOld)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                throw new IOException("No se pudo leer la carpeta de origen '" + source + "': " +
                                      ex.Message, ex);
            }

            foreach (string file in files)
            {
                string relative = RelativeTo(source, file);
                string destFile = Path.Combine(dest, relative);

                string destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                CopyOneFile(file, destFile, relative, dest, exeName, pendingOld);
            }

            Console.WriteLine("Copiados " + files.Length + " archivos.");
        }

        private static void CopyOneFile(string sourceFile, string destFile, string relative,
                                        string dest, string exeName, List<string> pendingOld)
        {
            if (TryCopy(sourceFile, destFile))
                return;

            // Una DLL nativa cargada —el caso real es fsuipcClient.dll en F:\FS\vmsOpenAcars—
            // está mapeada como imagen en el proceso: Windows no permite reemplazar la imagen de
            // un módulo en uso y File.Copy devuelve sharing violation. Los reintentos de 500 ms no
            // ayudan porque el bloqueo no es momentáneo. Lo que sí se permite es RENOMBRARLA: el
            // cargador abre las imágenes con FILE_SHARE_DELETE, así que el nombre queda libre y la
            // copia nueva entra en su sitio. El .old no se puede borrar mientras el proceso siga
            // vivo y se limpia en la siguiente ejecución del actualizador.
            string oldFile = MoveAside(destFile, relative, dest, exeName);
            pendingOld.Add(oldFile);
            Console.WriteLine("Archivo en uso: " + relative + " → renombrado a " +
                              RelativeTo(dest, oldFile) + "; se copia la versión nueva.");

            Thread.Sleep(PostRenameSettleMs);

            try
            {
                File.Copy(sourceFile, destFile, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new IOException(
                    "No se pudo copiar '" + relative + "' ni después de renombrar el anterior a '" +
                    RelativeTo(dest, oldFile) + "'." + Environment.NewLine +
                    "  Detalle: " + ex.Message + Environment.NewLine +
                    "  Proceso(s) sospechoso(s): " + DescribeAppProcesses(exeName, dest) +
                    Environment.NewLine +
                    "  Cierra la aplicación desde el Administrador de tareas y vuelve a intentarlo.",
                    ex);
            }
        }

        /// <summary>Copia normal con los reintentos de siempre. false = bloqueado.</summary>
        private static bool TryCopy(string sourceFile, string destFile)
        {
            for (int attempt = 1; attempt <= CopyAttempts; attempt++)
            {
                try
                {
                    File.Copy(sourceFile, destFile, true);
                    return true;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    if (attempt == CopyAttempts)
                        return false;

                    Thread.Sleep(CopyRetryMs);
                }
            }

            return false;
        }

        /// <summary>
        /// Mueve el destino a «&lt;nombre&gt;.old» (o «.2.old», «.3.old»… si ya existía) para
        /// dejar libre el nombre que necesita la copia nueva. Si tampoco se puede renombrar,
        /// propaga un error con el archivo, el motivo y el proceso sospechoso.
        /// </summary>
        private static string MoveAside(string destFile, string relative, string dest,
                                        string exeName)
        {
            string oldFile = destFile + ".old";
            for (int n = 2; n < 1000 && File.Exists(oldFile); n++)
                oldFile = destFile + "." + n + ".old";

            Exception last = null;

            for (int attempt = 1; attempt <= RenameAttempts; attempt++)
            {
                try
                {
                    File.Move(destFile, oldFile);
                    return oldFile;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    last = ex;
                    if (attempt < RenameAttempts)
                        Thread.Sleep(RenameRetryMs);
                }
            }

            throw new IOException(
                "No se pudo copiar ni renombrar '" + relative + "'." + Environment.NewLine +
                "  Detalle: " + (last != null ? last.Message : "motivo desconocido") +
                Environment.NewLine +
                "  Proceso(s) sospechoso(s): " + DescribeAppProcesses(exeName, dest) +
                Environment.NewLine +
                "  Cierra la aplicación desde el Administrador de tareas y vuelve a intentarlo.",
                last);
        }

        // ------------------------------------------------------------------------------------
        // 4. Mensajes finales
        // ------------------------------------------------------------------------------------

        private static void ReportPendingOldFiles(string dest, List<string> pendingOld)
        {
            var still = new List<string>();
            foreach (string old in pendingOld)
            {
                try
                {
                    if (File.Exists(old))
                        still.Add(old);
                }
                catch { }
            }

            if (still.Count == 0)
                return;

            Console.WriteLine();
            Console.WriteLine("Aviso: " + still.Count +
                              " archivo(s) de la versión anterior han quedado como .old " +
                              "porque Windows no deja borrarlos con la DLL aún mapeada:");
            foreach (string old in still)
                Console.WriteLine("  • " + RelativeTo(dest, old));

            Console.WriteLine("Se borran solos la próxima vez que arranque el actualizador; " +
                              "también puedes eliminarlos a mano con la aplicación cerrada.");
        }

        private static string RelativeTo(string root, string full)
        {
            string prefix = root
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return full.Substring(prefix.Length);

            return Path.GetFileName(full);
        }
    }
}
