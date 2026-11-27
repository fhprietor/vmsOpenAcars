using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// El login del primer arranque contra **`POST {vms_api_url}/api/auth/login`** (implementado en
    /// phpVMS y verificado en vivo el 27/11/2026: un cuerpo vacío devuelve **400**, no 404).
    ///
    /// Sustituye a copiar la `api_key` del perfil: el piloto entra con el usuario y la contraseña que
    /// ya usa en la web y el cliente **recibe la clave**. Lo que se guarda es la clave, **nunca la
    /// contraseña**, y va cifrada con DPAPI (<see cref="ApiKeyStore"/>).
    ///
    /// No rota la clave: si el piloto ya tenía una, phpVMS devuelve la misma y `created` viene en
    /// `false`. Rotarla en cada login dejaría fuera a sus otros dispositivos.
    ///
    /// **Nunca lanza**: devuelve un <see cref="LoginResult"/> con el motivo, porque un fallo de red en
    /// el arranque no puede tumbar el ACARS.
    /// </summary>
    internal static class AuthService
    {
        private const int TimeoutSeconds = 20;

        internal static async Task<LoginResult> LoginAsync(string baseUrl, string login, string password)
        {
            string endpoint = AuthLoginContract.EndpointFor(baseUrl);
            if (endpoint == null)
            {
                // HTTPS obligatorio: la respuesta entrega una credencial de larga vida. Un http:// en la
                // casilla de la URL es un error de configuración del piloto, no un caso a soportar.
                return new LoginResult
                {
                    Outcome = LoginOutcome.Unavailable,
                    Detail = "la URL tiene que empezar por https://"
                };
            }

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            {
                return new LoginResult
                {
                    Outcome = LoginOutcome.InvalidCredentials,
                    Detail = "faltan el usuario o la contraseña"
                };
            }

            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) })
                using (var content = new StringContent(
                           AuthLoginContract.BuildRequest(login, password), Encoding.UTF8, "application/json"))
                using (HttpResponseMessage response = await http.PostAsync(endpoint, content).ConfigureAwait(false))
                {
                    string body = null;
                    try { body = await response.Content.ReadAsStringAsync().ConfigureAwait(false); }
                    catch { /* sin cuerpo: el código de estado ya dice lo que hay que decir */ }

                    return AuthLoginContract.Parse((int)response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                // Aquí queda TODO excepto la contraseña: el mensaje de la excepción habla de TLS, DNS o
                // tiempo de espera, nunca del cuerpo.
                return new LoginResult
                {
                    Outcome = LoginOutcome.Unavailable,
                    Detail = ex.Message
                };
            }
        }
    }
}
