using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Escribe claves en el `.config` del cliente **sin pasar por `ConfigurationManager.Save`**.
    ///
    /// Por qué (1.0.1): `ConfigurationManager.OpenExeConfiguration(...).Save(...)` falla en la app en
    /// marcha con «**Colección modificada; puede que no se ejecute la operación de enumeración**»
    /// —un `InvalidOperationException` que el piloto ve como «Error:» al guardar Ajustes—, porque
    /// serializa secciones mientras el propio `ConfigurationManager` las tiene cargadas. Y aunque no
    /// fallara, reescribe el fichero entero: comprobado con la plantilla real, la deja de **7 692 a
    /// 3 516 bytes** porque **se come todos los comentarios** (los binding redirects y las secciones
    /// sí se conservan). Los comentarios de la plantilla son la única documentación que el piloto
    /// tiene a mano, así que perderlos es un daño gratuito.
    ///
    /// Aquí se edita el XML: se buscan o se crean los `<add key="…"/>` de `<appSettings>` y **no se
    /// toca nada más**. Todo lo demás del fichero (comentarios, `runtime`, `system.web`, orden de las
    /// claves) sobrevive tal cual.
    /// </summary>
    internal static class ConfigFileWriter
    {
        /// <summary>
        /// Aplica las claves al fichero. Devuelve `false` y el motivo si no se pudo escribir;
        /// **nunca lanza**: guardar unos ajustes no puede tumbar la aplicación.
        /// </summary>
        internal static bool SetKeys(string configPath, IEnumerable<KeyValuePair<string, string>> values, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(configPath))
            {
                error = "sin ruta de configuración";
                return false;
            }

            // Se lee entero y se escribe por un temporal: un corte a medias deja el `.config` bueno
            // en su sitio en vez de uno truncado que impide arrancar.
            string temp = configPath + ".new";
            try
            {
                var docs = new XmlDocument { PreserveWhitespace = true };
                if (File.Exists(configPath))
                {
                    docs.Load(configPath);
                }
                else
                {
                    docs.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><appSettings /></configuration>");
                }

                XmlElement root = docs.DocumentElement;
                if (root == null || root.Name != "configuration")
                {
                    error = "el fichero no es un .config";
                    return false;
                }

                XmlElement appSettings = root["appSettings"];
                if (appSettings == null)
                {
                    appSettings = docs.CreateElement("appSettings");
                    root.AppendChild(appSettings);
                }

                foreach (KeyValuePair<string, string> pair in values)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key)) continue;

                    XmlElement node = null;
                    foreach (XmlNode candidate in appSettings.ChildNodes)
                    {
                        var element = candidate as XmlElement;
                        if (element != null
                            && element.Name == "add"
                            && string.Equals(element.GetAttribute("key"), pair.Key, StringComparison.Ordinal))
                        {
                            node = element;
                            break;
                        }
                    }

                    if (node == null)
                    {
                        node = docs.CreateElement("add");
                        node.SetAttribute("key", pair.Key);
                        appSettings.AppendChild(node);
                    }

                    node.SetAttribute("value", pair.Value ?? string.Empty);
                }

                using (var writer = new XmlTextWriter(temp, new UTF8Encoding(false)))
                {
                    writer.Formatting = Formatting.Indented;
                    docs.Save(writer);
                }

                if (File.Exists(configPath)) File.Delete(configPath);
                File.Move(temp, configPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* mejor esfuerzo */ }
                return false;
            }
        }
    }
}
