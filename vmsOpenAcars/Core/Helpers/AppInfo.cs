using System.Reflection;

namespace vmsOpenAcars.Core.Helpers
{
    public static class AppInfo
    {
        private static string _version;

        /// <summary>
        /// Obtiene la versión actual de la aplicación desde los metadatos del ensamblado.
        /// </summary>
        public static string Version
        {
            get
            {
                if (_version == null)
                {
                    try
                    {
                        // Intentar obtener la versión informativa (con sufijos beta/rc)
                        var informationalVersion = Assembly.GetExecutingAssembly()
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                            ?.InformationalVersion;

                        if (!string.IsNullOrEmpty(informationalVersion))
                        {
                            _version = informationalVersion;
                        }
                        else
                        {
                            // Fallback a la versión numérica
                            var version = Assembly.GetExecutingAssembly().GetName().Version;
                            _version = version != null ? version.ToString(3) : "1.0.0";
                        }
                    }
                    catch
                    {
                        _version = "1.0.0";
                    }
                }
                return _version;
            }
        }
    }
}