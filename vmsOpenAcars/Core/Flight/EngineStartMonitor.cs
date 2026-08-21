using System;

namespace vmsOpenAcars.Core.Flight
{
    /// <summary>Result of a pre-takeoff engine idle-time check.</summary>
    public sealed class EngineReadinessResult
    {
        /// <summary>True when all engines meet the minimum idle-time requirement.</summary>
        public bool Ready { get; }
        /// <summary>Human-readable reason for failure; null when <see cref="Ready"/> is true.</summary>
        public string Reason { get; }

        private EngineReadinessResult(bool ready, string reason) { Ready = ready; Reason = reason; }

        /// <summary>Returns a passing result.</summary>
        public static EngineReadinessResult Ok() => new EngineReadinessResult(true, null);
        /// <summary>Returns a failing result with a descriptive reason.</summary>
        public static EngineReadinessResult NotReady(string reason) => new EngineReadinessResult(false, reason);
    }

    /// <summary>
    /// Snapshot of engine lifecycle state for UI display.
    /// Populated by <see cref="FlightManager.GetEngineLifecycleSnapshot"/>.
    /// </summary>
    public struct EngineLifecycleSnapshot
    {
        /// <summary>Engine 1 is currently running.</summary>
        public bool Eng1Running;
        /// <summary>Engine 2 is currently running.</summary>
        public bool Eng2Running;
        /// <summary>Engine 1 meets N2, oil-pressure and oil-temperature stabilisation criteria.</summary>
        public bool Eng1Stabilized;
        /// <summary>Engine 2 meets N2, oil-pressure and oil-temperature stabilisation criteria.</summary>
        public bool Eng2Stabilized;
        /// <summary>Time since engine 1 started (during active monitoring); zero if engine started before monitoring.</summary>
        public TimeSpan Eng1IdleTime;
        /// <summary>Time since engine 2 started (during active monitoring); zero if engine started before monitoring.</summary>
        public TimeSpan Eng2IdleTime;
        /// <summary>Oil temperature °C for engine 1 (raw FSUIPC offset 0x2050).</summary>
        public float OilTemp1;
        /// <summary>Oil temperature °C for engine 2 (raw FSUIPC offset 0x2150).</summary>
        public float OilTemp2;
        /// <summary>Oil pressure PSI for engine 1 (raw FSUIPC offset 0x2058).</summary>
        public float OilPress1;
        /// <summary>Oil pressure PSI for engine 2 (raw FSUIPC offset 0x2158).</summary>
        public float OilPress2;
        /// <summary>Outside air temperature °C (FSUIPC offset 0x0E8C).</summary>
        public double OatCelsius;
        /// <summary>Required idle seconds before takeoff at current OAT (120 s warm / 300 s cold).</summary>
        public int RequiredIdleSeconds;
        /// <summary>True if thrust reversers were deployed above threshold since last touchdown.</summary>
        public bool ReversersUsed;
        /// <summary>Peak reverser deployment on engine 1 since last touchdown (%).</summary>
        public double MaxEng1RevPct;
        /// <summary>Peak reverser deployment on engine 2 since last touchdown (%).</summary>
        public double MaxEng2RevPct;
        /// <summary>Seconds elapsed since touchdown; 0 if not yet landed.</summary>
        public int CooldownSecondsElapsed;
        /// <summary>Required cool-down seconds when reversers were used; 0 when not required.</summary>
        public int CooldownSecondsRequired;
        /// <summary>True when the reverser FSUIPC offset has returned non-zero data (addon support confirmed).</summary>
        public bool ReverserDataAvailable;
    }

    /// <summary>
    /// Monitors per-engine start timing and idle stabilisation.
    /// Detects late engine starts (engines started during taxi-out without enough idle
    /// warm-up before takeoff power) and evaluates N2 / oil-condition criteria.
    /// </summary>
    public sealed class EngineStartMonitor
    {
        // ── Stabilisation thresholds ─────────────────────────────────────────
        /// <summary>Minimum N2 % considered stable idle (skip check when N2 data unavailable).</summary>
        private const double N2_IDLE_MIN_PCT     = 58.0;
        /// <summary>Minimum oil pressure (PSI) for green-range indication.</summary>
        private const double OIL_PRESS_GREEN_PSI = 15.0;
        /// <summary>Minimum oil temperature (°C) for green-range indication.</summary>
        private const double OIL_TEMP_GREEN_DEG  = 40.0;

