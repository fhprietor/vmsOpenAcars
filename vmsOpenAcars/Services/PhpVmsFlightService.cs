// Services/PhpVmsFlightService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Servicio de integración con phpVMS 7 para operaciones de vuelo:
    /// consulta de vuelos disponibles, flota, asignación de bids y despacho.
    /// </summary>
    public class PhpVmsFlightService
    {
        private readonly ApiService _apiService;

        public PhpVmsFlightService(ApiService apiService)
        {
            _apiService = apiService;
        }

        // ── Vuelos disponibles ────────────────────────────────────────────────

        /// <summary>
        /// Obtiene todos los vuelos disponibles desde un aeropuerto para un piloto,
        /// recorriendo todas las páginas de la API (paginación Laravel).
        /// </summary>
        public async Task<List<Flight>> GetAvailableFlightsFromAirport(string airportCode, Pilot pilot)
        {
            var flights = new List<Flight>();

            try
            {
                int currentPage = 1;
                int lastPage    = 1;

                do
                {
                    string url = $"{_apiService.BaseUrl}api/flights" +
                                 $"?dep_icao={airportCode}&pilot_id={pilot.Id}&available=true" +
                                 $"&page={currentPage}";

                    var response = await _apiService.HttpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode) break;

                    var json = await response.Content.ReadAsStringAsync();
                    var obj  = JObject.Parse(json);
                    var data = obj["data"] as JArray;
                    if (data == null) break;

                    foreach (var item in data)
                    {
                        var flightData = item as JObject;
                        if (flightData == null) continue;

                        double nmDistance = flightData["distance"]?["nmi"]?.Value<double?>() ?? 0;

                        var subfleets    = flightData["subfleets"] as JArray;
                        var allowedTypes = new List<string>();
                        if (subfleets != null)
                        {
                            foreach (var sub in subfleets)
                            {
                                string type = sub["type"]?.ToString();
                                if (!string.IsNullOrEmpty(type) && !allowedTypes.Contains(type))
                                    allowedTypes.Add(type);
                            }
                        }

                        var airline = flightData["airline"] as JObject;

                        flights.Add(new Flight
                        {
                            Id           = flightData["id"]?.ToString(),
                            FlightNumber = flightData["flight_number"]?.ToString(),
                            Airline      = airline?["icao"]?.ToString() ?? "",
                            Departure    = flightData["dpt_airport_id"]?.ToString(),
                            Arrival      = flightData["arr_airport_id"]?.ToString(),
                            AllowedAircraftTypes        = allowedTypes,
                            AllowedAircraftTypesDisplay = string.Join("/", allowedTypes),
                            Distance     = nmDistance,
                            FlightTime   = flightData["flight_time"]?.Value<int>() ?? 0,
                            Route        = flightData["route"]?.ToString(),
                            RequiredRank = GetRankFromString(flightData["flight_level"]?.ToString())
                        });
                    }

                    lastPage = obj["meta"]?["last_page"]?.Value<int>() ?? 1;
                    currentPage++;

                } while (currentPage <= lastPage);
            }
            catch { }

            return flights;
        }

        // ── Flota disponible ──────────────────────────────────────────────────

        /// <summary>
        /// Obtiene todos los aviones disponibles en un aeropuerto, filtrando por tipos permitidos.
        /// Recorre automáticamente todas las páginas de <c>api/fleet</c> para superar el límite
        /// de 20 registros por página que impone phpVMS por defecto.
        /// </summary>
        /// <param name="airportCode">ICAO del aeropuerto (ej. "SKRG").</param>
        /// <param name="aircraftTypes">
        /// Lista de tipos ICAO permitidos (ej. ["B738","A320"]).
        /// Si es null o vacía, no se filtra por tipo.
        /// </param>
        public async Task<List<Aircraft>> GetAvailableAircraftAtAirport(
            string airportCode, List<string> aircraftTypes)
        {
            var aircraftList = new List<Aircraft>();

            try
            {
                var allSubfleets = await FetchAllFleetPagesAsync();


                foreach (var subfleet in allSubfleets)
                {
                    string subfleetType = subfleet["type"]?.ToString() ?? "";

                    if (aircraftTypes != null && aircraftTypes.Any() &&
                        !aircraftTypes.Contains(subfleetType))
                        continue;

                    var fleetAircraft = subfleet["aircraft"] as JArray;
                    if (fleetAircraft == null) continue;

                    foreach (var ac in fleetAircraft)
                    {
                        string airportId = ac["airport_id"]?.ToString() ?? "";
                        string statusRaw = ac["status"]?.ToString() ?? "?";
                        string reg = ac["registration"]?.ToString() ?? "?";


                        // phpVMS puede devolver status como int (0=A) o string ("A")
                        bool isActive = statusRaw == "0" || statusRaw == "A" ||
                                        ac["status"]?.Value<int?>() == 0;

                        if (!airportId.Equals(airportCode, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!isActive)
                            continue;

                        aircraftList.Add(new Aircraft
                        {
                            Id = ac["id"]?.ToString(),
                            Registration = reg,
                            Type = subfleetType.Length > 0 ? subfleetType : ac["icao"]?.ToString(),
                            Subfleet = subfleet["name"]?.ToString(),
                            CurrentAirport = airportId,
                            HexCode = ac["hex_code"]?.ToString(),
                            IsAvailable = true
                        });
                    }
                }

            }
            catch { }

            return aircraftList;
        }

        /// <summary>
        /// Descarga todas las páginas de <c>api/fleet</c> y devuelve la lista
        /// completa de subfleets como un <see cref="JArray"/> plano.
        /// </summary>
        private async Task<JArray> FetchAllFleetPagesAsync()
        {
            var allSubfleets = new JArray();
            int currentPage = 1;
            int lastPage = 1;

            do
            {
                string url = $"{_apiService.BaseUrl}api/fleet?page={currentPage}";
                var response = await _apiService.HttpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    break;

                var json = await response.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);

                var data = obj["data"] as JArray;
                if (data != null)
                    foreach (var item in data)
                        allSubfleets.Add(item);

                // Leer metadatos de paginación Laravel
                lastPage = obj["meta"]?["last_page"]?.Value<int>() ?? 1;
                currentPage++;

            } while (currentPage <= lastPage);

            return allSubfleets;
        }

        // ── Bids ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Asigna un vuelo a un piloto (bid).
        /// </summary>
        /// <returns>Tuple con éxito y mensaje descriptivo.</returns>
        public async Task<(bool success, string message)> AssignFlightToPilot(
            string flightId, string pilotId)
        {
            try
            {
                var payload = new { flight_id = flightId, user_id = pilotId };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _apiService.HttpClient.PostAsync(
                    $"{_apiService.BaseUrl}api/user/bids", content);

                if (response.IsSuccessStatusCode)
                    return (true, "Flight assigned successfully");

                string errorContent = await response.Content.ReadAsStringAsync();
                return (false, ParseErrorMessage(errorContent));
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }
        }

        // ── Helpers privados ──────────────────────────────────────────────────

        private string ParseErrorMessage(string errorContent)
        {
            try
            {
                var errorJson = JObject.Parse(errorContent);

                // Estructura anidada error.message
                string nested = errorJson["error"]?["message"]?.ToString();
                if (!string.IsNullOrEmpty(nested)) return nested;

                // Campos planos en orden de preferencia
                foreach (string key in new[] { "title", "details", "message" })
                {
                    string val = errorJson[key]?.ToString();
                    if (!string.IsNullOrEmpty(val)) return val;
                }
            }
            catch { }

            return errorContent.Length > 200
                ? errorContent.Substring(0, 197) + "..."
                : errorContent;
        }

        private static int GetRankFromString(string level)
        {
            if (string.IsNullOrEmpty(level)) return 1;
            if (level.Contains("CAPT") || level.Contains("CAP")) return 5;
            if (level.Contains("FO") || level.Contains("F/O")) return 3;
            return 1;
        }
    }
}