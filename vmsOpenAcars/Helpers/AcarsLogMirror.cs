using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Qué líneas del log local NO se reenvían al log del PIREP.**
    ///
    /// `UIService.AddLog` espeja al PIREP (`POST /acars/position`, `type = 2`) **todo** lo que se
    /// escribe en el log de la aplicación mientras hay un PIREP activo. Los avisos del RAAS tienen
    /// además su camino propio —`FlightManager.LogRaasCallout`, con el prefijo `RAAS:` que se acordó
    /// con phpVMS y por `/acars/logs`—, así que cada aviso se registraba **dos veces**. Medido en el
    /// PIREP real `D0Wy9Pa7GNB5pYEo`:
    ///
    /// ```
    /// 01:03:09  RAAS: APPROACHING RUNWAY 26
    /// 01:03:09  🛫 APPROACHING RUNWAY 26
    /// ```
    ///
    /// y lo mismo con los otros tres avisos (`HOLD SHORT`, `OFF ROUTE`, `TAXI ROUTE COMPLETE`).
    ///
    /// **El espejo es el que sobra**, no el camino dedicado: el `RAAS:` es el que phpVMS sabe
    /// distinguir de las filas `CHK` del score. Por eso la marca es el prefijo de la línea local —
    /// `🎙️ RAAS:`— y no el texto del aviso, que cambia con el idioma y con la plantilla.
    ///
    /// **No se calla nada más**: la guía de rodaje, los anuncios de cabina y los avisos que no son un
    /// callout (`⚠️ RAAS: sin voz SAPI…`) siguen espejándose como hasta ahora.
    /// </summary>
    internal static class AcarsLogMirror
    {
        /// <summary>El prefijo de una línea de aviso del RAAS en el log local.</summary>
        internal const string RaasCalloutPrefix = "🎙️ RAAS:";

        /// <summary>
        /// ¿Hay que reenviar esta línea al log del PIREP? `false` solo para el aviso del RAAS, que ya
        /// viaja por su propio camino.
        /// </summary>
        internal static bool ShouldMirror(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            return !message.TrimStart().StartsWith(RaasCalloutPrefix, StringComparison.Ordinal);
        }
    }
}
