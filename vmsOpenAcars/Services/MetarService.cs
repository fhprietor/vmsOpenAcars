using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services
{
    public class MetarService : IDisposable
    {
        public MetarFetchState State { get; private set; } = MetarFetchState.Idle;
        public MetarData[] CurrentMetars { get; } = new MetarData[4];

        public event Action<MetarData[]> OnMetarUpdated;
        public event Action<MetarFetchState> OnStateChanged;
        public event Action<string> OnLog;

        private static readonly HttpClient _http;
        private Timer _refreshTimer;
        private int _retryCount;
        private const int MaxRetries = 3;
        private const int RefreshMs  = 5 * 60 * 1000;
        private const int RetryMs    = 30 * 1000;

        private string _origin, _dest, _alternate;
        private double _lat, _lon;
        private bool   _hasPosition;

        static MetarService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _http.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars/1.0");
        }

        public void SetStations(string origin, string dest, string alternate)
        {
            _origin    = origin;
            _dest      = dest;
            _alternate = alternate;
        }

        public void UpdatePosition(double lat, double lon)
        {
            _lat = lat; _lon = lon; _hasPosition = true;
        }

        public async Task FetchNowAsync()
        {
            if (State == MetarFetchState.Fetching) return;
            await DoFetchAsync();
        }

        private async Task DoFetchAsync()
        {
            SetState(MetarFetchState.Fetching);
            bool anySuccess = false;
            try
            {
                var t0 = SafeFetchByIcaoAsync(_origin,    0, "ORIG");
                var t1 = SafeFetchByIcaoAsync(_dest,      1, "DEST");
                var t2 = SafeFetchByIcaoAsync(_alternate, 2, "ALT");
                var t3 = _hasPosition
                    ? SafeFetchNearestAsync(_lat, _lon, 3, "ENRT")
                    : Task.CompletedTask;

                await Task.WhenAll(t0, t1, t2, t3);

                for (int i = 0; i < 4; i++)
                    if (CurrentMetars[i] != null) anySuccess = true;

                _retryCount = 0;
                SetState(MetarFetchState.Current);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"MetarService: {ex.Message}");
            }
            finally
            {
                OnMetarUpdated?.Invoke(CurrentMetars);

                if (anySuccess)
                {
                    RestartTimer(RefreshMs);
                }
                else
                {
                    _retryCount++;
                    if (_retryCount >= MaxRetries)
                    {
                        _retryCount = 0;
                        SetState(MetarFetchState.Idle);
                    }
                    else
                    {
                        SetState(MetarFetchState.Error);
                        RestartTimer(RetryMs);
                    }
                }
            }
        }

        private void RestartTimer(int delayMs)
        {
            _refreshTimer?.Dispose();
            _refreshTimer = new Timer(
                async _ => await DoFetchAsync(),
                null, delayMs, Timeout.Infinite);
        }

        private void SetState(MetarFetchState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);
        }

        private async Task SafeFetchByIcaoAsync(string icao, int slot, string label)
        {
            CurrentMetars[slot] = null;
            try
            {
                var result = await FetchByIcaoAsync(icao, label);
                if (result != null)
                    CurrentMetars[slot] = result;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"MetarService {label} ({icao}): {ex.Message}");
            }
        }

        private async Task SafeFetchNearestAsync(double lat, double lon, int slot, string label)
        {
            CurrentMetars[slot] = null;
            try
            {
                var result = await FetchNearestAsync(lat, lon, label);
                if (result != null)
                    CurrentMetars[slot] = result;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"MetarService {label}: {ex.Message}");
            }
        }

        private async Task<MetarData> FetchByIcaoAsync(string icao, string label)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            try
            {
                string url = "https://aviationweather.gov/api/data/metar?format=json&taf=false&ids=" +
                             Uri.EscapeDataString(icao.ToUpperInvariant());
                string json = await _http.GetStringAsync(url);
                var arr = JArray.Parse(json);
                return arr.Count > 0 ? ParseMetarToken(arr[0], label, icao) : null;
            }
            catch { return null; }
        }

        private async Task<MetarData> FetchNearestAsync(double lat, double lon, string label)
        {
            foreach (double d in new[] { 1.0, 2.0 })
            {
                try
                {
                    string bbox = string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3}", lat - d, lon - d, lat + d, lon + d);
                    string url = "https://aviationweather.gov/api/data/metar?format=json&taf=false&bbox=" + bbox;
                    string json = await _http.GetStringAsync(url);
                    var arr = JArray.Parse(json);
                    if (arr.Count == 0) continue;

                    JToken nearest = arr
                        .Where(t => t["lat"] != null && t["lon"] != null)
                        .OrderBy(t => Dist(lat, lon, t["lat"].Value<double>(), t["lon"].Value<double>()))
                        .FirstOrDefault() ?? arr[0];

                    return ParseMetarToken(nearest, label, null);
                }
                catch { }
            }
            return null;
        }

        private static double Dist(double a, double b, double c, double d) =>
            Math.Sqrt((a - c) * (a - c) + (b - d) * (b - d));

        private static MetarData ParseMetarToken(JToken t, string label, string reqIcao)
        {
            try
            {
                var m = new MetarData
                {
                    StationLabel  = label,
                    RequestedIcao = reqIcao,
                    FetchedIcao   = t["icaoId"]?.ToString() ?? t["stationId"]?.ToString() ?? reqIcao ?? "----",
                    Raw           = t["rawOb"]?.ToString() ?? "",
                    FetchedAt     = DateTime.UtcNow,
                    TempC         = t["temp"]?.Value<double?>(),
                    DewPointC     = t["dewp"]?.Value<double?>(),
                    QnhHpa        = t["altim"]?.Value<double?>(),
                    WxString      = t["wxString"]?.ToString(),
                };

                string wdirStr = t["wdir"]?.ToString();
                if (!string.IsNullOrEmpty(wdirStr) && wdirStr != "VRB" &&
                    int.TryParse(wdirStr, out int wd)) m.WindDir = wd;
                if (t["wspd"] != null && int.TryParse(t["wspd"].ToString(), out int ws)) m.WindSpeedKt = ws;
                if (t["wgst"] != null && int.TryParse(t["wgst"].ToString(), out int wg)) m.WindGustKt  = wg;

                string visStr = t["visib"]?.ToString();
                if (!string.IsNullOrEmpty(visStr) &&
                    double.TryParse(visStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double visSm))
                    m.VisibilityKm = visSm * 1.60934;

                if (t["clouds"] is JArray clouds)
                {
                    foreach (var c in clouds)
                    {
                        string cover = c["cover"]?.ToString();
                        if (cover == "BKN" || cover == "OVC")
                        {
                            int? baseAlt = c["base"]?.Value<int?>();
                            if (baseAlt.HasValue && (m.CeilingFt == null || baseAlt < m.CeilingFt))
                                m.CeilingFt = baseAlt;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(m.Raw))
                {
                    string[] parts = m.Raw.Split(' ');
                    if (parts.Length > 0)
                    {
                        string last = parts[parts.Length - 1];
                        if (last == "NOSIG" || last == "BECMG" || last == "TEMPO") m.Trend = last;
                    }
                }

                m.Condition = CalcCondition(m.VisibilityKm, m.CeilingFt);
                return m;
            }
            catch
            {
                return null;
            }
        }

        public static MetarCondition CalcCondition(double? visKm, int? ceilingFt)
        {
            if (visKm == null && ceilingFt == null) return MetarCondition.Unknown;
            if ((visKm.HasValue && visKm < 3.0) || (ceilingFt.HasValue && ceilingFt < 1000))
                return MetarCondition.IMC;
            if ((visKm.HasValue && visKm < 5.0) || (ceilingFt.HasValue && ceilingFt < 1500))
                return MetarCondition.MVMC;
            return MetarCondition.VMC;
        }

        public void Dispose() => _refreshTimer?.Dispose();
    }
}