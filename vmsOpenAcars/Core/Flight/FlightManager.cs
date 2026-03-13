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

        #region Properties

        /// <summary>
        /// Gets the current airport based on pilot's location or phpVMS assignment.
        /// </summary>
        public string CurrentAirport
        {
            get => _currentAirport;
            private set
            {
                _currentAirport = value;
                OnAirportChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// Gets the current latitude from the simulator.
        /// </summary>
        public double CurrentLat { get; private set; }

        /// <summary>
        /// Gets the current longitude from the simulator.
        /// </summary>
        public double CurrentLon { get; private set; }

        /// <summary>
        /// Gets the current indicated airspeed in knots.
        /// </summary>
        public int CurrentIndicatedAirspeed { get; private set; }

        /// <summary>
        /// Gets the current altitude in feet MSL.
        /// </summary>
        public int CurrentAltitude { get; private set; }

        /// <summary>
        /// Gets the current vertical speed in feet per minute.
        /// </summary>
        public int CurrentVerticalSpeed { get; private set; }

        /// <summary>
        /// Gets the touchdown vertical speed (negative for landing).
        /// </summary>
        public int? TouchdownFpm { get; private set; }

        /// <summary>
        /// Gets the current flight phase.
        /// </summary>
        public FlightPhase CurrentPhase { get; private set; }

        /// <summary>
        /// Gets the current ground speed in knots.
        /// </summary>
        public int CurrentGroundSpeed { get; private set; }

        /// <summary>
        /// Gets the current fuel total in pounds.
        /// </summary>
        public double CurrentFuel { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the aircraft is on ground.
        /// </summary>
        public bool IsOnGround { get; private set; }

        /// <summary>
        /// Gets the active PIREP ID returned by phpVMS after prefile.
        /// </summary>
        public string ActivePirepId { get; private set; } = "";

        /// <summary>
        /// Gets the flight start time (UTC).
        /// </summary>
        public DateTime FlightStartTime { get; private set; }

        /// <summary>
        /// Gets the active pilot information.
        /// </summary>
        public Pilot ActivePilot => _activePilot;

        /// <summary>
        /// Gets the active flight plan from SimBrief.
        /// </summary>
        public SimbriefPlan ActivePlan => _activePlan;

        /// <summary>
        /// Gets a value indicating whether the simulator is connected.
        /// </summary>
        public bool IsSimulatorConnected { get; private set; }

        /// <summary>
        /// Gets the current position validation status (ICAO and GPS).
        /// </summary>
        public ValidationStatus PositionValidationStatus { get; private set; }

        /// <summary>
        /// Gets the current fuel flow in pounds per hour.
        /// </summary>
        public double CurrentFuelFlow { get; private set; }

        /// <summary>
        /// Gets the current transponder code.
        /// </summary>
        public int CurrentTransponder { get; private set; }

        /// <summary>
        /// Gets a value indicating whether autopilot is engaged.
        /// </summary>
        public bool AutopilotEngaged { get; private set; }

        /// <summary>
        /// Gets the current simulation time (UTC).
        /// </summary>
        public DateTime SimTime { get; private set; }

        /// <summary>
        /// Gets the current radar altitude (AGL) in feet.
        /// </summary>
        public double RadarAltitude { get; private set; }

        /// <summary>
        /// Gets the current position order number for ACARS updates.
        /// </summary>
        public int PositionOrder { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the phase changes (string version for logs).
        /// </summary>
        public event Action<string> OnPhaseChanged;

        /// <summary>
        /// Occurs when the phase changes (enum version for UI).
        /// </summary>
        public event Action<FlightPhase> PhaseChanged;

        /// <summary>
        /// Occurs when the current airport changes.
        /// </summary>
        public event Action<string> OnAirportChanged;

        /// <summary>
        /// Occurs when a log message is generated.
        /// </summary>
        public event Action<string, Color> OnLog;

        /// <summary>
        /// Occurs when position validation status changes.
        /// </summary>
        public event Action<ValidationStatus> OnPositionValidated;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="FlightManager"/> class.
        /// </summary>
        /// <param name="apiService">The API service for phpVMS communication.</param>
        public FlightManager(ApiService apiService)
        {
            _apiService = apiService;
            _positionValidator = new PositionValidator();
            CurrentPhase = FlightPhase.Idle;
            PositionValidationStatus = new ValidationStatus();
        }

        #region Private Methods

        /// <summary>
        /// Updates the PIREP status on the server.
        /// </summary>
        /// <param name="statusCode">The status code (e.g., "BST", "TXI", "ARR").</param>
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

        /// <summary>
        /// Updates the block off time on the server when pushback/taxi begins.
        /// </summary>
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

        /// <summary>
        /// Validates if the current airport matches the flight plan origin.
        /// </summary>
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

        /// <summary>
        /// Validates the current GPS position against the assigned airport.
        /// </summary>
        /// <param name="currentLat">Current latitude from simulator.</param>
        /// <param name="currentLon">Current longitude from simulator.</param>
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

        /// <summary>
        /// Registers a touchdown event with the vertical speed.
        /// </summary>
        /// <param name="verticalSpeed">Vertical speed at touchdown.</param>
        private void RegisterTouchdown(int verticalSpeed)
        {
            if (_touchdownCaptured)
                return;

            TouchdownFpm = verticalSpeed;
            _touchdownCaptured = true;
            OnLog?.Invoke($"✈️ Touchdown: {verticalSpeed} FPM", Theme.MainText);
        }

        /// <summary>
        /// Calculates the Haversine distance between two coordinates.
        /// </summary>
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

        /// <summary>
        /// Resets all flight-related state after completion or cancellation.
        /// </summary>
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

        /// <summary>
        /// Cancels the current flight and deletes the PIREP from the server.
        /// </summary>
        /// <returns>True if cancellation was successful.</returns>
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

        /// <summary>
        /// Sets the active pilot after login.
        /// </summary>
        /// <param name="pilot">The pilot data from phpVMS.</param>
        public void SetActivePilot(Pilot pilot)
        {
            _activePilot = pilot;
            CurrentAirport = pilot?.CurrentAirport ?? "";

            if (_activePlan != null)
            {
                ValidateAirportMatch();
            }
        }

        /// <summary>
        /// Sets the active flight plan after loading from SimBrief.
        /// </summary>
        /// <param name="plan">The SimBrief flight plan.</param>
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

        /// <summary>
        /// Updates the simulator connection state and triggers position validation.
        /// </summary>
        /// <param name="connected">Whether the simulator is connected.</param>
        /// <param name="latitude">Current latitude (if connected).</param>
        /// <param name="longitude">Current longitude (if connected).</param>
        public void SetSimulatorConnected(bool connected, double? latitude = null, double? longitude = null)
        {
            // System.Diagnostics.Debug.WriteLine($"FlightManager.SetSimulatorConnected({connected}, {latitude}, {longitude})");
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

        /// <summary>
        /// Forces a position validation update.
        /// </summary>
        /// <param name="lat">Current latitude.</param>
        /// <param name="lon">Current longitude.</param>
        public void UpdatePositionValidation(double lat, double lon)
        {
            if (_activePilot != null && IsSimulatorConnected)
            {
                ValidateSimulatorPosition(lat, lon);
            }
        }

        /// <summary>
        /// Attempts to detect the nearest airport using the phpVMS API.
        /// </summary>
        /// <param name="latitude">Current latitude.</param>
        /// <param name="longitude">Current longitude.</param>
        /// <returns>ICAO code of the nearest airport.</returns>
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

        /// <summary>
        /// Checks if the pilot is at the required departure airport.
        /// </summary>
        /// <param name="requiredAirport">Required airport ICAO code.</param>
        /// <returns>True if the pilot is at the correct airport.</returns>
        public bool IsPilotAtDepartureAirport(string requiredAirport)
        {
            return CurrentAirport?.Equals(requiredAirport, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        /// <summary>
        /// Starts a new flight by prefiling the PIREP and setting initial phase to Boarding.
        /// </summary>
        /// <param name="plan">The SimBrief flight plan.</param>
        /// <param name="pilot">The active pilot.</param>
        /// <returns>True if flight started successfully.</returns>
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

        /// <summary>
        /// Checks if all conditions are met to start a flight.
        /// </summary>
        /// <returns>True if flight can be started.</returns>
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
        /// Updates the flight phase based on current telemetry.
        /// </summary>
        /// <param name="altitude">Current altitude in feet.</param>
        /// <param name="groundSpeed">Current ground speed in knots.</param>
        /// <param name="isOnGround">Whether the aircraft is on ground.</param>
        /// <param name="verticalSpeed">Current vertical speed in fpm.</param>
        /// <param name="distanceToDestination">Distance to destination (optional).</param>
        public void UpdatePhase(int altitude, int groundSpeed, bool isOnGround, int verticalSpeed, double distanceToDestination = -1)
        {
            var previousPhase = CurrentPhase;

            if (altitude > _maxAltitudeReached)
                _maxAltitudeReached = altitude;

            if (_wasOnGround == false && isOnGround == true)
            {
                RegisterTouchdown(verticalSpeed);
                CurrentPhase = FlightPhase.AfterLanding;
                Task.Run(() => UpdatePirepStatus("LAN"));
            }

            if (isOnGround)
            {
                if (CurrentPhase == FlightPhase.Boarding && groundSpeed > 3)
                {
                    CurrentPhase = FlightPhase.TaxiOut;
                    Task.Run(() => UpdatePirepStatus("TXI"));
                }
                else if (CurrentPhase == FlightPhase.TaxiOut && groundSpeed > 40)
                {
                    CurrentPhase = FlightPhase.Takeoff;
                    Task.Run(() => UpdatePirepStatus("TOF"));
                }
                else if (CurrentPhase == FlightPhase.AfterLanding && groundSpeed < 40)
                {
                    CurrentPhase = FlightPhase.TaxiIn;
                    Task.Run(() => UpdatePirepStatus("TXI"));
                }
                else if (CurrentPhase == FlightPhase.TaxiIn && groundSpeed < 2)
                {
                    CurrentPhase = FlightPhase.Completed;
                    Task.Run(() => UpdatePirepStatus("ARR"));
                }
            }
            else
            {
                bool isClimbing = verticalSpeed > 100;
                bool isDescending = verticalSpeed < -100;

                if (CurrentPhase == FlightPhase.Takeoff || CurrentPhase == FlightPhase.Climb)
                {
                    if (isClimbing)
                    {
                        if (CurrentPhase == FlightPhase.Takeoff && altitude > 1500)
                        {
                            CurrentPhase = FlightPhase.Climb;
                            Task.Run(() => UpdatePirepStatus("ICL"));
                        }
                    }
                    else if (Math.Abs(verticalSpeed) <= 100)
                    {
                        if (altitude > 3000 && _maxAltitudeReached - altitude < 500)
                        {
                            CurrentPhase = FlightPhase.Enroute;
                            Task.Run(() => UpdatePirepStatus("ENR"));
                        }
                    }
                }
                else if (CurrentPhase == FlightPhase.Enroute)
                {
                    if (isDescending)
                    {
                        CurrentPhase = FlightPhase.Descent;
                        Task.Run(() => UpdatePirepStatus("APR"));
                    }
                }
                else if (CurrentPhase == FlightPhase.Descent)
                {
                    int altitudeAboveDestination = altitude - _destinationElevation;

                    if (distanceToDestination > 0 && distanceToDestination < 15)
                    {
                        CurrentPhase = FlightPhase.Approach;
                        Task.Run(() => UpdatePirepStatus("FIN"));
                    }
                    else if (altitudeAboveDestination < 3000)
                    {
                        CurrentPhase = FlightPhase.Approach;
                        Task.Run(() => UpdatePirepStatus("FIN"));
                    }
                    else if (isClimbing && altitudeAboveDestination > 4000)
                    {
                        CurrentPhase = FlightPhase.Climb;
                        Task.Run(() => UpdatePirepStatus("ICL"));
                    }
                }
                else if (CurrentPhase == FlightPhase.Approach)
                {
                    if (isClimbing)
                    {
                        CurrentPhase = FlightPhase.Climb;
                        Task.Run(() => UpdatePirepStatus("ICL"));
                    }
                }
            }

            // Record block_off_time when pushback/taxi begins
            if (previousPhase == FlightPhase.Boarding &&
                (CurrentPhase == FlightPhase.TaxiOut || CurrentPhase == FlightPhase.Pushback))
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

        /// <summary>
        /// Updates telemetry data from the simulator.
        /// </summary>
        /// <param name="altitude">Current altitude.</param>
        /// <param name="groundSpeed">Current ground speed.</param>
        /// <param name="verticalSpeed">Current vertical speed.</param>
        /// <param name="isOnGround">On ground status.</param>
        /// <param name="fuel">Fuel remaining.</param>
        /// <param name="lat">Current latitude.</param>
        /// <param name="lon">Current longitude.</param>
        /// <param name="indicatedAirspeed">Indicated airspeed (optional).</param>
        /// <param name="fuelFlow">Fuel flow (optional).</param>
        /// <param name="transponder">Transponder code (optional).</param>
        /// <param name="autopilot">Autopilot status (optional).</param>
        /// <param name="simTime">Simulation time (optional).</param>
        /// <param name="radarAlt">Radar altitude (optional).</param>
        /// <param name="order">Position order number (optional).</param>
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

        /// <summary>
        /// Aborts the current flight and deletes the PIREP from the server.
        /// </summary>
        /// <returns>True if abort was successful.</returns>
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

        /// <summary>
        /// Files the completed PIREP to phpVMS.
        /// </summary>
        /// <returns>True if filing was successful.</returns>
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

        /// <summary>
        /// Updates the PIREP flight time on the server.
        /// </summary>
        public async Task UpdatePirepFlightTime()
        {
            if (string.IsNullOrEmpty(ActivePirepId))
                return;

            int flightTimeMinutes = (int)(DateTime.UtcNow - FlightStartTime).TotalMinutes;

            try
            {
                var updateData = new { flight_time = flightTimeMinutes };
                bool success = await _apiService.UpdatePirep(ActivePirepId, updateData);

                if (success)
                {
                    if (flightTimeMinutes % 5 == 0) // Log every 5 minutes
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

    /// <summary>
    /// Represents the current validation status for a flight.
    /// </summary>
    public class ValidationStatus
    {
        /// <summary>
        /// Gets or sets a value indicating whether the ICAO codes match.
        /// </summary>
        public bool IcaoMatch { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the GPS position is valid.
        /// </summary>
        public bool GpsValid { get; set; }

        /// <summary>
        /// Gets or sets the distance from the assigned airport in kilometers.
        /// </summary>
        public double DistanceFromAirport { get; set; }

        /// <summary>
        /// Gets or sets the airport assigned by phpVMS.
        /// </summary>
        public string PhpVmsAirport { get; set; }

        /// <summary>
        /// Gets or sets the origin airport from the SimBrief plan.
        /// </summary>
        public string SimbriefAirport { get; set; }

        /// <summary>
        /// Gets a value indicating whether the flight can be started.
        /// </summary>
        public bool CanStart => IcaoMatch;

        /// <summary>
        /// Returns a string representation of the validation status.
        /// </summary>
        public override string ToString()
        {
            return $"ICAO: {(IcaoMatch ? "✅" : "❌")} " +
                   $"GPS: {(GpsValid ? "✅" : "⏳")} " +
                   $"Dist: {DistanceFromAirport:F1}km";
        }
    }
}