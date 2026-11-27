using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Los valores de la plantilla `App.Release.config` son **marcadores** (`your_phpvms_url`,
    /// `your_phpvms_apikey`, `your_simbrief_user`), no datos.
    ///
    /// Por qué importa (v1.0.0): hasta ahora, un piloto sin `.config` veía `""` en <see cref="AppConfig"/>,
    /// y el cliente ya sabía convivir con eso. Al instalarse la plantilla sola, ese valor pasa a ser
    /// **una cadena no vacía y falsa**: una URL inválida que `NavDataKeyProvider` intenta usar al
    /// conectar. Un marcador tiene que significar «no configurado», nunca «dato».
    /// </summary>
    internal static class ConfigPlaceholders
    {
        /// <summary>Prefijo único de todos los marcadores de la plantilla.</summary>
        internal const string Prefix = "your_";

        /// <summary>`true` si el valor es un marcador de la plantilla.</summary>
        internal static bool IsPlaceholder(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>`true` si no hay valor utilizable: `null`, vacío, espacios o marcador.</summary>
        internal static bool IsMissing(string value)
        {
            return string.IsNullOrWhiteSpace(value) || IsPlaceholder(value);
        }

        /// <summary>El valor utilizable, o `""` cuando es un marcador (nunca devuelve el marcador).</summary>
        internal static string OrEmpty(string value)
        {
            return IsPlaceholder(value) ? string.Empty : (value ?? string.Empty);
        }
    }
}
