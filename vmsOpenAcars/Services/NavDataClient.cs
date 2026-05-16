using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

        public static bool IsReachable { get; private set; }
        public static bool IsKeyValid  { get; private set; }

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
                        if (updateFlags) { IsReachable = true; IsKeyValid = keyValid; }
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
                IsReachable = true;

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
    }
}
