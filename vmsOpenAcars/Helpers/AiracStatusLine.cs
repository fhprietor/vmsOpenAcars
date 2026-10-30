using System;
using System.Globalization;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La fecha de vigencia del AIRAC, tal como se pinta en la línea de arranque.**
    ///
    /// El ciclo solo no dice nada útil: «AIRAC 2611 — activo» no contesta la pregunta que se hace el
    /// piloto («¿hasta cuándo me sirve esta carta?»). Ese dato ya viaja en `airac_valid_until` y el
    /// cliente lo recibía sin enseñarlo.
    ///
    /// **El formato de salida es `yyyy-MM-dd` por una razón, no por gusto.** NavData no fija el
    /// formato del campo: puede llegar `2026-11-12` o un instante ISO completo (`2026-11-12T00:00:00Z`),
    /// así que el valor se parsea **de forma tolerante**. Pero lo que se imprime no puede quedar en
    /// manos de `DateTime.ToString()` ni de la cultura del equipo: `12/11/2026` es el 12 de noviembre
    /// en España y el 11 de diciembre en Estados Unidos, y ese mismo texto se lee desde los dos lados
    /// del Atlántico. `2026-11-12` se lee igual en todas partes.
    ///
    /// **Degrada sin dato, nunca lanza**: si el campo falta, llega vacío o no parsea, devuelve cadena
    /// vacía y el que llama elige la variante de la línea sin fecha (`Log_NavDataApiOkNoDate`). Es la
    /// misma regla que los filtros de plausibilidad: sin dato no se bloquea nada —aquí, no se imprime
    /// «hasta » con la fecha vacía— y la otra regla decide.
    /// </summary>
    internal static class AiracStatusLine
    {
        /// <summary>Formato inequívoco de la fecha: cuatro dígitos, mes y día a dos.</summary>
        internal const string DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// Devuelve la fecha de vigencia del AIRAC como `yyyy-MM-dd`, o cadena vacía si el dato falta
        /// o no se puede parsear.
        ///
        /// Se usa <see cref="DateTimeOffset"/> y no <see cref="DateTime"/> a propósito: con `DateTime`
        /// y `DateTimeStyles.None`, un `2026-11-12T00:00:00Z` se convierte a la hora local del equipo y
        /// en un huso al oeste de Greenwich devuelve **2026-11-11** —un día antes del real—. El
        /// `DateTimeOffset` conserva el offset que trae el texto, así que la fecha impresa es la misma
        /// en cualquier máquina.
        /// </summary>
        internal static string FormatValidUntil(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            DateTimeOffset parsed;
            if (!DateTimeOffset.TryParse(raw.Trim(), CultureInfo.InvariantCulture,
                                         DateTimeStyles.None, out parsed))
                return string.Empty;

            return parsed.ToString(DateFormat, CultureInfo.InvariantCulture);
        }
    }
}
