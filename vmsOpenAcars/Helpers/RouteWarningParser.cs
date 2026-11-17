using Newtonsoft.Json.Linq;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Lee el campo `route_warning` del recurso del PIREP, tal como lo publica phpVMS.
    ///
    /// Contrato (mensaje del 16/11/2026, comprobado en el recurso real: la clave ya viaja y hoy vale
    /// `null` en los PIREP limpios):
    /// <code>
    /// "route_warning": { "area": "SKR11 / BARRANQUILLA", "limits": "GND–FL999",
    ///                    "leg": "GIKPU–PADUD", "action": "revisar_ruta" }
    /// </code>
    ///
    /// **Es un dato de fuera y se lee como tal**: cualquier forma inesperada —una lista, un objeto sin
    /// ninguno de los cuatro campos, un texto en vez de un objeto— se resuelve con `null`, que es
    /// exactamente «no hay nada que avisar». Nunca lanza: el prefile no puede romperse por el aviso,
    /// igual que en el servidor no puede fallar por esto. Helper puro, con test.
    /// </summary>
    internal static class RouteWarningParser
    {
        internal static RouteWarning Parse(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return null;

            // Una lista de avisos se degrada a su primero: el contrato habla de un objeto, y el
            // servidor guarda un solo juego de campos (`route_airspace`/`route_limits`/`route_leg`).
            if (token is JArray array)
            {
                foreach (JToken item in array)
                {
                    RouteWarning first = Parse(item);
                    if (first != null) return first;
                }
                return null;
            }

            if (!(token is JObject obj)) return null;

            var warning = new RouteWarning
            {
                Area       = Text(obj["area"]),
                Limits     = Text(obj["limits"]),
                Leg        = Text(obj["leg"]),
                Action     = Text(obj["action"]),
                Type       = Text(obj["type"]),
                FromServer = true,
            };

            // Sin ninguno de los cuatro campos no hay aviso: un `{}` no es un cruce.
            bool any = warning.Area != null || warning.Limits != null
                    || warning.Leg != null  || warning.Action != null;
            return any ? warning : null;
        }

        /// <summary>
        /// El texto de un campo, o `null`. Se recorta porque el servidor puede mandar `" "` y un aviso
        /// con el área en blanco es peor que no tenerlo: la UI pinta «?» y no se puede comprobar nada.
        /// </summary>
        private static string Text(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Object
                || token.Type == JTokenType.Array)
                return null;

            string value = token.ToString()?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
