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
        private readonly MetarService _metarService = new MetarService();
        private readonly IvaoService  _ivaoService  = new IvaoService();
        private DateTime _lastPositionUpdate = DateTime.MinValue;
        private AcarsPosition _lastSentPosition = null;
        private readonly TimeSpan _positionUpdateInterval = TimeSpan.FromSeconds(5);
        private AcarsPositionUpdate _lastTelemetry;
        private (double lat, double lon)? _lastPosition;
        private int _lastEngineRpm = 0;
        private bool _aircraftInfoShown = false;

        // Eventos para comunicación con la UI (sin cambios)
        public event Action<string, Color> OnLog;
        public event Action<string> OnPositionUpdate;
        public event Action<FlightPhase> OnPhaseChanged;
        public event Action<FlightPhase> OnAirStatusChanged;
        public event Action<ValidationStatus> OnValidationStatusChanged;
        public event Action OnFlightInfoChanged;
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
        public event Action OnFlightEnded;
        public event Action<MetarData[]> OnMetarUpdated;

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

            // Desuscribir eventos de FsuipcService (nuevos)
            _fsuipc.TelemetryUpdated -= OnTelemetryUpdated;
            _fsuipc.Connected -= OnFsuipcConnected;
            _fsuipc.Disconnected -= OnFsuipcDisconnected;
            _fsuipc.TakeoffDetected -= OnTakeoffDetectedEvent;
            _fsuipc.TouchdownDetected -= OnTouchdownDetectedEvent;
            _fsuipc.GearChanged -= OnGearChanged;
            _fsuipc.FlapsChanged -= OnFlapsChanged;
            _fsuipc.SpoilersChanged -= OnSpoilersChanged;
            _fsuipc.ParkingBrakeChanged -= OnParkingBrakeChanged;
            _fsuipc.EnginesChanged -= OnEnginesChanged;
            _fsuipc.RawDataUpdated -= OnRawDataUpdated;

            // Suscribir eventos de FlightManager
            _flightManager.OnLog += OnFlightManagerLog;
            _flightManager.PhaseChanged += OnFlightPhaseChanged;
            _flightManager.OnPositionValidated += OnPositionValidated;
            _flightManager.OnAirportChanged += OnAirportChanged;
            _flightManager.OnLandingDetected += OnLandingDetected;
            _flightManager.OnBlockDetected += OnBlockDetected;
            _flightManager.OnTakeoffDetected += OnTakeoffDetected;

            // Suscribir eventos de FsuipcService
            _fsuipc.TelemetryUpdated += OnTelemetryUpdated;
            _fsuipc.RawDataUpdated += OnRawDataUpdated;
            _fsuipc.Connected += OnFsuipcConnected;
            _fsuipc.Disconnected += OnFsuipcDisconnected;
            _fsuipc.TakeoffDetected += OnTakeoffDetectedEvent;
            _fsuipc.TouchdownDetected += OnTouchdownDetectedEvent;
            _fsuipc.GearChanged += OnGearChanged;
            _fsuipc.FlapsChanged += OnFlapsChanged;
            _fsuipc.SpoilersChanged += OnSpoilersChanged;
            _fsuipc.ParkingBrakeChanged += OnParkingBrakeChanged;
            _fsuipc.EnginesChanged += OnEnginesChanged;
            _fsuipc.OnAircraftInfoReady += OnAircraftInfoReady;
            _fsuipc.NavLightChanged += on => OnLog?.Invoke(
                on ? "💡 NAV lights ON" : "💡 NAV lights OFF", Theme.MainText);

            _fsuipc.StrobeLightChanged += on => OnLog?.Invoke(
                on ? "💡 STROBE lights ON" : "💡 STROBE lights OFF", Theme.MainText);

            _fsuipc.LandingLightChanged += on => OnLog?.Invoke(
                on ? "💡 LANDING lights ON" : "💡 LANDING lights OFF", Theme.MainText);

            // Beacon ya existente — ampliar para log:
            _fsuipc.BeaconChanged += on => OnLog?.Invoke(
                on ? "🔴 BEACON ON" : "🔴 BEACON OFF", Theme.MainText);

            _metarService.OnMetarUpdated += metars => OnMetarUpdated?.Invoke(metars);
        }
        private int _lastUiAltitude;
        private int _lastUiSpeed;
        private string _lastUiPhase = string.Empty;
        private string _lastUiPosition = string.Empty;
        private FlightPhase _prevPhase = FlightPhase.Idle;

        private void OnRawDataUpdated(object sender, RawTelemetryData e)
        {
            _flightManager?.UpdateTelemetry(e);   // ← ver C1, pasa el objeto completo
            _metarService?.UpdatePosition(e.Latitude, e.Longitude);

            // Solo notificar UI si hay cambio real (threshold para evitar micro-fluctuaciones)
            bool altChanged = Math.Abs((int)e.AltitudeFeet - _lastUiAltitude) > 10;
            bool speedChanged = Math.Abs((int)e.GroundSpeedKt - _lastUiSpeed) > 1;
            string posStr = $"{e.Latitude:F4}/{e.Longitude:F4}";
            bool posChanged = posStr != _lastUiPosition;
            string phaseStr = _flightManager?.CurrentPhase.ToString() ?? string.Empty;
            bool phaseChanged = phaseStr != _lastUiPhase;

            if (altChanged || speedChanged || posChanged || phaseChanged)
            {
                _lastUiAltitude = (int)e.AltitudeFeet;
                _lastUiSpeed = (int)e.GroundSpeedKt;
                _lastUiPosition = posStr;
                _lastUiPhase = phaseStr;
                OnFlightInfoChanged?.Invoke();
            }
        }
        private void OnAircraftInfoReady()
        {
            // Evitar mostrar múltiples veces
            if (_aircraftInfoShown) return;
            _aircraftInfoShown = true;

            // Mostrar primero el simulador (ya se muestra en OnFsuipcConnected)

            // Mostrar fabricante
            if (_fsuipc.AircraftManufacturer != "Unknown")
            {
                OnLog?.Invoke($"🏭 Manufacturer: {_fsuipc.AircraftManufacturer}", Theme.SecondaryText);
            }

            // Mostrar ICAO
            if (_fsuipc.AircraftIcao != "????")
            {
                OnLog?.Invoke($"📋 ICAO: {_fsuipc.AircraftIcao}", Theme.SecondaryText);
            }

            // Mostrar aeronave (título completo)
            if (!string.IsNullOrEmpty(_fsuipc.AircraftTitle) && _fsuipc.AircraftTitle != "Unknown")
            {
                OnLog?.Invoke($"✈️ Aircraft: {_fsuipc.AircraftTitle}", Theme.MainText);
            }

            // Mostrar livery (extraída correctamente)
            string livery = _fsuipc.GetAircraftLivery();
            if (livery != "Unknown" && livery != _fsuipc.AircraftIcao)
            {
                OnLog?.Invoke($"🎨 Livery: {livery}", Theme.SecondaryText);
            }
            // ===== MOSTRAR ALTITUD ACTUAL =====
            OnLog?.Invoke($"📏 XXX Altitude: {_fsuipc.CurrentAltitudeFeet:F0} ft", Theme.MainText);
        }

        // ========== EVENTOS DE FLIGHTMANAGER (sin cambios) ==========
        private void OnFlightManagerLog(string msg, Color color) => OnLog?.Invoke(msg, color);
        private async void OnFlightPhaseChanged(FlightPhase phase)
        {
            // Verificar IVAO al salir de Boarding (blocks-off real)
            if (_prevPhase == FlightPhase.Boarding &&
                (phase == FlightPhase.Pushback || phase == FlightPhase.TaxiOut))
            {
                await CheckIvaoAtBlocksOffAsync();
            }
            _prevPhase = phase;

            OnPhaseChanged?.Invoke(phase);
            if (phase == FlightPhase.OnBlock || phase == FlightPhase.Completed)
                OnButtonStateChanged?.Invoke("SEND PIREP", Color.Green, true);
        }

        private async Task CheckIvaoAtBlocksOffAsync()
        {
            var pilot = _flightManager.ActivePilot;
            if (pilot?.IvaoId <= 0) return;

            bool? isOnline = await _ivaoService.IsOnlineAsync(pilot.IvaoId);
            if (isOnline == true)
            {
                OnLog?.Invoke($"✅ Conectado en IVAO (VID {pilot.IvaoId})", Theme.Success);
            }
            else if (isOnline == false)
            {
                OnLog?.Invoke($"⚠️ VID {pilot.IvaoId} no detectado en IVAO — −5 pts aplicados", Theme.Warning);
                _flightManager.MarkOfflineFlight();
            }
            else
            {
                OnLog?.Invoke("⚠️ No se pudo verificar presencia en IVAO (feed no disponible)", Theme.Warning);
            }
        }
        private void OnPositionValidated(ValidationStatus status) => OnValidationStatusChanged?.Invoke(status);
        private void OnTakeoffDetected(int speed, int altitude, int verticalSpeed)
        {
            OnLog?.Invoke($"🛫 TAKEOFF DETECTED - Speed: {speed} kts, Alt: {altitude} ft, VS: {verticalSpeed} fpm", Theme.Success);
            // (opcional: crear registro ACARS)
        }
        private void OnBlockDetected()
        {
            OnLog?.Invoke($"🅿️ ON BLOCK DETECTED", Theme.Success);
            var blockRecord = new AcarsPosition
            {
                type = 0,
                status = "ARR",
                name = "ON BLOCK",
                lat = _flightManager.CurrentLat,
                lon = _flightManager.CurrentLon,
                altitude = _flightManager.CurrentAltitude,
                heading = (int)_fsuipc.CurrentHeading,
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
        private void OnLandingDetected(int verticalSpeed, double gforce, double pitch, double bank)
        {
            var landingRecord = new AcarsPosition
            {
                type = 0,
                status = "LDG",
                nav_type = 0,
                name = "TOUCHDOWN",
                lat = _flightManager.CurrentLat,
                lon = _flightManager.CurrentLon,
                altitude = _flightManager.CurrentAltitude,
                altitude_agl = 0,
                heading = (int)_fsuipc.CurrentHeading,
                vs = verticalSpeed,
                gs = _flightManager.CurrentGroundSpeed,
                ias = _flightManager.CurrentIndicatedAirspeed,
                gforce = gforce,
                pitch = pitch,
                bank = bank,
                sim_time = DateTime.UtcNow,
                source = "vmsOpenAcars"
            };
            Task.Run(async () =>
            {
                var update = new AcarsPositionUpdate { positions = new[] { landingRecord } };
                await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, update);
                OnLog?.Invoke($"🛬 Landing recorded: {verticalSpeed} fpm, {gforce:F2} G, Heading: {(int)_fsuipc.CurrentHeading}°, Pitch: {pitch:F1}°, Bank: {bank:F1}°", Theme.Success);
            });
        }

        // ========== EVENTOS DE FSUIPCSERVICE (nuevos) ==========
        private void OnTelemetryUpdated(object sender, TelemetryData e)
        {
            // Esto ahora es SOLO para UI y envío a phpVMS
            // La actualización de FlightManager ya la hace OnRawDataUpdated

            // Actualizar UI (posición, altitudes, etc.)
            OnPositionUpdate?.Invoke($"{e.Latitude:F3}/{e.Longitude:F3}");
            OnPhaseChanged?.Invoke(_flightManager.CurrentPhase);
            OnAirStatusChanged?.Invoke(_flightManager.CurrentPhase);

            // Validación de posición
            if (string.IsNullOrEmpty(_flightManager.ActivePirepId))
                _flightManager.UpdatePositionValidation(e.Latitude, e.Longitude);

            OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);

            // Preparar telemetría para servidor
            PrepareTelemetry(e);
        }

        // ---------------------------------------------------------------------------
        // PrepareTelemetry
        // ---------------------------------------------------------------------------
        // AGL = MSL − elevación del aeropuerto de referencia de la fase actual.
        // RadarAltitudeFeet del FSUIPC ya viene en feet (corregido en FsuipcService).
        // Se usa el radar altimeter SOLO si está disponible Y el avión está en el aire
        // (en tierra el radar alt puede ser 0 o ruidoso).
        // En todos los demás casos, se calcula AGL como MSL − elevación de referencia.
        private void PrepareTelemetry(TelemetryData e)
        {
            if (string.IsNullOrEmpty(_flightManager?.ActivePirepId))
                return;

            double totalDistanceKm = _flightManager.TotalDistanceKm;

            System.Diagnostics.Debug.WriteLine($"[DEBUG] PrepareTelemetry - CurrentPhase: {_flightManager.CurrentPhase}");

            if (_lastPosition.HasValue)
            {
                double distKm = _flightManager.CalculateDistanceKm(
                    _lastPosition.Value.lat, _lastPosition.Value.lon,
                    e.Latitude, e.Longitude);
                // filtrar saltos imposibles (>10 km entre ticks = dato corrupto)
                if (distKm > 0.001 && distKm < 10.0)
                    totalDistanceKm = _flightManager.TotalDistanceKm; // ya acumulado en FlightManager
            }
            _lastPosition = (e.Latitude, e.Longitude);

            // ---- Cálculo AGL ----
            // Elevación del aeropuerto de referencia para la fase actual
            double refElevation = GetTerrainElevation(_flightManager.CurrentPhase);

            // AGL intencional = MSL − elevación aeropuerto de referencia
            // Este valor es el que tiene sentido para detección de fases en zonas montañosas
            double aglRelative = e.AltitudeFeet - refElevation;

            // El radar altimeter del FSUIPC (ya en feet) se envía como altitude_agl
            // si está disponible y el avión está en el aire (>0).
            // Si no está disponible, usamos el AGL relativo al aeropuerto.
            bool radarAvailable = !e.IsOnGround && e.RadarAltitudeFeet > 0.0;
            double aglFinal = radarAvailable ? e.RadarAltitudeFeet : Math.Max(0.0, aglRelative);

            // ---- Construcción de AcarsPosition ----
            var position = new AcarsPosition
            {
                type = 0,
                nav_type = e.NavType,
                order = e.Order,
                name = GetPhaseName(_flightManager.CurrentPhase),
                status = FlightPhaseHelper.GetStatusCode(_flightManager.CurrentPhase),
                lat = e.Latitude,
                lon = e.Longitude,
                distance = Math.Round(totalDistanceKm, 2),
                heading = (int)Math.Round(e.HeadingDeg, 0),

                // altitude     = MSL en feet (lo que phpVMS llama "altitude")
                altitude = Math.Round(e.AltitudeFeet, 0),

                // altitude_agl = AGL en feet (radar si disponible, sino MSL−ref)
                altitude_agl = Math.Round(aglFinal, 0),

                // altitude_msl = MSL en feet (siempre)
                altitude_msl = Math.Round(e.AltitudeFeet, 0),

                vs = Math.Round(e.VerticalSpeedFpm, 0),
                gs = (int)Math.Round(e.GroundSpeedKt, 0),
                ias = (int)Math.Round(e.IndicatedAirspeedKt, 0),
                transponder = e.Transponder,
                autopilot = e.AutopilotEngaged,

                // Combustible en lbs (unidad estándar phpVMS para fuel en posiciones ACARS)
                fuel = Math.Round(e.FuelLbs, 1),           // Combustible en lbs
                pitch = e.PitchDeg,                        // Pitch en grados
                bank = e.BankDeg,                          // Bank en grados

                // Fecha/hora siempre UTC, formato ISO 8601 por el serializador JSON
                sim_time = DateTime.UtcNow,
                source = "vmsOpenAcars"
            };
            if (HasSignificantChange(position))
            {
                _lastSentPosition = position;
                _lastTelemetry = new AcarsPositionUpdate { positions = new[] { position } };
            }
        }
        /// <summary>
        /// Determina si la nueva posición tiene cambios significativos respecto a la última enviada
        /// </summary>
        private bool HasSignificantChange(AcarsPosition newPos)
        {
            if (_lastSentPosition == null) return true;

            // Umbrales para considerar un cambio significativo
            const double positionThreshold = 0.0003;   // ~30 metros (más sensible)
            const int headingThreshold = 5;             // 5 grados
            const int altitudeThreshold = 30;           // 30 pies
            const int speedThreshold = 5;               // 5 nudos
            const int vsThreshold = 100;                // 100 fpm

            bool positionChanged = Math.Abs(newPos.lat - _lastSentPosition.lat) > positionThreshold ||
                                   Math.Abs(newPos.lon - _lastSentPosition.lon) > positionThreshold;

            bool headingChanged = Math.Abs((newPos.heading ?? 0) - (_lastSentPosition.heading ?? 0)) > headingThreshold;
            bool altitudeChanged = Math.Abs((newPos.altitude ?? 0) - (_lastSentPosition.altitude ?? 0)) > altitudeThreshold;
            bool speedChanged = Math.Abs((newPos.gs ?? 0) - (_lastSentPosition.gs ?? 0)) > speedThreshold;
            bool vsChanged = Math.Abs((newPos.vs ?? 0) - (_lastSentPosition.vs ?? 0)) > vsThreshold;
            bool phaseChanged = newPos.status != _lastSentPosition.status;

            return positionChanged || headingChanged || altitudeChanged || speedChanged ||
                   vsChanged || phaseChanged;
        }
        private void OnTakeoffDetectedEvent(object sender, TakeoffData data)
        {
            OnLog?.Invoke($"🛫 ACCURATE TAKEOFF DATA:", Theme.Success);
            OnLog?.Invoke($"   Rotation Speed: {data.RotationIasKt:F0} kts", Theme.MainText);
            OnLog?.Invoke($"   Ground Speed: {data.GroundSpeedKt:F0} kts", Theme.MainText);
            OnLog?.Invoke($"   Pitch: {data.PitchDeg:F1}° | Bank: {data.BankDeg:F1}°", Theme.MainText);
            OnLog?.Invoke($"   Heading: {data.HeadingDeg:F0}°", Theme.MainText);

            if (data.EngineType == "N1")
            {
                OnLog?.Invoke($"   N1: {data.Eng1N1Pct:F0}% / {data.Eng2N1Pct:F0}%", Theme.MainText);
            }
            else if (data.EngineType == "PROP RPM")
            {
                OnLog?.Invoke($"   Prop RPM: {data.Eng1Rpm:F0} / {data.Eng2Rpm:F0}", Theme.MainText);
            }
            else if (data.EngineType == "PISTON RPM")
            {
                OnLog?.Invoke($"   RPM: {data.Eng1Rpm:F0} / {data.Eng2Rpm:F0}", Theme.MainText);
            }

            OnLog?.Invoke($"   Flaps: {data.FlapsPosition * 100:F0}%", Theme.MainText);
            OnLog?.Invoke($"   OAT: {data.OatCelsius:F0}°C | Wind: {data.WindSpeedKt:F0}@{data.WindDirDeg:F0}°", Theme.MainText);
        }

        private void OnTouchdownDetectedEvent(object sender, TouchdownData data)
        {
            string rating = data.GForceAtTouch < 1.3 ? "Perfect" : (data.GForceAtTouch < 1.8 ? "Normal" : (data.GForceAtTouch < 2.5 ? "Hard" : "Crash"));
            OnLog?.Invoke($"🛬 ACCURATE TOUCHDOWN DATA:", Theme.Success);
            OnLog?.Invoke($"   VS: {data.VerticalSpeedFpm:F0} fpm", Theme.MainText);
            OnLog?.Invoke($"   G-Force: {data.GForceAtTouch:F2}g ({rating})", Theme.MainText);
            OnLog?.Invoke($"   Speed: {data.IasKt:F0} kts (IAS) / {data.GroundSpeedKt:F0} kts (GS)", Theme.MainText);
            OnLog?.Invoke($"   Pitch: {data.PitchDeg:F1}° | Bank: {data.BankDeg:F1}°", Theme.MainText);
            OnLog?.Invoke($"   Flaps: {data.FlapsPosition * 100:F0}% | Spoilers: {data.SpoilersPosition * 100:F0}%", Theme.MainText);
            OnLog?.Invoke($"   Reversers: {data.Eng1ReverserPct:F0}% / {data.Eng2ReverserPct:F0}%", Theme.MainText);
            OnLog?.Invoke($"   Brakes: L={data.BrakeLeft * 100:F0}% R={data.BrakeRight * 100:F0}% | Autobrake: {GetAutobrakeName(data.AutobrakeSetting)}", Theme.MainText);
            OnLog?.Invoke($"   OAT: {data.OatCelsius:F0}°C | Wind: {data.WindSpeedKt:F0}@{data.WindDirDeg:F0}°", Theme.MainText);
        }

        private string GetAutobrakeName(int setting)
        {
            switch (setting)
            {
                case 0: return "RTO";
                case 1: return "OFF";
                case 2: return "1";
                case 3: return "2";
                case 4: return "3";
                case 5: return "MAX";
                default: return setting.ToString();
            }
        }

        private void OnGearChanged(int oldPos, int newPos)
        {
            string status = newPos == 1 ? "DOWN" : "UP";
            OnLog?.Invoke($"🛬 Gear {status}", Theme.MainText);
        }

        private void OnFlapsChanged(double oldPercent, double newPercent)
        {
            OnLog?.Invoke($"🛫 Flaps: {oldPercent:F0}% → {newPercent:F0}%", Theme.SecondaryText);
        }

        private void OnSpoilersChanged(bool deployed)
        {
            OnLog?.Invoke($"🛫 Spoilers: {(deployed ? "DEPLOYED" : "RETRACTED")}", Theme.Warning);
        }

        private void OnParkingBrakeChanged(bool engaged)
        {
            string status = engaged ? "SET" : "RELEASED";
            OnLog?.Invoke($"🅿️ Parking Brake: {status}", Theme.MainText);
        }

        private void OnEnginesChanged(bool running)
        {
            if (running)
                OnLog?.Invoke($"🔄 Engines started", Theme.Success);
            else
                OnLog?.Invoke($"🔄 Engines shutdown", Theme.Warning);
        }

        // ========== CONEXIÓN FSUIPC ==========
        private void OnFsuipcConnected(object sender, EventArgs e)
        {
            double lat = _fsuipc.CurrentLatitude;
            double lon = _fsuipc.CurrentLongitude;
            _flightManager.SetSimulatorConnected(true, lat, lon);
            OnSimulatorNameChanged?.Invoke(_fsuipc.SimulatorName);
            OnAcarsStatusChanged?.Invoke(true);

            // Mostrar solo el simulador por ahora
            OnLog?.Invoke($"🛩️ Simulator: {_fsuipc.SimulatorName}", Theme.SecondaryText);

            // La información del avión se mostrará cuando esté lista (evento OnAircraftInfoReady)

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

        // ========== MÉTODOS PÚBLICOS (sin cambios significativos) ==========
        public void Start()
        {
            _fsuipc?.Start();
            StartTimers();
        }

        public void Stop()
        {
            _fsuipc?.Stop();
            _metarService?.Dispose();
            _ivaoService?.Dispose();
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

            OnFlightInfoChanged?.Invoke();   // Esto llamará a UpdateFlightInfoPanel()

            if (!string.IsNullOrEmpty(_flightManager?.ActivePirepId) && _lastTelemetry != null)
            {
                // Solo enviar si hay telemetría nueva (con cambios significativos)
                if (DateTime.UtcNow - _lastPositionUpdate >= _positionUpdateInterval)
                {
                    bool success = await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, _lastTelemetry);
                    if (success)
                    {
                        _lastPositionUpdate = DateTime.UtcNow;
                        await _flightManager.UpdateFlightProgress();

                        // Limpiar para no reenviar el mismo
                        _lastTelemetry = null;
                    }
                    OnAcarsStatusChanged?.Invoke(success);
                }
            }
            UpdateSimulatorName();
        }

        private void UpdateSimulatorName()
        {
            OnSimulatorNameChanged?.Invoke(_fsuipc?.IsConnected == true ? _fsuipc.SimulatorName : "AWAITING SIM");
        }

        public async Task TriggerMetarFetchAsync() => await _metarService.FetchNowAsync();

        public void SetActivePlan(SimbriefPlan plan)
        {
            if (plan == null) return;
            _flightManager.SetActivePlan(plan);
            UpdateFlightInfo();
            _metarService.SetStations(plan.Origin, plan.Destination, plan.Alternate);
            if (_fsuipc.IsConnected)
            {
                _flightManager.UpdatePositionValidation(_fsuipc.CurrentLatitude, _fsuipc.CurrentLongitude);
                OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
            }
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

        public async Task HandleStartStopButton(string buttonText)
        {
            switch (buttonText)
            {
                case "START": await StartFlight(); break;
                case "ABORT": await AbortFlight(); break;
                case "SEND PIREP": await SendPirep(); break;
            }
        }

        private async Task StartFlight()
        {
            if (!_flightManager.CanStartFlight())
            {
                OnLog?.Invoke("⛔ No se cumplen las condiciones para iniciar el vuelo", Theme.Warning);
                return;
            }
            var plan = _flightManager.ActivePlan;
            if (plan == null) { OnLog?.Invoke("⛔ No flight plan loaded", Theme.Warning); return; }
            if (!_fsuipc.IsConnected)
            {
                OnLog?.Invoke("❌ Simulator not connected. Cannot verify fuel quantity.", Theme.Danger);
                return;
            }

            // ===== 1. OBTENER VALORES CRUDOS =====
            double actualFuelLbs = _fsuipc.CurrentFuelLbs;
            double actualFuelKg = actualFuelLbs * 0.453592;
            double plannedFuel = plan.BlockFuel;
            double plannedFuelKg = (plan.Units?.ToUpper() == "LBS") ? plannedFuel * 0.453592 : plannedFuel;

            // ===== 2. CALCULAR DIFERENCIA CORRECTA =====
            double diffKg = actualFuelKg - plannedFuelKg;
            double diffInPlanUnits = (plan.Units?.ToUpper() == "LBS") ? diffKg / 0.453592 : diffKg;

            // ===== 3. VALIDAR (usa kg) =====
            bool fuelValid = IsFuelWithinTolerance(plannedFuelKg, actualFuelKg);
            if (!fuelValid)
            {
                double toleranceKg = Math.Max(50.0, plannedFuelKg * 0.05);
                string warningMessage = $"❌ FUEL VALIDATION FAILED\n\nPlanned: {plannedFuelKg:F0} kg\nSimulator: {actualFuelKg:F0} kg\nDifference: {Math.Abs(diffKg):F0} kg\nTolerance: {toleranceKg:F0} kg";
                OnLog?.Invoke(warningMessage, Theme.Danger);
                if (OnShowConfirmation != null)
                    await OnShowConfirmation(warningMessage, "FUEL ERROR", EcamDialogButtons.OK);
                return;
            }

            // ===== 4. LOGS (EN ORDEN CORRECTO) =====
            // Primero la diferencia
            string diffSymbol = diffInPlanUnits > 0 ? "+" : "";
            OnLog?.Invoke($"ℹ️ Fuel difference from plan: {diffSymbol}{Math.Abs(diffInPlanUnits):F0} {plan.Units ?? "kg"}", Theme.MainText);

            // Luego la validación
            OnLog?.Invoke($"✅ Fuel validation passed (tolerance: {Math.Max(50.0, plannedFuelKg * 0.05):F0} kg)", Theme.Success);

            // Luego los detalles
            OnLog?.Invoke($"📋 Planned fuel: {plannedFuel:F0} {plan.Units} ({plannedFuelKg:F0} kg)", Theme.MainText);
            OnLog?.Invoke($"⛽ Simulator fuel: {actualFuelLbs:F0} lbs ({actualFuelKg:F0} kg)", Theme.MainText);

            // ===== 5. VERIFICAR PRESENCIA IVAO (advertencia, sin penalización) =====
            var activePilot = _flightManager.ActivePilot;
            if (activePilot?.IvaoId > 0)
            {
                bool? isOnline = await _ivaoService.IsOnlineAsync(activePilot.IvaoId);
                if (isOnline == false)
                {
                    OnLog?.Invoke($"⚠️ VID {activePilot.IvaoId} no detectado en IVAO", Theme.Warning);
                    if (OnShowConfirmation != null)
                    {
                        var confirmed = await OnShowConfirmation(
                            $"El piloto (VID {activePilot.IvaoId}) no está conectado a IVAO.\n\n" +
                            "Puedes conectarte antes del pushback/taxi para evitar penalización.\n\n" +
                            "¿Deseas iniciar el vuelo de todas formas?",
                            "⚠️ IVAO OFFLINE",
                            EcamDialogButtons.YesNo);
                        if (confirmed != DialogResult.Yes) return;
                    }
                }
                else if (isOnline == true)
                {
                    OnLog?.Invoke($"✅ Conectado en IVAO (VID {activePilot.IvaoId})", Theme.Success);
                }
            }

            // ===== 6. INICIAR VUELO =====
            bool started = await _flightManager.StartFlight(plan, _flightManager.ActivePilot, actualFuelKg);
            if (started)
            {
                OnButtonStateChanged?.Invoke("ABORT", Color.Red, true);
                OnLog?.Invoke(_("FlightStarted"), Theme.Success);
                OnFlightStarted?.Invoke();
            }
        }

        private bool IsFuelWithinTolerance(double plannedKg, double actualKg)
        {
            if (actualKg <= 0 || plannedKg <= 0) return false;
            double diffKg = Math.Abs(actualKg - plannedKg);
            double toleranceKg = Math.Max(50.0, plannedKg * 0.05);  // 5% o 50 kg mínimo
            return diffKg <= toleranceKg;
        }
        public async Task CancelFlight()
        {
            if (await _flightManager.CancelFlight())
            {
                OnButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), false);
                OnLog?.Invoke("✖️ Vuelo cancelado", Theme.Warning);
                OnFlightEnded?.Invoke();  // <-- añadir
            }
        }

        public async Task AbortFlight()
        {
            if (OnShowConfirmation != null)
            {
                var result = await OnShowConfirmation("ABORT FLIGHT?\n\nThis will cancel the current flight.", "CONFIRM ABORT", EcamDialogButtons.YesNo);
                if (result != DialogResult.Yes) return;
            }

            if (await _flightManager.AbortFlight())
            {
                OnButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), false);
                OnLog?.Invoke("✖️ Flight aborted", Theme.Warning);
                OnFlightEnded?.Invoke();
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
                OnFlightEnded?.Invoke();
                // Fire-and-forget: refresh pilot airport once phpVMS processes the PIREP
                Task.Run(RefreshPilotDataAfterPirep);
            }
        }

        /// <summary>
        /// Waits a few seconds for phpVMS to process the filed PIREP (which updates
        /// the pilot's current_airport to the destination), then fetches fresh pilot
        /// data so the departure-airport validation reflects the new location.
        /// Does NOT call CheckAndResumeFlight to avoid false-positive resume prompts.
        /// </summary>
        private async Task RefreshPilotDataAfterPirep()
        {
            await Task.Delay(5000);
            try
            {
                var result = await _apiService.GetPilotData();
                if (result.Data != null)
                {
                    _flightManager.SetActivePilot(result.Data);
                    OnLog?.Invoke($"📍 Base actualizada: {result.Data.CurrentAirport}", Theme.Success);
                    OnAirportChanged?.Invoke(result.Data.CurrentAirport);
                    if (_fsuipc.IsConnected)
                    {
                        _flightManager.UpdatePositionValidation(
                            _fsuipc.CurrentLatitude, _fsuipc.CurrentLongitude);
                        OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ No se pudo actualizar ubicación del piloto: {ex.Message}", Theme.Warning);
            }
        }

        // En MainViewModel.cs

        /// <summary>
        /// Verifica si hay PIREPs activos (huérfanos) y permite al usuario limpiarlos
        /// </summary>
        /// <returns>True si se puede continuar con la planificación, False si se debe cancelar</returns>
        public async Task<bool> CheckAndCleanActivePireps()
        {
            try
            {
                var activePireps = await _apiService.GetActivePireps();

                if (!activePireps.Any())
                    return true; // No hay PIREPs activos, continuar

                // Construir mensaje con información de los vuelos activos
                var pirepInfo = string.Join("\n", activePireps.Select(p =>
                    $"✈️ {p.FlightNumber} | {p.Origin} → {p.Destination} | {p.StateDescription}"));

                var message = $"⚠️ ACTIVE FLIGHT(S) DETECTED ⚠️\n\n" +
                              $"You have {activePireps.Count} active flight(s) in the system:\n" +
                              $"{pirepInfo}\n" +
                              $"• DELETE the active flight(s) and continue\n" +
                              $"• or close this dialog and do nothing";

                // Mostrar diálogo de confirmación
                if (OnShowConfirmation != null)
                {
                    var result = await OnShowConfirmation(message, "ACTIVE FLIGHTS", EcamDialogButtons.YesNo);

                    if (result == DialogResult.Yes)
                    {
                        // Eliminar todos los PIREPs activos
                        var allDeleted = true;
                        foreach (var pirep in activePireps)
                        {
                            var deleted = await _apiService.DeletePirepById(pirep.Id);
                            if (!deleted)
                            {
                                OnLog?.Invoke($"❌ Could not delete active flight {pirep.FlightNumber}", Theme.Danger);
                                allDeleted = false;
                            }
                            else
                            {
                                OnLog?.Invoke($"✅ Deleted orphaned flight: {pirep.FlightNumber}", Theme.Success);
                            }
                        }

                        if (allDeleted)
                        {
                            OnLog?.Invoke($"✅ All orphaned flights cleared. You can now plan a new flight.", Theme.Success);
                        }
                        else
                        {
                            OnLog?.Invoke($"⚠️ Some flights could not be deleted. Please check manually.", Theme.Warning);
                        }

                        return allDeleted;
                    }
                    else
                    {
                        OnLog?.Invoke($"ℹ️ Flight planner cancelled due to active flights", Theme.MainText);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error checking active flights: {ex.Message}", Theme.Danger);
                return true; // En caso de error, permitir continuar
            }
        }
        /// <summary>
        /// Llamado justo después de un login exitoso.
        /// Busca PIREPs IN_PROGRESS con última actualización dentro de los últimos
        /// 20 minutos y ofrece al usuario retomar el vuelo.
        /// </summary>
        private async Task CheckAndResumeFlight(Pilot pilot)
        {
            const int ResumeWindowMinutes = 20;

            try
            {
                var activePireps = await _apiService.GetActivePireps();
                if (!activePireps.Any()) return;

                // Filtrar por ventana de 20 minutos usando updated_at (o created_at como fallback)
                var resumable = activePireps
                    .Where(p =>
                    {
                        var raw = !string.IsNullOrEmpty(p.UpdatedAt) ? p.UpdatedAt : p.CreatedAt;
                        if (!DateTime.TryParse(raw, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                            return false;
                        return (DateTime.UtcNow - dt.ToUniversalTime()).TotalMinutes <= ResumeWindowMinutes;
                    })
                    .ToList();

                if (!resumable.Any()) return;

                // Tomar el más reciente
                var candidate = resumable
                    .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                    .First();

                // Obtener detalle completo del PIREP
                var detail = await _apiService.GetPirepDetail(candidate.Id);
                if (detail == null) detail = candidate; // fallback a datos del listado

                var lastUpdate = !string.IsNullOrEmpty(detail.UpdatedAt) ? detail.UpdatedAt : detail.CreatedAt;
                var minutesAgo = "";
                if (DateTime.TryParse(lastUpdate, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var lastDt))
                    minutesAgo = $"{(int)(DateTime.UtcNow - lastDt.ToUniversalTime()).TotalMinutes} min ago";

                var message = $"🔄 ACTIVE FLIGHT FOUND\n\n" +
                              $"Flight:       {detail.FlightNumber}\n" +
                              $"Route:        {detail.Origin} → {detail.Destination}\n" +
                              $"Aircraft:     {detail.AircraftType}\n" +
                              $"Flight time:  {detail.FlightTime} min\n" +
                              $"Last update:  {minutesAgo}\n\n" +
                              $"Do you want to resume this flight?";

                if (OnShowConfirmation == null) return;

                var result = await OnShowConfirmation(message, "RESUME FLIGHT?", EcamDialogButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    _flightManager.ResumeFlight(detail, pilot);
                    UpdateFlightInfo();
                    OnButtonStateChanged?.Invoke("ABORT", Color.Red, true);
                    OnLog?.Invoke("✅ Flight resumed — polling and updates restarted.", Theme.Success);
                }
                else
                {
                    OnLog?.Invoke("ℹ️ Resume declined. You can start a new flight normally.", Theme.MainText);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Could not check for resumable flights: {ex.Message}", Theme.Warning);
            }
        }
        /// <summary>
        /// Autentica al piloto contra phpVMS usando la API Key configurada.
        /// Carga los datos del piloto, establece el aeropuerto asignado y
        /// actualiza el estado de validación de posición si el simulador está conectado.
        /// </summary>
        /// <returns>
        /// <c>true</c> si el login fue exitoso; <c>false</c> en caso contrario.
        /// </returns>
        /// <remarks>
        /// Este método es <c>async Task</c> (no <c>async void</c>) para que el caller
        /// pueda awaitar y capturar excepciones correctamente.
        /// </remarks>
        public async Task<bool> Login()
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
                        _flightManager.UpdatePositionValidation(
                            _fsuipc.CurrentLatitude, _fsuipc.CurrentLongitude);
                        OnLog?.Invoke($"📏 Altitude: {_fsuipc.CurrentAltitudeFeet:F0} ft", Theme.MainText);
                        OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
                    }
                    await CheckAndResumeFlight(pilot);
                    return true;
                }

                OnLog?.Invoke($"❌ Error de login: {result.Error}", Theme.Danger);
                OnAcarsStatusChanged?.Invoke(false);
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Excepción en login: {ex.Message}", Theme.Danger);
                OnAcarsStatusChanged?.Invoke(false);
                return false;
            }
        }

        public void OpenFlightPlanner()
        {
            if (_flightManager?.ActivePilot == null)
                OnLog?.Invoke("⚠️ Debes iniciar sesión primero", Theme.Warning);
            else
                OnOpenFlightPlanner?.Invoke();
        }

        public void ShowOFP()
        {
            // Mantenido como fallback; el flujo principal pasa por DownloadOFPPdfAsync
            var plan = _flightManager?.ActivePlan;
            if (plan == null)
                OnShowMessage?.Invoke("No hay OFP cargado. Usa DISPATCH para generar uno en SimBrief.", "Info");
            else
                OnShowMessage?.Invoke($"OFP: {plan.Route}\nCombustible: {plan.BlockFuel} {plan.Units}", "Operational Flight Plan");
        }

        public bool HasOFPPdf() => !string.IsNullOrEmpty(_flightManager?.ActivePlan?.PdfUrl);

        public string GetOFPTitle()
        {
            var plan = _flightManager?.ActivePlan;
            return plan != null ? $"{plan.Origin} → {plan.Destination}" : "OFP";
        }

        public string GetCachedOFPPath()
        {
            string path = _flightManager?.ActivePlan?.LocalPdfPath;
            return (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) ? path : null;
        }

        public async System.Threading.Tasks.Task<string> DownloadOFPPdfAsync()
        {
            var plan = _flightManager?.ActivePlan;
            string url = plan?.PdfUrl;
            if (string.IsNullOrEmpty(url)) return null;

            // Usar caché si el archivo todavía existe
            if (!string.IsNullOrEmpty(plan.LocalPdfPath) && System.IO.File.Exists(plan.LocalPdfPath))
                return plan.LocalPdfPath;

            string tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"vmsOFP_{Guid.NewGuid():N}.pdf");

            var response = await _apiService.HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using (var fs = new System.IO.FileStream(tempPath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                await response.Content.CopyToAsync(fs);

            plan.LocalPdfPath = tempPath;
            return tempPath;
        }

        public void LogButtonPress(string buttonText) => OnLog?.Invoke($"🔘 Botón {buttonText} presionado", Theme.MainText);

        public void LoadFlightFromBid(Flight flight)
        {
            if (flight == null) return;
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
            _flightManager.SetActivePlan(plan);
            UpdateFlightInfo();
            OnLog?.Invoke($"📋 Flight data loaded from bid", Theme.MainText);
        }

        public async Task<List<Flight>> LoadPilotBids()
        {
            try
            {
                if (_flightManager.ActivePilot == null)
                {
                    OnLog?.Invoke("⚠️ No hay piloto activo", Theme.Warning);
                    return new List<Flight>();
                }
                return await _apiService.GetPilotBids() ?? new List<Flight>();
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Error cargando reservas: {ex.Message}", Theme.Danger);
                return new List<Flight>();
            }
        }

        // Métodos auxiliares
        // ---------------------------------------------------------------------------
        // GetTerrainElevation
        // ---------------------------------------------------------------------------
        // Lógica AGL intencionalmente relativa al aeropuerto de referencia de la fase:
        //   - Fases de salida  (pre-vuelo hasta climb) → elevación aeropuerto ORIGEN
        //   - Fases de llegada (descent, approach, landing, post-aterrizaje) → elevación DESTINO
        //   - Enroute → usa origen como referencia (AGL solo es útil en salidas/llegadas;
        //     en crucero el valor simplemente será grande, lo cual es correcto)
        //   - Sin plan activo → 0 (fallback seguro)
        //
        // Ejemplo Colombia:
        //   SKBO (8360 ft) → SKRG (7025 ft): un avión en crucero a FL230 (23000 ft MSL)
        //   AGL respecto a SKBO = 23000 − 8360 = 14640 ft  ← útil para detección
        //   AGL respecto a SKRG = 23000 − 7025 = 15975 ft

        private double GetTerrainElevation(FlightPhase phase)
        {
            var plan = _flightManager?.ActivePlan;
            if (plan == null) return 0.0;

            switch (phase)
            {
                // ---- Fases pre-vuelo y de salida: referencia = ORIGEN ----
                case FlightPhase.Boarding:
                case FlightPhase.Pushback:
                case FlightPhase.TaxiOut:
                case FlightPhase.TakeoffRoll:
                case FlightPhase.Takeoff:
                case FlightPhase.Climb:
                case FlightPhase.Enroute:       // en crucero AGL es grande: correcto
                    return plan.OriginElevation;

                // ---- Fases de llegada: referencia = DESTINO ----
                case FlightPhase.Descent:
                case FlightPhase.Approach:
                case FlightPhase.Landing:
                case FlightPhase.Landed:
                case FlightPhase.AfterLanding:
                case FlightPhase.TaxiIn:
                case FlightPhase.OnBlock:
                case FlightPhase.Arrived:
                case FlightPhase.Completed:
                    return plan.DestinationElevation;

                default:
                    return 0.0;
            }
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
    }
}