// Services/SimbriefEnhancedService.cs
using System;
using System.Collections.Generic;
using System.Configuration;
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
        /// 
        public string GenerateDispatchUrl(Flight flight, Pilot pilot, Aircraft aircraft)
        {
            // Calcular hora de salida: UTC actual + 30 minutos
            DateTime depTime = DateTime.UtcNow.AddMinutes(30);
            
            // Determinar el tipo de vuelo para SimBrief
            // "s" = scheduled (regular), "n" = non-scheduled (charter, ferry, etc.)
            string simbriefFlightType = flight.FlightType == "J" ? "s" : "n";

            var parameters = new Dictionary<string, string>
            {
                // Parámetros básicos (como en CrewSystem)
                ["airline"] = flight.Airline,
                ["fltnum"] = flight.FlightNumber,
                ["orig"] = flight.Departure,
                ["dest"] = flight.Arrival,
                ["type"] = GetSimbriefAircraftCode(aircraft.Type),
                ["reg"] = aircraft.Registration,

                // Datos del piloto (usan "cpt")
                ["cpt"] = pilot.Name,

                // Ruta
                ["route"] = flight.Route ?? "",

                // Pistas (valores por defecto, idealmente del plan)
                ["origrwy"] = "",
                ["destrwy"] = "",

                // Opciones de planificación
                ["civalue"] = ConfigurationManager.AppSettings["simbrief_civalue"] ?? "30",
                ["units"] = ConfigurationManager.AppSettings["simbrief_units"] ?? "lbs",
                ["pax"] = "",
                ["cargo"] = "",

                // Parámetros adicionales
                ["maps"] = "detailed",
                ["static_url"] = "1",

                // Hora de salida (formato HH y MM)
                ["deph"] = depTime.ToString("HH"),
                ["depm"] = depTime.ToString("mm"),
                ["extrarmk"] = ConfigurationManager.AppSettings["simbrief_extrarmk"] ?? "",
                ["flightrules"] = "",
                ["flighttype"] = simbriefFlightType
            };

            var queryString = BuildQueryString(parameters);
            return $"https://dispatch.simbrief.com/options/custom?{queryString}";
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
                        Distance = json["general"]?["route_distance"]?.Value<double>()
                                   ?? json["general"]?["gc_distance"]?.Value<double>()
                                   ?? 0,

                        // Aeronave
                        AircraftIcao = json["aircraft"]?["icao_code"]?.ToString() ?? "B58",
                        Aircraft = json["aircraft"]?["icaocode"]?.ToString() ?? "B58",
                        Registration = json["aircraft"]?["reg"]?.ToString() ?? "N-ACARS",

                        // Combustible
                        BlockFuel = json["fuel"]?["plan_ramp"]?.Value<double>() ?? 0,
                        DepartureFuel = json["fuel"]?["plan_ramp"]?.Value<double>() ?? 0, // Si lo necesitas

                        // Pesos
                        PayLoad = json["weights"]?["payload"]?.Value<double>() ?? 0,
                        ZeroFuelWeight = json["weights"]?["est_zfw"]?.Value<double>() ?? 0,
                        PaxCount = json["weights"]?["pax_count"]?.Value<int>() ?? 0,

                        // Tiempos - CORREGIDO: usar times.est_time_enroute
                        EstTimeEnroute = json["times"]?["est_time_enroute"]?.Value<int>() ?? 0,

                        // Altitud
                        PlannedAltitude = json["general"]?["initial_altitude"]?.ToString() ?? "FL230",

                        // Unidades
                        Units = json["params"]?["units"]?.ToString()?.ToUpperInvariant() ?? "KG",

                        TimeGenerated = json["params"]?["time_generated"]?.Value<long>() ?? 0,
                        ScheduledOffTime = json["times"]?["sched_off"]?.Value<long>() ?? 0,

                        // URL del PDF: directory + filename
                        PdfUrl = BuildPdfUrl(json),
                };
                System.Diagnostics.Debug.WriteLine($"SimBrief Distance: {plan.Distance} NM");
                System.Diagnostics.Debug.WriteLine($"SimBrief EstTimeEnroute: {plan.EstTimeEnroute} seconds ({plan.EstTimeEnroute / 60} minutes)");
                return plan;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Simbrief fetch error: {ex}");
                return null;
            }
        }

        private static string BuildPdfUrl(JObject json)
        {
            string directory = json["files"]?["directory"]?.ToString() ?? "";

            var pdfToken = json["files"]?["pdf"];
            string file = pdfToken?.Type == JTokenType.Object
                ? pdfToken["link"]?.ToString() ?? ""
                : pdfToken?.ToString() ?? "";

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(file))
                return null;

            return directory.TrimEnd('/') + "/" + file.TrimStart('/');
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