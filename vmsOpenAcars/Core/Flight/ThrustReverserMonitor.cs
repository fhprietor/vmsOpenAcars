using System;

namespace vmsOpenAcars.Core.Flight
{
    /// <summary>
    /// Monitors thrust reverser usage after landing and verifies that engines
    /// have completed the required idle cool-down before shutdown.
    ///
    /// **Por qué el umbral es 10 % y no 0,6 % (v0.9.50).** Con 0,6 % cualquier lectura positiva
    /// contaba como «reversa usada», así que el umbral no filtraba nada: el comentario decía
    /// «filters out noise» y no lo hacía. En el PIREP real **`5ow69vzDNW5ZMayB`** (SKRG, 21/10/2026)
    /// el despliegue llegó a **95,2 % en los dos motores** —lo que se ve en el rodaje de verdad—, así
    /// que 10 % sigue detectando de sobra y descarta el ruido de un offset que se mueve unas décimas.
    ///
    /// **Se distinguen dos cosas que antes eran la misma:** que el addon **publique** el dato
    /// (`ReverserDataAvailable`, cualquier lectura por encima de 0,5 %) y que la reversa se haya
    /// **usado** (`ReversersUsed`, 10 % o más). Antes una lectura de 1 % marcaba las dos, y el piloto
    /// veía «reversa usada» sin haberla tocado.
    ///
    /// **El 0 % del instante del toque no es una medida.** Las reversas se despliegan 1-3 s *después*
    /// de la toma; leer el offset en el mismo tick del toque da 0 siempre, y eso es exactamente lo que
    /// publicaba el bloque de datos de aterrizaje (`Reversas: 0% / 0%` a las 20:25:39, con el avión
    /// ya en la pista y las reversas todavía guardadas). Por eso el dato se publica **cuando ocurre**
    /// (`FirstDetection`, que el llamante registra en el momento) y el **máximo** se resume al entrar
    /// en `TaxiIn`.
    /// </summary>
    public sealed class ThrustReverserMonitor
    {
        // ── Thresholds ───────────────────────────────────────────────────────
        /// <summary>
        /// Despliegue (%) a partir del cual se considera que la reversa se ha usado. Filtra el ruido
        /// del offset; el rodaje real de SKRG llegó a 95,2 % (`5ow69vzDNW5ZMayB`).
        /// </summary>
        internal const double REVERSER_THRESHOLD_PCT = 10.0;

        /// <summary>
        /// Lectura mínima para dar el offset por publicado (`ReverserDataAvailable`). Por debajo de
        /// esto no se puede distinguir «guardada» de «el addon no lo escribe».
        /// </summary>
        internal const double DATA_SEEN_THRESHOLD_PCT = 0.5;

        /// <summary>Required idle seconds after reverser use before engine shutdown.</summary>
        public const int COOLDOWN_REQUIRED_SECONDS = 180;

        /// <summary>
        /// Seconds after touchdown with no reverser data before the addon-support
        /// warning is issued. Gives the sim time to reflect deployed reversers.
        /// </summary>
        private const int ADDON_CHECK_DELAY_SECONDS = 30;

        // ── State ────────────────────────────────────────────────────────────
        private readonly Func<DateTime> _clock;
        private bool      _reversersUsed;
        private bool      _firstDetection;
        private double    _maxEng1RevPct;
        private double    _maxEng2RevPct;
        private DateTime? _touchdownTime;
        private bool      _reverserDataSeen;   // alguna lectura > 0,5 % durante el rodaje

