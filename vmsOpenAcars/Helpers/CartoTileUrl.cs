using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// URL de tesela de CARTO con su API key.
    ///
    /// CARTO retiró el acceso sin clave a `basemaps.cartocdn.com`: sin `?key=…` las teselas siguen
    /// llegando, pero **marcadas con el aviso «API key required»** (el mapa se ve, con la marca de
    /// agua encima). El formato lo documenta CARTO y es el mismo endpoint de siempre con un
    /// parámetro de consulta:
    ///
    ///     https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png?key=YOUR_KEY
    ///
    /// La clave se pide gratis (sin cuenta) en <https://carto.com/basemaps/apikey/>; el plan libre
    /// son 5 millones de teselas al mes para uso no comercial.
    ///
    /// **Trampa de las apps de escritorio**: si la clave se crea con *restricciones de web*, CARTO
    /// responde **403** —y el mapa sale en blanco— porque un cliente de escritorio no envía la
    /// cabecera `Referer`. Esta clave debe crearse **sin restricción de web**; si se quiere
    /// restringir, hay que fijar además `GMapProvider.RefererUrl` al dominio autorizado.
    /// </summary>
    internal static class CartoTileUrl
    {
        /// <summary>
        /// La URL de la tesela con la clave añadida. Sin clave configurada devuelve la URL tal cual,
        /// que es como estaba antes de esto: el mapa se ve con la marca de agua, no se rompe.
        /// </summary>
        /// <summary>
        /// Teselas que **no** se pudieron traer (CARTO o el proxy devolvieron error o vacío). Es el
        /// contador que NavData nos pidió: **si sube mucho, el proxy no está cumpliendo** y hay que
        /// decírselo. Se reinicia con cada arranque.
        /// </summary>
        internal static int TileFailures;

        internal static string WithKey(string url, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (string.IsNullOrWhiteSpace(apiKey)) return url;

            string separator = url.IndexOf('?') >= 0 ? "&" : "?";
            return url + separator + "key=" + Uri.EscapeDataString(apiKey.Trim());
        }
    }
}
