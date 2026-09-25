using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Speech.Synthesis;
using System.Threading;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Voz del RAAS por SAPI (System.Speech).
    ///
    /// Se eligió SAPI para la primera versión porque no exige grabar ni distribuir una librería
    /// de frases: el texto sale tal cual de las plantillas de idioma. Sus dos pegas quedan
    /// asumidas y documentadas: depende de que Windows tenga una voz instalada para el idioma
    /// —si no la hay se degrada en silencio y se avisa una sola vez en el log— y el timbre cambia
    /// entre equipos.
    ///
    /// La síntesis va en un hilo propio con cola FIFO: `Speak` solo encola, así que el hilo de
    /// telemetría (que evalúa los avisos) nunca se bloquea esperando a que termine de hablar.
    /// </summary>
    internal static class RaasVoice
    {
        private static readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private static readonly object _gate = new object();

        private static Thread             _worker;
        private static SpeechSynthesizer  _synth;
        private static volatile bool      _enabled;
        private static volatile int       _volume = 80;
        private static volatile bool      _available = true;
        private static CultureInfo        _culture;
        private static bool               _noVoiceWarned;

        /// <summary>Se llama con el motivo cuando no se puede hablar (una sola vez por motivo).</summary>
        internal static Action<string> Log;

        internal static bool Enabled
        {
            get => _enabled;
            set { _enabled = value; if (!value) Cancel(); }
        }

        internal static int Volume
        {
            get => _volume;
            set => _volume = value < 0 ? 0 : (value > 100 ? 100 : value);
        }

        /// <summary>False cuando el equipo no tiene voces SAPI utilizables.</summary>
        internal static bool Available => _available;

        /// <summary>
        /// Fija idioma, volumen y habilitación. El idioma es el de la aplicación (`es`/`en`): se
        /// intenta la voz de esa cultura y, si no existe, se sigue con la que haya por defecto.
        /// </summary>
        internal static void Configure(bool enabled, int volume, string language)
        {
            Volume  = volume;
            Enabled = enabled;

            string tag = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
                       ? "en-US" : "es-ES";
            try { _culture = new CultureInfo(tag); }
            catch { _culture = null; }

            if (enabled) EnsureWorker();
        }

        /// <summary>Encola un aviso. No bloquea y no falla nunca: si no hay voz, no dice nada.</summary>
        internal static void Speak(string text)
        {
            if (!_enabled || !_available || string.IsNullOrWhiteSpace(text)) return;
            EnsureWorker();
            _queue.Enqueue(text);
        }

        /// <summary>Corta lo que esté sonando y vacía la cola (al desactivar el RAAS).</summary>
        internal static void Cancel()
        {
            string discarded;
            while (_queue.TryDequeue(out discarded)) { }
            try { _synth?.SpeakAsyncCancelAll(); } catch { }
        }

        internal static void Shutdown()
        {
            Enabled = false;
            try { lock (_gate) { _synth?.Dispose(); _synth = null; } } catch { }
            _available = true;
            _worker    = null;
        }

        private static void EnsureWorker()
        {
            if (_worker != null) return;
            lock (_gate)
            {
                if (_worker != null) return;
                _worker = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name         = "RaasVoice",
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
                }
            }
            catch (Exception ex)
            {
                _available = false;
                WarnOnce($"SAPI no está disponible ({ex.Message}); los avisos RAAS saldrán solo en pantalla");
                return;
            }

            TrySelectCultureVoice();

            while (true)
            {
                string text;
                if (!_queue.TryDequeue(out text))
                {
                    Thread.Sleep(120);
                    continue;
                }
                if (!_enabled || !_available) continue;
                try
                {
                    _synth.Volume = _volume;
                    _synth.Rate   = 1;              // un RAAS real habla rápido: frases cortas y seguidas
                    _synth.Speak(text);
                }
                catch (Exception ex)
                {
                    WarnOnce($"la síntesis de voz falló ({ex.Message}); se silencia el RAAS hablado");
                    _available = false;
                }
            }
        }

        private static void TrySelectCultureVoice()
        {
            if (_culture == null) return;
            try
            {
                _synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, _culture);
            }
            catch
            {
                WarnOnce($"no hay voz instalada para {_culture.Name}; se usa la voz por defecto de Windows");
            }
        }

        private static void WarnOnce(string message)
        {
            if (_noVoiceWarned) return;
            _noVoiceWarned = true;
            try { Log?.Invoke(message); } catch { }
        }
    }
}
