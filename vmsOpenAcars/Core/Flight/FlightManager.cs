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

        // Control de fases
        private DateTime _phaseStartTime = DateTime.UtcNow;
        private FlightPhase _lastStablePhase;
        private DateTime _pushbackStartTime = DateTime.MinValue;
        private DateTime _stoppedStartTime = DateTime.MinValue;

        // Constantes para transiciones
        private const double PUSHBACK_MAX_SPEED = 5.0;
        private const int PUSHBACK_MIN_DURATION = 8;
        private const double TAXIOUT_MIN_SPEED = 5.0;
        private const int TAXIOUT_MIN_DURATION = 2;

        // Optimización
        private int _lastFlightTimeMinutesLogged = -1;
        private double _lastDistanceLogged = -1;

        #region Properties

        public string CurrentAirport
        {
            get => _currentAirport;
            private set { _currentAirport = value; OnAirportChanged?.Invoke(value); }
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
        public DateTime FlightStartTime { get; private set; }
        public Pilot ActivePilot => _activePilot;
        public SimbriefPlan ActivePlan => _activePlan;
        public bool IsSimulatorConnected { get; private set; }
        public ValidationStatus PositionValidationStatus { get; private set; }
        public double CurrentFuelFlow { get; private set; }
        public int CurrentTransponder { get; private set; }
        public bool AutopilotEngaged { get; private set; }
        public DateTime SimTime { get; private set; }
        public double RadarAltitude { get; private set; }
        public int PositionOrder { get; private set; }
        public double InitialFuel => _initialFuel;
        public double TotalFuelUsed => _totalFuelUsed;
        public bool IsBlockOffRecorded => _blockOffRecorded;
        public bool IsTimerStarted => _isTimerStarted;
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

        public FlightManager(ApiService apiService)
        {
            _apiService = apiService;
            _positionValidator = new PositionValidator();
            CurrentPhase = FlightPhase.Idle;
            PositionValidationStatus = new ValidationStatus();
        }

        #region Private Methods

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
            double gforce = CalculateGForce(verticalSpeed);
            OnLog?.Invoke($"✈️ Touchdown: {verticalSpeed} FPM, {gforce:F2} G", Theme.MainText);
            OnLandingDetected?.Invoke(verticalSpeed, gforce, _currentPitch, _currentBank);
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
            OnPhaseChanged?.Invoke(CurrentPhase.ToString());
            PhaseChanged?.Invoke(CurrentPhase);
        }

        #endregion

        #region Public Methods

        public async Task RecordBlockOff()
        {
            await UpdateBlockOffTime();
        }

        public double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }



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

            // ELIMINAR ESTE BLOQUE DE LOG DE DIFERENCIA
            // double diff = actualFuel - plannedFuel;
            // if (Math.Abs(diff) > 0) OnLog?.Invoke($"ℹ️ Fuel difference from plan: {(diff > 0 ? "+" : "")}{diff:F0} {plan.Units ?? "kg"}", Theme.MainText);

            var result = await _apiService.PrefileFlight(plan, pilot);
            ActivePirepId = result.pirepId;
            if (!string.IsNullOrEmpty(ActivePirepId))
            {
                _initialFuel = actualFuel;
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
                TouchdownFpm = null;
                CurrentPhase = FlightPhase.Boarding;
                _phaseStartTime = DateTime.UtcNow;
                FlightStartTime = DateTime.Now;
                return true;
            }
            OnLog?.Invoke("ERROR: Server did not return a PIREP ID.", Theme.Danger);
            return false;
        }

        public bool CanStartFlight()
        {
            if (_activePilot == null) { OnLog?.Invoke("⛔ No active pilot", Theme.Warning); return false; }
            if (_activePlan == null) { OnLog?.Invoke("⛔ No flight plan loaded", Theme.Warning); return false; }
            if (!PositionValidationStatus.IcaoMatch) { OnLog?.Invoke("⛔ Assigned airport does not match flight plan", Theme.Warning); return false; }
            return true;
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
            // Detectar cuando el pitch supera 2 grados Y hay velocidad suficiente Y estamos en tierra
            if (_wasOnGround && groundSpeed > 60 && _currentPitch > 2.0 && _currentPitch < 20)
            {
                if (CurrentPhase != FlightPhase.Takeoff && CurrentPhase != FlightPhase.TakeoffRoll)
                {
                    CurrentPhase = FlightPhase.Takeoff;
                    _phaseStartTime = DateTime.UtcNow;
                    OnLog?.Invoke($"🛫 ROTATION DETECTED! Speed: {groundSpeed} kts, Pitch: {_currentPitch:F1}°, VS: {verticalSpeed} fpm", Theme.Success);
                    OnTakeoffDetected?.Invoke(groundSpeed, altitude, verticalSpeed);
                    Task.Run(() => UpdatePirepStatus("TOF"));
                    OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                    PhaseChanged?.Invoke(CurrentPhase);
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
            if (!_wasOnGround && isOnGround)
            {
                RegisterTouchdown(verticalSpeed);
                CurrentPhase = FlightPhase.AfterLanding;
                _phaseStartTime = DateTime.UtcNow;
                Task.Run(() => UpdatePirepStatus("LAN"));
                OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                PhaseChanged?.Invoke(CurrentPhase);
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

                            // Movimiento lento y sostenido = pushback
                            if (groundSpeed <= PUSHBACK_MAX_SPEED && secondsMoving >= PUSHBACK_MIN_DURATION)
                            {
                                CurrentPhase = FlightPhase.Pushback;
                                _phaseStartTime = DateTime.UtcNow;
                                _pushbackStartTime = DateTime.MinValue;
                                OnLog?.Invoke($"🔄 Pushback confirmed at {groundSpeed:F1} kts", Theme.Taxi);
                                OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                                PhaseChanged?.Invoke(CurrentPhase);
                            }
                            // Movimiento rápido y sostenido = taxiout directo
                            else if (groundSpeed > TAXIOUT_MIN_SPEED && secondsMoving >= TAXIOUT_MIN_DURATION)
                            {
                                CurrentPhase = FlightPhase.TaxiOut;
                                _phaseStartTime = DateTime.UtcNow;
                                _pushbackStartTime = DateTime.MinValue;
                                OnLog?.Invoke($"🛻 Taxi out (direct) at {groundSpeed:F1} kts", Theme.Taxi);
                                OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                                PhaseChanged?.Invoke(CurrentPhase);
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
                            CurrentPhase = FlightPhase.TaxiOut;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"🛻 Taxi out (after pushback) at {groundSpeed:F1} kts", Theme.Taxi);
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        break;

                    case FlightPhase.TaxiOut:
                        // Inicio de la carrera de despegue
                        if (groundSpeed > 30 && _currentPitch < 1.0)
                        {
                            CurrentPhase = FlightPhase.TakeoffRoll;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"🛫 Takeoff roll started at {groundSpeed} kts", Theme.Takeoff);
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        break;

                    case FlightPhase.TakeoffRoll:
                        if (groundSpeed > 50 && _currentPitch > 2.0)
                        {
                            CurrentPhase = FlightPhase.Takeoff;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"🛫 ROTATION at {groundSpeed} kts, Pitch: {_currentPitch:F1}°", Theme.Success);
                            OnTakeoffDetected?.Invoke(groundSpeed, altitude, verticalSpeed);
                            Task.Run(() => UpdatePirepStatus("TOF"));
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        else if (groundSpeed < 30)
                        {
                            CurrentPhase = FlightPhase.TaxiOut;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"🛑 Takeoff aborted, returning to taxi", Theme.Warning);
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        break;

                    case FlightPhase.AfterLanding:
                        if (groundSpeed < 40)
                        {
                            CurrentPhase = FlightPhase.TaxiIn;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        break;

                    case FlightPhase.TaxiIn:
                        if (groundSpeed < 1)
                        {
                            if (_stoppedStartTime == DateTime.MinValue)
                                _stoppedStartTime = DateTime.UtcNow;

                            if ((DateTime.UtcNow - _stoppedStartTime).TotalSeconds >= 15)
                            {
                                CurrentPhase = FlightPhase.OnBlock;
                                _phaseStartTime = DateTime.UtcNow;
                                _stoppedStartTime = DateTime.MinValue;
                                OnLog?.Invoke($"🅿️ On block", Theme.Success);
                                OnBlockDetected?.Invoke();
                                Task.Run(() => UpdateBlockOnTime());
                                OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                                PhaseChanged?.Invoke(CurrentPhase);
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
                        // Salir de Takeoff cuando está en el aire Y ascendiendo
                        if (!isOnGround && (isClimbing || verticalSpeed > 0))
                        {
                            CurrentPhase = FlightPhase.Climb;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        break;

                    case FlightPhase.Climb:
                        // Si estamos cerca del crucero o lo hemos superado, o la tasa de ascenso es muy baja
                        if (Math.Abs(altitude - cruiseAlt) < 2000 || altitude >= cruiseAlt || verticalSpeed < 300)
                        {
                            CurrentPhase = FlightPhase.Enroute;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"✈️ Cruise detected at {altitude} ft", Theme.MainText);
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        else if (isDescending && altitude < cruiseAlt * 0.9)
                        {
                            CurrentPhase = FlightPhase.Descent;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"✈️ Early descent at {altitude} ft", Theme.MainText);
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        break;

                    case FlightPhase.Enroute:
                        if (isDescending && (altitude < cruiseAlt * 0.9 || altitude < _maxAltitudeReached - 1000) && canChangePhase)
                        {
                            CurrentPhase = FlightPhase.Descent;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"✈️ Descent started from {_maxAltitudeReached} ft", Theme.MainText);
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        break;

                    case FlightPhase.Descent:
                        if ((distanceToDestination > 0 && distanceToDestination < approachDistanceThreshold) ||
                            (altitudeAboveDest < aglThreshold && altitudeAboveDest > 0))
                        {
                            if (canChangePhase)
                            {
                                CurrentPhase = FlightPhase.Approach;
                                _phaseStartTime = DateTime.UtcNow;
                                OnLog?.Invoke($"🛬 Approach started", Theme.Approach);
                                OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                                PhaseChanged?.Invoke(CurrentPhase);
                            }
                        }
                        break;

                    case FlightPhase.Approach:
                        if (isClimbing && altitudeAboveDest < 10000 && altitudeAboveDest > 500 && canChangePhase)
                        {
                            CurrentPhase = FlightPhase.Climb;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"✈️ Go-around detected!", Theme.Warning);
                            OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                            PhaseChanged?.Invoke(CurrentPhase);
                        }
                        break;
                }
            }

            // ===== REGISTRAR BLOCK OFF AL ENTRAR A TAXIOUT =====
            if (CurrentPhase == FlightPhase.TaxiOut && !_blockOffRecorded && _isTimerStarted)
            {
                OnLog?.Invoke($"🛫 Block Off (entering TaxiOut)", Theme.MainText);
                Task.Run(() => UpdateBlockOffTime());
            }

            _wasOnGround = isOnGround;

            // ===== ACTUALIZAR ESTADO EN EL SERVIDOR (solo si cambió la fase) =====
            if (previousPhase != CurrentPhase)
            {
                Task.Run(() => UpdatePirepStatus(FlightPhaseHelper.GetStatusCode(CurrentPhase)));
            }
        }
        public void UpdateTelemetry(int altitude, int groundSpeed, int verticalSpeed, bool isOnGround, double fuel,
            double lat, double lon, double indicatedAirspeed = 0, double fuelFlow = 0, int transponder = 1200,
            bool autopilot = false, DateTime simTime = default, double radarAlt = 0, int order = 0,
            double pitch = 0, double bank = 0)
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return;

            CurrentAltitude = altitude;
            CurrentGroundSpeed = groundSpeed;
            CurrentVerticalSpeed = verticalSpeed;
            CurrentFuel = fuel;
            IsOnGround = isOnGround;
            CurrentLat = lat;
            CurrentLon = lon;
            _currentPitch = pitch;
            _currentBank = bank;
            CurrentIndicatedAirspeed = indicatedAirspeed > 0 ? (int)indicatedAirspeed : groundSpeed;
            CurrentFuelFlow = fuelFlow;
            CurrentTransponder = transponder;
            AutopilotEngaged = autopilot;
            SimTime = simTime == default ? DateTime.UtcNow : simTime;
            RadarAltitude = radarAlt;
            PositionOrder = order;

            if (_lastPosition.HasValue && _lastPositionTime.HasValue)
            {
                double distKm = CalculateDistanceKm(_lastPosition.Value.lat, _lastPosition.Value.lon, lat, lon);
                if (distKm > 0 && distKm < 10) _totalDistanceKm += distKm;
            }
            _lastPosition = (lat, lon);
            _lastPositionTime = DateTime.UtcNow;
            if (!isOnGround && !_lastAirborneTime.HasValue) _lastAirborneTime = DateTime.UtcNow;

            UpdatePhase(altitude, groundSpeed, isOnGround, verticalSpeed);
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

            double fuelUsed = 0;
            if (_activePlan?.BlockFuel > 0 && CurrentFuel > 0)
                fuelUsed = _activePlan.BlockFuel - CurrentFuel;
            if (fuelUsed <= 0 && _totalFuelUsed > 0) fuelUsed = _totalFuelUsed;
            if (fuelUsed < 0) fuelUsed = 0;

            double totalDistanceNm = _totalDistanceKm * 0.539957;
            double plannedDistance = _activePlan?.Distance ?? 0;
            int plannedFlightTimeMinutes = (_activePlan?.EstTimeEnroute ?? 0) / 60;
            double blockFuel = _activePlan?.BlockFuel ?? 0;

            OnLog?.Invoke($"📊 Planned Distance: {plannedDistance:F1} NM", Theme.MainText);
            OnLog?.Invoke($"📊 Planned Flight Time: {plannedFlightTimeMinutes} min", Theme.MainText);
            OnLog?.Invoke($"📊 Actual Distance: {totalDistanceNm:F1} NM", Theme.MainText);
            OnLog?.Invoke($"📊 Actual Flight Time: {actualFlightTimeMinutes} min", Theme.MainText);
            OnLog?.Invoke($"⛽ Fuel Used: {fuelUsed:F0} {_activePlan?.Units ?? "kg"}", Theme.MainText);

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