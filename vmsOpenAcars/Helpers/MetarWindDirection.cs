using System;
using System.Globalization;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La dirección del viento de `/weather/{icao}/` no siempre es un número.**
    ///
    /// Medido el 10/11/2026 a las 03:36 UTC en SKRG: el servicio devuelve `"wind_dir":"VRB"`
    /// —viento variable— en el mismo campo en el que otros METAR traen un entero. `NavWeather.WindDir`
    /// estaba tipado `int?`, así que Json.NET lanzaba
    /// `Could not convert string to integer: VRB. Path 'wind_dir'` y `GetWeatherAsync` caía en su
    /// `catch`: devolvía la entrada vieja de la caché —el METAR de las 0200Z— sin llegar a guardar
    /// nunca el de las 0300Z. Reproducido con el JSON real de esa consulta: el parseo fallaba
    /// **siempre**, no solo al pulsar REFRESH. Por eso la caducidad por observación sola no habría
    /// bastado.
    ///
    /// La regla: **`VRB` es «sin dirección fija» y se guarda como `null`**, nunca como 0 —0 es
    /// norte, y el METAR ya distingue `00000KT` (calma)—. Un valor no numérico tampoco inventa un
    /// rumbo, y fuera de 0..360 se descarta. Degrada sin dato; no bloquea la lectura del resto del
    /// METAR, que es lo que el piloto mira.
    /// </summary>
    internal static class MetarWindDirection
    {
        /// <summary>
        /// Grados de la dirección del viento, o `null` si es variable (`VRB`), viene vacío o no es un
        /// número utilizable.
        /// </summary>
        internal static int? Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string value = raw.Trim();
            if (string.Equals(value, "VRB", StringComparison.OrdinalIgnoreCase)) return null;

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int degrees))
                return null;

            // 360 y 0 son la misma dirección (norte); el METAR puede publicar cualquiera de las dos.
            return (degrees >= 0 && degrees <= 360) ? degrees : (int?)null;
        }
    }
}
