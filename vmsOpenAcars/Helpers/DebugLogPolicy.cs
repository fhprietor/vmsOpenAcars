using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Qué líneas del log son depuración y cuándo se ocultan de la pantalla del piloto.**
    ///
    /// El log del vuelo mezcla dos cosas muy distintas: los eventos que el piloto tiene que leer
    /// (fases, toque, combustible de rodaje, penalizaciones, avisos del RAAS) y los volcados que el
    /// cliente escribe para diagnosticar los addons. Los segundos son imprescindibles cuando algo
    /// falla —la línea `🛠` del panel del PMDG fue la que localizó los fallos de la 0.9.74—, pero en
    /// un vuelo normal son decenas por hora y entierran lo importante: el PIREP `ez9OqzRnYxbnGLR8`
    /// acumuló una línea `↳ TWY …, Next …` cada pocos segundos durante todo el rodaje.
    ///
    /// **El filtro es solo de la línea visible.** `UIService.AddLog` sigue espejando **todo** al log
    /// del PIREP (`AcarsLogMirror`) y sigue disparando las notificaciones: lo que se oculta es lo que
    /// se pinta en la pantalla del piloto, nunca lo que se guarda. Si se filtrara también el espejo,
    /// el volcado que sirve para diagnosticar un addon desaparecería justo cuando hace falta.
    ///
    /// **Regla de oro para clasificar:** si el piloto no puede actuar sobre la línea y no describe un
    /// evento del vuelo, es depuración. Por eso nada que empiece por `⚠️`, `✅`, `──`, `🛬`, `⛽`,
    /// `🎙️`, `🎯`, `🅿️`, etc. puede ocultarse, y una línea sin letras ni dígitos (un separador) se
    /// trata siempre como visible.
    /// </summary>
    internal static class DebugLogPolicy
    {
        /// <summary>
        /// **La bandera del espejo al PIREP**, y el literal exacto que phpVMS puede filtrar (por
        /// ejemplo con `LIKE '[DBG] %'` o un `starts_with`). Va **solo** en la copia que se manda al
        /// servidor: la línea que se pinta en la pantalla del piloto no la lleva. Es constante y no
        /// pasa por `_()` a propósito: el filtro del backend no puede depender del idioma.
        /// </summary>
        internal const string DebugPrefix = "[DBG] ";

        /// <summary>
        /// **La lista cerrada de volcados**, con el porqué de cada uno. Cualquier línea que no
        /// empiece por uno de estos prefijos se muestra siempre.
        /// </summary>
        internal static readonly string[] DebugPrefixes =
        {
            // El panel crudo del addon: banderas del piloto automático, carteles de cabina y
            // autobrake (`FlightManager.LogPanelDiagnostics`, al entrar en crucero y aproximación).
            "🛠",

            // El chatter del "Next" del rodaje (`Lnm_TaxiPosition`/`Lnm_TaxiwayChange`): decenas por
            // vuelo, cambia con cada intersección y no pide ninguna acción.
            "↳ ",

            // Los diagnósticos del monitor de reversa: el aviso de que se está vigilando, el de que
            // el addon no publica el offset, y el máximo del rodaje (que en `ez9OqzRnYxbnGLR8`
            // publicó un 100 %/100 % falso 22 s después del toque).
            "Reversa: monitoreando",
            "Reversa: no detectada",
            "Reversa: máximo del rodaje",

            // El volcado de tiempos de ralentí y OAT al entrar en pista
            // (`Motor: ENG1 idle 291s [STAB ✓] … OAT 16°C`). El evento que importa —«motores no
            // estabilizados»— va aparte y empieza por `⚠️`.
            "Motor: ",

            // El código de estado que devuelve phpVMS (`📊 PIREP Status: TXI`): es la respuesta del
            // servidor, no algo que el piloto pueda corregir.
            "📊 PIREP Status: ",
        };

        /// <summary>
        /// **Lo que nunca se oculta.** La comprobación va antes que la de depuración para que un
        /// prefijo nuevo no pueda tapar por accidente un evento o una penalización.
        /// </summary>
        internal static readonly string[] ProtectedPrefixes =
        {
            "⚠️", "✅", "──", "🛬", "⛽", "🎙️", "🎯", "🅿️",
            "🛫", "🔄", "✖️", "❌", "⛔", "ℹ️",
        };

        /// <summary>
        /// ¿Es una línea de depuración? Se decide **por el texto**, sin estado y sin configuración,
        /// para que la clasificación sea la misma con el ajuste encendido o apagado (el ajuste solo
        /// decide si se pinta). Sin texto no es depuración.
        /// </summary>
        internal static bool IsDebug(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            // Si ya viene con la bandera del PIREP, se clasifica por lo que hay detrás: así la
            // clasificación es estable y `ForPirep` no puede prefijar dos veces.
            string text = StripDebugFlag(message.TrimStart());

            // La regla de oro manda sobre la lista: nada con evento o penalización se oculta.
            foreach (string prefix in ProtectedPrefixes)
                if (text.StartsWith(prefix, StringComparison.Ordinal))
                    return false;

            // Un separador (`────`, `✈️`) no es un volcado: no hay nada que diagnosticar en él.
            if (!HasLetterOrDigit(text)) return false;

            foreach (string prefix in DebugPrefixes)
                if (text.StartsWith(prefix, StringComparison.Ordinal))
                    return true;

            return false;
        }

        /// <summary>
        /// La línea que se manda al PIREP: la misma, con <see cref="DebugPrefix"/> delante cuando es
        /// depuración. **Es el único sitio que compone la bandera** (también la idempotencia: una
        /// línea que ya la lleva no se prefija otra vez).
        /// </summary>
        internal static string ForPirep(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;
            if (!IsDebug(message)) return message;

            string text = message.TrimStart();
            if (text.StartsWith(DebugPrefix, StringComparison.Ordinal)) return message;

            return DebugPrefix + message;
        }

        private static string StripDebugFlag(string text)
            => text.StartsWith(DebugPrefix, StringComparison.Ordinal)
                ? text.Substring(DebugPrefix.Length)
                : text;

        /// <summary>
        /// ¿Se pinta esta línea en el log visible? `true` salvo que sea depuración y el piloto haya
        /// dejado el ajuste apagado. **Sin texto se muestra**: ocultar por accidente es peor que
        /// enseñar una línea de más.
        /// </summary>
        internal static bool ShouldDisplay(bool showDebug, string message)
        {
            if (string.IsNullOrEmpty(message)) return true;
            return showDebug || !IsDebug(message);
        }

        private static bool HasLetterOrDigit(string text)
        {
            foreach (char c in text)
                if (char.IsLetterOrDigit(c)) return true;
            return false;
        }
    }
}
