using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public ApiService(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
            _apiKey = apiKey;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // Header correcto para phpVMS 7
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);

            // TLS moderno
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls13;
        }

        // =========================================================
        // GET PILOT DATA
        // =========================================================
        public async Task<(Pilot Data, string Error)> GetPilotData()
        {
            try
            {
                // Limpieza de URL por si acaso
                string fullUrl = $"{_baseUrl}api/user";
                var response = await _httpClient.GetAsync(fullUrl);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(content);
                    var data = json["data"];

                    var pilot = new Pilot
                    {
                        Id = (int)(data["id"] ?? 0),
                        PilotId = data["ident"]?.ToString(),
                        Name = data["name"]?.ToString(),
                        AirlineId = (int)(data["airline_id"] ?? 1),
                        Rank = data["rank"]?["name"]?.ToString()
                    };
                    return (pilot, null); // Todo OK
                }

                // Si falló, extraemos el porqué
                string errorDetail = await response.Content.ReadAsStringAsync();
                return (null, $"Server Error ({(int)response.StatusCode} {response.StatusCode}). Check URL: {fullUrl}");
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Network Error: {ex.Message}. Check your internet or if the domain vholar.co is active.");
            }
            catch (Exception ex)
            {
                return (null, $"Unexpected Error: {ex.Message}");
            }
        }
        // =========================================================
        // FETCH SIMBRIEF
        // =========================================================
        public async Task<SimbriefPlan> FetchSimbrief(string pilotId)
        {
            try
            {
                string url = $"https://www.simbrief.com/api/xml.fetcher.php?userid={pilotId}&json=1";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return null;

                string content = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(content);

                return new SimbriefPlan
                {
                    FlightNumber = json["general"]?["flight_number"]?.ToString() ?? "N/A",
                    Airline = json["general"]?["icao_airline"]?.ToString() ?? "N/A",
                    Origin = json["origin"]?["icao_code"]?.ToString() ?? "ZZZZ",
                    Destination = json["destination"]?["icao_code"]?.ToString() ?? "ZZZZ",
                    Route = json["general"]?["route"]?.ToString() ?? "DIRECT",

                    // Aquí estaba el error. Usamos ? para que si "aircraft" es null, no explote.
                    AircraftIcao = json["aircraft"]?["icao_type"]?.ToString() ?? "B58",
                    Aircraft = json["aircraft"]?["icaocode"]?.ToString() ?? "B58",
                    Registration = json["aircraft"]?["registration"]?.ToString() ?? "N-ACARS",

                    // Para números, usamos una verificación de existencia
                    BlockFuel = json["fuel"]?["plan_block"] != null ? (double)json["fuel"]["plan_block"] : 0,
                    PaxCount = json["weights"]?["pax_count"] != null ? (int)json["weights"]["pax_count"] : 0,

                    PlannedAltitude = json["general"]?["planned_altitude"]?.ToString() ?? "10000"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Simbrief Exception: {ex}");
                return null;
            }
        }

        // =========================================================
        // PREFILE FLIGHT (CREATE PIREP IN_PROGRESS)
        // =========================================================
        public async Task<string> PrefileFlight(SimbriefPlan plan, Pilot pilot)
        {
            var payload = new
            {
                airline_id = pilot.AirlineId,
                aircraft_id = 2,
                flight_number = plan.FlightNumber,
                dpt_airport_id = plan.Origin,
                arr_airport_id = plan.Destination,
                route = plan.Route,
                level = plan.PlannedAltitude,
                source_name = "vmsOpenAcars",
                state = "in_progress"
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{_baseUrl}api/pireps/prefile", content);

            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // phpVMS suele devolver errores en formatos variados. 
                // Intentamos extraer el mensaje más legible posible.
                var errorJson = JObject.Parse(result);
                var message = errorJson["message"]?.ToString()
                              ?? errorJson["error"]?["message"]?.ToString()
                              ?? errorJson["title"]?.ToString()
                              ?? result;

                throw new Exception(message);
            }

            var jsonResult = JObject.Parse(result);
            // phpVMS v7 devuelve el ID dentro del objeto "data"
            return jsonResult["data"]?["id"]?.ToString() ?? jsonResult["id"]?.ToString();
        }

        // =========================================================
        // SEND POSITION UPDATE
        // =========================================================
        public async Task<bool> SendPositionUpdate(string pirepId, object telemetry)
        {
            try
            {
                string json = JsonConvert.SerializeObject(telemetry);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}api/pireps/{pirepId}/acars/position", content);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Position Update Error: {error}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Position Update Exception: {ex}");
                return false;
            }
        }

        // =========================================================
        // FINALIZE PIREP
        // =========================================================
        public async Task<bool> FilePirep(string pirepId, object finalData)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{_baseUrl}api/pireps/{pirepId}/file", finalData);

                string result = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("FilePirep Error:");
                    System.Diagnostics.Debug.WriteLine(result);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FilePirep Exception: {ex}");
                return false;
            }
        }
        public async Task MovePilotAsync(string airportIcao)
        {
            var payload = new
            {
                curr_airport_id = airportIcao
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(
                $"{_baseUrl}api/user", content);

            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Move pilot failed: {result}");
            }
        }
    }
}