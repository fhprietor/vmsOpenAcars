using System;
using System.Threading.Tasks;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Core.Flight
{
    public partial class FlightManager
    {
        private double CalculateGForce(int verticalSpeedFpm)
        {
            int vs = Math.Abs(verticalSpeedFpm);
            if (vs <= 30) return 1.00;
            if (vs <= 60) return 1.02;
            if (vs <= 90) return 1.05;
            if (vs <= 120) return 1.10;
            if (vs <= 150) return 1.18;
            if (vs <= 180) return 1.28;
            if (vs <= 210) return 1.38;
            if (vs <= 240) return 1.48;
            if (vs <= 270) return 1.58;
            if (vs <= 300) return 1.68;
            if (vs <= 350) return 1.85;
            if (vs <= 400) return 2.00;
            if (vs <= 500) return 2.20;
            if (vs <= 600) return 2.40;
            return 2.50;
        }

        private void RegisterTouchdown(int verticalSpeed)
        {
            if (_td.Captured) return;
            double gforce = CalculateGForce(verticalSpeed);
            _td.Capture(verticalSpeed, _currentPitch, _currentBank, gforce, CurrentLat, CurrentLon, CurrentHeading);
            _approachValidator.ClearBelowMinimumsIfLanded();
            _reverserMonitor.OnTouchdown();
            OnLog?.Invoke("Reversa: monitoreando despliegue post-aterrizaje (offset 0x207C/0x217C)", Theme.MainText);
            OnLandingDetected?.Invoke(verticalSpeed, gforce, _currentPitch, _currentBank);
        }

        private void ApplyRawData(RawTelemetryData data)
        {
            LastRawData = data;
            HasSimulatorData = true;

            CurrentAltitude          = (int)data.AltitudeFeet;
            CurrentGroundSpeed       = (int)data.GroundSpeedKt;
            CurrentVerticalSpeed     = (int)data.VerticalSpeedFpm;
            IsOnGround               = data.IsOnGround;
            if (data.GroundAltitudeFeet > 0)
                _groundAltitudeFeet  = data.GroundAltitudeFeet;
            CurrentLat               = data.Latitude;
            CurrentLon               = data.Longitude;
            CurrentHeading           = data.HeadingDeg;
            _currentPitch            = data.PitchDeg;
            _currentBank             = data.BankDeg;
            CurrentIndicatedAirspeed = data.IndicatedAirspeedKt > 0
                                           ? (int)data.IndicatedAirspeedKt
                                           : (int)data.GroundSpeedKt;
            CurrentFuel              = data.FuelLbs * 0.453592;
            CurrentTransponder       = data.Transponder;

            if (data.AutopilotEngaged)
            {
                _apEngagedCounter = Math.Min(_apEngagedCounter + 1, ApEngageDebounce);
                if (_apEngagedCounter >= ApEngageDebounce && !AutopilotEngaged)
                {
                    bool isAirbornePhase = CurrentPhase == FlightPhase.Takeoff  ||
                                          CurrentPhase == FlightPhase.Climb     ||
                                          CurrentPhase == FlightPhase.Enroute   ||
                                          CurrentPhase == FlightPhase.Descent   ||
                                          CurrentPhase == FlightPhase.Approach;
                    if (isAirbornePhase)
                    {
                        AutopilotEngaged = true;
                        OnLog?.Invoke(_("Log_AutopilotEngaged", data.ApNavMode, data.ApVertMode, (int)CurrentAGL), Theme.MainText);
                    }
                }
            }
            else
            {
                if (AutopilotEngaged)
                    OnLog?.Invoke(_("Log_AutopilotDisengaged", (int)CurrentAGL), Theme.Warning);
                AutopilotEngaged  = false;
                _apEngagedCounter = 0;
            }

            SimTime            = DateTime.UtcNow;
            RadarAltitude      = data.RadarAltitudeFeet;
            PositionOrder      = data.Order;
            _isParkingBrakeSet = data.ParkingBrakeOn;

            IsGearDown           = data.GearDown;
            CurrentFlapsPosition = data.FlapsPercent;
            FlapsLabel           = data.FlapsLabel;

            DebounceState(data.SpoilersDeployed, ref _isSpoilersOn,      ref _pendingSpoilers, ref _spoilersPending, SpoilersDebounceSeconds);
            AreSpoilersDeployed  = _isSpoilersOn;
            AutobrakeSetting     = data.AutobrakeSetting;

            DebounceState(data.NavLightOn,     ref _isNavOn,          ref _pendingNavOn,    ref _navPending,     LightDebounceSeconds);
            DebounceState(data.BeaconLightOn,  ref _isBeaconOn,       ref _pendingBeaconOn, ref _beaconPending,  LightDebounceSeconds);
            DebounceState(data.LandingLightOn, ref _isLandingLightOn, ref _pendingLandingOn, ref _landingPending, LightDebounceSeconds);
            DebounceState(data.TaxiLightOn,    ref _isTaxiLightOn,    ref _pendingTaxiOn,   ref _taxiPending,    LightDebounceSeconds);
            DebounceState(data.StrobeLightOn,  ref _isStrobeOn,       ref _pendingStrobeOn, ref _strobePending,  LightDebounceSeconds);
            IsNavLightOn     = _isNavOn;
            IsBeaconLightOn  = _isBeaconOn;
            IsLandingLightOn = _isLandingLightOn;
            IsTaxiLightOn    = _isTaxiLightOn;
            IsStrobeLightOn  = _isStrobeOn;

            ApNavMode     = data.ApNavMode;
            ApVertMode    = data.ApVertMode;
            AircraftQnhMb = data.AircraftQnhMb;
            N1_1 = data.N1_1; N1_2 = data.N1_2;
        }

        private void CheckEngineEvents(RawTelemetryData data)
        {
            if (data.EnginesRunning && !_areEnginesOn && !string.IsNullOrEmpty(ActivePirepId))
            {
                if (!_isBeaconOn && !data.HotelModeActive)
                {
                    _approachValidator.AddLightsViolation();
                    OnLog?.Invoke(_("Log_PenaltyBeacon"), Theme.Warning);
                }

                if (CurrentPhase == FlightPhase.Boarding && !_timer.BlockOffRecorded && !data.HotelModeActive)
                {
                    OnLog?.Invoke(_("Log_BlockOffEngines"), Theme.MainText);
                    Task.Run(() => UpdateBlockOffTime());
                }
            }

            if (!data.EnginesRunning && _areEnginesOn && CurrentPhase == FlightPhase.TaxiIn)
            {
                string shutdownWarning = _reverserMonitor.CheckShutdown();
                if (shutdownWarning != null)
                {
                    _pen.EngineCooldownViolation = true;
                    OnLog?.Invoke($"⚠️ {shutdownWarning}", Theme.Warning);
                }

                string addonWarning = _reverserMonitor.CheckAddonSupport();
                if (addonWarning != null)
                    OnLog?.Invoke($"ℹ️ {addonWarning}", Theme.MainText);
            }

            _engStartMonitor.Update(data.Eng1Running, data.Eng2Running,
                data.N2_1, data.N2_2,
                data.OilPress_1, data.OilPress_2,
                data.OilTemp_1, data.OilTemp_2);

            if (!_engStabilizedOsdFired && !string.IsNullOrEmpty(ActivePirepId))
            {
                bool anyRunning = data.Eng1Running || data.Eng2Running;
                bool allStab    = (!data.Eng1Running || _engStartMonitor.Eng1Stabilized)
                               && (!data.Eng2Running || _engStartMonitor.Eng2Stabilized);
                if (anyRunning && allStab &&
                    (CurrentPhase == FlightPhase.Boarding ||
                     CurrentPhase == FlightPhase.Pushback ||
                     CurrentPhase == FlightPhase.TaxiOut))
                {
                    _engStabilizedOsdFired = true;
                    OnOsdMessage?.Invoke("ENGINES STABLE  ✓", OsdSeverity.Info);
                    OnLog?.Invoke("✅ Motores estabilizados — aceite en temperatura", Theme.Success);
                }
            }

            _areEnginesOn    = data.EnginesRunning;
            _hotelModeActive = data.HotelModeActive;
        }

        private void UpdateSingleEngineTaxi(RawTelemetryData data)
        {
            if (data.Eng1Running && data.Eng2Running)
                _pen.BothEnginesRunning = true;

            if (string.IsNullOrEmpty(ActivePirepId)) return;

            bool oneEngineOnly = data.Eng1Running ^ data.Eng2Running;
            bool moving        = CurrentGroundSpeed > 3;
            if (CurrentPhase == FlightPhase.TaxiOut)
            {
                if (moving)               _pen.TaxiOutMovingCycles++;
                if (moving && oneEngineOnly) _pen.TaxiOutSingleEngineCycles++;
            }
            else if (CurrentPhase == FlightPhase.TaxiIn)
            {
                if (moving)               _pen.TaxiInMovingCycles++;
                if (moving && oneEngineOnly) _pen.TaxiInSingleEngineCycles++;
            }
        }

        private void UpdateFlightTracking(RawTelemetryData data)
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return;

            if (_lastPosition.HasValue)
            {
                double distKm = CalculateDistanceKm(
                    _lastPosition.Value.lat, _lastPosition.Value.lon,
                    data.Latitude, data.Longitude);
                if (distKm > 0 && distKm < 10)
                    _totalDistanceKm += distKm;
            }
            _lastPosition    = (data.Latitude, data.Longitude);
            _lastPositionTime = DateTime.UtcNow;

            if (!data.IsOnGround && !_lastAirborneTime.HasValue)
                _lastAirborneTime = DateTime.UtcNow;

            if (data.IsOnGround && _td.Captured)
                _reverserMonitor.Update(data.Rev1Pct, data.Rev2Pct);

            _phaseMachine.Update(BuildPhaseInput());

            if (!data.IsOnGround)
            {
                var ctx = BuildApproachContext(data);
                _approachValidator.CheckViolations(ctx);
                if (CurrentPhase == FlightPhase.Approach)
                {
                    _approachValidator.CheckStabilizedApproachGate(ctx);
                    _approachValidator.CheckApproachBelowGate(ctx);
                }
            }
        }

        private PhaseInput BuildPhaseInput() => new PhaseInput
        {
            Altitude                = CurrentAltitude,
            GroundSpeed             = CurrentGroundSpeed,
            VerticalSpeed           = CurrentVerticalSpeed,
            IsOnGround              = IsOnGround,
            Pitch                   = _currentPitch,
            Lat                     = CurrentLat,
            Lon                     = CurrentLon,
            Heading                 = CurrentHeading,
            CruiseAltitude          = _activePlan?.CruiseAltitude ?? 10000,
            TotalDistanceNm         = _activePlan?.Distance ?? 100,
            DestinationElevation    = _destinationElevation,
            DistanceToDestinationNm = -1,
            Origin                  = _activePlan?.Origin ?? _currentAirport,
            Destination             = _activePlan?.Destination ?? _currentAirport,
            AreEnginesOn            = _areEnginesOn,
            HotelModeActive         = _hotelModeActive,
            SecondsSinceTouchdown   = _td.Timestamp != DateTime.MinValue
                                        ? (DateTime.UtcNow - _td.Timestamp).TotalSeconds
                                        : double.MaxValue,
        };

        private ApproachContext BuildApproachContext(RawTelemetryData data) => new ApproachContext
        {
            Phase            = CurrentPhase,
            AGL              = CurrentAGL,
            AltitudeMsl      = CurrentAltitude,
            IAS              = CurrentIndicatedAirspeed,
            VS               = CurrentVerticalSpeed,
            Bank             = _currentBank,
            Pitch            = _currentPitch,
            Heading          = CurrentHeading,
            Lat              = CurrentLat,
            Lon              = CurrentLon,
            LandingLightOn   = data.LandingLightOn,
            BeaconLightOn    = _isBeaconOn,
            StrobeOn         = _isStrobeOn,
            HotelMode        = data.HotelModeActive,
            GearDown         = data.GearDown,
            IsOnGround       = data.IsOnGround,
            FlapsPosition    = data.FlapsPercent,
            QnhMb            = AircraftQnhMb,
            Nav1FreqMhz      = data.Nav1FrequencyMhz,
            ActivePirepPresent = !string.IsNullOrEmpty(ActivePirepId),
        };
    }
}
