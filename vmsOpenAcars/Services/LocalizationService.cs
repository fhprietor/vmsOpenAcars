using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace vmsOpenAcars.Services
{
    public class LocalizationService
    {
        private static readonly Lazy<LocalizationService> _instance =
            new Lazy<LocalizationService>(() => new LocalizationService());
        public static LocalizationService Instance => _instance.Value;

        private Dictionary<string, string> _currentStrings;
        private Dictionary<string, string> _defaultStrings;
        private string _currentLanguage;

        private LocalizationService()
        {
            // Cargar idioma por defecto (español) como respaldo
            LoadDefaultLanguage();

            string configured = ConfigurationManager.AppSettings["language"] ?? "es";
            LoadLanguage(configured);
        }

        private void LoadDefaultLanguage()
        {
            string defaultPath = Path.Combine(LanguageFolder, "es.json");
            if (File.Exists(defaultPath))
            {
                try
                {
                    string json = File.ReadAllText(defaultPath);
                    _defaultStrings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                        ?? new Dictionary<string, string>();
                }
                catch
                {
                    _defaultStrings = new Dictionary<string, string>();
                }
            }
            else
            {
                _defaultStrings = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// De dónde cuelga `Languages\`.
        ///
        /// Es la carpeta del **ejecutable que aloja este ensamblado**, no la del proceso que lo carga.
        /// En el cliente coinciden, pero cuando el ensamblado lo hospeda otro proceso —el host de
        /// pruebas vive en la carpeta de Visual Studio— `Application.StartupPath` apunta al host y el
        /// servicio se quedaba **sin ningún idioma**: `GetString` devolvía `[[clave]]` y cualquier
        /// prueba que mirase un rótulo traducido medía el marcador de depuración en vez del texto.
        /// Con la ruta del ensamblado, lo que se mide en las pruebas es lo que ve el piloto.
        /// </summary>
        private static string LanguageFolder
        {
            get
            {
                try
                {
                    string dir = Path.GetDirectoryName(typeof(LocalizationService).Assembly.Location);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(Path.Combine(dir, "Languages")))
                        return Path.Combine(dir, "Languages");
                }
                catch
                {
                    // Sin ruta del ensamblado se cae al directorio del proceso, que es lo de siempre.
                }
                return Path.Combine(Application.StartupPath, "Languages");
            }
        }

        public void LoadLanguage(string languageCode)
        {
            try
            {
                string filePath = Path.Combine(LanguageFolder, $"{languageCode}.json");
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    _currentStrings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                        ?? new Dictionary<string, string>();
                    _currentLanguage = languageCode;
                }
                else
                {
                    // Si no existe el archivo, usar el de respaldo
                    _currentStrings = new Dictionary<string, string>(_defaultStrings);
                    _currentLanguage = "es";
                }
            }
            catch
            {
                // Si hay error al leer, usar respaldo
                _currentStrings = new Dictionary<string, string>(_defaultStrings);
                _currentLanguage = "es";
            }
        }

        public string GetString(string key, params object[] args)
        {
            // Buscar en idioma actual
            if (_currentStrings != null && _currentStrings.TryGetValue(key, out string value))
            {
                if (args.Length > 0)
                    return string.Format(value, args);
                return value;
            }

            // Fallback al idioma por defecto
            if (_defaultStrings != null && _defaultStrings.TryGetValue(key, out string defaultValue))
            {
                if (args.Length > 0)
                    return string.Format(defaultValue, args);
                return defaultValue;
            }

            // Si no está en ningún lado, devolver marcador visible para depuración
            return $"[[{key}]]";
        }

        public string CurrentLanguage => _currentLanguage;
    }
}