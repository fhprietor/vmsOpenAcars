// ApiService.cs
using System;
using System.Collections.Generic;
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

        public HttpClient HttpClient => _httpClient;
        public string BaseUrl => _baseUrl;

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
        public async Task<bool> UpdatePirep(string pirepId, object data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Usar PUT para actualizar
                var response = await _httpClient.PutAsync(
                    $"{_baseUrl}api/pireps/{pirepId}", content);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error updating PIREP: {error}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception updating PIREP: {ex}");
                return false;
            }
        }
        public async Task<string> GetNearestAirport(double latitude, double longitude)
        {
            try
            {
                string url = $"{_baseUrl}api/airports/nearest?lat={latitude}&lon={longitude}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json)["data"];
                    return data?["icao"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting nearest airport: {ex}");
            }

            return null;
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
                        Rank = data["rank"]?["name"]?.ToString(),
                        CurrentAirport = data["curr_airport"]?.ToString()
                    };
                    // Si tenemos el aeropuerto, obtener sus coordenadas (opcional)
                    if (!string.IsNullOrEmpty(pilot.CurrentAirport))
                    {
                        await LoadAirportCoordinates(pilot);
                    }
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

        private async Task LoadAirportCoordinates(Pilot pilot)
        {
            try
            {
                string url = $"{_baseUrl}api/airports/{pilot.CurrentAirport}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var airportData = json["data"];

                    pilot.CurrentAirportLat = airportData?["lat"]?.Value<double>();
                    pilot.CurrentAirportLon = airportData?["lon"]?.Value<double>();
                    // Log temporal
                    System.Diagnostics.Debug.WriteLine($"Coordenadas cargadas: {pilot.CurrentAirportLat}, {pilot.CurrentAirportLon}");
                }
                else
                {
                    // Fallback local
                    SetFallbackCoordinates(pilot);
                }
            }
            catch
            {
                SetFallbackCoordinates(pilot);
            }
        }

        private void SetFallbackCoordinates(Pilot pilot)
        {
            // Coordenadas de aeropuertos comunes de Colombia
            var coords = new Dictionary<string, (double lat, double lon)>
            {
                ["SKBO"] = (4.7011, -74.1469),
                ["SKBG"] = (7.1265, -73.1848),
                ["SKRG"] = (6.1718, -75.4221),
                ["SKCL"] = (3.5439, -76.3816),
                ["SKBQ"] = (10.8896, -74.7808),
                ["SKCG"] = (10.4425, -75.5131)
            };

            if (coords.TryGetValue(pilot.CurrentAirport, out var c))
            {
                pilot.CurrentAirportLat = c.lat;
                pilot.CurrentAirportLon = c.lon;
            }
        }
        // En ApiService.cs

        public async Task<bool> DeletePirep(string pirepId)
        {
            try
            {
                var response = await _httpClient.PutAsync($"{_baseUrl}api/pireps/{pirepId}/cancel", null);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    // Log para depuración
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error cancelando PIREP: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excepción cancelando PIREP: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> FilePirep(string pirepId, object finalData)
        {
            try
            {
                string json = JsonConvert.SerializeObject(finalData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}api/pireps/{pirepId}/file", content);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error filing PIREP: {response.StatusCode} - {error}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception filing PIREP: {ex}");
                return false;
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
                aircraft_id = 2, // Esto debería venir del plan o configuración
                flight_number = plan.FlightNumber,
                dpt_airport_id = plan.Origin,
                arr_airport_id = plan.Destination,
                route = plan.Route,
                level = plan.PlannedAltitude,

                // ===== NUEVOS CAMPOS =====
                planned_distance = Math.Round(plan.Distance, 2),
                planned_flight_time = plan.EstTimeEnroute / 60, // Convertir segundos a minutos
                block_fuel = Math.Round(plan.BlockFuel, 0),
                distance = 0, // Inicia en 0
                flight_time = 0, // Inicia en 0
                fuel_used = 0, // Inicia en 0
                submitted_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),

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
                var errorJson = JObject.Parse(result);
                var message = errorJson["message"]?.ToString()
                              ?? errorJson["error"]?["message"]?.ToString()
                              ?? errorJson["title"]?.ToString()
                              ?? result;
                throw new Exception(message);
            }

            var jsonResult = JObject.Parse(result);
            return jsonResult["data"]?["id"]?.ToString() ?? jsonResult["id"]?.ToString();
        }

        // =========================================================
        // SEND POSITION UPDATE
        // =========================================================
        public async Task<bool> SendPositionUpdate(string pirepId, object telemetry)
        {
            try
            {
                string json = JsonConvert.SerializeObject(telemetry, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore // Ignorar campos null
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}api/pireps/{pirepId}/acars/position", content);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Position Update Error: {error}");

                    // Intentar parsear el error para más detalles
                    try
                    {
                        var errorJson = JObject.Parse(error);
                        System.Diagnostics.Debug.WriteLine($"Error details: {errorJson}");
                    }
                    catch { }

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