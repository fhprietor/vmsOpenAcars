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
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Core.Flight
{
    public class FlightManager
    {
        private readonly ApiService _apiService;
        private readonly WeatherService _weatherService;
        private readonly PositionValidator _positionValidator;
        private bool _wasOnGround = true;
        private bool _touchdownCaptured = false;
        private DateTime _touchdownTimestamp = DateTime.MinValue;
        private string _currentAirport = "";
        private Pilot _activePilot;
        private SimbriefPlan _activePlan;
        private double _maxAltitudeReached = 0;
        private int _destinationElevation = 0;
        private double _totalDistanceKm = 0;
        private DateTime? _lastAirborneTime = null;
        private DateTime? _lastPositionTime = null;
        private (double lat, double lon)? _lastPosition = null;
        private double _currentPitch = 0;
        private double _currentBank = 0;
        private DateTime _lastDescentWarning = DateTime.MinValue;
        private const int DescentWarningIntervalSeconds = 30;
        private bool _hasLandedThisFlight = false;
        private DateTime _climbStableStart     = DateTime.MinValue;
        private DateTime _descentStart         = DateTime.MinValue;
        private DateTime _stepClimbStart       = DateTime.MinValue;
        private DateTime _descentToClimbStart  = DateTime.MinValue;
        private bool _isParkingBrakeSet;
        private bool _areEnginesOn;

        // Timer del vuelo (basado en server)
        private DateTime _serverCreatedAt;
        private DateTime _serverBlockOffTime;
        private DateTime _serverBlockOnTime;
        private bool _isTimerStarted = false;

        // Combustible
        private bool _blockOffRecorded = false;
        private double _initialFuel = 0;
        private double _lastFuelUpdate = 0;
        private double _totalFuelUsed = 0;
        private double _fuelAtTakeoffRoll = 0;
        private double _fuelAtTaxiInStart = 0;

        // ── Scoring ──────────────────────────────────────────────────────────────────
        private double _touchdownPitch = 0;
        private double _touchdownBank = 0;
        private double _touchdownGForce = 0;
        private double _touchdownLatSaved = 0;
        private double _touchdownLonSaved = 0;
        private double _touchdownHeadingDeg = 0;
        private double _touchdownDistanceFt = 0;
        private double _touchdownCenterlineDeviationFt = 0;
        private string _touchdownRunwayName = null;
        private double _groundAltitudeFeet = 0;
        public int LastFlightScore { get; private set; } = 0;
        private int _overspeedCount = 0;
        private int _overspeedPenaltyCount = 0;
        private bool _wasOverspeed = false;
        private int _lightsViolationCount = 0;
        private bool _lightsViolationActive = false;
        private bool _beaconViolationActive = false;
        private bool _landingLightReminderSent = false;
        private bool _passing10kFtSent = false;
        private int _qnhViolationCount = 0;
        private bool _isOfflineFlight = false;
        private bool _departedLate = false;
        private int  _procedureSpdViolations = 0;
        private int _vmoKts = 320;
        private int _apEngagedCounter = 0;
        private const int ApEngageDebounce = 6; // 6 × 50 ms = 300 ms
        private bool _singleEngineTaxiDetected = false;
        private bool _bothEnginesRunning = false;
        private int  _taxiOutMovingCycles = 0;
        private int  _taxiOutSingleEngineCycles = 0;
        private int  _taxiInMovingCycles = 0;
        private int  _taxiInSingleEngineCycles = 0;
        private const double SingleEngineTaxiMinRatio = 0.5; // ≥ 50 % del tiempo en movimiento

        // ── Transition Altitude / Level ───────────────────────────────────────────
        private double _originTransitionAltFt  = 0;
        private double _destTransitionLevelFt  = 0;
        private string _effectiveDestination   = null; // overrides _activePlan.Destination when landing at alternate
        private bool   _taOsdSent              = false;
        private bool   _tlOsdSent              = false;
        private bool   _destQnhChecked         = false;
        private bool   _originQnhChecked       = false;

        // ── ILS / Approach tracking ──────────────────────────────────────────────
        private IlsData _expectedIls;
        private ApproachInfo _expectedApproach;
        private IList<ApproachFix> _approachFixes;
        private int _nextFixIndex;
        private int _localizerViolations;
        private bool _belowMinimums;
        private double _daAltitudeFt;
        private bool _ilsGateChecked;
        private bool _ilsTunedCorrectly = true;

        // ── Approximation gate (1000 ft AGL) ─────────────────────────────────────
        private bool _approachGateEvaluated = false;
        private double _prevApproachAgl = double.MaxValue;
        private int _stabilizedApproachDeductions = 0;
        private bool _isNavOn;
        private bool _isStrobeOn;
        private bool _isTaxiLightOn;
        private bool _isLandingLightOn;
        private bool _isBeaconOn;

        // Aeronaves con switch único beacon/strobe: encender strobes apaga beacon
        private static readonly HashSet<string> BeaconStrobeSharedAircraft = new HashSet<string>
        {
            "DH8D"  // Dash 8-400 / Q400
        };

        // Debounce de luces: evita falsos OFF durante cambios de fuente de energía
        private const double LightDebounceSeconds = 2.0;
        private const double SpoilersDebounceSeconds = 1.5;
        private bool _hotelModeActive = false;
        private bool _pendingNavOn, _pendingBeaconOn, _pendingLandingOn, _pendingTaxiOn, _pendingStrobeOn, _pendingSpoilers;
        private DateTime _navPending = DateTime.MinValue;
        private DateTime _beaconPending = DateTime.MinValue;
        private DateTime _landingPending = DateTime.MinValue;
        private DateTime _taxiPending = DateTime.MinValue;
        private DateTime _strobePending = DateTime.MinValue;
        private DateTime _spoilersPending = DateTime.MinValue;
        private bool _isSpoilersOn;

        // Control de fases
        private DateTime _phaseStartTime = DateTime.UtcNow;
        private DateTime _lastTaxiPositionEvent = DateTime.MinValue;
        private FlightPhase _lastStablePhase;
        private DateTime _pushbackStartTime = DateTime.MinValue;
        private DateTime _stoppedStartTime = DateTime.MinValue;
        private DateTime _goAroundStart = DateTime.MinValue;

        // Constantes para transiciones
        private const double PUSHBACK_MAX_SPEED = 6.0;
        private const int PUSHBACK_MIN_DURATION = 8;
        private const double TAXIOUT_MIN_SPEED = 5.0;
        private const int TAXIOUT_MIN_DURATION = 2;

        // Optimización
        private int _lastFlightTimeMinutesLogged = -1;
        private double _lastDistanceLogged = -1;

        #region Properties
        // Seat Belt Sign
        /// <summary>QNH seleccionado en el altímetro del avión en hPa. Actualizado cada ciclo.</summary>
        public double AircraftQnhMb { get; private set; }

        // Autopilot modo
        public string ApNavMode { get; private set; } = "HDG";
        public string ApVertMode { get; private set; } = "ALT";

        public string CurrentAirport
        {
            get => _currentAirport;
            private set { _currentAirport = value; OnAirportChanged?.Invoke(value); }
        }
        /// <summary>Distancia planificada en NM. Viene del plan SimBrief o del Flight phpVMS.</summary>
        public double PlannedDistanceNm =>
            _activePlan?.Distance > 0
                ? _activePlan.Distance
                : (_activePlan?.Distance ?? 0);
        /// <summary>
        /// Elevación del aeropuerto de referencia para la fase actual (ft MSL).
        /// Origen en fases de salida, destino en fases de llegada.
        /// </summary>
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
        /// <summary>
        /// Determina si la aproximación está estabilizada según criterios VASI/PANS-OPS:
        /// válido solo por debajo de 1000 ft AGL en fase Approach.
        /// </summary>
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
        public double TouchdownLat => _touchdownLatSaved;
        public double TouchdownLon => _touchdownLonSaved;
        public double TouchdownHeadingDeg => _touchdownHeadingDeg;
        public int CurrentIndicatedAirspeed { get; private set; }
        public int CurrentAltitude { get; private set; }
        public int CurrentVerticalSpeed { get; private set; }
        public int? TouchdownFpm { get; private set; }
        public double TouchdownDistanceFt   => _touchdownDistanceFt;
        public double TouchdownCenterlineFt => _touchdownCenterlineDeviationFt;
        public string TouchdownRunwayName   => _touchdownRunwayName;
        public double TouchdownGForce       => _touchdownGForce;

        // ── Penalty state (read by MainViewModel for checkpoint encoding/decoding) ──
        public int  OverspeedCount               => _overspeedCount;
        public int  OverspeedPenaltyCount        => _overspeedPenaltyCount;
        public int  LightsViolationCount         => _lightsViolationCount;
        public int  StabilizedApproachDeductions => _stabilizedApproachDeductions;
        public int  QnhViolationCount            => _qnhViolationCount;
        public bool IsOfflineFlight              => _isOfflineFlight;
        public bool DepartedLate                 => _departedLate;
        public int  ProcedureSpdViolations       => _procedureSpdViolations;
        public int  LocalizerViolations          => _localizerViolations;
        public bool BelowMinimums                => _belowMinimums;
        /// <summary>
        /// Set by MainViewModel. Returns true when COM1 is tuned to an active IVAO ATC station.
        /// When true, overspeed and Vapp violations are warned but not penalized (ATC-directed).
        /// If null, penalties apply normally.
        /// </summary>
        public Func<bool> IsOnAtcFrequency { get; set; }
        public FlightPhase CurrentPhase { get; private set; }
        public int CurrentGroundSpeed { get; private set; }
        public double CurrentFuel { get; private set; }
        public bool IsOnGround { get; private set; }
        public string ActivePirepId { get; private set; } = "";
        /// <summary>
        /// Último RawTelemetryData recibido desde FsuipcService.
        /// Disponible para que la UI pueda acceder a datos de motores
        /// sin necesidad de suscribirse al evento.
        /// </summary>
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
        public bool IsBlockOffRecorded => _blockOffRecorded;
        public bool IsTimerStarted => _isTimerStarted;
        public bool IsParkingBrakeSet => _isParkingBrakeSet;
        public bool AreEnginesOn => _areEnginesOn;

        public bool HasSimulatorData { get; private set; }

        /// <summary>
        /// Altura sobre el nivel del suelo en pies, calculada según la fase de vuelo:
        /// - Despegue / Climb / Tierra  → MSL − elevación aeropuerto de salida
        /// - Crucero (Enroute)          → MSL − elevación de terreno bajo el avión (FSUIPC 0x0020)
        /// - Descenso / Aproximación    → MSL − elevación aeropuerto de destino
        /// El radar altímetro no se usa en aproximación porque en terreno montañoso
        /// (Andes, Alpes, Himalaya) lee la orografía, no la elevación del aeropuerto.
        /// </summary>
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

        // Sistemas y luces
        public bool IsGearDown { get; private set; }
        public double CurrentFlapsPosition { get; private set; }
        public string FlapsLabel { get; private set; } = "UP";
        public bool AreSpoilersDeployed { get; private set; }
        public string AutobrakeSetting { get; private set; } = "RTO";

        // Luces
        public bool IsNavLightOn { get; private set; }
        public bool IsBeaconLightOn { get; private set; }
        public bool IsLandingLightOn { get; private set; }
        public bool IsTaxiLightOn { get; private set; }
        public bool IsStrobeLightOn { get; private set; }

        // Parámetros de motor (Jet)
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
        // lat, lon, heading, airport, isTaxiIn
        public event Action<double, double, double, string, bool> OnTaxiPositionUpdate;

        #endregion

        public FlightManager(ApiService apiService, WeatherService weatherService)
        {
            _apiService = apiService;
            _weatherService = weatherService;
            _positionValidator = new PositionValidator();
            CurrentPhase = FlightPhase.Idle;
            PositionValidationStatus = new ValidationStatus();
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

        private void TransitionTo(FlightPhase newPhase, FlightPhase from)
        {
            // Reset the approach gate so a new evaluation can happen on re-approach after go-around
            if (from == FlightPhase.Approach && newPhase == FlightPhase.Climb)
            {
                _approachGateEvaluated = false;
                _prevApproachAgl = double.MaxValue;
            }
            CurrentPhase = newPhase;
            _phaseStartTime = DateTime.UtcNow;
            if (newPhase != FlightPhase.Idle)
                OnLog?.Invoke(_("Log_Phase" + newPhase), Theme.MainText);
            PhaseChanged?.Invoke(newPhase);
        }

        /// <summary>
        /// Verifica compliance de procedimientos al entrar en una nueva fase.
        /// Penaliza y logea cada violación encontrada.
        /// Se llama una sola vez por transición de fase.
        /// </summary>
        private void CheckProcedureAtPhaseEntry(FlightPhase newPhase)
        {
            switch (newPhase)
            {
                // ── Pushback iniciado: Nav obligatoria ─────────────────────────────
                case FlightPhase.Pushback:
                    if (!_isNavOn)
                    {
                        _lightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyNav"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  NAV LIGHTS  −5 PTS", OsdSeverity.Warning);
                    }
                    break;

                // ── Taxi out: Nav + Taxi lights obligatorias ───────────────────────
                case FlightPhase.TaxiOut:
                    if (!_isNavOn)
                    {
                        _lightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyNavTaxi"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  NAV LIGHTS  −5 PTS", OsdSeverity.Warning);
                    }
                    if (!_isTaxiLightOn)
                    {
                        _lightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyTaxi"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  TAXI LIGHTS  −5 PTS", OsdSeverity.Warning);
                    }
                    break;

                // ── Takeoff roll: Strobe + Landing lights obligatorias ─────────
                case FlightPhase.TakeoffRoll:
                    _apEngagedCounter = 0; // discard any pre-accumulated ground AP signal
                    if (!_isStrobeOn)
                    {
                        _lightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyStrobe"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  STROBE  −5 PTS", OsdSeverity.Warning);
                    }
                    if (!_isLandingLightOn)
                    {
                        _lightsViolationCount++;
                        OnLog?.Invoke(_("Log_PenaltyLanding"), Theme.Warning);
                        OnOsdMessage?.Invoke("PENALTY  LANDING LT  −5 PTS", OsdSeverity.Warning);
                    }
                    // Combustible consumido en taxi-out
                    _fuelAtTakeoffRoll = CurrentFuel;
                    double taxiOutFuel = _initialFuel - CurrentFuel;
                    if (taxiOutFuel > 0)
                        OnLog?.Invoke(_("Log_FuelTaxiOut", (int)Math.Round(taxiOutFuel)), Theme.MainText);
                    // Single-engine TaxiOut evaluation
                    if (!_singleEngineTaxiDetected && _bothEnginesRunning &&
                        _taxiOutMovingCycles > 0 &&
                        (double)_taxiOutSingleEngineCycles / _taxiOutMovingCycles >= SingleEngineTaxiMinRatio)
                    {
                        _singleEngineTaxiDetected = true;
                        OnLog?.Invoke(_("Log_SingleEngineTaxiOut"), Theme.MainText);
                        OnOsdMessage?.Invoke("SINGLE ENGINE TAXI  +5 PTS", OsdSeverity.Success);
                    }
                    // Verificar QNH antes de despegar
                    if (!string.IsNullOrEmpty(_activePlan?.Origin))
                        CheckQnhAsync(_activePlan.Origin, AircraftQnhMb).ConfigureAwait(false);
                    break;

            }
        }

        /// <summary>
        /// Compara el QNH del altímetro del avión con el METAR del aeropuerto indicado.
        /// Penaliza 5 pts si la diferencia supera 2 hPa. Logea el resultado en cualquier caso.
        /// </summary>
        /// <param name="icao">Código ICAO del aeropuerto a consultar (origen o destino).</param>
        /// <param name="aircraftQnhMb">QNH actual del altímetro en hPa.</param>
        private async Task CheckQnhAsync(string icao, double aircraftQnhMb)
        {
            if (string.IsNullOrWhiteSpace(icao) || aircraftQnhMb <= 0) return;

            double? stationQnh = await _weatherService.GetQnhMbAsync(icao);

            if (stationQnh == null)
            {
                OnLog?.Invoke(_("Log_QnhUnavailable", icao), Theme.Warning);
                return;
            }

            double diff = Math.Abs(aircraftQnhMb - stationQnh.Value);
            string label = $"QNH | Avión: {aircraftQnhMb:F0} hPa  {icao}: {stationQnh.Value:F0} hPa  Δ{diff:F0} hPa";

            if (diff <= 2.0)
            {
                OnLog?.Invoke(_("Log_QnhOk", label), Theme.Success);
            }
            else
            {
                _qnhViolationCount++;
                OnLog?.Invoke(_("Log_QnhPenalty", label), Theme.Warning);
                OnOsdMessage?.Invoke("PENALTY  QNH  −5 PTS", OsdSeverity.Warning);
            }
        }

        private void CheckStdPressure()
        {
            const double stdMb = 1013.25;
            double diff = Math.Abs(AircraftQnhMb - stdMb);
            string label = $"STD | Avión: {AircraftQnhMb:F0} hPa  STD: 1013 hPa  Δ{diff:F0} hPa";
            if (diff <= 2.0)
                OnLog?.Invoke(_("Log_QnhOk", label), Theme.Success);
            else
            {
                _qnhViolationCount++;
                OnLog?.Invoke(_("Log_QnhPenalty", label), Theme.Warning);
                OnOsdMessage?.Invoke("PENALTY  QNH  −5 PTS", OsdSeverity.Warning);
            }
        }

        /// <summary>
        /// Convierte el código de status de phpVMS al FlightPhase interno más apropiado
        /// para retomar un vuelo interrumpido.
        /// </summary>
        private static FlightPhase PhaseFromPirepStatus(string status)
        {
            switch (status?.ToUpperInvariant())
            {
                case "INI":
                case "BST": return FlightPhase.Boarding;
                case "PBK": return FlightPhase.Pushback;
                case "TXI": return FlightPhase.TaxiOut;
                case "TKF": return FlightPhase.Takeoff;
                case "CLB": return FlightPhase.Climb;
                case "ENR":
                case "CRZ": return FlightPhase.Enroute;
                case "DSC": return FlightPhase.Descent;
                case "APR":
                case "FIN": return FlightPhase.Approach;
                case "LND": return FlightPhase.Landing;
                case "ONB": return FlightPhase.AfterLanding;
                case "ARR": return FlightPhase.TaxiIn;
                default: return FlightPhase.Enroute; // fallback seguro
            }
        }
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

        private async Task UpdatePirepStatus(string statusCode)
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return;
            try
            {
                bool success = await _apiService.UpdatePirep(ActivePirepId, new { status = statusCode });
                if (success) OnLog?.Invoke(_("Log_PirepStatus", statusCode), Theme.MainText);
            }
            catch (Exception ex) { OnLog?.Invoke(_("Log_ErrorPirepStatus", ex.Message), Theme.Danger); }
        }

        private async Task UpdateBlockOffTime()
        {
            if (string.IsNullOrEmpty(ActivePirepId) || _blockOffRecorded) return;
            try
            {
                var payload = new { block_off_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _apiService.HttpClient.PutAsync($"{_apiService.BaseUrl}api/pireps/{ActivePirepId}", content);
                if (response.IsSuccessStatusCode)
                {
                    _blockOffRecorded = true;
                    _serverBlockOffTime = DateTime.UtcNow;
                    OnLog?.Invoke(_("Log_BlockOff", _serverBlockOffTime.ToString("HH:mm:ss")), Theme.MainText);

                    if (_activePlan?.ScheduledOutTime > 0)
                    {
                        var std = DateTimeOffset.FromUnixTimeSeconds(_activePlan.ScheduledOutTime).UtcDateTime;
                        double deltaMins = (_serverBlockOffTime - std).TotalMinutes;
                        if (Math.Abs(deltaMins) > 10)
                        {
                            _departedLate = true;
                            string deptKey = deltaMins > 0 ? "Log_DepartedTooLate" : "Log_DepartedTooEarly";
                            OnLog?.Invoke(_( deptKey, (int)Math.Abs(deltaMins), std.ToString("HH:mm")), Theme.Warning);
                        }
                        else
                        {
                            OnLog?.Invoke(_("Log_DepartedOnTime", $"{deltaMins:+0;-0}", std.ToString("HH:mm")), Theme.Success);
                        }
                    }
                }
                else OnLog?.Invoke(_("Log_BlockOffError"), Theme.Warning);
            }
            catch (Exception ex) { OnLog?.Invoke(_("Log_BlockOffSaveError", ex.Message), Theme.Danger); }
        }

        private async Task UpdateBlockOnTime()
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return;
            try
            {
                var payload = new { block_on_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _apiService.HttpClient.PutAsync($"{_apiService.BaseUrl}api/pireps/{ActivePirepId}", content);
                if (response.IsSuccessStatusCode)
                {
                    _serverBlockOnTime = DateTime.UtcNow;
                    OnLog?.Invoke(_("Log_BlockOn", _serverBlockOnTime.ToString("HH:mm:ss")), Theme.MainText);
                }
                else OnLog?.Invoke(_("Log_BlockOnError"), Theme.Warning);
            }
            catch (Exception ex) { OnLog?.Invoke(_("Log_BlockOnSaveError", ex.Message), Theme.Danger); }
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

        private void RegisterTouchdown(int verticalSpeed)
        {
            if (_touchdownCaptured) return;
            TouchdownFpm = verticalSpeed;
            _touchdownCaptured = true;
            _touchdownTimestamp = DateTime.UtcNow;
            _hasLandedThisFlight = true;
            _belowMinimums = false;
            double gforce = CalculateGForce(verticalSpeed);
            _touchdownPitch = _currentPitch;
            _touchdownBank = _currentBank;
            _touchdownGForce = gforce;
            _touchdownLatSaved = CurrentLat;
            _touchdownLonSaved = CurrentLon;
            _touchdownHeadingDeg = CurrentHeading;
            OnLandingDetected?.Invoke(verticalSpeed, gforce, _currentPitch, _currentBank);
        }
        /// <summary>
        /// Monitors overspeed and lights compliance violations while a PIREP is active.
        /// Call once per telemetry cycle, airborne only.
        /// </summary>
        private void CheckViolations(int ias, int altitudeAgl, int altitudeMsl, bool landingLightOn, bool beaconLightOn)
        {
            bool isNowOverspeed = ias > _vmoKts;
            if (isNowOverspeed && !_wasOverspeed)
            {
                _overspeedCount++;
                if (IsOnAtcFrequency?.Invoke() != true) _overspeedPenaltyCount++;
                OnLog?.Invoke(_("Log_Overspeed", ias, _vmoKts), Theme.Warning);
                OnOsdMessage?.Invoke($"OVERSPEED  {ias} KTS", OsdSeverity.Critical);
            }
            _wasOverspeed = isNowOverspeed;

            // ── Landing lights por debajo de 10 000 ft AGL ────────────────────────────
            bool lightsRequired = altitudeAgl < 9_500;
            bool lightsViolating = lightsRequired && !landingLightOn;

            if (lightsViolating && !_lightsViolationActive)
            {
                _lightsViolationActive = true;
                _lightsViolationCount++;
                OnLog?.Invoke(_("Log_LightsBelow10k", altitudeAgl), Theme.Warning);
                OnOsdMessage?.Invoke("LANDING LT OFF  −5 PTS", OsdSeverity.Warning);
            }
            else if (!lightsViolating)
            {
                _lightsViolationActive = false;
            }

            // ── Reminder OSD a 10 500 ft AGL en Descent con landing lights apagadas ──
            if (CurrentPhase == FlightPhase.Descent && altitudeAgl <= 10_500 && !landingLightOn && !_landingLightReminderSent)
            {
                _landingLightReminderSent = true;
                OnOsdMessage?.Invoke("LANDING LT OFF", OsdSeverity.Warning);
            }
            else if (altitudeAgl > 10_500)
            {
                _landingLightReminderSent = false;
            }

            // ── 10 000 ft callout (ascenso) ──────────────────────────────────────────
            if (CurrentPhase == FlightPhase.Climb && altitudeAgl >= 10_000 && !_passing10kFtSent)
            {
                _passing10kFtSent = true;
                OnOsdMessage?.Invoke("10 000 FT", OsdSeverity.Info);
            }

            // ── Beacon light (siempre debe estar encendida en vuelo) ────────────────────────────
            // Excepción 1: aeronaves con switch compartido beacon/strobe (ej: Q400)
            // Excepción 2: hotel mode (turbina en marcha, hélice bloqueada — beacon no requerido)
            bool beaconExempt = (BeaconStrobeSharedAircraft.Contains(_activePlan?.AircraftIcao ?? "") && _isStrobeOn)
                             || _hotelModeActive;
            if (!beaconLightOn && !_beaconViolationActive && !beaconExempt)
            {
                _beaconViolationActive = true;
                _lightsViolationCount++;
            }
            else if (beaconLightOn || beaconExempt)
            {
                _beaconViolationActive = false;
            }

            // ── Transition Altitude (origen → en climb, ajustar a STD 1013) ────────
            if (_originTransitionAltFt > 0
                && CurrentPhase == FlightPhase.Climb
                && altitudeMsl >= (int)_originTransitionAltFt
                && !_taOsdSent)
            {
                _taOsdSent = true;
                OnLog?.Invoke(_("Log_TransitionAlt", (int)_originTransitionAltFt), Theme.MainText);
                OnOsdMessage?.Invoke("TRANS ALT  SET STD 1013", OsdSeverity.Warning);
            }

            // ── STD pressure check (1000 ft sobre TA — penaliza si no es 1013) ───
            if (_originTransitionAltFt > 0
                && CurrentPhase == FlightPhase.Climb
                && altitudeMsl >= (int)(_originTransitionAltFt + 1000)
                && !_originQnhChecked)
            {
                _originQnhChecked = true;
                CheckStdPressure();
            }

            // ── Transition Level (destino → en descenso, ajustar a QNH) ──────────
            if (_destTransitionLevelFt > 0
                && (CurrentPhase == FlightPhase.Descent || CurrentPhase == FlightPhase.Approach)
                && altitudeMsl <= (int)_destTransitionLevelFt
                && !_tlOsdSent)
            {
                _tlOsdSent = true;
                int fl = (int)Math.Round(_destTransitionLevelFt / 100.0);
                OnLog?.Invoke(_("Log_TransitionLevel", $"{fl:D3}"), Theme.MainText);
                OnOsdMessage?.Invoke("TRANS LEVEL  SET QNH", OsdSeverity.Warning);
            }

            // ── QNH destino basado en TL (500 ft bajo el TL, dar tiempo al piloto) ──
            if (_destTransitionLevelFt > 0
                && (CurrentPhase == FlightPhase.Descent || CurrentPhase == FlightPhase.Approach)
                && altitudeMsl <= (int)(_destTransitionLevelFt - 1000)
                && !_destQnhChecked)
            {
                string qnhIcao = _effectiveDestination ?? _activePlan?.Destination;
                if (!string.IsNullOrEmpty(qnhIcao))
                {
                    _destQnhChecked = true;
                    CheckQnhAsync(qnhIcao, AircraftQnhMb).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Evaluates 6 stabilized approach criteria when the aircraft crosses
        /// the 1000 ft AGL gate in Approach phase. Fires once per approach segment
        /// (reset on go-around via TransitionTo).
        /// Throttle position is intentionally excluded: offset 0x2028 reports the
        /// physical lever position, which on FBW/autothrottle aircraft (A320, B737NG…)
        /// can read 0 % even when the FADEC is delivering approach thrust.
        /// Unstabilized idle-power dives are already caught by the VS and speed checks.
        /// </summary>
        private void CheckStabilizedApproachGate(RawTelemetryData data)
        {
            if (_approachGateEvaluated) return;
            if (string.IsNullOrEmpty(ActivePirepId)) return;

            double agl = CurrentAGL;

            // Detect the downward crossing of 1000 ft
            if (_prevApproachAgl > 1000 && agl <= 1000)
            {
                _approachGateEvaluated = true;

                var (vappMin, vappMax) = AircraftPerformanceTable.GetApproachSpeedRange(_activePlan?.AircraftIcao);
                int deductions = 0;

                // 1. ILS tuning check — must run FIRST so subsequent criteria know the approach type.
                // If NAV1 doesn't match the expected ILS, the pilot is flying RNP/visual:
                // null _expectedIls so that bank-angle and Localizer/DA checks are skipped.
                if (_expectedIls != null && !_ilsGateChecked)
                {
                    _ilsGateChecked = true;
                    double freqDelta = Math.Abs(data.Nav1FrequencyMhz - _expectedIls.FrequencyMhz);
                    if (freqDelta > 0.05)
                    {
                        OnLog?.Invoke(_("Log_IlsApproachSkipped", $"{data.Nav1FrequencyMhz:F2}", $"{_expectedIls.FrequencyMhz:F2}"), Theme.MainText);
                        _expectedIls  = null;
                        _daAltitudeFt = 0;
                    }
                    else
                    {
                        OnLog?.Invoke(_("Log_IlsTunedOk", $"{data.Nav1FrequencyMhz:F2}", $"{_expectedIls.Course:F0}"), Theme.Success);
                    }
                }

                // 2. Speed
                if (CurrentIndicatedAirspeed < vappMin || CurrentIndicatedAirspeed > vappMax)
                {
                    if (IsOnAtcFrequency?.Invoke() != true) deductions += 5;
                    OnLog?.Invoke(_("Log_ApproachGateSpeed", CurrentIndicatedAirspeed, vappMin, vappMax), Theme.Warning);
                }

                // 3. Descent rate: must be between -1000 and -100 fpm
                if (CurrentVerticalSpeed < -1000)
                {
                    deductions += 5;
                    OnLog?.Invoke(_("Log_ApproachGateVs", CurrentVerticalSpeed), Theme.Warning);
                }
                else if (CurrentVerticalSpeed > -100)
                {
                    deductions += 5;
                    OnLog?.Invoke(_("Log_ApproachGateVsLow", CurrentVerticalSpeed), Theme.Warning);
                }

                // 4. Bank angle — only for ILS approaches. On RNP/visual, the final turn
                // to align with the runway may legitimately exceed 7° at 1000 ft AGL.
                if (_expectedIls != null && Math.Abs(_currentBank) > 7.0)
                {
                    deductions += 3;
                    OnLog?.Invoke(_("Log_ApproachGateBank", _currentBank.ToString("F1")), Theme.Warning);
                }

                // 5. Pitch angle
                if (_currentPitch < -2.5 || _currentPitch > 10.0)
                {
                    deductions += 3;
                    OnLog?.Invoke(_("Log_ApproachGatePitch", _currentPitch.ToString("F1")), Theme.Warning);
                }

                // 6. Gear
                if (!data.GearDown)
                {
                    deductions += 5;
                    OnLog?.Invoke(_("Log_ApproachGateGear"), Theme.Warning);
                }

                // 7. Flaps
                if (CurrentFlapsPosition < 50)
                {
                    deductions += 4;
                    OnLog?.Invoke(_("Log_ApproachGateFlaps", CurrentFlapsPosition.ToString("F0")), Theme.Warning);
                }

                _stabilizedApproachDeductions = deductions;

                if (deductions == 0)
                {
                    OnLog?.Invoke(_("Log_ApproachGateOk", (int)agl), Theme.Success);
                }
                else
                {
                    OnLog?.Invoke(_("Log_ApproachGateUnstable", (int)agl, deductions), Theme.Warning);
                    OnOsdMessage?.Invoke($"UNSTABILIZED  −{deductions} PTS", OsdSeverity.Critical);
                }

                // 9. QNH de destino — si hay Transition Level de NavData, ya se comprobó
                //    500 ft bajo el TL (CheckViolations). Sin TL, fallback a este gate.
                if (_destTransitionLevelFt <= 0 && !_destQnhChecked)
                {
                    string qnhIcao = _effectiveDestination ?? _activePlan?.Destination;
                    if (!string.IsNullOrEmpty(qnhIcao))
                    {
                        _destQnhChecked = true;
                        CheckQnhAsync(qnhIcao, AircraftQnhMb).ConfigureAwait(false);
                    }
                }
            }

            _prevApproachAgl = agl;
        }

        /// <summary>
        /// Monitors localizer alignment and decision altitude while below the 1000 ft gate.
        /// Called every telemetry cycle in Approach phase.
        /// Also advances waypoint sequencing when the aircraft passes within 0.5 NM of a fix.
        /// </summary>
        private void CheckApproachBelowGate(RawTelemetryData data)
        {
            double agl = CurrentAGL;

            // Localizer heading alignment (below 500 ft AGL, above 50 ft to avoid rollout noise)
            if (_expectedIls != null && agl < 500 && agl > 50)
            {
                double hdgDelta = ((CurrentHeading - _expectedIls.Course + 540) % 360) - 180;
                if (Math.Abs(hdgDelta) > 5.0 && _localizerViolations < 2)
                {
                    _localizerViolations++;
                    OnLog?.Invoke(_("Log_LocalizerDeviation", $"{CurrentHeading:F0}", $"{hdgDelta:+0.0;-0.0}", $"{_expectedIls.Course:F0}"), Theme.Warning);
                }
            }

            // Decision altitude check (only when DA is known and aircraft is still in the air)
            if (_daAltitudeFt > 0 && CurrentAltitude < _daAltitudeFt && !IsOnGround && !_belowMinimums)
            {
                _belowMinimums = true;
                OnLog?.Invoke(_("Log_BelowMinimums", (int)CurrentAltitude, $"{_daAltitudeFt:F0}"), Theme.Warning);
            }

            // Waypoint sequencing — flyby: 0.5 NM; flyover: 0.3 NM (must cross the fix)
            if (_approachFixes != null && _nextFixIndex < _approachFixes.Count)
            {
                var fix = _approachFixes[_nextFixIndex];
                double cosLat = Math.Cos(CurrentLat * Math.PI / 180.0);
                double dN = (fix.Lat - CurrentLat) * 111320.0;
                double dE = (fix.Lon - CurrentLon) * 111320.0 * cosLat;
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

        private void ResetFlightState()
        {
            ActivePirepId = "";
            _activePlan = null;
            CurrentPhase = FlightPhase.Idle;
            TouchdownFpm = null;
            _totalDistanceKm = 0;
            _lastAirborneTime = null;
            _lastPosition = null;
            _lastPositionTime = null;
            _isTimerStarted = false;
            _serverCreatedAt = default;
            _serverBlockOffTime = default;
            _serverBlockOnTime = default;
            _blockOffRecorded = false;
            _initialFuel = 0;
            _totalFuelUsed = 0;
            _fuelAtTakeoffRoll = 0;
            _fuelAtTaxiInStart = 0;
            CurrentFuel = 0;
            _hasLandedThisFlight = false;
            _climbStableStart    = DateTime.MinValue;
            _descentStart        = DateTime.MinValue;
            _stepClimbStart      = DateTime.MinValue;
            _descentToClimbStart = DateTime.MinValue;
            _goAroundStart       = DateTime.MinValue;
            _lastTaxiPositionEvent = DateTime.MinValue;
            _blockOffRecorded = false;
            _touchdownPitch = 0;
            _touchdownBank = 0;
            _touchdownGForce = 0;
            _touchdownLatSaved = 0;
            _touchdownLonSaved = 0;
            _touchdownHeadingDeg = 0;
            _touchdownDistanceFt = 0;
            _touchdownCenterlineDeviationFt = 0;
            _touchdownRunwayName = null;
            _expectedIls       = null;
            _expectedApproach  = null;
            _approachFixes     = null;
            _nextFixIndex      = 0;
            _localizerViolations = 0;
            _belowMinimums     = false;
            _daAltitudeFt      = 0;
            _ilsGateChecked    = false;
            _ilsTunedCorrectly = true;
            _overspeedCount = 0; _overspeedPenaltyCount = 0;
            _wasOverspeed = false;
            _lightsViolationCount = 0;
            _lightsViolationActive = false;
            _landingLightReminderSent = false;
            _passing10kFtSent = false;
            _qnhViolationCount = 0;
            _isOfflineFlight = false;
            _departedLate = false;
            _procedureSpdViolations = 0;
            _approachGateEvaluated = false;
            _prevApproachAgl = double.MaxValue;
            _stabilizedApproachDeductions = 0;
            _taOsdSent = false; _tlOsdSent = false; _destQnhChecked = false; _originQnhChecked = false;
            _originTransitionAltFt = 0; _destTransitionLevelFt = 0; _effectiveDestination = null;
            OnPhaseChanged?.Invoke(CurrentPhase.ToString());
            PhaseChanged?.Invoke(CurrentPhase);
        }

        #endregion

        #region Public Methods

        public double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
            => UnitConverter.CalculateDistanceKm(lat1, lon1, lat2, lon2);



        public string CurrentTimerDisplay
        {
            get
            {
                if (!_isTimerStarted) return "00:00:00";
                TimeSpan elapsed = DateTime.UtcNow - _serverCreatedAt;
                return $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        public TimeSpan CurrentFlightTime => _isTimerStarted ? DateTime.UtcNow - _serverCreatedAt : TimeSpan.Zero;

        public async Task<bool> CancelFlight()
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return false;
            bool success = await _apiService.DeletePirep(ActivePirepId);
            if (success) { OnLog?.Invoke(_("Log_FlightCancelled"), Theme.Warning); ResetFlightState(); }
            return success;
        }

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

        public async Task<bool> StartFlight(SimbriefPlan plan, Pilot pilot, double actualFuel)
        {
            if (_apiService == null) { OnLog?.Invoke(string.Format("{0}: ApiService not configured.", _("Error")), Theme.Warning); return false; }
            if (actualFuel <= 0) { OnLog?.Invoke(string.Format("{0}: No fuel data from simulator.", _("Error")), Theme.Warning); return false; }

            // Reset all scoring state before the API call so stale data from a
            // previous flight never survives into a new one, even if prefileing fails.
            _touchdownCaptured = false; _touchdownTimestamp = DateTime.MinValue;
            TouchdownFpm = null;
            _touchdownPitch = 0; _touchdownBank = 0; _touchdownGForce = 0;
            _touchdownLatSaved = 0; _touchdownLonSaved = 0; _touchdownHeadingDeg = 0;
            _touchdownDistanceFt = 0; _touchdownCenterlineDeviationFt = 0; _touchdownRunwayName = null;
            _overspeedCount = 0; _overspeedPenaltyCount = 0; _wasOverspeed = false;
            _apEngagedCounter = 0;
            _singleEngineTaxiDetected = false; _bothEnginesRunning = false;
            _taxiOutMovingCycles = 0; _taxiOutSingleEngineCycles = 0;
            _taxiInMovingCycles  = 0; _taxiInSingleEngineCycles  = 0;
            _lightsViolationCount = 0; _lightsViolationActive = false; _landingLightReminderSent = false; _passing10kFtSent = false;
            _qnhViolationCount = 0; _isOfflineFlight = false;
            _approachGateEvaluated = false; _prevApproachAgl = double.MaxValue;
            _stabilizedApproachDeductions = 0; _hasLandedThisFlight = false;

            _activePlan = plan;
            _activePilot = pilot;
            OnLog?.Invoke($"{_("SendingPrefile")}...", Theme.MainText);

            double plannedFuel = plan.BlockFuel;
            plan.BlockFuel = actualFuel;

            var result = await _apiService.PrefileFlight(plan, pilot);
            ActivePirepId = result.pirepId;
            if (!string.IsNullOrEmpty(ActivePirepId))
            {
                _initialFuel = actualFuel;   // ya viene en kg desde MainViewModel
                _totalFuelUsed = 0;
                _serverCreatedAt = result.serverCreatedAt;
                _isTimerStarted = true;
                _serverBlockOffTime = default;
                _serverBlockOnTime = default;
                _blockOffRecorded = false;

                if (!string.IsNullOrEmpty(plan.BidId))
                    await _apiService.DeleteBid(plan.BidId);

                await Task.Run(() => UpdatePirepStatus("BST"));
                // Resolver Vmo según tipo de avión del plan
                var perf = AircraftPerformanceTable.Get(_activePlan?.AircraftIcao);
                _vmoKts = perf.VmoKts;
                CurrentPhase = FlightPhase.Boarding;
                _phaseStartTime = DateTime.UtcNow;
                FlightStartTime = DateTime.Now;
                PhaseChanged?.Invoke(FlightPhase.Boarding);
                return true;
            }
            OnLog?.Invoke(string.Format("{0}: Server did not return a PIREP ID.", _("Error")), Theme.Danger);
            return false;
        }

        public void MarkOfflineFlight() => _isOfflineFlight = true;

        public void SetRunwayTouchdownData(double thresholdDistFt, double centerlineDeviationFt, string runwayName)
        {
            _touchdownDistanceFt = thresholdDistFt;
            _touchdownCenterlineDeviationFt = centerlineDeviationFt;
            _touchdownRunwayName = runwayName;
        }

        /// <summary>
        /// Called from MainViewModel just before FilePirep to report SID/STAR speed violations.
        /// </summary>
        public void SetProcedureSpdViolations(int count)
        {
            _procedureSpdViolations = count;
        }

        /// <summary>
        /// Loads ILS and approach procedure data for scoring and waypoint sequencing.
        /// Call from MainViewModel when the Approach phase starts (after GetRunwayThreshold).
        /// </summary>
        public void SetApproachData(IlsData ils, ApproachInfo approach, IList<ApproachFix> fixes)
        {
            _expectedIls      = ils;
            _expectedApproach = approach;
            _approachFixes    = fixes ?? new List<ApproachFix>();
            _nextFixIndex     = 0;
            _ilsGateChecked   = false;
            _localizerViolations = 0;
            _belowMinimums    = false;
            _ilsTunedCorrectly = true;
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

        public void SetOriginTransitionAlt(double ft)   { if (ft > 0) _originTransitionAltFt = ft; }
        public void SetDestTransitionLevel(double ft)   { if (ft > 0) _destTransitionLevelFt  = ft; }
        public void SetEffectiveDestination(string icao){ _effectiveDestination = icao; }

        public bool CanStartFlight()
        {
            if (_activePilot == null) return false;
            if (_activePlan == null) return false;
            if (!PositionValidationStatus.IcaoMatch) return false;
            return true;
        }

        /// <summary>
        /// Restaura el estado interno del FlightManager a partir de un PIREP IN_PROGRESS
        /// encontrado en el servidor. No hace ninguna llamada de red — solo reconstruye
        /// el estado local para que el polling y las actualizaciones retomen normalmente.
        /// </summary>
        /// <param name="pirep">PIREP activo obtenido del servidor.</param>
        /// <param name="pilot">Piloto autenticado en esta sesión.</param>
        public void ResumeFlight(Models.Pirep pirep, Pilot pilot)
        {
            _activePilot = pilot;

            // Reconstruir el plan mínimo necesario para FilePirep y CheckViolations
            _activePlan = new SimbriefPlan
            {
                FlightNumber = pirep.FlightNumber,
                Origin = pirep.Origin,
                Destination = pirep.Destination,
                AircraftIcao = pirep.AircraftType,
                Aircraft = pirep.AircraftType,
                BlockFuel = pirep.BlockFuel,
                PlannedBlockFuel = pirep.BlockFuel,
                Distance = pirep.Distance,
            };

            // Restaurar estado de vuelo
            ActivePirepId = pirep.Id;
            _initialFuel = pirep.BlockFuel;
            _totalFuelUsed = pirep.FuelUsed;
            _isTimerStarted = true;
            _blockOffRecorded = true;  // el block-off ya fue enviado en la sesión anterior

            // Intentar parsear el server created_at para cálculo correcto de flight_time
            if (DateTime.TryParse(pirep.CreatedAt, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var created))
                _serverCreatedAt = created;
            else
                _serverCreatedAt = DateTime.UtcNow.AddMinutes(-pirep.FlightTime);

            CurrentPhase = PhaseFromPirepStatus(pirep.Status);
            _phaseStartTime = DateTime.UtcNow;
            FlightStartTime = DateTime.Now;

            // Resolver Vmo para overspeed detection
            var perf = AircraftPerformanceTable.Get(pirep.AircraftType);
            _vmoKts = perf.VmoKts;

            // El scoring de esta sesión arranca limpio (no podemos recuperar
            // los datos de la sesión anterior)
            _touchdownCaptured = false; _touchdownTimestamp = DateTime.MinValue;
            TouchdownFpm = null;
            _touchdownPitch = 0;
            _touchdownBank = 0;
            _touchdownGForce = 0;
            _touchdownLatSaved = 0;
            _touchdownLonSaved = 0;
            _touchdownHeadingDeg = 0;
            _touchdownDistanceFt = 0;
            _touchdownCenterlineDeviationFt = 0;
            _touchdownRunwayName = null;
            _expectedIls       = null;
            _expectedApproach  = null;
            _approachFixes     = null;
            _nextFixIndex      = 0;
            _localizerViolations = 0;
            _belowMinimums     = false;
            _daAltitudeFt      = 0;
            _ilsGateChecked    = false;
            _ilsTunedCorrectly = true;
            _overspeedCount = 0; _overspeedPenaltyCount = 0;
            _wasOverspeed = false;
            _lightsViolationCount = 0;
            _lightsViolationActive = false;
            _beaconViolationActive = false;
            _landingLightReminderSent = false;
            _passing10kFtSent = false;
            _qnhViolationCount = 0;
            _isOfflineFlight = false;
            _approachGateEvaluated = false; _prevApproachAgl = double.MaxValue;
            _stabilizedApproachDeductions = 0; _hasLandedThisFlight = false;
            _taOsdSent = false; _tlOsdSent = false; _destQnhChecked = false; _originQnhChecked = false;
            _effectiveDestination = null;
        }

        /// <summary>
        /// Restaura las penalizaciones acumuladas de una sesión anterior a partir del
        /// último checkpoint SCH encontrado en el historial ACARS de phpVMS.
        /// Solo llamar justo después de ResumeFlight().
        /// </summary>
        public void SetResumedPenalties(int overspeed, int lights, int stabilized,
            int qnh, bool offline, bool late, int procSpd, int localizer, bool belowMins)
        {
            _overspeedCount               = overspeed;
            _overspeedPenaltyCount        = overspeed; // conservative: resumed events assumed penalized
            _lightsViolationCount         = lights;
            _stabilizedApproachDeductions = stabilized;
            _qnhViolationCount            = qnh;
            _isOfflineFlight              = offline;
            _departedLate                 = late;
            _procedureSpdViolations       = procSpd;
            _localizerViolations          = localizer;
            _belowMinimums                = belowMins;
        }

        /// <summary>
        /// Updates the flight phase based on current telemetry using relative thresholds.
        /// </summary>
        /// <summary>
        /// Updates the flight phase based on current telemetry using relative thresholds.
        /// </summary>
        public void UpdatePhase(int altitude, int groundSpeed, bool isOnGround, int verticalSpeed, double distanceToDestination = -1)
        {
            var previousPhase = CurrentPhase;

            // ===== ACTUALIZAR ALTITUD MÁXIMA =====
            if (altitude > _maxAltitudeReached)
                _maxAltitudeReached = altitude;

            // ===== OBTENER REFERENCIAS DEL PLAN =====
            int cruiseAlt = _activePlan?.CruiseAltitude ?? 10000;
            double totalDistance = _activePlan?.Distance ?? 100;
            double destElev = _destinationElevation;

            // ===== UMBRALES RELATIVOS =====
            double approachDistanceThreshold = Math.Min(totalDistance * 0.1, 20);
            double aglThreshold = Math.Min(5000, cruiseAlt * 0.2);

            // ===== DETECCIÓN DE TENDENCIA =====
            bool isClimbing = verticalSpeed > 100;
            bool isDescending = verticalSpeed < -100;
            bool canChangePhase = (DateTime.UtcNow - _phaseStartTime).TotalSeconds >= 5;

            // ===== DETECCIÓN DE ROTACIÓN (inicio del despegue) =====
            // AHORA con verificación de que no se haya aterrizado ya en este vuelo
            if (_wasOnGround && groundSpeed > 60 && _currentPitch > 2.0 && _currentPitch < 20 && !_hasLandedThisFlight)
            {
                if (CurrentPhase != FlightPhase.Takeoff && CurrentPhase != FlightPhase.TakeoffRoll)
                {
                    OnTakeoffDetected?.Invoke(groundSpeed, altitude, verticalSpeed);
                    TransitionTo(FlightPhase.Takeoff, previousPhase);
                }
            }

            // ===== LIFTOFF REAL (momento en que la rueda deja el suelo) =====
            if (_wasOnGround && !isOnGround && CurrentPhase == FlightPhase.Takeoff)
            {
                _wasOnGround = isOnGround;
                // No cambiamos fase, seguimos en Takeoff
            }

            // ===== DETECCIÓN DE TOMACONTACTO (transición aire → suelo) =====
            if (!_wasOnGround && isOnGround && CurrentPhase != FlightPhase.AfterLanding)
            {
                // Solo en fases de aproximación/descenso se considera un aterrizaje real.
                // En Takeoff/Climb/Enroute el flicker de SimOnGround es un falso positivo
                // (rebote durante la rotación o glitch del simulador).
                if (CurrentPhase == FlightPhase.Descent
                 || CurrentPhase == FlightPhase.Approach
                 || CurrentPhase == FlightPhase.Landing)
                {
                    RegisterTouchdown(verticalSpeed);
                    TransitionTo(FlightPhase.AfterLanding, previousPhase);
                    Task.Run(() => UpdatePirepStatus("LAN"));
                    _wasOnGround = isOnGround;
                    return;
                }
            }

            // ===== FASES EN TIERRA =====
            if (isOnGround)
            {
                switch (CurrentPhase)
                {
                    case FlightPhase.Boarding:
                        if (groundSpeed > 0.5)
                        {
                            if (_pushbackStartTime == DateTime.MinValue)
                                _pushbackStartTime = DateTime.UtcNow;

                            double secondsMoving = (DateTime.UtcNow - _pushbackStartTime).TotalSeconds;

                            if (groundSpeed <= PUSHBACK_MAX_SPEED && secondsMoving >= PUSHBACK_MIN_DURATION)
                            {
                                _pushbackStartTime = DateTime.MinValue;
                                CheckProcedureAtPhaseEntry(FlightPhase.Pushback);
                                TransitionTo(FlightPhase.Pushback, previousPhase);
                                Task.Run(() => UpdateBlockOffTime());
                            }
                            else if (groundSpeed > TAXIOUT_MIN_SPEED && secondsMoving >= TAXIOUT_MIN_DURATION)
                            {
                                _pushbackStartTime = DateTime.MinValue;
                                CheckProcedureAtPhaseEntry(FlightPhase.TaxiOut);
                                TransitionTo(FlightPhase.TaxiOut, previousPhase);
                                Task.Run(() => UpdateBlockOffTime());
                            }
                        }
                        else
                        {
                            _pushbackStartTime = DateTime.MinValue;
                        }
                        break;

                    case FlightPhase.Pushback:
                        if (groundSpeed > TAXIOUT_MIN_SPEED)
                        {
                            CheckProcedureAtPhaseEntry(FlightPhase.TaxiOut);
                            TransitionTo(FlightPhase.TaxiOut, previousPhase);
                        }
                        break;

                    case FlightPhase.TaxiOut:
                        if ((DateTime.UtcNow - _lastTaxiPositionEvent).TotalSeconds >= 2.0)
                        {
                            _lastTaxiPositionEvent = DateTime.UtcNow;
                            OnTaxiPositionUpdate?.Invoke(CurrentLat, CurrentLon, CurrentHeading,
                                _activePlan?.Origin ?? _currentAirport, false);
                        }
                        if (groundSpeed > 30 && _currentPitch < 1.0)
                        {
                            CheckProcedureAtPhaseEntry(FlightPhase.TakeoffRoll);
                            TransitionTo(FlightPhase.TakeoffRoll, previousPhase);
                        }
                        break;

                    case FlightPhase.TakeoffRoll:
                        if (groundSpeed > 50 && _currentPitch > 2.0)
                        {
                            OnLog?.Invoke(_("Log_TakeoffRotation", groundSpeed, _currentPitch.ToString("F1")), Theme.Success);
                            OnTakeoffDetected?.Invoke(groundSpeed, altitude, verticalSpeed);
                            TransitionTo(FlightPhase.Takeoff, previousPhase);
                        }
                        else if (groundSpeed < 30)
                        {
                            TransitionTo(FlightPhase.TaxiOut, previousPhase);
                        }
                        break;

                    case FlightPhase.AfterLanding:
                        if ((DateTime.UtcNow - _lastTaxiPositionEvent).TotalSeconds >= 2.0)
                        {
                            _lastTaxiPositionEvent = DateTime.UtcNow;
                            OnTaxiPositionUpdate?.Invoke(CurrentLat, CurrentLon, CurrentHeading,
                                _activePlan?.Destination ?? _currentAirport, true);
                        }
                        if (groundSpeed < 40)
                        {
                            if (_fuelAtTakeoffRoll > 0)
                            {
                                double tripFuel = _fuelAtTakeoffRoll - CurrentFuel;
                                if (tripFuel > 0)
                                    OnLog?.Invoke(_("Log_FuelTrip", (int)Math.Round(tripFuel)), Theme.MainText);
                            }
                            _fuelAtTaxiInStart = CurrentFuel;
                            TransitionTo(FlightPhase.TaxiIn, previousPhase);
                        }
                        break;

                    case FlightPhase.TaxiIn:
                        if ((DateTime.UtcNow - _lastTaxiPositionEvent).TotalSeconds >= 2.0)
                        {
                            _lastTaxiPositionEvent = DateTime.UtcNow;
                            OnTaxiPositionUpdate?.Invoke(CurrentLat, CurrentLon, CurrentHeading,
                                _activePlan?.Destination ?? _currentAirport, true);
                        }
                        if (groundSpeed < 1)
                        {
                            if (_stoppedStartTime == DateTime.MinValue)
                                _stoppedStartTime = DateTime.UtcNow;

                                if ((DateTime.UtcNow - _stoppedStartTime).TotalSeconds >= 90 &&
                                    (!_areEnginesOn || _hotelModeActive))
                                {
                                    _stoppedStartTime = DateTime.MinValue;
                                    if (_fuelAtTaxiInStart > 0)
                                    {
                                        double taxiInFuel = _fuelAtTaxiInStart - CurrentFuel;
                                        if (taxiInFuel > 0)
                                            OnLog?.Invoke(_("Log_FuelTaxiIn", (int)Math.Round(taxiInFuel)), Theme.MainText);
                                    }
                                    // Single-engine TaxiIn evaluation
                                    if (!_singleEngineTaxiDetected && _bothEnginesRunning &&
                                        _taxiInMovingCycles > 0 &&
                                        (double)_taxiInSingleEngineCycles / _taxiInMovingCycles >= SingleEngineTaxiMinRatio)
                                    {
                                        _singleEngineTaxiDetected = true;
                                        OnLog?.Invoke(_("Log_SingleEngineTaxiIn"), Theme.MainText);
                                        OnOsdMessage?.Invoke("SINGLE ENGINE TAXI  +5 PTS", OsdSeverity.Success);
                                    }
                                    OnBlockDetected?.Invoke();
                                    Task.Run(() => UpdateBlockOnTime());
                                    TransitionTo(FlightPhase.OnBlock, previousPhase);
                                }
                        }
                        else
                        {
                            _stoppedStartTime = DateTime.MinValue;
                        }
                        break;
                }
            }
            else  // ===== FASES EN EL AIRE =====
            {
                double altitudeAboveDest = altitude - destElev;

                switch (CurrentPhase)
                {
                    case FlightPhase.Takeoff:
                    case FlightPhase.TakeoffRoll:
                        if (!isOnGround && (isClimbing || verticalSpeed > 0))
                            TransitionTo(FlightPhase.Climb, previousPhase);
                        break;

                    case FlightPhase.Climb:
                        // Condición para pasar a Enroute:
                        // - Si estamos cerca del crucero planificado (dentro de 500 ft) y ascenso bajo,
                        //   O bien si hemos estado en Climb más de 5 minutos y el ascenso es muy bajo (<100 fpm)
                        double altDiff = Math.Abs(altitude - cruiseAlt);
                        bool nearCruise = altDiff < 500;
                        bool lowVs = Math.Abs(verticalSpeed) < 200;
                        bool climbTimeout = (DateTime.UtcNow - _phaseStartTime).TotalMinutes >= 5;
                        bool veryLowVs = Math.Abs(verticalSpeed) < 100;

                        if ((nearCruise && lowVs) || (climbTimeout && veryLowVs))
                        {
                            if (_climbStableStart == DateTime.MinValue)
                                _climbStableStart = DateTime.UtcNow;
                            else if ((DateTime.UtcNow - _climbStableStart).TotalSeconds >= 10)
                            {
                                _climbStableStart = DateTime.MinValue;
                                TransitionTo(FlightPhase.Enroute, previousPhase);
                            }
                        }
                        else
                        {
                            _climbStableStart = DateTime.MinValue;
                        }

                        // Descenso directo desde Climb — umbral más alto para no confundirse con
                        // fluctuaciones de QNH (~100 fpm) en salidas con altitud restringida.
                        if (verticalSpeed < -500 && altitude < _maxAltitudeReached - 500
                            && (DateTime.UtcNow - _phaseStartTime).TotalSeconds >= 5)
                        {
                            if (_descentStart == DateTime.MinValue)
                                _descentStart = DateTime.UtcNow;
                            else if ((DateTime.UtcNow - _descentStart).TotalSeconds >= 20)
                            {
                                _descentStart = DateTime.MinValue;
                                TransitionTo(FlightPhase.Descent, previousPhase);
                            }
                        }
                        else
                        {
                            _descentStart = DateTime.MinValue;
                        }
                        break;

                    case FlightPhase.Enroute:
                        // Detectar step climb
                        if (verticalSpeed > 500 && altitude < cruiseAlt - 500)
                        {
                            if (_stepClimbStart == DateTime.MinValue)
                                _stepClimbStart = DateTime.UtcNow;
                            else if ((DateTime.UtcNow - _stepClimbStart).TotalSeconds >= 10)
                            {
                                _stepClimbStart = DateTime.MinValue;
                                TransitionTo(FlightPhase.Climb, previousPhase);
                            }
                        }
                        else
                        {
                            _stepClimbStart = DateTime.MinValue;
                        }

                        // Detectar descenso sostenido — umbral elevado para no dispararse con
                        // turbulencia suave o cambios de QNH en zonas de transición
                        if (verticalSpeed < -500 && altitude < _maxAltitudeReached - 500)
                        {
                            if (_descentStart == DateTime.MinValue)
                                _descentStart = DateTime.UtcNow;
                            else if ((DateTime.UtcNow - _descentStart).TotalSeconds >= 20)
                            {
                                _descentStart = DateTime.MinValue;
                                TransitionTo(FlightPhase.Descent, previousPhase);
                            }
                        }
                        else
                        {
                            _descentStart = DateTime.MinValue;
                        }
                        break;

                    case FlightPhase.Descent:
                        if ((distanceToDestination > 0 && distanceToDestination < approachDistanceThreshold) ||
                            (altitudeAboveDest < aglThreshold && altitudeAboveDest > 0))
                        {
                            if (canChangePhase)
                            {
                                OnLog?.Invoke(_("Log_ApproachStarted"), Theme.Approach);
                                TransitionTo(FlightPhase.Approach, previousPhase);
                            }
                            _descentToClimbStart = DateTime.MinValue;
                        }
                        // Recuperación: si el VS es considerablemente positivo y el avión no está
                        // cerca del destino, volver a Climb (cubre falsa transición por QNH o
                        // altitud restringida con ascenso posterior).
                        else if (verticalSpeed > 500)
                        {
                            if (_descentToClimbStart == DateTime.MinValue)
                                _descentToClimbStart = DateTime.UtcNow;
                            else if ((DateTime.UtcNow - _descentToClimbStart).TotalSeconds >= 20)
                            {
                                _descentToClimbStart = DateTime.MinValue;
                                OnLog?.Invoke(_("Log_ResumingClimb", altitude), Theme.MainText);
                                TransitionTo(FlightPhase.Climb, previousPhase);
                            }
                        }
                        else
                        {
                            _descentToClimbStart = DateTime.MinValue;
                        }
                        break;

                    case FlightPhase.Approach:
                        // AGL en aproximación = MSL − elevación aeropuerto destino.
                        // No usamos radar altímetro aquí: en terrenos montañosos (Andes,
                        // Alpes, Himalaya) el radar lee la orografía bajo las ruedas, que
                        // puede estar muy por encima del aeropuerto y daría AGL incorrecto.
                        bool isGoAroundClimb = verticalSpeed > 600;
                        bool inGoAroundRange = altitudeAboveDest > 100 && altitudeAboveDest < 3000;
                        bool enoughTimeInApproach = (DateTime.UtcNow - _phaseStartTime).TotalSeconds >= 30;

                        if (isGoAroundClimb && inGoAroundRange && enoughTimeInApproach)
                        {
                            if (_goAroundStart == DateTime.MinValue)
                                _goAroundStart = DateTime.UtcNow;
                            else if ((DateTime.UtcNow - _goAroundStart).TotalSeconds >= 8)
                            {
                                _goAroundStart = DateTime.MinValue;
                                OnLog?.Invoke(_("Log_GoAround", (int)altitudeAboveDest, verticalSpeed), Theme.Warning);
                                OnOsdMessage?.Invoke("GO AROUND", OsdSeverity.Warning);
                                TransitionTo(FlightPhase.Climb, previousPhase);
                            }
                        }
                        else
                        {
                            _goAroundStart = DateTime.MinValue;
                        }
                        break;

                    case FlightPhase.AfterLanding:
                        // Touch and go: el avión volvió al aire sin completar el taxi.
                        // Requiere mínimo 5 s en tierra para descartar rebotes (bounce).
                        if (groundSpeed > 60 &&
                            (DateTime.UtcNow - _touchdownTimestamp).TotalSeconds >= 5.0)
                        {
                            OnLog?.Invoke(_("Log_TouchAndGo", groundSpeed), Theme.Warning);
                            _touchdownCaptured = false;
                            _approachGateEvaluated = false;
                            _prevApproachAgl = double.MaxValue;
                            _touchdownDistanceFt = 0;
                            _touchdownCenterlineDeviationFt = 0;
                            _touchdownRunwayName = null;
                            _expectedIls       = null;
                            _expectedApproach  = null;
                            _approachFixes     = null;
                            _nextFixIndex      = 0;
                            _localizerViolations = 0;
                            _belowMinimums     = false;
                            _daAltitudeFt      = 0;
                            _ilsGateChecked    = false;
                            _ilsTunedCorrectly = true;
                            _taOsdSent = false; _tlOsdSent = false;
                            _destQnhChecked = false; _originQnhChecked = false;
                            TransitionTo(FlightPhase.Climb, previousPhase);
                        }
                        break;
                }
            }

            _wasOnGround = isOnGround;

            if (previousPhase != CurrentPhase)
            {
                Task.Run(() => UpdatePirepStatus(FlightPhaseHelper.GetStatusCode(CurrentPhase)));
            }
        }

        /// <summary>
        /// Actualiza el estado del gestor de vuelo con los datos crudos del simulador.
        /// Calcula la distancia acumulada, actualiza la fase de vuelo y registra el estado
        /// de todos los sistemas de la aeronave.
        /// </summary>
        /// <param name="data">
        /// Datos telémétricos crudos leídos directamente desde FSUIPC en el ciclo de polling.
        /// </param>
        public void UpdateTelemetry(RawTelemetryData data)
        {
            if (data == null) return;

            LastRawData = data;
            HasSimulatorData = true;

            CurrentAltitude = (int)data.AltitudeFeet;
            CurrentGroundSpeed = (int)data.GroundSpeedKt;
            CurrentVerticalSpeed = (int)data.VerticalSpeedFpm;
            IsOnGround = data.IsOnGround;
            if (data.GroundAltitudeFeet > 0)
                _groundAltitudeFeet = data.GroundAltitudeFeet;
            CurrentLat = data.Latitude;
            CurrentLon = data.Longitude;
            CurrentHeading = data.HeadingDeg;
            _currentPitch = data.PitchDeg;
            _currentBank = data.BankDeg;
            CurrentIndicatedAirspeed = data.IndicatedAirspeedKt > 0
                                           ? (int)data.IndicatedAirspeedKt
                                           : (int)data.GroundSpeedKt;
            CurrentFuel = data.FuelLbs * 0.453592; ;
            CurrentTransponder = data.Transponder;
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
                AutopilotEngaged = false;
                _apEngagedCounter = 0;
            }
            SimTime = DateTime.UtcNow;
            RadarAltitude = data.RadarAltitudeFeet;
            PositionOrder = data.Order;
            _isParkingBrakeSet = data.ParkingBrakeOn;

            if (data.EnginesRunning && !_areEnginesOn && !string.IsNullOrEmpty(ActivePirepId))
            {
                // Motores recién encendidos — beacon requerido salvo hotel mode (hélice bloqueada)
                if (!_isBeaconOn && !data.HotelModeActive)
                {
                    _lightsViolationCount++;
                    OnLog?.Invoke(_("Log_PenaltyBeacon"), Theme.Warning);
                }

                // Blocks Off al encender motores en Boarding (aviones que no necesitan pushback)
                if (CurrentPhase == FlightPhase.Boarding && !_blockOffRecorded && !data.HotelModeActive)
                {
                    OnLog?.Invoke(_("Log_BlockOffEngines"), Theme.MainText);
                    Task.Run(() => UpdateBlockOffTime());
                }
            }

            _areEnginesOn = data.EnginesRunning;
            _hotelModeActive = data.HotelModeActive;

            // ── Detección de taxi con un solo motor ───────────────────────────────
            if (data.Eng1Running && data.Eng2Running)
                _bothEnginesRunning = true;

            if (!string.IsNullOrEmpty(ActivePirepId))
            {
                bool oneEngineOnly = data.Eng1Running ^ data.Eng2Running;
                bool moving = CurrentGroundSpeed > 3;
                if (CurrentPhase == FlightPhase.TaxiOut)
                {
                    if (moving) _taxiOutMovingCycles++;
                    if (moving && oneEngineOnly) _taxiOutSingleEngineCycles++;
                }
                else if (CurrentPhase == FlightPhase.TaxiIn)
                {
                    if (moving) _taxiInMovingCycles++;
                    if (moving && oneEngineOnly) _taxiInSingleEngineCycles++;
                }
            }

            // Sistemas
            IsGearDown = data.GearDown;
            CurrentFlapsPosition = data.FlapsPercent;
            FlapsLabel = data.FlapsLabel;
            
            // Spoilers con debounce
            DebounceState(data.SpoilersDeployed, ref _isSpoilersOn, ref _pendingSpoilers, ref _spoilersPending, SpoilersDebounceSeconds);
            AreSpoilersDeployed = _isSpoilersOn;

            AutobrakeSetting = data.AutobrakeSetting;

            // Luces (con debounce para filtrar parpadeos por cambio de fuente de energía)
            DebounceState(data.NavLightOn,     ref _isNavOn,         ref _pendingNavOn,     ref _navPending,     LightDebounceSeconds);
            DebounceState(data.BeaconLightOn,  ref _isBeaconOn,      ref _pendingBeaconOn,  ref _beaconPending,  LightDebounceSeconds);
            DebounceState(data.LandingLightOn, ref _isLandingLightOn, ref _pendingLandingOn, ref _landingPending, LightDebounceSeconds);
            DebounceState(data.TaxiLightOn,    ref _isTaxiLightOn,   ref _pendingTaxiOn,    ref _taxiPending,    LightDebounceSeconds);
            DebounceState(data.StrobeLightOn,  ref _isStrobeOn,      ref _pendingStrobeOn,  ref _strobePending,  LightDebounceSeconds);
            IsNavLightOn     = _isNavOn;
            IsBeaconLightOn  = _isBeaconOn;
            IsLandingLightOn = _isLandingLightOn;
            IsTaxiLightOn    = _isTaxiLightOn;
            IsStrobeLightOn  = _isStrobeOn;
            ApNavMode = data.ApNavMode;
            ApVertMode = data.ApVertMode;
            AircraftQnhMb = data.AircraftQnhMb;
            // Motores
            N1_1 = data.N1_1; N1_2 = data.N1_2;

            // Acumulación de distancia (solo con vuelo activo)
            if (!string.IsNullOrEmpty(ActivePirepId))
            {
                if (_lastPosition.HasValue)
                {
                    double distKm = CalculateDistanceKm(
                        _lastPosition.Value.lat, _lastPosition.Value.lon,
                        data.Latitude, data.Longitude);
                    if (distKm > 0 && distKm < 10)
                        _totalDistanceKm += distKm;
                }

                _lastPosition = (data.Latitude, data.Longitude);
                _lastPositionTime = DateTime.UtcNow;

                if (!data.IsOnGround && !_lastAirborneTime.HasValue)
                    _lastAirborneTime = DateTime.UtcNow;

                UpdatePhase(CurrentAltitude, CurrentGroundSpeed,
                            data.IsOnGround, CurrentVerticalSpeed);
                // Monitoreo de violaciones para scoring (solo en vuelo)
                if (!data.IsOnGround)
                {
                    CheckViolations(CurrentIndicatedAirspeed, (int)CurrentAGL, (int)CurrentAltitude, data.LandingLightOn, _isBeaconOn);
                    if (CurrentPhase == FlightPhase.Approach)
                    {
                        CheckStabilizedApproachGate(data);
                        CheckApproachBelowGate(data);
                    }
                }
            }
        }
        public async Task<bool> AbortFlight()
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return false;
            bool success = await _apiService.DeletePirep(ActivePirepId);
            if (success) { OnLog?.Invoke(_("Log_FlightAborted"), Theme.Warning); ResetFlightState(); }
            return success;
        }

        public async Task<bool> FilePirep()
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return false;

            int totalFlightTimeMinutes = (int)(DateTime.UtcNow - _serverCreatedAt).TotalMinutes;
            int actualFlightTimeMinutes = totalFlightTimeMinutes;
            if (_serverBlockOffTime != default && _serverBlockOnTime != default)
                actualFlightTimeMinutes = (int)(_serverBlockOnTime - _serverBlockOffTime).TotalMinutes;
            else if (_serverBlockOffTime != default)
                actualFlightTimeMinutes = (int)(DateTime.UtcNow - _serverBlockOffTime).TotalMinutes;

            double fuelUsed = _initialFuel - CurrentFuel;
            if (fuelUsed < 0) fuelUsed = 0;

            double totalDistanceNm = _totalDistanceKm * 0.539957;
            double plannedDistance = _activePlan?.Distance ?? 0;
            int plannedFlightTimeMinutes = (_activePlan?.EstTimeEnroute ?? 0) / 60;
            double blockFuel = _activePlan?.BlockFuel ?? 0;

            OnLog?.Invoke(_("Log_PlannedDistance", $"{plannedDistance:F1}"), Theme.MainText);
            OnLog?.Invoke(_("Log_PlannedTime", plannedFlightTimeMinutes), Theme.MainText);
            OnLog?.Invoke(_("Log_ActualDistance", $"{totalDistanceNm:F1}"), Theme.MainText);
            OnLog?.Invoke(_("Log_ActualTime", actualFlightTimeMinutes), Theme.MainText);
            if (_fuelAtTakeoffRoll > 0 && _fuelAtTaxiInStart > 0)
            {
                double taxiOutKg  = _initialFuel      - _fuelAtTakeoffRoll;
                double tripKg     = _fuelAtTakeoffRoll - _fuelAtTaxiInStart;
                double taxiInKg   = _fuelAtTaxiInStart - CurrentFuel;
                double taxiTotalKg = Math.Max(0, taxiOutKg) + Math.Max(0, taxiInKg);
                OnLog?.Invoke(_("Log_FuelSummary",
                    (int)Math.Round(Math.Max(0, taxiTotalKg)),
                    (int)Math.Round(Math.Max(0, tripKg)),
                    (int)Math.Round(fuelUsed)), Theme.MainText);
            }
            else
            {
                OnLog?.Invoke(_("Log_FuelUsed", $"{fuelUsed:F0}"), Theme.MainText);
            }
            // ── Calcular score ────────────────────────────────────────────────────────
            var scoreData = new FlightScoreData
            {
                LandingRate = (int)(TouchdownFpm ?? 0),
                LandingPitch = _touchdownPitch,
                LandingBank = _touchdownBank,
                LandingGForce = _touchdownGForce,
                OverspeedCount = _overspeedCount,
                OverspeedPenaltyCount = _overspeedPenaltyCount,
                LightsViolations = _lightsViolationCount,
                StabilizedApproachDeductions = _stabilizedApproachDeductions,
                QnhViolations = _qnhViolationCount,
                WasOfflineFlight = _isOfflineFlight,
                DepartedLate = _departedLate,
                TouchdownDistanceFt = _touchdownDistanceFt,
                CenterlineDeviationFt = _touchdownCenterlineDeviationFt,
                RunwayName = _touchdownRunwayName,
                IlsTunedCorrectly      = _ilsTunedCorrectly,
                LocalizerViolations    = _localizerViolations,
                BelowMinimums          = _belowMinimums,
                SingleEngineTaxi       = _singleEngineTaxiDetected && _bothEnginesRunning,
                ProcedureSpdViolations = _procedureSpdViolations,
            };
            var scoring = new ScoringService();
            ScoringResult scoreResult = scoring.Calculate(scoreData);
            LastFlightScore = scoreResult.TotalScore;
            string ratingKey = "Score_" + scoreResult.LandingRating.Replace(" ", "");
            OnLog?.Invoke(string.Format(_("Score_Result"), scoreResult.TotalScore, _(ratingKey)), Theme.Success);
            var critKeyMap = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Landing Rate",        "Score_CritLandingRate"  },
                { "G-Force",             "Score_CritGForce"       },
                { "Bank Angle",          "Score_CritBankAngle"    },
                { "Pitch Angle",         "Score_CritPitchAngle"   },
                { "Overspeed",           "Score_CritOverspeed"    },
                { "Lights Compliance",   "Score_CritLights"       },
                { "Stabilized Approach", "Score_CritStabilized"   },
                { "QNH Compliance",      "Score_CritQnh"          },
                { "IVAO Presence",       "Score_CritIvao"         },
                { "On-Time Departure",   "Score_CritDeparture"    },
                { "Touchdown Zone",       "Score_CritTdz"          },
                { "Centreline",           "Score_CritCentreline"   },
                { "Localizer Alignment",  "Score_CritLocalizer"    },
                { "Minimums Compliance",  "Score_CritMinimums"     },
                { "Procedure Speed",      "Score_CritProcSpeed"    }
            };
            foreach (var ded in scoreResult.Deductions)
            {
                string critLabel = critKeyMap.TryGetValue(ded.Criterion, out string ck) ? _(ck) : ded.Criterion;
                OnLog?.Invoke(string.Format(_("Score_Deduction"), ded.PointsDeducted, critLabel, ded.Reason), Theme.Warning);
            }
            if (scoreResult.SingleEngineTaxiBonus > 0)
                OnLog?.Invoke(_("Log_BonusSingleEngine", scoreResult.SingleEngineTaxiBonus), Theme.Success);

            var finalData = new
            {
                state = 2,
                submitted_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                distance = Math.Round(totalDistanceNm, 2),
                planned_distance = Math.Round(plannedDistance, 2),
                flight_time = actualFlightTimeMinutes,
                planned_flight_time = plannedFlightTimeMinutes,
                block_fuel = Math.Round(blockFuel, 0),
                fuel_used = Math.Round(fuelUsed, 0),
                landing_rate = (int)(TouchdownFpm ?? 0),
                score = scoreResult.TotalScore,          // ← NUEVO
                notes = $"vmsOpenAcars Report - Total: {totalFlightTimeMinutes} min, Flight: {actualFlightTimeMinutes} min, Dist: {totalDistanceNm:F1} NM"
            };

            bool success = await _apiService.FilePirep(ActivePirepId, finalData);
            if (success)
            {
                ActivePirepId = "";          // impide que CancelFlight borre el PIREP si falla algo después
                OnLog?.Invoke(_("Log_PirepFiled"), Theme.Success);
                ResetFlightState();
                return true;
            }
            return false;
        }

        public async Task UpdateFlightProgress()
        {
            if (string.IsNullOrEmpty(ActivePirepId) || !_isTimerStarted) return;
            DateTime reference = _serverBlockOffTime != default ? _serverBlockOffTime : _serverCreatedAt;
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