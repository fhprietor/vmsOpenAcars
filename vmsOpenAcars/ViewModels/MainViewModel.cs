using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;
using static vmsOpenAcars.Helpers.L;
using vmsOpenAcars.UI.Forms;
using System.Linq;

namespace vmsOpenAcars.ViewModels
{
    public class MainViewModel
    {
        // Propiedades públicas
        public FlightManager FlightManager => _flightManager;
        public PhpVmsFlightService PhpVmsFlightService => _phpVmsFlightService;
        public SimbriefEnhancedService SimbriefEnhancedService => _simbriefEnhancedService;
        public ApiService ApiService => _apiService;
        public Pilot ActivePilot => _flightManager.ActivePilot;

        private readonly FlightManager _flightManager;
        private readonly FsuipcService _fsuipc;
        private readonly ApiService _apiService;
        private readonly PhpVmsFlightService _phpVmsFlightService;
        private readonly SimbriefEnhancedService _simbriefEnhancedService;
        private DateTime _lastPositionUpdate = DateTime.MinValue;
        private readonly TimeSpan _positionUpdateInterval = TimeSpan.FromSeconds(5);
        private object _lastTelemetry;
        private (double lat, double lon)? _lastPosition;

        // Eventos para comunicación con la UI
        public event Action<string, Color> OnLog;
        public event Action<string> OnPositionUpdate;
        public event Action<FlightPhase> OnPhaseChanged;
        public event Action<FlightPhase> OnAirStatusChanged;
        public event Action<int> OnAltitudeChanged;
        public event Action<int> OnSpeedChanged;
        public event Action<ValidationStatus> OnValidationStatusChanged;
        public event Action<string> OnFlightInfoChanged;
        public event Action<string> OnSimulatorNameChanged;
        public event Action<bool> OnAcarsStatusChanged;
        public event Action<string> OnAirportChanged;
        public event Action<string, Color, bool> OnButtonStateChanged;
        public event Action OnOpenFlightPlanner;
        public event Action<string, string> OnShowMessage;
        public event Action<string> OnFlightNoChanged;
        public event Action<string> OnDepArrChanged;
        public event Action<string> OnAlternateChanged;
        public event Action<string> OnRouteChanged;
        public event Action<string> OnAircraftChanged;
        public event Action<string> OnFuelChanged;
        public event Action<string> OnTypeChanged;
        public event Action<string> OnRegistrationChanged;
        public event Func<string, string, EcamDialogButtons, Task<DialogResult>> OnShowConfirmation;
        

        public MainViewModel(
            FlightManager flightManager,
            FsuipcService fsuipc,
            ApiService apiService,
            PhpVmsFlightService phpVmsFlightService,
            SimbriefEnhancedService simbriefEnhancedService)
        {
            _flightManager = flightManager;
            _fsuipc = fsuipc;
            _apiService = apiService;
            _phpVmsFlightService = phpVmsFlightService;
            _simbriefEnhancedService = simbriefEnhancedService;

            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            // Desuscribir primero para evitar duplicados
            _flightManager.OnLog -= OnFlightManagerLog;
            _flightManager.PhaseChanged -= OnFlightPhaseChanged;
            _flightManager.OnPositionValidated -= OnPositionValidated;
            _flightManager.OnAirportChanged -= OnAirportChanged;
            _fsuipc.DataUpdated -= OnFsuipcDataUpdated;
            _fsuipc.Connected -= OnFsuipcConnected;
            _fsuipc.Disconnected -= OnFsuipcDisconnected;

            // Suscribir
            _flightManager.OnLog += OnFlightManagerLog;
            _flightManager.PhaseChanged += OnFlightPhaseChanged;
            _flightManager.OnPositionValidated += OnPositionValidated;
            _flightManager.OnAirportChanged += OnAirportChanged;
            _fsuipc.DataUpdated += OnFsuipcDataUpdated;
            _fsuipc.Connected += OnFsuipcConnected;
            _fsuipc.Disconnected += OnFsuipcDisconnected;
        }