        // ── Idle-time requirements ───────────────────────────────────────────
        /// <summary>Required idle seconds before TOGA when OAT ≥ <see cref="COLD_OAT_THRESHOLD"/>.</summary>
        public const int WARM_IDLE_SECONDS     = 120;
        /// <summary>Required idle seconds before TOGA when OAT &lt; <see cref="COLD_OAT_THRESHOLD"/>.</summary>
        public const int COLD_IDLE_SECONDS     = 300;
        /// <summary>OAT (°C) below which cold-soak idle requirements apply.</summary>
        public const double COLD_OAT_THRESHOLD = 5.0;

        // ── Per-engine state ─────────────────────────────────────────────────
        private DateTime? _eng1StartedAt;
        private DateTime? _eng2StartedAt;
        private bool _eng1PrevRunning;
        private bool _eng2PrevRunning;
        // Guards against recording a false start on the first cycle after Reset().
        private bool _needsInit = true;
        // True once any N2 reading > 1% is observed; gates the N2 stabilisation check.
        private bool _n2DataSeen;
        // True once any oil temp > 5°C or oil press > 1 PSI is observed; gates the oil criteria check.
        // Prevents false "not stabilized" when the addon does not write FSUIPC oil offsets (leave at 0).
        private bool _oilDataSeen;

        // ── UI snapshot properties ───────────────────────────────────────────
        /// <summary>True when engine 1 meets N2, oil-pressure and oil-temperature criteria.</summary>
        public bool Eng1Stabilized { get; private set; }
        /// <summary>True when engine 2 meets N2, oil-pressure and oil-temperature criteria.</summary>
        public bool Eng2Stabilized { get; private set; }

        /// <summary>
        /// Elapsed time since engine 1 started during monitoring.
        /// Zero when engine was already running at monitoring start.
        /// </summary>
        public TimeSpan Eng1IdleTime =>
            _eng1StartedAt.HasValue ? DateTime.UtcNow - _eng1StartedAt.Value : TimeSpan.Zero;

        /// <summary>
        /// Elapsed time since engine 2 started during monitoring.
        /// Zero when engine was already running at monitoring start.
        /// </summary>
        public TimeSpan Eng2IdleTime =>
            _eng2StartedAt.HasValue ? DateTime.UtcNow - _eng2StartedAt.Value : TimeSpan.Zero;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Updates monitor state each telemetry cycle.
        /// Detects engine-start transitions and evaluates stabilisation criteria.
        /// </summary>
        /// <param name="eng1Running">Whether engine 1 is currently running.</param>
        /// <param name="eng2Running">Whether engine 2 is currently running.</param>
        /// <param name="n2_1Pct">N2 high-pressure spool % for engine 1 (0 if not available).</param>
        /// <param name="n2_2Pct">N2 high-pressure spool % for engine 2 (0 if not available).</param>
        /// <param name="oilPress1Psi">Oil pressure PSI for engine 1.</param>
        /// <param name="oilPress2Psi">Oil pressure PSI for engine 2.</param>
        /// <param name="oilTemp1Deg">Oil temperature °C for engine 1.</param>
        /// <param name="oilTemp2Deg">Oil temperature °C for engine 2.</param>
        public void Update(bool eng1Running, bool eng2Running,
                           double n2_1Pct, double n2_2Pct,
                           double oilPress1Psi, double oilPress2Psi,
                           double oilTemp1Deg, double oilTemp2Deg)
        {
            // On the first cycle after Reset(), seed _prevRunning from current state
            // to avoid recording a false start for engines that were already running.
            if (_needsInit)
            {
                _eng1PrevRunning = eng1Running;
                _eng2PrevRunning = eng2Running;
                _needsInit = false;
            }

            // Track N2 data availability (skip N2 criterion when addon doesn't write it)
            if (eng1Running && n2_1Pct > 1.0) _n2DataSeen = true;
            if (eng2Running && n2_2Pct > 1.0) _n2DataSeen = true;

            // Track oil data availability (skip oil criteria when addon doesn't write FSUIPC offsets)
            if (eng1Running && (oilTemp1Deg > 5.0 || oilPress1Psi > 1.0)) _oilDataSeen = true;
            if (eng2Running && (oilTemp2Deg > 5.0 || oilPress2Psi > 1.0)) _oilDataSeen = true;

            // Engine 1 start/stop transition
            if (eng1Running && !_eng1PrevRunning)
                _eng1StartedAt = DateTime.UtcNow;
            else if (!eng1Running)
                _eng1StartedAt = null;

            // Engine 2 start/stop transition
            if (eng2Running && !_eng2PrevRunning)
                _eng2StartedAt = DateTime.UtcNow;
            else if (!eng2Running)
                _eng2StartedAt = null;

            _eng1PrevRunning = eng1Running;
            _eng2PrevRunning = eng2Running;

            Eng1Stabilized = eng1Running && IsStabilized(n2_1Pct, oilPress1Psi, oilTemp1Deg);
            Eng2Stabilized = eng2Running && IsStabilized(n2_2Pct, oilPress2Psi, oilTemp2Deg);
        }

