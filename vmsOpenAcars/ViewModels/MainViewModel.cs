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
using vmsOpenAcars.Db;
using vmsOpenAcars.Models.NavData;

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
        public LandingLogService LandingLogService => _landingLogService;

        private readonly FlightManager _flightManager;
        private readonly FsuipcService _fsuipc;
        private readonly ApiService _apiService;
        private readonly PhpVmsFlightService _phpVmsFlightService;
        private readonly SimbriefEnhancedService _simbriefEnhancedService;
        private readonly MetarService _metarService = new MetarService();
        private readonly IvaoService  _ivaoService  = new IvaoService();
        private readonly NavDataService    _runwayService     = new NavDataService();
        private readonly LandingLogService _landingLogService = new LandingLogService(AppConfig.LandingLogPath);

        // Approach track capture
        private List<ApproachTrackPoint> _approachBuffer    = new List<ApproachTrackPoint>();
        private RunwayTouchdownResult    _approachThreshold = null;
        private DateTime                 _lastApproachCapture = DateTime.MinValue;

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
        public event Action<double, double, double> OnMapPositionUpdate; // lat, lon, headingDeg
        public event Action<SimbriefPlan> OnPlanChanged;
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
        public event Action<MetarData[]>     OnMetarUpdated;
        public event Action<MetarFetchState> OnMetarStateChanged;
        public event Action<string, OsdSeverity> OnOsdMessage;

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
            _flightManager.OnOsdMessage -= OnFlightManagerOsd;
            _flightManager.PhaseChanged -= OnFlightPhaseChanged;
            _flightManager.OnPositionValidated -= OnPositionValidated;
            _flightManager.OnAirportChanged -= OnAirportChanged;
            _flightManager.OnLandingDetected -= OnLandingDetected;
            _flightManager.OnBlockDetected -= OnBlockDetected;
            _flightManager.OnTakeoffDetected -= OnTakeoffDetected;
            _flightManager.OnTaxiPositionUpdate -= HandleTaxiPositionUpdate;

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
            _flightManager.OnOsdMessage += OnFlightManagerOsd;
            _flightManager.PhaseChanged += OnFlightPhaseChanged;
            _flightManager.OnPositionValidated += OnPositionValidated;
            _flightManager.OnAirportChanged += OnAirportChanged;
            _flightManager.OnLandingDetected += OnLandingDetected;
            _flightManager.OnBlockDetected += OnBlockDetected;
            _flightManager.OnTakeoffDetected += OnTakeoffDetected;
            _flightManager.OnTaxiPositionUpdate += HandleTaxiPositionUpdate;

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
            _fsuipc.NavLightChanged += on =>
            {
                string agl = AglSuffix();
                OnLog?.Invoke(on ? _("Log_NavLightsOn", agl) : _("Log_NavLightsOff", agl), Theme.MainText);
            };
            _fsuipc.StrobeLightChanged += on =>
            {
                string agl = AglSuffix();
                OnLog?.Invoke(on ? _("Log_StrobeLightsOn", agl) : _("Log_StrobeLightsOff", agl), Theme.MainText);
                if (on && !_cabinOnRunwaySent && _lastGroundSpeedKt <= 40
                    && _flightManager?.CurrentPhase != FlightPhase.Idle)
                {
                    _cabinOnRunwaySent = true;
                    _cabinAnnouncements.QueueAnnouncement("on_runway");
                }
            };
            _fsuipc.LandingLightChanged += on =>
            {
                string agl = AglSuffix();
                OnLog?.Invoke(on ? _("Log_LandingLightsOn", agl) : _("Log_LandingLightsOff", agl), Theme.MainText);
                if (on && !_cabinOnRunwaySent && _lastGroundSpeedKt <= 40
                    && _flightManager?.CurrentPhase != FlightPhase.Idle)
                {
                    _cabinOnRunwaySent = true;
                    _cabinAnnouncements.QueueAnnouncement("on_runway");
                }
            };
            _fsuipc.BeaconChanged += on =>
            {
                string agl = AglSuffix();
                OnLog?.Invoke(on ? _("Log_BeaconOn", agl) : _("Log_BeaconOff", agl), Theme.MainText);
            };

            _metarService.OnMetarUpdated  += metars => OnMetarUpdated?.Invoke(metars);
            _metarService.OnStateChanged  += state  => OnMetarStateChanged?.Invoke(state);
            _metarService.OnLog           += msg   => OnLog?.Invoke(msg, Theme.MainText);
        }
        private int _lastUiAltitude;
        private int _mapUpdateCounter;
        private int _lastUiSpeed;
        private string _lastUiPhase = string.Empty;
        private string _lastUiPosition = string.Empty;
        private FlightPhase _prevPhase = FlightPhase.Idle;
        private readonly CabinAnnouncementService _cabinAnnouncements = new CabinAnnouncementService();
        private bool     _cabinCruiseSent;
        private bool     _cabinOnRunwaySent;
        private DateTime _cabinCruiseCheckStart = DateTime.MinValue;
        private double   _lastGroundSpeedKt;
        private bool   _wasOnRunwayForEntry  = false;
        private bool   _wasOnRunwayForExit   = false;
        private int    _pendingRunwayOnCount = 0;
        private string _lastLoggedTaxiway    = null;
        private string _lastHoldingShortRwy  = null;
        private string _lastTaxiPositionMsg  = null;
        private string _pendingTaxiway       = null;
        private int    _pendingTaxiwayCount  = 0;
        // Min heading divergence from current taxiway axis before a taxiway change is considered.
        // Filters GPS proximity blips on crossing/parallel taxiways without a real turn.
        private const double TaxiwayChangeHeadingThreshold = 25.0;

        // Procedure fix restriction tracking (SID/STAR speed restriction OSD + scoring)
        private List<SimbriefWaypoint> _procFixes     = null;   // ordered CLB or DSC fixes with restriction
        private int                    _procFixIdx     = 0;      // index of next unannounced fix
        private int                    _procSpdViolations = 0;   // speed violations this flight
        private bool                   _procFixAnnounced = false; // whether current fix was already OSD'd

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

            // Map update every 5 raw cycles (~250 ms) — independent of adaptive telemetry rate
            if (++_mapUpdateCounter >= 5)
            {
                _mapUpdateCounter = 0;
                OnMapPositionUpdate?.Invoke(e.Latitude, e.Longitude, e.HeadingDeg);
            }

            // Cabin announcements — GS tracking + cruise 30 s sustained check
            _lastGroundSpeedKt = e.GroundSpeedKt;
            if (_flightManager?.CurrentPhase == FlightPhase.Enroute && !_cabinCruiseSent)
            {
                double agl = e.AltitudeFeet - (_flightManager.ActivePlan?.OriginElevation ?? 0);
                if (agl > 10000)
                {
                    if (_cabinCruiseCheckStart == DateTime.MinValue)
                        _cabinCruiseCheckStart = DateTime.UtcNow;
                    else if ((DateTime.UtcNow - _cabinCruiseCheckStart).TotalSeconds >= 30)
                    {
                        _cabinCruiseSent = true;
                        _cabinAnnouncements.QueueAnnouncement("cruise");
                    }
                }
                else { _cabinCruiseCheckStart = DateTime.MinValue; }
            }

            // Procedure fix restriction tracking (CLB / DSC phases only)
            var curPhase = _flightManager?.CurrentPhase;
            if (curPhase == FlightPhase.Climb || curPhase == FlightPhase.Descent)
                CheckProcedureRestrictions(e.Latitude, e.Longitude, e.IndicatedAirspeedKt);

            // Approach track capture — every 2 s when in approach phase, AGL < 3000 ft
            if (_flightManager?.CurrentPhase == FlightPhase.Approach)
            {
                // Threshold acquisition: keep retrying every cycle until a runway
                // is found that satisfies heading-alignment AND lateral-proximity.
                // This avoids latching onto a runway during a base-to-final turn
                // when heading is closer to a different runway than the actual target.
                if (_approachThreshold == null && _runwayService.IsAvailable)
                {
                    string dest = _flightManager.ActivePlan?.Destination ?? _flightManager.CurrentAirport;
                    _approachThreshold = _runwayService.GetRunwayThreshold(dest, e.Latitude, e.Longitude, e.HeadingDeg);
                    if (_approachThreshold != null)
                    {
                        Task.Run(() => LoadApproachData(dest, _approachThreshold.RunwayName));
                    }
                }

                double computedAgl = _flightManager.CurrentAGL;
                if (_approachThreshold != null
                    && computedAgl < 3000
                    && _landingLogService.IsAvailable
                    && (DateTime.UtcNow - _lastApproachCapture).TotalSeconds >= 2.0)
                {
                    _lastApproachCapture = DateTime.UtcNow;
                    var (distNm, lateralFt) = NavDataService.ComputeApproachMetrics(
                        _approachThreshold.ThresholdLat,
                        _approachThreshold.ThresholdLon,
                        _approachThreshold.ThresholdHeading,
                        e.Latitude, e.Longitude);

                    // First capture point — log runway, AGL, distance
                    if (_approachBuffer.Count == 0)
                    {
                        OnLog?.Invoke(
                            string.Format(_("Lnm_ApproachCaptureStart"),
                                _approachThreshold.RunwayName,
                                (int)computedAgl,
                                distNm.ToString("F1")),
                            Theme.Success);
                    }

                    _approachBuffer.Add(new ApproachTrackPoint
                    {
                        SeqNo      = _approachBuffer.Count,
                        Lat        = e.Latitude,
                        Lon        = e.Longitude,
                        AltFt      = e.AltitudeFeet,
                        AglFt      = computedAgl,
                        IasKt      = e.IndicatedAirspeedKt,
                        VsFpm      = e.VerticalSpeedFpm,
                        HeadingDeg = e.HeadingDeg,
                        DistNm     = distNm,
                        LateralFt  = lateralFt
                    });
            }
            }
        }
        private void OnAircraftInfoReady()
        {
            // Evitar mostrar múltiples veces
            if (_aircraftInfoShown) return;
            _aircraftInfoShown = true;

            // Mostrar fabricante
            if (_fsuipc.AircraftManufacturer != "Unknown")
            {
                OnLog?.Invoke(_("Log_Manufacturer", _fsuipc.AircraftManufacturer), Theme.SecondaryText);
            }

            // Mostrar ICAO
            if (_fsuipc.AircraftIcao != "????")
            {
                OnLog?.Invoke(_("Log_ICAO", _fsuipc.AircraftIcao), Theme.SecondaryText);
            }

            // Mostrar aeronave (título completo)
            if (!string.IsNullOrEmpty(_fsuipc.AircraftTitle) && _fsuipc.AircraftTitle != "Unknown")
            {
                OnLog?.Invoke(_("Log_Aircraft", _fsuipc.AircraftTitle), Theme.MainText);
            }

            // Mostrar livery (extraída correctamente)
            string livery = _fsuipc.GetAircraftLivery();
            if (livery != "Unknown" && livery != _fsuipc.AircraftIcao)
            {
                OnLog?.Invoke(_("Log_Livery", livery), Theme.SecondaryText);
            }
            // ===== MOSTRAR ALTITUD ACTUAL =====
            OnLog?.Invoke(_("Log_Altitude", $"{_fsuipc.CurrentAltitudeFeet:F0}"), Theme.MainText);
        }

        // ========== EVENTOS DE FLIGHTMANAGER (sin cambios) ==========
        private void OnFlightManagerLog(string msg, Color color) => OnLog?.Invoke(msg, color);
        private void OnFlightManagerOsd(string msg, OsdSeverity sev) => OnOsdMessage?.Invoke(msg, sev);
        private async void OnFlightPhaseChanged(FlightPhase phase)
        {
            // Verificar IVAO al iniciar TaxiOut
            if (phase == FlightPhase.TaxiOut)
            {
                await CheckIvaoAtBlocksOffAsync();
            }

            if (phase == FlightPhase.Boarding && _runwayService.IsAvailable)
            {
                // Gate captured NOW — aircraft hasn't moved yet (pilot just pressed START)
                double dLat  = _flightManager.CurrentLat;
                double dLon  = _flightManager.CurrentLon;
                string depAp = _flightManager.ActivePlan?.Origin ?? _flightManager.CurrentAirport;
                Task.Run(() => LookupDepartureParking(depAp, dLat, dLon));
            }

            if (phase == FlightPhase.TakeoffRoll)
            {
                _wasOnRunwayForEntry  = false;
                _wasOnRunwayForExit   = false;
                _pendingRunwayOnCount = 0;
                _lastLoggedTaxiway    = null;
                _lastHoldingShortRwy  = null;
                _lastTaxiPositionMsg  = null;
                _pendingTaxiway       = null;
                _pendingTaxiwayCount  = 0;
                if (_runwayService.IsAvailable)
                {
                    double lat = _flightManager.CurrentLat;
                    double lon = _flightManager.CurrentLon;
                    double hdg = _flightManager.CurrentHeading;
                    string dep = _flightManager.ActivePlan?.Origin ?? _flightManager.CurrentAirport;
                    Task.Run(() => LookupTakeoffRunwayData(dep, lat, lon, hdg));
                }
            }
            else if (phase == FlightPhase.TaxiIn && _prevPhase == FlightPhase.AfterLanding)
            {
                _lastLoggedTaxiway    = null;
                _lastTaxiPositionMsg  = null;
                _pendingTaxiway       = null;
                _pendingTaxiwayCount  = 0;
                _pendingRunwayOnCount = 0;
            }
            else if (phase == FlightPhase.OnBlock && _runwayService.IsAvailable)
            {
                double aLat  = _flightManager.CurrentLat;
                double aLon  = _flightManager.CurrentLon;
                string arrAp = _flightManager.ActivePlan?.Destination ?? _flightManager.CurrentAirport;
                Task.Run(() => LookupArrivalParking(arrAp, aLat, aLon));
            }

            // Approach capture: reset state on entering approach. Threshold is
            // acquired lazily in OnRawDataUpdated when the aircraft is aligned with
            // a runway and laterally close to its extended centreline.
            if (phase == FlightPhase.Approach && _runwayService.IsAvailable)
            {
                _approachBuffer.Clear();
                _lastApproachCapture = DateTime.MinValue;
                _approachThreshold = null;
            }
            else if (phase != FlightPhase.Approach)
            {
                _approachThreshold = null;
            }

            // OSD phase notifications + cabin announcements
            switch (phase)
            {
                case FlightPhase.TaxiOut:
                    OnOsdMessage?.Invoke("TAXI OUT", OsdSeverity.Info);
                    _cabinAnnouncements.QueueAnnouncement("taxi_out");
                    break;
                case FlightPhase.TakeoffRoll: OnOsdMessage?.Invoke("TAKEOFF ROLL", OsdSeverity.Info); break;
                case FlightPhase.Enroute:     OnOsdMessage?.Invoke("CRUISE",       OsdSeverity.Info); break;
                case FlightPhase.Descent:
                    OnOsdMessage?.Invoke("DESCENDING", OsdSeverity.Info);
                    _cabinAnnouncements.QueueAnnouncement("top_of_descent");
                    break;
                case FlightPhase.Approach:
                    OnOsdMessage?.Invoke("APPROACH", OsdSeverity.Info);
                    _cabinAnnouncements.QueueAnnouncement("approach");
                    break;
                case FlightPhase.TaxiIn:
                    _cabinAnnouncements.QueueAnnouncement("taxi_in");
                    break;
                case FlightPhase.OnBlock:     OnOsdMessage?.Invoke("ON BLOCK",     OsdSeverity.Info); break;
                case FlightPhase.Climb:
                    if (_prevPhase == FlightPhase.AfterLanding)
                        OnOsdMessage?.Invoke("TOUCH AND GO", OsdSeverity.Warning);
                    break;
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
                OnLog?.Invoke(_("Ivao_Online", pilot.IvaoId), Theme.Success);
            }
            else if (isOnline == false)
            {
                OnLog?.Invoke(_("Ivao_OfflinePenalty", pilot.IvaoId), Theme.Warning);
                _flightManager.MarkOfflineFlight();
            }
            else
            {
                OnLog?.Invoke(_("Ivao_FeedUnavailable"), Theme.Warning);
            }
        }
        private void OnPositionValidated(ValidationStatus status) => OnValidationStatusChanged?.Invoke(status);
        private void OnTakeoffDetected(int speed, int altitude, int verticalSpeed)
        {
            OnLog?.Invoke(_("Log_TakeoffDetected", speed, altitude, verticalSpeed), Theme.Success);
            // (opcional: crear registro ACARS)
        }
        private void OnBlockDetected()
        {
            OnLog?.Invoke(_("Log_OnBlockDetected"), Theme.Success);
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
                OnLog?.Invoke(_("Log_LandingRecorded", verticalSpeed, $"{gforce:F2}", (int)_fsuipc.CurrentHeading, $"{pitch:F1}", $"{bank:F1}"), Theme.Success);
            });

            int absVs = Math.Abs(verticalSpeed);
            OsdSeverity tdSev  = absVs <= 300 ? OsdSeverity.Success
                               : absVs <= 600 ? OsdSeverity.Warning
                               : OsdSeverity.Critical;
            string tdLabel = absVs <= 300 ? "TOUCHDOWN"
                           : absVs <= 600 ? "FIRM LANDING"
                           : "HARD LANDING";
            OnOsdMessage?.Invoke($"{tdLabel}  {verticalSpeed} FPM  {gforce:F2} G", tdSev);
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
                distance = Math.Round(totalDistanceKm * 0.539957, 2),
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
            OnLog?.Invoke(_("Log_AccurateTakeoff"), Theme.Success);
            OnLog?.Invoke(_("Log_TakeoffRotation", $"{data.RotationIasKt:F0}"), Theme.MainText);
            OnLog?.Invoke(_("Log_TakeoffGroundSpeed", $"{data.GroundSpeedKt:F0}"), Theme.MainText);
            OnLog?.Invoke(_("Log_PitchBank", $"{data.PitchDeg:F1}", $"{data.BankDeg:F1}"), Theme.MainText);
            OnLog?.Invoke(_("Log_TakeoffHeading", $"{data.HeadingDeg:F0}"), Theme.MainText);

            if (data.EngineType == "N1")
            {
                OnLog?.Invoke(_("Log_TakeoffN1", $"{data.Eng1N1Pct:F0}", $"{data.Eng2N1Pct:F0}"), Theme.MainText);
            }
            else if (data.EngineType == "PROP RPM")
            {
                OnLog?.Invoke(_("Log_TakeoffPropRpm", $"{data.Eng1Rpm:F0}", $"{data.Eng2Rpm:F0}"), Theme.MainText);
            }
            else if (data.EngineType == "PISTON RPM")
            {
                OnLog?.Invoke(_("Log_TakeoffRpm", $"{data.Eng1Rpm:F0}", $"{data.Eng2Rpm:F0}"), Theme.MainText);
            }

            OnLog?.Invoke(_("Log_TakeoffFlaps", $"{data.FlapsPosition * 100:F0}"), Theme.MainText);
            OnLog?.Invoke(_("Log_OatWind", $"{data.OatCelsius:F0}", $"{data.WindSpeedKt:F0}", $"{data.WindDirDeg:F0}"), Theme.MainText);
        }

        private void OnTouchdownDetectedEvent(object sender, TouchdownData data)
        {
            string rating = data.GForcePeak < 1.3 ? _("Score_Perfect") : (data.GForcePeak < 1.8 ? _("Score_Normal") : (data.GForcePeak < 2.5 ? _("Score_Hard") : _("Score_Crash")));
            OnLog?.Invoke(_("Log_AccurateTouchdown"), Theme.Success);
            OnLog?.Invoke(_("Log_TouchdownVs", $"{data.VerticalSpeedFpm:F0}"), Theme.MainText);
            OnLog?.Invoke(_("Log_TouchdownGForce", $"{data.GForcePeak:F2}", rating), Theme.MainText);
            OnLog?.Invoke(_("Log_TouchdownSpeed", $"{data.IasKt:F0}", $"{data.GroundSpeedKt:F0}"), Theme.MainText);
            OnLog?.Invoke(_("Log_PitchBank", $"{data.PitchDeg:F1}", $"{data.BankDeg:F1}"), Theme.MainText);
            OnLog?.Invoke(_("Log_TouchdownFlapsSpoilers", $"{data.FlapsPosition * 100:F0}", $"{data.SpoilersPosition * 100:F0}"), Theme.MainText);
            OnLog?.Invoke(_("Log_TouchdownReversers", $"{data.Eng1ReverserPct:F0}", $"{data.Eng2ReverserPct:F0}"), Theme.MainText);
            OnLog?.Invoke(_("Log_TouchdownBrakes", $"{data.BrakeLeft * 100:F0}", $"{data.BrakeRight * 100:F0}", GetAutobrakeName(data.AutobrakeSetting)), Theme.MainText);
            OnLog?.Invoke(_("Log_OatWind", $"{data.OatCelsius:F0}", $"{data.WindSpeedKt:F0}", $"{data.WindDirDeg:F0}"), Theme.MainText);

            if (_runwayService.IsAvailable)
                Task.Run(() => LookupRunwayData(data));
        }

        private void LookupRunwayData(TouchdownData data)
        {
            string airport = _flightManager.ActivePlan?.Destination;
            if (string.IsNullOrEmpty(airport)) return;

            var result = _runwayService.FindTouchdownRunway(
                airport, data.LatitudeDeg, data.LongitudeDeg, data.HeadingDeg);

            if (result == null)
            {
                OnLog?.Invoke(string.Format(_("Lnm_RunwayNotFound"), airport, (int)data.HeadingDeg), Theme.Warning);
                return;
            }

            _flightManager.SetRunwayTouchdownData(
                result.ThresholdDistanceFt,
                result.CenterlineDeviationFt,
                result.RunwayName);

            OnLog?.Invoke(
                string.Format(_("Lnm_TouchdownInfo"),
                    result.RunwayName,
                    (int)result.ThresholdDistanceFt,
                    (int)result.CenterlineDeviationFt),
                Theme.Success);
        }

        private void HandleTaxiPositionUpdate(
            double lat, double lon, double heading, string airport, bool isTaxiIn)
        {
            if (!_runwayService.IsAvailable || string.IsNullOrEmpty(airport)) return;

            Task.Run(() =>
            {
                bool onRunway = false;

                // ── Runway presence detection (both TaxiOut and TaxiIn / AfterLanding) ──
                var entry = _runwayService.FindRunwayEntry(airport, lat, lon, heading);
                onRunway  = entry != null;

                // Debounce: require 2 consecutive detections to confirm runway presence,
                // preventing GPS jitter on parallel taxiways from firing false entries.
                if (onRunway) _pendingRunwayOnCount++;
                else          _pendingRunwayOnCount = 0;
                bool confirmedOnRunway = onRunway && _pendingRunwayOnCount >= 2;

                if (!isTaxiIn)
                {
                    // Departure: runway entry
                    if (confirmedOnRunway && !_wasOnRunwayForEntry)
                    {
                        _lastLoggedTaxiway   = null;
                        _lastHoldingShortRwy = null;
                        _lastTaxiPositionMsg = null;
                        _pendingTaxiway      = null;
                        _pendingTaxiwayCount = 0;
                        if (entry.IsBacktrack)
                        {
                            if (!string.IsNullOrEmpty(entry.TaxiwayName))
                                OnLog?.Invoke(string.Format(_("Lnm_RunwayBacktrackTwy"), entry.RunwayName, entry.TaxiwayName), Theme.Warning);
                            else
                                OnLog?.Invoke(string.Format(_("Lnm_RunwayBacktrack"), entry.RunwayName), Theme.Warning);
                            OnOsdMessage?.Invoke($"BACKTRACK  RWY {entry.RunwayName}", OsdSeverity.Warning);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(entry.TaxiwayName))
                                OnLog?.Invoke(string.Format(_("Lnm_RunwayEntered"), entry.RunwayName, entry.TaxiwayName), Theme.Takeoff);
                            else
                                OnLog?.Invoke(string.Format(_("Lnm_RunwayEnteredNoTwy"), entry.RunwayName), Theme.Takeoff);
                            OnOsdMessage?.Invoke($"ENTERING RWY {entry.RunwayName}", OsdSeverity.Warning);
                        }
                    }
                    _wasOnRunwayForEntry = confirmedOnRunway;
                }
                else
                {
                    // Arrival: re-entry into runway (backtrack) — only after aircraft has exited
                    if (confirmedOnRunway && !_wasOnRunwayForExit && entry?.IsBacktrack == true)
                    {
                        _lastLoggedTaxiway   = null;
                        _pendingTaxiway      = null;
                        _pendingTaxiwayCount = 0;
                        OnLog?.Invoke(string.Format(_("Lnm_RunwayBacktrack"), entry.RunwayName), Theme.Warning);
                        OnOsdMessage?.Invoke($"BACKTRACK  RWY {entry.RunwayName}", OsdSeverity.Warning);
                    }
                    // Arrival: runway exit — log taxiway when leaving runway
                    if (!confirmedOnRunway && _wasOnRunwayForExit)
                    {
                        string twy = _runwayService.FindNearestTaxiway(airport, lat, lon, heading);
                        if (!string.IsNullOrEmpty(twy))
                            OnLog?.Invoke(string.Format(_("Lnm_RunwayVacated"), twy), Theme.Success);
                        else
                            OnLog?.Invoke(_("Lnm_RunwayVacatedNoTwy"), Theme.Success);
                        OnOsdMessage?.Invoke("RWY VACATED", OsdSeverity.Info);
                    }
                    _wasOnRunwayForExit = confirmedOnRunway;
                }

                if (!onRunway)
                {
                    // ── Taxi position: current taxiway + next intersection ──────
                    string twy  = _runwayService.FindNearestTaxiway(airport, lat, lon, heading);
                    string next = _runwayService.FindNextIntersection(airport, lat, lon, heading);

                    if (!string.IsNullOrEmpty(twy))
                    {
                        if (twy != _lastLoggedTaxiway)
                        {
                            // Angular criterion: only advance hysteresis if the aircraft has
                            // turned away from the current taxiway axis by more than the threshold.
                            // This prevents proximity blips on crossing or parallel taxiways from
                            // triggering a taxiway change while the aircraft is still rolling
                            // straight along its current taxiway. The criterion is skipped when
                            // no taxiway has been confirmed yet (first taxiway of the session).
                            bool headingDiverged = true;
                            if (!string.IsNullOrEmpty(_lastLoggedTaxiway))
                            {
                                double curBrg = _runwayService.FindTaxiwaySegmentBearing(
                                    airport, _lastLoggedTaxiway, lat, lon);
                                if (!double.IsNaN(curBrg))
                                {
                                    double d1 = Math.Abs(heading - curBrg) % 360.0;
                                    if (d1 > 180.0) d1 = 360.0 - d1;
                                    double d2 = Math.Abs(heading - (curBrg + 180.0) % 360.0) % 360.0;
                                    if (d2 > 180.0) d2 = 360.0 - d2;
                                    headingDiverged = Math.Min(d1, d2) > TaxiwayChangeHeadingThreshold;
                                }
                            }

                            if (headingDiverged)
                            {
                                if (twy == _pendingTaxiway)
                                    _pendingTaxiwayCount++;
                                else
                                {
                                    _pendingTaxiway      = twy;
                                    _pendingTaxiwayCount = 1;
                                }

                                if (_pendingTaxiwayCount >= 3)
                                {
                                    _pendingTaxiway      = null;
                                    _pendingTaxiwayCount = 0;
                                    _lastLoggedTaxiway   = twy;

                                    string msg = !string.IsNullOrEmpty(next)
                                        ? string.Format(_("Lnm_TaxiPosition"), twy, next)
                                        : string.Format(_("Lnm_TaxiwayChange"), twy);
                                    _lastTaxiPositionMsg = msg;
                                    OnLog?.Invoke(msg, Theme.Taxi);
                                }
                            }
                            else
                            {
                                // Heading still aligned with current taxiway — suppress candidate
                                _pendingTaxiway      = null;
                                _pendingTaxiwayCount = 0;
                            }
                        }
                        else
                        {
                            // Same taxiway — reset pending, allow next-intersection updates freely
                            _pendingTaxiway      = null;
                            _pendingTaxiwayCount = 0;

                            string msg = !string.IsNullOrEmpty(next)
                                ? string.Format(_("Lnm_TaxiPosition"), twy, next)
                                : string.Format(_("Lnm_TaxiwayChange"), twy);

                            if (msg != _lastTaxiPositionMsg)
                            {
                                _lastTaxiPositionMsg = msg;
                                OnLog?.Invoke(msg, Theme.Taxi);
                            }
                        }
                    }

                    if (!isTaxiIn)
                    {
                        // ── Holding short detection ──────────────────────────────
                        var hp = _runwayService.FindHoldingPoint(airport, lat, lon, heading);
                        if (hp != null && hp.RunwayName != _lastHoldingShortRwy)
                        {
                            _lastHoldingShortRwy = hp.RunwayName;
                            if (!string.IsNullOrEmpty(hp.TaxiwayName))
                                OnLog?.Invoke(string.Format(_("Lnm_HoldingShort"), hp.RunwayName, hp.TaxiwayName), Theme.Taxi);
                            else
                                OnLog?.Invoke(string.Format(_("Lnm_HoldingShortNoTwy"), hp.RunwayName), Theme.Taxi);
                        }
                        else if (hp == null && _lastHoldingShortRwy != null)
                        {
                            _lastHoldingShortRwy = null;
                        }
                    }
                }
            });
        }

        private void LookupTakeoffRunwayData(string airport, double lat, double lon, double heading)
        {
            if (string.IsNullOrEmpty(airport)) return;

            var result = _runwayService.FindTakeoffRunway(airport, lat, lon, heading);
            if (result == null)
            {
                OnLog?.Invoke(string.Format(_("Lnm_TakeoffRunwayNotFound"), airport, (int)heading), Theme.Warning);
                return;
            }

            OnLog?.Invoke(
                string.Format(_("Lnm_TakeoffInfo"),
                    result.RunwayName,
                    (int)result.ThresholdDistanceFt,
                    (int)result.CenterlineDeviationFt),
                Theme.Success);
        }

        private void LoadApproachData(string airport, string runwayName)
        {
            if (string.IsNullOrEmpty(airport)) return;
            var ils      = _runwayService.GetIlsForRunway(airport, runwayName);
            var approach = _runwayService.GetApproachType(airport, runwayName);
            var fixes    = approach != null
                           ? _runwayService.GetApproachFixes(airport, runwayName)
                           : null;
            _flightManager?.SetApproachData(ils, approach, fixes);
        }

        private void LookupDepartureParking(string airport, double lat, double lon)
        {
            var spot = _runwayService.FindNearestParking(airport, lat, lon);
            if (spot != null)
                OnLog?.Invoke(string.Format(_("Lnm_DepartureParking"), spot.DisplayName), Theme.Taxi);
        }

        private void LookupArrivalParking(string airport, double lat, double lon)
        {
            var spot = _runwayService.FindNearestParking(airport, lat, lon);
            if (spot != null)
                OnLog?.Invoke(string.Format(_("Lnm_ArrivalParking"), spot.DisplayName), Theme.Success);
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
            // GearChanged fires before RawDataUpdated, so FlightManager.IsOnGround may still be
            // true right after liftoff (stale from previous tick). Compute AGL directly from
            // fsuipc altitude (already fresh) to avoid the IsOnGround guard returning 0.
            double msl  = _fsuipc.CurrentAltitudeFeet;
            double elev = newPos == 0
                ? (_flightManager.ActivePlan?.OriginElevation      ?? 0)
                : (_flightManager.ActivePlan?.DestinationElevation ?? 0);
            int agl = (int)(msl - elev);
            string aglStr = agl > 50 ? $" ({agl} ft AGL)" : "";
            OnLog?.Invoke(_("Log_GearChanged", status, aglStr), Theme.MainText);
        }

        private string AglSuffix()
        {
            int agl = (int)(_flightManager?.CurrentAGL ?? 0);
            return agl > 50 ? $" ({agl} ft AGL)" : "";
        }

        private void LogPlanSummary(SimbriefPlan p)
        {
            if (p == null) return;
            string origIata = string.IsNullOrEmpty(p.OriginIata)      ? "---" : p.OriginIata;
            string destIata = string.IsNullOrEmpty(p.DestinationIata) ? "---" : p.DestinationIata;
            string date     = p.ScheduledOffTime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(p.ScheduledOffTime).UtcDateTime.ToString("ddMMMyyyy").ToUpper()
                : DateTimeOffset.UtcNow.ToString("ddMMMyyyy").ToUpper();
            string tripStr  = p.TripFuel > 0 ? $"  TRIP {p.TripFuel:F0}" : "";

            OnLog?.Invoke(
                $"📋 {p.Airline}{p.FlightNumber}  {p.Origin}/{origIata} → {p.Destination}/{destIata}" +
                $"  {p.AircraftIcao} {p.Registration}  {date}",
                Theme.Success);
            OnLog?.Invoke(
                $"   PAX {p.PaxCount}  FUEL {p.BlockFuel:F0}{tripStr}  CARGO {p.CargoWeight:F0}  FL{p.CruiseAltitude / 100}",
                Theme.MainText);
        }

        private void OnFlapsChanged(double oldPercent, double newPercent)
        {
            OnLog?.Invoke(_("Log_FlapsChanged", $"{oldPercent:F0}", $"{newPercent:F0}"), Theme.SecondaryText);
        }

        private void OnSpoilersChanged(bool deployed)
        {
            OnLog?.Invoke(deployed ? _("Log_SpoilersDeployed") : _("Log_SpoilersRetracted"), Theme.Warning);
        }

        private void OnParkingBrakeChanged(bool engaged)
        {
            OnLog?.Invoke(engaged ? _("Log_ParkingBrakeSet") : _("Log_ParkingBrakeReleased"), Theme.MainText);
        }

        private void OnEnginesChanged(bool running)
        {
            if (running)
                OnLog?.Invoke(_("Log_EnginesStarted"), Theme.Success);
            else
                OnLog?.Invoke(_("Log_EnginesShutdown"), Theme.Warning);
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
            OnLog?.Invoke(_("Log_SimulatorConnected", _fsuipc.SimulatorName), Theme.SecondaryText);
            SystemInfoHelper.SetSimVersion(_fsuipc.SimulatorName);
            if (!string.IsNullOrEmpty(SystemInfoHelper.SimSummary))
                OnLog?.Invoke(SystemInfoHelper.SimSummary, Theme.SecondaryText);

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
            LogLnmDatabaseStatus();
        }

        private void LogLnmDatabaseStatus()
        {
            Task navCheck = Task.Run(async () =>
            {
                var result = await NavDataClient.TestApiAsync().ConfigureAwait(false);
                if (!result.Reachable)
                    OnLog?.Invoke(_("Log_NavDataApiDown", AppConfig.NavDataApiUrl), Theme.Warning);
                else if (!result.KeyValid)
                    OnLog?.Invoke(_("Log_NavDataApiNoKey"), Theme.Warning);
                else
                {
                    OnLog?.Invoke(_("Log_NavDataApiOk", AppConfig.NavDataApiUrl, result.NavStatus?.AiracCycle), Theme.Success);
                    if (NavDataClient.IsAiracExpired)
                    {
                        OnLog?.Invoke(_("Log_NavDataAiracExpired", NavDataClient.AiracCycle, NavDataClient.AiracValidUntil), Theme.Warning);
                        OnOsdMessage?.Invoke($"AIRAC {NavDataClient.AiracCycle}  EXPIRED", OsdSeverity.Warning);
                    }
                }
            });
        }

        private void LogNavDataPrefetch(string icao, bool isOrigin)
        {
            _runwayService.PrefetchAirport(icao);
            Task t = Task.Run(() =>
            {
                // GetRunways/etc. block via GetResult until prefetch task completes
                var rwys  = NavDataClient.GetRunways(icao);
                var twys  = NavDataClient.GetTaxiways(icao);
                var parks = NavDataClient.GetParkings(icao);
                var apps  = NavDataClient.GetApproaches(icao);
                var info  = NavDataClient.GetAirportInfo(icao);

                if (isOrigin && info?.TransitionAltitudeFt > 0)
                    _flightManager.SetOriginTransitionAlt(info.TransitionAltitudeFt.Value);
                else if (!isOrigin && info?.TransitionLevelFt > 0)
                    _flightManager.SetDestTransitionLevel(info.TransitionLevelFt.Value);

                if (rwys.Count == 0 && twys.Count == 0)
                    OnLog?.Invoke(_("Log_NavDataPrefetchEmpty", icao), Theme.Warning);
                else
                    OnLog?.Invoke(_("Log_NavDataPrefetchOk", icao, rwys.Count, twys.Count, parks.Count, apps.Count), Theme.Success);
            });
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

        public SimbriefPlan ActivePlan => _flightManager?.ActivePlan;

        public void SetActivePlan(SimbriefPlan plan)
        {
            if (plan == null) return;
            _flightManager.SetActivePlan(plan);
            OnPlanChanged?.Invoke(plan);
            UpdateFlightInfo();
            _metarService.SetStations(plan.Origin, plan.Destination, plan.Alternate);
            if (_fsuipc.IsConnected)
            {
                _flightManager.UpdatePositionValidation(_fsuipc.CurrentLatitude, _fsuipc.CurrentLongitude);
                OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
            }
            OnButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), true);

            // Resolve SID/STAR restrictions async — populates _procFixes for OSD + scoring
            _procFixes  = null;
            _procFixIdx = 0;
            _procSpdViolations  = 0;
            _procFixAnnounced   = false;
            Task.Run(() => LoadProcedureRestrictions(plan));
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

        // ── Procedure restriction tracking ────────────────────────────────────────

        /// <summary>
        /// Resolves SID/STAR procedure legs from NavData and builds the ordered list of
        /// waypoints that have altitude or speed restrictions. Called async on plan activation.
        /// </summary>
        private void LoadProcedureRestrictions(SimbriefPlan plan)
        {
            try
            {
                if (plan?.Waypoints == null || plan.Waypoints.Count == 0) return;

                NavProcedure sidProc = null, starProc = null;
                if (!string.IsNullOrEmpty(plan.Origin))
                    sidProc  = ResolveProcedure(
                        plan.Waypoints.Where(w => (w.Stage ?? "CRZ") == "CLB" && w.Type != "apt")
                                      .Select(w => w.Ident).ToList(),
                        NavDataClient.GetSids(plan.Origin), plan.OriginRunway);

                if (!string.IsNullOrEmpty(plan.Destination))
                    starProc = ResolveProcedure(
                        plan.Waypoints.Where(w => (w.Stage ?? "CRZ") == "DSC" && w.Type != "apt")
                                      .Select(w => w.Ident).ToList(),
                        NavDataClient.GetStars(plan.Destination), plan.DestinationRunway);

                // Build restriction lookup
                var restrDict = new Dictionary<string, FixRestriction>(StringComparer.OrdinalIgnoreCase);
                foreach (var proc in new[] { sidProc, starProc })
                {
                    if (proc?.Legs == null) continue;
                    foreach (var leg in proc.Legs)
                    {
                        if (string.IsNullOrEmpty(leg.Fix)) continue;
                        if (!leg.AltitudeFt.HasValue && !leg.SpeedKts.HasValue) continue;
                        restrDict[leg.Fix] = new FixRestriction
                        {
                            AltFt    = leg.AltitudeFt,
                            Alt2Ft   = leg.Altitude2Ft,
                            AltDescr = leg.AltDescriptor,
                            SpeedKts = leg.SpeedKts,
                            SpdType  = leg.SpeedLimitType,
                        };
                    }
                }

                if (restrDict.Count == 0) return;

                // Collect CLB + DSC fixes (in order) that have a restriction
                var procFixes = new List<SimbriefWaypoint>();
                foreach (var wp in plan.Waypoints)
                {
                    if (wp.Type == "apt" || wp.Type == "latlon") continue;
                    string stage = wp.Stage ?? "CRZ";
                    if (stage != "CLB" && stage != "DSC") continue;
                    if (!restrDict.TryGetValue(wp.Ident ?? "", out FixRestriction r)) continue;
                    // Stamp the restriction onto the waypoint (in-memory copy is safe)
                    wp.Restriction = r;
                    procFixes.Add(wp);
                }

                _procFixes = procFixes.Count > 0 ? procFixes : null;
            }
            catch { /* non-critical — proceed without restriction tracking */ }
        }

        /// <summary>
        /// Simplified procedure match: finds the NavData procedure whose leg idents best
        /// overlap with the plan waypoints for that stage.
        /// </summary>
        private static NavProcedure ResolveProcedure(
            IList<string> planIdents, IList<NavProcedure> procedures, string runwayHint)
        {
            if (procedures == null || procedures.Count == 0 || planIdents.Count == 0)
                return null;

            NavProcedure best  = null;
            int          bestScore = 0;

            foreach (var proc in procedures)
            {
                if (!string.IsNullOrEmpty(runwayHint) && !string.IsNullOrEmpty(proc.Runway)
                    && !proc.Runway.Equals(runwayHint, StringComparison.OrdinalIgnoreCase)
                    && !proc.Runway.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (proc.Legs == null) continue;
                var legIdents = new HashSet<string>(
                    proc.Legs.Select(l => l.Fix ?? ""), StringComparer.OrdinalIgnoreCase);
                int score = planIdents.Count(id => legIdents.Contains(id));
                if (score > bestScore) { bestScore = score; best = proc; }
            }
            return best;
        }

        /// <summary>
        /// Called each telemetry cycle during CLB/DSC. Announces the next approaching fix (≤3 NM)
        /// via OSD, and records a speed violation if IAS exceeds the published max when passing (≤0.5 NM).
        /// </summary>
        private void CheckProcedureRestrictions(double lat, double lon, double iasKt)
        {
            var fixes = _procFixes;
            if (fixes == null || _procFixIdx >= fixes.Count) return;

            var wp = fixes[_procFixIdx];
            double distNm = HaversineNm(lat, lon, wp.Lat, wp.Lon);

            // Announce on approach (≤3 NM) — fire once per fix
            if (!_procFixAnnounced && distNm <= 3.0)
            {
                _procFixAnnounced = true;
                string restrLine = wp.Restriction?.OsdLine();
                string osdMsg = string.IsNullOrEmpty(restrLine)
                    ? wp.Ident
                    : $"{wp.Ident}  {restrLine}";
                OnOsdMessage?.Invoke($"{_("Osd_ProcNextFix")} {osdMsg}", OsdSeverity.Info);
                OnLog?.Invoke(
                    string.Format(_("Log_ProcFixApproaching"), wp.Ident, restrLine ?? ""),
                    Theme.MainText);
            }

            // Check speed violation when passing (≤0.5 NM) and there is a max speed restriction
            if (distNm <= 0.5)
            {
                if (wp.Restriction?.SpeedKts.HasValue == true
                    && (wp.Restriction.SpdType == "max" || wp.Restriction.SpdType == null)
                    && iasKt > wp.Restriction.SpeedKts.Value + 5)   // 5 kt tolerance
                {
                    _procSpdViolations++;
                    OnLog?.Invoke(
                        $"⚠ SPD RESTRICTION  {wp.Ident}  {(int)iasKt} kt  / {wp.Restriction.SpeedKts} kt limit",
                        Theme.Warning);
                }
                _procFixIdx++;
                _procFixAnnounced = false;
            }
        }

        private static double HaversineNm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 3440.065;   // Earth radius in NM
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
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
            OnLog?.Invoke(_("Log_FuelDiff", diffSymbol, $"{Math.Abs(diffInPlanUnits):F0}", plan.Units ?? "kg"), Theme.MainText);

            // Luego la validación
            OnLog?.Invoke(_("Log_FuelValidOk", $"{Math.Max(50.0, plannedFuelKg * 0.05):F0}"), Theme.Success);

            // Luego los detalles
            OnLog?.Invoke(_("Log_FuelPlanned", $"{plannedFuel:F0}", plan.Units, $"{plannedFuelKg:F0}"), Theme.MainText);
            OnLog?.Invoke(_("Log_FuelSim", $"{actualFuelLbs:F0}", $"{actualFuelKg:F0}"), Theme.MainText);

            // ===== 5. VERIFICAR PRESENCIA IVAO (informativo — penalización se aplica al iniciar TaxiOut) =====
            var activePilot = _flightManager.ActivePilot;
            if (activePilot?.IvaoId > 0)
            {
                bool? isOnline = await _ivaoService.IsOnlineAsync(activePilot.IvaoId);
                if (isOnline == false)
                    OnLog?.Invoke(_("Log_IvaoOffline", activePilot.IvaoId), Theme.Warning);
                else if (isOnline == true)
                    OnLog?.Invoke(_("Log_IvaoOnline", activePilot.IvaoId), Theme.Success);
            }

// ===== 6. INICIAR VUELO =====
            bool started = await _flightManager.StartFlight(plan, _flightManager.ActivePilot, actualFuelKg);
            if (started)
            {
                string acDev  = _fsuipc.GetAircraftDeveloper();
                string acType = _fsuipc.AircraftIcao != "????" ? _fsuipc.AircraftIcao : "Unknown";
                string acLine = string.IsNullOrEmpty(acDev) ? $"✈️ {acType}" : $"✈️ {acType}  [{acDev}]";
                OnLog?.Invoke(_("Log_SimRunning", _fsuipc.SimulatorName), Theme.MainText);
                OnLog?.Invoke(acLine, Theme.MainText);

                // Enviar info de sistema a la tabla ACARS de phpVMS al inicio del vuelo
                string _startPirepId = _flightManager.ActivePirepId;
                if (!string.IsNullOrEmpty(_startPirepId))
                {
                    double sLat = _flightManager.CurrentLat;
                    double sLon = _flightManager.CurrentLon;
                    int    sHdg = (int)_fsuipc.CurrentHeading;
                    string acLogEntry = string.IsNullOrEmpty(acDev) ? acType : $"{acType} / {acDev}";
                    var logPositions = new System.Collections.Generic.List<AcarsPosition>();
                    if (!string.IsNullOrEmpty(SystemInfoHelper.OsSummary))
                        logPositions.Add(new AcarsPosition { lat = sLat, lon = sLon, heading = sHdg, log = SystemInfoHelper.OsSummary,  status = "ground", source = "vmsOpenAcars" });
                    if (!string.IsNullOrEmpty(SystemInfoHelper.GpuSummary))
                        logPositions.Add(new AcarsPosition { lat = sLat, lon = sLon, heading = sHdg, log = SystemInfoHelper.GpuSummary, status = "ground", source = "vmsOpenAcars" });
                    if (!string.IsNullOrEmpty(SystemInfoHelper.SimSummary))
                        logPositions.Add(new AcarsPosition { lat = sLat, lon = sLon, heading = sHdg, log = SystemInfoHelper.SimSummary, status = "ground", source = "vmsOpenAcars" });
                    logPositions.Add(new AcarsPosition { lat = sLat, lon = sLon, heading = sHdg, log = acLogEntry, status = "ground", source = "vmsOpenAcars" });
                    var startupUpdate = new AcarsPositionUpdate { positions = logPositions.ToArray() };
                    Task.Run(async () => await _apiService.SendPositionUpdate(_startPirepId, startupUpdate));
                }

                LogNavDataPrefetch(plan.Origin,      isOrigin: true);
                LogNavDataPrefetch(plan.Destination, isOrigin: false);
                OnButtonStateChanged?.Invoke("ABORT", Color.Red, true);
                LogPlanSummary(plan);
                OnLog?.Invoke(_("FlightStarted"), Theme.Success);
                OnFlightStarted?.Invoke();
                OnOsdMessage?.Invoke("ACARS ACTIVE", OsdSeverity.Success);

                // Prefetch cabin announcements — boarding se encola al terminar
                _cabinCruiseSent       = false;
                _cabinOnRunwaySent     = false;
                _cabinCruiseCheckStart = DateTime.MinValue;
                _procFixIdx            = 0;
                _procSpdViolations     = 0;
                _procFixAnnounced      = false;
                Task.Run(async () => await _cabinAnnouncements.PrefetchAsync(
                    plan.Origin,
                    plan.Destination,
                    _flightManager.ActivePilot?.AirlineCountry ?? "",
                    _flightManager.ActivePilot?.AircraftSeats  ?? 0));
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
                _cabinAnnouncements.Reset();
                _cabinCruiseSent       = false;
                _cabinOnRunwaySent     = false;
                _cabinCruiseCheckStart = DateTime.MinValue;
                _procFixIdx            = 0;
                _procSpdViolations     = 0;
                _procFixAnnounced      = false;
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
                _cabinAnnouncements.Reset();
                _cabinCruiseSent       = false;
                _cabinOnRunwaySent     = false;
                _cabinCruiseCheckStart = DateTime.MinValue;
                _procFixIdx            = 0;
                _procSpdViolations     = 0;
                _procFixAnnounced      = false;
                OnLog?.Invoke("✖️ Flight aborted", Theme.Warning);
                OnFlightEnded?.Invoke();
            }
        }

        public async Task SendPirep()
        {
            // Pass procedure speed violations before score is calculated inside FilePirep
            _flightManager.SetProcedureSpdViolations(_procSpdViolations);

            // Snapshot plan + touchdown data before FilePirep resets state
            var pendingRecord = SnapshotLandingRecord();

            if (await _flightManager.FilePirep())
            {
                int pirepScore = _flightManager.LastFlightScore;
                OsdSeverity scoreSev = pirepScore >= 80 ? OsdSeverity.Success
                                     : pirepScore >= 60 ? OsdSeverity.Info
                                     : OsdSeverity.Warning;
                OnOsdMessage?.Invoke($"PIREP FILED   SCORE {pirepScore} / 100", scoreSev);

                OnButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), false);
                _lastTelemetry = null;
                _lastPositionUpdate = DateTime.MinValue;
                _cabinAnnouncements.Reset();
                _cabinCruiseSent       = false;
                _cabinOnRunwaySent     = false;
                _cabinCruiseCheckStart = DateTime.MinValue;
                OnLog?.Invoke("✅ Vuelo reportado, listo para siguiente vuelo", Theme.Success);
                OnFlightEnded?.Invoke();
                SaveLandingRecord(pendingRecord);
                // Fire-and-forget: refresh pilot airport once phpVMS processes the PIREP
                Task.Run(RefreshPilotDataAfterPirep);
            }
        }

        // Called before FilePirep() so plan and touchdown data are still populated.
        private FlightRecord SnapshotLandingRecord()
        {
            var fm   = _flightManager;
            var plan = fm.ActivePlan;
            return new FlightRecord
            {
                FlightNumber    = plan?.FlightNumber     ?? "",
                Origin          = plan?.Origin           ?? "",
                Destination     = plan?.Destination      ?? "",
                RunwayName      = fm.TouchdownRunwayName ?? "",
                FlightDate      = DateTime.UtcNow,
                LandingRateFpm  = fm.TouchdownFpm        ?? 0,
                GForce          = fm.TouchdownGForce,
                TouchdownDistFt = fm.TouchdownDistanceFt,
                CenterlineDevFt = fm.TouchdownCenterlineFt,
            };
        }

        private void SaveLandingRecord(FlightRecord record)
        {
            int bufCount = _approachBuffer?.Count ?? 0;
            bool svcOk   = _landingLogService?.IsAvailable ?? false;

            if (!svcOk)
            {
                OnLog?.Invoke(_("Log_LandingLogNoService"), Theme.Warning);
                return;
            }
            if (bufCount < 3)
            {
                OnLog?.Invoke(_("Log_LandingLogTooFew", bufCount), Theme.Warning);
                return;
            }
            try
            {
                // Score is computed inside FilePirep and is not reset by ResetFlightState
                record.Score = _flightManager.LastFlightScore;

                int newId = _landingLogService.SaveFlight(record, _approachBuffer);
                if (newId > 0)
                {
                    OnLog?.Invoke(_("Log_LandingLogSaved", newId, bufCount, record.RunwayName), Theme.Success);
                }
                else
                {
                    OnLog?.Invoke(_("Log_LandingLogBadId", newId), Theme.Danger);
                }
                _approachBuffer.Clear();
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(_("Log_LandingLogError", ex.Message), Theme.Danger);
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
                    OnLog?.Invoke(_("Log_BaseUpdated", result.Data.CurrentAirport), Theme.Success);
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
                OnLog?.Invoke(_("Log_BaseUpdateError", ex.Message), Theme.Warning);
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
                                OnLog?.Invoke(_("Log_ActiveFlightDeleteFail", pirep.FlightNumber), Theme.Danger);
                                allDeleted = false;
                            }
                            else
                            {
                                OnLog?.Invoke(_("Log_OrphanedFlightDeleted", pirep.FlightNumber), Theme.Success);
                            }
                        }

                        if (allDeleted)
                        {
                            OnLog?.Invoke(_("Log_OrphansCleared"), Theme.Success);
                        }
                        else
                        {
                            OnLog?.Invoke(_("Log_OrphansPartial"), Theme.Warning);
                        }

                        return allDeleted;
                    }
                    else
                    {
                        OnLog?.Invoke(_("Log_PlannerCancelled"), Theme.MainText);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(_("Log_ActiveFlightsError", ex.Message), Theme.Danger);
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
                    OnLog?.Invoke(_("Log_FlightResumed"), Theme.Success);
                }
                else
                {
                    OnLog?.Invoke(_("Log_ResumeDeclined"), Theme.MainText);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(_("Log_ResumeCheckError", ex.Message), Theme.Warning);
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
                    OnLog?.Invoke(_("Log_AirportAssigned", pilot.CurrentAirport), Theme.MainText);
                    OnAirportChanged?.Invoke(pilot.CurrentAirport);
                    OnAcarsStatusChanged?.Invoke(true);

                    if (_fsuipc.IsConnected)
                    {
                        _flightManager.UpdatePositionValidation(
                            _fsuipc.CurrentLatitude, _fsuipc.CurrentLongitude);
                        OnLog?.Invoke(_("Log_AltitudeFt", $"{_fsuipc.CurrentAltitudeFeet:F0}"), Theme.MainText);
                        OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
                    }
                    await CheckAndResumeFlight(pilot);
                    return true;
                }

                OnLog?.Invoke(_("Log_LoginError", result.Error), Theme.Danger);
                OnAcarsStatusChanged?.Invoke(false);
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(_("Log_LoginException", ex.Message), Theme.Danger);
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

        public void LogButtonPress(string buttonText) => OnLog?.Invoke(_("Log_ButtonPress", buttonText), Theme.MainText);

        public Task<string> TestCabinAnnouncementAsync(string phase)
            => _cabinAnnouncements.TestAnnouncementAsync(phase, "en");

        public void SetCabinVolume(int volume)
            => _cabinAnnouncements.SetVolume(volume);

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
            OnLog?.Invoke(_("Log_FlightFromBid"), Theme.MainText);
        }

        public async Task<List<Flight>> LoadPilotBids()
        {
            try
            {
                if (_flightManager.ActivePilot == null)
                {
                    OnLog?.Invoke(_("Log_NoPilot"), Theme.Warning);
                    return new List<Flight>();
                }
                return await _apiService.GetPilotBids() ?? new List<Flight>();
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(_("Log_BidsLoadError", ex.Message), Theme.Danger);
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