        private void OnFlightManagerLog(string msg, Color color)
        {
            OnLog?.Invoke(msg, color);
        }

        private void OnFlightPhaseChanged(FlightPhase phase)
        {
            OnPhaseChanged?.Invoke(phase);

            // Actualizar botón según la fase
            if (phase == FlightPhase.Completed)
            {
                OnButtonStateChanged?.Invoke("SEND PIREP", Color.Green, true);
            }
        }

        private void OnPositionValidated(ValidationStatus status)
        {
            OnValidationStatusChanged?.Invoke(status);
        }

        private void OnFsuipcConnected(object sender, EventArgs e)
        {
            // System.Diagnostics.Debug.WriteLine("✅ FSUIPC CONECTADO - Evento recibido en ViewModel");

            double lat = _fsuipc.GetLatitude();
            double lon = _fsuipc.GetLongitude();
            // System.Diagnostics.Debug.WriteLine($"   Posición: {lat}, {lon}");

            _flightManager.SetSimulatorConnected(true, lat, lon);

            OnSimulatorNameChanged?.Invoke(_fsuipc.SimulatorName);
            OnAcarsStatusChanged?.Invoke(true);

            // Forzar validación
            if (_flightManager.ActivePilot != null)
            {
                _flightManager.UpdatePositionValidation(lat, lon);
                OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
            }
        }

        private void OnFsuipcDisconnected(object sender, EventArgs e)
        {
            OnSimulatorNameChanged?.Invoke("AWAITING SIM");
            OnAcarsStatusChanged?.Invoke(false);
            OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
        }

        private void OnFsuipcDataUpdated(object sender, DataUpdatedEventArgs e)
        {
            // Actualizar posición
            OnPositionUpdate?.Invoke($"POS: {e.Latitude:F4}° / {e.Longitude:F4}°");

            // Actualizar FlightManager
            _flightManager?.UpdateTelemetry(
                (int)e.Altitude,
                (int)e.GroundSpeed,
                (int)e.VerticalSpeed,
                e.IsOnGround,
                e.FuelTotal,
                e.Latitude,
                e.Longitude,
                e.IndicatedAirspeed,
                e.FuelFlow,
                e.TransponderCode,
                e.AutopilotMaster,
                e.SimulationZuluTime,
                e.RadarAltitude,
                e.Order
            );

            // Actualizar UI específica
            OnAltitudeChanged?.Invoke((int)e.Altitude);
            OnSpeedChanged?.Invoke((int)e.GroundSpeed);
            OnPhaseChanged?.Invoke(_flightManager.CurrentPhase);
            OnAirStatusChanged?.Invoke(_flightManager.CurrentPhase);

            // Validación de posición
            if (string.IsNullOrEmpty(_flightManager.ActivePirepId))
            {
                _flightManager.UpdatePositionValidation(e.Latitude, e.Longitude);
            }

            OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);

            // Preparar telemetría para servidor
            PrepareTelemetry(e);
        }

        private void PrepareTelemetry(DataUpdatedEventArgs e)
        {
            if (string.IsNullOrEmpty(_flightManager?.ActivePirepId))
                return;

            double? distance = null;
            if (_lastPosition.HasValue)
            {
                distance = CalculateDistance(
                    _lastPosition.Value.lat,
                    _lastPosition.Value.lon,
                    e.Latitude,
                    e.Longitude
                );
            }
            _lastPosition = (e.Latitude, e.Longitude);

            double agl = e.RadarAltitude > 0 ? e.RadarAltitude :
                        (e.Altitude - GetTerrainElevation(_flightManager.CurrentPhase));

            var position = new AcarsPosition
            {
                type = 0,
                nav_type = e.NavType,
                order = e.Order,
                name = GetPhaseName(_flightManager.CurrentPhase),
                status = GetPhpVmsStatusCode(_flightManager.CurrentPhase),
                lat = e.Latitude,
                lon = e.Longitude,
                distance = distance,
                heading = (int)Math.Round(e.Heading, 0),
                altitude = Math.Round(e.Altitude, 0),
                altitude_agl = Math.Round(agl, 0),
                altitude_msl = Math.Round(e.Altitude, 0),
                vs = Math.Round(e.VerticalSpeed, 0),
                gs = (int)Math.Round(e.GroundSpeed, 0),
                ias = (int)Math.Round(e.IndicatedAirspeed, 0),
                transponder = e.TransponderCode,
                autopilot = e.AutopilotMaster,
                fuel_flow = Math.Round(e.FuelFlow, 0),
                fuel = Math.Round(e.FuelTotal, 0),
                sim_time = e.SimulationZuluTime,
                source = "vmsOpenAcars"
            };

            _lastTelemetry = new AcarsPositionUpdate { positions = new[] { position } };
        }

