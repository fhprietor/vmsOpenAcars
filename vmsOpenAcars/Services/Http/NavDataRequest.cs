using System.Net.Http;
using System.Threading.Tasks;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Services.Http
{
    /// <summary>
    /// Peticiones contra NavData con la credencial puesta **en el momento de la petición**.
    ///
    /// El porqué: hasta ahora `X-API-Key` viajaba como cabecera por defecto de
    /// <see cref="HttpClientProvider.NavData"/>, leída del `.config` **al arrancar**. Con el cambio de
    /// phpVMS esa clave ya no está en el `.config`: se pide a `GET /api/navdata` y **llega después del
    /// arranque** (una vez por sesión). Y `HttpClient.DefaultRequestHeaders` **no se puede tocar
    /// después** de la primera petición: lanza `InvalidOperationException`. Así que la única forma de
    /// que el vuelo use la clave entregada es ponerla en cada petición.
    ///
    /// No hay dos claves: <see cref="AppConfig.NavDataApiKeyEffective"/> es la del `.config` si viene
    /// —comportamiento de siempre, para quien la tenga— y, si no, la del sobre en memoria.
    /// </summary>
    internal static class NavDataRequest
    {
        /// <summary>
        /// Añade la credencial a una petición ya construida. <paramref name="keyOverride"/> sirve al
        /// botón TEST de Settings, que valida una clave escrita y todavía no guardada.
        /// </summary>
        internal static void ApplyAuth(HttpRequestMessage request, string keyOverride = null)
        {
            string key = keyOverride ?? AppConfig.NavDataApiKeyEffective;
            if (!string.IsNullOrEmpty(key))
                request.Headers.TryAddWithoutValidation("X-API-Key", key);
        }

        /// <summary>Envía una petición ya construida con la credencial puesta.</summary>
        internal static async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, string keyOverride = null)
        {
            ApplyAuth(request, keyOverride);
            return await HttpClientProvider.NavData.SendAsync(request).ConfigureAwait(false);
        }

        /// <summary>GET autenticado. El contenido se lee completo antes de devolver, así que la
        /// petición se puede liberar aquí sin dejar el cuerpo a medias.</summary>
        internal static async Task<HttpResponseMessage> GetAsync(string url)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                return await SendAsync(request).ConfigureAwait(false);
        }
    }
}