        internal ThrustReverserMonitor(Func<DateTime> clock)
        {
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        public ThrustReverserMonitor() : this(null) { }

        // ── Public snapshot properties ───────────────────────────────────────
        /// <summary>True if reversers exceeded threshold during the current landing rollout.</summary>
        public bool ReversersUsed => _reversersUsed;

        /// <summary>
        /// True **solo en la llamada a <see cref="Update"/> en la que se detecta el despliegue**, para
        /// que el llamante pueda publicarlo en el momento y no 44 s después.
        /// </summary>
        public bool FirstDetection => _firstDetection;

        /// <summary>Peak reverser deployment on engine 1 since touchdown (%).</summary>
        public double MaxEng1RevPct => _maxEng1RevPct;

        /// <summary>Peak reverser deployment on engine 2 since touchdown (%).</summary>
        public double MaxEng2RevPct => _maxEng2RevPct;

        /// <summary>Seconds elapsed since touchdown; 0 if not yet landed.</summary>
        public int SecondsSinceTouchdown =>
            _touchdownTime.HasValue ? (int)(_clock() - _touchdownTime.Value).TotalSeconds : 0;

        /// <summary>
        /// True once the reverser offset produced any reading above
        /// <see cref="DATA_SEEN_THRESHOLD_PCT"/>, confirming the addon publishes this offset.
        /// </summary>
        public bool ReverserDataAvailable => _reverserDataSeen;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Call at touchdown to start tracking reverser deployment from this point.
        /// Clears any state from a previous landing.
        /// </summary>
        public void OnTouchdown()
        {
            _reversersUsed    = false;
            _firstDetection   = false;
            _maxEng1RevPct    = 0;
            _maxEng2RevPct    = 0;
            _touchdownTime    = _clock();
            _reverserDataSeen = false;
        }

        /// <summary>
        /// Feed each telemetry cycle during the post-landing rollout and taxi-in.
        /// </summary>
        /// <param name="eng1RevPct">Engine 1 reverser deployment % (0–100).</param>
        /// <param name="eng2RevPct">Engine 2 reverser deployment % (0–100).</param>
        public void Update(double eng1RevPct, double eng2RevPct)
        {
            _firstDetection = false;
            if (!_touchdownTime.HasValue) return;

            if (eng1RevPct > DATA_SEEN_THRESHOLD_PCT || eng2RevPct > DATA_SEEN_THRESHOLD_PCT)
                _reverserDataSeen = true;

            if ((eng1RevPct >= REVERSER_THRESHOLD_PCT || eng2RevPct >= REVERSER_THRESHOLD_PCT)
                && !_reversersUsed)
            {
                _reversersUsed  = true;
                _firstDetection = true;
            }

            if (eng1RevPct > _maxEng1RevPct) _maxEng1RevPct = eng1RevPct;
            if (eng2RevPct > _maxEng2RevPct) _maxEng2RevPct = eng2RevPct;
        }

        /// <summary>
        /// Evaluates whether engines may be shut down safely.
        /// </summary>
        /// <returns>
        /// Null when cool-down is satisfied (or reversers were not used).
        /// A non-null string describes the shortfall and must be logged as a warning.
        /// </returns>
        public string CheckShutdown()
        {
            if (!_reversersUsed) return null;

            int elapsed = SecondsSinceTouchdown;
            if (elapsed >= COOLDOWN_REQUIRED_SECONDS) return null;

            return $"COOL-DOWN REVERSA: {elapsed}s idle < {COOLDOWN_REQUIRED_SECONDS}s requeridos " +
                   $"(ENG1 {_maxEng1RevPct:F1}% / ENG2 {_maxEng2RevPct:F1}%)";
        }

        /// <summary>
        /// Checks whether the reverser FSUIPC offset appears unsupported by the current addon.
        /// Should be called after a reasonable time has elapsed post-touchdown.
        /// </summary>
        /// <returns>A warning string or null when data is available (or landing not yet completed).</returns>
        public string CheckAddonSupport()
        {
            if (_touchdownTime.HasValue
                && !_reverserDataSeen
                && SecondsSinceTouchdown >= ADDON_CHECK_DELAY_SECONDS)
            {
                return "Offset de reversas retornó 0 en todo el rodaje — el addon puede no soportar este offset";
            }
            return null;
        }

        /// <summary>Resets monitor state for a new flight.</summary>
        public void Reset()
        {
            _reversersUsed    = false;
            _firstDetection   = false;
            _maxEng1RevPct    = _maxEng2RevPct = 0;
            _touchdownTime    = null;
            _reverserDataSeen = false;
        }
    }
}
