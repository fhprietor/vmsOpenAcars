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
            Thread.Sleep(2000);

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