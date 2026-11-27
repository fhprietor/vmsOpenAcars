using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// La `api_key` del piloto, **cifrada con DPAPI** (`DataProtectionScope.CurrentUser`).
    ///
    /// Por qué no en el `.config` (v1.0.0): el login entrega una credencial de **larga vida** y el
    /// `.config` es un fichero de texto plano que el piloto acaba subiendo a foros y al soporte. DPAPI
    /// la ata a **su cuenta de Windows** y a **su máquina**: copiar el fichero a otro equipo no la
    /// descifra, y sin su sesión de Windows no hay forma de leerla.
    ///
    /// Precedencia: lo guardado aquí **manda** sobre la `vms_api_key` del `.config`, que se conserva
    /// como respaldo para quien ya la tenía puesta a mano. La clave **nunca** se escribe en el log.
    /// </summary>
    internal static class ApiKeyStore
    {
        private static readonly object Gate = new object();
        private static string _cached;
        private static bool _loaded;

        /// <summary>`%APPDATA%\vmsOpenAcars\api_key.bin` — por usuario, no por máquina.</summary>
        internal static string FilePath { get { return Path.Combine(StorageFolder, "api_key.bin"); } }

        private static string StorageFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vmsOpenAcars");
            }
        }

        /// <summary>La clave guardada, o `null` si no hay (nunca lanza).</summary>
        internal static string Current { get { lock (Gate) { EnsureLoaded(); return _cached; } } }

        internal static bool HasKey { get { return !string.IsNullOrEmpty(Current); } }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _cached = ReadFromDisk();
        }

        private static string ReadFromDisk()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return null;
                byte[] cipher = File.ReadAllBytes(path);
                byte[] plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                string key = Encoding.UTF8.GetString(plain).Trim();
                return key.Length == 0 ? null : key;
            }
            catch
            {
                // Ilegible, de otra cuenta de Windows o corrupto: se trata como «no hay clave» y el
                // piloto vuelve a hacer login. Nunca se propaga: sin clave se puede seguir al login.
                return null;
            }
        }

        /// <summary>Guarda la clave cifrada. `false` si no se pudo escribir; no lanza.</summary>
        internal static bool Save(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return false;

            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(StorageFolder);
                    byte[] plain = Encoding.UTF8.GetBytes(apiKey.Trim());
                    byte[] cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);

                    // Por temporal y moviendo al final: un corte a medias no deja un fichero cifrado
                    // incompleto que luego no descifra y obliga a borrarlo a mano.
                    string tmp = FilePath + ".tmp";
                    File.WriteAllBytes(tmp, cipher);
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                    File.Move(tmp, FilePath);

                    _cached = apiKey.Trim();
                    _loaded = true;
                    return true;
                }
                catch
                {
                    try { if (File.Exists(FilePath + ".tmp")) File.Delete(FilePath + ".tmp"); } catch { }
                    return false;
                }
            }
        }

        /// <summary>Borra la clave guardada (un 401 significa que el piloto la regeneró).</summary>
        internal static void Clear()
        {
            lock (Gate)
            {
                try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { /* mejor esfuerzo */ }
                _cached = null;
                _loaded = true;
            }
        }
    }
}
