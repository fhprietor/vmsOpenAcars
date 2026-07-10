using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Updater
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Uso: Updater.exe <origen> <destino> <app.exe>");
                return;
            }

            string source = args[0];
            string dest = args[1];
            string relaunch = args[2];

            Console.WriteLine("Esperando que la aplicación cierre...");

            // Wait for the main app process to fully exit before copying files.
            // A fixed sleep is unreliable: DLLs like fsuipcClient.dll stay mapped in
            // the process's address space until the process terminates — regardless of
            // when managed code calls Close(). We find the process by the executable
            // name embedded in the relaunch argument and wait up to 30 seconds.
            string exeName = Path.GetFileNameWithoutExtension(relaunch);
            var procs = Process.GetProcessesByName(exeName);
            foreach (var p in procs)
            {
                try { p.WaitForExit(30000); } catch { }
                p.Dispose();
            }
            Thread.Sleep(500); // brief grace period for OS to release file handles

            try
            {
                // Copiar todos los archivos nuevos sobre los existentes
                foreach (string file in Directory.GetFiles(source, "*",
                             SearchOption.AllDirectories))
                {
                    string relative = file
                        .Substring(source.Length)
                        .TrimStart(Path.DirectorySeparatorChar,
                                   Path.AltDirectorySeparatorChar);

                    string destFile = Path.Combine(dest, relative);
                    string destDir = Path.GetDirectoryName(destFile);

                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    // Reintentar hasta 3 veces por si algún archivo está en uso
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            File.Copy(file, destFile, overwrite: true);
                            break;
                        }
                        catch when (i < 2)
                        {
                            Thread.Sleep(500);
                        }
                    }
                }

                Console.WriteLine("Actualización completada. Relanzando...");
                Process.Start(relaunch);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error durante la actualización: {ex.Message}");
                Console.WriteLine("Presiona cualquier tecla para salir...");
                Console.ReadKey();
            }
        }
    }
}