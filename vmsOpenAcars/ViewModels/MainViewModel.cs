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
using vmsOpenAcars.Helpers;

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
        private int _lastEngineRpm = 0;

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
        public event Action OnFlightStarted;
        

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
            // Desuscribir eventos de FlightManager
            _flightManager.OnLog -= OnFlightManagerLog;
            _flightManager.PhaseChanged -= OnFlightPhaseChanged;
            _flightManager.OnPositionValidated -= OnPositionValidated;
            _flightManager.OnAirportChanged -= OnAirportChanged;
            _flightManager.OnLandingDetected -= OnLandingDetected;
            _flightManager.OnBlockDetected -= OnBlockDetected;
            _flightManager.OnTakeoffDetected -= OnTakeoffDetected;

            // Desuscribir eventos de FsuipcService
            _fsuipc.DataUpdated -= OnFsuipcDataUpdated;
            _fsuipc.Connected -= OnFsuipcConnected;
            _fsuipc.Disconnected -= OnFsuipcDisconnected;
            _fsuipc.OnTakeoffDetected -= OnTakeoffDetectedHighPrecision;
            _fsuipc.OnTouchdownDetected -= OnTouchdownDetectedHighPrecision;
            _fsuipc.OnGearChanged -= OnGearChanged;
            _fsuipc.OnFlapsChanged -= OnFlapsChanged;
            _fsuipc.OnSpoilersChanged -= OnSpoilersChanged;
            _fsuipc.OnEngineChanged -= OnEngineChanged;
            _fsuipc.OnParkingBrakeChanged -= OnParkingBrakeChanged;

            // Suscribir eventos de FlightManager
            _flightManager.OnLog += OnFlightManagerLog;
            _flightManager.PhaseChanged += OnFlightPhaseChanged;
            _flightManager.OnPositionValidated += OnPositionValidated;
            _flightManager.OnAirportChanged += OnAirportChanged;
            _flightManager.OnLandingDetected += OnLandingDetected;
            _flightManager.OnBlockDetected += OnBlockDetected;
            _flightManager.OnTakeoffDetected += OnTakeoffDetected;

            // Suscribir eventos de FsuipcService
            _fsuipc.DataUpdated += OnFsuipcDataUpdated;
            _fsuipc.Connected += OnFsuipcConnected;
            _fsuipc.Disconnected += OnFsuipcDisconnected;
            _fsuipc.OnTakeoffDetected += OnTakeoffDetectedHighPrecision;
            _fsuipc.OnTouchdownDetected += OnTouchdownDetectedHighPrecision;
            _fsuipc.OnGearChanged += OnGearChanged;
            _fsuipc.OnFlapsChanged += OnFlapsChanged;
            _fsuipc.OnSpoilersChanged += OnSpoilersChanged;
            _fsuipc.OnEngineChanged += OnEngineChanged;
            _fsuipc.OnParkingBrakeChanged += OnParkingBrakeChanged;
        }
        // Eventos de alta precisión
        private void OnTakeoffDetectedHighPrecision(int speed, int altitude, int vs, double pitch, double bank, double heading, double flaps)
        {
            OnLog?.Invoke($"🛫 LIFTOFF @ {speed} kts, VS: {vs} fpm, Pitch: {pitch:F1}°", Theme.Success);

            var takeoffRecord = new AcarsPosition
            {
                type = 0,
                status = "TOF",
                name = "LIFTOFF",
                lat = _flightManager.CurrentLat,
                lon = _flightManager.CurrentLon,
                altitude = altitude,
                gs = speed,
                ias = _flightManager.CurrentIndicatedAirspeed,
                vs = vs,
                pitch = pitch,
                bank = bank,
                heading = (int)heading,
                flaps = flaps,
                sim_time = DateTime.UtcNow,
                source = "vmsOpenAcars"
            };

            Task.Run(async () =>
            {
                var update = new AcarsPositionUpdate { positions = new[] { takeoffRecord } };
                await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, update);
            });
        }

        private void OnTouchdownDetectedHighPrecision(int vs, int speed, int altitude, double pitch, double bank, double heading, bool spoilers, double flaps, int gear)
        {
            double gforce = CalculateGForceFromVS(vs);

            OnLog?.Invoke($"🛬 TOUCHDOWN @ {speed} kts, VS: {vs} fpm, G: {gforce:F2}g", Theme.Success);

            var landingRecord = new AcarsPosition
            {
                type = 0,
                status = "LDG",
                name = "TOUCHDOWN",
                lat = _flightManager.CurrentLat,
                lon = _flightManager.CurrentLon,
                altitude = altitude,
                gs = speed,
                vs = vs,
                gforce = gforce,
                pitch = pitch,
                bank = bank,
                heading = (int)heading,
                spoilers = spoilers ? 1 : 0,
                flaps = flaps,
                gear = gear,
                sim_time = DateTime.UtcNow,
                source = "vmsOpenAcars"
            };

            Task.Run(async () =>
            {
                var update = new AcarsPositionUpdate { positions = new[] { landingRecord } };
                await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, update);
            });
        }

        private void OnGearChanged(int oldPos, int newPos)
        {
            string status = newPos == 1 ? "DOWN" : "UP";
            OnLog?.Invoke($"🛬 Gear {status}", Theme.MainText);
        }

        private void OnFlapsChanged(double oldPos, double newPos)
        {
            OnLog?.Invoke($"🛫 Flaps: {oldPos:F0}% → {newPos:F0}%", Theme.SecondaryText);
        }

        private void OnSpoilersChanged(bool deployed)
        {
            OnLog?.Invoke($"🛫 Spoilers: {(deployed ? "DEPLOYED" : "RETRACTED")}", Theme.Warning);
        }

        private void OnEngineChanged(int rpm)
        {
            if (rpm > 100 && _lastEngineRpm <= 100)
                OnLog?.Invoke($"🔄 Engines started", Theme.Success);
            else if (rpm <= 100 && _lastEngineRpm > 100)
                OnLog?.Invoke($"🔄 Engines shutdown", Theme.Warning);
            _lastEngineRpm = rpm;
        }

        private void OnParkingBrakeChanged(bool engaged)
        {
            string status = engaged ? "SET" : "RELEASED";
            OnLog?.Invoke($"🅿️ Parking Brake: {status}", Theme.MainText);
        }

        private double CalculateGForceFromVS(int vsFpm)
        {
            int vs = Math.Abs(vsFpm);

            // Tabla más precisa basada en datos reales
            if (vs <= 50) return 1.00;
            if (vs <= 100) return 1.05;
            if (vs <= 150) return 1.12;
            if (vs <= 200) return 1.20;  // 187 fpm -> ~1.20g
            if (vs <= 250) return 1.30;
            if (vs <= 300) return 1.40;
            if (vs <= 350) return 1.55;
            if (vs <= 400) return 1.70;
            if (vs <= 500) return 1.90;
            if (vs <= 600) return 2.10;
            if (vs <= 700) return 2.30;
            if (vs <= 800) return 2.50;
            if (vs <= 900) return 2.70;
            return 3.00;
        }

        private void OnTakeoffDetected(int speed, int altitude, int verticalSpeed)
        {
            OnLog?.Invoke($"🛫 TAKEOFF DETECTED - Speed: {speed} kts, Alt: {altitude} ft, VS: {verticalSpeed} fpm",
                Theme.Success);

            // Crear registro ACARS para el despegue
            var takeoffRecord = new AcarsPosition
            {
                type = 0,
                status = "TOF",
                name = "TAKEOFF",
                lat = _flightManager.CurrentLat,
                lon = _flightManager.CurrentLon,
                altitude = altitude,
                gs = speed,
                vs = verticalSpeed,
                sim_time = DateTime.UtcNow,
                source = "vmsOpenAcars"
            };

            Task.Run(async () =>
            {
                if (!string.IsNullOrEmpty(_flightManager.ActivePirepId))
                {
                    var update = new AcarsPositionUpdate {positions = new[] {takeoffRecord}};
                    await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, update);
                }
            });
        }

        private void OnBlockDetected()
        {
            OnLog?.Invoke($"🅿️ ON BLOCK DETECTED", Theme.Success);

            // Crear registro ACARS para OnBlock
            var blockRecord = new AcarsPosition
            {
                type = 0,
                status = "ARR",
                name = "ON BLOCK",
                lat = _flightManager.CurrentLat,
                lon = _flightManager.CurrentLon,
                altitude = _flightManager.CurrentAltitude,
                heading = (int)_fsuipc.GetHeading(),
                sim_time = DateTime.UtcNow,
                source = "vmsOpenAcars"
            };

            Task.Run(async () =>
            {
                if (!string.IsNullOrEmpty(_flightManager.ActivePirepId))
                {
                    var update = new AcarsPositionUpdate { positions = new[] { blockRecord } };
                    await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, update);
                }
            });
        }

        // Método que maneja el aterrizaje
        private void OnLandingDetected(int verticalSpeed, double gforce, double pitch, double bank)
        {
            // Crear registro ACARS especial para el aterrizaje
            var landingRecord = new AcarsPosition
            {
                type = 0,                           // FLIGHT_PATH
                status = "LDG",                     // Landing status
                nav_type = 0,
                order = _flightManager.PositionOrder + 1,
                name = "TOUCHDOWN",
                lat = _flightManager.CurrentLat,
                lon = _flightManager.CurrentLon,
                altitude = _flightManager.CurrentAltitude,
                altitude_agl = 0,                   // En el momento del touchdown
                heading = (int)_fsuipc.GetHeading(),
                vs = verticalSpeed,
                gs = _flightManager.CurrentGroundSpeed,
                ias = _flightManager.CurrentIndicatedAirspeed,
                gforce = gforce,                    // Campo nuevo
                pitch = pitch,                      // Campo nuevo
                bank = bank,                        // Campo nuevo
                sim_time = DateTime.UtcNow,
                source = "vmsOpenAcars"
            };

            // Enviar inmediatamente al servidor
            Task.Run(async () =>
            {
                var update = new AcarsPositionUpdate { positions = new[] { landingRecord } };
                await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, update);

                OnLog?.Invoke($"🛬 Landing recorded: {verticalSpeed} fpm, {gforce:F2} G, Heading: {(int)_fsuipc.GetHeading()}°, Pitch: {pitch:F1}°, Bank: {bank:F1}°", Theme.Success);
            });
        }
        private void OnFlightManagerLog(string msg, Color color)
        {
            OnLog?.Invoke(msg, color);
        }

        private void OnFlightPhaseChanged(FlightPhase phase)
        {
            OnPhaseChanged?.Invoke(phase);

            if (phase == FlightPhase.OnBlock || phase == FlightPhase.Completed)
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
                e.Order,
                e.Pitch,   
                e.Bank
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

            double totalDistanceKm = _flightManager.TotalDistanceKm;

            double? incrementalDistance = null;
            if (_lastPosition.HasValue)
            {
                double distKm = _flightManager.CalculateDistanceKm(
                    _lastPosition.Value.lat,
                    _lastPosition.Value.lon,
                    e.Latitude,
                    e.Longitude
                );

                if (distKm > 0.001)
                {
                    incrementalDistance = Math.Round(distKm, 3);
                }
            }
            _lastPosition = (e.Latitude, e.Longitude);

            double agl = e.RadarAltitude > 0 ? e.RadarAltitude :
                (e.Altitude - GetTerrainElevation(_flightManager.CurrentPhase));

            var position = new AcarsPosition
            {
                type = 0,
                nav_type = e.NavType,  // AHORA SÍ EXISTE
                order = e.Order,
                name = GetPhaseName(_flightManager.CurrentPhase),
                status = FlightPhaseHelper.GetStatusCode(_flightManager.CurrentPhase),
                lat = e.Latitude,
                lon = e.Longitude,
                distance = Math.Round(totalDistanceKm, 2),
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
                        await _flightManager.UpdateFlightProgress();
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

        // En MainViewModel.cs - StartFlight()

        private async Task StartFlight()
        {
            if (!_flightManager.CanStartFlight())
            {
                OnLog?.Invoke("⛔ No se cumplen las condiciones para iniciar el vuelo", Theme.Warning);
                return;
            }

            var plan = _flightManager.ActivePlan;
            if (plan == null)
            {
                OnLog?.Invoke("⛔ No flight plan loaded", Theme.Warning);
                return;
            }

            // ===== VALIDAR COMBUSTIBLE ANTES DE PREFILE =====
            double actualFuel = 0;
            bool simulatorConnected = _fsuipc.IsConnected;

            if (!simulatorConnected)
            {
                OnLog?.Invoke("❌ Simulator not connected. Cannot verify fuel quantity.", Theme.Danger);
                OnLog?.Invoke("⛔ Please connect simulator before starting flight.", Theme.Warning);
                return;
            }

            // Obtener combustible actual del simulador
            actualFuel = _fsuipc.GetTotalFuel();
            double plannedFuel = plan.BlockFuel;

            OnLog?.Invoke($"⛽ Simulator fuel: {actualFuel:F0} {plan.Units ?? "kg"}", Theme.MainText);
            OnLog?.Invoke($"📋 Planned fuel: {plannedFuel:F0} {plan.Units ?? "kg"}", Theme.MainText);

            // Calcular diferencia
            double difference = actualFuel - plannedFuel;
            double differenceAbs = Math.Abs(difference);
            double differencePercent = (differenceAbs / plannedFuel) * 100;

            // Validar dentro de tolerancia
            bool fuelValid = IsFuelWithinTolerance(plannedFuel, actualFuel);

            if (!fuelValid)
            {
                string warningMessage = $"❌ FUEL VALIDATION FAILED\n\n" +
                                        $"Planned fuel: {plannedFuel:F0} {plan.Units ?? "kg"}\n" +
                                        $"Simulator fuel: {actualFuel:F0} {plan.Units ?? "kg"}\n" +
                                        $"Difference: {differenceAbs:F0} {plan.Units ?? "kg"} ({differencePercent:F1}%)\n\n" +
                                        $"Fuel must be within {Constants.FuelTolerancePercent * 100}% or {Constants.FuelToleranceAbsolute} {plan.Units ?? "kg"} of the flight plan.\n\n" +
                                        $"Please adjust fuel in the simulator to match the flight plan.";

                OnLog?.Invoke($"❌ Fuel validation failed", Theme.Danger);

                if (OnShowConfirmation != null)
                {
                    await OnShowConfirmation(warningMessage, "FUEL ERROR", EcamDialogButtons.OK);
                }
                return;
            }

            // Mostrar información de combustible válido
            string diffSymbol = difference > 0 ? "+" : "";
            OnLog?.Invoke($"✅ Fuel validation passed: {diffSymbol}{difference:F0} {plan.Units ?? "kg"} ({diffSymbol}{differencePercent:F1}%)", Theme.Success);

            // Si hay diferencia pequeña, mostrar advertencia pero permitir
            if (differenceAbs > 0)
            {
                OnLog?.Invoke($"ℹ️ Fuel difference within tolerance. Continuing with flight.", Theme.MainText);
            }

            // ===== AHORA SÍ, CREAR EL PIREP =====
            bool started = await _flightManager.StartFlight(
                plan,
                _flightManager.ActivePilot,
                actualFuel  // Pasar el combustible real
            );

            if (started)
            {
                OnButtonStateChanged?.Invoke("ABORT", Color.Red, true);
                OnLog?.Invoke(_("FlightStarted"), Theme.Success);
                OnFlightStarted?.Invoke();
            }
        }

        /// <summary>
        /// Valida si el combustible real está dentro de la tolerancia permitida
        /// </summary>
        private bool IsFuelWithinTolerance(double plannedFuel, double actualFuel)
        {
            if (actualFuel <= 0) return false;
            if (plannedFuel <= 0) return false;

            double difference = Math.Abs(actualFuel - plannedFuel);
            double differencePercent = (difference / plannedFuel) * 100;

            // Verificar tolerancia: dentro del porcentaje O dentro del valor absoluto
            bool withinPercent = differencePercent <= (Constants.FuelTolerancePercent * 100);
            bool withinAbsolute = difference <= Constants.FuelToleranceAbsolute;

            return withinPercent || withinAbsolute;
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
                OnLog?.Invoke(L._("LoggingIn"), Theme.MainText);

                var result = await _apiService.GetPilotData();
                if (result.Data != null)
                {
                    Pilot pilot = result.Data;
                    _flightManager.SetActivePilot(pilot);
                    OnLog?.Invoke(string.Format(L._("LoginSuccess"), pilot.Name, pilot.Rank), Theme.Success);
                    OnLog?.Invoke($"{L._("AirportAssigned")}: {pilot.CurrentAirport}", Theme.MainText);
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
                Alternate = "",
                Route = flight.Route,
                CruiseAltitude = flight.Level,
                Distance = flight.Distance,
                EstTimeEnroute = flight.FlightTime * 60,
                AircraftIcao = flight.AircraftType,
                Aircraft = flight.AircraftType,
                BlockFuel = 0,    
                ZeroFuelWeight = 0, 
                PayLoad = 0    
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
        // En MainViewModel.cs - añadir método para mostrar estado de combustible

        private async Task<bool> ValidateAndShowFuelStatus()
        {
            var plan = _flightManager.ActivePlan;
            if (plan == null) return false;

            if (!_fsuipc.IsConnected)
            {
                OnLog?.Invoke("❌ Simulator not connected. Cannot verify fuel.", Theme.Danger);
                return false;
            }

            double actualFuel = _fsuipc.GetTotalFuel();
            double plannedFuel = plan.BlockFuel;
            double difference = actualFuel - plannedFuel;
            double differenceAbs = Math.Abs(difference);
            double differencePercent = (differenceAbs / plannedFuel) * 100;

            bool isValid = IsFuelWithinTolerance(plannedFuel, actualFuel);

            string statusIcon = isValid ? "✅" : "❌";
            Color statusColor = isValid ? Theme.Success : Theme.Danger;

            string diffSymbol = difference > 0 ? "+" : "";
            string diffText = difference == 0 ? "exact match" : $"{diffSymbol}{difference:F0} ({diffSymbol}{differencePercent:F1}%)";

            OnLog?.Invoke($"{statusIcon} Fuel validation: {actualFuel:F0} vs {plannedFuel:F0} = {diffText}", statusColor);

            if (!isValid)
            {
                OnLog?.Invoke($"📏 Tolerance: {Constants.FuelTolerancePercent * 100}% or {Constants.FuelToleranceAbsolute} {plan.Units ?? "kg"}", Theme.Warning);
            }

            return isValid;
        }
    }
}