using System;
using System.ComponentModel;
using System.IO;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Clasifica el fallo al abrir la tubería de FSUIPC**, que hasta v0.9.64 se perdía dentro de un
    /// `catch` y solo dejaba el estado en «desconectado».
    ///
    /// El caso que lo obliga: el simulador o FSUIPC7 corre **elevado** y el ACARS no. Windows (UIPI)
    /// no deja abrir la tubería entre procesos de distinto nivel de integridad y devuelve **acceso
    /// denegado** —sin UAC, sin diálogo, sin nada— así que el piloto veía «simulador desconectado»
    /// igual que si el simulador estuviera cerrado. La diferencia no es cosmética: una se arregla
    /// abriendo el simulador, la otra abriendo el ACARS como administrador.
    ///
    /// Es puro a propósito (sin red, sin WinForms, sin estado): la regla se prueba con excepciones
    /// reales del runtime y el servicio solo decide cuándo preguntarla.
    /// </summary>
    internal static class SimLinkFailure
    {
        /// <summary>
        /// Clave del aviso accionable cuando el fallo es de **permisos** (el simulador/FSUIPC corre
        /// elevado y nosotros no).
        /// </summary>
        internal const string AccessDeniedKey = "SimLink_AccessDenied";

        /// <summary>
        /// Clave del aviso cuando el fallo es **cualquier otro** (simulador cerrado, FSUIPC sin
        /// instalar, tubería ocupada): el ACARS sigue reintentando y el vuelo no se bloquea.
        /// </summary>
        internal const string UnavailableKey = "SimLink_Unavailable";

        /// <summary>
        /// ¿El fallo es el **acceso denegado** de UIPI? Se aceptan las tres formas en que Windows lo
        /// entrega: `UnauthorizedAccessException` directa, o un `IOException`/`Win32Exception` cuyo
        /// mensaje hable de «access is denied» / «acceso denegado» —el texto llega traducido según el
        /// idioma del sistema, por eso se miran las dos cadenas—. También se recorre
        /// `InnerException`, porque la DLL de FSUIPC envuelve el error nativo.
        ///
        /// Sin excepción (`null`) devuelve `false`: no hay nada que clasificar y no puede lanzar.
        /// </summary>
        internal static bool IsAccessDenied(Exception ex)
        {
            if (ex == null) return false;

            if (ex is UnauthorizedAccessException) return true;

            if ((ex is IOException || ex is Win32Exception) && MentionsAccessDenied(ex.Message))
                return true;

            return IsAccessDenied(ex.InnerException);
        }

        /// <summary>
        /// La clave del texto que se enseña al piloto: <see cref="AccessDeniedKey"/> cuando manda la
        /// elevación y <see cref="UnavailableKey"/> para el resto de fallos.
        /// </summary>
        internal static string MessageKeyFor(Exception ex) =>
            IsAccessDenied(ex) ? AccessDeniedKey : UnavailableKey;

        private static bool MentionsAccessDenied(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;

            return message.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("acceso denegado", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
