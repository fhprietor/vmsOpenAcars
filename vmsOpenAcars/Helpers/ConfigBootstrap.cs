using System;
using System.IO;

namespace vmsOpenAcars.Helpers
{
    /// <summary>Cómo acabó el primer arranque, para poder contarlo sin adivinar.</summary>
    internal enum ConfigInstallOutcome
    {
        /// <summary>El piloto ya tenía `vmsOpenACars.exe.config`: no se ha tocado.</summary>
        AlreadyPresent,

        /// <summary>No había config y se ha instalado la plantilla.</summary>
        Installed,

        /// <summary>No había config y tampoco plantilla al lado del `.exe`: falta el paquete.</summary>
        TemplateMissing,

        /// <summary>Se intentó y falló (permisos, disco). Se dice y se sigue.</summary>
        Failed
    }

    /// <summary>
    /// El primer arranque de una instalación limpia (v1.0.0): hasta ahora el piloto que descargaba el
    /// cliente se quedaba **sin `vmsOpenACars.exe.config`** y tenía que bajarse la plantilla aparte
    /// (`App.Release.config`), que hasta v0.9.81 ni siquiera viajaba en el zip.
    ///
    /// Aquí la plantilla se copia a `vmsOpenACars.exe.config` **solo si ese fichero no existe**, que
    /// es la misma regla que hace que el config del piloto sobreviva a las actualizaciones (el
    /// `Updater` copia sin borrar: si viajara el del cliente, lo pisaría en cada una).
    ///
    /// Se ejecuta al principio de `Program.Main`, antes de construir la ventana: `ConfigurationManager`
    /// abre el fichero en el **primer acceso**, no al arrancar, así que no hace falta reiniciar.
    /// </summary>
    internal static class ConfigBootstrap
    {
        internal const string ConfigFileName   = "vmsOpenACars.exe.config";
        internal const string TemplateFileName = "App.Release.config";

        /// <summary>
        /// Copia la plantilla si no hay config. Devuelve cómo fue y, cuando falla, el motivo, para que
        /// el log lo pueda decir. **Nunca lanza**: que no se pueda escribir un config no puede impedir
        /// que la aplicación arranque.
        /// </summary>
        internal static ConfigInstallOutcome InstallIfMissing(string baseDirectory, out string detail)
        {
            detail = null;

            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                detail = "sin directorio base";
                return ConfigInstallOutcome.Failed;
            }

            string configPath;
            string templatePath;
            try
            {
                configPath   = Path.Combine(baseDirectory, ConfigFileName);
                templatePath = Path.Combine(baseDirectory, TemplateFileName);
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return ConfigInstallOutcome.Failed;
            }

            // 1) La regla que no se negocia: si ya hay config, no se toca ni para «mejorarlo».
            try
            {
                if (File.Exists(configPath)) return ConfigInstallOutcome.AlreadyPresent;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return ConfigInstallOutcome.Failed;
            }

            // 2) Sin plantilla no hay nada que instalar: el paquete vino incompleto. Se dice y se sigue,
            //    que el piloto todavía puede configurarlo a mano en Ajustes.
            try
            {
                if (!File.Exists(templatePath))
                {
                    detail = "no está " + TemplateFileName;
                    return ConfigInstallOutcome.TemplateMissing;
                }
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return ConfigInstallOutcome.Failed;
            }

            // 3) Copia **byte a byte** y por un temporal: si se corta a medias no deja un config
            //    corrupto en su sitio (que además ya contaría como «existe» y no se reintentaría nunca).
            string tempPath = configPath + ".tmp";
            try
            {
                File.Copy(templatePath, tempPath, true);
                File.Move(tempPath, configPath);
                return ConfigInstallOutcome.Installed;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* limpieza muda */ }
                return ConfigInstallOutcome.Failed;
            }
        }
    }
}
