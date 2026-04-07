// Services/PhpVmsFlightService.cs
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services
{
    public class PhpVmsFlightService
    {
        private readonly ApiService _apiService;

        public PhpVmsFlightService(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<List<Flight>> GetAvailableFlightsFromAirport(string airportCode, Pilot pilot)
        {
            var flights = new List<Flight>();

            try
            {
                var response = await _apiService.HttpClient.GetAsync(
                    $"{_apiService.BaseUrl}api/flights?dep_icao={airportCode}&pilot_id={pilot.Id}&available=true");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json)["data"] as JArray;

                    if (data != null)
                    {
                        foreach (var item in data)
                        {
                            var flightData = item as JObject;

                            // Distancia en NM
                            int nmDistance = flightData["distance"]?["nmi"]?.Value<int>() ?? 0;

                            // Tipos permitidos
                            var subfleets = flightData["subfleets"] as JArray;
                            var allowedTypes = new List<string>();
                            if (subfleets != null)
                            {
                                foreach (var sub in subfleets)
                                {
                                    var type = sub["type"]?.ToString();
                                    if (!string.IsNullOrEmpty(type) && !allowedTypes.Contains(type))
                                        allowedTypes.Add(type);
                                }
                            }

                            var airline = flightData["airline"] as JObject;

                            flights.Add(new Flight
                            {
                                Id = flightData["id"]?.ToString(),
                                FlightNumber = flightData["flight_number"]?.ToString(),
                                Airline = airline?["icao"]?.ToString() ?? "",
                                Departure = flightData["dpt_airport_id"]?.ToString(),
                                Arrival = flightData["arr_airport_id"]?.ToString(),
                                AllowedAircraftTypes = allowedTypes,
                                AllowedAircraftTypesDisplay = string.Join("/", allowedTypes),
                                Distance = nmDistance,
                                FlightTime = flightData["flight_time"]?.Value<int>() ?? 0,
                                Route = flightData["route"]?.ToString(),
                                RequiredRank = GetRankFromString(flightData["flight_level"]?.ToString())
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting flights: {ex.Message}");
            }

            return flights;
        }
        /// <summary>
        /// Obtiene avión disponible en aeropuerto
        /// </summary>
        public async Task<List<Aircraft>> GetAvailableAircraftAtAirport(string airportCode, List<string> aircraftTypes)
        {
            var aircraftList = new List<Aircraft>();

            try
            {
                string url = $"{_apiService.BaseUrl}api/fleet";
                var response = await _apiService.HttpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var obj = JObject.Parse(json);
                    var subfleets = obj["data"] as JArray;

                    if (subfleets != null)
                    {
                        foreach (var subfleet in subfleets)
                        {
                            // Si hay lista de tipos, filtrar subflota
                            if (aircraftTypes != null && aircraftTypes.Any() &&
                                !aircraftTypes.Contains(subfleet["type"]?.ToString()))
                            {
                                continue;
                            }

                            var fleetAircraft = subfleet["aircraft"] as JArray;
                            if (fleetAircraft != null)
                            {
                                foreach (var ac in fleetAircraft)
                                {
                                    if (ac["airport_id"]?.ToString() == airportCode)
                                    {
                                        aircraftList.Add(new Aircraft
                                        {
                                            Id = ac["id"]?.ToString(),
                                            Registration = ac["registration"]?.ToString(),
                                            Type = subfleet["type"]?.ToString() ?? ac["icao"]?.ToString(),
                                            Subfleet = subfleet["name"]?.ToString(),
                                            CurrentAirport = ac["airport_id"]?.ToString(),
                                            HexCode = ac["hex_code"]?.ToString(),
                                            IsAvailable = ac["status"]?.ToString() == "A"
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error {response.StatusCode} al obtener flota: {error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excepción en GetAvailableAircraftAtAirport: {ex.Message}");
            }

            return aircraftList;
        }
        /// <summary>
        /// Asigna un vuelo a un piloto (bid)
        /// </summary>
        // En Services/PhpVmsFlightService.cs

        /// <summary>
        /// Asigna un vuelo a un piloto (bid)
        /// </summary>
        /// <returns>Tuple con éxito y mensaje de error si lo hay</returns>
        // En Services/PhpVmsFlightService.cs - método AssignFlightToPilot modificado

        public async Task<(bool success, string message)> AssignFlightToPilot(string flightId, string pilotId)
        {
            try
            {
                var payload = new
                {
                    flight_id = flightId,
                    user_id = pilotId
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _apiService.HttpClient.PostAsync(
                    $"{_apiService.BaseUrl}api/user/bids", content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Flight assigned successfully");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string errorMessage = ParseErrorMessage(errorContent);
                    return (false, errorMessage);
                }
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }
        }

        private string ParseErrorMessage(string errorContent)
        {
            try
            {
                var errorJson = Newtonsoft.Json.Linq.JObject.Parse(errorContent);

                // Buscar error.message (estructura anidada)
                var errorObj = errorJson["error"];
                if (errorObj != null)
                {
                    var errorMessage = errorObj["message"]?.ToString();
                    if (!string.IsNullOrEmpty(errorMessage))
                        return errorMessage;
                }

                // Buscar title
                var title = errorJson["title"]?.ToString();
                if (!string.IsNullOrEmpty(title))
                    return title;

                // Buscar details
                var details = errorJson["details"]?.ToString();
                if (!string.IsNullOrEmpty(details))
                    return details;

                // Buscar message directo
                var message = errorJson["message"]?.ToString();
                if (!string.IsNullOrEmpty(message))
                    return message;
            }
            catch { }

            if (errorContent.Length > 200)
                return errorContent.Substring(0, 197) + "...";

            return errorContent;
        }

        private int GetRankFromString(string level)
        {
            // Lógica simple para determinar rango requerido
            if (string.IsNullOrEmpty(level)) return 1;

            if (level.Contains("CAPT") || level.Contains("CAP")) return 5;
            if (level.Contains("FO") || level.Contains("F/O")) return 3;

            return 1;
        }
    }

}