        /// <summary>
        /// Checks that every engine that started during monitoring has accumulated
        /// enough idle time before takeoff power is applied.
        /// Engines that were already running at monitoring start are not checked.
        /// </summary>
        /// <returns>
        /// <see cref="EngineReadinessResult.Ok"/> when all checks pass, or a
        /// <see cref="EngineReadinessResult.NotReady"/> describing the shortfall.
        /// </returns>
        public EngineReadinessResult CheckPreTakeoff(double oatCelsius, bool eng1Running, bool eng2Running)
        {
            int requiredSec = oatCelsius < COLD_OAT_THRESHOLD ? COLD_IDLE_SECONDS : WARM_IDLE_SECONDS;

            var r = EvalEngine(1, _eng1StartedAt, eng1Running, requiredSec, oatCelsius);
            if (r != null) return r;
            r = EvalEngine(2, _eng2StartedAt, eng2Running, requiredSec, oatCelsius);
            return r ?? EngineReadinessResult.Ok();
        }

        /// <summary>Resets all state for a new flight.</summary>
        public void Reset()
        {
            _eng1StartedAt   = _eng2StartedAt   = null;
            _eng1PrevRunning = _eng2PrevRunning = false;
            Eng1Stabilized   = Eng2Stabilized   = false;
            _n2DataSeen      = false;
            _oilDataSeen     = false;
            _needsInit       = true;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private bool IsStabilized(double n2Pct, double oilPressPsi, double oilTempDeg)
        {
            // Skip each criterion when the addon does not write the corresponding FSUIPC offset.
            bool n2Ok    = !_n2DataSeen   || n2Pct      >= N2_IDLE_MIN_PCT;
            bool pressOk = !_oilDataSeen  || oilPressPsi >= OIL_PRESS_GREEN_PSI;
            bool tempOk  = !_oilDataSeen  || oilTempDeg  >= OIL_TEMP_GREEN_DEG;
            return n2Ok && pressOk && tempOk;
        }

        private static EngineReadinessResult EvalEngine(
            int engineNum, DateTime? startedAt, bool running, int requiredSec, double oatCelsius)
        {
            if (!running || !startedAt.HasValue) return null;

            int elapsed = (int)(DateTime.UtcNow - startedAt.Value).TotalSeconds;
            if (elapsed >= requiredSec) return null;

            return EngineReadinessResult.NotReady(
                $"ENG{engineNum} {elapsed}s idle < {requiredSec}s required (OAT {oatCelsius:F0}°C)");
        }
    }
}
