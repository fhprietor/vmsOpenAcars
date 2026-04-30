using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;
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
        private DateTime _climbStableStart = DateTime.MinValue;
        private DateTime _descentStart = DateTime.MinValue;
        private DateTime _stepClimbStart = DateTime.MinValue;
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

        // ── Scoring ──────────────────────────────────────────────────────────────────
        private double _touchdownPitch = 0;
        private double _touchdownBank = 0;
        private double _touchdownGForce = 0;
        private int _overspeedCount = 0;
        private bool _wasOverspeed = false;
        private int _lightsViolationCount = 0;
        private bool _lightsViolationActive = false;
        private int _qnhViolationCount = 0;
        private bool _isOfflineFlight = false;
        private int _vmoKts = 320;

        // ── Approximation gate (1000 ft AGL) ─────────────────────────────────────
        private bool _approachGateEvaluated = false;
        private double _prevApproachAgl = double.MaxValue;
        private int _stabilizedApproachDeductions = 0;
        private bool _isNavOn;
        private bool _isStrobeOn;
        private bool _isTaxiLightOn;
        private bool _isLandingLightOn;
        private bool _isBeaconOn;

        // Debounce de luces: evita falsos OFF durante cambios de fuente de energía
        private const double LightDebounceSeconds = 2.0;
        private bool _pendingNavOn, _pendingBeaconOn, _pendingLandingOn, _pendingTaxiOn, _pendingStrobeOn;
        private DateTime _navPending = DateTime.MinValue;
        private DateTime _beaconPending = DateTime.MinValue;
        private DateTime _landingPending = DateTime.MinValue;
        private DateTime _taxiPending = DateTime.MinValue;
        private DateTime _strobePending = DateTime.MinValue;

        // Control de fases
        private DateTime _phaseStartTime = DateTime.UtcNow;
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
        public bool IsSeatBeltSignOn { get; private set; }
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
        public int CurrentIndicatedAirspeed { get; private set; }
        public int CurrentAltitude { get; private set; }
        public int CurrentVerticalSpeed { get; private set; }
        public int? TouchdownFpm { get; private set; }
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
        /// - Crucero (Enroute)          → radar altímetro (clearance real sobre terreno de tránsito)
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
                        return RadarAltitude;
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
        public event Action<ValidationStatus> OnPositionValidated;
        public event Action<int, double, double, double> OnLandingDetected;
        public event Action OnBlockDetected;
        public event Action<int, int, int> OnTakeoffDetected;

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
        /// Aplica debounce a un estado de luz: solo acepta un cambio de estado
        /// después de que haya sido estable durante LightDebounceSeconds.
        /// Evita que parpadeos durante cambio de fuente de energía disparen penalizaciones.
        /// </summary>
        private static void DebounceLight(bool raw, ref bool stable, ref bool pending, ref DateTime pendingSince)
        {
            if (raw == stable) { pending = stable; return; }
            if (raw != pending) { pending = raw; pendingSince = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - pendingSince).TotalSeconds >= LightDebounceSeconds)
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
            OnLog?.Invoke($"✈️ Phase changed: {from} → {newPhase}", Theme.Takeoff);
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
                        OnLog?.Invoke("⚠️ PENALTY: NAV lights OFF at pushback start (-5 pts)", Theme.Warning);
                    }
                    break;

                // ── Taxi out: Nav + Taxi lights obligatorias ───────────────────────
                case FlightPhase.TaxiOut:
                    if (!_isNavOn)
                    {
                        _lightsViolationCount++;
                        OnLog?.Invoke("⚠️ PENALTY: NAV lights OFF at taxi start (-5 pts)", Theme.Warning);
                    }
                    if (!_isTaxiLightOn)
                    {
                        _lightsViolationCount++;
                        OnLog?.Invoke("⚠️ PENALTY: TAXI lights OFF during taxi (-5 pts)", Theme.Warning);
                    }
                    break;

                // ── Takeoff roll: Strobe + Landing lights obligatorias ─────────
                case FlightPhase.TakeoffRoll:
                    if (!_isStrobeOn)
                    {
                        _lightsViolationCount++;
                        OnLog?.Invoke("⚠️ PENALTY: STROBE lights OFF at takeoff roll (-5 pts)", Theme.Warning);
                    }
                    if (!_isLandingLightOn)
                    {
                        _lightsViolationCount++;
                        OnLog?.Invoke("⚠️ PENALTY: LANDING lights OFF at takeoff roll (-5 pts)", Theme.Warning);
                    }
                    // Verificar QNH antes de despegar
                    if (!string.IsNullOrEmpty(_activePlan?.Origin))
                        CheckQnhAsync(_activePlan.Origin, AircraftQnhMb).ConfigureAwait(false);
                    break;

                // ── Approach: verificar QNH de destino ────────────────────────
                case FlightPhase.Approach:
                    if (!string.IsNullOrEmpty(_activePlan?.Destination))
                        CheckQnhAsync(_activePlan.Destination, AircraftQnhMb).ConfigureAwait(false);
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
                OnLog?.Invoke($"⚠️ QNH {icao}: no se pudo obtener METAR", Theme.Warning);
                return;
            }

            double diff = Math.Abs(aircraftQnhMb - stationQnh.Value);
            string label = $"QNH | Avión: {aircraftQnhMb:F0} hPa  {icao}: {stationQnh.Value:F0} hPa  Δ{diff:F0} hPa";

            if (diff <= 2.0)
            {
                OnLog?.Invoke($"✅ {label}", Theme.Success);
            }
            else
            {
                _qnhViolationCount++;
                OnLog?.Invoke($"⚠️ PENALTY: {label} — QNH incorrecto (-5 pts)", Theme.Warning);
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
                if (success) OnLog?.Invoke($"📊 PIREP Status: {statusCode}", Theme.MainText);
            }
            catch (Exception ex) { OnLog?.Invoke($"❌ Error updating status: {ex.Message}", Theme.Danger); }
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
                    OnLog?.Invoke($"⏱️ Block Off recorded at {_serverBlockOffTime:HH:mm:ss} UTC", Theme.MainText);
                }
                else OnLog?.Invoke($"⚠️ Could not record block_off_time", Theme.Warning);
            }
            catch (Exception ex) { OnLog?.Invoke($"❌ Error recording block_off_time: {ex.Message}", Theme.Danger); }
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
                    OnLog?.Invoke($"⏱️ Block On recorded at {_serverBlockOnTime:HH:mm:ss} UTC", Theme.MainText);
                }
                else OnLog?.Invoke($"⚠️ Could not record block_on_time", Theme.Warning);
            }
            catch (Exception ex) { OnLog?.Invoke($"❌ Error recording block_on_time: {ex.Message}", Theme.Danger); }
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
            _hasLandedThisFlight = true;
            double gforce = CalculateGForce(verticalSpeed);
            // Capturar estado exacto en el momento del toque
            _touchdownPitch = _currentPitch;
            _touchdownBank = _currentBank;
            _touchdownGForce = gforce;
            OnLog?.Invoke($"✈️ Touchdown: {verticalSpeed} FPM, {gforce:F2} G, Pitch: {_currentPitch:F1}°, Bank: {_currentBank:F1}°", Theme.MainText);
            OnLandingDetected?.Invoke(verticalSpeed, gforce, _currentPitch, _currentBank);
        }
        /// <summary>
        /// Monitors overspeed and lights compliance violations while a PIREP is active.
        /// Call once per telemetry cycle, airborne only.
        /// </summary>
        private void CheckViolations(int ias, int altitudeFt, bool landingLightOn)
        {
            bool isNowOverspeed = ias > _vmoKts;
            if (isNowOverspeed && !_wasOverspeed)
            {
                _overspeedCount++;
                OnLog?.Invoke($"⚠️ OVERSPEED: {ias} kts (limit {_vmoKts} kts)", Theme.Warning);
            }
            _wasOverspeed = isNowOverspeed;

            // ── Landing lights por debajo de 10 000 ft ────────────────────────────
            bool lightsRequired = altitudeFt < 10_000;
            bool lightsViolating = lightsRequired && !landingLightOn;

            if (lightsViolating && !_lightsViolationActive)
            {
                _lightsViolationActive = true;
                _lightsViolationCount++;
                OnLog?.Invoke($"⚠️ Landing lights OFF below 10,000 ft ({altitudeFt} ft)", Theme.Warning);
            }
            else if (!lightsViolating)
            {
                _lightsViolationActive = false;
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

                // 1. Speed
                if (CurrentIndicatedAirspeed < vappMin || CurrentIndicatedAirspeed > vappMax)
                {
                    deductions += 5;
                    OnLog?.Invoke($"⚠️ APPROACH GATE: Speed {CurrentIndicatedAirspeed} kts outside [{vappMin}–{vappMax}] kts (−5)", Theme.Warning);
                }

                // 2. Descent rate: must be between -1000 and -100 fpm
                if (CurrentVerticalSpeed < -1000)
                {
                    deductions += 5;
                    OnLog?.Invoke($"⚠️ APPROACH GATE: Excessive descent {CurrentVerticalSpeed} fpm (−5)", Theme.Warning);
                }
                else if (CurrentVerticalSpeed > -100)
                {
                    deductions += 5;
                    OnLog?.Invoke($"⚠️ APPROACH GATE: Not descending ({CurrentVerticalSpeed} fpm) (−5)", Theme.Warning);
                }

                // 3. Bank angle
                if (Math.Abs(_currentBank) > 7.0)
                {
                    deductions += 3;
                    OnLog?.Invoke($"⚠️ APPROACH GATE: Bank {_currentBank:F1}° > 7° (−3)", Theme.Warning);
                }

                // 4. Pitch attitude
                if (_currentPitch < -2.5 || _currentPitch > 10.0)
                {
                    deductions += 3;
                    OnLog?.Invoke($"⚠️ APPROACH GATE: Pitch {_currentPitch:F1}° outside [−2.5°, +10°] (−3)", Theme.Warning);
                }

                // 5. Gear
                if (!IsGearDown)
                {
                    deductions += 5;
                    OnLog?.Invoke($"⚠️ APPROACH GATE: GEAR NOT DOWN (−5)", Theme.Warning);
                }

                // 6. Flap configuration (≥ 50 %)
                if (CurrentFlapsPosition < 50.0)
                {
                    deductions += 4;
                    OnLog?.Invoke($"⚠️ APPROACH GATE: Flaps {CurrentFlapsPosition:F0}% < 50% (−4)", Theme.Warning);
                }

                _stabilizedApproachDeductions = deductions;

                if (deductions == 0)
                    OnLog?.Invoke($"✅ APPROACH GATE ({(int)agl} ft AGL): STABILIZED — all criteria met", Theme.Success);
                else
                    OnLog?.Invoke($"⚠️ APPROACH GATE ({(int)agl} ft AGL): UNSTABILIZED — {deductions} pts deducted", Theme.Warning);
            }

            _prevApproachAgl = agl;
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
            CurrentFuel = 0;
            _hasLandedThisFlight = false;
            _climbStableStart = DateTime.MinValue;
            _descentStart = DateTime.MinValue;
            _stepClimbStart = DateTime.MinValue;
            _goAroundStart = DateTime.MinValue;
            _blockOffRecorded = false;
            _touchdownPitch = 0;
            _touchdownBank = 0;
            _touchdownGForce = 0;
            _overspeedCount = 0;
            _wasOverspeed = false;
            _lightsViolationCount = 0;
            _lightsViolationActive = false;
            _qnhViolationCount = 0;
            _isOfflineFlight = false;
            _approachGateEvaluated = false;
            _prevApproachAgl = double.MaxValue;
            _stabilizedApproachDeductions = 0;
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
            if (success) { OnLog?.Invoke("✖️ Flight cancelled on server", Theme.Warning); ResetFlightState(); }
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
                OnLog?.Invoke($"📊 Plan loaded. Destination: {plan.Destination} (elevation: {_destinationElevation} ft)", Theme.MainText);
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
                    OnLog?.Invoke($"📍 Airport detected: {airport}", Theme.MainText);
                    return airport;
                }
            }
            return CurrentAirport ?? "SKBO";
        }

        public bool IsPilotAtDepartureAirport(string requiredAirport) => CurrentAirport?.Equals(requiredAirport, StringComparison.OrdinalIgnoreCase) ?? false;

        public async Task<bool> StartFlight(SimbriefPlan plan, Pilot pilot, double actualFuel)
        {
            if (_apiService == null) { OnLog?.Invoke("ERROR: ApiService not configured.", Theme.Warning); return false; }
            if (actualFuel <= 0) { OnLog?.Invoke("ERROR: No fuel data from simulator.", Theme.Warning); return false; }

            _activePlan = plan;
            _activePilot = pilot;
            OnLog?.Invoke($"{_("SendingPrefile")}...", Theme.MainText);
            OnLog?.Invoke($"⛽ Using actual simulator fuel: {actualFuel:F0} kg", Theme.MainText);

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
                {
                    bool bidDeleted = await _apiService.DeleteBid(plan.BidId);
                    if (bidDeleted) OnLog?.Invoke($"✅ Bid {plan.BidId} removed", Theme.Success);
                    else OnLog?.Invoke($"⚠️ Could not remove bid {plan.BidId}", Theme.Warning);
                }

                await Task.Run(() => UpdatePirepStatus("BST"));
                OnLog?.Invoke($"⏱️ PIREP created at: {_serverCreatedAt:HH:mm:ss} UTC", Theme.MainText);
                OnLog?.Invoke($"📊 Flight timer started (server time)", Theme.Success);
                OnLog?.Invoke($"⛽ Initial fuel recorded: {_initialFuel:F0} kg", Theme.Success);
                _touchdownCaptured = false;
                // Resolver Vmo según tipo de avión del plan
                var perf = AircraftPerformanceTable.Get(_activePlan?.AircraftIcao);
                _vmoKts = perf.VmoKts;
                OnLog?.Invoke($"⚡ Aircraft type: {_activePlan?.AircraftIcao} → Vmo {_vmoKts} kts ({perf.Category})", Theme.MainText);
                TouchdownFpm = null;
                CurrentPhase = FlightPhase.Boarding;
                _phaseStartTime = DateTime.UtcNow;
                FlightStartTime = DateTime.Now;
                PhaseChanged?.Invoke(FlightPhase.Boarding);
                return true;
            }
            OnLog?.Invoke("ERROR: Server did not return a PIREP ID.", Theme.Danger);
            return false;
        }

        public void MarkOfflineFlight() => _isOfflineFlight = true;

        public bool CanStartFlight()
        {
            if (_activePilot == null) { OnLog?.Invoke("⛔ No active pilot", Theme.Warning); return false; }
            if (_activePlan == null) { OnLog?.Invoke("⛔ No flight plan loaded", Theme.Warning); return false; }
            if (!PositionValidationStatus.IcaoMatch) { OnLog?.Invoke("⛔ Assigned airport does not match flight plan", Theme.Warning); return false; }
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
            _touchdownCaptured = false;
            TouchdownFpm = null;
            _touchdownPitch = 0;
            _touchdownBank = 0;
            _touchdownGForce = 0;
            _overspeedCount = 0;
            _wasOverspeed = false;
            _lightsViolationCount = 0;
            _lightsViolationActive = false;
            _qnhViolationCount = 0;
            _isOfflineFlight = false;
            _approachGateEvaluated = false;
            _prevApproachAgl = double.MaxValue;
            _stabilizedApproachDeductions = 0;

            OnLog?.Invoke($"🔄 Flight resumed: {pirep.FlightNumber} {pirep.Origin}→{pirep.Destination}", Theme.Success);
            OnLog?.Invoke($"   PIREP ID: {pirep.Id} | Aircraft: {pirep.AircraftType} (Vmo {_vmoKts} kts)", Theme.MainText);
            OnLog?.Invoke($"   Block fuel: {pirep.BlockFuel:F0} kg | Fuel used so far: {pirep.FuelUsed:F0} kg", Theme.MainText);
            OnLog?.Invoke($"   Flight time so far: {pirep.FlightTime} min | Distance: {pirep.Distance:F1} NM", Theme.MainText);
            OnLog?.Invoke($"⚡ Aircraft type: {pirep.AircraftType} → Vmo {_vmoKts} kts ({perf.Category})", Theme.MainText);
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
                    OnLog?.Invoke($"🛫 ROTATION DETECTED! Speed: {groundSpeed} kts, Pitch: {_currentPitch:F1}°, VS: {verticalSpeed} fpm", Theme.Success);
                    OnTakeoffDetected?.Invoke(groundSpeed, altitude, verticalSpeed);
                    TransitionTo(FlightPhase.Takeoff, previousPhase);
                }
            }

            // ===== LIFTOFF REAL (momento en que la rueda deja el suelo) =====
            if (_wasOnGround && !isOnGround && CurrentPhase == FlightPhase.Takeoff)
            {
                OnLog?.Invoke($"🛫 LIFTOFF! Speed: {groundSpeed} kts, VS: {verticalSpeed} fpm", Theme.Success);
                _wasOnGround = isOnGround;
                // No cambiamos fase, seguimos en Takeoff
            }

            // ===== DETECCIÓN DE TOMACONTACTO (transición aire → suelo) =====
            if (!_wasOnGround && isOnGround && CurrentPhase != FlightPhase.AfterLanding)
            {
                RegisterTouchdown(verticalSpeed);
                TransitionTo(FlightPhase.AfterLanding, previousPhase);
                Task.Run(() => UpdatePirepStatus("LAN"));
                _wasOnGround = isOnGround;
                return;
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
                                OnLog?.Invoke($"🔄 Pushback confirmed at {groundSpeed:F1} kts", Theme.Taxi);
                                TransitionTo(FlightPhase.Pushback, previousPhase);
                                OnLog?.Invoke($"🛫 Block Off (entering Pushback)", Theme.MainText);
                                Task.Run(() => UpdateBlockOffTime());
                            }
                            else if (groundSpeed > TAXIOUT_MIN_SPEED && secondsMoving >= TAXIOUT_MIN_DURATION)
                            {
                                _pushbackStartTime = DateTime.MinValue;
                                CheckProcedureAtPhaseEntry(FlightPhase.TaxiOut);
                                OnLog?.Invoke($"🛻 Taxi out (direct) at {groundSpeed:F1} kts", Theme.Taxi);
                                TransitionTo(FlightPhase.TaxiOut, previousPhase);
                                OnLog?.Invoke($"🛫 Block Off (entering TaxiOut)", Theme.MainText);
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
                            OnLog?.Invoke($"🛻 Taxi out (after pushback) at {groundSpeed:F1} kts", Theme.Taxi);
                            TransitionTo(FlightPhase.TaxiOut, previousPhase);
                        }
                        break;

                    case FlightPhase.TaxiOut:
                        if (groundSpeed > 30 && _currentPitch < 1.0)
                        {
                            CheckProcedureAtPhaseEntry(FlightPhase.TakeoffRoll);
                            OnLog?.Invoke($"🛫 Takeoff roll started at {groundSpeed} kts", Theme.Takeoff);
                            TransitionTo(FlightPhase.TakeoffRoll, previousPhase);
                        }
                        break;

                    case FlightPhase.TakeoffRoll:
                        if (groundSpeed > 50 && _currentPitch > 2.0)
                        {
                            OnLog?.Invoke($"🛫 ROTATION at {groundSpeed} kts, Pitch: {_currentPitch:F1}°", Theme.Success);
                            OnTakeoffDetected?.Invoke(groundSpeed, altitude, verticalSpeed);
                            TransitionTo(FlightPhase.Takeoff, previousPhase);
                        }
                        else if (groundSpeed < 30)
                        {
                            OnLog?.Invoke($"🛑 Takeoff aborted, returning to taxi", Theme.Warning);
                            TransitionTo(FlightPhase.TaxiOut, previousPhase);
                        }
                        break;

                    case FlightPhase.AfterLanding:
                        if (groundSpeed < 40)
                            TransitionTo(FlightPhase.TaxiIn, previousPhase);
                        break;

                    case FlightPhase.TaxiIn:
                        if (groundSpeed < 1)
                        {
                            if (_stoppedStartTime == DateTime.MinValue)
                                _stoppedStartTime = DateTime.UtcNow;

                            if ((DateTime.UtcNow - _stoppedStartTime).TotalSeconds >= 90 &&
                                !_areEnginesOn)
                            {
                                _stoppedStartTime = DateTime.MinValue;
                                OnLog?.Invoke($"🅿️ On block", Theme.Success);
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
                                OnLog?.Invoke($"✈️ Cruise detected at {altitude} ft", Theme.MainText);
                                TransitionTo(FlightPhase.Enroute, previousPhase);
                            }
                        }
                        else
                        {
                            _climbStableStart = DateTime.MinValue;
                        }

                        // Si durante el ascenso se detecta un descenso sostenido, pasar directamente a Descent
                        if (isDescending && (DateTime.UtcNow - _phaseStartTime).TotalSeconds >= 5)
                        {
                            if (_descentStart == DateTime.MinValue)
                                _descentStart = DateTime.UtcNow;
                            else if ((DateTime.UtcNow - _descentStart).TotalSeconds >= 10)
                            {
                                _descentStart = DateTime.MinValue;
                                OnLog?.Invoke($"✈️ Descent started from {altitude} ft (direct from climb)", Theme.MainText);
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
                                OnLog?.Invoke($"✈️ Step climb detected, returning to Climb", Theme.MainText);
                                TransitionTo(FlightPhase.Climb, previousPhase);
                            }
                        }
                        else
                        {
                            _stepClimbStart = DateTime.MinValue;
                        }

                        // Detectar descenso sostenido
                        if (verticalSpeed < -300 && altitude < _maxAltitudeReached - 500)
                        {
                            if (_descentStart == DateTime.MinValue)
                                _descentStart = DateTime.UtcNow;
                            else if ((DateTime.UtcNow - _descentStart).TotalSeconds >= 10)
                            {
                                _descentStart = DateTime.MinValue;
                                OnLog?.Invoke($"✈️ Descent started from {_maxAltitudeReached} ft", Theme.MainText);
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
                                OnLog?.Invoke($"🛬 Approach started", Theme.Approach);
                                TransitionTo(FlightPhase.Approach, previousPhase);
                            }
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
                                OnLog?.Invoke($"✈️ Go-around at {(int)altitudeAboveDest} ft AGL, VS +{verticalSpeed} fpm", Theme.Warning);
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
                        // Con GS > 60 kt en el aire es inequívoco que hay un nuevo despegue.
                        if (groundSpeed > 60)
                        {
                            OnLog?.Invoke($"✈️ Touch and go detected (GS {groundSpeed} kt) — resetting for new approach", Theme.Warning);
                            _touchdownCaptured = false;
                            _approachGateEvaluated = false;
                            _prevApproachAgl = double.MaxValue;
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
            CurrentLat = data.Latitude;
            CurrentLon = data.Longitude;
            _currentPitch = data.PitchDeg;
            _currentBank = data.BankDeg;
            CurrentIndicatedAirspeed = data.IndicatedAirspeedKt > 0
                                           ? (int)data.IndicatedAirspeedKt
                                           : (int)data.GroundSpeedKt;
            CurrentFuel = data.FuelLbs * 0.453592; ;
            CurrentTransponder = data.Transponder;
            if (data.AutopilotEngaged != AutopilotEngaged)
            {
                AutopilotEngaged = data.AutopilotEngaged;
                if (AutopilotEngaged)
                    OnLog?.Invoke($"🤖 A/P ENGAGED — {data.ApNavMode}/{data.ApVertMode}", Theme.MainText);
                else
                    OnLog?.Invoke("🤖 A/P DISENGAGED", Theme.Warning);
            }
            else
            {
                AutopilotEngaged = data.AutopilotEngaged;
            }
            SimTime = DateTime.UtcNow;
            RadarAltitude = data.RadarAltitudeFeet;
            PositionOrder = data.Order;
            _isParkingBrakeSet = data.ParkingBrakeOn;

            if (data.EnginesRunning && !_areEnginesOn && !string.IsNullOrEmpty(ActivePirepId))
            {
                // Motores recién encendidos
                if (!_isBeaconOn)
                {
                    _lightsViolationCount++;
                    OnLog?.Invoke("⚠️ PENALTY: BEACON OFF at engine start (-5 pts)", Theme.Warning);
                }
            }

            _areEnginesOn = data.EnginesRunning;

            // Sistemas
            IsGearDown = data.GearDown;
            CurrentFlapsPosition = data.FlapsPercent;
            FlapsLabel = data.FlapsLabel;
            AreSpoilersDeployed = data.SpoilersDeployed;
            AutobrakeSetting = data.AutobrakeSetting;

            // Luces (con debounce para filtrar parpadeos por cambio de fuente de energía)
            DebounceLight(data.NavLightOn,     ref _isNavOn,         ref _pendingNavOn,     ref _navPending);
            DebounceLight(data.BeaconLightOn,  ref _isBeaconOn,      ref _pendingBeaconOn,  ref _beaconPending);
            DebounceLight(data.LandingLightOn, ref _isLandingLightOn, ref _pendingLandingOn, ref _landingPending);
            DebounceLight(data.TaxiLightOn,    ref _isTaxiLightOn,   ref _pendingTaxiOn,    ref _taxiPending);
            DebounceLight(data.StrobeLightOn,  ref _isStrobeOn,      ref _pendingStrobeOn,  ref _strobePending);
            IsNavLightOn     = _isNavOn;
            IsBeaconLightOn  = _isBeaconOn;
            IsLandingLightOn = _isLandingLightOn;
            IsTaxiLightOn    = _isTaxiLightOn;
            IsStrobeLightOn  = _isStrobeOn;
            IsSeatBeltSignOn = data.SeatBeltSign;
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
                    CheckViolations(CurrentIndicatedAirspeed, CurrentAltitude, data.LandingLightOn);
                    if (CurrentPhase == FlightPhase.Approach)
                        CheckStabilizedApproachGate(data);
                }
            }
        }
        public async Task<bool> AbortFlight()
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return false;
            bool success = await _apiService.DeletePirep(ActivePirepId);
            if (success) { OnLog?.Invoke("✖️ Flight aborted on server", Theme.Warning); ResetFlightState(); }
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

            OnLog?.Invoke($"📊 Planned Distance: {plannedDistance:F1} NM", Theme.MainText);
            OnLog?.Invoke($"📊 Planned Flight Time: {plannedFlightTimeMinutes} min", Theme.MainText);
            OnLog?.Invoke($"📊 Actual Distance: {totalDistanceNm:F1} NM", Theme.MainText);
            OnLog?.Invoke($"📊 Actual Flight Time: {actualFlightTimeMinutes} min", Theme.MainText);
            OnLog?.Invoke($"⛽ Fuel Used: {fuelUsed:F0} kg", Theme.MainText);
            // ── Calcular score ────────────────────────────────────────────────────────
            var scoreData = new FlightScoreData
            {
                LandingRate = (int)(TouchdownFpm ?? 0),
                LandingPitch = _touchdownPitch,
                LandingBank = _touchdownBank,
                LandingGForce = _touchdownGForce,
                OverspeedCount = _overspeedCount,
                LightsViolations = _lightsViolationCount,
                StabilizedApproachDeductions = _stabilizedApproachDeductions,
                QnhViolations = _qnhViolationCount,
                WasOfflineFlight = _isOfflineFlight
            };
            var scoring = new ScoringService();
            ScoringResult scoreResult = scoring.Calculate(scoreData);
            OnLog?.Invoke($"🏆 Score: {scoreResult.TotalScore}/100 — {scoreResult.LandingRating}", Theme.Success);
            foreach (var ded in scoreResult.Deductions)
                OnLog?.Invoke($"   −{ded.PointsDeducted} pts: {ded.Criterion} ({ded.Reason})", Theme.Warning);

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
                OnLog?.Invoke($"✅ PIREP filed successfully", Theme.Success);
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
            catch (Exception ex) { OnLog?.Invoke($"❌ Error updating flight progress: {ex.Message}", Theme.Danger); }
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