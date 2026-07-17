using System;
using System.Drawing;
using vmsOpenAcars.Models;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Core.Flight
{
    // Input snapshot passed to Update() on every telemetry cycle.
    internal struct PhaseInput
    {
        public int    Altitude, GroundSpeed, VerticalSpeed;
        public bool   IsOnGround;
        public double Pitch;
        public double Lat, Lon, Heading;

        // Plan data
        public int    CruiseAltitude;
        public double TotalDistanceNm, DestinationElevation;
        public double DistanceToDestinationNm; // -1 = unknown

        // Airports
        public string Origin, Destination;

        // Engine / hotel mode
        public bool AreEnginesOn, HotelModeActive;

        // Touch-and-go: seconds since last touchdown (double.MaxValue = no touchdown)
        public double SecondsSinceTouchdown;
    }

    // Pure phase-state machine extracted from FlightManager.
    // Owns all phase-transition timers.
    // All side-effects are delegated via events; no direct API or logging dependencies.
    internal sealed class FlightPhaseStateMachine
    {
        // ── Phase timer state ─────────────────────────────────────────────────
        private DateTime _phaseStartTime       = DateTime.UtcNow;
        private DateTime _lastTaxiPosEvent     = DateTime.MinValue;
        private DateTime _climbStableStart     = DateTime.MinValue;
        private DateTime _descentStart         = DateTime.MinValue;
        private DateTime _stepClimbStart       = DateTime.MinValue;
        private DateTime _descentToClimbStart  = DateTime.MinValue;
        private DateTime _goAroundStart        = DateTime.MinValue;
        private DateTime _pushbackStartTime    = DateTime.MinValue;
        private DateTime _stoppedStartTime     = DateTime.MinValue;
        private double   _maxAltitudeReached;
        private bool     _wasOnGround          = true;
        private bool     _hasLandedThisFlight;
        // True once GS ≤ 0.5 kt is observed during Boarding — gates pushback/taxi detection
        // so that pre-existing movement at StartFlight does not trigger an immediate false positive.
        private bool     _boardingStationaryConfirmed;

        // ── Constants ─────────────────────────────────────────────────────────
        private const double PushbackMaxSpeed       = 6.0;
        private const int    PushbackMinSec         = 8;
        private const double TaxiOutMinSpeed        = 5.0;
        private const int    TaxiOutMinSec          = 2;
        private const double SingleEngineMinRatio   = 0.5;

        // ── Public state ──────────────────────────────────────────────────────
        public FlightPhase CurrentPhase { get; private set; } = FlightPhase.Idle;

        // ── Events ────────────────────────────────────────────────────────────
        // Fired on every phase change; (from, to).
        public event Action<FlightPhase, FlightPhase> OnTransitioned;

        // Fired once when wheels-off rotation is detected (gs, alt, vs).
        public event Action<int, int, int> OnTakeoffDetected;

        // Fired once at touchdown (verticalSpeed).
        public event Action<int> OnTouchdownDetected;

        // Fired every 2 s while taxiing (lat, lon, heading, airportIcao, isTaxiIn).
        public event Action<double, double, double, string, bool> OnTaxiPositionUpdate;

        // Fired when stopped + engines off/hotel mode (block on condition met).
        public event Action OnBlockOnDetected;

        // Fired when movement starts from Boarding (block off trigger).
        public event Action OnBlockOffNeeded;

        // Fired on go-around or touch-and-go — caller must reset approach gate data.
        public event Action OnApproachGateReset;

        // Fired specifically on touch-and-go (caller resets touchdown state).
        public event Action OnTouchAndGo;

        // Logging / OSD (subscribers format and display; no direct UI dependency here).
        public event Action<string, Color>       OnLog;
        public event Action<string, OsdSeverity> OnOsdMessage;

        // ── API ───────────────────────────────────────────────────────────────
        public void SetPhase(FlightPhase phase)
        {
            CurrentPhase   = phase;
            _phaseStartTime = DateTime.UtcNow;
        }

        public void Reset()
        {
            CurrentPhase          = FlightPhase.Idle;
            _phaseStartTime       = DateTime.UtcNow;
            _lastTaxiPosEvent     = DateTime.MinValue;
            _climbStableStart     = DateTime.MinValue;
            _descentStart         = DateTime.MinValue;
            _stepClimbStart       = DateTime.MinValue;
            _descentToClimbStart  = DateTime.MinValue;
            _goAroundStart        = DateTime.MinValue;
            _pushbackStartTime           = DateTime.MinValue;
            _stoppedStartTime            = DateTime.MinValue;
            _maxAltitudeReached          = 0;
            _wasOnGround                 = true;
            _hasLandedThisFlight         = false;
            _boardingStationaryConfirmed = false;
        }

        // ── Core update ───────────────────────────────────────────────────────
        public void Update(PhaseInput inp)
        {
            var prev = CurrentPhase;

            if (inp.Altitude > _maxAltitudeReached)
                _maxAltitudeReached = inp.Altitude;

            int    cruiseAlt    = inp.CruiseAltitude > 0 ? inp.CruiseAltitude : 10000;
            double totalDist    = inp.TotalDistanceNm > 0 ? inp.TotalDistanceNm : 100;
            double approachThr  = Math.Min(totalDist * 0.1, 20);
            double aglThr       = Math.Min(5000, cruiseAlt * 0.2);
            bool   isClimbing   = inp.VerticalSpeed > 100;
            bool   canChange    = (DateTime.UtcNow - _phaseStartTime).TotalSeconds >= 5;

            // ── Rotation detection (while still on ground per _wasOnGround) ──
            if (_wasOnGround && inp.GroundSpeed > 60 && inp.Pitch > 2.0 && inp.Pitch < 20
                && !_hasLandedThisFlight)
            {
                if (CurrentPhase != FlightPhase.Takeoff && CurrentPhase != FlightPhase.TakeoffRoll)
                {
                    OnTakeoffDetected?.Invoke(inp.GroundSpeed, inp.Altitude, inp.VerticalSpeed);
                    TransitionTo(FlightPhase.Takeoff, prev);
                    prev = CurrentPhase;
                }
            }

            // ── Liftoff (wheels leave ground) ─────────────────────────────────
            if (_wasOnGround && !inp.IsOnGround && CurrentPhase == FlightPhase.Takeoff)
                _wasOnGround = inp.IsOnGround;

            // ── Touchdown detection (air → ground) ───────────────────────────
            if (!_wasOnGround && inp.IsOnGround && CurrentPhase != FlightPhase.AfterLanding)
            {
                if (CurrentPhase == FlightPhase.Descent  ||
                    CurrentPhase == FlightPhase.Approach ||
                    CurrentPhase == FlightPhase.Landing)
                {
                    _hasLandedThisFlight = true;
                    OnTouchdownDetected?.Invoke(inp.VerticalSpeed);
                    TransitionTo(FlightPhase.AfterLanding, prev);
                    _wasOnGround = inp.IsOnGround;
                    return;
                }
            }

            if (inp.IsOnGround)
                HandleGroundPhases(inp, prev);
            else
                HandleAirPhases(inp, prev, cruiseAlt, approachThr, aglThr, canChange, isClimbing);

            _wasOnGround = inp.IsOnGround;
        }

        // ── Ground phase logic ────────────────────────────────────────────────
        private void HandleGroundPhases(PhaseInput inp, FlightPhase prev)
        {
            switch (CurrentPhase)
            {
                case FlightPhase.Boarding:
                    if (inp.GroundSpeed <= 0.5)
                    {
                        // Aircraft is stationary — confirm we've seen a stop, reset any pending timer.
                        _boardingStationaryConfirmed = true;
                        _pushbackStartTime = DateTime.MinValue;
                    }
                    else if (_boardingStationaryConfirmed)
                    {
                        // Movement detected after a confirmed stop — now safe to evaluate pushback/taxi.
                        if (_pushbackStartTime == DateTime.MinValue)
                            _pushbackStartTime = DateTime.UtcNow;

                        double secMoving = (DateTime.UtcNow - _pushbackStartTime).TotalSeconds;

                        if (inp.GroundSpeed <= PushbackMaxSpeed && secMoving >= PushbackMinSec)
                        {
                            _pushbackStartTime = DateTime.MinValue;
                            TransitionTo(FlightPhase.Pushback, prev);
                            OnBlockOffNeeded?.Invoke();
                        }
                        else if (inp.GroundSpeed > TaxiOutMinSpeed && secMoving >= TaxiOutMinSec)
                        {
                            _pushbackStartTime = DateTime.MinValue;
                            TransitionTo(FlightPhase.TaxiOut, prev);
                            OnBlockOffNeeded?.Invoke();
                        }
                    }
                    // else: aircraft was already moving at StartFlight — ignore until it stops.
                    break;

                case FlightPhase.Pushback:
                    if (inp.GroundSpeed > TaxiOutMinSpeed)
                        TransitionTo(FlightPhase.TaxiOut, prev);
                    break;

                case FlightPhase.TaxiOut:
                    EmitTaxiPosition(inp.Lat, inp.Lon, inp.Heading, inp.Origin, false);
                    if (inp.GroundSpeed > 30 && inp.Pitch < 1.0)
                        TransitionTo(FlightPhase.TakeoffRoll, prev);
                    break;

                case FlightPhase.TakeoffRoll:
                    if (inp.GroundSpeed > 50 && inp.Pitch > 2.0)
                    {
                        OnLog?.Invoke(_("Log_TakeoffRotation", inp.GroundSpeed, inp.Pitch.ToString("F1")), Theme.Success);
                        OnTakeoffDetected?.Invoke(inp.GroundSpeed, inp.Altitude, inp.VerticalSpeed);
                        TransitionTo(FlightPhase.Takeoff, prev);
                    }
                    else if (inp.GroundSpeed < 30)
                    {
                        TransitionTo(FlightPhase.TaxiOut, prev);
                    }
                    break;

                case FlightPhase.AfterLanding:
                    EmitTaxiPosition(inp.Lat, inp.Lon, inp.Heading, inp.Destination, true);
                    if (inp.GroundSpeed < 40)
                        TransitionTo(FlightPhase.TaxiIn, prev);
                    break;

                case FlightPhase.TaxiIn:
                    EmitTaxiPosition(inp.Lat, inp.Lon, inp.Heading, inp.Destination, true);
                    HandleTaxiIn(inp, prev);
                    break;
            }
        }

        private void HandleTaxiIn(PhaseInput inp, FlightPhase prev)
        {
            if (inp.GroundSpeed < 1)
            {
                if (_stoppedStartTime == DateTime.MinValue)
                    _stoppedStartTime = DateTime.UtcNow;

                if ((DateTime.UtcNow - _stoppedStartTime).TotalSeconds >= 90 &&
                    (!inp.AreEnginesOn || inp.HotelModeActive))
                {
                    _stoppedStartTime = DateTime.MinValue;
                    OnBlockOnDetected?.Invoke();
                    TransitionTo(FlightPhase.OnBlock, prev);
                }
            }
            else
            {
                _stoppedStartTime = DateTime.MinValue;
            }
        }

        // ── Air phase logic ───────────────────────────────────────────────────
        private void HandleAirPhases(PhaseInput inp, FlightPhase prev,
            int cruiseAlt, double approachThr, double aglThr, bool canChange, bool isClimbing)
        {
            double altAboveDest = inp.Altitude - inp.DestinationElevation;

            switch (CurrentPhase)
            {
                case FlightPhase.Takeoff:
                case FlightPhase.TakeoffRoll:
                    if (!inp.IsOnGround && (isClimbing || inp.VerticalSpeed > 0))
                        TransitionTo(FlightPhase.Climb, prev);
                    break;

                case FlightPhase.Climb:
                {
                    double altDiff    = Math.Abs(inp.Altitude - cruiseAlt);
                    bool   nearCruise = altDiff < 500;
                    bool   lowVs      = Math.Abs(inp.VerticalSpeed) < 200;
                    bool   timeout    = (DateTime.UtcNow - _phaseStartTime).TotalMinutes >= 5;
                    bool   veryLowVs  = Math.Abs(inp.VerticalSpeed) < 100;

                    if ((nearCruise && lowVs) || (timeout && veryLowVs))
                    {
                        if (_climbStableStart == DateTime.MinValue)
                            _climbStableStart = DateTime.UtcNow;
                        else if ((DateTime.UtcNow - _climbStableStart).TotalSeconds >= 10)
                        {
                            _climbStableStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.Enroute, prev);
                        }
                    }
                    else { _climbStableStart = DateTime.MinValue; }

                    if (inp.VerticalSpeed < -500 && inp.Altitude < _maxAltitudeReached - 500
                        && (DateTime.UtcNow - _phaseStartTime).TotalSeconds >= 5)
                    {
                        if (_descentStart == DateTime.MinValue) _descentStart = DateTime.UtcNow;
                        else if ((DateTime.UtcNow - _descentStart).TotalSeconds >= 20)
                        {
                            _descentStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.Descent, prev);
                        }
                    }
                    else { _descentStart = DateTime.MinValue; }
                    break;
                }

                case FlightPhase.Enroute:
                {
                    if (inp.VerticalSpeed > 500 && inp.Altitude < cruiseAlt - 500)
                    {
                        if (_stepClimbStart == DateTime.MinValue) _stepClimbStart = DateTime.UtcNow;
                        else if ((DateTime.UtcNow - _stepClimbStart).TotalSeconds >= 10)
                        {
                            _stepClimbStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.Climb, prev);
                        }
                    }
                    else { _stepClimbStart = DateTime.MinValue; }

                    if (inp.VerticalSpeed < -500 && inp.Altitude < _maxAltitudeReached - 500)
                    {
                        if (_descentStart == DateTime.MinValue) _descentStart = DateTime.UtcNow;
                        else if ((DateTime.UtcNow - _descentStart).TotalSeconds >= 20)
                        {
                            _descentStart = DateTime.MinValue;
                            TransitionTo(FlightPhase.Descent, prev);
                        }
                    }
                    else { _descentStart = DateTime.MinValue; }
                    break;
                }

                case FlightPhase.Descent:
                {
                    bool nearDest = (inp.DistanceToDestinationNm > 0
                                        && inp.DistanceToDestinationNm < approachThr)
                                 || (altAboveDest < aglThr && altAboveDest > 0);

                    if (nearDest)
                    {
                        if (canChange)
                        {
                            OnLog?.Invoke(_("Log_ApproachStarted"), Theme.Approach);
                            TransitionTo(FlightPhase.Approach, prev);
                        }
                        _descentToClimbStart = DateTime.MinValue;
                    }
                    else if (inp.VerticalSpeed > 500)
                    {
                        if (_descentToClimbStart == DateTime.MinValue)
                            _descentToClimbStart = DateTime.UtcNow;
                        else if ((DateTime.UtcNow - _descentToClimbStart).TotalSeconds >= 20)
                        {
                            _descentToClimbStart = DateTime.MinValue;
                            OnLog?.Invoke(_("Log_ResumingClimb", inp.Altitude), Theme.MainText);
                            TransitionTo(FlightPhase.Climb, prev);
                        }
                    }
                    else { _descentToClimbStart = DateTime.MinValue; }
                    break;
                }

                case FlightPhase.Approach:
                {
                    bool goAround = inp.VerticalSpeed > 600
                                 && altAboveDest > 100 && altAboveDest < 3000
                                 && (DateTime.UtcNow - _phaseStartTime).TotalSeconds >= 30;

                    if (goAround)
                    {
                        if (_goAroundStart == DateTime.MinValue)
                            _goAroundStart = DateTime.UtcNow;
                        else if ((DateTime.UtcNow - _goAroundStart).TotalSeconds >= 8)
                        {
                            _goAroundStart = DateTime.MinValue;
                            OnLog?.Invoke(_("Log_GoAround", (int)altAboveDest, inp.VerticalSpeed), Theme.Warning);
                            OnOsdMessage?.Invoke("GO AROUND", OsdSeverity.Warning);
                            TransitionTo(FlightPhase.Climb, prev);
                        }
                    }
                    else { _goAroundStart = DateTime.MinValue; }
                    break;
                }

                case FlightPhase.AfterLanding:
                    // Touch-and-go: aircraft went airborne again after landing
                    if (inp.GroundSpeed > 60 && inp.SecondsSinceTouchdown >= 5.0)
                    {
                        OnLog?.Invoke(_("Log_TouchAndGo", inp.GroundSpeed), Theme.Warning);
                        _hasLandedThisFlight = false;
                        OnTouchAndGo?.Invoke();
                        OnApproachGateReset?.Invoke();
                        TransitionTo(FlightPhase.Climb, prev);
                    }
                    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void TransitionTo(FlightPhase to, FlightPhase from)
        {
            if (from == FlightPhase.Approach && to == FlightPhase.Climb)
                OnApproachGateReset?.Invoke();
            CurrentPhase    = to;
            _phaseStartTime = DateTime.UtcNow;
            OnTransitioned?.Invoke(from, to);
        }

        private void EmitTaxiPosition(double lat, double lon, double heading, string airport, bool isTaxiIn)
        {
            if ((DateTime.UtcNow - _lastTaxiPosEvent).TotalSeconds >= 2.0)
            {
                _lastTaxiPosEvent = DateTime.UtcNow;
                OnTaxiPositionUpdate?.Invoke(lat, lon, heading, airport, isTaxiIn);
            }
        }
    }
}
