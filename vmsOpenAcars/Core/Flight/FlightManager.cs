using System;
using System.Drawing;
using System.Threading.Tasks;
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
        private double _totalDistance = 0;
        private DateTime? _lastAirborneTime = null;
        private DateTime? _lastPositionTime = null;
        private (double lat, double lon)? _lastPosition = null;

        // Propiedades
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

        // Eventos
        public event Action<string> OnPhaseChanged;
        public event Action<FlightPhase> PhaseChanged;
        public event Action<string> OnAirportChanged;
        public event Action<string, Color> OnLog;
        public event Action<ValidationStatus> OnPositionValidated;

        public FlightManager(ApiService apiService)
        {
            _apiService = apiService;
            _positionValidator = new PositionValidator();
            CurrentPhase = FlightPhase.Idle;
            PositionValidationStatus = new ValidationStatus();
        }

        private async Task UpdatePirepStatus(string statusCode)
        {
            if (string.IsNullOrEmpty(ActivePirepId))
                return;

            try
            {
                // Actualizar usando el endpoint de update (PUT)
                var updateData = new { status = statusCode };
                bool success = await _apiService.UpdatePirep(ActivePirepId, updateData);

                if (success)
                {
                    OnLog?.Invoke($"📊 Estado PIREP: {statusCode}", Theme.MainText);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error actualizando estado: {ex.Message}", Theme.Danger);
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
                OnLog?.Invoke($"⏱️ Block Off Time registrado", Theme.MainText);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error actualizando block_off_time: {ex.Message}", Theme.Danger);
            }
        }
        public async Task<bool> CancelFlight()
        {
            try
            {
                if (string.IsNullOrEmpty(ActivePirepId))
                    return false;

                bool success = await _apiService.DeletePirep(ActivePirepId);
                if (success)
                {
                    OnLog?.Invoke("✖️ Vuelo cancelado en servidor", Theme.Warning);
                    ResetFlightState();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error cancelando vuelo: {ex.Message}", Theme.Danger);
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
                OnLog?.Invoke($"📊 Plan cargado. Destino: {plan.Destination} (elevación: {_destinationElevation} ft)", Theme.MainText);
            }

            // Validar contra el aeropuerto de phpVMS
            ValidateAirportMatch(); // Esto actualiza IcaoMatch

            // También actualizar validación GPS si hay posición reciente
            if (CurrentLat != 0 && CurrentLon != 0)
            {
                UpdatePositionValidation(CurrentLat, CurrentLon);
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
                        OnLog?.Invoke("⚠️ No hay coordenadas del aeropuerto para validar posición", Theme.Warning);
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

            // Verificar si hubo cambio en el estado GPS
            bool gpsChanged = (PositionValidationStatus.GpsValid != isValid) ||
                              (Math.Abs(PositionValidationStatus.DistanceFromAirport - distance) > 0.01);

            // Actualizar siempre el estado
            PositionValidationStatus.GpsValid = isValid;
            PositionValidationStatus.DistanceFromAirport = distance;

            // Solo registrar en log cuando cambia
            if (gpsChanged)
            {
                OnLog?.Invoke(message, color);
            }

            // Siempre notificar a la UI para que actualice, aunque no haya cambio de texto
            OnPositionValidated?.Invoke(PositionValidationStatus);
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
                        OnLog?.Invoke($"📍 Aeropuerto detectado: {airport}", Theme.MainText);
                        return airport;
                    }
                }

                OnLog?.Invoke("⚠️ No se pudo detectar aeropuerto vía API", Theme.Warning);
                return CurrentAirport ?? "SKBO";
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Error detectando aeropuerto: {ex.Message}", Theme.Danger);
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
                    OnLog?.Invoke("ERROR: ApiService no está configurado.", Theme.Warning);
                    return false;
                }

                _activePlan = plan;
                _activePilot = pilot;

                OnLog?.Invoke($"{_("SendingPrefile")}...", Theme.MainText);

                // 1. Prefile - crea el PIREP (estado por defecto podría ser INI o SCH)
                ActivePirepId = await _apiService.PrefileFlight(plan, pilot);

                if (!string.IsNullOrEmpty(ActivePirepId))
                {
                    // 2. Cambiar inmediatamente a BOARDING (BST)
                    await Task.Run(() => UpdatePirepStatus("BST"));
                    
                    _touchdownCaptured = false;
                    TouchdownFpm = null;
                    CurrentPhase = FlightPhase.Boarding;
                    FlightStartTime = DateTime.Now;
                    return true;
                }

                OnLog?.Invoke("ERROR: El servidor no devolvió un ID de PIREP.", Theme.Danger);
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
                OnLog?.Invoke("⛔ No hay piloto activo", Theme.Warning);
                return false;
            }

            if (_activePlan == null)
            {
                OnLog?.Invoke("⛔ No hay plan de vuelo cargado", Theme.Warning);
                return false;
            }

            if (!PositionValidationStatus.IcaoMatch)
            {
                OnLog?.Invoke("⛔ El aeropuerto asignado no coincide con el plan", Theme.Warning);
                return false;
            }

            return true;
        }

        public void UpdatePhase(int altitude, int groundSpeed, bool isOnGround, int verticalSpeed, double distanceToDestination = -1)
        {
            var previousPhase = CurrentPhase;

            if (altitude > _maxAltitudeReached)
                _maxAltitudeReached = altitude;

            if (_wasOnGround == false && isOnGround == true)
            {
                RegisterTouchdown(verticalSpeed);
                CurrentPhase = FlightPhase.AfterLanding;
                Task.Run(() => UpdatePirepStatus("LAN")); // Landed
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

            // Registrar block_off_time cuando comienza el pushback/taxi
            if (previousPhase == FlightPhase.Boarding &&
                (CurrentPhase == FlightPhase.TaxiOut || CurrentPhase == FlightPhase.Pushback))
            {
                Task.Run(() => UpdateBlockOffTime());
            }

            _wasOnGround = isOnGround;

            if (previousPhase != CurrentPhase)
            {
                OnLog?.Invoke($"✈️ Fase cambiada: {previousPhase} → {CurrentPhase}", Theme.Takeoff);
                PhaseChanged?.Invoke(CurrentPhase);
            }
        }
        private void RegisterTouchdown(int verticalSpeed)
        {
            if (_touchdownCaptured)
                return;

            TouchdownFpm = verticalSpeed;
            _touchdownCaptured = true;
            OnLog?.Invoke($"✈️ Touchdown: {verticalSpeed} FPM", Theme.MainText);
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

        public async Task<bool> AbortFlight()
        {
            try
            {
                if (string.IsNullOrEmpty(ActivePirepId))
                    return false;

                bool success = await _apiService.DeletePirep(ActivePirepId);
                if (success)
                {
                    OnLog?.Invoke("✖️ Vuelo cancelado en servidor", Theme.Warning);
                    ResetFlightState();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error cancelando vuelo: {ex.Message}", Theme.Danger);
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
                    OnLog?.Invoke("✅ PIREP enviado correctamente", Theme.Success);
                    ResetFlightState();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error enviando PIREP: {ex.Message}", Theme.Danger);
                return false;
            }
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
                    // Opcional: loggear cada cierto tiempo
                    if (flightTimeMinutes % 5 == 0) // Cada 5 minutos
                        OnLog?.Invoke($"⏱️ Tiempo de vuelo: {flightTimeMinutes} min", Theme.MainText);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error actualizando tiempo de vuelo: {ex.Message}", Theme.Danger);
            }
        }

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