        public void Start()
        {
            _fsuipc?.Start();
            StartTimers();
        }

        public void Stop()
        {
            _fsuipc?.Stop();
        }

        private async void StartTimers()
        {
            while (true)
            {
                await Task.Delay(1000);
                OnTimerTick();
            }
        }

        private async void OnTimerTick()
        {
            UpdateFlightInfo();

            if (!string.IsNullOrEmpty(_flightManager?.ActivePirepId) && _lastTelemetry != null)
            {
                if (DateTime.UtcNow - _lastPositionUpdate >= _positionUpdateInterval)
                {
                    bool success = await _apiService.SendPositionUpdate(
                        _flightManager.ActivePirepId,
                        _lastTelemetry
                    );

                    if (success)
                    {
                        _lastPositionUpdate = DateTime.UtcNow;
                        await _flightManager.UpdatePirepFlightTime();
                    }

                    OnAcarsStatusChanged?.Invoke(success);
                }
            }

            UpdateSimulatorName();
        }

        public void SetActivePlan(SimbriefPlan plan)
        {
            if (plan == null) return;

            _flightManager.SetActivePlan(plan);
            UpdateFlightInfo();

            // Forzar una validación inmediata con la posición actual del simulador
            if (_fsuipc.IsConnected)
            {
                double lat = _fsuipc.GetLatitude();
                double lon = _fsuipc.GetLongitude();
                _flightManager.UpdatePositionValidation(lat, lon);
            }

            // Disparar evento para actualizar UI
            OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
        }

        public void UpdateFlightInfo()
        {
            var plan = _flightManager?.ActivePlan;
            if (plan != null)
            {
                OnFlightNoChanged?.Invoke($"FLT NO: {plan.Airline}{plan.FlightNumber}");
                OnDepArrChanged?.Invoke($"DEP/ARR: {plan.Origin}/{plan.Destination}");
                OnAlternateChanged?.Invoke($"ALTN: {plan.Alternate ?? "N/A"}");
                OnRouteChanged?.Invoke($"ROUTE: {plan.Route}");
                OnAircraftChanged?.Invoke($"ACFT: {plan.AircraftIcao}");
                OnFuelChanged?.Invoke($"FUEL: {plan.BlockFuel:F0} {plan.Units ?? "kg"}");
                OnTypeChanged?.Invoke($"TYPE: {plan.Aircraft}");
                OnRegistrationChanged?.Invoke($"REG: {plan.Registration}");
            }
            else
            {
                OnFlightNoChanged?.Invoke("FLT NO: ----");
                OnDepArrChanged?.Invoke("DEP/ARR: ----/----");
                OnAlternateChanged?.Invoke("ALTN: ----");
                OnRouteChanged?.Invoke("ROUTE: ----");
                OnAircraftChanged?.Invoke("ACFT: ----");
                OnFuelChanged?.Invoke("FUEL: ---- kg");
                OnTypeChanged?.Invoke("TYPE: ----");
                OnRegistrationChanged?.Invoke("REG: ----");
            }
        }

        private void UpdateSimulatorName()
        {
            if (_fsuipc != null && _fsuipc.IsConnected)
                OnSimulatorNameChanged?.Invoke(_fsuipc.GetSimName());
            else
                OnSimulatorNameChanged?.Invoke("AWAITING SIM");
        }

