using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using vmsOpenAcars.Core.Helpers;
using vmsOpenAcars.Db;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.Services.Interfaces;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Core.Flight
{
    // Per-cycle snapshot passed to ApproachValidator.Check* methods.
    internal struct ApproachContext
    {
        public FlightPhase Phase;
        public double AGL;
        public int    AltitudeMsl, IAS, VS;
        public double Bank, Pitch, Heading, Lat, Lon;
        public bool   LandingLightOn, BeaconLightOn, StrobeOn, HotelMode;
        public bool   GearDown, IsOnGround;
        public double FlapsPosition;
        public double QnhMb;
        public double Nav1FreqMhz;
        public bool   ActivePirepPresent;
    }

    // Validates approach and in-flight compliance rules.
    // Owns all penalty counters and approach-state flags; side effects go out via events.
    internal sealed class ApproachValidator
    {
        // ── External dependencies ─────────────────────────────────────────────────
        private IWeatherService _weatherService;

        internal void SetWeatherService(IWeatherService ws) => _weatherService = ws;

        // ── Per-flight config ─────────────────────────────────────────────────────
        public int    VmoKts                = 320;
        public double OriginTransitionAltFt = 0;
        public double DestTransitionLevelFt = 0;
        public string AircraftIcao;
        public string DestIcao;
        public string EffectiveDestination;
        public Func<bool> IsOnAtcFrequency;

        private readonly HashSet<string> _beaconStrobeSharedAircraft;

        // ── Scoring counters (public read, private write) ─────────────────────────
        public int  OverspeedCount          { get; private set; }
        public int  OverspeedPenaltyCount   { get; private set; }
        public int  LightsViolationCount    { get; private set; }
        public int  StabilizedDeductions    { get; private set; }
        public int  LocalizerViolations     { get; private set; }
        public int  QnhViolations           { get; private set; }
        public bool BelowMinimums           { get; private set; }
        public bool IlsTunedCorrectly       { get; private set; } = true;

        // ── Events ────────────────────────────────────────────────────────────────
        public event Action<string, Color>       OnLog;
        public event Action<string, OsdSeverity> OnOsdMessage;

        // ── Internal tracking state ───────────────────────────────────────────────
        private bool   _wasOverspeed;
        private bool   _lightsViolationActive;
        private bool   _beaconViolationActive;
        private bool   _landingLightReminderSent;
        private bool   _passing10kFtSent;
        private bool   _taOsdSent, _tlOsdSent;
        private bool   _originQnhChecked, _destQnhChecked;
        private bool   _approachGateEvaluated;
        private double _prevApproachAgl = double.MaxValue;

        // ── ILS / approach data ───────────────────────────────────────────────────
        private IlsData            _expectedIls;
        private ApproachInfo       _expectedApproach;
        private IList<ApproachFix> _approachFixes;
        private int    _nextFixIndex;
        private bool   _ilsGateChecked;
        private double _daAltitudeFt;

        public ApproachValidator(HashSet<string> beaconStrobeSharedAircraft)
        {
            _beaconStrobeSharedAircraft = beaconStrobeSharedAircraft;
        }

        // ── Public setup ──────────────────────────────────────────────────────────

        public void SetApproachData(IlsData ils, ApproachInfo approach, IList<ApproachFix> fixes)
        {
            _expectedIls      = ils;
            _expectedApproach = approach;
            _approachFixes    = fixes ?? new List<ApproachFix>();
            _nextFixIndex     = 0;
            _ilsGateChecked   = false;
            LocalizerViolations = 0;
            BelowMinimums     = false;
            IlsTunedCorrectly = true;
            if (ils != null)
            {
                if (ils.GlideslopeAltFt.HasValue && ils.GlideslopeAltFt.Value > 0)
                    _daAltitudeFt = ils.GlideslopeAltFt.Value;
                else if (ils.ThresholdElevFt > 0)
                    _daAltitudeFt = ils.ThresholdElevFt + 200.0;
                else
                    _daAltitudeFt = 0;
            }
            else
                _daAltitudeFt = 0;
        }

        public void AddLightsViolation() => LightsViolationCount++;

        // Checks light compliance at phase transitions. Returns false if violation detected.
        internal void CheckPhaseEntryLights(
            FlightPhase phase, bool navOn, bool taxiOn, bool strobeOn, bool landingOn)
        {
            switch (phase)
            {
                case FlightPhase.Pushback:
                    if (!navOn)
                    {
                        LightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyNav"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  NAV LIGHTS  −5 PTS", OsdSeverity.Warning);
                    }
                    break;

                case FlightPhase.TaxiOut:
                    if (!navOn)
                    {
                        LightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyNavTaxi"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  NAV LIGHTS  −5 PTS", OsdSeverity.Warning);
                    }
                    if (!taxiOn)
                    {
                        LightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyTaxi"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  TAXI LIGHTS  −5 PTS", OsdSeverity.Warning);
                    }
                    break;

                case FlightPhase.TakeoffRoll:
                    if (!strobeOn)
                    {
                        LightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyStrobe"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  STROBE  −5 PTS", OsdSeverity.Warning);
                    }
                    if (!landingOn)
                    {
                        LightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyLanding"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  LANDING LT  −5 PTS", OsdSeverity.Warning);
                    }
                    break;
            }
        }

        // Reset the approach gate (called on go-around via OnApproachGateReset)
        public void ResetGate()
        {
            _approachGateEvaluated = false;
            _prevApproachAgl       = double.MaxValue;
        }

        // Reset approach-specific data (called on touch-and-go)
        public void ResetApproachData()
        {
            _expectedIls      = null;
            _expectedApproach = null;
            _approachFixes    = null;
            _nextFixIndex     = 0;
            LocalizerViolations = 0;
            BelowMinimums       = false;
            _daAltitudeFt       = 0;
            _ilsGateChecked     = false;
            IlsTunedCorrectly   = true;
            _taOsdSent          = false;
            _tlOsdSent          = false;
            _destQnhChecked     = false;
            _originQnhChecked   = false;
            ResetGate();
        }

        // Full reset — call in ResetFlightState and at the start of a new flight
        public void Reset()
        {
            OverspeedCount        = 0;
            OverspeedPenaltyCount = 0;
            LightsViolationCount  = 0;
            StabilizedDeductions  = 0;
            QnhViolations         = 0;
            _wasOverspeed               = false;
            _lightsViolationActive      = false;
            _beaconViolationActive      = false;
            _landingLightReminderSent   = false;
            _passing10kFtSent           = false;
            VmoKts                = 320;
            OriginTransitionAltFt = 0;
            DestTransitionLevelFt = 0;
            AircraftIcao          = null;
            DestIcao              = null;
            EffectiveDestination  = null;
            ResetApproachData();
        }

        // Restore penalties from a resumed flight checkpoint
        public void SetResumedPenalties(int overspeed, int lights, int stabilized,
            int qnh, int localizer, bool belowMins)
        {
            OverspeedCount        = overspeed;
            OverspeedPenaltyCount = overspeed;
            LightsViolationCount  = lights;
            StabilizedDeductions  = stabilized;
            QnhViolations         = qnh;
            LocalizerViolations   = localizer;
            BelowMinimums         = belowMins;
        }

        // ── Check methods ─────────────────────────────────────────────────────────

        public void CheckViolations(ApproachContext ctx)
        {
            // ── Overspeed ─────────────────────────────────────────────────────────
            bool isNowOverspeed = ctx.IAS > VmoKts;
            if (isNowOverspeed && !_wasOverspeed)
            {
                OverspeedCount++;
                if (IsOnAtcFrequency?.Invoke() != true) OverspeedPenaltyCount++;
                OnLog?.Invoke(_("Log_Overspeed", ctx.IAS, VmoKts), Theme.Warning);
                OnOsdMessage?.Invoke($"OVERSPEED  {ctx.IAS} KTS", OsdSeverity.Critical);
            }
            _wasOverspeed = isNowOverspeed;

            // ── Landing lights below 10 000 ft AGL ───────────────────────────────
            bool lightsRequired  = ctx.AGL < 9_500;
            bool lightsViolating = lightsRequired && !ctx.LandingLightOn;
            if (lightsViolating && !_lightsViolationActive)
            {
                _lightsViolationActive = true;
                LightsViolationCount++;
                OnLog?.Invoke(_("Log_LightsBelow10k", (int)ctx.AGL), Theme.Warning);
                OnOsdMessage?.Invoke("LANDING LT OFF  −5 PTS", OsdSeverity.Warning);
            }
            else if (!lightsViolating)
                _lightsViolationActive = false;

            // ── Reminder OSD at 10 500 ft AGL in Descent with lights off ─────────
            if (ctx.Phase == FlightPhase.Descent && ctx.AGL <= 10_500 && !ctx.LandingLightOn && !_landingLightReminderSent)
            {
                _landingLightReminderSent = true;
                OnOsdMessage?.Invoke("LANDING LT OFF", OsdSeverity.Warning);
            }
            else if (ctx.AGL > 10_500)
                _landingLightReminderSent = false;

            // ── 10 000 ft callout (climb) ─────────────────────────────────────────
            if (ctx.Phase == FlightPhase.Climb && ctx.AGL >= 10_000 && !_passing10kFtSent)
            {
                _passing10kFtSent = true;
                OnOsdMessage?.Invoke("10 000 FT", OsdSeverity.Info);
            }

            // ── Beacon light ──────────────────────────────────────────────────────
            bool beaconExempt = (_beaconStrobeSharedAircraft.Contains(AircraftIcao ?? "") && ctx.StrobeOn)
                             || ctx.HotelMode;
            if (!ctx.BeaconLightOn && !_beaconViolationActive && !beaconExempt)
            {
                _beaconViolationActive = true;
                LightsViolationCount++;
            }
            else if (ctx.BeaconLightOn || beaconExempt)
                _beaconViolationActive = false;

            // ── Transition Altitude ───────────────────────────────────────────────
            if (OriginTransitionAltFt > 0 && ctx.Phase == FlightPhase.Climb
                && ctx.AltitudeMsl >= (int)OriginTransitionAltFt && !_taOsdSent)
            {
                _taOsdSent = true;
                OnLog?.Invoke(_("Log_TransitionAlt", (int)OriginTransitionAltFt), Theme.MainText);
                OnOsdMessage?.Invoke("TRANS ALT  SET STD 1013", OsdSeverity.Warning);
            }

            // ── STD pressure check ────────────────────────────────────────────────
            if (OriginTransitionAltFt > 0 && ctx.Phase == FlightPhase.Climb
                && ctx.AltitudeMsl >= (int)(OriginTransitionAltFt + 1000) && !_originQnhChecked)
            {
                _originQnhChecked = true;
                CheckStdPressure(ctx.QnhMb);
            }

            // ── Transition Level ──────────────────────────────────────────────────
            if (DestTransitionLevelFt > 0
                && (ctx.Phase == FlightPhase.Descent || ctx.Phase == FlightPhase.Approach)
                && ctx.AltitudeMsl <= (int)DestTransitionLevelFt && !_tlOsdSent)
            {
                _tlOsdSent = true;
                int fl = (int)Math.Round(DestTransitionLevelFt / 100.0);
                OnLog?.Invoke(_("Log_TransitionLevel", $"{fl:D3}"), Theme.MainText);
                OnOsdMessage?.Invoke("TRANS LEVEL  SET QNH", OsdSeverity.Warning);
            }

            // ── QNH dest check (500 ft below TL) ─────────────────────────────────
            string destIcao = EffectiveDestination ?? DestIcao;
            if (DestTransitionLevelFt > 0
                && (ctx.Phase == FlightPhase.Descent || ctx.Phase == FlightPhase.Approach)
                && ctx.AltitudeMsl <= (int)(DestTransitionLevelFt - 1000) && !_destQnhChecked)
            {
                if (!string.IsNullOrEmpty(destIcao))
                {
                    _destQnhChecked = true;
                    CheckQnhAsync(destIcao, ctx.QnhMb).ConfigureAwait(false);
                }
            }
        }

        public void CheckStabilizedApproachGate(ApproachContext ctx)
        {
            if (_approachGateEvaluated) return;
            if (!ctx.ActivePirepPresent) return;

            double agl = ctx.AGL;
            if (!(_prevApproachAgl > 1000 && agl <= 1000))
            {
                _prevApproachAgl = agl;
                return;
            }

            _approachGateEvaluated = true;

            var (vappMin, vappMax) = AircraftPerformanceTable.GetApproachSpeedRange(AircraftIcao);
            int deductions = 0;

            // 1. ILS tuning check — must run first so subsequent criteria know approach type
            if (_expectedIls != null && !_ilsGateChecked)
            {
                _ilsGateChecked = true;
                double freqDelta = Math.Abs(ctx.Nav1FreqMhz - _expectedIls.FrequencyMhz);
                if (freqDelta > 0.05)
                {
                    OnLog?.Invoke(_("Log_IlsApproachSkipped",
                        $"{ctx.Nav1FreqMhz:F2}", $"{_expectedIls.FrequencyMhz:F2}"), Theme.MainText);
                    _expectedIls  = null;
                    _daAltitudeFt = 0;
                }
                else
                {
                    OnLog?.Invoke(_("Log_IlsTunedOk",
                        $"{ctx.Nav1FreqMhz:F2}", $"{_expectedIls.Course:F0}"), Theme.Success);
                }
            }

            // 2. Speed
            if (ctx.IAS < vappMin || ctx.IAS > vappMax)
            {
                if (IsOnAtcFrequency?.Invoke() != true) deductions += 5;
                OnLog?.Invoke(_("Log_ApproachGateSpeed", ctx.IAS, vappMin, vappMax), Theme.Warning);
            }

            // 3. Descent rate
            if (ctx.VS < -1000)
            {
                deductions += 5;
                OnLog?.Invoke(_("Log_ApproachGateVs", ctx.VS), Theme.Warning);
            }
            else if (ctx.VS > -100)
            {
                deductions += 5;
                OnLog?.Invoke(_("Log_ApproachGateVsLow", ctx.VS), Theme.Warning);
            }

            // 4. Bank angle — ILS approaches only
            if (_expectedIls != null && Math.Abs(ctx.Bank) > 7.0)
            {
                deductions += 3;
                OnLog?.Invoke(_("Log_ApproachGateBank", ctx.Bank.ToString("F1")), Theme.Warning);
            }

            // 5. Pitch angle
            if (ctx.Pitch < -2.5 || ctx.Pitch > 10.0)
            {
                deductions += 3;
                OnLog?.Invoke(_("Log_ApproachGatePitch", ctx.Pitch.ToString("F1")), Theme.Warning);
            }

            // 6. Gear
            if (!ctx.GearDown)
            {
                deductions += 5;
                OnLog?.Invoke(_("Log_ApproachGateGear"), Theme.Warning);
            }

            // 7. Flaps
            if (ctx.FlapsPosition < 50)
            {
                deductions += 4;
                OnLog?.Invoke(_("Log_ApproachGateFlaps", ctx.FlapsPosition.ToString("F0")), Theme.Warning);
            }

            StabilizedDeductions = deductions;

            if (deductions == 0)
                OnLog?.Invoke(_("Log_ApproachGateOk", (int)agl), Theme.Success);
            else
            {
                OnLog?.Invoke(_("Log_ApproachGateUnstable", (int)agl, deductions), Theme.Warning);
                OnOsdMessage?.Invoke($"UNSTABILIZED  −{deductions} PTS", OsdSeverity.Critical);
            }

            // QNH fallback when no Transition Level was set
            string destIcao = EffectiveDestination ?? DestIcao;
            if (DestTransitionLevelFt <= 0 && !_destQnhChecked && !string.IsNullOrEmpty(destIcao))
            {
                _destQnhChecked = true;
                CheckQnhAsync(destIcao, ctx.QnhMb).ConfigureAwait(false);
            }

            _prevApproachAgl = agl;
        }

        public void CheckApproachBelowGate(ApproachContext ctx)
        {
            double agl = ctx.AGL;

            // ── Localizer heading alignment (below 500 ft, above 50 ft) ──────────
            if (_expectedIls != null && agl < 500 && agl > 50)
            {
                double hdgDelta = ((ctx.Heading - _expectedIls.Course + 540) % 360) - 180;
                if (Math.Abs(hdgDelta) > 5.0 && LocalizerViolations < 2)
                {
                    LocalizerViolations++;
                    OnLog?.Invoke(_("Log_LocalizerDeviation",
                        $"{ctx.Heading:F0}", $"{hdgDelta:+0.0;-0.0}", $"{_expectedIls.Course:F0}"), Theme.Warning);
                }
            }

            // ── Decision altitude check ───────────────────────────────────────────
            if (_daAltitudeFt > 0 && ctx.AltitudeMsl < _daAltitudeFt && !ctx.IsOnGround && !BelowMinimums)
            {
                BelowMinimums = true;
                OnLog?.Invoke(_("Log_BelowMinimums", ctx.AltitudeMsl, $"{_daAltitudeFt:F0}"), Theme.Warning);
            }

            // ── Waypoint sequencing ───────────────────────────────────────────────
            if (_approachFixes != null && _nextFixIndex < _approachFixes.Count)
            {
                var fix = _approachFixes[_nextFixIndex];
                double cosLat = Math.Cos(ctx.Lat * Math.PI / 180.0);
                double dN = (fix.Lat - ctx.Lat) * 111320.0;
                double dE = (fix.Lon - ctx.Lon) * 111320.0 * cosLat;
                double distM = Math.Sqrt(dN * dN + dE * dE);
                double thresholdM = fix.IsFlyover ? 556.0 : 926.0; // 0.3 NM / 0.5 NM
                if (distM < thresholdM)
                {
                    string fixLabel = string.IsNullOrEmpty(fix.FixType)
                        ? fix.Name
                        : $"{fix.Name} ({fix.FixType})";
                    OnLog?.Invoke(_("Log_ApproachFix", fixLabel, (int)agl), Theme.MainText);
                    _nextFixIndex++;
                }
            }
        }

        // ── QNH compliance ────────────────────────────────────────────────────────

        internal async Task CheckQnhAsync(string icao, double aircraftQnhMb)
        {
            if (string.IsNullOrWhiteSpace(icao) || aircraftQnhMb <= 0 || _weatherService == null) return;

            double? stationQnh = await _weatherService.GetQnhMbAsync(icao);
            if (stationQnh == null)
            {
                OnLog?.Invoke(_("Log_QnhUnavailable", icao), Theme.Warning);
                return;
            }

            double diff  = Math.Abs(aircraftQnhMb - stationQnh.Value);
            string label = $"QNH | Avión: {aircraftQnhMb:F0} hPa  {icao}: {stationQnh.Value:F0} hPa  Δ{diff:F0} hPa";

            if (diff <= 2.0)
            {
                OnLog?.Invoke(_("Log_QnhOk", label), Theme.Success);
            }
            else
            {
                QnhViolations++;
                OnLog?.Invoke(_("Log_QnhPenalty", label), Theme.Warning);
                OnOsdMessage?.Invoke("PENALTY  QNH  −5 PTS", OsdSeverity.Warning);
            }
        }

        internal void CheckStdPressure(double aircraftQnhMb)
        {
            const double stdMb = 1013.25;
            double diff  = Math.Abs(aircraftQnhMb - stdMb);
            string label = $"STD | Avión: {aircraftQnhMb:F0} hPa  STD: 1013 hPa  Δ{diff:F0} hPa";
            if (diff <= 2.0)
            {
                OnLog?.Invoke(_("Log_QnhOk", label), Theme.Success);
            }
            else
            {
                QnhViolations++;
                OnLog?.Invoke(_("Log_QnhPenalty", label), Theme.Warning);
                OnOsdMessage?.Invoke("PENALTY  QNH  −5 PTS", OsdSeverity.Warning);
            }
        }
    }
}
