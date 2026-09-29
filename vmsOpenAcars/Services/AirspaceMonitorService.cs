using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Models.NavData;
using vmsOpenAcars.Services.Http;

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
        public event Action<NavAirspace>                  OnAirspaceAlert;       // Prohibited/Restricted/Danger entered
        public event Action<NavAirspace>                  OnAirspaceApproaching; // heading toward restricted space (predictive)
        public event Action<NavAirspace>                  OnAirspaceOverflight;  // inside polygon but above upper limit
        public event Action<NavAirspace, NavAirspaceFreq> OnAirspaceEntered;     // CTR/TMA/RMZ entered
        public event Action<NavAirspace>                  OnAirspaceExited;      // CTR/TMA/RMZ exited
        public event Action<IList<IvaoAtcStation>>        OnAtcUpdated;          // IVAO poll complete

        // ── IVAO HTTP ────────────────────────────────────────────────────────────
        private const string WhazzupUrl       = "https://api.ivao.aero/v2/tracker/whazzup";
        private const int    PollIntervalMs   = 3 * 60 * 1000;   // 3 minutes
        private const double AtcMaxDistanceNm         = 150.0;    // general radius
        private const double AtcMaxDistanceApproachNm = 80.0;     // approach/landing phase


        // ── State ────────────────────────────────────────────────────────────────
        private readonly object           _lock          = new object();
        private List<NavAirspace>         _airspaces     = new List<NavAirspace>();
        private HashSet<string>           _insideIds     = new HashSet<string>();
        private HashSet<string>           _approachingIds = new HashSet<string>();
        private HashSet<string>           _overflightIds  = new HashSet<string>();
        private List<IvaoAtcStation>      _atcStations   = new List<IvaoAtcStation>();
        private HashSet<string>           _relevantIcaos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, double[]> _airportCoordsCache
            = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        private double    _lastAcLat;
        private double    _lastAcLon;
        private bool      _isApproachPhase;
        private string    _originIcao;
        private string    _destIcao;
        private System.Threading.Timer    _pollTimer;

        // ── Route initialisation ─────────────────────────────────────────────────

        public async Task InitRouteAsync(string originIcao, string destIcao,
            double initLat = 0, double initLon = 0)
        {
            try
            {
                // Store initial position for first ATC poll
                lock (_lock)
                {
                    _lastAcLat  = initLat;
                    _lastAcLon  = initLon;
                    _originIcao = originIcao;
                    _destIcao   = destIcao;
                }
                var origInfo = NavDataClient.GetAirportInfo(originIcao);
                var destInfo = NavDataClient.GetAirportInfo(destIcao);
                if (origInfo == null || destInfo == null) return;

                double oLat = origInfo.Lat, oLon = origInfo.Lon;
                double dLat = destInfo.Lat, dLon = destInfo.Lon;

                // La cobertura la declara el servidor —200 nm hasta el aviso de NavData del
                // 29/09/2026, **54 nm** desde entonces—, así que se muestrea la ruta entera en vez
                // de pedir sólo origen, destino y, a veces, el punto medio: en un SKBO→KBOS eso
                // cubría 600 nm de 2.200 y el resto se pintaba como cielo vacío.
                var samples = AirspaceRouteSampler.Sample(
                    oLat, oLon, dLat, dLon, NavDataClient.LastAirspaceRadiusNm);

                var results = await Task.WhenAll(
                        samples.Select(p => NavDataClient.GetAirspacesAsync(p.Lat, p.Lon)))
                    .ConfigureAwait(false);

                bool anyFresh   = results.Any(r => !r.Unavailable);
                bool anyPartial = results.Any(r => r.Partial);
                bool anyCapped  = results.Any(r => r.Capped);

                // Procedencia: si algún punto se resolvió por el respaldo (`openaip_api`) es que
                // falta el export de un país en el índice, y eso se reporta. Los países se listan
                // para poder señalar el corredor concreto.
                string source = results.Any(r => r.Source == "openaip_api") ? "openaip_api"
                              : results.Any(r => r.Source == "local")      ? "local"
                              : "";
                var countries = results
                    .Where(r => r.Countries != null)
                    .SelectMany(r => r.Countries)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                var dict     = new Dictionary<string, NavAirspace>(StringComparer.Ordinal);
                var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var res in results)
                    foreach (var a in res.Airspaces)
                    {
                        if (string.IsNullOrEmpty(a.Id)) continue;
                        if (!dict.ContainsKey(a.Id)) dict[a.Id] = a;
                        string icao = a.ExtractIcao();
                        if (!string.IsNullOrEmpty(icao)) relevant.Add(icao);
                    }

                // Always include origin/dest so local ATC (TWR/GND/DEL) is never filtered out
                relevant.Add(originIcao);
                relevant.Add(destIcao);

                int knownCount;
                if (!anyFresh && GetAirspaces().Count > 0)
                {
                    // Sin datos frescos NO se pisa lo que ya teníamos: una caída del upstream no
                    // puede vaciar el cielo (era justo el síntoma que describen en su aviso).
                    lock (_lock) { _relevantIcaos = relevant; }
                    knownCount = GetAirspaces().Count;
                }
                else
                {
                    var fresh = dict.Values.ToList();
                    lock (_lock)
                    {
                        _airspaces     = fresh;
                        _relevantIcaos = relevant;
                        _insideIds     = new HashSet<string>();
                    }
                    knownCount = fresh.Count;
                }

                LastAirspaceLoadUnavailable = !anyFresh;
                LastAirspaceLoadPartial     = anyPartial;
                LastAirspaceLoadSamples     = samples.Count;
                LastAirspaceLoadCount       = knownCount;
                LastAirspaceLoadSource      = source;
                LastAirspaceLoadCapped      = anyCapped;
                LastAirspaceLoadCountries   = countries;

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

        /// <summary>
        /// Cómo fue la última carga de espacios aéreos. Existe porque «0 espacios» y «no pude
        /// preguntar» no pueden contarse igual en el log: durante una caída del upstream el
        /// cliente decía cero espacios aéreos, que es un dato falso, no una ausencia.
        /// </summary>
        public bool LastAirspaceLoadUnavailable { get; private set; }
        public bool LastAirspaceLoadPartial     { get; private set; }
        public int  LastAirspaceLoadSamples     { get; private set; }
        public int  LastAirspaceLoadCount       { get; private set; }

        /// <summary>`"local"` (índice por país) u `"openaip_api"` (respaldo: falta el export de un
        /// país). Se reporta en el log porque un corredor servido por el respaldo es un hueco que
        /// el equipo de NavData puede cerrar añadiendo ese país al índice.</summary>
        public string LastAirspaceLoadSource { get; private set; } = "";

        /// <summary>Algún punto vino recortado por número máximo de espacios (zona densa): la
        /// cobertura real es menor que el radio nominal.</summary>
        public bool LastAirspaceLoadCapped { get; private set; }

        /// <summary>Países que aportaron espacios a la ruta.</summary>
        public List<string> LastAirspaceLoadCountries { get; private set; } = new List<string>();

        /// <summary>
        /// Updates aircraft position and flight phase for ATC station filtering.
        /// Called from MainViewModel on each telemetry cycle.
        /// </summary>
        public void UpdateAircraftState(double lat, double lon, FlightPhase phase, string destIcao)
        {
            lock (_lock)
            {
                _lastAcLat = lat;
                _lastAcLon = lon;
                _isApproachPhase = phase == FlightPhase.Approach
                                || phase == FlightPhase.Landing;
                _destIcao = destIcao;
            }
        }

        // Origin/dest airports always bypass distance and phase filters.
        // When acLat/acLon == 0 (no aircraft position) distance filter is skipped entirely.
        private List<IvaoAtcStation> FilterAtcStations(
            List<IvaoAtcStation> raw, double acLat, double acLon,
            bool isApproachPhase, string destIcao, string originIcao)
        {
            bool hasPosition = acLat != 0.0 || acLon != 0.0;
            var result = new List<IvaoAtcStation>(raw.Count);
            foreach (var s in raw)
            {
                bool isRouteAirport =
                    string.Equals(s.Icao, originIcao, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(s.Icao, destIcao,   StringComparison.OrdinalIgnoreCase);

                if (!isRouteAirport && hasPosition)
                {
                    double[] coords;
                    if (_airportCoordsCache.TryGetValue(s.Icao, out coords))
                    {
                        double maxDist = isApproachPhase
                            ? AtcMaxDistanceApproachNm
                            : AtcMaxDistanceNm;
                        if (DistanceNm(acLat, acLon, coords[0], coords[1]) > maxDist) continue;
                    }

                    if (isApproachPhase && !string.IsNullOrEmpty(destIcao))
                    {
                        if (s.Position != "APP" && s.Position != "DEP") continue;
                    }
                }

                result.Add(s);
            }
            return result;
        }

        // ── Position check ───────────────────────────────────────────────────────

        public void CheckPosition(double lat, double lon, double altFt,
            double headingDeg = 0, double groundSpeedKts = 0)
        {
            List<NavAirspace> snapshot;
            lock (_lock) snapshot = _airspaces.ToList();

            foreach (var a in snapshot)
            {
                if (string.IsNullOrEmpty(a.Id)) continue;
                if (a.Geometry?.Coordinates == null || a.Geometry.Coordinates.Count == 0) continue;

                var ring           = a.Geometry.Coordinates[0];
                bool laterally     = IsPointInPolygon(lat, lon, ring);
                bool inside        = laterally && IsWithinVerticalLimits(a, altFt);

                bool wasInside;
                lock (_lock) wasInside = _insideIds.Contains(a.Id);

                if (inside && !wasInside)
                {
                    lock (_lock) { _insideIds.Add(a.Id); _approachingIds.Remove(a.Id); }
                    DispatchEntry(a);
                }
                else if (!inside && wasInside)
                {
                    lock (_lock) _insideIds.Remove(a.Id);
                    if (IsCtrTma(a.Type)) OnAirspaceExited?.Invoke(a);
                }

                // Predictive / overflight checks — alert-type spaces only, not currently entered
                if (!IsAlertType(a.Type) || inside) continue;

                double? upperFt    = GetUpperLimitFt(a);
                bool aboveUpper    = laterally && upperFt.HasValue && altFt > upperFt.Value;

                bool wasOverflying;
                lock (_lock) wasOverflying = _overflightIds.Contains(a.Id);

                if (aboveUpper && !wasOverflying)
                {
                    lock (_lock) _overflightIds.Add(a.Id);
                    OnAirspaceOverflight?.Invoke(a);
                }
                else if (!laterally && wasOverflying)
                {
                    lock (_lock) _overflightIds.Remove(a.Id);
                }

                // Predictive: project ~3 min forward and check if heading into the space
                if (!aboveUpper && !laterally && groundSpeedKts >= 30)
                {
                    double lookaheadNm = Math.Min(groundSpeedKts * 3.0 / 60.0, 20.0);
                    ProjectPosition(lat, lon, headingDeg, lookaheadNm,
                        out double pLat, out double pLon);
                    bool predictedInside = IsPointInPolygon(pLat, pLon, ring)
                                       && IsWithinVerticalLimits(a, altFt);

                    bool wasApproaching;
                    lock (_lock) wasApproaching = _approachingIds.Contains(a.Id);

                    if (predictedInside && !wasApproaching)
                    {
                        lock (_lock) _approachingIds.Add(a.Id);
                        OnAirspaceApproaching?.Invoke(a);
                    }
                    else if (!predictedInside && wasApproaching)
                    {
                        lock (_lock) _approachingIds.Remove(a.Id);
                    }
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
            try   { json = await HttpClientProvider.Ivao.GetStringAsync(WhazzupUrl).ConfigureAwait(false); }
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

            // ── Populate airport coords cache (lazy) and embed coords in each station ──
            lock (_lock)
            {
                foreach (var s in stations)
                {
                    if (!_airportCoordsCache.ContainsKey(s.Icao))
                    {
                        var info = NavDataClient.GetAirportInfo(s.Icao);
                        if (info != null && (info.Lat != 0 || info.Lon != 0))
                            _airportCoordsCache[s.Icao] = new[] { info.Lat, info.Lon };
                    }
                    double[] c;
                    if (_airportCoordsCache.TryGetValue(s.Icao, out c))
                    {
                        s.Lat = c[0];
                        s.Lon = c[1];
                    }
                }
            }

            // ── Filter: distance + phase (route airports bypass distance) ──
            double acLat, acLon;
            bool isApproach;
            string destIcao, originIcao;
            lock (_lock)
            {
                acLat      = _lastAcLat;
                acLon      = _lastAcLon;
                isApproach = _isApproachPhase;
                destIcao   = _destIcao;
                originIcao = _originIcao;
            }

            var filtered = FilterAtcStations(stations, acLat, acLon, isApproach, destIcao, originIcao);

            lock (_lock) _atcStations = filtered;
            OnAtcUpdated?.Invoke(filtered);
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

        // Delegado en GeoMath (fórmula antes duplicada en tres módulos).
        private static void ProjectPosition(double lat, double lon,
            double headingDeg, double distNm, out double outLat, out double outLon)
            => GeoMath.Project(lat, lon, headingDeg, distNm, out outLat, out outLon);

        private static double? GetUpperLimitFt(NavAirspace a)
        {
            if (a.UpperLimit == null || a.UpperLimit.Display == "UNL") return null;
            return a.UpperLimit.ValueFt ?? ParseAltDisplay(a.UpperLimit.Display);
        }

        private static double? ParseAltDisplay(string display)
        {
            if (string.IsNullOrEmpty(display) || display == "UNL") return null;
            if (display == "GND" || display == "SFC") return 0.0;

            var m = System.Text.RegularExpressions.Regex.Match(
                display, @"FL\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int fl))
                return fl * 100.0;

            m = System.Text.RegularExpressions.Regex.Match(display, @"(\d+)");
            if (m.Success && double.TryParse(m.Groups[1].Value, out double ft))
                return ft;

            return null;
        }

        private static bool IsWithinVerticalLimits(NavAirspace a, double altFt)
        {
            double lower = a.LowerLimit?.ValueFt
                           ?? ParseAltDisplay(a.LowerLimit?.Display)
                           ?? 0.0;

            bool unlimitedUpper = a.UpperLimit == null
                               || a.UpperLimit.Display == "UNL";
            if (unlimitedUpper) return altFt >= lower;

            double? upper = a.UpperLimit.ValueFt
                            ?? ParseAltDisplay(a.UpperLimit.Display);

            if (upper == null) return altFt >= lower;

            return altFt >= lower && altFt <= upper.Value;
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
                _approachingIds.Clear();
                _overflightIds.Clear();
                _atcStations.Clear();
                _relevantIcaos.Clear();
                _lastAcLat       = 0;
                _lastAcLon       = 0;
                _isApproachPhase = false;
                _originIcao      = null;
                _destIcao        = null;
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
        public double       Lat       { get; set; }   // ARP latitude  (0 if unknown)
        public double       Lon       { get; set; }   // ARP longitude (0 if unknown)

        public string AtisText => AtisLines?.Count > 0
            ? string.Join(" · ", AtisLines) : string.Empty;
    }
}
