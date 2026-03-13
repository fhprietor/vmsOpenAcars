// Services/PhpVmsFlightService.cs
using System;
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

        /// <summary>
        /// Obtiene vuelos disponibles desde un aeropuerto
        /// </summary>
        public async Task<List<Flight>> GetAvailableFlightsFromAirport(string airportCode, Pilot pilot)
        {
            var flights = new List<Flight>();

            try
            {
                // Usar el mismo HttpClient de ApiService
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
                            flights.Add(new Flight
                            {
                                Id = item["id"]?.ToString(),
                                FlightNumber = item["flight_number"]?.ToString(),
                                Departure = item["dpt_airport_id"]?.ToString(),
                                Arrival = item["arr_airport_id"]?.ToString(),
                                AircraftType = item["aircraft_type"]?.ToString(),
                                Distance = item["distance"]?.Value<int>() ?? 0,
                                FlightTime = item["flight_time"]?.Value<int>() ?? 0,
                                Route = item["route"]?.ToString(),
                                RequiredRank = GetRankFromString(item["flight_level"]?.ToString())
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
        public async Task<Aircraft> GetAvailableAircraftAtAirport(string airportCode, string aircraftType = null)
        {
            try
            {
                var url = $"{_apiService.BaseUrl}api/aircraft?airport={airportCode}&available=true";
                if (!string.IsNullOrEmpty(aircraftType))
                    url += $"&subfleet={aircraftType}";

                var response = await _apiService.HttpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json)["data"] as JArray;

                    if (data != null && data.Count > 0)
                    {
                        var first = data[0];
                        return new Aircraft
                        {
                            Id = first["id"]?.ToString(),
                            Registration = first["registration"]?.ToString(),
                            Type = first["subfleet"]?["type"]?.ToString(),
                            Subfleet = first["subfleet"]?["name"]?.ToString(),
                            CurrentAirport = first["current_airport_id"]?.ToString(),
                            IsAvailable = true
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting aircraft: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Asigna un vuelo a un piloto (bid)
        /// </summary>
        public async Task<bool> AssignFlightToPilot(string flightId, string pilotId)
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
                    $"{_apiService.BaseUrl}api/bids", content);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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

    // Modelo Flight (nuevo o extiende SimbriefPlan)
    public class Flight
    {
        public string Id { get; set; }
        public string FlightNumber { get; set; }
        public string Departure { get; set; }
        public string Arrival { get; set; }
        public string AircraftType { get; set; }
        public int Distance { get; set; }
        public int FlightTime { get; set; }
        public string Route { get; set; }
        public int RequiredRank { get; set; }
        public bool IsAvailable { get; set; }

        public override string ToString() => $"{FlightNumber} → {Arrival} ({AircraftType})";
    }
}