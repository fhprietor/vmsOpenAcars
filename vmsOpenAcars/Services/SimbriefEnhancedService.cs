// Services/SimbriefEnhancedService.cs
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services
{
    public class SimbriefEnhancedService
    {
        private readonly ApiService _apiService;

        public SimbriefEnhancedService(ApiService apiService)
        {
            _apiService = apiService;
        }

        /// <summary>
        /// Genera URL para pre-cargar datos en SimBrief
        /// </summary>
        public string GenerateDispatchUrl(Flight flight, Pilot pilot, Aircraft aircraft)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, string>
            {
                ["flight_num"] = flight.FlightNumber,
                ["orig"] = flight.Departure,
                ["dest"] = flight.Arrival,
                ["aircraft"] = GetSimbriefAircraftCode(flight.AircraftType),
                ["pilot"] = pilot.Name,
                ["pilot_id"] = pilot.PilotId,
                ["route"] = flight.Route ?? "",
                ["static_url"] = "1"
            };

            var queryString = BuildQueryString(parameters);
            return $"https://www.simbrief.com/system/dispatch.php?{queryString}";
        }

        /// <summary>
        /// Recupera OFP desde SimBrief y lo convierte a tu modelo SimbriefPlan
        /// </summary>
        public async Task<SimbriefPlan> FetchAndParseOFP(string simbriefUsername)
        {
            try
            {
                string url = $"https://www.simbrief.com/api/xml.fetcher.php?username={simbriefUsername}&json=1";
                var response = await _apiService.HttpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return null;

                string content = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(content);

                var plan = new SimbriefPlan
                {
                    // Información general del vuelo
                    FlightNumber = json["general"]?["flight_number"]?.ToString() ?? "N/A",
                    Airline = json["general"]?["icao_airline"]?.ToString() ?? "N/A",

                    // Origen y destino
                    Origin = json["origin"]?["icao_code"]?.ToString() ?? "ZZZZ",
                    Destination = json["destination"]?["icao_code"]?.ToString() ?? "ZZZZ",
                    DestinationElevation = json["destination"]?["elevation"]?.Value<int>() ?? 0,
                    Alternate = json["alternate"]?["icao_code"]?.ToString(),

                    // Ruta
                    Route = json["general"]?["route"]?.ToString() ?? "DIRECT",
                    CruiseAltitude = json["general"]?["initial_altitude"]?.Value<int>() ?? 0,

                    // Distancia
                    Distance = json["general"]?["gc_distance"]?.Value<double>() ?? 0,

                    // Aeronave
                    AircraftIcao = json["aircraft"]?["icao_code"]?.ToString() ?? "B58",
                    Aircraft = json["aircraft"]?["icaocode"]?.ToString() ?? "B58",
                    Registration = json["aircraft"]?["reg"]?.ToString() ?? "N-ACARS",

                    // Combustible
                    BlockFuel = json["fuel"]?["plan_ramp"]?.Value<double>() ?? 0,
                    DepartureFuel = json["fuel"]?["plan_ramp"]?.Value<double>() ?? 0, // Si lo necesitas

                    // Pesos
                    PayLoad = json["weights"]?["payload"]?.Value<double>() ?? 0,
                    PaxCount = json["weights"]?["pax_count"]?.Value<int>() ?? 0,

                    // Tiempos - CORREGIDO: usar times.est_time_enroute
                    EstTimeEnroute = json["times"]?["est_time_enroute"]?.Value<int>() ?? 0,

                    // Altitud
                    PlannedAltitude = json["general"]?["initial_altitude"]?.ToString() ?? "FL230",

                    // Unidades
                    Units = json["params"]?["units"]?.ToString()?.ToUpperInvariant() ?? "KG",
                };

                return plan;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Simbrief fetch error: {ex}");
                return null;
            }
        }

        private string BuildQueryString(System.Collections.Generic.Dictionary<string, string> parameters)
        {
            string query = "";
            foreach (var param in parameters)
            {
                if (!string.IsNullOrEmpty(param.Value))
                {
                    query += $"{param.Key}={System.Web.HttpUtility.UrlEncode(param.Value)}&";
                }
            }
            return query.TrimEnd('&');
        }

        private string GetSimbriefAircraftCode(string phpvmsType)
        {
            var mapping = new System.Collections.Generic.Dictionary<string, string>
            {
                { "A320", "A320" },
                { "B738", "B738" },
                { "B737", "B737" },
                { "C172", "C172" },
                { "B58", "B58" },
                { "PA28", "P28A" }
            };

            return mapping.ContainsKey(phpvmsType) ? mapping[phpvmsType] : phpvmsType;
        }
    }
}