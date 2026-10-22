using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Qué avisos de cabina se pueden decir en cada fase. Aquí solo está la regla del **final de la
    /// turbulencia**, que es la que se equivocó en el PIREP real `5ow69vzDNW5ZMayB`: el cliente
    /// anunció «hemos salido de la zona, pueden moverse por la cabina» a las 13:19:12, **2 min
    /// después del despegue y a 5 200 ft subiendo a 1 800 fpm**.
    ///
    /// «Pueden moverse por la cabina» es un permiso, y en subida o en descenso no se da: en subida
    /// porque la tripulación está amarrada y no hay servicio, y en descenso porque es justo lo
    /// contrario —«regresen a sus asientos»—. Solo tiene sentido en **crucero**. La entrada en
    /// turbulencia, en cambio, se puede anunciar en cualquier fase en el aire: una salida turbulenta
    /// es de lo más normal.
    /// </summary>
    internal static class CabinCalloutPolicy
    {
        /// <summary>¿Se puede decir «pueden moverse por la cabina» en esta fase?</summary>
        internal static bool AnnouncesTurbulenceCleared(FlightPhase phase) =>
            phase == FlightPhase.Enroute;
    }
}