        // ===== MÉTODOS LLAMADOS DESDE MAINFORM =====
        public async Task HandleStartStopButton(string buttonText)
        {
            switch (buttonText)
            {
                case "START":
                    await StartFlight();
                    break;
                case "ABORT":
                    await AbortFlight();
                    break;
                case "SEND PIREP":
                    await SendPirep();
                    break;
            }
        }

        private async Task StartFlight()
        {
            if (!_flightManager.CanStartFlight())
            {
                OnLog?.Invoke("⛔ No se cumplen las condiciones para iniciar el vuelo", Theme.Warning);
                return;
            }

            bool started = await _flightManager.StartFlight(
                _flightManager.ActivePlan,
                _flightManager.ActivePilot
            );

            if (started)
            {
                OnButtonStateChanged?.Invoke("ABORT", Color.Red, true);
                OnLog?.Invoke(_("FlightStarted"), Theme.Success);
            }
        }

        public async Task CancelFlight()
        {
            if (await _flightManager.CancelFlight())
            {
                OnButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), false);
                OnLog?.Invoke("✖️ Vuelo cancelado", Theme.Warning);
            }
        }

        public async Task AbortFlight()
        {
            // Solicitar confirmación a través del evento
            if (OnShowConfirmation != null)
            {
                var result = await OnShowConfirmation(
                    "ABORT FLIGHT?\n\nThis will cancel the current flight and delete the PIREP from the server.\n\nAre you sure?",
                    "CONFIRM ABORT",
                    EcamDialogButtons.YesNo
                );

                if (result != DialogResult.Yes)
                    return;
            }

            if (await _flightManager.AbortFlight())
            {
                OnButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), false);
                OnLog?.Invoke("✖️ Flight aborted", Theme.Warning);
            }
        }

        public async Task SendPirep()
        {
            if (await _flightManager.FilePirep())
            {
                OnButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), false);
                _lastTelemetry = null;
                _lastPositionUpdate = DateTime.MinValue;
                OnLog?.Invoke("✅ Vuelo reportado, listo para siguiente vuelo", Theme.Success);
            }
        }

        public async void Login()
        {
            try
            {
                OnLog?.Invoke("🔑 Iniciando sesión...", Theme.MainText);

                var result = await _apiService.GetPilotData();
                if (result.Data != null)
                {
                    Pilot pilot = result.Data;
                    _flightManager.SetActivePilot(pilot);
                    OnLog?.Invoke($"✅ Login exitoso: {pilot.Name} (Rango: {pilot.Rank})", Theme.Success);
                    OnLog?.Invoke($"📍 Aeropuerto asignado: {pilot.CurrentAirport}", Theme.MainText);
                    OnAirportChanged?.Invoke(pilot.CurrentAirport);

                    OnAcarsStatusChanged?.Invoke(true);

                    if (_fsuipc.IsConnected)
                    {
                        double lat = _fsuipc.GetLatitude();
                        double lon = _fsuipc.GetLongitude();
                        _flightManager.UpdatePositionValidation(lat, lon);
                        OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
                    }
                }
                else
                {
                    OnLog?.Invoke($"❌ Error de login: {result.Error}", Theme.Danger);
                    OnAcarsStatusChanged?.Invoke(false);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Excepción en login: {ex.Message}", Theme.Danger);
                OnAcarsStatusChanged?.Invoke(false);
            }
        }

        public void OpenFlightPlanner()
        {
            if (_flightManager?.ActivePilot == null)
            {
                OnLog?.Invoke("⚠️ Debes iniciar sesión primero", Theme.Warning);
                return;
            }

            OnOpenFlightPlanner?.Invoke();
        }

        public void ShowOFP()
        {
            var plan = _flightManager?.ActivePlan;
            if (plan != null)
            {
                string message = $"OFP: {plan.Route}\nCombustible: {plan.BlockFuel} {plan.Units}";
                OnShowMessage?.Invoke(message, "Operational Flight Plan");
            }
            else
            {
                OnShowMessage?.Invoke("No hay OFP cargado.", "Info");
            }
        }

        public void LogButtonPress(string buttonText)
        {
            OnLog?.Invoke($"🔘 Botón {buttonText} presionado", Theme.MainText);
        }

        // ===== MÉTODOS AUXILIARES =====
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

        private double GetTerrainElevation(FlightPhase phase)
        {
            var plan = _flightManager?.ActivePlan;
            if (plan == null) return 0;

            if (phase == FlightPhase.Descent || phase == FlightPhase.Approach || phase == FlightPhase.Landing)
                return plan.DestinationElevation;
            else if (phase == FlightPhase.Takeoff || phase == FlightPhase.Climb)
                return plan.OriginElevation;

            return 0;
        }

        private string GetPhaseName(FlightPhase phase)
        {
            switch (phase)
            {
                case FlightPhase.Boarding: return "BOARDING";
                case FlightPhase.Pushback: return "PUSHBACK";
                case FlightPhase.TaxiOut: return "TAXI OUT";
                case FlightPhase.Takeoff: return "TAKEOFF";
                case FlightPhase.Climb: return "CLIMB";
                case FlightPhase.Enroute: return "ENROUTE";
                case FlightPhase.Descent: return "DESCENT";
                case FlightPhase.Approach: return "APPROACH";
                case FlightPhase.Landing: return "LANDING";
                case FlightPhase.TaxiIn: return "TAXI IN";
                case FlightPhase.Completed: return "COMPLETED";
                default: return phase.ToString().ToUpper();
            }
        }

        private string GetPhpVmsStatusCode(FlightPhase phase)
        {
            var dict = new System.Collections.Generic.Dictionary<FlightPhase, string>
            {
                [FlightPhase.Boarding] = "BST",
                [FlightPhase.Pushback] = "PBT",
                [FlightPhase.TaxiOut] = "TXI",
                [FlightPhase.TaxiIn] = "TXI",
                [FlightPhase.Takeoff] = "TOF",
                [FlightPhase.Climb] = "ICL",
                [FlightPhase.Enroute] = "ENR",
                [FlightPhase.Descent] = "APR",
                [FlightPhase.Approach] = "FIN",
                [FlightPhase.Landing] = "LDG",
                [FlightPhase.Completed] = "ARR"
            };
            return dict.TryGetValue(phase, out string code) ? code : "INI";
        }
        /// <summary>
        /// Carga los datos de un vuelo seleccionado desde las reservas
        /// </summary>
        public void LoadFlightFromBid(Flight flight)
        {
            if (flight == null) return;

            // Crear un SimbriefPlan básico con los datos del vuelo
            var plan = new SimbriefPlan
            {
                FlightNumber = flight.FlightNumber,
                Airline = flight.Airline,
                Origin = flight.Departure,
                Destination = flight.Arrival,
                Route = flight.Route,
                CruiseAltitude = flight.Level,
                Distance = flight.Distance,
                EstTimeEnroute = flight.FlightTime * 60, // Convertir minutos a segundos
                AircraftIcao = flight.AircraftType,
                Aircraft = flight.AircraftType
            };

            // Guardar el plan en el FlightManager (parcialmente completo)
            _flightManager.SetActivePlan(plan);

            // Actualizar UI
            UpdateFlightInfo();
            OnLog?.Invoke($"📋 Flight data loaded from bid", Theme.MainText);
        }
        public async Task<List<Flight>> LoadPilotBids()
        {
            try
            {
                var activePilot = _flightManager.ActivePilot;
                if (activePilot == null)
                {
                    OnLog?.Invoke("⚠️ No hay piloto activo", Theme.Warning);
                    return new List<Flight>();
                }

                var bids = await _apiService.GetPilotBids();
                return bids ?? new List<Flight>();
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error cargando reservas: {ex.Message}", Theme.Danger);
                return new List<Flight>();
            }
        }
    }
}