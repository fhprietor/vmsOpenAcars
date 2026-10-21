using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Speech.Synthesis;
using System.Threading;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Voz de los anuncios de cabina por SAPI (`System.Speech`), para las fases que **no tienen
    /// clip pregrabado** y como respaldo cuando el briefing de NavData no responde.
    ///
    /// Es distinto de <see cref="RaasVoice"/> a propósito, y no por duplicar: un RAAS habla frases
    /// de cuatro palabras a ritmo rápido (`Rate = 1`) y su volumen es el del aviso de cabina de
    /// vuelo; un anuncio de cabina habla quince segundos a ritmo normal (`Rate = 0`) con el volumen
    /// del slider de cabina, y además **bloquea** al hilo de anuncios mientras dura, porque va en
    /// una cola FIFO con el chime y los clips: si no esperara, el clip siguiente se le montaría
    /// encima. Compartir el sintetizador habría obligado a mezclar las dos políticas.
    ///
    /// El idioma **no** lo decide la aerolínea aquí: entra ya resuelto por `SapiVoicePolicy`, que es
    /// puro y se prueba sin tarjeta de sonido. Este servicio solo aporta la lista de voces que hay.
    /// </summary>
    internal static class CabinVoice
    {
        private sealed class Request
        {
            public string Text;
            public string Language;
            public ManualResetEventSlim Done;
            public volatile bool Cancelled;
        }

        private static readonly ConcurrentQueue<Request> _queue = new ConcurrentQueue<Request>();
        private static readonly object  _gate  = new object();

        private static Thread            _worker;
        private static SpeechSynthesizer _synth;
        private static volatile bool     _available = true;
        private static volatile int      _volume    = 80;
        private static volatile bool     _warned;
        private static Request           _current;
        private static string            _selectedLanguage;
        private static int               _appliedVolume = -1;
        private static List<string>      _installedCultures;

        /// <summary>Se llama una sola vez por motivo cuando no se puede locutar.</summary>
        internal static Action<string> Log;

        /// <summary>False cuando el equipo no tiene SAPI utilizable (sin voces, sin dispositivo de audio).</summary>
        internal static bool Available => _available;

        internal static int Volume
        {
            get => _volume;
            set => _volume = value < 0 ? 0 : (value > 100 ? 100 : value);
        }

        /// <summary>
        /// Idioma en el que se va a locutar de verdad. Lo pide el servicio **antes** de construir el
        /// texto, porque el texto tiene que ir en el idioma de la voz disponible: si el equipo solo
        /// tiene voz inglesa, se locuta el anuncio inglés aunque la aerolínea sea colombiana.
        /// </summary>
        internal static string ResolveLanguage(string requested) =>
            SapiVoicePolicy.ResolveLanguage(requested, InstalledCultures());

        /// <summary>
        /// Locuta y espera a que termine. Solo se llama desde el hilo de anuncios (que ya es de
        /// fondo), nunca desde telemetría.
        /// </summary>
        /// <returns>False si no hay voz, si se canceló o si SAPI falló.</returns>
        internal static bool SpeakSync(string text, string language)
        {
            if (!_available || string.IsNullOrWhiteSpace(text)) return false;

            EnsureWorker();

            var request = new Request
            {
                Text     = text,
                Language = SapiVoicePolicy.Normalize(language),
                Done     = new ManualResetEventSlim(false)
            };

            _queue.Enqueue(request);

            // Techo de espera: un anuncio largo son ~40 s; más que esto es un sintetizador colgado
            // y no puede dejar la cola de anuncios bloqueada para siempre.
            bool finished = request.Done.Wait(TimeSpan.FromSeconds(120));
            try { request.Done.Dispose(); } catch { }

            return finished && !request.Cancelled && _available;
        }

        /// <summary>Corta lo que esté sonando y vacía la cola (al desactivar los anuncios o cerrar el vuelo).</summary>
        internal static void Cancel()
        {
            Request pending;
            while (_queue.TryDequeue(out pending))
            {
                pending.Cancelled = true;
                try { pending.Done.Set(); } catch { }
            }

            Request current = _current;
            if (current != null) current.Cancelled = true;

            try { _synth?.SpeakAsyncCancelAll(); } catch { }
        }

        internal static void Shutdown()
        {
            Cancel();
            try { lock (_gate) { _synth?.Dispose(); _synth = null; } } catch { }
            _available = true;
            _worker    = null;
            _current   = null;
            _selectedLanguage = null;
        }

        // ── Voces instaladas ──────────────────────────────────────────────────────

        /// <summary>
        /// Lista de culturas instaladas, leída **una vez**. Se hace con un sintetizador temporal
        /// desde el hilo que pregunta (no desde el de locución) para que `ResolveLanguage` sea
        /// barato y síncrono: el servicio lo necesita antes de decidir el texto.
        /// Null cuando no se pudo enumerar — el policy entonces respeta el idioma pedido.
        /// </summary>
        private static List<string> InstalledCultures()
        {
            if (_installedCultures != null) return _installedCultures;

            try
            {
                var cultures = new List<string>();
                using (var probe = new SpeechSynthesizer())
                {
                    foreach (InstalledVoice voice in probe.GetInstalledVoices())
                    {
                        try
                        {
                            if (!voice.Enabled) continue;
                            string name = voice.VoiceInfo?.Culture?.Name;
                            if (!string.IsNullOrWhiteSpace(name)) cultures.Add(name);
                        }
                        catch { }
                    }
                }
                _installedCultures = cultures;
            }
            catch (Exception ex)
            {
                WarnOnce($"no se pudieron enumerar las voces de Windows ({ex.Message}); se usará la voz por defecto");
                return null;
            }

            return _installedCultures;
        }

        // ── Hilo de locución ──────────────────────────────────────────────────────

        private static void EnsureWorker()
        {
            if (_worker != null) return;
            lock (_gate)
            {
                if (_worker != null) return;
                _worker = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name         = "CabinVoice",
                    Priority     = ThreadPriority.BelowNormal
                };
                _worker.Start();
            }
        }

        private static void WorkerLoop()
        {
            try
            {
                lock (_gate)
                {
                    _synth = new SpeechSynthesizer();
                    _synth.SetOutputToDefaultAudioDevice();
                    _synth.Rate = 0;   // un PA de cabina no habla a la velocidad de un RAAS
                }
            }
            catch (Exception ex)
            {
                _available = false;
                WarnOnce($"SAPI no está disponible ({ex.Message}); los anuncios sin grabación no sonarán");
                DrainQueueAsFailed();
                return;
            }

            while (true)
            {
                Request request;
                if (!_queue.TryDequeue(out request))
                {
                    Thread.Sleep(120);
                    continue;
                }

                _current = request;
                try
                {
                    if (!request.Cancelled && _available) Speak(request);
                }
                catch (Exception ex)
                {
                    WarnOnce($"la locución falló ({ex.Message}); los anuncios sin grabación se silencian");
                    _available = false;
                }
                finally
                {
                    _current = null;
                    try { request.Done.Set(); } catch { }
                }
            }
        }

        private static void Speak(Request request)
        {
            if (_synth == null) return;

            if (_appliedVolume != _volume)
            {
                _synth.Volume  = _volume;
                _appliedVolume = _volume;
            }

            SelectVoice(request.Language);

            Prompt prompt = _synth.SpeakAsync(request.Text);
            while (prompt != null && !prompt.IsCompleted)
            {
                if (request.Cancelled) { try { _synth.SpeakAsyncCancelAll(); } catch { } }
                if (_appliedVolume != _volume) { try { _synth.Volume = _volume; _appliedVolume = _volume; } catch { } }
                Thread.Sleep(40);
            }
        }

        private static void SelectVoice(string language)
        {
            if (_selectedLanguage == language) return;
            _selectedLanguage = language;

            CultureInfo culture;
            try { culture = new CultureInfo(language == "es" ? "es-ES" : "en-US"); }
            catch { return; }

            try
            {
                _synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, culture);
            }
            catch
            {
                WarnOnce($"no hay voz instalada para {culture.Name}; se usa la voz por defecto de Windows");
            }
        }

        private static void DrainQueueAsFailed()
        {
            Request pending;
            while (_queue.TryDequeue(out pending))
            {
                pending.Cancelled = true;
                try { pending.Done.Set(); } catch { }
            }
        }

        private static void WarnOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            try { Log?.Invoke(message); } catch { }
        }
    }
}
