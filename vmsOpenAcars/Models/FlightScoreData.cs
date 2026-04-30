using System.Collections.Generic;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Holds all performance data collected during a flight that feeds into score calculation.
    /// Populate this object progressively throughout the flight via the state machine and
    /// FSUIPC telemetry. Pass the completed instance to <see cref="Services.ScoringService.Calculate"/>.
    /// </summary>
    public class FlightScoreData
    {
        // ─── Touchdown Data ───────────────────────────────────────────────────────

        /// <summary>
        /// Vertical speed at the moment of touchdown, in ft/min.
        /// Typically negative (descending). Captured by the touchdown event in the state machine.
        /// Source: FSUIPC offset 0x030C or vertical speed at gear-contact detection.
        /// </summary>
        public int LandingRate { get; set; }

        /// <summary>
        /// Pitch angle at touchdown in degrees. Positive = nose up, negative = nose down.
        /// Ideal range: +1° to +5° (slight nose-up attitude).
        /// Source: FSUIPC offset 0x0366 scaled (x / 65536.0 * (360.0 / 65536.0) * ... → degrees).
        /// </summary>
        public double LandingPitch { get; set; }

        /// <summary>
        /// Bank angle at touchdown in degrees. Positive = right bank, negative = left.
        /// Ideal: within ±2°. Use Math.Abs() for evaluation.
        /// Source: FSUIPC offset 0x02BC scaled to degrees.
        /// </summary>
        public double LandingBank { get; set; }

        /// <summary>
        /// G-force experienced at the moment of touchdown (1.0 = normal gravity).
        /// Values above 1.3g indicate a harder-than-ideal landing.
        /// Source: FSUIPC offset 0x11BA (int16, divide by 625 to get g).
        /// If not available from FSUIPC, leave at 0.0 to skip this criterion.
        /// </summary>
        public double LandingGForce { get; set; }

        // ─── In-Flight Violations ─────────────────────────────────────────────────

        /// <summary>
        /// Number of overspeed events detected during the flight.
        /// An overspeed event occurs when IAS exceeds Vmo/Mmo.
        /// Increment this counter in your overspeed detection logic.
        /// </summary>
        public int OverspeedCount { get; set; }

        /// <summary>
        /// Number of lights compliance violations detected during the flight.
        /// Typical violations tracked:
        ///   - Landing lights off below 10,000 ft AGL during flight
        ///   - Beacon off during taxi/engine running
        /// Increment this counter in your lights monitoring logic.
        /// </summary>
        public int LightsViolations { get; set; }

        /// <summary>
        /// Total raw penalty points accrued at the 1000 ft AGL stabilized approach gate.
        /// Capped at MaxStabilizedApproachDeduction (15 pts) in ScoringService.
        /// </summary>
        public int StabilizedApproachDeductions { get; set; }

        /// <summary>
        /// Number of QNH compliance violations (incorrect altimeter setting at departure or arrival).
        /// Each violation = 5 pts, capped at MaxQnhDeduction (5 pts) in ScoringService.
        /// </summary>
        public int QnhViolations { get; set; }

        /// <summary>
        /// True if the pilot was not connected to IVAO when the flight was started.
        /// Results in a fixed -5 pt deduction.
        /// </summary>
        public bool WasOfflineFlight { get; set; }

        /// <summary>
        /// Resets all fields to their default (zero) values.
        /// Call this when a new flight begins (e.g., on prefile).
        /// </summary>
        public void Reset()
        {
            LandingRate = 0;
            LandingPitch = 0.0;
            LandingBank = 0.0;
            LandingGForce = 0.0;
            OverspeedCount = 0;
            LightsViolations = 0;
            StabilizedApproachDeductions = 0;
            QnhViolations = 0;
            WasOfflineFlight = false;
        }
    }
}