using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Services
{
    /// <summary>Cómo acabó el ciclo de la credencial de NavData en esta sesión.</summary>
    internal enum NavDataKeyOutcome
    {
        /// <summary>Todavía no se ha pedido nada (ni hacía falta).</summary>
        Idle,

        /// <summary>El `.config` ya traía `navdata_api_key`: no se pregunta a phpVMS.</summary>
        Configured,

        /// <summary>Sobre pedido, abierto y guardado en memoria.</summary>
        Delivered,

        /// <summary>`400 navdata-unsupported-cipher`: el servidor no habla el cifrado pedido.</summary>
        UnsupportedCipher,

        /// <summary>`401`: clave ausente/inválida o piloto no `ACTIVE`. No se reintenta.</summary>
        Unauthorized,

        /// <summary>`503 navdata-not-configured`: el staff no la ha configurado. El vuelo sigue sin NavData.</summary>
        NotConfigured,

        /// <summary>Fallo de red, respuesta ilegible o sobre que no abre. Se degrada sin NavData.</summary>
        Failed,
    }

    /// <summary>
    /// Pide a phpVMS la credencial de NavData **una vez por sesión** y la deja en memoria.
    ///
    /// El porqué de que esto exista: la `navdata_api_key` **estaba publicada** en el `.config` que se
    /// descarga del gestor de ficheros. Ahora ese `.config` la deja vacía y phpVMS la entrega en un
    /// sobre cifrado (`GET {vms_api_url}/api/navdata` con `X-API-KEY` — **no** `Authorization: Bearer`,
    /// que no funciona) derivando la clave de cifrado de la `vms_api_key` del piloto, el único secreto
    /// ya compartido. phpVMS **no hace de proxy**: entrega `{url, key}` y a partir de ahí se habla
    /// directo con NavData.
    ///
    /// Reglas de convivencia con el servicio (contrato de phpVMS, §6):
    /// - **30/min por piloto** → **una petición por sesión**; el resto se sirve de memoria.
    /// - **`401`** (clave inválida o piloto no `ACTIVE`) y **`400`** (`unsupported-cipher`) son
    ///   deterministas: **no se reintentan en bucle**, se marca la sesión sin credencial.
    /// - **`503 navdata-not-configured`** → el vuelo sigue **sin scoring NavData**, como hoy sin clave.
    /// - **La clave no se registra jamás**: ni en el log del ACARS, ni en telemetría, ni en el `.config`.
    ///   Lo único que se puede registrar es el `key_id`, que no es secreto.
    /// </summary>
    internal static class NavDataKeyProvider
    {
        private static readonly object Gate = new object();
        private static Task<bool> _inflight;
        private static bool _attempted;     // una petición por sesión: sin bucles
        private static bool _unavailable;   // 401/400/503/fallo: esta sesión se queda sin NavData

        /// <summary>Cómo acabó el intento, para que la UI pueda decir algo sin tocar la clave.</summary>
        internal static NavDataKeyOutcome LastOutcome { get; private set; } = NavDataKeyOutcome.Idle;

        /// <summary>Último `key_id` entregado. **No es secreto**: identifica la clave, no la revela.</summary>
        internal static string LastKeyId { get; private set; }

        /// <summary>`true` si la `url` del sobre coincide con `navdata_api_url` del `.config`.</summary>
        internal static bool UrlMatchesConfigured { get; private set; }

        /// <summary>Credencial en uso: la del `.config` o, si no hay, la del sobre en memoria.</summary>
        internal static string ApiKey => AppConfig.NavDataApiKeyEffective;

        /// <summary>
        /// Asegura que hay credencial utilizable. Se llama **al arrancar la sesión**, antes del primer
        /// uso de NavData. Devuelve `true` si hay clave; `false` significa «esta sesión va sin NavData»
        /// y **el vuelo no se resiente**: los filtros de plausibilidad y el scoring degradan sin datos.
        /// </summary>
        internal static Task<bool> EnsureAsync()
        {
            NavDataEnvelope current = NavDataKeyState.Current;

            NavDataKeyAction action = NavDataKeyPolicy.Decide(
                AppConfig.NavDataApiKeyConfigured,
                current != null,
                current?.ExpiresAtUtc,
                _attempted,
                DateTime.UtcNow);

            switch (action)
            {
                case NavDataKeyAction.UseConfiguredKey:
                    LastOutcome = NavDataKeyOutcome.Configured;
                    return Task.FromResult(true);

                case NavDataKeyAction.UseCachedEnvelope:
                    return Task.FromResult(true);

                case NavDataKeyAction.GiveUp:
                    return Task.FromResult(false);

                default:
                    return RequestOnce();
            }
        }

        /// <summary>
        /// Lanza la petición como mucho una vez: quien llegue mientras está en vuelo comparte el mismo
        /// `Task`. `_attempted` se marca **antes** de salir a la red, así que un fallo tampoco se
        /// reintenta después.
        /// </summary>
        private static Task<bool> RequestOnce()
        {
            lock (Gate)
            {
                if (_inflight != null) return _inflight;
                _attempted = true;
                _inflight = FetchAsync();
                return _inflight;
            }
        }

        private static async Task<bool> FetchAsync()
        {
            try
            {
                string baseUrl = AppConfig.VmsApiUrl;
                string ikm     = AppConfig.VmsApiKey;

                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrEmpty(ikm))
                {
                    _unavailable = true;
                    LastOutcome  = NavDataKeyOutcome.Failed;
                    return false;
                }

                string url = baseUrl.TrimEnd('/') + "/api/navdata";

                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    // X-API-KEY es la autenticación de phpVMS para este endpoint: `Authorization: Bearer`
                    // NO funciona (dicho por phpVMS y comprobado en vivo: 401).
                    request.Headers.TryAddWithoutValidation("X-API-KEY", ikm);
                    request.Headers.TryAddWithoutValidation("X-NavData-Cipher", NavDataCipher.CipherName);
                    // phpVMS pide `User-Agent: vmsOpenACars/<versión>` para poder auditar quién pide el
                    // sobre; el de por defecto de HttpClient es el que .NET quiera y no sirve.
                    request.Headers.TryAddWithoutValidation(
                        "User-Agent", "vmsOpenACars/" + Core.Helpers.AppInfo.Version);

                    using (var response = await client.SendAsync(request).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            // navdata-not-configured: el staff no la ha configurado. No es un error
                            // nuestro y no se insiste en esta sesión.
                            _unavailable = true;
                            LastOutcome  = NavDataKeyOutcome.NotConfigured;
                            return false;
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            // Clave inválida o piloto no ACTIVE: determinista, no se reintenta.
                            _unavailable = true;
                            LastOutcome  = NavDataKeyOutcome.Unauthorized;
                            return false;
                        }

                        if ((int)response.StatusCode == 400)
                        {
                            _unavailable = true;
                            LastOutcome  = NavDataKeyOutcome.UnsupportedCipher;
                            return false;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            _unavailable = true;
                            LastOutcome  = NavDataKeyOutcome.Failed;
                            return false;
                        }

                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return HandleSuccess(json, ikm);
                    }
                }
            }
            catch
            {
                _unavailable = true;
                LastOutcome  = NavDataKeyOutcome.Failed;
                return false;
            }
        }

        /// <summary>Interpreta `{data:{…payload…}}` y deja la credencial en memoria.</summary>
        private static bool HandleSuccess(string json, string ikm)
        {
            JToken data = JObject.Parse(json)["data"];
            if (data == null)
            {
                _unavailable = true;
                LastOutcome  = NavDataKeyOutcome.Failed;
                return false;
            }

            string cipher = data["cipher"]?.ToString();
            if (!string.IsNullOrEmpty(cipher) &&
                !string.Equals(cipher, NavDataCipher.CipherName, StringComparison.OrdinalIgnoreCase))
            {
                // El servidor contesta con un cifrado que no sabemos abrir: mejor no usar nada que
                // fingir que hay credencial.
                _unavailable = true;
                LastOutcome  = NavDataKeyOutcome.UnsupportedCipher;
                return false;
            }

            if (!NavDataCipher.TryOpen(data["payload"]?.ToString(), ikm,
                                       out NavDataEnvelope envelope, out string error))
            {
                // Ojo: `error` nunca lleva la clave; el mensaje es del contrato del sobre.
                Debug.WriteLine("NavData: el sobre no abre (" + error + ")");
                _unavailable = true;
                LastOutcome  = NavDataKeyOutcome.Failed;
                return false;
            }

            // `key_id` distinto al de la sesión anterior ⇒ la caché de NavData quedó servida con la
            // credencial vieja. Se purga antes de usarla.
            NavDataCache.Initialize();
            string previousKeyId = NavDataCache.LastNavDataKeyId;
            if (NavDataKeyPolicy.CacheMustBePurged(previousKeyId, envelope.KeyId))
            {
                NavDataCache.PurgeAirportData();
                NavDataClient.ClearMemoryCache();
            }
            NavDataCache.StoreNavDataKeyId(envelope.KeyId);

            NavDataKeyState.Store(envelope);

            LastKeyId            = envelope.KeyId;
            UrlMatchesConfigured = NavDataKeyPolicy.UrlMatches(envelope.Url, AppConfig.NavDataApiUrl);
            LastOutcome          = NavDataKeyOutcome.Delivered;

            // Log de diagnóstico SIN la clave: solo qué se entregó y cuándo vence.
            Debug.WriteLine(
                "NavData: credencial entregada por phpVMS (key_id=" + envelope.KeyId +
                ", expira=" + envelope.ExpiresAt + ", clave de " + envelope.Key.Length + " caracteres)");
            return true;
        }
    }
}
