using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services.Interfaces;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Llama al endpoint de despacho de SimBrief de phpVMS
    /// (`GET {vms_api_url}/api/flights/{id}/dispatch?aircraft_id={id}`) y devuelve su respuesta ya
    /// parseada. Es una capa fina sobre el cliente autenticado: **no** construye la URL de
    /// SimBrief ni la toca — la decisión de abrirla o no vive en <c>SimbriefDispatchPolicy</c>.
    ///
    /// A diferencia de <see cref="SimbriefEnhancedService"/> (que habla con SimBrief, un tercero, y
    /// a propósito no recibe <see cref="IApiService"/>), este servicio sí habla con phpVMS y por eso
    /// recibe el cliente autenticado a través de la interfaz encapsulada.
    /// </summary>
    public class SimbriefDispatchService
    {
        private readonly IApiService _apiService;

        public SimbriefDispatchService(IApiService apiService)
        {
            _apiService = apiService;
        }

        /// <summary>
        /// Pide el despacho. **Nunca lanza y nunca devuelve null**: si no hay red o el cuerpo no se
        /// puede leer, devuelve un objeto con <c>StatusCode = 0</c> para que la política decida
        /// degradar al constructor local. El código HTTP se conserva siempre porque es lo que
        /// distingue «endpoint no desplegado» de «vuelo inexistente».
        /// </summary>
        public async Task<SimbriefDispatch> FetchAsync(string flightId, string aircraftId)
        {
            var dispatch = new SimbriefDispatch();

            if (string.IsNullOrWhiteSpace(flightId))
                return dispatch; // StatusCode 0 → respaldo

            try
            {
                // `aircraft_id` va como query, tal cual pide el contrato. Si viene vacío se manda
                // igual vacío: el servidor responde 422 aircraft_not_found y eso es un mensaje
                // legítimo para el piloto (no hay avión seleccionado), no un fallo del cliente.
                string path = $"api/flights/{Uri.EscapeDataString(flightId)}/dispatch" +
                              $"?aircraft_id={Uri.EscapeDataString(aircraftId ?? string.Empty)}";

                var (status, body) = await _apiService.GetWithStatusAsync(path);
                dispatch.StatusCode = status;

                if (!string.IsNullOrWhiteSpace(body))
                    ParseInto(dispatch, body);
            }
            catch
            {
                // Red, TLS, JSON corrupto: se queda en StatusCode 0 = «no hubo respuesta».
                dispatch.StatusCode = 0;
                dispatch.Url = null;
            }

            return dispatch;
        }

        /// <summary>
        /// Vuelca el cuerpo JSON en el DTO. **Puro** (sin red ni WinForms) para poder fijarlo con
        /// una respuesta real del servidor en los tests. Si un campo falta se deja en null/vacío:
        /// degradar sin datos, nunca inventar. Un cuerpo que no sea JSON no rompe nada — el
        /// llamante ya tiene el código HTTP para decidir.
        /// </summary>
        internal static void ParseInto(SimbriefDispatch dispatch, string body)
        {
            if (dispatch == null || string.IsNullOrWhiteSpace(body)) return;

            JObject json;
            try { json = JObject.Parse(body); }
            catch { return; }

            // `error` es una cadena en los errores de negocio (`{"error":"aircraft_not_found"}`)
            // pero un objeto en el 401 de Laravel (`{"error":{"code":"401",...}}`). Se acepta
            // cualquiera de las dos formas para no perder el código.
            var error = json["error"];
            if (error != null && error.Type == JTokenType.String)
                dispatch.ErrorCode = error.ToString();
            else if (error != null && error.Type == JTokenType.Object)
                dispatch.ErrorCode = error["code"]?.ToString() ?? error["message"]?.ToString();

            // `route` (nivel superior) es {dpt, arr} y NO la ruta de navegación: esa va en
            // `simbrief.params.route` y solo aparece si el vuelo la tiene. Aquí solo se lee la URL,
            // que es lo único que el cliente abre.
            dispatch.Url          = json["simbrief"]?["url"]?.ToString();
            dispatch.Applicable   = json["applicable"]?.Value<bool?>() ?? true;
            dispatch.Reason       = json["reason"]?.ToString();
            dispatch.FlightNumber = json["flight_number"]?.ToString();
            dispatch.AircraftType = json["aircraft_type"]?.ToString();
            dispatch.Registration = json["registration"]?.ToString();
            dispatch.RouteCode    = json["route_code"]?.ToString();

            var suggestion = json["suggestion"];
            if (suggestion != null && suggestion.Type == JTokenType.Object)
            {
                dispatch.Suggestion = new SimbriefDispatchSuggestion
                {
                    Pax           = suggestion["pax"]?.Value<int?>(),
                    Cargo         = suggestion["cargo"]?.Value<double?>(),
                    Revenue       = suggestion["revenue"]?.Value<double?>(),
                    Target        = suggestion["target"]?.Value<double?>(),
                    MarginPct     = suggestion["margin_pct"]?.Value<double?>(),
                    TargetReached = suggestion["target_reached"]?.Value<bool?>(),
                };
            }

            var notes = json["notes"] as JArray;
            if (notes != null)
            {
                dispatch.Notes = new List<SimbriefDispatchNote>();
                foreach (var note in notes)
                {
                    string text = note["text"]?.ToString();
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    dispatch.Notes.Add(new SimbriefDispatchNote
                    {
                        Level = note["level"]?.ToString(),
                        Text  = text,
                    });
                }
            }
        }
    }
}
