using System;
using System.Drawing;
using System.Threading.Tasks;
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

        // Variables para control de histéresis
        private DateTime _phaseStartTime = DateTime.UtcNow;
        private FlightPhase _lastStablePhase;

        // Variables para pushback
        private DateTime _pushbackStartTime = DateTime.MinValue;
        private const double PUSHBACK_MIN_SPEED = 0.5;
        private const double PUSHBACK_MAX_SPEED = 5.0;
        private const int PUSHBACK_MIN_DURATION = 5;

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
                var updateData = new
                {
                    block_off_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                };
                await _apiService.UpdatePirep(ActivePirepId, updateData);
                OnLog?.Invoke($"⏱️ Block Off Time recorded", Theme.MainText);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error updating block_off_time: {ex.Message}", Theme.Danger);
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
            const double R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
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
            OnPhaseChanged?.Invoke(CurrentPhase.ToString());
            PhaseChanged?.Invoke(CurrentPhase);
        }

        #endregion

        #region Public Methods

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

        public async Task<bool> StartFlight(SimbriefPlan plan, Pilot pilot)
        {
            try
            {
                if (_apiService == null)
                {
                    OnLog?.Invoke("ERROR: ApiService not configured.", Theme.Warning);
                    return false;
                }

                _activePlan = plan;
                _activePilot = pilot;

                OnLog?.Invoke($"{_("SendingPrefile")}...", Theme.MainText);

                ActivePirepId = await _apiService.PrefileFlight(plan, pilot);

                if (!string.IsNullOrEmpty(ActivePirepId))
                {
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
                        // Esperar 10 segundos parado antes de declarar ONBLOCK
                        if (groundSpeed < 2)
                        {
                            if (_pushbackStartTime == DateTime.MinValue)
                                _pushbackStartTime = DateTime.UtcNow;

                            if ((DateTime.UtcNow - _pushbackStartTime).TotalSeconds >= 10)
                            {
                                CurrentPhase = FlightPhase.OnBlock;
                                _phaseStartTime = DateTime.UtcNow;
                                _pushbackStartTime = DateTime.MinValue;
                                OnLog?.Invoke($"🅿️ On block", Theme.Success);
                            }
                        }
                        else
                        {
                            _pushbackStartTime = DateTime.MinValue;
                        }
                        break;

                    case FlightPhase.OnBlock:
                        // Esperar a que el usuario envíe el PIREP manualmente
                        // O detectar motores apagados para pasar a Completed
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

            _wasOnGround = isOnGround;

            if (previousPhase != CurrentPhase)
            {
                OnLog?.Invoke($"✈️ Phase changed: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                PhaseChanged?.Invoke(CurrentPhase);
            }
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

        public async Task<bool> FilePirep()
        {
            try
            {
                if (string.IsNullOrEmpty(ActivePirepId))
                    return false;

                DateTime now = DateTime.UtcNow;
                int totalFlightTime = (int)(now - FlightStartTime).TotalMinutes;
                int airTime = (int)(_lastAirborneTime.HasValue ?
                    (now - _lastAirborneTime.Value).TotalMinutes : totalFlightTime);

                double fuelUsed = 0;
                if (_activePlan?.BlockFuel > 0 && CurrentFuel > 0)
                {
                    fuelUsed = _activePlan.BlockFuel - CurrentFuel;
                }

                double totalDistance = _totalDistance;

                var finalData = new
                {
                    state = 2,
                    submitted_at = now.ToString("yyyy-MM-dd HH:mm:ss"),
                    block_off_time = FlightStartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    block_on_time = now.ToString("yyyy-MM-dd HH:mm:ss"),
                    distance = Math.Round(totalDistance, 2),
                    planned_distance = _activePlan?.Distance ?? 0,
                    flight_time = airTime,
                    planned_flight_time = _activePlan?.EstTimeEnroute ?? 0,
                    block_fuel = Math.Round(_activePlan?.BlockFuel ?? 0, 0),
                    fuel_used = Math.Round(Math.Max(0, fuelUsed), 0),
                    landing_rate = (int)(TouchdownFpm ?? 0),
                    notes = "vmsOpenAcars Report"
                };

                bool success = await _apiService.FilePirep(ActivePirepId, finalData);
                if (success)
                {
                    OnLog?.Invoke("✅ PIREP filed successfully", Theme.Success);
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

        public async Task UpdatePirepFlightTime()
        {
            if (string.IsNullOrEmpty(ActivePirepId))
                return;

            int flightTimeMinutes = (int)(DateTime.UtcNow - FlightStartTime).TotalMinutes;

            // Log para depuración
            System.Diagnostics.Debug.WriteLine($"Updating flight time: {flightTimeMinutes} min");

            try
            {
                var updateData = new { flight_time = flightTimeMinutes };
                bool success = await _apiService.UpdatePirep(ActivePirepId, updateData);

                if (success)
                {
                    if (flightTimeMinutes % 5 == 0)
                        OnLog?.Invoke($"⏱️ Flight time: {flightTimeMinutes} min", Theme.MainText);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error updating flight time: {ex.Message}", Theme.Danger);
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