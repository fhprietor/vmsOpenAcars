using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>De dónde sale la URL que se abre en el navegador.</summary>
    internal enum SimbriefDispatchSource
    {
        /// <summary>La `simbrief.url` que devolvió el servidor: se abre **tal cual**, sin tocarla.</summary>
        ServerUrl,

        /// <summary>El constructor local (`SimbriefEnhancedService.GenerateDispatchUrl`), como respaldo.</summary>
        LocalFallback,

        /// <summary>No se abre nada: el servidor dijo «no» y hay que decírselo al piloto.</summary>
        Rejected
    }

    /// <summary>Decisión completa, con el motivo técnico para el log y la clave de idioma a mostrar.</summary>
    internal sealed class SimbriefDispatchDecision
    {
        public SimbriefDispatchSource Source  { get; set; }
        /// <summary>Clave de idioma a mostrar cuando <see cref="Source"/> es <c>Rejected</c>; null si no hay error.</summary>
        public string               ErrorKey  { get; set; }
        /// <summary>Por qué se decidió esto, en corto y en técnico: es lo que va al log.</summary>
        public string               Reason    { get; set; }
    }

    /// <summary>
    /// Decide si se abre la URL del servidor o el constructor local, y por qué. Puro: sin red, sin
    /// WinForms, sin estado — recibe el resultado de la llamada y solo razona.
    ///
    /// La regla de fondo: **si el endpoint contesta, manda él**. Aunque el constructor local
    /// funcione, usar el suyo es lo que elimina la segunda versión de la URL (parámetros que se
    /// quedan atrás: `maps=detailed` en vez de `detail`, `static_url` que no existe, `fl` en pies o
    /// en centenas de pie…). El constructor local vuelve a existir **solo** cuando no hubo
    /// respuesta: servidor viejo, 5xx, timeout o red.
    ///
    /// El caso que obliga a mirar el cuerpo y no solo el código es el **404**: el 404 de una ruta
    /// que no existe (servidor viejo) y el `404 flight_not_found` del endpoint desplegado son el
    /// mismo código HTTP. Se distinguen por `error` del cuerpo: sin `flight_not_found` es que la
    /// ruta no está desplegada → respaldo; con él, el vuelo ya no existe → se le dice al piloto.
    ///
    /// Los cuatro errores con mensaje (401, 404 vuelo, 422 avión, 403 subflota) **no** caen al
    /// respaldo: el fallback construiría una URL con el avión equivocado y el piloto planificaría
    /// un vuelo que la aerolínea le va a rechazar. Se muestran y se para.
    /// </summary>
    internal static class SimbriefDispatchPolicy
    {
        // Claves de idioma de los cuatro errores (es.json / en.json, simétricas).
        internal const string KeyInvalidApiKey       = "Fp_DispatchInvalidKey";
        internal const string KeyFlightNotFound      = "Fp_DispatchFlightNotFound";
        internal const string KeyAircraftNotFound    = "Fp_DispatchAircraftNotFound";
        internal const string KeyAircraftNotAllowed  = "Fp_DispatchAircraftNotAllowed";

        internal const string ErrorFlightNotFound     = "flight_not_found";
        internal const string ErrorAircraftNotFound   = "aircraft_not_found";
        internal const string ErrorAircraftNotAllowed = "aircraft_not_allowed";

        internal static SimbriefDispatchDecision Decide(SimbriefDispatch dispatch)
        {
            // Sin objeto o sin respuesta (timeout, DNS, red): no hay nada que decidir con el
            // servidor, así que se usa el de siempre. Nunca se lanza.
            if (dispatch == null || dispatch.StatusCode == 0)
                return Fallback("sin_respuesta");

            var d = dispatch;

            if (d.StatusCode == 200)
            {
                // Contesta y trae URL → es SU url, tal cual. Ni se recorta ni se completa.
                if (!string.IsNullOrWhiteSpace(d.Url))
                    return new SimbriefDispatchDecision
                    {
                        Source = SimbriefDispatchSource.ServerUrl,
                        Reason = "servidor"
                    };
                // 200 sin url: el contrato no se cumplió, pero el servidor nuevo no está roto
                // del todo — se degrada al constructor local en vez de dejar al piloto sin plan.
                return Fallback("respuesta_200_sin_url");
            }

            if (d.StatusCode == 401)
                return Rejected(KeyInvalidApiKey, "401_clave_invalida");

            if (d.StatusCode == 404)
            {
                // Mismo código, dos mundos: `flight_not_found` es el endpoint desplegado
                // diciendo que el vuelo ya no está; cualquier otro 404 es la ruta que no
                // existe porque phpVMS todavía no tiene el endpoint.
                return string.Equals(d.ErrorCode, ErrorFlightNotFound,
                                     System.StringComparison.OrdinalIgnoreCase)
                    ? Rejected(KeyFlightNotFound, "404_flight_not_found")
                    : Fallback("404_endpoint_no_desplegado");
            }

            if (d.StatusCode == 422)
                return string.Equals(d.ErrorCode, ErrorAircraftNotFound,
                                     System.StringComparison.OrdinalIgnoreCase)
                    ? Rejected(KeyAircraftNotFound, "422_aircraft_not_found")
                    : Fallback("422_sin_codigo_conocido");

            if (d.StatusCode == 403)
                return string.Equals(d.ErrorCode, ErrorAircraftNotAllowed,
                                     System.StringComparison.OrdinalIgnoreCase)
                    ? Rejected(KeyAircraftNotAllowed, "403_aircraft_not_allowed")
                    : Fallback("403_sin_codigo_conocido");

            // 5xx y cualquier código no contemplado: fallo del servidor, se cae al respaldo.
            // Degradar es la regla del repo: si falta el dato, deciden las otras reglas; aquí,
            // el plan lo arma el constructor local con lo que el cliente ya tenía.
            return Fallback("http_" + d.StatusCode);
        }

        private static SimbriefDispatchDecision Fallback(string reason)
            => new SimbriefDispatchDecision { Source = SimbriefDispatchSource.LocalFallback, Reason = reason };

        private static SimbriefDispatchDecision Rejected(string key, string reason)
            => new SimbriefDispatchDecision { Source = SimbriefDispatchSource.Rejected, ErrorKey = key, Reason = reason };
    }
}
