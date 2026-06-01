// Services/SimbriefEnhancedService.cs
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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
                        OriginIata = json["origin"]?["iata_code"]?.ToString() ?? "---",
                        OriginRunway = json["origin"]?["plan_rwy"]?.ToString(),
                        Destination = json["destination"]?["icao_code"]?.ToString() ?? "ZZZZ",
                        DestinationIata = json["destination"]?["iata_code"]?.ToString() ?? "---",
                        DestinationRunway = json["destination"]?["plan_rwy"]?.ToString(),
                        DestinationElevation = json["destination"]?["elevation"]?.Value<int>() ?? 0,
                        OriginElevation = json["origin"]?["elevation"]?.Value<int>() ?? 0,
                        Alternate = json["alternate"]?["icao_code"]?.ToString(),

                        // Ruta
                        Route = json["general"]?["route"]?.ToString() ?? "DIRECT",
                        CruiseAltitude = json["general"]?["initial_altitude"]?.Value<int>() ?? 0,
                        CostIndex = json["general"]?["costindex"]?.Value<int>() ?? 0,

                        // Distancia
                        Distance = json["general"]?["route_distance"]?.Value<double>()
                                   ?? json["general"]?["gc_distance"]?.Value<double>()
                                   ?? 0,

                        // Aeronave
                        AircraftIcao = json["aircraft"]?["icao_code"]?.ToString() ?? "B58",
                        Aircraft = json["aircraft"]?["icaocode"]?.ToString() ?? "B58",
                        Registration = json["aircraft"]?["reg"]?.ToString() ?? "N-ACARS",

                        // Combustible
                        BlockFuel    = json["fuel"]?["plan_ramp"]?.Value<double>() ?? 0,
                        TripFuel     = json["fuel"]?["enroute_burn"]?.Value<double>() ?? 0,
                        DepartureFuel = json["fuel"]?["plan_ramp"]?.Value<double>() ?? 0,

                        // Pesos
                        PayLoad = json["weights"]?["payload"]?.Value<double>() ?? 0,
                        ZeroFuelWeight = json["weights"]?["est_zfw"]?.Value<double>() ?? 0,
                        PaxCount = json["weights"]?["pax_count"]?.Value<int>() ?? 0,
                        CargoWeight = json["weights"]?["cargo"]?.Value<double>()
                                   ?? json["weights"]?["freight_added"]?.Value<double>() ?? 0,

                        // Viento e ISA promedio en ruta
                        AvgWindDir = json["general"]?["avg_wind_dir"]?.Value<int>() ?? 0,
                        AvgWindSpd = json["general"]?["avg_wind_spd"]?.Value<int>() ?? 0,
                        AvgIsaDev  = ParseIsaDev(json["general"]?["avg_temp_dev"]?.ToString()),

                        // Tiempos - CORREGIDO: usar times.est_time_enroute
                        EstTimeEnroute = json["times"]?["est_time_enroute"]?.Value<int>() ?? 0,

                        // Altitud
                        PlannedAltitude = json["general"]?["initial_altitude"]?.ToString() ?? "FL230",

                        // Unidades
                        Units = json["params"]?["units"]?.ToString()?.ToUpperInvariant() ?? "KG",

                        TimeGenerated    = json["params"]?["time_generated"]?.Value<long>() ?? 0,
                        ScheduledOutTime = json["times"]?["sched_out"]?.Value<long>() ?? 0,
                        ScheduledOffTime = json["times"]?["sched_off"]?.Value<long>() ?? 0,

                        // URL del PDF: directory + filename
                        PdfUrl = BuildPdfUrl(json),

                        // SID y STAR: campo directo de SimBrief, con fallback al primer/último
                        // token de la cadena de ruta (SimBrief incluye el nombre ahí).
                        SidName  = json["general"]?["sid"]?.ToString()?.Trim()
                                   ?? ExtractProcedureFromRoute(
                                          json["general"]?["route"]?.ToString(), isFirst: true),
                        StarName = json["general"]?["star"]?.ToString()?.Trim()
                                   ?? ExtractProcedureFromRoute(
                                          json["general"]?["route"]?.ToString(), isFirst: false),
                };
                // Navlog — waypoints con coordenadas para el mapa
                var fixes = json["navlog"]?["fix"];
                if (fixes != null)
                {
                    foreach (var fix in fixes)
                    {
                        string latStr = fix["pos_lat"]?.ToString();
                        string lonStr = (fix["pos_long"] ?? fix["pos_lon"])?.ToString();
                        if (!double.TryParse(latStr, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double lat)) continue;
                        if (!double.TryParse(lonStr, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double lon)) continue;
                        int.TryParse(fix["altitude_feet"]?.ToString(), out int alt);
                        string wType = fix["type"]?.ToString()?.ToLower() ?? "wpt";
                        string freq  = null;
                        if (wType == "vor" || wType == "ndb" || wType == "dme")
                        {
                            string rawFreq = fix["pos_freq"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(rawFreq) &&
                                float.TryParse(rawFreq,
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out float fVal) && fVal > 0f)
                                freq = rawFreq;
                        }
                        // Magnetic track to next fix — try track_mag first, then track
                        double? magTrack = null;
                        string trackStr = fix["track_mag"]?.ToString() ?? fix["track"]?.ToString();
                        if (!string.IsNullOrEmpty(trackStr) &&
                            double.TryParse(trackStr,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out double parsedTrack))
                            magTrack = parsedTrack;

                        plan.Waypoints.Add(new vmsOpenAcars.Models.SimbriefWaypoint
                        {
                            Ident     = fix["ident"]?.ToString() ?? "",
                            Type      = wType,
                            Lat       = lat,
                            Lon       = lon,
                            Airway    = fix["via_airway"]?.ToString() ?? "DCT",
                            Stage     = fix["stage"]?.ToString() ?? "CRZ",
                            AltFt     = alt,
                            IsSidStar = fix["is_sid_star"]?.ToString() == "1",
                            Freq      = freq,
                            MagTrack  = magTrack,
                        });
                    }
                }

                // Rellenar frecuencias VOR/NDB/DME desde NavData si SimBrief no las proveyó
                try
                {
                    var lookup = new Dictionary<string, Task<vmsOpenAcars.Models.NavData.NavNavaid>>(
                        StringComparer.OrdinalIgnoreCase);
                    foreach (var wp in plan.Waypoints.Where(w =>
                        (w.Type == "vor" || w.Type == "ndb" || w.Type == "dme")
                        && string.IsNullOrEmpty(w.Freq)
                        && !string.IsNullOrEmpty(w.Ident)))
                    {
                        if (!lookup.ContainsKey(wp.Ident))
                            lookup[wp.Ident] = NavDataClient.GetNavaidAsync(wp.Ident, wp.Lat, wp.Lon, wp.Type);
                    }

                    if (lookup.Count > 0)
                    {
                        await Task.WhenAll(lookup.Values).ConfigureAwait(false);

                        foreach (var wp in plan.Waypoints.Where(w =>
                            (w.Type == "vor" || w.Type == "ndb" || w.Type == "dme")
                            && string.IsNullOrEmpty(w.Freq)))
                        {
                            if (!lookup.TryGetValue(wp.Ident, out Task<vmsOpenAcars.Models.NavData.NavNavaid> task)) continue;
                            var n = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion
                                ? task.Result : null;
                            if (n == null) continue;

                            if (n.FrequencyMhz.HasValue && n.FrequencyMhz > 0)
                                wp.Freq = n.FrequencyMhz.Value.ToString(
                                    "000.00", System.Globalization.CultureInfo.InvariantCulture);
                            else if (n.FrequencyKhz.HasValue && n.FrequencyKhz > 0)
                                wp.Freq = ((int)Math.Round(n.FrequencyKhz.Value)).ToString();
                        }
                    }
                }
                catch { /* Las frecuencias son opcionales; ignorar errores de la API */ }

                return plan;
            }
            catch
            {
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

        /// <summary>
        /// Parses SimBrief avg_temp_dev which can be "+17", "-3", "P17", "M03", or a raw integer string.
        /// Returns the signed integer value (positive = above ISA).
        /// </summary>
        private static int ParseIsaDev(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            raw = raw.Trim();
            if (raw.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(raw.Substring(1), out int v) ? v : 0;
            if (raw.StartsWith("M", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(raw.Substring(1), out int v2) ? -v2 : 0;
            return int.TryParse(raw, out int v3) ? v3 : 0;
        }

        private static string ExtractProcedureFromRoute(string route, bool isFirst)
        {
            if (string.IsNullOrWhiteSpace(route)) return null;
            var tokens = route.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return null;
            string t = isFirst ? tokens[0] : tokens[tokens.Length - 1];
            if (string.Equals(t, "DCT",  StringComparison.OrdinalIgnoreCase)) return null;
            if (string.Equals(t, "SID",  StringComparison.OrdinalIgnoreCase)) return null;
            if (string.Equals(t, "STAR", StringComparison.OrdinalIgnoreCase)) return null;
            return t;
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
