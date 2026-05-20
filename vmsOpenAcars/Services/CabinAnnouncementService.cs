using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Media;
using NAudio.Wave;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Services
{
    internal sealed class CabinAnnouncementService : IDisposable
    {
        // ── Países hispanohablantes ──────────────────────────────────────────────
        private static readonly HashSet<string> SpanishCountries =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CO","MX","ES","AR","PE","CL","VE","EC","BO","PY",
                "UY","CR","PA","DO","GT","HN","SV","NI","CU","PR","GQ"
            };

        // ── Estado ───────────────────────────────────────────────────────────────
        private readonly string _cacheDir;
        private readonly object _lock  = new object();
        private readonly Queue<string> _queue = new Queue<string>();
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>();
        private volatile bool _isPlaying;
        private volatile WaveOutEvent  _currentOutput;
        private volatile AudioFileReader _currentReader;
        private string _nativeLang    = "en";
        private bool   _isInternational;

        private const string ChimeSentinel = "__chime__";

        private static readonly string[] Phases =
        {
            "boarding", "taxi_out", "on_runway", "cruise",
            "top_of_descent", "approach", "taxi_in"
        };

        // ── Init / Dispose ───────────────────────────────────────────────────────

        public CabinAnnouncementService()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "vmsacars", "briefing");
            Directory.CreateDirectory(_cacheDir);
        }

        public void Dispose() => ClearCacheFiles();

        // ── Prefetch ─────────────────────────────────────────────────────────────

        public async Task PrefetchAsync(string originIcao, string destIcao, string airlineCountry,
                                         int aircraftSeats = 0)
        {
            // Suppress cabin announcements for small aircraft (< 40 seats).
            // aircraftSeats == 0 means the value was unavailable — default to enabled.
            if (aircraftSeats > 0 && aircraftSeats < 40) return;

            _nativeLang = SpanishCountries.Contains(airlineCountry ?? "") ? "es" : "en";

            _isInternational = originIcao != null && destIcao != null
                && originIcao.Length >= 2 && destIcao.Length >= 2
                && !string.Equals(
                    originIcao.Substring(0, 2),
                    destIcao.Substring(0, 2),
                    StringComparison.OrdinalIgnoreCase);

            string[] langs = (_isInternational && _nativeLang != "en")
                ? new[] { _nativeLang, "en" }
                : new[] { _nativeLang };

            var tasks = new List<Task>();
            foreach (string phase in Phases)
                foreach (string lang in langs)
                {
                    string p = phase, l = lang;
                    tasks.Add(Task.Run(async () => await FetchOneAsync(p, l)));
                }
            await Task.WhenAll(tasks).ConfigureAwait(false);

            QueueAnnouncement("boarding");
        }

        private async Task FetchOneAsync(string phase, string lang)
        {
            try
            {
                BriefingCheckResult check =
                    await NavDataClient.CheckAnnouncementAsync(phase, lang).ConfigureAwait(false);
                if (check == null || !check.Available) return;

                string cachePath = Path.Combine(
                    _cacheDir, phase + "_" + lang + "_" + check.Version + ".mp3");

                if (!File.Exists(cachePath))
                {
                    byte[] bytes = await NavDataClient.FetchBytesAsync(
                        "briefing/download/?phase=" + phase + "&lang=" + lang)
                        .ConfigureAwait(false);
                    if (bytes == null || bytes.Length == 0) return;
                    File.WriteAllBytes(cachePath, bytes);
                }

                lock (_lock)
                    _paths[phase + "_" + lang] = cachePath;
            }
            catch { }
        }

        // ── Queue ────────────────────────────────────────────────────────────────

        public void QueueAnnouncement(string phase)
        {
            if (!AppConfig.CabinAnnouncementsEnabled) return;

            string pathNative = null, pathEn = null;
            lock (_lock)
            {
                _paths.TryGetValue(phase + "_" + _nativeLang, out pathNative);
                if (_isInternational && _nativeLang != "en")
                    _paths.TryGetValue(phase + "_en", out pathEn);
            }

            bool hasAny = pathNative != null || pathEn != null;
            if (!hasAny) return;

            lock (_lock)
            {
                _queue.Enqueue(ChimeSentinel);

                if (_isInternational && _nativeLang != "en")
                {
                    if (pathEn     != null) _queue.Enqueue(pathEn);
                    if (pathNative != null) _queue.Enqueue(pathNative);
                }
                else
                {
                    if (pathNative != null) _queue.Enqueue(pathNative);
                }
            }

            if (!_isPlaying) PlayNext();
        }

        // ── Playback (FIFO, secuencial, background) ──────────────────────────────

        private void StopCurrent()
        {
            try { _currentOutput?.Stop(); } catch { }
        }

        private void PlayNext()
        {
            string item;
            lock (_lock)
            {
                if (_queue.Count == 0) { _isPlaying = false; return; }
                item = _queue.Dequeue();
            }
            _isPlaying = true;

            Task.Run(() =>
            {
                try
                {
                    if (item == ChimeSentinel) PlayChime();
                    else                       PlayAudioSync(item);
                }
                catch { }
                finally { PlayNext(); }
            });
        }

        private static void PlayChime()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("vmsOpenAcars.Resources.Audio.chime_warning.wav"))
                {
                    if (stream == null) return;
                    using (var player = new SoundPlayer(stream))
                    {
                        player.Load();
                        player.PlaySync();
                    }
                }
            }
            catch { }
        }

        private void PlayAudioSync(string filePath)
        {
            if (!File.Exists(filePath)) return;
            using (var reader = new AudioFileReader(filePath))
            using (var output = new WaveOutEvent())
            using (var done   = new ManualResetEventSlim(false))
            {
                reader.Volume  = Math.Max(0f, Math.Min(1f,
                    AppConfig.CabinAnnouncementsVolume / 100f));
                _currentReader = reader;
                _currentOutput = output;
                output.PlaybackStopped += (s, e) => done.Set();
                output.Init(reader);
                output.Play();
                done.Wait();
                _currentReader = null;
                _currentOutput = null;
            }
        }

        public void SetVolume(int volume)
        {
            AppConfig.CabinAnnouncementsVolume = volume;
            var reader = _currentReader;
            if (reader != null)
                reader.Volume = Math.Max(0f, Math.Min(1f, volume / 100f));
        }

        private static string DetectFormat(string filePath)
        {
            try
            {
                var h = new byte[4];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    fs.Read(h, 0, 4);
                if (h[0] == 0x49 && h[1] == 0x44 && h[2] == 0x33)  return "MP3/ID3";
                if (h[0] == 0xFF && (h[1] & 0xE0) == 0xE0)          return "MP3";
                if (h[0] == 0x4F && h[1] == 0x67 && h[2] == 0x67)  return "OGG";
                if (h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46)  return "WAV";
                if (h[0] == 0xFF && (h[1] & 0xF6) == 0xF0)          return "AAC";
                if (h[0] == 0x66 && h[1] == 0x4C && h[2] == 0x61)  return "FLAC";
                return $"0x{h[0]:X2}{h[1]:X2}{h[2]:X2}{h[3]:X2}";
            }
            catch { return "?"; }
        }

        // ── Test (Settings) ──────────────────────────────────────────────────────

        public async Task<string> TestAnnouncementAsync(string phase, string lang = "en")
        {
            try
            {
                string cachePath = null;
                lock (_lock)
                    _paths.TryGetValue(phase + "_" + lang, out cachePath);

                if (cachePath == null)
                    await FetchOneAsync(phase, lang).ConfigureAwait(false);

                // Stop whatever is playing and replace the queue with this test
                StopCurrent();
                lock (_lock)
                {
                    _queue.Clear();
                    _paths.TryGetValue(phase + "_" + lang, out cachePath);
                    _queue.Enqueue(ChimeSentinel);
                    if (cachePath != null) _queue.Enqueue(cachePath);
                }
                _isPlaying = false;

                PlayNext();

                if (cachePath == null)
                    return "Phase not available from API";

                string fmt = DetectFormat(cachePath);
                long   kb  = new FileInfo(cachePath).Length / 1024;
                return $"OK  [{fmt}  {kb} KB]  {Path.GetFileName(cachePath)}";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        // ── Reset ────────────────────────────────────────────────────────────────

        public void Reset()
        {
            StopCurrent();
            lock (_lock)
            {
                _queue.Clear();
                _paths.Clear();
            }
            _isPlaying = false;
            ClearCacheFiles();
        }

        private void ClearCacheFiles()
        {
            try
            {
                if (!Directory.Exists(_cacheDir)) return;
                foreach (string f in Directory.GetFiles(_cacheDir, "*.mp3"))
                    File.Delete(f);
            }
            catch { }
        }
    }
}
