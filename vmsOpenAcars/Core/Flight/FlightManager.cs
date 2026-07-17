using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using vmsOpenAcars.Core.Helpers;
using vmsOpenAcars.Db;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.Services.Interfaces;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Core.Flight
{
    public partial class FlightManager
    {
        private readonly IApiService _apiService;
        private readonly IWeatherService _weatherService;
        private readonly PositionValidator _positionValidator;
        private readonly FlightPhaseStateMachine _phaseMachine = new FlightPhaseStateMachine();
        private readonly ApproachValidator _approachValidator;
        private readonly TouchdownState        _td               = new TouchdownState();
        private readonly PenaltyState          _pen              = new PenaltyState();
        private readonly EngineStartMonitor    _engStartMonitor  = new EngineStartMonitor();
        private readonly ThrustReverserMonitor _reverserMonitor  = new ThrustReverserMonitor();
        private readonly FlightTimer      _timer = new FlightTimer();
        private string _currentAirport = "";
        private Pilot _activePilot;
        private SimbriefPlan _activePlan;
        private int _destinationElevation = 0;
        private double _totalDistanceKm = 0;
        private DateTime? _lastAirborneTime = null;
        private DateTime? _lastPositionTime = null;
        private (double lat, double lon)? _lastPosition = null;
        private double _currentPitch = 0;
        private double _currentBank = 0;
        private bool _isParkingBrakeSet;
        private bool _areEnginesOn;

        private double _initialFuel = 0;
        private double _totalFuelUsed = 0;
        private double _fuelAtTakeoffRoll = 0;
        private double _fuelAtTaxiInStart = 0;
        private double _groundAltitudeFeet = 0;
        public int LastFlightScore { get; private set; } = 0;
        private int _apEngagedCounter = 0;
        private const int ApEngageDebounce = 6;
        private const double SingleEngineTaxiMinRatio = 0.5;
        private string _effectiveDestination = null;
        private bool _isNavOn, _isStrobeOn, _isTaxiLightOn, _isLandingLightOn, _isBeaconOn, _isSpoilersOn;
        private bool _pendingNavOn, _pendingBeaconOn, _pendingLandingOn, _pendingTaxiOn, _pendingStrobeOn, _pendingSpoilers;
        private DateTime _navPending, _beaconPending, _landingPending, _taxiPending, _strobePending, _spoilersPending;
        private bool _hotelModeActive;
        // Aeronaves con switch único beacon/strobe: encender strobes apaga beacon (DH8D Q400)
        private static readonly HashSet<string> BeaconStrobeSharedAircraft = new HashSet<string> { "DH8D" };
        private const double LightDebounceSeconds = 2.0;
        private const double SpoilersDebounceSeconds = 1.5;
        private int _lastFlightTimeMinutesLogged = -1;
        private double _lastDistanceLogged = -1;

        #region Properties
        public double AircraftQnhMb { get; private set; }
        public string ApNavMode { get; private set; } = "HDG";
        public string ApVertMode { get; private set; } = "ALT";

        public string CurrentAirport
        {
            get => _currentAirport;
            private set { _currentAirport = value; OnAirportChanged?.Invoke(value); }
        }
        public double PlannedDistanceNm => _activePlan?.Distance ?? 0;
        public double ReferenceAirportElevation
        {
            get
            {
                if (_activePlan == null) return 0;
                switch (CurrentPhase)
                {
                    case FlightPhase.Descent:
                    case FlightPhase.Approach:
                    case FlightPhase.Landing:
                    case FlightPhase.AfterLanding:
                    case FlightPhase.TaxiIn:
                    case FlightPhase.OnBlock:
                    case FlightPhase.Arrived:
                    case FlightPhase.Completed:
                        return _activePlan.DestinationElevation;
                    default:
                        return _activePlan.OriginElevation;
                }
            }
        }
        public bool IsApproachStabilized
        {
            get
            {
                if (CurrentPhase != FlightPhase.Approach &&
                    CurrentPhase != FlightPhase.Landing) return false;

                double agl = CurrentAltitude - ReferenceAirportElevation;
                if (agl > 1000) return true;   // por encima del gate — aún no aplica

                bool speedOk = CurrentIndicatedAirspeed >= 100 &&
                               CurrentIndicatedAirspeed <= 160;
                bool vsOk = CurrentVerticalSpeed >= -1000 &&
                            CurrentVerticalSpeed <= -100;
                bool bankOk = Math.Abs(_currentBank) <= 7.0;
                bool pitchOk = _currentPitch >= -2.5 && _currentPitch <= 10.0;
                bool gearOk = IsGearDown;
                bool configOk = CurrentFlapsPosition >= 50;

                return speedOk && vsOk && bankOk && pitchOk && gearOk && configOk;
            }
        }
        public double TotalDistanceKm => _totalDistanceKm;
        public double CurrentLat { get; private set; }
        public double CurrentLon { get; private set; }
        public double CurrentHeading { get; private set; }
        public double TouchdownLat        => _td.Lat;
        public double TouchdownLon        => _td.Lon;
        public double TouchdownHeadingDeg => _td.HeadingDeg;
        public int CurrentIndicatedAirspeed { get; private set; }
        public int CurrentAltitude { get; private set; }
        public int CurrentVerticalSpeed { get; private set; }
        public int?   TouchdownFpm          => _td.Fpm;
        public double TouchdownDistanceFt   => _td.DistanceFt;
        public double TouchdownCenterlineFt => _td.CenterlineDeviationFt;
        public string TouchdownRunwayName   => _td.RunwayName;
        public double TouchdownGForce       => _td.GForce;

        public int  OverspeedCount               => _approachValidator.OverspeedCount;
        public int  OverspeedPenaltyCount        => _approachValidator.OverspeedPenaltyCount;
        public int  LightsViolationCount         => _approachValidator.LightsViolationCount;
        public int  StabilizedApproachDeductions => _approachValidator.StabilizedDeductions;
        public int  QnhViolationCount            => _approachValidator.QnhViolations;
        public bool IsOfflineFlight              => _pen.IsOfflineFlight;
        public bool DepartedLate                 => _pen.DepartedLate;
        public int  ProcedureSpdViolations       => _pen.ProcedureSpdViolations;
        public int  LocalizerViolations          => _approachValidator.LocalizerViolations;
        public bool BelowMinimums                => _approachValidator.BelowMinimums;
        public Func<bool> IsOnAtcFrequency
        {
            get => _approachValidator.IsOnAtcFrequency;
            set => _approachValidator.IsOnAtcFrequency = value;
        }
        public FlightPhase CurrentPhase => _phaseMachine.CurrentPhase;
        public int CurrentGroundSpeed { get; private set; }
        public double CurrentFuel { get; private set; }
        public bool IsOnGround { get; private set; }
        public string ActivePirepId { get; private set; } = "";
        public RawTelemetryData LastRawData { get; private set; }
        public DateTime FlightStartTime { get; private set; }
        public Pilot ActivePilot => _activePilot;
        public SimbriefPlan ActivePlan => _activePlan;
        public bool IsSimulatorConnected { get; private set; }
        public ValidationStatus PositionValidationStatus { get; private set; }
        public int CurrentTransponder { get; private set; }
        public bool AutopilotEngaged { get; private set; }
        public DateTime SimTime { get; private set; }
        public double RadarAltitude { get; private set; }
        public int PositionOrder { get; private set; }
        public double InitialFuel => _initialFuel;
        public double TotalFuelUsed => _totalFuelUsed;
        public bool IsBlockOffRecorded => _timer.BlockOffRecorded;
        public bool IsTimerStarted     => _timer.IsTimerStarted;
        public bool IsParkingBrakeSet => _isParkingBrakeSet;
        public bool AreEnginesOn => _areEnginesOn;

        public bool HasSimulatorData { get; private set; }

        public double CurrentAGL
        {
            get
            {
                if (IsOnGround) return 0;
                switch (CurrentPhase)
                {
                    case FlightPhase.Enroute:
                        return Math.Max(0, CurrentAltitude - _groundAltitudeFeet);
                    default:
                        return CurrentAltitude - ReferenceAirportElevation;
                }
            }
        }

        public bool IsGearDown { get; private set; }
        public double CurrentFlapsPosition { get; private set; }
        public string FlapsLabel { get; private set; } = "UP";
        public bool AreSpoilersDeployed { get; private set; }
        public string AutobrakeSetting { get; private set; } = "RTO";
        public bool IsNavLightOn { get; private set; }
        public bool IsBeaconLightOn { get; private set; }
        public bool IsLandingLightOn { get; private set; }
        public bool IsTaxiLightOn { get; private set; }
        public bool IsStrobeLightOn { get; private set; }
        public float N1_1 { get; private set; }
        public float N1_2 { get; private set; }
        #endregion

        #region Events

        public event Action<string> OnPhaseChanged;
        public event Action<FlightPhase> PhaseChanged;
        public event Action<string> OnAirportChanged;
        public event Action<string, Color> OnLog;
        public event Action<string, OsdSeverity> OnOsdMessage;
        public event Action<ValidationStatus> OnPositionValidated;
        public event Action<int, double, double, double> OnLandingDetected;
        public event Action OnBlockDetected;
        public event Action<int, int, int> OnTakeoffDetected;
        public event Action<double, double, double, string, bool> OnTaxiPositionUpdate;

        #endregion

        public FlightManager(IApiService apiService, IWeatherService weatherService)
        {
            _apiService = apiService;
            _weatherService = weatherService;
            _positionValidator = new PositionValidator();
            PositionValidationStatus = new ValidationStatus();
            _approachValidator = new ApproachValidator(BeaconStrobeSharedAircraft);
            _approachValidator.SetWeatherService(weatherService);
            WirePhaseMachine();
            WireApproachValidator();
        }

        private void WirePhaseMachine()
        {
            _phaseMachine.OnTransitioned += (from, to) =>
            {
                if (to != FlightPhase.Idle)
                    OnLog?.Invoke(_("Log_Phase" + to), Theme.MainText);
                PhaseChanged?.Invoke(to);
                OnPhaseChanged?.Invoke(to.ToString());
                CheckProcedureAtPhaseEntry(to);

                // AfterLanding→TaxiIn: log trip fuel, record taxi-in start fuel
                if (to == FlightPhase.TaxiIn)
                {
                    if (_fuelAtTakeoffRoll > 0)
                    {
                        double tripFuel = _fuelAtTakeoffRoll - CurrentFuel;
                        if (tripFuel > 0)
                            OnLog?.Invoke(_("Log_FuelTrip", (int)Math.Round(tripFuel)), Theme.MainText);
                    }
                    _fuelAtTaxiInStart = CurrentFuel;
                }

                // phpVMS status update — AfterLanding uses "LAN" (touchdown); others via helper
                if (to != FlightPhase.OnBlock && to != FlightPhase.Completed)
                {
                    string code = to == FlightPhase.AfterLanding
                        ? "LAN"
                        : FlightPhaseHelper.GetStatusCode(to);
                    Task.Run(() => UpdatePirepStatus(code));
                }
            };

            _phaseMachine.OnTakeoffDetected  += (gs, alt, vs) => OnTakeoffDetected?.Invoke(gs, alt, vs);
            _phaseMachine.OnTaxiPositionUpdate += (lat, lon, hdg, ap, isTaxiIn) =>
                OnTaxiPositionUpdate?.Invoke(lat, lon, hdg, ap, isTaxiIn);

            _phaseMachine.OnTouchdownDetected += vs => RegisterTouchdown(vs);

            _phaseMachine.OnBlockOffNeeded += () => Task.Run(() => UpdateBlockOffTime());

            _phaseMachine.OnBlockOnDetected += () =>
            {
                // Taxi-in fuel log
                if (_fuelAtTaxiInStart > 0)
                {
                    double taxiInFuel = _fuelAtTaxiInStart - CurrentFuel;
                    if (taxiInFuel > 0)
                        OnLog?.Invoke(_("Log_FuelTaxiIn", (int)Math.Round(taxiInFuel)), Theme.MainText);
                }
                // Single-engine TaxiIn evaluation
                if (!_pen.SingleEngineTaxiDetected && _pen.BothEnginesRunning &&
                    _pen.TaxiInMovingCycles > 0 &&
                    (double)_pen.TaxiInSingleEngineCycles / _pen.TaxiInMovingCycles >= SingleEngineTaxiMinRatio)
                {
                    _pen.SingleEngineTaxiDetected = true;
                    OnLog?.Invoke(_("Log_SingleEngineTaxiIn"), Theme.MainText);
                    OnOsdMessage?.Invoke("SINGLE ENGINE TAXI  +5 PTS", OsdSeverity.Success);
                }
                _timer.RecordBlockOn(DateTime.UtcNow);
                OnLog?.Invoke(_("Log_BlockOn", _timer.ServerBlockOnTime.ToString("HH:mm:ss")), Theme.MainText);
                OnBlockDetected?.Invoke();
            };

            _phaseMachine.OnApproachGateReset += () => _approachValidator.ResetGate();

            _phaseMachine.OnTouchAndGo += () =>
            {
                _td.ResetRunwayData();
                _approachValidator.ResetApproachData();
            };

            _phaseMachine.OnLog       += (msg, color) => OnLog?.Invoke(msg, color);
            _phaseMachine.OnOsdMessage += (msg, sev)  => OnOsdMessage?.Invoke(msg, sev);
        }

        private void WireApproachValidator()
        {
            _approachValidator.OnLog        += (msg, color) => OnLog?.Invoke(msg, color);
            _approachValidator.OnOsdMessage += (msg, sev)   => OnOsdMessage?.Invoke(msg, sev);
        }

        #region Private Methods

        /// <summary>
        /// Aplica debounce a un estado booleano: solo acepta un cambio de estado
        /// después de que haya sido estable durante el tiempo especificado.
        /// </summary>
        private static void DebounceState(bool raw, ref bool stable, ref bool pending, ref DateTime pendingSince, double debounceSeconds)
        {
            if (raw == stable) { pending = stable; return; }
            if (raw != pending) { pending = raw; pendingSince = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - pendingSince).TotalSeconds >= debounceSeconds)
                stable = pending;
        }

        /// <summary>
        /// Verifica compliance de procedimientos al entrar en una nueva fase.
        /// Penaliza y logea cada violación encontrada.
        /// Se llama una sola vez por transición de fase.
        /// </summary>
        private void CheckProcedureAtPhaseEntry(FlightPhase newPhase)
        {
            _approachValidator.CheckPhaseEntryLights(newPhase, _isNavOn, _isTaxiLightOn, _isStrobeOn, _isLandingLightOn);

            if (newPhase == FlightPhase.TakeoffRoll)
            {
                _apEngagedCounter  = 0;
                _fuelAtTakeoffRoll = CurrentFuel;

                // Engine idle-time check: warn if any engine started during taxi-out
                // without meeting the minimum warm-up period.
                double oat     = LastRawData?.OatCelsius ?? 15.0;
                bool eng1      = LastRawData?.Eng1Running ?? false;
                bool eng2      = LastRawData?.Eng2Running ?? false;
                var readiness  = _engStartMonitor.CheckPreTakeoff(oat, eng1, eng2);
                if (!readiness.Ready)
                {
                    OnLog?.Invoke($"⚠️ IDLE MOTOR INSUFICIENTE: {readiness.Reason}", Theme.Warning);
                    OnOsdMessage?.Invoke("ENGINE IDLE TIME  ⚠️", OsdSeverity.Warning);
                }
                else
                {
                    string e1 = _engStartMonitor.Eng1IdleTime > TimeSpan.Zero
                        ? $"ENG1 idle {(int)_engStartMonitor.Eng1IdleTime.TotalSeconds}s"
                        : $"ENG1 pre-arrancado";
                    string e2 = _engStartMonitor.Eng2IdleTime > TimeSpan.Zero
                        ? $"ENG2 idle {(int)_engStartMonitor.Eng2IdleTime.TotalSeconds}s"
                        : $"ENG2 pre-arrancado";
                    string stab1 = _engStartMonitor.Eng1Stabilized ? "STAB ✓" : "sin estabilizar";
                    string stab2 = _engStartMonitor.Eng2Stabilized ? "STAB ✓" : "sin estabilizar";
                    OnLog?.Invoke($"Motor: {e1} [{stab1}]  {e2} [{stab2}]  OAT {oat:F0}°C", Theme.MainText);
                }
                double taxiOutFuel = _initialFuel - CurrentFuel;
                if (taxiOutFuel > 0)
                    OnLog?.Invoke(_("Log_FuelTaxiOut", (int)Math.Round(taxiOutFuel)), Theme.MainText);
                if (!_pen.SingleEngineTaxiDetected && _pen.BothEnginesRunning &&
                    _pen.TaxiOutMovingCycles > 0 &&
                    (double)_pen.TaxiOutSingleEngineCycles / _pen.TaxiOutMovingCycles >= SingleEngineTaxiMinRatio)
                {
                    _pen.SingleEngineTaxiDetected = true;
                    OnLog?.Invoke(_("Log_SingleEngineTaxiOut"), Theme.MainText);
                    OnOsdMessage?.Invoke("SINGLE ENGINE TAXI  +5 PTS", OsdSeverity.Success);
                }
                if (!string.IsNullOrEmpty(_activePlan?.Origin))
                    _approachValidator.CheckQnhAsync(_activePlan.Origin, AircraftQnhMb).ConfigureAwait(false);
            }

            if (newPhase == FlightPhase.TaxiIn)
            {
                if (_reverserMonitor.ReversersUsed)
                    OnLog?.Invoke($"Reversa: detectada  ENG1 {_reverserMonitor.MaxEng1RevPct:F1}%  ENG2 {_reverserMonitor.MaxEng2RevPct:F1}%  — cool-down {ThrustReverserMonitor.COOLDOWN_REQUIRED_SECONDS}s requeridos", Theme.MainText);
                else
                    OnLog?.Invoke("Reversa: no detectada" + (_reverserMonitor.ReverserDataAvailable ? "" : " (offset 0x207C sin datos — addon puede no soportarlo)"), Theme.MainText);
            }
        }


        private void ValidateAirportMatch()
        {
            if (_activePilot == null || _activePlan == null) return;
            bool match = _positionValidator.CompareIcaoCodes(_activePilot.CurrentAirport, _activePlan.Origin);
            PositionValidationStatus.IcaoMatch = match;
            PositionValidationStatus.PhpVmsAirport = _activePilot.CurrentAirport;
            PositionValidationStatus.SimbriefAirport = _activePlan.Origin;
            if (match) OnLog?.Invoke($"{_("DepartureAirportOk")} {_activePlan.Origin}", Theme.MainText);
            else OnLog?.Invoke($"{_("Warning")}: {_("YouAreAssigned")} {_activePilot.CurrentAirport}, {_("ButFlightDepartureIs")} {_activePlan.Origin}", Theme.Warning);
            OnPositionValidated?.Invoke(PositionValidationStatus);
        }

        private void ValidateSimulatorPosition(double currentLat, double currentLon)
        {
            if (_activePilot == null) return;
            var (isValid, distance, message, color) = _positionValidator.ValidatePosition(
                _activePilot.CurrentAirport, _activePilot.CurrentAirportLat, _activePilot.CurrentAirportLon, currentLat, currentLon);
            bool changed = (PositionValidationStatus.GpsValid != isValid) || (Math.Abs(PositionValidationStatus.DistanceFromAirport - distance) > 0.01);
            PositionValidationStatus.GpsValid = isValid;
            PositionValidationStatus.DistanceFromAirport = distance;
            if (changed) OnLog?.Invoke(message, color);
            OnPositionValidated?.Invoke(PositionValidationStatus);
        }

        #endregion

        #region Public Methods

        public double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
            => UnitConverter.CalculateDistanceKm(lat1, lon1, lat2, lon2);

        /// <summary>Returns a snapshot of engine lifecycle state for UI display.</summary>
        public EngineLifecycleSnapshot GetEngineLifecycleSnapshot()
        {
            double oat = LastRawData?.OatCelsius ?? 20.0;
            return new EngineLifecycleSnapshot
            {
                Eng1Running             = LastRawData?.Eng1Running ?? false,
                Eng2Running             = LastRawData?.Eng2Running ?? false,
                Eng1Stabilized          = _engStartMonitor.Eng1Stabilized,
                Eng2Stabilized          = _engStartMonitor.Eng2Stabilized,
                Eng1IdleTime            = _engStartMonitor.Eng1IdleTime,
                Eng2IdleTime            = _engStartMonitor.Eng2IdleTime,
                OilTemp1                = LastRawData?.OilTemp_1 ?? 0f,
                OilTemp2                = LastRawData?.OilTemp_2 ?? 0f,
                OilPress1               = LastRawData?.OilPress_1 ?? 0f,
                OilPress2               = LastRawData?.OilPress_2 ?? 0f,
                OatCelsius              = oat,
                RequiredIdleSeconds     = oat < EngineStartMonitor.COLD_OAT_THRESHOLD
                                            ? EngineStartMonitor.COLD_IDLE_SECONDS
                                            : EngineStartMonitor.WARM_IDLE_SECONDS,
                ReversersUsed           = _reverserMonitor.ReversersUsed,
                MaxEng1RevPct           = _reverserMonitor.MaxEng1RevPct,
                MaxEng2RevPct           = _reverserMonitor.MaxEng2RevPct,
                CooldownSecondsElapsed  = _reverserMonitor.SecondsSinceTouchdown,
                CooldownSecondsRequired = _reverserMonitor.ReversersUsed
                                            ? ThrustReverserMonitor.COOLDOWN_REQUIRED_SECONDS : 0,
                ReverserDataAvailable   = _reverserMonitor.ReverserDataAvailable,
            };
        }



        public string   CurrentTimerDisplay => _timer.CurrentTimerDisplay;
        public TimeSpan CurrentFlightTime   => _timer.CurrentFlightTime;

        public void SetActivePilot(Pilot pilot)
        {
            _activePilot = pilot;
            CurrentAirport = pilot?.CurrentAirport ?? "";
            if (_activePlan != null) ValidateAirportMatch();
        }

        public void SetActivePlan(SimbriefPlan plan)
        {
            _activePlan = plan;
            if (plan != null)
            {
                _destinationElevation = plan.DestinationElevation;
                OnLog?.Invoke(_("Log_PlanDestination", plan.Destination, _destinationElevation), Theme.MainText);
            }
            ValidateAirportMatch();
            if (CurrentLat != 0 && CurrentLon != 0) UpdatePositionValidation(CurrentLat, CurrentLon);
        }

        public void SetSimulatorConnected(bool connected, double? latitude = null, double? longitude = null)
        {
            IsSimulatorConnected = connected;
            if (connected && latitude.HasValue && longitude.HasValue && _activePilot != null)
            {
                if (_activePilot.CurrentAirportLat.HasValue && _activePilot.CurrentAirportLon.HasValue)
                    ValidateSimulatorPosition(latitude.Value, longitude.Value);
                else
                {
                    PositionValidationStatus.GpsValid = false;
                    OnPositionValidated?.Invoke(PositionValidationStatus);
                }
            }
            else
            {
                PositionValidationStatus.GpsValid = false;
                OnPositionValidated?.Invoke(PositionValidationStatus);
            }
        }

        public void UpdatePositionValidation(double lat, double lon)
        {
            if (_activePilot != null && IsSimulatorConnected) ValidateSimulatorPosition(lat, lon);
        }

        public async Task<string> DetectNearestAirport(double latitude, double longitude)
        {
            if (_apiService != null)
            {
                var airport = await _apiService.GetNearestAirport(latitude, longitude);
                if (!string.IsNullOrEmpty(airport))
                {
                    CurrentAirport = airport;
                    return airport;
                }
            }
            return CurrentAirport ?? "SKBO";
        }

        public bool IsPilotAtDepartureAirport(string requiredAirport) => CurrentAirport?.Equals(requiredAirport, StringComparison.OrdinalIgnoreCase) ?? false;

        public void MarkOfflineFlight() => _pen.IsOfflineFlight = true;

        public void SetRunwayTouchdownData(double thresholdDistFt, double centerlineDeviationFt, string runwayName)
            => _td.SetRunwayData(thresholdDistFt, centerlineDeviationFt, runwayName);

        public void SetProcedureSpdViolations(int count)
            => _pen.ProcedureSpdViolations = count;

        public void SetApproachData(IlsData ils, ApproachInfo approach, IList<ApproachFix> fixes)
            => _approachValidator.SetApproachData(ils, approach, fixes);

        public void SetOriginTransitionAlt(double ft)
        {
            if (ft > 0) _approachValidator.OriginTransitionAltFt = ft;
        }
        public void SetDestTransitionLevel(double ft)
        {
            if (ft > 0) _approachValidator.DestTransitionLevelFt = ft;
        }
        public void SetEffectiveDestination(string icao)
        {
            _effectiveDestination = icao;
            _approachValidator.EffectiveDestination = icao;
        }

        public bool CanStartFlight()
        {
            if (_activePilot == null) return false;
            if (_activePlan == null) return false;
            if (!PositionValidationStatus.IcaoMatch) return false;
            return true;
        }

        public void UpdateTelemetry(RawTelemetryData data)
        {
            if (data == null) return;
            ApplyRawData(data);
            CheckEngineEvents(data);
            UpdateSingleEngineTaxi(data);
            UpdateFlightTracking(data);
        }

        public async Task UpdateFlightProgress()
        {
            if (string.IsNullOrEmpty(ActivePirepId) || !_timer.IsTimerStarted) return;
            DateTime reference = _timer.ServerBlockOffTime != default ? _timer.ServerBlockOffTime : _timer.ServerCreatedAt;
            int flightTimeMinutes = (int)(DateTime.UtcNow - reference).TotalMinutes;
            double currentDistance = _totalDistanceKm * 0.539957;

            bool timeChanged = Math.Abs(flightTimeMinutes - _lastFlightTimeMinutesLogged) >= 1;
            bool distanceChanged = Math.Abs(currentDistance - _lastDistanceLogged) >= 1;
            if (!timeChanged && !distanceChanged) return;

            try
            {
                bool success = await _apiService.UpdatePirep(ActivePirepId, new { flight_time = flightTimeMinutes, distance = currentDistance });
                if (success)
                {
                    _lastFlightTimeMinutesLogged = flightTimeMinutes;
                    _lastDistanceLogged = currentDistance;
                }
            }
            catch (Exception ex) { OnLog?.Invoke(_("Log_ErrorFlightProgress", ex.Message), Theme.Danger); }
        }

        #endregion
    }

    public class ValidationStatus
    {
        public bool IcaoMatch { get; set; }
        public bool GpsValid { get; set; }
        public double DistanceFromAirport { get; set; }
        public string PhpVmsAirport { get; set; }
        public string SimbriefAirport { get; set; }
        public bool CanStart => IcaoMatch;
        public override string ToString() => $"ICAO: {(IcaoMatch ? "✅" : "❌")} GPS: {(GpsValid ? "✅" : "⏳")} Dist: {DistanceFromAirport:F1}km";
    }
}
