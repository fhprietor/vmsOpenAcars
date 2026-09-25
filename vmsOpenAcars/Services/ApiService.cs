using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Models;
using vmsOpenAcars.Core.Helpers;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Services.Interfaces;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Provides communication with the phpVMS API for all server-side operations.
    /// Handles authentication, PIREP management, position updates, and airport data retrieval.
    /// </summary>
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        /// <summary>
        /// Cliente HTTP autenticado contra phpVMS, para operaciones avanzadas sobre el
        /// mismo host. NO debe usarse contra hosts de terceros: lleva la API key del
        /// piloto en las cabeceras por defecto. Para terceros usar
        /// <see cref="Services.Http.HttpClientProvider"/> (Simbrief/Metar/Ivao/General).
        /// </summary>
        public HttpClient HttpClient => _httpClient;

        /// <summary>
        /// Gets the base URL of the phpVMS installation.
        /// </summary>
        public string BaseUrl => _baseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiService"/> class.
        /// </summary>
        /// <param name="baseUrl">The base URL of the phpVMS installation (e.g., "https://vholar.co/").</param>
        /// <param name="apiKey">The API key for authentication (x-api-key header).</param>
        public ApiService(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
            _apiKey = apiKey;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // phpVMS 7 uses x-api-key header for authentication
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);

            // Enable modern TLS protocols
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls13;
        }
        #region BID Management

        /// <summary>
        /// Updates an existing PIREP with new data using HTTP PUT.
        /// </summary>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        /// <summary>
        /// Obtiene las reservas (bids) del piloto actualmente autenticado
        /// </summary>
        // En ApiService.cs
        public async Task<List<Flight>> GetPilotBids()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}api/user/bids");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var data = json["data"] as JArray;
                    var flights = new List<Flight>();

                    if (data != null)
                    {
                        foreach (var item in data)
                        {
                            JObject flightData = null;

                            try
                            {
                                flightData = item["flight"] as JObject;
                                if (flightData == null) continue;

                                // Distancia en millas náuticas
                                double nmDistance = flightData["distance"]?["nmi"]?.Value<double?>() ?? 0;

                                // Tipos de aeronave permitidos
                                var allowedTypes = new List<string>();
                                var subfleets = flightData["subfleets"] as JArray;
                                if (subfleets != null)
                                {
                                    foreach (var sub in subfleets)
                                    {
                                        var type = sub["type"]?.ToString();
                                        if (!string.IsNullOrEmpty(type))
                                            allowedTypes.Add(type);
                                    }
                                }
                                // Aeronave reservada
                                string bidAircraftId = item["aircraft_id"]?.ToString() ?? "";
                                // Aerolínea
                                string airlineCode = "VHR";
                                var airline = flightData["airline"] as JObject;
                                if (airline != null)
                                {
                                    airlineCode = airline["icao"]?.ToString() ?? airlineCode;
                                }

                                // Número de vuelo (numérico)
                                string flightNumber = flightData["flight_number"]?.ToString() ?? "";

                                // Callsign (el identificador real del vuelo)
                                string callsign = flightData["callsign"]?.ToString() ?? "";

                                // Tipo de vuelo: J = Programado, C = Charter, P = Posicionamiento/Ferry
                                string flightType = flightData["flight_type"]?.ToString() ?? "J";

                                // Para vuelos charter (C) o ferry (P), usar callsign en lugar de flight_number
                                string displayFlightNumber;
                                if (flightType == "C" || flightType == "P")
                                {
                                    displayFlightNumber = callsign; // Ej: "55CH"
                                }
                                else
                                {
                                    displayFlightNumber = $"{flightNumber}"; // Ej: "1234"
                                }

                                // Tiempo de vuelo
                                int flightTime = flightData["flight_time"]?.Value<int?>() ?? 0;

                                // Nivel
                                int level = flightData["level"]?.Value<int?>() ?? 0;

                                flights.Add(new Flight
                                {
                                    Id = flightData["id"]?.ToString(),
                                    FlightNumber = displayFlightNumber, // Usamos el valor calculado
                                    Airline = airlineCode,
                                    Departure = flightData["dpt_airport_id"]?.ToString(),
                                    Arrival = flightData["arr_airport_id"]?.ToString(),
                                    AllowedAircraftTypes = allowedTypes,
                                    AllowedAircraftTypesDisplay = string.Join("/", allowedTypes),
                                    Distance = nmDistance,
                                    FlightTime = flightTime,
                                    Route = flightData["route"]?.ToString() ?? "",
                                    Level = level,
                                    BidId = item["id"]?.ToString(),
                                    AircraftId = bidAircraftId,
                                    FlightType = flightType,
                                });
                            }
                            catch { }
                        }
                    }
                    return flights;
                }
                return new List<Flight>();
            }
            catch
            {
                return new List<Flight>();
            }
        }

        /// <summary>
        /// Obtiene el ID del bid para un vuelo específico
        /// </summary>
        public async Task<string> GetBidIdForFlight(string flightId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}api/user/bids");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var data = json["data"] as JArray;

                    if (data != null)
                    {
                        foreach (var item in data)
                        {
                            var flight = item["flight"] as JObject;
                            if (flight != null && flight["id"]?.ToString() == flightId)
                            {
                                return item["id"]?.ToString();
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
        public async Task<bool> DeleteBid(string bidId)
        {
            try
            {
                var payload = new { bid_id = bidId };
                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"{_baseUrl}api/user/bids"),
                    Content = content
                };

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region PIREP Management

        /// <summary>
        /// Updates an existing PIREP with new data using HTTP PUT.
        /// </summary>
        /// <param name="pirepId">The ID of the PIREP to update.</param>
        /// <param name="data">An object containing the fields to update.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        public async Task<bool> UpdatePirep(string pirepId, object data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{_baseUrl}api/pireps/{pirepId}", content);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes/cancels a PIREP using the dedicated cancel endpoint.
        /// </summary>
        /// <param name="pirepId">The ID of the PIREP to cancel.</param>
        /// <returns>True if cancellation was successful; otherwise, false.</returns>
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
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Files a completed PIREP, submitting it for approval/processing.
        /// </summary>
        /// <param name="pirepId">The ID of the PIREP to file.</param>
        /// <param name="finalData">Object containing final flight data (fuel used, landing rate, flight time, etc.).</param>
        /// <returns>True if filing was successful; otherwise, false.</returns>
        public async Task<bool> FilePirep(string pirepId, object finalData)
        {
            try
            {
                string json = JsonConvert.SerializeObject(finalData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}api/pireps/{pirepId}/file", content);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Prefile un vuelo y devuelve el ID del PIREP y la hora de creación del servidor
        /// </summary>
        public async Task<(string pirepId, DateTime serverCreatedAt)> PrefileFlight(SimbriefPlan plan, Pilot pilot)
        {
            string appVersion = AppInfo.Version;
            var payload = new
            {
                airline_id = pilot.AirlineId,
                aircraft_id = plan.AircraftId,
                flight_number = plan.FlightNumber,
                flight_id = plan.FlightId,
                dpt_airport_id = plan.Origin,
                arr_airport_id = plan.Destination,
                alt_airport_id = plan.Alternate ?? "",
                route = plan.Route,
                level = plan.PlannedAltitude,
                planned_distance = Math.Round(plan.Distance, 2),
                planned_flight_time = plan.EstTimeEnroute / 60,
                zfw = Math.Round(plan.ZeroFuelWeight, 0),
                block_fuel = Math.Round(plan.BlockFuel, 0),
                distance = 0,
                flight_time = 0,
                fuel_used = 0,
                notes = SystemInfoHelper.GetPrefileNotes(),
                source_name = $"vmsOpenAcars/{appVersion}",
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
                              ?? result;
                throw new Exception(message);
            }

            var jsonResult = JObject.Parse(result);

            // Obtener el ID del PIREP
            var pirepId = jsonResult["data"]?["id"]?.ToString()
                          ?? jsonResult["id"]?.ToString();

            // Obtener created_at de la respuesta del servidor
            DateTime serverCreatedAt = DateTime.UtcNow; // fallback
            var createdAt = jsonResult["data"]?["created_at"]?.ToString();
            if (!string.IsNullOrEmpty(createdAt))
            {
                if (DateTime.TryParse(createdAt, out DateTime parsedDate))
                {
                    serverCreatedAt = parsedDate;
                }
            }

            return (pirepId, serverCreatedAt);
        }

        /// <summary>
        /// Obtiene los detalles completos de un PIREP por ID.
        /// Usado para recuperar datos de vuelo al reanudar tras reconexión.
        /// </summary>
        public async Task<Models.Pirep> GetPirepDetail(string pirepId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}api/pireps/{pirepId}");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                var item = JObject.Parse(content)["data"];
                if (item == null) return null;

                return new Models.Pirep
                {
                    Id = item["id"]?.ToString(),
                    FlightNumber = item["flight_number"]?.ToString(),
                    Origin = item["dpt_airport_id"]?.ToString(),
                    Destination = item["arr_airport_id"]?.ToString(),
                    AircraftId = item["aircraft_id"]?.ToString(),
                    CreatedAt = item["created_at"]?.ToString(),
                    SubmittedAt = item["submitted_at"]?.ToString(),
                    UpdatedAt = item["updated_at"]?.ToString(),
                    Status = item["status"]?.ToString(),
                    // `state` es el campo numérico que indica si el PIREP sigue activo o
                    // fue archivado. Se dejaba sin leer, y el fallback de FilePirep()
                    // acababa comparando `status` (un código de fase ACARS, no numérico)
                    // contra "1"/"6", de modo que cualquier respuesta no nula se
                    // interpretaba como "PIREP archivado". Si el campo no viene, se usa
                    // Pirep.UnknownState para no confundirlo con InProgress = 0.
                    State = item["state"]?.Value<int?>() ?? Models.Pirep.UnknownState,
                    // Objetos anidados — extraer el campo kg / nmi según corresponda
                    BlockFuel = item["block_fuel"]?["kg"]?.Value<double>() ?? 0,
                    FuelUsed = item["fuel_used"]?["kg"]?.Value<double>() ?? 0,
                    Distance = item["distance"]?["nmi"]?.Value<double>() ?? 0,

                    // Escalar directo
                    FlightTime = item["flight_time"]?.Value<int>() ?? 0,

                    // AircraftType viene de aircraft.icao (no subfleet.type)
                    AircraftType = item["aircraft"]?["icao"]?.ToString() ?? "",
                };
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// Obtiene los PIREPs activos (en progreso) del piloto autenticado
        /// </summary>
        public async Task<List<Models.Pirep>> GetActivePireps()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}api/user/pireps");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var data = json["data"] as JArray;
                    var activePireps = new List<Models.Pirep>();

                    if (data != null)
                    {
                        foreach (var item in data)
                        {
                            var state = item["state"]?.Value<int>();
                            // state 0 = in_progress (activo)
                            if (state == 0)
                            {
                                activePireps.Add(new Models.Pirep
                                {
                                    Id = item["id"]?.ToString(),
                                    FlightNumber = item["flight_number"]?.ToString(),
                                    Origin = item["dpt_airport_id"]?.ToString(),
                                    Destination = item["arr_airport_id"]?.ToString(),
                                    AircraftId = item["aircraft_id"]?.ToString(),
                                    State = state ?? 0,
                                    CreatedAt = item["created_at"]?.ToString(),
                                    SubmittedAt = item["submitted_at"]?.ToString(),
                                    UpdatedAt = item["updated_at"]?.ToString(),

                                    // Objetos anidados — extraer el campo kg / nmi según corresponda
                                    BlockFuel = item["block_fuel"]?["kg"]?.Value<double>() ?? 0,
                                    FuelUsed = item["fuel_used"]?["kg"]?.Value<double>() ?? 0,
                                    Distance = item["distance"]?["nmi"]?.Value<double>() ?? 0,

                                    // Escalar directo
                                    FlightTime = item["flight_time"]?.Value<int>() ?? 0,

                                    // AircraftType viene de aircraft.icao (no subfleet.type)
                                    AircraftType = item["aircraft"]?["icao"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                    return activePireps;
                }
                return new List<Models.Pirep>();
            }
            catch
            {
                return new List<Models.Pirep>();
            }
        }

        /// <summary>
        /// Cancela/elimina un PIREP por ID usando el endpoint de cancelación
        /// </summary>
        public async Task<bool> DeletePirepById(string pirepId)
        {
            try
            {
                // Usar DELETE api/pireps/{pirep_id}/cancel (también funciona PUT)
                var response = await _httpClient.DeleteAsync($"{_baseUrl}api/pireps/{pirepId}/cancel");

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region ACARS Updates

        /// <summary>
        /// Sends a position update (telemetry) for an active flight to the ACARS endpoint.
        /// </summary>
        /// <param name="pirepId">The ID of the active PIREP.</param>
        /// <param name="telemetry">Object containing position data (lat, lon, alt, speed, etc.).</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        public async Task<bool> SendPositionUpdate(string pirepId, object telemetry)
        {
            try
            {
                string json = JsonConvert.SerializeObject(telemetry, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore // Ignore null fields
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}api/pireps/{pirepId}/acars/position", content);

                if (!response.IsSuccessStatusCode)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Retrieves the ACARS history for a PIREP — the recorded positions and the SCH log
        /// entries — ordered chronologically, oldest first.
        /// </summary>
        public async Task<List<Models.AcarsPosition>> GetPirepAcarsAsync(string pirepId)
        {
            try
            {
                // /acars/position es la única ruta que existe: /acars responde 404 en esta
                // instalación (`The route api/pireps/{id}/acars could not be found`), y como el
                // fallo se devolvía vacío, el resume desde el historial nunca recibía nada.
                // Verificado con el PIREP MNjR664PBAr25RbD: 967 entradas y 1,8 MB en ~2 s.
                var response = await _httpClient.GetAsync(
                    $"{_baseUrl}api/pireps/{pirepId}/acars/position");
                if (!response.IsSuccessStatusCode) return new List<Models.AcarsPosition>();

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                // phpVMS returns { "data": [ ... ] }
                var dataToken = json["data"];
                if (dataToken == null) return new List<Models.AcarsPosition>();

                var entries = dataToken.ToObject<List<Models.AcarsPosition>>()
                              ?? new List<Models.AcarsPosition>();

                // La API NO devuelve el historial en orden cronológico: entrega primero todos los
                // CHK y después las posiciones, y dentro de cada grupo tampoco respeta la hora
                // (en ese PIREP los 239 CHK venían al principio con horas cruzadas). Los
                // consumidores asumen "el más nuevo al final" —el resume restaura el último CHK y
                // muestra los últimos 20 apuntes—, así que sin ordenar aquí se restauraba el
                // checkpoint de las 22:43 en vez del de las 02:52. Se ordena en la frontera de la
                // API, una sola vez, para que ese contrato sea cierto.
                return entries
                    .OrderBy(a => a.created_at ?? DateTime.MinValue)
                    .ToList();
            }
            catch
            {
                return new List<Models.AcarsPosition>();
            }
        }

        #endregion

        #region Pilot & Airport Data

        /// <summary>
        /// Retrieves the authenticated pilot's data from the phpVMS API.
        /// </summary>
        /// <returns>A tuple containing the Pilot object and an error message (if any).</returns>
        public async Task<(Pilot Data, string Error)> GetPilotData()
        {
            try
            {
                string fullUrl = $"{_baseUrl}api/user";
                var response = await _httpClient.GetAsync(fullUrl);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(content);
                    var data = json["data"];

                    var pilot = new Pilot
                    {
                        Id             = (int)(data["id"] ?? 0),
                        PilotId        = data["ident"]?.ToString(),
                        Name           = data["name"]?.ToString(),
                        AirlineId      = (int)(data["airline_id"] ?? 1),
                        Rank           = data["rank"]?["name"]?.ToString(),
                        CurrentAirport = data["curr_airport"]?.ToString(),
                        IvaoId         = int.TryParse(data["ivao_id"]?.ToString(), out int ivaoVid) ? ivaoVid : 0,
                        AirlineCountry = data["airline"]?["country"]?.ToString() ?? "",
                        AircraftSeats  = (int)(data["curr_aircraft"]?["subfleet"]?["total_seats"] ?? 0),
                    };

                    // Load airport coordinates if available
                    if (!string.IsNullOrEmpty(pilot.CurrentAirport))
                    {
                        await LoadAirportCoordinates(pilot);
                    }
                    return (pilot, null);
                }

                string errorDetail = await response.Content.ReadAsStringAsync();
                return (null, $"Server Error ({(int)response.StatusCode} {response.StatusCode}). Check URL: {fullUrl}");
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Network Error: {ex.Message}. Check your internet or if the domain is active.");
            }
            catch (Exception ex)
            {
                return (null, $"Unexpected Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the latitude and longitude coordinates for the pilot's current airport.
        /// </summary>
        /// <param name="pilot">The pilot object to update with coordinates.</param>
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
                }
                else
                {
                    SetFallbackCoordinates(pilot);
                }
            }
            catch
            {
                SetFallbackCoordinates(pilot);
            }
        }

        /// <summary>
        /// Provides fallback coordinates for common Colombian airports when the API fails.
        /// </summary>
        /// <param name="pilot">The pilot object to update with fallback coordinates.</param>
        private void SetFallbackCoordinates(Pilot pilot)
        {
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

        #endregion

        #region Accesos genéricos encapsulados

        // Rutas de phpVMS que no tienen método propio (listados paginados de vuelos y
        // flota, asignación de bids). Encapsularlas aquí permite que los servicios de
        // dominio trabajen contra phpVMS sin recibir el HttpClient autenticado.

        /// <summary>GET autenticado contra phpVMS. Devuelve null si la respuesta no es 2xx.</summary>
        public async Task<string> GetAsync(string path)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}{path.TrimStart('/')}");
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsStringAsync();
            }
            catch { return null; }
        }

        /// <summary>
        /// POST con cuerpo JSON contra phpVMS. Devuelve el código de éxito y el cuerpo
        /// de la respuesta (también en error, para poder reportar el mensaje del
        /// servidor). Body es null si la petición no llegó a completarse.
        /// </summary>
        public async Task<(bool Success, string Body)> PostJsonAsync(string path, object payload)
        {
            try
            {
                var content = new StringContent(
                    JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}{path.TrimStart('/')}", content);
                string body = await response.Content.ReadAsStringAsync();
                return (response.IsSuccessStatusCode, body);
            }
            catch { return (false, null); }
        }

        #endregion
    }
}
