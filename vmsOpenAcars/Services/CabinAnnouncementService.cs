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
    /// <summary>
    /// Anuncios de cabina: **clip pregrabado si existe, locución SAPI si no**.
    ///
    /// La grabación profesional que sirve NavData (`/briefing/check/` + `/briefing/download/`) sigue
    /// mandando cuando está: es mejor locución. Pero NavData **no va a publicar más clips** que los
    /// siete que ya tiene, así que los momentos que faltaban en una operación real —puerta cerrada,
    /// después del despegue, llegada a puesto— no tendrían voz nunca. Y si el briefing no responde
    /// (503, sin clave, sin red), las siete fases se quedaban en silencio. Por eso el respaldo
    /// hablado no es un extra: es lo que hace que el sistema de anuncios no dependa de la
    /// disponibilidad de un endpoint.
    ///
    /// Orden de la cola, uno por anuncio: **chime → clip(s) → o el texto locutado**. FIFO y en un
    /// hilo de fondo, para que el hilo de telemetría nunca espere a que termine de hablar.
    /// </summary>
    internal sealed class CabinAnnouncementService : IDisposable
    {
        // ── Países hispanohablantes ──────────────────────────────────────────────
        private static readonly HashSet<string> SpanishCountries =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CO","MX","ES","AR","PE","CL","VE","EC","BO","PY",
                "UY","CR","PA","DO","GT","HN","SV","NI","CU","PR","GQ"
            };

        /// <summary>Espera máxima a que termine la descarga del briefing antes de decidir clip o voz.</summary>
        private const int PrefetchWaitMs = 5000;

        /// <summary>Espera máxima al refresco del METAR, que solo usan dos fases.</summary>
        private const int WeatherWaitMs = 3000;

        /// <summary>Un anuncio pendiente: la fase de la que hay que decir algo.</summary>
        private sealed class Pending
        {
            public string Phase;
            public string Lang;      // solo en la prueba de Settings: fuerza idioma
            public bool   IsTest;
        }

        // ── Estado ───────────────────────────────────────────────────────────────
        private readonly string _cacheDir;
        private readonly object _lock  = new object();
        private readonly Queue<Pending> _queue = new Queue<Pending>();
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>();
        private volatile bool _isPlaying;

        private volatile WaveOutEvent   _currentOutput;
        private volatile AudioFileReader _currentReader;

        private string _nativeLang    = "en";
        private bool   _isInternational;

        // Supresión por tamaño de avión: no se descarga nada y tampoco se locuta.
        private volatile bool _suppressed;

        private volatile Task _prefetchTask;
        private volatile bool _prefetchCompleted;

        private CabinAnnouncementContext _context;

        /// <summary>Refresco del contexto (METAR del destino) antes de locutar una fase que habla del tiempo.</summary>
        internal Func<Task> ContextRefresh { get; set; }

        /// <summary>Línea informativa para el log de vuelo (mismo canal que `RaasVoice`).</summary>
        internal Action<string> Log { get; set; }

        // ── Init / Dispose ───────────────────────────────────────────────────────

        public CabinAnnouncementService()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "vmsacars", "briefing");
            Directory.CreateDirectory(_cacheDir);

            CabinVoice.Volume = AppConfig.CabinAnnouncementsVolume;
            CabinVoice.Log    = message => Log?.Invoke(message);
        }

        public void Dispose()
        {
            try { CabinVoice.Shutdown(); } catch { }
            ClearCacheFiles();
        }

        /// <summary>Datos de vuelo para las plantillas (aerolínea, vuelo, destino, meteo).</summary>
        internal void SetFlightContext(CabinAnnouncementContext context)
        {
            lock (_lock) _context = context?.Clone();
        }

        // ── Prefetch ─────────────────────────────────────────────────────────────

        public async Task PrefetchAsync(string originIcao, string destIcao, string airlineCountry,
                                         int aircraftSeats = 0)
        {
            _suppressed         = false;
            _prefetchCompleted  = false;
            _prefetchTask       = null;

            // Supresión para aviones pequeños (< 40 asientos). aircraftSeats == 0 significa que el
            // dato no estaba disponible: se deja activado.
            if (aircraftSeats > 0 && aircraftSeats < 40)
            {
                _suppressed        = true;
                _prefetchCompleted = true;
                return;
            }

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
            foreach (string phase in CabinAnnouncementScript.Phases)
                foreach (string lang in langs)
                {
                    string p = phase, l = lang;
                    tasks.Add(Task.Run(async () => await FetchOneAsync(p, l)));
                }

            Task download = Task.WhenAll(tasks);
            _prefetchTask = download;

            try { await download.ConfigureAwait(false); } catch { }

            _prefetchCompleted = true;

            // La bienvenida va la última a propósito: es el anuncio que el piloto oye al pulsar
            // START y no puede adelantarse a que la cola tenga lo que necesita para decidir.
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
            if (_suppressed) return;
            if (!CabinAnnouncementScript.IsKnownPhase(phase)) return;

            lock (_lock) _queue.Enqueue(new Pending { Phase = phase });

            if (!_isPlaying) PlayNext();
        }

        // ── Reproducción (FIFO, secuencial, background) ──────────────────────────

        private void StopCurrent()
        {
            try { _currentOutput?.Stop(); } catch { }
            try { CabinVoice.Cancel(); } catch { }
        }

        private void PlayNext()
        {
            Pending item;
            lock (_lock)
            {
                if (_queue.Count == 0) { _isPlaying = false; return; }
                item = _queue.Dequeue();
            }
            _isPlaying = true;

            Task.Run(async () =>
            {
                try { await PlayAsync(item).ConfigureAwait(false); }
                catch { }
                finally { PlayNext(); }
            });
        }

        /// <summary>
        /// Decide y reproduce un anuncio: grabación si la hay, locución si no.
        /// </summary>
        private async Task PlayAsync(Pending item)
        {
            // Si el briefing aún se está descargando no se puede saber si hay clip: se espera un
            // poco. Sin esto, un enlace lento haría sonar la voz y medio minuto después el clip
            // del mismo anuncio.
            Task prefetch = _prefetchTask;
            if (!_prefetchCompleted && prefetch != null)
                await Task.WhenAny(prefetch, Task.Delay(PrefetchWaitMs)).ConfigureAwait(false);

            string nativeLang = item.Lang ?? _nativeLang;
            string clipNative, clipEn = null;
            lock (_lock)
            {
                _paths.TryGetValue(item.Phase + "_" + nativeLang, out clipNative);
                if (!item.IsTest && _isInternational && _nativeLang != "en")
                    _paths.TryGetValue(item.Phase + "_en", out clipEn);
            }

            if (clipNative != null || clipEn != null)
            {
                // Igual que en la locución: la marca es el principio, no el final del audio.
                Log?.Invoke($"🔊 PA {item.Phase} (grabación)");
                PlayChime();
                if (clipEn     != null) PlayAudioSync(clipEn);
                if (clipNative != null) PlayAudioSync(clipNative);
                return;
            }

            await SpeakFallbackAsync(item, nativeLang).ConfigureAwait(false);
        }

        /// <summary>
        /// Respaldo hablado. El idioma se resuelve **contra las voces instaladas** antes de construir
        /// el texto: si el equipo no tiene voz española, se locuta el anuncio inglés en vez de leer
        /// español con fonética inglesa (`Helpers/SapiVoicePolicy`).
        /// </summary>
        private async Task SpeakFallbackAsync(Pending item, string nativeLang)
        {
            if (!CabinVoice.Available)
            {
                Log?.Invoke($"🔇 PA {item.Phase}: no hay voz de Windows disponible");
                return;
            }

            if (CabinAnnouncementScript.UsesWeather(item.Phase) && ContextRefresh != null)
            {
                try { await Task.WhenAny(ContextRefresh(), Task.Delay(WeatherWaitMs)).ConfigureAwait(false); }
                catch { }
            }

            string voiceLang = item.IsTest
                ? SapiVoicePolicy.Normalize(item.Lang ?? nativeLang)
                : CabinVoice.ResolveLanguage(nativeLang);

            CabinAnnouncementContext context;
            lock (_lock) context = _context?.Clone() ?? new CabinAnnouncementContext();

            // La hora local se calcula aquí, en el momento de hablar, y no cuando se fijó el
            // contexto: un anuncio de llegada puede construirse horas después de pulsar START.
            if (!context.LocalTime.HasValue && context.DestinationLongitude.HasValue)
                context.LocalTime = LocalTimeOfDay.At(DateTime.UtcNow, context.DestinationLongitude.Value);

            string text = CabinAnnouncementScript.Build(item.Phase, voiceLang, context);
            if (string.IsNullOrWhiteSpace(text))
            {
                Log?.Invoke($"🔇 PA {item.Phase}: sin texto para la fase");
                return;
            }

            // La línea se escribe ANTES de locutar: `SpeakSync` es síncrono y bloquea hasta el final
            // del audio, así que el registro antiguo marcaba el **fin** del anuncio (en
            // `r4JNk2b2bBMJd8lD`, 16 s después de la fase de pushback) y hacía imposible juzgar la
            // oportunidad del aviso por el log.
            PlayChime();
            Log?.Invoke($"🔊 PA {item.Phase} (voz {voiceLang})");
            if (!CabinVoice.SpeakSync(text, voiceLang))
                Log?.Invoke($"🔇 PA {item.Phase}: la voz no pudo locutar");
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
                reader.Volume  = Math.Max(0f, Math.Min(2.0f,
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
            CabinVoice.Volume = volume;

            var reader = _currentReader;
            if (reader != null)
                reader.Volume = Math.Max(0f, Math.Min(2.0f, volume / 100f));
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

        // ── Prueba desde Settings ────────────────────────────────────────────────

        /// <summary>
        /// Prueba un anuncio concreto. Igual que en vuelo: grabación si existe, voz si no —lo que
        /// convierte el menú de Settings en la forma de comprobar la voz del equipo antes de volar.
        /// Sin idioma explícito usa el de la aerolínea de la sesión, que es el que sonaría.
        /// </summary>
        public async Task<string> TestAnnouncementAsync(string phase, string lang = null)
        {
            try
            {
                if (!CabinAnnouncementScript.IsKnownPhase(phase))
                    return "Unknown phase";

                string language = SapiVoicePolicy.Normalize(lang ?? _nativeLang);
                string cachePath = null;
                lock (_lock)
                    _paths.TryGetValue(phase + "_" + language, out cachePath);

                if (cachePath == null)
                    await FetchOneAsync(phase, language).ConfigureAwait(false);

                // Corta lo que esté sonando y sustituye la cola por esta prueba.
                StopCurrent();
                lock (_lock)
                {
                    _queue.Clear();
                    _paths.TryGetValue(phase + "_" + language, out cachePath);
                    _queue.Enqueue(new Pending { Phase = phase, Lang = language, IsTest = true });
                }
                _isPlaying = false;
                PlayNext();

                if (cachePath != null)
                {
                    string fmt = DetectFormat(cachePath);
                    long   kb  = new FileInfo(cachePath).Length / 1024;
                    return $"OK  [clip {fmt}  {kb} KB]  {Path.GetFileName(cachePath)}";
                }

                if (!CabinVoice.Available) return "No recording and no Windows voice";

                string voiceLang = CabinVoice.ResolveLanguage(language);
                return voiceLang == language
                    ? $"OK  [voice {voiceLang}]  {phase}"
                    : $"OK  [voice {voiceLang} — no {language} voice installed]  {phase}";
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
                _context = null;
            }
            _isPlaying        = false;
            _prefetchTask     = null;
            _prefetchCompleted = false;
            _suppressed       = false;
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
