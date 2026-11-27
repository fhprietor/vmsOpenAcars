using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace vmsOpenAcars.Helpers
{
    /// <summary>Cómo acabó un intento de login contra `POST /api/auth/login`.</summary>
    internal enum LoginOutcome
    {
        Ok,
        InvalidCredentials,   // 401
        AccountNotActive,     // 403
        TooManyAttempts,      // 429
        BadRequest,           // 400/422: el cuerpo no le gustó (no debería pasar si enviamos bien)
        Unavailable           // red, TLS, 5xx, respuesta ilegible
    }

    /// <summary>Lo que devuelve el login, ya interpretado.</summary>
    internal sealed class LoginResult
    {
        internal LoginOutcome Outcome { get; set; }
        internal string ApiKey { get; set; }
        /// <summary>`true` si phpVMS acaba de crear la clave (primer login del piloto).</summary>
        internal bool Created { get; set; }
        /// <summary>`ident` del piloto (`VHR017`), para saludarlo por su nombre si viene.</summary>
        internal string PilotIdent { get; set; }
        internal string PilotName { get; set; }
        /// <summary>Texto del error para el log/la pantalla, sin credenciales dentro.</summary>
        internal string Detail { get; set; }
    }

    /// <summary>
    /// **El contrato del login**, aislado y con test: lo que se envía, lo que se acepta y de dónde se
    /// lee. Los números y el formato están verificados contra el servicio real el 27/11/2026
    /// (`POST https://vholar.co/api/auth/login` con `{}` responde **400**, no 404: el endpoint existe).
    ///
    /// Tres reglas que no son de estilo:
    ///
    /// 1. **La contraseña no se registra nunca**, ni en el `Detail` de un fallo ni en el log.
    /// 2. **La `api_key` que llega no se registra tampoco**: es una credencial de larga vida.
    /// 3. **`created`** viene en la respuesta para poder decirle al piloto «se ha creado tu clave» o
    ///    registrar el evento, sin deducirlo del `200`.
    /// </summary>
    internal static class AuthLoginContract
    {
        internal const string Path = "/api/auth/login";

        /// <summary>El cuerpo que se manda. `{ "login": ..., "password": ... }`.</summary>
        internal static string BuildRequest(string login, string password)
        {
            var body = new JObject
            {
                ["login"] = login ?? string.Empty,
                ["password"] = password ?? string.Empty
            };
            return body.ToString(Formatting.None);
        }

        /// <summary>
        /// El login solo puede ir por **HTTPS**: la respuesta entrega una credencial de larga vida.
        /// Un `http://` en la casilla de la URL es un error de configuración, no un caso a soportar.
        /// </summary>
        internal static bool IsSecureUrl(string baseUrl)
        {
            Uri uri;
            if (!Uri.TryCreate((baseUrl ?? string.Empty).Trim(), UriKind.Absolute, out uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttps;
        }

        /// <summary>La URL del endpoint, o `null` si la base no sirve (no es absoluta o no es HTTPS).</summary>
        internal static string EndpointFor(string baseUrl)
        {
            if (!IsSecureUrl(baseUrl)) return null;
            return (baseUrl ?? string.Empty).Trim().TrimEnd('/') + Path;
        }

        /// <summary>
        /// Interpreta la respuesta. **No lanza**: un cuerpo raro es `Unavailable`, no una excepción en
        /// mitad del arranque.
        /// </summary>
        internal static LoginResult Parse(int statusCode, string body)
        {
            var result = new LoginResult();

            if (statusCode == 200)
            {
                string apiKey = null;
                bool created = false;
                string ident = null;
                string name = null;

                try
                {
                    JObject root = string.IsNullOrWhiteSpace(body) ? null : JObject.Parse(body);
                    if (root != null)
                    {
                        apiKey = (string)root["api_key"];
                        created = root["created"] != null && root["created"].Type == JTokenType.Boolean
                                  && (bool)root["created"];
                        JObject user = root["user"] as JObject;
                        if (user != null)
                        {
                            ident = (string)user["ident"];
                            name = (string)user["name"];
                        }
                    }
                }
                catch
                {
                    apiKey = null; // respuesta ilegible: se trata como no válida
                }

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    result.Outcome = LoginOutcome.Unavailable;
                    result.Detail = "200 sin api_key legible";
                    return result;
                }

                result.Outcome = LoginOutcome.Ok;
                result.ApiKey = apiKey.Trim();
                result.Created = created;
                result.PilotIdent = ident;
                result.PilotName = name;
                return result;
            }

            switch (statusCode)
            {
                case 401:
                    result.Outcome = LoginOutcome.InvalidCredentials;
                    result.Detail = FirstMessage(body) ?? "credenciales incorrectas";
                    break;
                case 403:
                    result.Outcome = LoginOutcome.AccountNotActive;
                    result.Detail = FirstMessage(body) ?? "cuenta no activa";
                    break;
                case 429:
                    result.Outcome = LoginOutcome.TooManyAttempts;
                    result.Detail = FirstMessage(body) ?? "demasiados intentos";
                    break;
                case 400:
                case 422:
                    result.Outcome = LoginOutcome.BadRequest;
                    result.Detail = FirstMessage(body) ?? "petición rechazada";
                    break;
                default:
                    result.Outcome = LoginOutcome.Unavailable;
                    result.Detail = "HTTP " + statusCode;
                    break;
            }

            return result;
        }

        /// <summary>El mensaje del cuerpo si phpVMS manda uno (`message`/`error`), sin volcar el JSON.</summary>
        private static string FirstMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                JObject root = JObject.Parse(body);
                string text = (string)root["message"] ?? (string)root["error"];
                if (string.IsNullOrWhiteSpace(text)) return null;
                return text.Length > 200 ? text.Substring(0, 200) : text;
            }
            catch { return null; }
        }
    }
}
