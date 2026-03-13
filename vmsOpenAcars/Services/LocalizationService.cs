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
            // Leer idioma de App.config, por defecto "es"
            string langCode = ConfigurationManager.AppSettings["language"] ?? "es";
            LoadLanguage(langCode);
        }

        private void LoadDefaultLanguage()
        {
            string defaultPath = Path.Combine(Application.StartupPath, "Languages", "es.json");
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

        public void LoadLanguage(string languageCode)
        {
            try
            {
                string filePath = Path.Combine(Application.StartupPath, "Languages", $"{languageCode}.json");
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