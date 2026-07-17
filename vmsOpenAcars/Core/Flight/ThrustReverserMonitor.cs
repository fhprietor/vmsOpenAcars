using System;

namespace vmsOpenAcars.Core.Flight
{
    /// <summary>
    /// Monitors thrust reverser usage after landing and verifies that engines
    /// have completed the required idle cool-down before shutdown.
    /// </summary>
    public sealed class ThrustReverserMonitor
    {
        // ── Thresholds ───────────────────────────────────────────────────────
        /// <summary>
        /// Minimum reverser deployment (%) considered an intentional reverser application.
        /// Filters out noise / asymmetric feedback from some addons.
        /// </summary>
        private const double REVERSER_THRESHOLD_PCT = 0.6;

        /// <summary>Required idle seconds after reverser use before engine shutdown.</summary>
        public const int COOLDOWN_REQUIRED_SECONDS = 180;

        /// <summary>
        /// Seconds after touchdown with no reverser data before the addon-support
        /// warning is issued. Gives the sim time to reflect deployed reversers.
        /// </summary>
        private const int ADDON_CHECK_DELAY_SECONDS = 30;

        // ── State ────────────────────────────────────────────────────────────
        private bool     _reversersUsed;
        private double   _maxEng1RevPct;
        private double   _maxEng2RevPct;
        private DateTime? _touchdownTime;
        private bool     _reverserDataSeen;   // any reading > threshold during rollout

        // ── Public snapshot properties ───────────────────────────────────────
        /// <summary>True if reversers exceeded threshold during the current landing rollout.</summary>
        public bool ReversersUsed => _reversersUsed;

        /// <summary>Peak reverser deployment on engine 1 since touchdown (%).</summary>
        public double MaxEng1RevPct => _maxEng1RevPct;

        /// <summary>Peak reverser deployment on engine 2 since touchdown (%).</summary>
        public double MaxEng2RevPct => _maxEng2RevPct;

        /// <summary>Seconds elapsed since touchdown; 0 if not yet landed.</summary>
        public int SecondsSinceTouchdown =>
            _touchdownTime.HasValue ? (int)(DateTime.UtcNow - _touchdownTime.Value).TotalSeconds : 0;

        /// <summary>
        /// True once the reverser offset produced a reading above threshold,
        /// confirming the addon supports this FSUIPC offset.
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
            _maxEng1RevPct    = 0;
            _maxEng2RevPct    = 0;
            _touchdownTime    = DateTime.UtcNow;
            _reverserDataSeen = false;
        }

        /// <summary>
        /// Feed each telemetry cycle during the post-landing rollout and taxi-in.
        /// </summary>
        /// <param name="eng1RevPct">Engine 1 reverser deployment % (0–100).</param>
        /// <param name="eng2RevPct">Engine 2 reverser deployment % (0–100).</param>
        public void Update(double eng1RevPct, double eng2RevPct)
        {
            if (!_touchdownTime.HasValue) return;

            if (eng1RevPct > REVERSER_THRESHOLD_PCT || eng2RevPct > REVERSER_THRESHOLD_PCT)
            {
                _reverserDataSeen = true;
                _reversersUsed    = true;
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
            _maxEng1RevPct    = _maxEng2RevPct = 0;
            _touchdownTime    = null;
            _reverserDataSeen = false;
        }
    }
}
