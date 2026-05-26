using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Monitors airspaces along the active route (NavData) and IVAO ATC/ATIS coverage.
    /// Fires events when the aircraft enters/exits airspaces or when ATC stations change.
    /// Thread-safe; events fire on thread-pool threads — callers must InvokeRequired as needed.
    /// </summary>
    internal sealed class AirspaceMonitorService : IDisposable
    {
        // ── Events ───────────────────────────────────────────────────────────────
        public event Action<NavAirspace>                  OnAirspaceAlert;    // Prohibited/Restricted/Danger entered
        public event Action<NavAirspace, NavAirspaceFreq> OnAirspaceEntered;  // CTR/TMA/RMZ entered
        public event Action<NavAirspace>                  OnAirspaceExited;   // CTR/TMA/RMZ exited
        public event Action<IList<IvaoAtcStation>>        OnAtcUpdated;       // IVAO poll complete

        // ── IVAO HTTP ────────────────────────────────────────────────────────────
        private static readonly HttpClient _ivaoHttp;
        private const string WhazzupUrl       = "https://api.ivao.aero/v2/tracker/whazzup";
        private const int    PollIntervalMs   = 3 * 60 * 1000;   // 3 minutes

        static AirspaceMonitorService()
        {
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            _ivaoHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            _ivaoHttp.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars/1.0");
        }

        // ── State ────────────────────────────────────────────────────────────────
        private readonly object           _lock          = new object();
        private List<NavAirspace>         _airspaces     = new List<NavAirspace>();
        private HashSet<string>           _insideIds     = new HashSet<string>();
        private List<IvaoAtcStation>      _atcStations   = new List<IvaoAtcStation>();
        private HashSet<string>           _relevantIcaos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private System.Threading.Timer    _pollTimer;

        // ── Route initialisation ─────────────────────────────────────────────────

        public async Task InitRouteAsync(string originIcao, string destIcao)
        {
            try
            {
                var origInfo = NavDataClient.GetAirportInfo(originIcao);
                var destInfo = NavDataClient.GetAirportInfo(destIcao);
                if (origInfo == null || destInfo == null) return;

                double oLat = origInfo.Lat, oLon = origInfo.Lon;
                double dLat = destInfo.Lat, dLon = destInfo.Lon;
                double mLat = (oLat + dLat) / 2.0;
                double mLon = (oLon + dLon) / 2.0;
                double dist = DistanceNm(oLat, oLon, dLat, dLon);

                var tasks = new List<Task<List<NavAirspace>>>
                {
                    NavDataClient.GetAirspacesAsync(oLat, oLon),
                    NavDataClient.GetAirspacesAsync(dLat, dLon),
                };
                if (dist > 100)
                    tasks.Add(NavDataClient.GetAirspacesAsync(mLat, mLon));

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                var dict     = new Dictionary<string, NavAirspace>(StringComparer.Ordinal);
                var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var list in results)
                    foreach (var a in list)
                    {
                        if (string.IsNullOrEmpty(a.Id)) continue;
                        if (!dict.ContainsKey(a.Id)) dict[a.Id] = a;
                        string icao = a.ExtractIcao();
                        if (!string.IsNullOrEmpty(icao)) relevant.Add(icao);
                    }

                lock (_lock)
                {
                    _airspaces     = dict.Values.ToList();
                    _relevantIcaos = relevant;
                    _insideIds     = new HashSet<string>();
                }

                // Start 3-min polling; fire immediately (dueTime = 0)
                _pollTimer?.Dispose();
                _pollTimer = new System.Threading.Timer(
                    _ => Task.Run(async () => { try { await PollIvaoAsync(); } catch { } }),
                    null, 0, PollIntervalMs);
            }
            catch { }
        }

        // ── Public accessors ─────────────────────────────────────────────────────

        public IList<NavAirspace>    GetAirspaces()    { lock (_lock) return _airspaces.ToList(); }
        public IList<IvaoAtcStation> GetAtcStations()  { lock (_lock) return _atcStations.ToList(); }

        public void TriggerIvaoRefresh()
            => Task.Run(async () => { try { await PollIvaoAsync(); } catch { } });

        // ── Position check ───────────────────────────────────────────────────────

        public void CheckPosition(double lat, double lon, double altFt)
        {
            List<NavAirspace> snapshot;
            lock (_lock) snapshot = _airspaces.ToList();

            foreach (var a in snapshot)
            {
                if (string.IsNullOrEmpty(a.Id)) continue;
                if (a.Geometry?.Coordinates == null || a.Geometry.Coordinates.Count == 0) continue;

                bool inside = IsPointInPolygon(lat, lon, a.Geometry.Coordinates[0])
                           && IsWithinVerticalLimits(a, altFt);

                bool wasInside;
                lock (_lock) wasInside = _insideIds.Contains(a.Id);

                if (inside && !wasInside)
                {
                    lock (_lock) _insideIds.Add(a.Id);
                    DispatchEntry(a);
                }
                else if (!inside && wasInside)
                {
                    lock (_lock) _insideIds.Remove(a.Id);
                    if (IsCtrTma(a.Type)) OnAirspaceExited?.Invoke(a);
                }
            }
        }

        private void DispatchEntry(NavAirspace a)
        {
            if (IsAlertType(a.Type))
            {
                OnAirspaceAlert?.Invoke(a);
            }
            else if (IsCtrTma(a.Type))
            {
                var freq = a.Frequencies?.FirstOrDefault(f => f.Primary)
                        ?? a.Frequencies?.FirstOrDefault();
                OnAirspaceEntered?.Invoke(a, freq);
            }
        }

        // ── IVAO polling ─────────────────────────────────────────────────────────

        private async Task PollIvaoAsync()
        {
            string json;
            try   { json = await _ivaoHttp.GetStringAsync(WhazzupUrl).ConfigureAwait(false); }
            catch { return; }

            JObject root;
            try { root = JObject.Parse(json); } catch { return; }

            var atcsArr = root["clients"]?["atcs"] as JArray;
            if (atcsArr == null) return;

            HashSet<string> relevant;
            lock (_lock) relevant = new HashSet<string>(_relevantIcaos, StringComparer.OrdinalIgnoreCase);

            var stations = new List<IvaoAtcStation>();
            foreach (var entry in atcsArr)
            {
                string callsign = entry["callsign"]?.Value<string>();
                if (string.IsNullOrEmpty(callsign)) continue;
                int us = callsign.IndexOf('_');
                if (us < 2) continue;

                string icao = callsign.Substring(0, us).ToUpperInvariant();
                string pos  = callsign.Substring(us + 1).ToUpperInvariant();

                // Match exact ICAO or 2-char FIR prefix
                bool match = relevant.Contains(icao)
                          || relevant.Any(r => r.Length >= 2
                                            && icao.StartsWith(r.Substring(0, Math.Min(2, r.Length)),
                                                               StringComparison.OrdinalIgnoreCase));
                if (!match) continue;

                double freq   = entry["atcSession"]?["frequency"]?.Value<double>() ?? 0;
                var    lines  = (entry["atis"] as JArray)
                                    ?.Select(l => l.Value<string>())
                                    .Where(l => !string.IsNullOrWhiteSpace(l))
                                    .ToList()
                               ?? new List<string>();

                stations.Add(new IvaoAtcStation
                {
                    Callsign  = callsign,
                    Icao      = icao,
                    Position  = pos,
                    Frequency = freq,
                    AtisLines = lines,
                });
            }

            lock (_lock) _atcStations = stations;
            OnAtcUpdated?.Invoke(stations);
        }

        // ── Geometry helpers ─────────────────────────────────────────────────────

        // Ray-casting point-in-polygon. GeoJSON order: ring[i] = [longitude, latitude].
        private static bool IsPointInPolygon(double lat, double lon, List<double[]> ring)
        {
            if (ring == null || ring.Count < 3) return false;
            bool inside = false;
            int  n      = ring.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double lonA = ring[i][0], latA = ring[i][1];
                double lonB = ring[j][0], latB = ring[j][1];
                if ((latA > lat) != (latB > lat) &&
                    lon < lonA + (lat - latA) * (lonB - lonA) / (latB - latA))
                    inside = !inside;
            }
            return inside;
        }

        private static bool IsWithinVerticalLimits(NavAirspace a, double altFt)
        {
            double lower = a.LowerLimit?.ValueFt ?? 0;
            if (a.UpperLimit?.Display == "UNL" || a.UpperLimit?.ValueFt == null)
                return altFt >= lower;
            return altFt >= lower && altFt <= a.UpperLimit.ValueFt.Value;
        }

        private static bool IsAlertType(string type)
            => type == "Prohibited" || type == "Restricted" || type == "Danger";

        private static bool IsCtrTma(string type)
            => type == "CTR" || type == "TMA" || type == "ATZ" || type == "RMZ" || type == "CTA";

        private static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R    = 3440.065;
            double       dLat = (lat2 - lat1) * Math.PI / 180;
            double       dLon = (lon2 - lon1) * Math.PI / 180;
            double       a    = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        // ── Reset / Dispose ──────────────────────────────────────────────────────

        public void Reset()
        {
            _pollTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            lock (_lock)
            {
                _airspaces.Clear();
                _insideIds.Clear();
                _atcStations.Clear();
                _relevantIcaos.Clear();
            }
        }

        public void Dispose()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }
    }

    public sealed class IvaoAtcStation
    {
        public string       Callsign  { get; set; }
        public string       Icao      { get; set; }
        public string       Position  { get; set; }   // ATIS, TWR, APP, DEP, CTR, GND
        public double       Frequency { get; set; }
        public List<string> AtisLines { get; set; } = new List<string>();

        public string AtisText => AtisLines?.Count > 0
            ? string.Join(" · ", AtisLines) : string.Empty;
    }
}
