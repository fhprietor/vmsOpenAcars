using System;
using System.IO;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La carpeta donde se descomprime la actualización: una nueva por intento, y se borra al terminar.**
    ///
    /// El porqué (fallo real reportado por el mantenedor, v0.9.59): la carpeta era **fija**,
    /// `%TEMP%\vmsOpenAcars_update`, y se extraía con `ZipFile.ExtractToDirectory(zip, carpeta)`, la
    /// sobrecarga de **dos argumentos**, que **no sobrescribe**: lanza `IOException` en cuanto
    /// encuentra un fichero que ya existe. Basta con que quede la carpeta de un intento anterior
    /// —una descarga fallida, un reintento, o el mismo zip extraído dos veces— para que a partir de
    /// ahí **falle siempre**, y el diálogo lo enseñaba crudo: «The file
    /// 'C:\Temp\vmsOpenAcars_update\pdfium.dll' already exists». El que chocó fue `pdfium.dll`, que
    /// va en el paquete y también en `x64/`.
    ///
    /// La regla es, por tanto: **la extracción no puede depender de que una carpeta esté limpia**.
    /// En vez de limpiar la carpeta fija y confiar en que el borrado funcione —un fichero bloqueado,
    /// por ejemplo el propio `Updater.exe` ya lanzado desde ahí, deja el `Directory.Delete` a medias—,
    /// cada intento estrena una carpeta con un sufijo aleatorio y la anterior se tira. Así no hay
    /// estado que arrastrar entre intentos y no existe la carrera de «borro antes de extraer».
    ///
    /// <see cref="Cleanup"/> **nunca lanza**: si un fichero sigue bloqueado, se deja lo que haya y el
    /// piloto no ve un error que no puede resolver. Queda además una responsabilidad del llamante que
    /// este helper no puede decidir: **cuando el `Updater.exe` ya se ha lanzado, la carpeta es suya**
    /// —copia desde ella mientras el cliente se cierra— y no se puede borrar aquí.
    ///
    /// Es puro (solo `Path`/`Directory`/`Guid`): sin WinForms y sin red, con test.
    /// </summary>
    internal static class UpdateStaging
    {
        /// <summary>
        /// Prefijo de la carpeta de trabajo. La de antes era exactamente `vmsOpenAcars_update`; con
        /// el sufijo aleatorio el nombre no puede coincidir con el de un intento previo ni con el
        /// de otra instancia del cliente que se esté actualizando a la vez.
        /// </summary>
        internal const string WorkspacePrefix = "vmsOpenAcars_update_";

        /// <summary>
        /// Crea una carpeta de trabajo **nueva y vacía** bajo <paramref name="baseTempDir"/> (si viene
        /// vacío, `%TEMP%`) y devuelve su ruta completa. El nombre es aleatorio, así que una carpeta
        /// anterior —aunque exista y tenga ficheros dentro— **no impide** crear la nueva.
        /// </summary>
        internal static string CreateWorkspace(string baseTempDir)
        {
            string root = string.IsNullOrWhiteSpace(baseTempDir) ? Path.GetTempPath() : baseTempDir;

            // `Guid.NewGuid()` ya no se repite en la práctica; el reintento está porque crear la
            // carpeta es la única operación de la que depende toda la actualización y basta un
            // `Directory.CreateDirectory` sobre algo que ya existe para acabar mezclando ficheros
            // de dos paquetes distintos. Es barato y no oculta el fallo real: si no se consigue,
            // se lanza con la ruta en el mensaje.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string dir = Path.Combine(
                    root,
                    WorkspacePrefix + Guid.NewGuid().ToString("N").Substring(0, 8));

                if (Directory.Exists(dir))
                    continue;

                Directory.CreateDirectory(dir);
                return dir;
            }

            throw new InvalidOperationException(
                "No se pudo crear la carpeta de trabajo de la actualización en '" + root + "'.");
        }

        /// <summary>
        /// Borra la carpeta de trabajo **si sigue existiendo**, con todo su contenido. No lanza:
        /// una carpeta que ya no está, una ruta vacía o un fichero bloqueado se dan por buenos y se
        /// sigue. El piloto no puede hacer nada con un error de borrado en `%TEMP%`.
        /// </summary>
        internal static void Cleanup(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
                return;

            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // Deja lo que haya: es una carpeta temporal y el intento siguiente estrena otra.
            }
        }
    }
}
