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
    /// Hay **una sola** clave: <see cref="AppConfig.NavDataApiKeyEffective"/>, que es la del sobre en
    /// memoria. Sin sobre **no hay clave** (una escrita en el `.config` se ignora) y la petición sale
    /// sin `X-API-Key`, que es lo honesto: el servidor contestará 401 y el log dirá por qué.
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
