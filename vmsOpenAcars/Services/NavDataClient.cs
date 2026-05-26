using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Services
{
    internal sealed class NavAirportCache
    {
        public List<NavRunway>    Runways    { get; set; } = new List<NavRunway>();
        public List<NavTaxiway>   Taxiways   { get; set; } = new List<NavTaxiway>();
        public List<NavParking>   Parkings   { get; set; } = new List<NavParking>();
        public List<NavHoldShort> HoldShorts { get; set; } = new List<NavHoldShort>();
        public List<NavApproach>  Approaches { get; set; } = new List<NavApproach>();
        public NavAirportInfo     Info       { get; set; }
    }

    internal sealed class NavApiTestResult
    {
        public bool              Reachable { get; set; }
        public bool              KeyValid  { get; set; }
        public NavStatusResponse NavStatus { get; set; }
    }

    internal static class NavDataClient
    {
        private static readonly HttpClient _http;

        private static readonly ConcurrentDictionary<string, Task<NavAirportCache>> _cache
            = new ConcurrentDictionary<string, Task<NavAirportCache>>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, Task<NavNavaid>> _navaidCache
            = new ConcurrentDictionary<string, Task<NavNavaid>>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, Task<List<NavProcedure>>> _sidCache
            = new ConcurrentDictionary<string, Task<List<NavProcedure>>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Task<List<NavProcedure>>> _starCache
            = new ConcurrentDictionary<string, Task<List<NavProcedure>>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Task<List<NavAirportWaypoint>>> _airportWaypointCache
            = new ConcurrentDictionary<string, Task<List<NavAirportWaypoint>>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Task<List<NavIls>>> _ilsCache
            = new ConcurrentDictionary<string, Task<List<NavIls>>>(StringComparer.OrdinalIgnoreCase);

        // Airspace session cache — keyed by tile bucket "lat:lon:radius"
        private static readonly ConcurrentDictionary<string, List<NavAirspace>> _airspaceMemCache
            = new ConcurrentDictionary<string, List<NavAirspace>>(StringComparer.Ordinal);

        // Weather cache — keyed by ICAO, stores result + fetch timestamp for 5-min TTL.
        private static readonly ConcurrentDictionary<string, (NavWeather Data, DateTime FetchedAt)> _weatherCache
            = new ConcurrentDictionary<string, (NavWeather, DateTime)>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan _weatherTtl = TimeSpan.FromMinutes(5);

        public static bool   IsReachable     { get; private set; }
        public static bool   IsKeyValid      { get; private set; }
        public static string AiracCycle      { get; private set; }
        public static string AiracValidUntil { get; private set; }

        public static bool IsAiracExpired =>
            !string.IsNullOrEmpty(AiracValidUntil)
            && DateTime.TryParse(AiracValidUntil,
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None, out var d)
            && d.Date < DateTime.UtcNow.Date;

        static NavDataClient()
        {
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls13;

            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            string key    = AppConfig.NavDataApiKey;
            string domain = AppConfig.NavDataApiDomain;
            if (!string.IsNullOrEmpty(key))    _http.DefaultRequestHeaders.Add("X-API-Key", key);
            if (!string.IsNullOrEmpty(domain)) _http.DefaultRequestHeaders.Add("X-Origin-Domain", domain);

            NavDataCache.Initialize();
        }

        // ── Prefetch ──────────────────────────────────────────────────────────────

        public static void PrefetchAirport(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return;
            _cache.GetOrAdd(icao.ToUpperInvariant(), k => LoadAirportAsync(k));
        }

        // ── Sync accessors (safe from Task.Run threads via GetResult) ─────────────

        public static List<NavRunway>    GetRunways(string icao)    => GetCache(icao)?.Runways    ?? new List<NavRunway>();
        public static List<NavTaxiway>   GetTaxiways(string icao)   => GetCache(icao)?.Taxiways   ?? new List<NavTaxiway>();
        public static List<NavParking>   GetParkings(string icao)   => GetCache(icao)?.Parkings   ?? new List<NavParking>();
        public static List<NavHoldShort> GetHoldShorts(string icao) => GetCache(icao)?.HoldShorts ?? new List<NavHoldShort>();
        public static List<NavApproach>  GetApproaches(string icao) => GetCache(icao)?.Approaches ?? new List<NavApproach>();
        public static NavAirportInfo     GetAirportInfo(string icao) => GetCache(icao)?.Info;

        // ── Navaids ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Obtiene datos de un navaid (VOR/NDB/DME) por ident y posición aproximada.
        /// Los resultados se cachean por ident (insensible a mayúsculas).
        /// </summary>
        /// <summary>
        /// type: "vor", "dme" → endpoint /vor/{ident}/  |  "ndb" → /ndb/{ident}/
        /// </summary>
        public static Task<NavNavaid> GetNavaidAsync(string ident, double lat, double lon, string type = "vor")
        {
            if (string.IsNullOrWhiteSpace(ident)) return Task.FromResult<NavNavaid>(null);
            string key = $"{(type ?? "vor").ToLower()}:{ident.ToUpperInvariant()}";
            return _navaidCache.GetOrAdd(key, _ => FetchNavaidAsync(ident, lat, lon, type ?? "vor"));
        }

        private static async Task<NavNavaid> FetchNavaidAsync(
            string ident, double lat, double lon, string type)
        {
            string cacheKey = $"{(type ?? "vor").ToLower()}:{ident.ToUpperInvariant()}";

            string cached = NavDataCache.TryGetNavaid(cacheKey);
            if (cached != null)
            {
                var hit = JsonConvert.DeserializeObject<NavNavaid>(cached);
                if (hit != null) return hit;
            }

            string baseUrl  = AppConfig.NavDataApiUrl.TrimEnd('/');
            string endpoint = type == "ndb" ? "ndb" : "vor";   // dme usa /vor/
            string url      = $"{baseUrl}/{endpoint}/{Uri.EscapeDataString(ident)}/";

            try
            {
                using (var resp = await _http.GetAsync(url).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode) return null;
                    string json  = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var    obj   = JObject.Parse(json);
                    // Respuesta: {"vors":[...]} o {"ndbs":[...]}
                    string listKey = endpoint == "ndb" ? "ndbs" : "vors";
                    var    items   = obj[listKey]?.ToObject<List<NavNavaid>>();
                    var    result  = items?.Count > 0 ? FindNearestNavaid(items, lat, lon) : null;
                    if (result != null)
                        NavDataCache.StoreNavaid(cacheKey, JsonConvert.SerializeObject(result));
                    return result;
                }
            }
            catch { return null; }
        }

        private static NavNavaid FindNearestNavaid(List<NavNavaid> list, double lat, double lon)
        {
            if (list.Count == 1) return list[0];
            NavNavaid best     = list[0];
            double    bestDist = double.MaxValue;
            foreach (var n in list)
            {
                double d = (n.Lat - lat) * (n.Lat - lat) + (n.Lon - lon) * (n.Lon - lon);
                if (d < bestDist) { bestDist = d; best = n; }
            }
            return best;
        }

        // ── Procedures (SID / STAR) ───────────────────────────────────────────────

        public static List<NavProcedure> GetSids(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            var task = _sidCache.GetOrAdd(icao.ToUpperInvariant(), k => FetchProceduresAsync(k, "sids"));
            try   { return task.GetAwaiter().GetResult(); }
            catch { return null; }
        }

        public static List<NavProcedure> GetStars(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            var task = _starCache.GetOrAdd(icao.ToUpperInvariant(), k => FetchProceduresAsync(k, "stars"));
            try   { return task.GetAwaiter().GetResult(); }
            catch { return null; }
        }

        private static async Task<List<NavProcedure>> FetchProceduresAsync(string icao, string type)
        {
            string cached = NavDataCache.TryGet(type, icao);
            if (cached != null)
            {
                var hit = JsonConvert.DeserializeObject<List<NavProcedure>>(cached);
                if (hit != null) return hit;
            }

            string url = $"{AppConfig.NavDataApiUrl.TrimEnd('/')}/airport/{icao}/{type}/";
            try
            {
                using (var resp = await _http.GetAsync(url).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode) return null;
                    string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var    obj  = JObject.Parse(json);
                    var    list = obj[type]?.ToObject<List<NavProcedure>>();
                    if (list != null)
                        NavDataCache.Store(type, icao, JsonConvert.SerializeObject(list));
                    return list;
                }
            }
            catch { return null; }
        }

        // ── Airport Waypoints (ambient) ───────────────────────────────────────────

        public static List<NavAirportWaypoint> GetAirportWaypoints(string icao, int radiusNm = 40)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            var task = _airportWaypointCache.GetOrAdd(
                icao.ToUpperInvariant(),
                k => FetchWaypointsAsync(k, radiusNm));
            try   { return task.GetAwaiter().GetResult(); }
            catch { return null; }
        }

        private static async Task<List<NavAirportWaypoint>> FetchWaypointsAsync(
            string icao, int radiusNm)
        {
            string cached = NavDataCache.TryGet("waypoints", icao);
            if (cached != null)
            {
                var hit = JsonConvert.DeserializeObject<List<NavAirportWaypoint>>(cached);
                if (hit != null) return hit;
            }

            string url = $"{AppConfig.NavDataApiUrl.TrimEnd('/')}/airport/{icao}/waypoints/?radius_nm={radiusNm}";
            var result = await FetchAsync<NavAirportWaypointsResponse>(url).ConfigureAwait(false);
            var list   = result?.Waypoints;
            if (list != null)
                NavDataCache.Store("waypoints", icao, JsonConvert.SerializeObject(list));
            return list;
        }

        // ── ILS ───────────────────────────────────────────────────────────────────

        public static List<NavIls> GetIls(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            var task = _ilsCache.GetOrAdd(icao.ToUpperInvariant(), k => FetchIlsAsync(k));
            try   { return task.GetAwaiter().GetResult(); }
            catch { return null; }
        }

        private static async Task<List<NavIls>> FetchIlsAsync(string icao)
        {
            string cached = NavDataCache.TryGet("ils", icao);
            if (cached != null)
            {
                var hit = JsonConvert.DeserializeObject<List<NavIls>>(cached);
                if (hit != null) return hit;
            }

            string url = $"{AppConfig.NavDataApiUrl.TrimEnd('/')}/airport/{icao}/ils/";
            try
            {
                using (var resp = await _http.GetAsync(url).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode) return null;
                    string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var    obj  = JObject.Parse(json);
                    var    list = obj["ils"]?.ToObject<List<NavIls>>();
                    if (list != null)
                        NavDataCache.Store("ils", icao, JsonConvert.SerializeObject(list));
                    return list;
                }
            }
            catch { return null; }
        }

        // ── Weather ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Obtiene datos meteorológicos desde /weather/{icao}/.
        /// Resultado cacheado 5 minutos (igual al TTL del servidor).
        /// En caso de fallo devuelve el último valor exitoso o null.
        /// </summary>
        public static async Task<NavWeather> GetWeatherAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            string key = icao.ToUpperInvariant();

            if (_weatherCache.TryGetValue(key, out var cached)
                && DateTime.UtcNow - cached.FetchedAt < _weatherTtl)
                return cached.Data;

            string url = $"{AppConfig.NavDataApiUrl.TrimEnd('/')}/weather/{key}/";
            try
            {
                using (var resp = await _http.GetAsync(url).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                        return _weatherCache.TryGetValue(key, out var stale1) ? stale1.Data : null;
                    string json    = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var    weather = JsonConvert.DeserializeObject<NavWeather>(json);
                    if (weather != null) _weatherCache[key] = (weather, DateTime.UtcNow);
                    return weather;
                }
            }
            catch
            {
                return _weatherCache.TryGetValue(key, out var stale2) ? stale2.Data : null;
            }
        }

        // ── Connectivity + key validation ─────────────────────────────────────────

        /// <summary>
        /// Tests both service reachability and API key validity.
        /// When apiKeyOverride is null the result updates IsReachable/IsKeyValid flags.
        /// </summary>
        public static async Task<NavApiTestResult> TestApiAsync(string apiKeyOverride = null)
        {
            bool updateFlags = (apiKeyOverride == null);
            string key     = apiKeyOverride ?? AppConfig.NavDataApiKey;
            string baseUrl = AppConfig.NavDataApiUrl.TrimEnd('/');
            string domain  = AppConfig.NavDataApiDomain;

            try
            {
                // Step 1: /status/ is public — just verifies the service is reachable
                NavStatusResponse navStatus = null;
                using (var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/status/"))
                {
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    using (var resp = await _http.SendAsync(req).ConfigureAwait(false))
                    {
                        if (!resp.IsSuccessStatusCode)
                            return new NavApiTestResult { Reachable = false };
                        string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        navStatus = JsonConvert.DeserializeObject<NavStatusResponse>(json);
                    }
                }

                // Step 2: authenticated endpoint — 401/403 means the key is rejected
                using (var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/airport/LEMD/runways/"))
                {
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (!string.IsNullOrEmpty(key))    req.Headers.Add("X-API-Key", key);
                    if (!string.IsNullOrEmpty(domain)) req.Headers.Add("X-Origin-Domain", domain);
                    using (var resp = await _http.SendAsync(req).ConfigureAwait(false))
                    {
                        bool keyValid = resp.StatusCode != HttpStatusCode.Unauthorized
                                     && resp.StatusCode != HttpStatusCode.Forbidden;
                        if (updateFlags)
                        {
                            IsReachable = true;
                            IsKeyValid  = keyValid;
                            if (navStatus != null && !string.IsNullOrEmpty(navStatus.AiracCycle))
                            {
                                AiracCycle      = navStatus.AiracCycle;
                                AiracValidUntil = navStatus.AiracValidUntil;
                                NavDataCache.SyncAirac(AiracCycle, AiracValidUntil);
                            }
                        }
                        return new NavApiTestResult { Reachable = true, KeyValid = keyValid, NavStatus = navStatus };
                    }
                }
            }
            catch { return new NavApiTestResult { Reachable = false }; }
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private static NavAirportCache GetCache(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            var task = _cache.GetOrAdd(icao.ToUpperInvariant(), k => LoadAirportAsync(k));
            try   { return task.GetAwaiter().GetResult(); }
            catch { return null; }
        }

        private static async Task<NavAirportCache> LoadAirportAsync(string icao)
        {
            // ── SQLite cache ──────────────────────────────────────────────────────
            string cached = NavDataCache.TryGet("block", icao);
            if (cached != null)
            {
                var hit = JsonConvert.DeserializeObject<NavAirportCache>(cached);
                if (hit?.Runways != null)
                {
                    if (hit.Runways.Count > 0 || hit.Taxiways?.Count > 0) IsReachable = true;
                    return hit;
                }
            }

            // ── API ───────────────────────────────────────────────────────────────
            string baseUrl = AppConfig.NavDataApiUrl.TrimEnd('/');

            var tRunways    = FetchAsync<NavRunwaysResponse>   ($"{baseUrl}/airport/{icao}/runways/");
            var tTaxiways   = FetchAsync<NavTaxiwaysResponse>  ($"{baseUrl}/airport/{icao}/taxiways/");
            var tParkings   = FetchAsync<NavParkingsResponse>  ($"{baseUrl}/airport/{icao}/parkings/");
            var tHoldShorts = FetchAsync<NavHoldShortResponse> ($"{baseUrl}/airport/{icao}/holdshort/");
            var tApproaches = FetchAsync<NavApproachesResponse>($"{baseUrl}/airport/{icao}/approaches/");
            var tInfo       = FetchAsync<NavAirportInfo>       ($"{baseUrl}/airport/{icao}/");

            await Task.WhenAll(tRunways, tTaxiways, tParkings, tHoldShorts, tApproaches, tInfo)
                      .ConfigureAwait(false);

            var cache = new NavAirportCache
            {
                Runways    = tRunways.Result?.Runways       ?? new List<NavRunway>(),
                Taxiways   = tTaxiways.Result?.Taxiways     ?? new List<NavTaxiway>(),
                Parkings   = tParkings.Result?.Parkings     ?? new List<NavParking>(),
                HoldShorts = tHoldShorts.Result?.Holdshort  ?? new List<NavHoldShort>(),
                Approaches = tApproaches.Result?.Approaches ?? new List<NavApproach>(),
                Info       = tInfo.Result,
            };

            if (cache.Runways.Count > 0 || cache.Taxiways.Count > 0)
            {
                IsReachable = true;
                NavDataCache.Store("block", icao, JsonConvert.SerializeObject(cache));
            }

            return cache;
        }

        private static async Task<T> FetchAsync<T>(string url) where T : class
        {
            try
            {
                using (var resp = await _http.GetAsync(url).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode) return null;
                    string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
            catch { return null; }
        }

        // ── Airspaces ─────────────────────────────────────────────────────────────

        public static async Task<List<NavAirspace>> GetAirspacesAsync(double lat, double lon)
        {
            // Bucket to nearest integer degree to maximise cache hits across nearby calls
            string tileKey = $"{(int)Math.Round(lat)}:{(int)Math.Round(lon)}";

            // 1 — session in-memory
            if (_airspaceMemCache.TryGetValue(tileKey, out var memHit))
                return memHit;

            // 2 — SQLite persistent (7-day TTL)
            string cachedJson = NavDataCache.TryGetAirspace(tileKey);
            if (cachedJson != null)
            {
                var fromDb = JsonConvert.DeserializeObject<List<NavAirspace>>(cachedJson);
                if (fromDb != null)
                {
                    _airspaceMemCache[tileKey] = fromDb;
                    return fromDb;
                }
            }

            // 3 — HTTP fetch (server always returns 200 nm coverage, full pagination)
            try
            {
                string url = $"{AppConfig.NavDataApiUrl.TrimEnd('/')}/airspaces/" +
                             $"?lat={lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                             $"&lon={lon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";
                var result = await FetchAsync<NavAirspacesResponse>(url).ConfigureAwait(false);
                var list   = result?.Airspaces ?? new List<NavAirspace>();
                if (list.Count > 0)
                {
                    _airspaceMemCache[tileKey] = list;
                    NavDataCache.StoreAirspace(tileKey, JsonConvert.SerializeObject(list));
                }
                return list;
            }
            catch { return new List<NavAirspace>(); }
        }

        // ── Cabin Announcements ───────────────────────────────────────────────────

        public static async Task<byte[]> FetchBytesAsync(string path)
        {
            try
            {
                string url = AppConfig.NavDataApiUrl.TrimEnd('/') + "/" + path.TrimStart('/');
                using (var response = await _http.GetAsync(url).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) return null;
                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
            catch { return null; }
        }

        public static async Task<BriefingCheckResult> CheckAnnouncementAsync(string phase, string lang)
        {
            try
            {
                string url = AppConfig.NavDataApiUrl.TrimEnd('/')
                    + "/briefing/check/?phase=" + phase + "&lang=" + lang;
                using (var response = await _http.GetAsync(url).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) return null;
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var obj = JObject.Parse(json);
                    return new BriefingCheckResult
                    {
                        Available = obj["available"]?.Value<bool>() ?? false,
                        Version   = obj["version"]?.ToString()     ?? "unknown",
                    };
                }
            }
            catch { return null; }
        }
    }
}
