using System;
using System.Text.RegularExpressions;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// URL de las teselas del mapa: **el proxy de NavData** como camino normal y CARTO directo como
    /// respaldo.
    ///
    /// Por qué el proxy (medido el 30/09/2026): CARTO ya no sirve las teselas sin clave, y lo que
    /// devuelve **no es un mapa marcado, es un cartel** —`dark_all/14/4736/8200.png` sin clave son
    /// 2.513 B que dicen «API KEY REQUIRED · carto.com/basemaps/apikey»—. Como los pilotos no llevan
    /// clave de CARTO, el mapa **solo funciona** pidiéndoselas a NavData, que las cachea y las sirve
    /// a nombre de la aerolínea.
    ///
    /// La clave de NavData viaja **en la URL** (`?key=`) y no en cabecera porque GMap.NET construye la
    /// petición de teselas por dentro (`GetTileImageUsingHttp`) y **no permite añadir cabeceras**.
    /// NavData lo sabe y admite esa forma **solo en la ruta de teselas**: en cualquier otra ruta, sin
    /// cabecera, sigue devolviendo 401. De ahí:
    ///
    ///     {navdata_api_url}tiles/{style}/{z}/{x}/{y}.png?key=…&origin_domain=…
    ///
    /// **La clave no se escribe en ningún log**: para registrar la URL, <see cref="Mask"/>.
    /// </summary>
    internal static class CartoTileUrl
    {
        /// <summary>Teselas con las que no se pudo servir nada: ni el proxy ni CARTO.</summary>
        internal static int TileFailures;

        /// <summary>
        /// Veces que el proxy falló y sirvió el respaldo directo de CARTO. Es el número que NavData
        /// pidió: **si sube mucho, el proxy no está cumpliendo**. Se reinicia con cada arranque.
        /// </summary>
        internal static int ProxyFallbacks;

        /// <summary>
        /// Teselas servidas por CARTO **sin clave**, o sea el cartel «API KEY REQUIRED» y no un mapa.
        /// Se coteja con el contador `placeholder` de NavData: si los dos suben a la vez, el problema
        /// está aguas arriba; si solo sube el nuestro, el que no cumple es el proxy.
        /// </summary>
        internal static int PlaceholderTiles;

        /// <summary>
        /// Base del proxy: `navdata_api_url` + `tiles`, o el valor explícito de `tile_proxy_url` si
        /// está configurado. Vacía si no hay ninguna de las dos cosas —entonces se va directo a CARTO,
        /// que es el comportamiento anterior a v0.9.18—. Pura, con test.
        /// </summary>
        internal static string ProxyBase(string navDataApiUrl, string overrideUrl)
        {
            string b = string.IsNullOrWhiteSpace(overrideUrl) ? navDataApiUrl : overrideUrl;
            if (string.IsNullOrWhiteSpace(b)) return "";

            b = b.Trim().TrimEnd('/');
            if (b.Length == 0) return "";

            // Si ya apunta a la ruta de teselas se respeta; si es la base de la API (…/api/v1) se le
            // añade `tiles`, que es lo que documenta NavData.
            return b.EndsWith("/tiles", StringComparison.OrdinalIgnoreCase) ? b : b + "/tiles";
        }

        /// <summary>
        /// URL de una tesela del proxy, con la clave y el dominio de origen. Devuelve cadena vacía si
        /// no hay base o estilo (el llamante entonces no lo intenta). Pura, con test.
        /// </summary>
        internal static string ProxyTile(string proxyBase, string style, int zoom, long x, long y,
                                         string navDataKey, string originDomain)
        {
            if (string.IsNullOrWhiteSpace(proxyBase) || string.IsNullOrWhiteSpace(style)) return "";

            string url = WithKey(proxyBase.TrimEnd('/') + "/" + style + "/" + zoom + "/" + x + "/" + y + ".png",
                                 navDataKey);
            if (!string.IsNullOrWhiteSpace(originDomain) &&
                url.IndexOf("origin_domain=", StringComparison.Ordinal) < 0)
            {
                url += (url.IndexOf('?') >= 0 ? "&" : "?") + "origin_domain=" + Uri.EscapeDataString(originDomain.Trim());
            }
            return url;
        }

        /// <summary>La URL con la clave tapada, para poder registrarla sin filtrarla. Pura, con test.</summary>
        internal static string Mask(string url)
        {
            if (string.IsNullOrEmpty(url)) return url ?? "";
            return Regex.Replace(url, "(key=)[^&]*", "$1***");
        }

        /// <summary>
        /// La URL de la tesela con la clave añadida. Sin clave configurada devuelve la URL tal cual:
        /// el llamante decide entonces si le sirve (para el proxy no, porque responde 401).
        /// </summary>
        internal static string WithKey(string url, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (string.IsNullOrWhiteSpace(apiKey)) return url;

            string separator = url.IndexOf('?') >= 0 ? "&" : "?";
            return url + separator + "key=" + Uri.EscapeDataString(apiKey.Trim());
        }
    }
}
