using System.Collections.Generic;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Respuesta de `GET {vms_api_url}/api/flights/{id}/dispatch?aircraft_id={id}`, el endpoint que
    /// phpVMS desplegó para que **el servidor** monte la URL de SimBrief y el cliente la abra **tal
    /// cual**. El cliente ya no construye la URL: en cuanto toca un parámetro vuelven a existir dos
    /// versiones del mismo plan, que es exactamente lo que este endpoint elimina.
    ///
    /// Se parsea aquí, en la frontera de la red, y se degrada sin datos: `StatusCode = 0` es «no
    /// hubo respuesta» (red/timeout) y no un error de contrato.
    /// </summary>
    public sealed class SimbriefDispatch
    {
        /// <summary>Código HTTP de la respuesta. **0 = no hubo respuesta** (timeout, DNS, red caída).</summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// `error` del cuerpo cuando el servidor rechaza la petición: `flight_not_found`,
        /// `aircraft_not_found`, `aircraft_not_allowed`… Es lo que distingue un **404 de ruta**
        /// (endpoint no desplegado) de un **404 de vuelo inexistente**.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>`simbrief.url`: la URL que hay que abrir **tal cual**. Null/vacía si no vino.</summary>
        public string Url { get; set; }

        /// <summary>
        /// `applicable`: false en charter/ferry. Hay URL, pero **sin `pax`/`cargo`**: que falte el
        /// sugerido en ese caso es lo esperado, no un error que haya que pintar en rojo.
        /// </summary>
        public bool Applicable { get; set; } = true;

        /// <summary>`reason` del servidor (`ok`, `charter`, `ferry`…): informativo, se registra tal cual.</summary>
        public string Reason { get; set; }

        public string FlightNumber { get; set; }
        public string AircraftType { get; set; }
        public string Registration { get; set; }
        public string RouteCode { get; set; }

        /// <summary>Sugerido del servidor. Null si el vuelo no es aplicable o no vino en el cuerpo.</summary>
        public SimbriefDispatchSuggestion Suggestion { get; set; }

        /// <summary>Avisos **ya redactados** por el servidor: se muestran tal cual, sin reescribir.</summary>
        public List<SimbriefDispatchNote> Notes { get; set; } = new List<SimbriefDispatchNote>();
    }

    /// <summary>
    /// `suggestion` del despacho. Son los números que el servidor ya calculó (reparto de pasaje y
    /// carga contra el objetivo de ingresos): el cliente **no recalcula** nada, solo los pinta.
    /// </summary>
    public sealed class SimbriefDispatchSuggestion
    {
        public int?    Pax             { get; set; }
        public double? Cargo           { get; set; }
        public double? Revenue         { get; set; }
        public double? Target          { get; set; }
        public double? MarginPct       { get; set; }
        public bool?   TargetReached   { get; set; }
    }

    /// <summary>Un aviso del servidor: `level` ∈ ok|warn|danger|info y el texto ya redactado.</summary>
    public sealed class SimbriefDispatchNote
    {
        public string Level { get; set; }
        public string Text  { get; set; }
    }
}
