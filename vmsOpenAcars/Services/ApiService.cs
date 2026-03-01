using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using vmsOpenAcars.Models;
using Newtonsoft.Json.Linq; // Asegúrate de tener este using arriba

namespace vmsOpenAcars.Services
{
    public class ApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public ApiService(string baseUrl, string apiKey)
        {
            // Limpiar y asegurar la URL base
            _baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
            _apiKey = apiKey;

            // Configuración de Headers para phpVMS 7
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // phpVMS 7 suele usar 'x-api-key'
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);

            // Timeout preventivo para conexiones via VPN/Wireguard o StarLink
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // TLS para Cloudflare
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
        }
        /// <summary>
        /// Valida la conexión y obtiene datos del piloto logueado
        /// </summary>
        public async Task<string> GetPilotData()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}api/user");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();

                return null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// Obtiene el plan de vuelo activo desde SimBrief
        /// </summary>
        public async Task<SimbriefPlan> FetchSimbrief(string pilotId)
        {
            try
            {
                string url = $"https://www.simbrief.com/api/xml.fetcher.php?userid={pilotId}&json=1";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode) return null;

                string content = await response.Content.ReadAsStringAsync();

                // Usamos JObject para tener control total de la jerarquía
                JObject json = JObject.Parse(content);

                // Accedemos usando ["nombre"] que es más seguro que el punto
                return new SimbriefPlan
                {
                    FlightNumber = json["general"]?["flight_number"]?.ToString(),
                    Airline = json["general"]?["icao_airline"]?.ToString(),
                    Origin = json["origin"]?["icao_code"]?.ToString(),
                    Destination = json["destination"]?["icao_code"]?.ToString(),
                    Route = json["general"]?["route"]?.ToString(),
                    AircraftIcao = json["aircraft"]?["icaocode"]?.ToString(),
                    Registration = json["aircraft"]?["registration"]?.ToString(),
                    BlockFuel = (double)(json["fuel"]?["plan_block"] ?? 0),
                    PaxCount = (int)(json["weights"]?["pax_count"] ?? 0),
                    PlannedAltitude = json["general"]?["planned_altitude"]?.ToString()
                };
            }
            catch (Exception ex)
            {
                // Esto te ayudará a ver en la consola si algo falló
                System.Diagnostics.Debug.WriteLine($"Simbrief Error: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Registra el vuelo en phpVMS (Cambia el estado a 'Boarding')
        /// </summary>
        public async Task<string> PrefileFlight(SimbriefPlan plan)
        {
                // El objeto que phpVMS 7 espera para un Prefile
                var prefileData = new
                {
                    airline_id = 1, // Esto deberías obtenerlo del perfil del piloto
                    aircraft_id = 1, // ID interno en tu base de datos de phpVMS
                    flight_number = plan.FlightNumber,
                    dpt_airport_id = plan.Origin,
                    arr_airport_id = plan.Destination,
                    route = plan.Route,
                    level = plan.PlannedAltitude,
                    source_name = "vmsOpenAcars"
                };

                var json = JsonConvert.SerializeObject(prefileData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}api/pireps/prefile", content);

                if (response.IsSuccessStatusCode)
                {
                    // phpVMS devuelve el objeto PIREP creado, necesitamos el ID para futuros updates
                    string result = await response.Content.ReadAsStringAsync();
                    dynamic pirep = JsonConvert.DeserializeObject(result);
                    return (string)pirep.data.id;
                }
                else
                {
                    string errorJson = await response.Content.ReadAsStringAsync();
                    try
                    {
                        JObject errorData = JObject.Parse(errorJson);
                        // phpVMS suele enviar el error en 'message' o 'details'
                        string errorMessage = errorData["message"]?.ToString() ?? errorData["details"]?.ToString() ?? "Unknown API Error";

                        // Lanzamos una excepción con el mensaje real del servidor
                        throw new Exception(errorMessage);
                    }
                    catch (JsonException)
                    {
                        throw new Exception($"Server returned {response.StatusCode}");
                    }
                }
        }

        /// <summary>
        /// Envía actualizaciones de posición (Live Map)
        /// </summary>
        public async Task<bool> SendPositionUpdate(string pirepId, object telemetry)
        {
            /*
            try
            {
                var json = JsonConvert.SerializeObject(telemetry);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}api/pireps/{pirepId}/updates", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
            */
            try
            {
                string json = JsonConvert.SerializeObject(telemetry);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // IMPORTANTE: La ruta debe ser api/pireps/{id}/updates
                // var response = await _httpClient.PostAsync($"{_baseUrl}api/pireps/{pirepId}/updates", content);
                var response = await _httpClient.PostAsync($"{_baseUrl}api/pireps/{pirepId}/acars/position", content);
                if (!response.IsSuccessStatusCode)
                {
                    // Mira esto en el Output de Visual Studio para ver por qué falla
                    string error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API ERROR] Update Failed: {error}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NET ERROR] {ex.Message}");
                return false;
            }
        }
    }
}