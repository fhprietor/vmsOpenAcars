using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Queries the IVAO real-time whazzup feed to verify whether a pilot VID is online.
    /// Rate-limited to one fetch per 15 seconds to respect IVAO's update interval.
    /// The 2–5 MB feed is fetched in the background and cached between calls.
    /// </summary>
    public class IvaoService : IDisposable
    {
        private static readonly HttpClient _http;
        private HashSet<int> _onlineVids;
        private DateTime _lastFetch = DateTime.MinValue;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private const int MinIntervalSeconds = 15;
        private const string WhazzupUrl = "https://api.ivao.aero/v2/tracker/whazzup";

        static IvaoService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars/1.0");
        }

        /// <summary>
        /// Returns true if the VID is online, false if not, null if the feed is unavailable.
        /// </summary>
        public async Task<bool?> IsOnlineAsync(int ivaoVid)
        {
            if (ivaoVid <= 0) return null;
            await RefreshIfStaleAsync();
            if (_onlineVids == null) return null;
            return _onlineVids.Contains(ivaoVid);
        }

        private async Task RefreshIfStaleAsync()
        {
            if ((DateTime.UtcNow - _lastFetch).TotalSeconds < MinIntervalSeconds) return;

            await _lock.WaitAsync();
            try
            {
                if ((DateTime.UtcNow - _lastFetch).TotalSeconds < MinIntervalSeconds) return;
                await FetchAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task FetchAsync()
        {
            try
            {
                string json = await _http.GetStringAsync(WhazzupUrl);
                var root = JObject.Parse(json);
                var pilots = root["clients"]?["pilots"] as JArray;

                var vids = new HashSet<int>();
                if (pilots != null)
                {
                    foreach (var p in pilots)
                    {
                        int uid = p["userId"]?.Value<int>() ?? 0;
                        if (uid > 0) vids.Add(uid);
                    }
                }
                _onlineVids = vids;
            }
            catch
            {
                // Keep previous cache; advance timestamp to avoid hammering the API on repeated errors
            }
            finally
            {
                _lastFetch = DateTime.UtcNow;
            }
        }

        public void Dispose() => _lock?.Dispose();
    }
}
