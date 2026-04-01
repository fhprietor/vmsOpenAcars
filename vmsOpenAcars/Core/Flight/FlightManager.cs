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
    /// <summary>
    /// Manages the core flight logic, including phase transitions, telemetry processing,
    /// and communication with the phpVMS API for PIREP management.
    /// </summary>
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
        private double _totalDistance = 0;
        private DateTime? _lastAirborneTime = null;
        private DateTime? _lastPositionTime = null;
        private (double lat, double lon)? _lastPosition = null;

        // Cronómetro real basado en Stopwatch
        private readonly FlightTimer _flightTimer = new FlightTimer();

        // Variables para optimización
        private int _lastFlightTimeMinutesLogged = -1;
        private double _lastDistanceLogged = -1;
        // Variables para el cronómetro basado en servidor
        private DateTime _serverCreatedAt;      // Momento de creación del PIREP
        private DateTime _serverBlockOffTime;   // Block off (si se registra)
        private DateTime _serverBlockOnTime;    // Block on (si se registra)
        private bool _isTimerStarted = false;

        // ===== VARIABLES DE COMBUSTIBLE =====
        private bool _blockOffRecorded = false;  // Flag para evitar múltiples registros de block_off
        private double _initialFuel = 0;         // Combustible al inicio del vuelo (kg/lbs)
        private double _lastFuelUpdate = 0;      // Último valor de combustible registrado
        private double _totalFuelUsed = 0;       // Combustible total consumido

        // Variables para control de histéresis
        private DateTime _phaseStartTime = DateTime.UtcNow;
        private FlightPhase _lastStablePhase;

        // Variables para pushback
        private DateTime _pushbackStartTime = DateTime.MinValue;
        private const double PUSHBACK_MIN_SPEED = 0.5;
        private const double PUSHBACK_MAX_SPEED = 4.0;
        private const int PUSHBACK_MIN_DURATION = 5;

        private DateTime _stoppedStartTime = DateTime.MinValue; // Para detectar inmovilidad sostenida

        #region Properties

        public string CurrentAirport
        {
            get => _currentAirport;
            private set
            {
                _currentAirport = value;
                OnAirportChanged?.Invoke(value);
            }
        }

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
        // ===== PROPIEDADES DE COMBUSTIBLE =====
        public double InitialFuel => _initialFuel;
        public double TotalFuelUsed => _totalFuelUsed;
        public bool IsBlockOffRecorded => _blockOffRecorded;

        #endregion

        #region Events

        public event Action<string> OnPhaseChanged;
        public event Action<FlightPhase> PhaseChanged;
        public event Action<string> OnAirportChanged;
        public event Action<string, Color> OnLog;
        public event Action<ValidationStatus> OnPositionValidated;

        #endregion

        public FlightManager(ApiService apiService)
        {
            _apiService = apiService;
            _positionValidator = new PositionValidator();
            CurrentPhase = FlightPhase.Idle;
            _lastStablePhase = FlightPhase.Idle;
            PositionValidationStatus = new ValidationStatus();
        }

        #region Private Methods

        private async Task UpdatePirepStatus(string statusCode)
        {
            if (string.IsNullOrEmpty(ActivePirepId))
                return;

            try
            {
                var updateData = new { status = statusCode };
                bool success = await _apiService.UpdatePirep(ActivePirepId, updateData);

                if (success)
                {
                    OnLog?.Invoke($"📊 PIREP Status: {statusCode}", Theme.MainText);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error updating status: {ex.Message}", Theme.Danger);
            }
        }

        private async Task UpdateBlockOffTime()
        {
            if (string.IsNullOrEmpty(ActivePirepId))
                return;

            try
            {
                // Obtener hora actual UTC (la usamos para calcular tiempo local)
                // Pero enviamos al servidor para que registre su propia hora
                var payload = new { block_off_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") };
                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _apiService.HttpClient.PutAsync(
                    $"{_apiService.BaseUrl}api/pireps/{ActivePirepId}", content);

                if (response.IsSuccessStatusCode)
                {
                    _blockOffRecorded = true;
                    // Guardamos la hora local como referencia para cálculos
                    _serverBlockOffTime = DateTime.UtcNow;
                    OnLog?.Invoke($"⏱️ Block Off recorded at {_serverBlockOffTime:HH:mm:ss} UTC", Theme.MainText);
                    OnLog?.Invoke($"📊 Timer reference updated: {GetFlightTimeDisplay()}", Theme.MainText);
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error setting block_off_time: {error}");
                    OnLog?.Invoke($"⚠️ Could not record block_off_time", Theme.Warning);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error recording block_off_time: {ex.Message}", Theme.Danger);
            }
        }


        private void ValidateAirportMatch()
        {
            if (_activePilot == null || _activePlan == null) return;

            bool match = _positionValidator.CompareIcaoCodes(
                _activePilot.CurrentAirport,
                _activePlan.Origin
            );

            PositionValidationStatus.IcaoMatch = match;
            PositionValidationStatus.PhpVmsAirport = _activePilot.CurrentAirport;
            PositionValidationStatus.SimbriefAirport = _activePlan.Origin;

            if (match)
            {
                OnLog?.Invoke($"{_("DepartureAirportOk")} {_activePlan.Origin}", Theme.MainText);
            }
            else
            {
                OnLog?.Invoke($"{_("Warning")}: {_("YouAreAssigned")} {_activePilot.CurrentAirport}, " +
                             $"{_("ButFlightDepartureIs")} {_activePlan.Origin}", Theme.Warning);
            }

            OnPositionValidated?.Invoke(PositionValidationStatus);
        }

        private void ValidateSimulatorPosition(double currentLat, double currentLon)
        {
            if (_activePilot == null) return;

            var (isValid, distance, message, color) = _positionValidator.ValidatePosition(
                _activePilot.CurrentAirport,
                _activePilot.CurrentAirportLat,
                _activePilot.CurrentAirportLon,
                currentLat,
                currentLon
            );

            bool gpsChanged = (PositionValidationStatus.GpsValid != isValid) ||
                              (Math.Abs(PositionValidationStatus.DistanceFromAirport - distance) > 0.01);

            PositionValidationStatus.GpsValid = isValid;
            PositionValidationStatus.DistanceFromAirport = distance;

            if (gpsChanged)
            {
                OnLog?.Invoke(message, color);
            }

            OnPositionValidated?.Invoke(PositionValidationStatus);
        }

        private void RegisterTouchdown(int verticalSpeed)
        {
            if (_touchdownCaptured)
                return;

            TouchdownFpm = verticalSpeed;
            _touchdownCaptured = true;
            OnLog?.Invoke($"✈️ Touchdown: {verticalSpeed} FPM", Theme.MainText);
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 3440.065; // Radio de la Tierra en millas náuticas (NM)
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;  // Devuelve en NM
        }

        private void ResetFlightState()
        {
            ActivePirepId = "";
            _activePlan = null;
            CurrentPhase = FlightPhase.Idle;
            TouchdownFpm = null;
            _totalDistance = 0;
            _lastAirborneTime = null;
            _lastPosition = null;
            _lastPositionTime = null;
            _isTimerStarted = false;
            _serverCreatedAt = default;
            _serverBlockOffTime = default;
            _serverBlockOnTime = default;
            _blockOffRecorded = false;
            _initialFuel = 0;
            _lastFuelUpdate = 0;
            _totalFuelUsed = 0;
            CurrentFuel = 0;
            CurrentFuelFlow = 0;
            OnPhaseChanged?.Invoke(CurrentPhase.ToString());
            PhaseChanged?.Invoke(CurrentPhase);
        }

        #endregion

        #region Public Methods
        // Propiedad pública para mostrar tiempo actual (para UI)
        public string CurrentTimerDisplay
        {
            get
            {
                if (!_isTimerStarted) return "00:00:00";

                TimeSpan elapsed = DateTime.UtcNow - _serverCreatedAt;
                return $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        public string CurrentFlightTimerDisplay
        {
            get
            {
                if (!_isTimerStarted) return "00:00:00";

                DateTime reference = _serverBlockOffTime != default ? _serverBlockOffTime : _serverCreatedAt;
                TimeSpan elapsed = DateTime.UtcNow - reference;
                return $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        // Propiedad para obtener el tiempo transcurrido desde created_at
        public TimeSpan CurrentFlightTime => _isTimerStarted
            ? DateTime.UtcNow - _serverCreatedAt
            : TimeSpan.Zero;

        // Propiedad para tiempo desde block_off (más preciso para vuelo real)
        public TimeSpan ActualFlightTime => (_serverBlockOffTime != default && _serverBlockOffTime > _serverCreatedAt)
            ? DateTime.UtcNow - _serverBlockOffTime
            : CurrentFlightTime;

        public async Task<bool> CancelFlight()
        {
            try
            {
                if (string.IsNullOrEmpty(ActivePirepId))
                    return false;

                bool success = await _apiService.DeletePirep(ActivePirepId);
                if (success)
                {
                    OnLog?.Invoke("✖️ Flight cancelled on server", Theme.Warning);
                    ResetFlightState();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error cancelling flight: {ex.Message}", Theme.Danger);
                return false;
            }
        }

        public void SetActivePilot(Pilot pilot)
        {
            _activePilot = pilot;
            CurrentAirport = pilot?.CurrentAirport ?? "";

            if (_activePlan != null)
            {
                ValidateAirportMatch();
            }
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

            if (CurrentLat != 0 && CurrentLon != 0)
            {
                UpdatePositionValidation(CurrentLat, CurrentLon);
            }
        }

        public void SetSimulatorConnected(bool connected, double? latitude = null, double? longitude = null)
        {
            System.Diagnostics.Debug.WriteLine($"FlightManager.SetSimulatorConnected({connected}, {latitude}, {longitude})");
            IsSimulatorConnected = connected;

            if (connected && latitude.HasValue && longitude.HasValue)
            {
                if (_activePilot != null)
                {
                    if (_activePilot.CurrentAirportLat.HasValue && _activePilot.CurrentAirportLon.HasValue)
                    {
                        ValidateSimulatorPosition(latitude.Value, longitude.Value);
                    }
                    else
                    {
                        PositionValidationStatus.GpsValid = false;
                        PositionValidationStatus.DistanceFromAirport = 0;
                        OnLog?.Invoke("⚠️ No airport coordinates available for position validation", Theme.Warning);
                        OnPositionValidated?.Invoke(PositionValidationStatus);
                    }
                }
            }
            else
            {
                PositionValidationStatus.GpsValid = false;
                PositionValidationStatus.DistanceFromAirport = 0;
                OnPositionValidated?.Invoke(PositionValidationStatus);
            }

            if (connected)
            {
                OnLog?.Invoke(_("SimConnected"), Theme.MainText);
            }
            else
            {
                OnLog?.Invoke(_("SimDisconnected"), Theme.Warning);
            }
        }

        public void UpdatePositionValidation(double lat, double lon)
        {
            if (_activePilot != null && IsSimulatorConnected)
            {
                ValidateSimulatorPosition(lat, lon);
            }
        }

        public async Task<string> DetectNearestAirport(double latitude, double longitude)
        {
            try
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

                OnLog?.Invoke("⚠️ Could not detect airport via API", Theme.Warning);
                return CurrentAirport ?? "SKBO";
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Error detecting airport: {ex.Message}", Theme.Danger);
                return CurrentAirport ?? "SKBO";
            }
        }

        public bool IsPilotAtDepartureAirport(string requiredAirport)
        {
            return CurrentAirport?.Equals(requiredAirport, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        // En FlightManager.cs

        // En FlightManager.cs - StartFlight() corregido

        public async Task<bool> StartFlight(SimbriefPlan plan, Pilot pilot, double actualFuel)
        {
            try
            {
                if (_apiService == null)
                {
                    OnLog?.Invoke("ERROR: ApiService not configured.", Theme.Warning);
                    return false;
                }

                // Validar que tenemos combustible real
                if (actualFuel <= 0)
                {
                    OnLog?.Invoke("ERROR: No fuel data from simulator.", Theme.Warning);
                    return false;
                }

                _activePlan = plan;
                _activePilot = pilot;

                OnLog?.Invoke($"{_("SendingPrefile")}...", Theme.MainText);
                OnLog?.Invoke($"⛽ Using actual simulator fuel: {actualFuel:F0} {plan.Units ?? "kg"}", Theme.MainText);

                // Guardar el combustible planeado original antes de modificarlo
                double plannedFuel = plan.BlockFuel;

                // ACTUALIZAR EL PLAN CON EL COMBUSTIBLE REAL
                plan.BlockFuel = actualFuel;

                // Obtener PIREP ID y created_at del servidor
                var result = await _apiService.PrefileFlight(plan, pilot);
                string pirepId = result.pirepId;
                DateTime serverCreatedAt = result.serverCreatedAt;

                ActivePirepId = pirepId;

                if (!string.IsNullOrEmpty(ActivePirepId))
                {
                    // REGISTRAR COMBUSTIBLE INICIAL
                    _initialFuel = actualFuel;
                    _lastFuelUpdate = actualFuel;
                    _totalFuelUsed = 0;

                    _serverCreatedAt = serverCreatedAt;
                    _isTimerStarted = true;
                    _serverBlockOffTime = default;
                    _serverBlockOnTime = default;
                    _blockOffRecorded = false;

                    OnLog?.Invoke($"⏱️ PIREP created at: {_serverCreatedAt:HH:mm:ss} UTC", Theme.MainText);
                    OnLog?.Invoke($"📊 Flight timer started (server time)", Theme.Success);
                    OnLog?.Invoke($"⛽ Initial fuel recorded: {_initialFuel:F0} {plan.Units ?? "kg"}", Theme.Success);

                    // Mostrar diferencia con el plan si es significativa
                    double diff = actualFuel - plannedFuel;
                    if (Math.Abs(diff) > 0)
                    {
                        string diffSymbol = diff > 0 ? "+" : "";
                        OnLog?.Invoke($"ℹ️ Fuel difference from plan: {diffSymbol}{diff:F0} {plan.Units ?? "kg"}", Theme.MainText);
                    }

                    if (!string.IsNullOrEmpty(plan.BidId))
                    {
                        bool bidDeleted = await _apiService.DeleteBid(plan.BidId);
                        if (bidDeleted)
                            OnLog?.Invoke($"✅ Bid {plan.BidId} removed", Theme.Success);
                        else
                            OnLog?.Invoke($"⚠️ Could not remove bid {plan.BidId}", Theme.Warning);
                    }

                    await Task.Run(() => UpdatePirepStatus("BST"));

                    _touchdownCaptured = false;
                    TouchdownFpm = null;
                    CurrentPhase = FlightPhase.Boarding;
                    _phaseStartTime = DateTime.UtcNow;
                    _lastStablePhase = FlightPhase.Boarding;
                    FlightStartTime = DateTime.Now;
                    return true;
                }

                OnLog?.Invoke("ERROR: Server did not return a PIREP ID.", Theme.Danger);
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ {ex.Message}", Theme.Warning);
                return false;
            }
        }

        public bool CanStartFlight()
        {
            if (_activePilot == null)
            {
                OnLog?.Invoke("⛔ No active pilot", Theme.Warning);
                return false;
            }

            if (_activePlan == null)
            {
                OnLog?.Invoke("⛔ No flight plan loaded", Theme.Warning);
                return false;
            }

            if (!PositionValidationStatus.IcaoMatch)
            {
                OnLog?.Invoke("⛔ Assigned airport does not match flight plan", Theme.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Updates the flight phase based on current telemetry using relative thresholds.
        /// </summary>
        public void UpdatePhase(int altitude, int groundSpeed, bool isOnGround, int verticalSpeed, double distanceToDestination = -1)
        {
            var previousPhase = CurrentPhase;

            if (altitude > _maxAltitudeReached)
                _maxAltitudeReached = altitude;

            // Obtener referencias del plan
            int cruiseAlt = _activePlan?.CruiseAltitude ?? 10000;
            double totalDistance = _activePlan?.Distance ?? 100;
            double destElev = _destinationElevation;

            // Umbrales relativos
            double approachDistanceThreshold = Math.Min(totalDistance * 0.1, 20);
            double aglThreshold = Math.Min(5000, cruiseAlt * 0.2);
            double descentThreshold = cruiseAlt * 0.8;

            // Detección de tendencia
            bool isClimbing = verticalSpeed > 100;
            bool isDescending = verticalSpeed < -100;
            bool isStable = Math.Abs(verticalSpeed) <= 100;

            // Umbral de movimiento
            int movingThreshold = _activePlan?.Aircraft?.StartsWith("B") == true ? 10 : 5;

            // Prevenir oscilaciones: no cambiar de fase si ha pasado poco tiempo
            bool canChangePhase = (DateTime.UtcNow - _phaseStartTime).TotalSeconds >= 5;

            // ===== TOUCHDOWN DETECTION =====
            if (_wasOnGround == false && isOnGround == true)
            {
                RegisterTouchdown(verticalSpeed);
                CurrentPhase = FlightPhase.AfterLanding;
                _phaseStartTime = DateTime.UtcNow;
                _lastStablePhase = FlightPhase.AfterLanding;
                Task.Run(() => UpdatePirepStatus("LAN"));
                _wasOnGround = isOnGround;
                return;
            }

            if (isOnGround)
            {
                // ===== FASES EN TIERRA MEJORADAS =====
                switch (CurrentPhase)
                {
                    case FlightPhase.Boarding:
                        if (groundSpeed > 0.5)
                        {
                            // Guardamos cuándo empezó el movimiento
                            if (_pushbackStartTime == DateTime.MinValue)
                                _pushbackStartTime = DateTime.UtcNow;

                            // Si la velocidad es > 5kt durante 3 segundos, TaxiOut directo
                            if (groundSpeed > 5.0 && (DateTime.UtcNow - _pushbackStartTime).TotalSeconds >= 3)
                            {
                                CurrentPhase = FlightPhase.TaxiOut;
                                _phaseStartTime = DateTime.UtcNow;
                                _pushbackStartTime = DateTime.MinValue;
                                OnLog?.Invoke($"🛻 Taxi out at {groundSpeed:F1} kts", Theme.Taxi);
                            }
                            // Velocidad baja - posible pushback
                            else if (groundSpeed <= PUSHBACK_MAX_SPEED)
                            {
                                // Aún no cambiamos, esperamos a ver si es pushback
                            }
                        }
                        else
                        {
                            // Reseteamos si se detiene
                            _pushbackStartTime = DateTime.MinValue;
                        }
                        break;

                    case FlightPhase.Pushback:
                        if (groundSpeed > PUSHBACK_MAX_SPEED)
                        {
                            CurrentPhase = FlightPhase.TaxiOut;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"🛻 Pushback complete, taxi at {groundSpeed:F1} kts", Theme.Taxi);
                        }
                        break;

                    case FlightPhase.TaxiOut:
                        if (groundSpeed > 50)
                        {
                            CurrentPhase = FlightPhase.Takeoff;
                            _phaseStartTime = DateTime.UtcNow;
                        }
                        break;

                    case FlightPhase.AfterLanding:
                        if (groundSpeed < 40)
                        {
                            CurrentPhase = FlightPhase.TaxiIn;
                            _phaseStartTime = DateTime.UtcNow;
                        }
                        break;

                    case FlightPhase.TaxiIn:
                        // Si estamos parados durante 15 segundos
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

                                // Registrar block_on_time en el servidor
                                Task.Run(() => UpdateBlockOnTime());
                            }
                        }
                        else
                        {
                            _stoppedStartTime = DateTime.MinValue;
                        }
                        break;
                }

                // Confirmar pushback si ha pasado el tiempo mínimo a baja velocidad
                if (CurrentPhase == FlightPhase.Boarding &&
                    groundSpeed > PUSHBACK_MIN_SPEED &&
                    groundSpeed <= PUSHBACK_MAX_SPEED &&
                    (DateTime.UtcNow - _pushbackStartTime).TotalSeconds >= PUSHBACK_MIN_DURATION)
                {
                    CurrentPhase = FlightPhase.Pushback;
                    _phaseStartTime = DateTime.UtcNow;
                    _pushbackStartTime = DateTime.MinValue;
                    OnLog?.Invoke($"🔄 Pushback confirmed at {groundSpeed:F1} kts", Theme.Taxi);
                }
            }
            else
            {
                // ===== FASES EN EL AIRE =====
                double altitudeAboveDest = altitude - destElev;

                switch (CurrentPhase)
                {
                    case FlightPhase.Takeoff:
                        if (isClimbing && canChangePhase)
                        {
                            CurrentPhase = FlightPhase.Climb;
                            _phaseStartTime = DateTime.UtcNow;
                            _lastStablePhase = FlightPhase.Climb;
                        }
                        break;

                    case FlightPhase.Climb:
                        // Si estamos cerca del crucero y llevamos un rato aquí
                        if (Math.Abs(altitude - cruiseAlt) < 2000 &&
                            (DateTime.UtcNow - _phaseStartTime).TotalMinutes > 2)
                        {
                            CurrentPhase = FlightPhase.Enroute;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"✈️ Cruise detected at {altitude} ft", Theme.MainText);
                        }
                        else if (isDescending && altitude < cruiseAlt * 0.9)
                        {
                            CurrentPhase = FlightPhase.Descent;
                            _phaseStartTime = DateTime.UtcNow;
                            OnLog?.Invoke($"✈️ Early descent at {altitude} ft", Theme.MainText);
                        }
                        break;

                    case FlightPhase.Enroute:
                        if (isDescending && (altitude < cruiseAlt * 0.9 || altitude < _maxAltitudeReached - 1000) && canChangePhase)
                        {
                            CurrentPhase = FlightPhase.Descent;
                            _phaseStartTime = DateTime.UtcNow;
                            _lastStablePhase = FlightPhase.Descent;
                            OnLog?.Invoke($"✈️ Descent started from {_maxAltitudeReached} ft", Theme.MainText);
                        }
                        break;

                    case FlightPhase.Descent:
                        if (distanceToDestination > 0 && distanceToDestination < approachDistanceThreshold && canChangePhase)
                        {
                            CurrentPhase = FlightPhase.Approach;
                            _phaseStartTime = DateTime.UtcNow;
                            _lastStablePhase = FlightPhase.Approach;
                            OnLog?.Invoke($"🛬 Approach started at {distanceToDestination:F0} nm", Theme.Approach);
                        }
                        else if (altitudeAboveDest < aglThreshold && altitudeAboveDest > 0 && canChangePhase)
                        {
                            CurrentPhase = FlightPhase.Approach;
                            _phaseStartTime = DateTime.UtcNow;
                            _lastStablePhase = FlightPhase.Approach;
                            OnLog?.Invoke($"🛬 Approach started at {altitudeAboveDest:F0} ft AGL", Theme.Approach);
                        }
                        break;

                    case FlightPhase.Approach:
                        // Go-around solo si estamos bajos (menos de 10000 ft AGL)
                        if (isClimbing && altitudeAboveDest < 10000 && altitudeAboveDest > 500 && canChangePhase)
                        {
                            CurrentPhase = FlightPhase.Climb;
                            _phaseStartTime = DateTime.UtcNow;
                            _lastStablePhase = FlightPhase.Climb;
                            OnLog?.Invoke($"✈️ Go-around detected!", Theme.Warning);
                        }
                        break;
                }
            }

            // Registrar block off time cuando comienza el taxi
            if (previousPhase == FlightPhase.Pushback && CurrentPhase == FlightPhase.TaxiOut)
            {
                Task.Run(() => UpdateBlockOffTime());
            }

            // ===== DETECCIÓN DE BLOCK_OFF MEJORADA =====
            if (!_blockOffRecorded && _isTimerStarted && ActivePirepId != null)
            {
                // Caso 1: Transición de Boarding a TaxiOut (sin pushback)
                if (previousPhase == FlightPhase.Boarding && CurrentPhase == FlightPhase.TaxiOut)
                {
                    OnLog?.Invoke($"🛫 Taxi out detected (direct), recording block_off", Theme.MainText);
                    Task.Run(() => UpdateBlockOffTime());
                }

                // Caso 2: Transición de Pushback a TaxiOut (con pushback)
                else if (previousPhase == FlightPhase.Pushback && CurrentPhase == FlightPhase.TaxiOut)
                {
                    OnLog?.Invoke($"🛫 Pushback complete, recording block_off", Theme.MainText);
                    Task.Run(() => UpdateBlockOffTime());
                }

                // Caso 3: Movimiento sostenido después de Boarding (detección directa)
                else if (CurrentPhase == FlightPhase.Boarding && groundSpeed > 10 &&
                         (DateTime.UtcNow - _phaseStartTime).TotalSeconds > 5)
                {
                    OnLog?.Invoke($"🛫 Sustained movement detected ({groundSpeed} kts), recording block_off", Theme.MainText);
                    CurrentPhase = FlightPhase.TaxiOut;
                    _phaseStartTime = DateTime.UtcNow;
                    Task.Run(() => UpdateBlockOffTime());
                }
            }

            _wasOnGround = isOnGround;

            if (previousPhase != CurrentPhase)
            {
                OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                string statusCode = FlightPhaseHelper.GetStatusCode(CurrentPhase);
                Task.Run(() => UpdatePirepStatus(statusCode));
                PhaseChanged?.Invoke(CurrentPhase);
            }
        }
        private async Task UpdateBlockOnTime()
        {
            if (string.IsNullOrEmpty(ActivePirepId))
                return;

            try
            {
                var payload = new { block_on_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") };
                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _apiService.HttpClient.PutAsync(
                    $"{_apiService.BaseUrl}api/pireps/{ActivePirepId}", content);

                if (response.IsSuccessStatusCode)
                {
                    _serverBlockOnTime = DateTime.UtcNow;
                    OnLog?.Invoke($"⏱️ Block On recorded at {_serverBlockOnTime:HH:mm:ss} UTC", Theme.MainText);
                    OnLog?.Invoke($"📊 Total flight time: {GetTotalFlightTimeDisplay()}", Theme.MainText);
                }
                else
                {
                    OnLog?.Invoke($"⚠️ Could not record block_on_time", Theme.Warning);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error recording block_on_time: {ex.Message}", Theme.Danger);
            }
        }
        // Método auxiliar para mostrar tiempo transcurrido
        private string GetFlightTimeDisplay()
        {
            TimeSpan elapsed = DateTime.UtcNow - _serverCreatedAt;
            return $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        private string GetTotalFlightTimeDisplay()
        {
            if (_serverBlockOffTime != default && _serverBlockOnTime != default)
            {
                TimeSpan flightTime = _serverBlockOnTime - _serverBlockOffTime;
                return $"{flightTime.Hours:D2}:{flightTime.Minutes:D2}:{flightTime.Seconds:D2}";
            }

            TimeSpan total = DateTime.UtcNow - _serverCreatedAt;
            return $"{total.Hours:D2}:{total.Minutes:D2}:{total.Seconds:D2}";
        }

        public void UpdateTelemetry(
            int altitude,
            int groundSpeed,
            int verticalSpeed,
            bool isOnGround,
            double fuel,
            double lat,
            double lon,
            double indicatedAirspeed = 0,
            double fuelFlow = 0,
            int transponder = 1200,
            bool autopilot = false,
            DateTime simTime = default,
            double radarAlt = 0,
            int order = 0)
        {
            if (string.IsNullOrEmpty(ActivePirepId))
                return;

            CurrentAltitude = altitude;
            CurrentGroundSpeed = groundSpeed;
            CurrentVerticalSpeed = verticalSpeed;
            CurrentFuel = fuel;
            IsOnGround = isOnGround;
            CurrentLat = lat;
            CurrentLon = lon;

            CurrentIndicatedAirspeed = indicatedAirspeed > 0 ? (int)indicatedAirspeed : groundSpeed;
            CurrentFuelFlow = fuelFlow;
            CurrentTransponder = transponder;
            AutopilotEngaged = autopilot;
            SimTime = simTime == default ? DateTime.UtcNow : simTime;
            RadarAltitude = radarAlt;
            PositionOrder = order;

            // Calcular distancia incremental (solo local, no se envía al servidor aquí)
            if (_lastPosition.HasValue && _lastPositionTime.HasValue)
            {
                double distance = CalculateDistance(
                    _lastPosition.Value.lat, _lastPosition.Value.lon,
                    lat, lon
                );
                _totalDistance += distance;
            }

            _lastPosition = (lat, lon);
            _lastPositionTime = DateTime.UtcNow;

            if (!isOnGround && !_lastAirborneTime.HasValue)
            {
                _lastAirborneTime = DateTime.UtcNow;
            }

            UpdatePhase(altitude, groundSpeed, isOnGround, verticalSpeed);
        }
        public async Task<bool> AbortFlight()
        {
            try
            {
                if (string.IsNullOrEmpty(ActivePirepId))
                    return false;

                bool success = await _apiService.DeletePirep(ActivePirepId);
                if (success)
                {
                    OnLog?.Invoke("✖️ Flight aborted on server", Theme.Warning);
                    ResetFlightState();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error aborting flight: {ex.Message}", Theme.Danger);
                return false;
            }
        }

        // En FlightManager.FilePirep() - versión corregida

        public async Task<bool> FilePirep()
        {
            try
            {
                if (string.IsNullOrEmpty(ActivePirepId))
                    return false;

                // Calcular tiempos basados en el cronómetro
                TimeSpan totalElapsed = DateTime.UtcNow - _serverCreatedAt;
                int totalFlightTimeMinutes = (int)totalElapsed.TotalMinutes;

                // Calcular tiempo real de vuelo (si tenemos block_off y block_on)
                int actualFlightTimeMinutes = totalFlightTimeMinutes;
                if (_serverBlockOffTime != default && _serverBlockOnTime != default)
                {
                    TimeSpan actualFlight = _serverBlockOnTime - _serverBlockOffTime;
                    actualFlightTimeMinutes = (int)actualFlight.TotalMinutes;
                    OnLog?.Invoke($"📊 Actual flight time (block_off to block_on): {actualFlightTimeMinutes} min", Theme.MainText);
                }
                else if (_serverBlockOffTime != default)
                {
                    TimeSpan sinceBlockOff = DateTime.UtcNow - _serverBlockOffTime;
                    actualFlightTimeMinutes = (int)sinceBlockOff.TotalMinutes;
                    OnLog?.Invoke($"⚠️ Block_on not recorded, using current time", Theme.Warning);
                }

                // Calcular combustible usado
                double fuelUsed = 0;
                if (_activePlan?.BlockFuel > 0 && CurrentFuel > 0)
                {
                    fuelUsed = _activePlan.BlockFuel - CurrentFuel;
                    if (fuelUsed < 0) fuelUsed = 0;
                }

                // Si no tenemos combustible, usar el tracking
                if (fuelUsed <= 0 && _totalFuelUsed > 0)
                {
                    fuelUsed = _totalFuelUsed;
                }

                double totalDistance = _totalDistance;

                // Obtener valores del plan
                double plannedDistance = _activePlan?.Distance ?? 0;
                int plannedFlightTimeSeconds = _activePlan?.EstTimeEnroute ?? 0;
                int plannedFlightTimeMinutes = plannedFlightTimeSeconds / 60;
                double blockFuel = _activePlan?.BlockFuel ?? 0;

                // Logs de depuración
                OnLog?.Invoke($"📊 Planned Distance: {plannedDistance:F1} NM", Theme.MainText);
                OnLog?.Invoke($"📊 Planned Flight Time: {plannedFlightTimeMinutes} min", Theme.MainText);
                OnLog?.Invoke($"📊 Actual Distance: {totalDistance:F1} NM", Theme.MainText);
                OnLog?.Invoke($"📊 Actual Flight Time: {actualFlightTimeMinutes} min", Theme.MainText);
                OnLog?.Invoke($"⛽ Fuel Used: {fuelUsed:F0} {_activePlan?.Units ?? "kg"}", Theme.MainText);

                var finalData = new
                {
                    state = 2,
                    submitted_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    distance = Math.Round(totalDistance, 2),
                    planned_distance = Math.Round(plannedDistance, 2),
                    flight_time = actualFlightTimeMinutes,
                    planned_flight_time = plannedFlightTimeMinutes,
                    block_fuel = Math.Round(blockFuel, 0),
                    fuel_used = Math.Round(Math.Max(0, fuelUsed), 0),
                    landing_rate = (int)(TouchdownFpm ?? 0),
                    notes = $"vmsOpenAcars Report - Total time: {totalFlightTimeMinutes} min, Flight time: {actualFlightTimeMinutes} min, Distance: {totalDistance:F1} NM"
                };

                bool success = await _apiService.FilePirep(ActivePirepId, finalData);
                if (success)
                {
                    OnLog?.Invoke($"✅ PIREP filed successfully", Theme.Success);
                    OnLog?.Invoke($"📊 Total time: {totalFlightTimeMinutes} min", Theme.MainText);
                    OnLog?.Invoke($"📊 Distance: {totalDistance:F1} NM", Theme.MainText);
                    OnLog?.Invoke($"✈️ Flight time: {actualFlightTimeMinutes} min", Theme.MainText);
                    OnLog?.Invoke($"⛽ Fuel used: {fuelUsed:F0} {_activePlan?.Units ?? "kg"}", Theme.Success);
                    ResetFlightState();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error filing PIREP: {ex.Message}", Theme.Danger);
                return false;
            }
        }

        /// <summary>
        /// Actualiza el estado del vuelo en el servidor (tiempo y distancia) en una sola llamada
        /// </summary>
        public async Task UpdateFlightProgress()
        {
            if (string.IsNullOrEmpty(ActivePirepId))
                return;

            if (!_isTimerStarted)
                return;

            // Calcular tiempos
            DateTime referenceTime = _serverBlockOffTime != default ? _serverBlockOffTime : _serverCreatedAt;
            int flightTimeMinutes = (int)(DateTime.UtcNow - referenceTime).TotalMinutes;
            double currentDistance = Math.Round(_totalDistance, 2);

            // Verificar si hay cambios significativos para actualizar en servidor
            bool timeChanged = Math.Abs(flightTimeMinutes - _lastFlightTimeMinutesLogged) >= 1;
            bool distanceChanged = Math.Abs(currentDistance - _lastDistanceLogged) >= 1;

            // Solo actualizar si hay cambios significativos
            if (!timeChanged && !distanceChanged)
                return;

            try
            {
                var updateData = new
                {
                    flight_time = flightTimeMinutes,
                    distance = currentDistance
                };

                bool success = await _apiService.UpdatePirep(ActivePirepId, updateData);

                if (success)
                {
                    // Actualizar últimos valores registrados
                    _lastFlightTimeMinutesLogged = flightTimeMinutes;
                    _lastDistanceLogged = currentDistance;

                    // Log cada 5 minutos o cada 10 NM
                    if (flightTimeMinutes % 5 == 0 && flightTimeMinutes > 0)
                    {
                        TimeSpan elapsed = DateTime.UtcNow - _serverCreatedAt;
                        OnLog?.Invoke($"⏱️ Flight time: {flightTimeMinutes} min | Distance: {currentDistance:F1} NM (Total: {elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2})", Theme.MainText);
                    }
                    else if ((int)currentDistance % 10 == 0 && currentDistance > 0 && currentDistance != _lastDistanceLogged)
                    {
                        OnLog?.Invoke($"📊 Distance: {currentDistance:F1} NM | Time: {flightTimeMinutes} min", Theme.MainText);
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error updating flight progress: {ex.Message}", Theme.Danger);
            }
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

        public override string ToString()
        {
            return $"ICAO: {(IcaoMatch ? "✅" : "❌")} " +
                   $"GPS: {(GpsValid ? "✅" : "⏳")} " +
                   $"Dist: {DistanceFromAirport:F1}km";
        }
    }
}