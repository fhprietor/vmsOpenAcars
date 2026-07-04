using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Core.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.Services.Interfaces;
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
        public IApiService ApiService => _apiService;
        public Pilot ActivePilot => _flightManager.ActivePilot;
        public ILandingLogService LandingLogService => _landingLogService;

        private readonly FlightManager _flightManager;
        private readonly FsuipcService _fsuipc;
        private readonly IApiService _apiService;
        private CancellationTokenSource _timerCts;
        private readonly PhpVmsFlightService _phpVmsFlightService;
        private readonly SimbriefEnhancedService _simbriefEnhancedService;
        private readonly IMetarService     _metarService;
        private readonly IvaoService       _ivaoService  = new IvaoService();
        private readonly INavDataService   _runwayService;
        private readonly ILandingLogService _landingLogService;
        private TelemetryCoordinator _tc;
        private AcarsReporter        _reporter;

        private int _lastEngineRpm = 0;
        private readonly Dictionary<string, IvaoAtcStation> _prevAtcStations
            = new Dictionary<string, IvaoAtcStation>(StringComparer.OrdinalIgnoreCase);

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
        public event Action<string, OsdSeverity>    OnOsdMessage;
        internal event Action<IList<NavAirspace>>     OnAirspacesReady;
        internal event Action<IList<IvaoAtcStation>> OnAtcStationsUpdated;

        public MainViewModel(
            FlightManager flightManager,
            FsuipcService fsuipc,
            IApiService apiService,
            PhpVmsFlightService phpVmsFlightService,
            SimbriefEnhancedService simbriefEnhancedService,
            IMetarService metarService,
            INavDataService navDataService,
            ILandingLogService landingLogService)
        {
            _flightManager           = flightManager;
            _fsuipc                  = fsuipc;
            _apiService              = apiService;
            _phpVmsFlightService     = phpVmsFlightService;
            _simbriefEnhancedService = simbriefEnhancedService;
            _metarService            = metarService;
            _runwayService           = navDataService;
            _landingLogService       = landingLogService;

            SubscribeToEvents();
            WireAirspaceMonitor();
            _flightManager.IsOnAtcFrequency = IsAtcOnCom1;
            _tc = new TelemetryCoordinator(
                _flightManager, _fsuipc, navDataService, _airspaceMonitor,
                _cabinAnnouncements, landingLogService,
                new TelemetryCallbacks
                {
                    Log                     = (msg, color) => OnLog?.Invoke(msg, color),
                    OsdMessage              = (msg, sev)   => OnOsdMessage?.Invoke(msg, sev),
                    FlightInfoChanged       = ()            => OnFlightInfoChanged?.Invoke(),
                    MapPositionUpdate       = (lat,lon,hdg) => OnMapPositionUpdate?.Invoke(lat, lon, hdg),
                    PositionUpdate          = pos           => OnPositionUpdate?.Invoke(pos),
                    PhaseChanged            = p             => OnPhaseChanged?.Invoke(p),
                    AirStatusChanged        = p             => OnAirStatusChanged?.Invoke(p),
                    ValidationStatusChanged = vs            => OnValidationStatusChanged?.Invoke(vs),
                    AcarsStatusChanged      = ok            => OnAcarsStatusChanged?.Invoke(ok),
                    SimulatorNameChanged    = name          => OnSimulatorNameChanged?.Invoke(name),
                });
            _tc.WireEvents();

            _reporter = new AcarsReporter(
                _flightManager, _apiService, _fsuipc,
                landingLogService, simbriefEnhancedService, _tc,
                new AcarsReporterCallbacks
                {
                    Log                     = (msg, color) => OnLog?.Invoke(msg, color),
                    OsdMessage              = (msg, sev)   => OnOsdMessage?.Invoke(msg, sev),
                    ButtonStateChanged      = (lbl, c, en) => OnButtonStateChanged?.Invoke(lbl, c, en),
                    FlightEnded             = ()            => OnFlightEnded?.Invoke(),
                    AirportChanged          = icao          => OnAirportChanged?.Invoke(icao),
                    ValidationStatusChanged = vs            => OnValidationStatusChanged?.Invoke(vs),
                    ResetTelemetry          = ()            => _tc?.Reset(),
                    ShowConfirmation        = (msg, t, b)   => OnShowConfirmation?.Invoke(msg, t, b),
                    SetActivePlan           = plan          => SetActivePlan(plan),
                    UpdateFlightInfo        = ()            => UpdateFlightInfo(),
                });

            _procTracker.Log          = (msg, color) => OnLog?.Invoke(msg, color);
            _procTracker.OsdMessage   = (msg, sev)   => OnOsdMessage?.Invoke(msg, sev);
            _procTracker.OnSpdViolation = ()          => { if (_reporter != null) _reporter.ProcSpdViolations++; };
        }

        private void WireAirspaceMonitor()
        {
            _airspaceMonitor.OnAirspaceAlert       += a => LogBoundedAirspace(a, "AIRSPACE",       OsdSeverity.Critical);
            _airspaceMonitor.OnAirspaceApproaching += a => LogBoundedAirspace(a, "AIRSPACE AHEAD", OsdSeverity.Warning);

            _airspaceMonitor.OnAirspaceOverflight += a =>
            {
                string icao  = a.ExtractIcao() ?? a.Name;
                string upper = a.UpperLimit?.Display ?? "UNL";
                OnLog?.Invoke(
                    $"⚠️ OVERFLIGHT  {a.Type.ToUpper()}  {icao}  [ABOVE {upper}]  DO NOT DESCEND",
                    Theme.Warning);
                OnOsdMessage?.Invoke(
                    $"ABOVE  {icao}  DO NOT DESCEND",
                    OsdSeverity.Warning);
            };

            _airspaceMonitor.OnAirspaceEntered += (a, freq) =>
            {
                string icao    = a.ExtractIcao() ?? a.Name;
                string freqStr = freq != null ? $"  {freq.Mhz} MHz" : string.Empty;
                OnLog?.Invoke($"📡 {a.Type}  {icao}{freqStr}", Theme.MainText);
                OnOsdMessage?.Invoke($"{a.Type}  {icao}{freqStr}", OsdSeverity.Info);
            };

            _airspaceMonitor.OnAirspaceExited += a =>
            {
                string icao = a.ExtractIcao() ?? a.Name;
                OnLog?.Invoke($"📡 {a.Type}  {icao}  —  left", Theme.SecondaryText);
            };

            _airspaceMonitor.OnAtcUpdated += stations =>
            {
                var nowMap = stations.ToDictionary(s => s.Callsign,
                    StringComparer.OrdinalIgnoreCase);

                // New or changed stations (ATIS excluded from log)
                foreach (var s in stations.Where(s => s.Position != "ATIS"))
                {
                    IvaoAtcStation prev;
                    bool isNew     = !_prevAtcStations.TryGetValue(s.Callsign, out prev);
                    bool isChanged = !isNew
                        && (prev.Frequency != s.Frequency || prev.AtisText != s.AtisText);
                    if (isNew || isChanged)
                    {
                        string freq = s.Frequency > 0 ? $" {s.Frequency:F3}" : string.Empty;
                        OnLog?.Invoke($"📻 {s.Icao}  {s.Position}{freq}", Theme.SecondaryText);
                    }
                }

                // Stations that went offline
                foreach (var prev in _prevAtcStations.Values.Where(s => s.Position != "ATIS"))
                {
                    if (!nowMap.ContainsKey(prev.Callsign))
                        OnLog?.Invoke($"📻 {prev.Icao}  {prev.Position}  offline",
                            Theme.SecondaryText);
                }

                _prevAtcStations.Clear();
                foreach (var s in stations)
                    _prevAtcStations[s.Callsign] = s;

                OnAtcStationsUpdated?.Invoke(stations);
            };
        }

        private void LogBoundedAirspace(NavAirspace a, string tag, OsdSeverity sev)
        {
            string icao   = a.ExtractIcao() ?? a.Name;
            string type   = a.Type.ToUpper();
            string bounds = $"[{a.LowerLimit?.Display ?? "SFC"} – {a.UpperLimit?.Display ?? "UNL"}]";
            OnLog?.Invoke($"⚠️ {tag}  {type}  {icao}  {bounds}", Theme.Warning);
            OnOsdMessage?.Invoke($"{tag}  {type}  {icao}", sev);
        }

        private void TriggerAirspaceLoadAsync(string orig, string dest)
        {
            Task.Run(async () =>
            {
                await _airspaceMonitor.InitRouteAsync(orig, dest,
                    _fsuipc?.CurrentLatitude ?? 0, _fsuipc?.CurrentLongitude ?? 0);
                var spaces = _airspaceMonitor.GetAirspaces();
                if (spaces.Count > 0)
                {
                    OnLog?.Invoke($"🗺️ {spaces.Count} airspaces loaded for route", Theme.SecondaryText);
                    OnAirspacesReady?.Invoke(spaces);
                }
            });
        }

        public IList<IvaoAtcStation> GetAtcStations() => _airspaceMonitor.GetAtcStations();
        internal IList<NavAirspace>  GetAirspaces()   => _airspaceMonitor.GetAirspaces();

        private bool IsAtcOnCom1()
        {
            double com1 = _fsuipc?.Com1FrequencyMhz ?? 0;
            if (com1 < 118.0) return false;
            var stations = _airspaceMonitor.GetAtcStations();
            return stations.Any(s => Math.Abs(s.Frequency - com1) < 0.005);
        }
        internal FsuipcService.AircraftCategory GetAircraftCategory()
        {
            var cat = _fsuipc?.EngineCategory ?? FsuipcService.AircraftCategory.Unknown;
            if (cat != FsuipcService.AircraftCategory.Unknown) return cat;
            return _fsuipc?.GetAircraftCategory() ?? FsuipcService.AircraftCategory.Unknown;
        }

        private void SubscribeToEvents()
        {
            _flightManager.OnLog               += OnFlightManagerLog;
            _flightManager.OnOsdMessage        += OnFlightManagerOsd;
            _flightManager.PhaseChanged        += OnFlightPhaseChanged;
            _flightManager.OnPositionValidated += OnPositionValidated;
            _flightManager.OnAirportChanged    += OnAirportChanged;
            _flightManager.OnLandingDetected   += OnLandingDetected;
            _flightManager.OnBlockDetected     += OnBlockDetected;
            _flightManager.OnTakeoffDetected   += OnTakeoffDetected;
            _flightManager.OnTaxiPositionUpdate += HandleTaxiPositionUpdate;

            _metarService.OnMetarUpdated += metars => OnMetarUpdated?.Invoke(metars);
            _metarService.OnStateChanged += state  => OnMetarStateChanged?.Invoke(state);
            _metarService.OnLog          += (msg, color) => OnLog?.Invoke(msg, color);
        }
        private FlightPhase _prevPhase = FlightPhase.Idle;
        private readonly CabinAnnouncementService      _cabinAnnouncements  = new CabinAnnouncementService();
        private readonly AirspaceMonitorService        _airspaceMonitor     = new AirspaceMonitorService();
        private readonly ProcedureRestrictionTracker   _procTracker         = new ProcedureRestrictionTracker();
        private const int CheckpointIntervalSeconds = 60;

        private void OnRawDataUpdated(object sender, RawTelemetryData e)
        {
            _metarService?.UpdatePosition(e.Latitude, e.Longitude);
            var curPhase = _flightManager?.CurrentPhase;
            if (curPhase == FlightPhase.Climb || curPhase == FlightPhase.Descent)
                _procTracker.Check(e.Latitude, e.Longitude, e.IndicatedAirspeedKt);
        }

        private void OnFlightManagerLog(string msg, Color color) => OnLog?.Invoke(msg, color);
        private void OnFlightManagerOsd(string msg, OsdSeverity sev) => OnOsdMessage?.Invoke(msg, sev);
        private async void OnFlightPhaseChanged(FlightPhase phase)
        {
            if (phase == FlightPhase.TaxiOut) await CheckIvaoAtBlocksOffAsync();

            _tc?.OnPhaseChanged(phase, _prevPhase);

            switch (phase)
            {
                case FlightPhase.TaxiOut:     OnOsdMessage?.Invoke("TAXI OUT",     OsdSeverity.Info); break;
                case FlightPhase.TakeoffRoll: OnOsdMessage?.Invoke("TAKEOFF ROLL", OsdSeverity.Info); break;
                case FlightPhase.Enroute:     OnOsdMessage?.Invoke("CRUISE",       OsdSeverity.Info); break;
                case FlightPhase.Descent:     OnOsdMessage?.Invoke("DESCENDING",   OsdSeverity.Info); break;
                case FlightPhase.Approach:    OnOsdMessage?.Invoke("APPROACH",     OsdSeverity.Info); break;
                case FlightPhase.OnBlock:     OnOsdMessage?.Invoke("ON BLOCK",     OsdSeverity.Info); break;
                case FlightPhase.Climb:
                    if (_prevPhase == FlightPhase.AfterLanding)
                        OnOsdMessage?.Invoke("TOUCH AND GO", OsdSeverity.Warning);
                    break;
            }

            _prevPhase = phase;

            OnPhaseChanged?.Invoke(phase);
            if ((phase == FlightPhase.OnBlock || phase == FlightPhase.Completed)
                && !string.IsNullOrEmpty(_flightManager.ActivePirepId))
                OnButtonStateChanged?.Invoke("SEND PIREP", Color.Green, true);
        }

        private async Task CheckIvaoAtBlocksOffAsync()
        {
            var pilot = _flightManager.ActivePilot;
            if (pilot?.IvaoId <= 0) return;
            bool? isOnline = await _ivaoService.IsOnlineAsync(pilot.IvaoId);
            if (isOnline == true)        OnLog?.Invoke(_("Ivao_Online",         pilot.IvaoId), Theme.Success);
            else if (isOnline == false) { OnLog?.Invoke(_("Ivao_OfflinePenalty", pilot.IvaoId), Theme.Warning); _flightManager.MarkOfflineFlight(); }
            else                         OnLog?.Invoke(_("Ivao_FeedUnavailable"), Theme.Warning);
        }
        private void OnPositionValidated(ValidationStatus status) => OnValidationStatusChanged?.Invoke(status);
        private void OnTakeoffDetected(int speed, int altitude, int verticalSpeed)
            => OnLog?.Invoke(_("Log_TakeoffDetected", speed, altitude, verticalSpeed), Theme.Success);
        private void OnBlockDetected()    => _reporter?.HandleBlockDetected();
        private void OnLandingDetected(int vs, double g, double p, double b)
            => _reporter?.HandleLandingDetected(vs, g, p, b);

        private void HandleTaxiPositionUpdate(
            double lat, double lon, double heading, string airport, bool isTaxiIn)
            => _tc?.HandleTaxiPositionUpdate(lat, lon, heading, airport, isTaxiIn);

        public void Start()
        {
            _fsuipc?.Start();
            _timerCts = new CancellationTokenSource();
            var timerTask = RunTimerLoopAsync(_timerCts.Token);
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
            _timerCts?.Cancel();
            _fsuipc?.Stop();
            (_metarService as IDisposable)?.Dispose();
            _ivaoService?.Dispose();
        }

        private async Task RunTimerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(1000, ct); }
                catch (TaskCanceledException) { break; }
                try { await OnTimerTickAsync(); }
                catch (Exception ex) { OnLog?.Invoke($"Timer error: {ex.Message}", Theme.Danger); }
            }
        }

        private async Task OnTimerTickAsync()
        {
            UpdateFlightInfo();

            OnFlightInfoChanged?.Invoke();   // Esto llamará a UpdateFlightInfoPanel()

            if (!string.IsNullOrEmpty(_flightManager?.ActivePirepId) && _tc?.LastTelemetry != null)
            {
                if (DateTime.UtcNow - _tc.LastPositionUpdate >= _tc.PositionUpdateInterval)
                {
                    bool success = await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, _tc.LastTelemetry);
                    if (success)
                    {
                        _tc.LastPositionUpdate = DateTime.UtcNow;
                        await _flightManager.UpdateFlightProgress();
                        _tc.LastTelemetry = null;
                    }
                    OnAcarsStatusChanged?.Invoke(success);
                }

                // Checkpoint de penalizaciones cada 60 s
                if (_reporter?.ShouldSendCheckpoint(CheckpointIntervalSeconds) == true)
                    await _reporter.SendScoringCheckpointAsync();
            }
            UpdateSimulatorName();
        }

        private void UpdateSimulatorName()
        {
            OnSimulatorNameChanged?.Invoke(_fsuipc?.IsConnected == true ? _fsuipc.SimulatorName : "AWAITING SIM");
        }

        public async Task TriggerMetarFetchAsync() => await _metarService.FetchNowAsync();

        public SimbriefPlan ActivePlan => _flightManager?.ActivePlan;

        public void UpdateProcedureOverrides(
            string originRwy, string sidName, string destRwy, string starName)
        {
            var plan = _flightManager?.ActivePlan;
            if (plan == null) return;
            if (originRwy != null) plan.OriginRunway      = originRwy;
            if (sidName   != null) plan.SidName           = sidName;
            if (destRwy   != null) plan.DestinationRunway = destRwy;
            if (starName  != null) plan.StarName          = starName;
        }

        public void SetActivePlan(SimbriefPlan plan)
        {
            if (plan == null) return;
            _flightManager.SetActivePlan(plan);

            // Advisory: advertir si el avión del OFP no coincide con el del simulador
            if (_fsuipc.IsConnected)
            {
                string simType  = _fsuipc.AircraftIcao ?? "";
                string planType = plan.AircraftIcao    ?? "";
                if (!string.IsNullOrEmpty(simType)  && simType  != "????" &&
                    !string.IsNullOrEmpty(planType) && planType != "????" &&
                    !string.Equals(simType, planType, StringComparison.OrdinalIgnoreCase))
                {
                    OnLog?.Invoke(
                        $"⚠️ Aircraft mismatch — Simulator: {simType} / OFP: {planType}",
                        Theme.Warning);
                }
            }

            OnPlanChanged?.Invoke(plan);
            UpdateFlightInfo();
            _metarService.SetStations(plan.Origin, plan.Destination, plan.Alternate);
            if (_fsuipc.IsConnected)
            {
                _flightManager.UpdatePositionValidation(_fsuipc.CurrentLatitude, _fsuipc.CurrentLongitude);
                OnValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
            }
            OnButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), true);

            // Resolve SID/STAR restrictions async — populates fixes for OSD + scoring
            if (_reporter != null) _reporter.ProcSpdViolations = 0;
            Task.Run(() => _procTracker.Load(plan));

            // Start airspace + IVAO polling immediately so the map shows ATC before flight start
            TriggerAirspaceLoadAsync(plan.Origin, plan.Destination);
        }

        public void UpdateFlightInfo()
        {
            var p = _flightManager?.ActivePlan;
            OnFlightNoChanged?.Invoke(p != null ? $"FLT NO: {p.Airline}{p.FlightNumber}"        : "FLT NO: ----");
            OnDepArrChanged?.Invoke(  p != null ? $"DEP/ARR: {p.Origin}/{p.Destination}"        : "DEP/ARR: ----/----");
            OnAlternateChanged?.Invoke(p != null ? $"ALTN: {p.Alternate ?? "N/A"}"              : "ALTN: ----");
            OnRouteChanged?.Invoke(   p != null ? $"ROUTE: {p.Route}"                           : "ROUTE: ----");
            OnAircraftChanged?.Invoke(p != null ? $"ACFT: {p.AircraftIcao}"                     : "ACFT: ----");
            OnFuelChanged?.Invoke(    p != null ? $"FUEL: {p.BlockFuel:F0} {p.Units ?? "kg"}"   : "FUEL: ---- kg");
            OnTypeChanged?.Invoke(    p != null ? $"TYPE: {p.Aircraft}"                         : "TYPE: ----");
            OnRegistrationChanged?.Invoke(p != null ? $"REG: {p.Registration}"                  : "REG: ----");
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

            if (!await ValidateAircraftTypeAsync(plan)) return;

            double? fuelKg = await ValidateAndLogFuelAsync(plan);
            if (fuelKg == null) return;

            await CheckIvaoStatusAsync();

            bool started = await _flightManager.StartFlight(plan, _flightManager.ActivePilot, fuelKg.Value);
            if (started)
                OnFlightStartedAsync(plan);
        }

        private async Task<bool> ValidateAircraftTypeAsync(SimbriefPlan plan)
        {
            string simType  = _fsuipc.AircraftIcao ?? "";
            string planType = plan.AircraftIcao    ?? "";
            bool canCheck   = !string.IsNullOrEmpty(simType)  && simType  != "????" &&
                              !string.IsNullOrEmpty(planType) && planType != "????";
            if (!canCheck || string.Equals(simType, planType, StringComparison.OrdinalIgnoreCase))
                return true;

            string warnMsg = $"⚠️ AIRCRAFT MISMATCH\n\nSimulator: {simType}\nOFP (SimBrief): {planType}\n\nThe OFP was generated for a different aircraft type. Fuel and performance data may not be accurate.\n\nDo you want to start the flight anyway?";
            OnLog?.Invoke($"⚠️ Aircraft mismatch — Sim: {simType} / OFP: {planType}", Theme.Warning);
            if (OnShowConfirmation != null && await OnShowConfirmation(warnMsg, "AIRCRAFT MISMATCH", EcamDialogButtons.YesNo) != DialogResult.Yes)
                return false;
            return true;
        }

        private async Task<double?> ValidateAndLogFuelAsync(SimbriefPlan plan)
        {
            double actualFuelLbs = _fsuipc.CurrentFuelLbs;
            double actualFuelKg  = actualFuelLbs * 0.453592;
            double plannedFuel   = plan.BlockFuel;
            double plannedFuelKg = plan.Units?.ToUpper() == "LBS" ? plannedFuel * 0.453592 : plannedFuel;
            double diffKg        = actualFuelKg - plannedFuelKg;
            double diffPlanUnits = plan.Units?.ToUpper() == "LBS" ? diffKg / 0.453592 : diffKg;
            if (!IsFuelWithinTolerance(plannedFuelKg, actualFuelKg))
            {
                double toleranceKg = Math.Max(50.0, plannedFuelKg * 0.05);
                string msg = $"❌ FUEL VALIDATION FAILED\n\nPlanned: {plannedFuelKg:F0} kg\nSimulator: {actualFuelKg:F0} kg\nDifference: {Math.Abs(diffKg):F0} kg\nTolerance: {toleranceKg:F0} kg";
                OnLog?.Invoke(msg, Theme.Danger);
                if (OnShowConfirmation != null) await OnShowConfirmation(msg, "FUEL ERROR", EcamDialogButtons.OK);
                return null;
            }
            string sym = diffPlanUnits > 0 ? "+" : "";
            OnLog?.Invoke(_("Log_FuelDiff", sym, $"{Math.Abs(diffPlanUnits):F0}", plan.Units ?? "kg"), Theme.MainText);
            OnLog?.Invoke(_("Log_FuelValidOk", $"{Math.Max(50.0, plannedFuelKg * 0.05):F0}"), Theme.Success);
            OnLog?.Invoke(_("Log_FuelPlanned", $"{plannedFuel:F0}", plan.Units, $"{plannedFuelKg:F0}"), Theme.MainText);
            OnLog?.Invoke(_("Log_FuelSim", $"{actualFuelLbs:F0}", $"{actualFuelKg:F0}"), Theme.MainText);
            return actualFuelKg;
        }

        private async Task CheckIvaoStatusAsync()
        {
            var pilot = _flightManager.ActivePilot;
            if (pilot?.IvaoId <= 0) return;
            bool? online = await _ivaoService.IsOnlineAsync(pilot.IvaoId);
            if (online == false) OnLog?.Invoke(_("Log_IvaoOffline", pilot.IvaoId), Theme.Warning);
            else if (online == true) OnLog?.Invoke(_("Log_IvaoOnline", pilot.IvaoId), Theme.Success);
        }

        private void OnFlightStartedAsync(SimbriefPlan plan)
        {
            string acDev  = _fsuipc.GetAircraftDeveloper();
            string acType = _fsuipc.AircraftIcao != "????" ? _fsuipc.AircraftIcao : "Unknown";
            string acLine = string.IsNullOrEmpty(acDev) ? $"✈️ {acType}" : $"✈️ {acType}  [{acDev}]";
            OnLog?.Invoke(_("Log_SimRunning", _fsuipc.SimulatorName), Theme.MainText);
            OnLog?.Invoke(acLine, Theme.MainText);

            string pirepId = _flightManager.ActivePirepId;
            if (!string.IsNullOrEmpty(pirepId))
            {
                double sLat = _flightManager.CurrentLat, sLon = _flightManager.CurrentLon;
                int    sHdg = (int)_fsuipc.CurrentHeading;
                AcarsPosition Pos(string log) => new AcarsPosition { lat = sLat, lon = sLon, heading = sHdg, log = log, type = 2, status = "SCH", source = "vmsOpenAcars" };
                var logs = new[] {
                    $"vmsOpenAcars v{AppInfo.Version}",
                    SystemInfoHelper.CpuSummary, SystemInfoHelper.GpuSummary, SystemInfoHelper.OsSummary,
                    NavDataClient.IsReachable && !string.IsNullOrEmpty(NavDataClient.AiracCycle) ? $"NavData API: AIRAC {NavDataClient.AiracCycle}" : null
                }.Where(s => !string.IsNullOrEmpty(s)).Select(Pos).ToArray();
                Task.Run(async () => await _apiService.SendPositionUpdate(pirepId, new AcarsPositionUpdate { positions = logs }));
            }

            LogNavDataPrefetch(plan.Origin,      isOrigin: true);
            LogNavDataPrefetch(plan.Destination, isOrigin: false);
            OnButtonStateChanged?.Invoke("ABORT", Color.Red, true);
            _reporter.LogPlanSummary(plan);
            OnLog?.Invoke(_("FlightStarted"), Theme.Success);
            OnFlightStarted?.Invoke();
            OnOsdMessage?.Invoke("ACARS ACTIVE", OsdSeverity.Success);

            _tc?.Reset();
            _reporter?.Reset();
            _procTracker.ResetProgress();
            Task.Run(async () => await _cabinAnnouncements.PrefetchAsync(
                plan.Origin, plan.Destination,
                _flightManager.ActivePilot?.AirlineCountry ?? "",
                _flightManager.ActivePilot?.AircraftSeats  ?? 0));
            TriggerAirspaceLoadAsync(plan.Origin, plan.Destination);
        }

        private bool IsFuelWithinTolerance(double plannedKg, double actualKg)
            => actualKg > 0 && plannedKg > 0 && Math.Abs(actualKg - plannedKg) <= Math.Max(50.0, plannedKg * 0.05);
        public async Task CancelFlight()
        {
            if (await _flightManager.CancelFlight())
            {
                OnButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), false);
                _tc?.Reset();
                _reporter?.Reset();
                _procTracker.ResetProgress();
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
                _tc?.Reset();
                _reporter?.Reset();
                _procTracker.ResetProgress();
                OnLog?.Invoke("✖️ Flight aborted", Theme.Warning);
                OnFlightEnded?.Invoke();
            }
        }

        public Task SendPirep() => _reporter.SendPirep();

        public Task<bool> CheckAndCleanActivePireps() => _reporter.CheckAndCleanActivePireps();

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
                    await _reporter.CheckAndResumeFlight(pilot);
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
            _flightManager.SetActivePlan(new SimbriefPlan
            {
                FlightNumber   = flight.FlightNumber, Airline        = flight.Airline,
                Origin         = flight.Departure,    Destination    = flight.Arrival,
                Route          = flight.Route,        Alternate      = "",
                CruiseAltitude = flight.Level,        Distance       = flight.Distance,
                EstTimeEnroute = flight.FlightTime * 60,
                AircraftIcao   = flight.AircraftType, Aircraft       = flight.AircraftType,
                BlockFuel      = 0, ZeroFuelWeight = 0, PayLoad      = 0
            });
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

    }
}