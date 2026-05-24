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
        /// True if blocks-off occurred more than 10 minutes before or after the scheduled
        /// departure time (sched_out from SimBrief). Results in a fixed -5 pt deduction.
        /// </summary>
        public bool DepartedLate { get; set; }

        // ─── NavMap runway data (optional — populated when LNM DB is configured) ──

        /// <summary>
        /// Distance from the runway threshold to the touchdown point, in feet.
        /// Positive = past threshold (normal). 0 = data not available.
        /// </summary>
        public double TouchdownDistanceFt { get; set; }

        /// <summary>
        /// Perpendicular deviation from the runway centreline at touchdown, in feet.
        /// Always non-negative. 0 = data not available or perfect centreline.
        /// </summary>
        public double CenterlineDeviationFt { get; set; }

        /// <summary>Runway designator used at landing (e.g. "13L"). Null if not available.</summary>
        public string RunwayName { get; set; }

        // ─── ILS / Approach compliance (optional — populated when LNM DB configured) ─

        /// <summary>
        /// False when an ILS approach was expected but NAV1 was not tuned to the correct
        /// frequency at the 1000 ft AGL gate. Treated as a localizer violation.
        /// Defaults to true (no penalty when ILS data is unavailable).
        /// </summary>
        public bool IlsTunedCorrectly { get; set; } = true;

        /// <summary>
        /// Number of localizer heading alignment violations while below 500 ft AGL.
        /// Only counted when ILS data is available (ILS approach detected).
        /// </summary>
        public int LocalizerViolations { get; set; }

        /// <summary>
        /// True if the aircraft descended below the Decision Altitude (DA = threshold
        /// elevation + 200 ft) without touching down — indicates a go-around was not
        /// executed when it should have been.
        /// Only evaluated when ILS data is available.
        /// </summary>
public bool BelowMinimums { get; set; }

        /// <summary>
        /// True if the aircraft performed single-engine taxi (TaxiOut or TaxiIn) on a
        /// multi-engine aircraft. Grants a +5 pt bonus (capped at 100).
        /// </summary>
        public bool SingleEngineTaxi { get; set; }

        /// <summary>
        /// Number of SID/STAR speed restriction violations (IAS exceeded the published
        /// limit when passing the fix). Each violation: -3 pts, capped at -10 pts.
        /// </summary>
        public int ProcedureSpdViolations { get; set; }

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
            DepartedLate = false;
            TouchdownDistanceFt = 0;
            CenterlineDeviationFt = 0;
            RunwayName = null;
            IlsTunedCorrectly = true;
            LocalizerViolations = 0;
            BelowMinimums = false;
            SingleEngineTaxi = false;
            ProcedureSpdViolations = 0;
        }
    }
}