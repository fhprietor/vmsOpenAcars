using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Db;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.Services.Interfaces;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.ViewModels
{
    internal sealed class TelemetryCoordinator
    {
        // ── Dependencies ──────────────────────────────────────────────────────────
        private readonly FlightManager            _flightManager;
        private readonly FsuipcService            _fsuipc;
        private readonly INavDataService          _navDataService;
        private readonly AirspaceMonitorService   _airspaceMonitor;
        private readonly CabinAnnouncementService _cabinAnnouncements;
        private readonly ILandingLogService       _landingLogService;
        private readonly TelemetryCallbacks        _cb;

        // ── Taxi position tracking ────────────────────────────────────────────────
        private bool   _wasOnRunwayForEntry;
        private bool   _wasOnRunwayForExit;
        private int    _pendingRunwayOnCount;
        private string _lastLoggedTaxiway;
        private string _lastHoldingShortRwy;
        private string _lastTaxiPositionMsg;
        private string _pendingTaxiway;
        private int    _pendingTaxiwayCount;
        private const double TaxiwayChangeHeadingThreshold = 25.0;

        // ── Approach track capture ────────────────────────────────────────────────
        private RunwayTouchdownResult _approachThreshold;
        private string                _approachDestination;
        private DateTime              _lastApproachCapture = DateTime.MinValue;
        internal List<ApproachTrackPoint> ApproachBuffer { get; } = new List<ApproachTrackPoint>();

        // ── UI delta tracking ─────────────────────────────────────────────────────
        private int    _lastUiAltitude;
        private int    _mapUpdateCounter;
        private int    _lastUiSpeed;
        private string _lastUiPhase    = string.Empty;
        private string _lastUiPosition = string.Empty;

        // ── Cabin state ───────────────────────────────────────────────────────────
        private bool     _cabinCruiseSent;
        private bool     _cabinOnRunwaySent;
        private DateTime _cabinCruiseCheckStart = DateTime.MinValue;
        internal double  LastGroundSpeedKt { get; private set; }

        // ── Airspace throttle ─────────────────────────────────────────────────────
        private DateTime _lastAirspaceCheckUtc = DateTime.MinValue;

        // ── Telemetry state ───────────────────────────────────────────────────────
        private AcarsPosition _lastSentPosition;
        private (double lat, double lon)? _lastPosition;
        internal AcarsPositionUpdate LastTelemetry      { get; set; }
        internal DateTime            LastPositionUpdate { get; set; } = DateTime.MinValue;
        internal TimeSpan            PositionUpdateInterval { get; }  = TimeSpan.FromSeconds(5);

        // ── Aircraft info guard ───────────────────────────────────────────────────
        private bool _aircraftInfoShown;

        // ─────────────────────────────────────────────────────────────────────────

        internal TelemetryCoordinator(
            FlightManager             flightManager,
            FsuipcService             fsuipc,
            INavDataService           navDataService,
            AirspaceMonitorService    airspaceMonitor,
            CabinAnnouncementService  cabinAnnouncements,
            ILandingLogService        landingLogService,
            TelemetryCallbacks        callbacks)
        {
            _flightManager      = flightManager;
            _fsuipc             = fsuipc;
            _navDataService     = navDataService;
            _airspaceMonitor    = airspaceMonitor;
            _cabinAnnouncements = cabinAnnouncements;
            _landingLogService  = landingLogService;
            _cb                 = callbacks;
        }

        // ── Event wiring ──────────────────────────────────────────────────────────

        internal void WireEvents()
        {
            UnwireEvents();
            _fsuipc.TelemetryUpdated    += OnTelemetryUpdated;
            _fsuipc.Connected           += OnFsuipcConnected;
            _fsuipc.Disconnected        += OnFsuipcDisconnected;
            _fsuipc.TakeoffDetected     += OnTakeoffDetectedEvent;
            _fsuipc.TouchdownDetected   += OnTouchdownDetectedEvent;
            _fsuipc.GearChanged         += OnGearChanged;
            _fsuipc.FlapsChanged        += OnFlapsChanged;
            _fsuipc.SpoilersChanged     += OnSpoilersChanged;
            _fsuipc.ParkingBrakeChanged += OnParkingBrakeChanged;
            _fsuipc.EnginesChanged      += OnEnginesChanged;
            _fsuipc.RawDataUpdated      += OnRawDataUpdated;
            _fsuipc.OnAircraftInfoReady += OnAircraftInfoReady;
            _fsuipc.NavLightChanged     += OnNavLightChanged;
            _fsuipc.StrobeLightChanged  += OnStrobeLightChanged;
            _fsuipc.LandingLightChanged += OnLandingLightChanged;
            _fsuipc.BeaconChanged       += OnBeaconChanged;
        }

        internal void UnwireEvents()
        {
            _fsuipc.TelemetryUpdated    -= OnTelemetryUpdated;
            _fsuipc.Connected           -= OnFsuipcConnected;
            _fsuipc.Disconnected        -= OnFsuipcDisconnected;
            _fsuipc.TakeoffDetected     -= OnTakeoffDetectedEvent;
            _fsuipc.TouchdownDetected   -= OnTouchdownDetectedEvent;
            _fsuipc.GearChanged         -= OnGearChanged;
            _fsuipc.FlapsChanged        -= OnFlapsChanged;
            _fsuipc.SpoilersChanged     -= OnSpoilersChanged;
            _fsuipc.ParkingBrakeChanged -= OnParkingBrakeChanged;
            _fsuipc.EnginesChanged      -= OnEnginesChanged;
            _fsuipc.RawDataUpdated      -= OnRawDataUpdated;
            _fsuipc.OnAircraftInfoReady -= OnAircraftInfoReady;
            _fsuipc.NavLightChanged     -= OnNavLightChanged;
            _fsuipc.StrobeLightChanged  -= OnStrobeLightChanged;
            _fsuipc.LandingLightChanged -= OnLandingLightChanged;
            _fsuipc.BeaconChanged       -= OnBeaconChanged;
        }

        // ── Post-PIREP reset ──────────────────────────────────────────────────────

        internal void Reset()
        {
            LastTelemetry          = null;
            LastPositionUpdate     = DateTime.MinValue;
            _cabinAnnouncements.Reset();
            _airspaceMonitor.Reset();
            _cabinCruiseSent       = false;
            _cabinOnRunwaySent     = false;
            _cabinCruiseCheckStart = DateTime.MinValue;
        }

        // ── Phase change ──────────────────────────────────────────────────────────

        internal void OnPhaseChanged(FlightPhase phase, FlightPhase prevPhase)
        {
            if (phase == FlightPhase.Boarding && _navDataService.IsAvailable)
            {
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
                if (_navDataService.IsAvailable)
                {
                    double lat = _flightManager.CurrentLat;
                    double lon = _flightManager.CurrentLon;
                    double hdg = _flightManager.CurrentHeading;
                    string dep = _flightManager.ActivePlan?.Origin ?? _flightManager.CurrentAirport;
                    Task.Run(() => LookupTakeoffRunwayData(dep, lat, lon, hdg));
                }
            }
            else if (phase == FlightPhase.TaxiIn && prevPhase == FlightPhase.AfterLanding)
            {
                _lastLoggedTaxiway    = null;
                _lastTaxiPositionMsg  = null;
                _pendingTaxiway       = null;
                _pendingTaxiwayCount  = 0;
                _pendingRunwayOnCount = 0;
            }
            else if (phase == FlightPhase.OnBlock && _navDataService.IsAvailable)
            {
                double aLat  = _flightManager.CurrentLat;
                double aLon  = _flightManager.CurrentLon;
                string arrAp = _flightManager.ActivePlan?.Destination ?? _flightManager.CurrentAirport;
                Task.Run(() => LookupArrivalParking(arrAp, aLat, aLon));
            }

            if (phase == FlightPhase.Approach && _navDataService.IsAvailable)
            {
                ApproachBuffer.Clear();
                _lastApproachCapture = DateTime.MinValue;
                _approachThreshold   = null;
                _approachDestination = null;
            }
            else if (phase != FlightPhase.Approach)
            {
                _approachThreshold = null;
            }

            switch (phase)
            {
                case FlightPhase.TaxiOut:
                    _cabinAnnouncements.QueueAnnouncement("taxi_out");
                    break;
                case FlightPhase.Descent:
                    _cabinAnnouncements.QueueAnnouncement("top_of_descent");
                    _airspaceMonitor.TriggerIvaoRefresh();
                    break;
                case FlightPhase.Approach:
                    _cabinAnnouncements.QueueAnnouncement("approach");
                    _airspaceMonitor.TriggerIvaoRefresh();
                    break;
                case FlightPhase.TaxiIn:
                    _cabinAnnouncements.QueueAnnouncement("taxi_in");
                    break;
            }
        }

        // ── Raw data handler ──────────────────────────────────────────────────────

        private void OnRawDataUpdated(object sender, RawTelemetryData e)
        {
            _flightManager?.UpdateTelemetry(e);
            ProcessRawData(e);
        }

        internal void ProcessRawData(RawTelemetryData e)
        {
            bool altChanged   = Math.Abs((int)e.AltitudeFeet - _lastUiAltitude) > 10;
            bool speedChanged = Math.Abs((int)e.GroundSpeedKt - _lastUiSpeed) > 1;
            string posStr     = $"{e.Latitude:F4}/{e.Longitude:F4}";
            bool posChanged   = posStr != _lastUiPosition;
            string phaseStr   = _flightManager?.CurrentPhase.ToString() ?? string.Empty;
            bool phaseChanged = phaseStr != _lastUiPhase;

            if (altChanged || speedChanged || posChanged || phaseChanged)
            {
                _lastUiAltitude = (int)e.AltitudeFeet;
                _lastUiSpeed    = (int)e.GroundSpeedKt;
                _lastUiPosition = posStr;
                _lastUiPhase    = phaseStr;
                _cb.FlightInfoChanged?.Invoke();
            }

            if (++_mapUpdateCounter >= 5)
            {
                _mapUpdateCounter = 0;
                _cb.MapPositionUpdate?.Invoke(e.Latitude, e.Longitude, e.HeadingDeg);
            }

            if (_flightManager?.CurrentPhase != FlightPhase.Idle &&
                (DateTime.UtcNow - _lastAirspaceCheckUtc).TotalSeconds >= 30)
            {
                _lastAirspaceCheckUtc = DateTime.UtcNow;
                _airspaceMonitor.CheckPosition(e.Latitude, e.Longitude, e.AltitudeFeet,
                    e.HeadingDeg, e.GroundSpeedKt);
                _airspaceMonitor.UpdateAircraftState(
                    e.Latitude, e.Longitude,
                    _flightManager.CurrentPhase,
                    _flightManager.ActivePlan?.Destination);
            }

            LastGroundSpeedKt = e.GroundSpeedKt;

            // ── Cabin cruise check ────────────────────────────────────────────────
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
                else
                {
                    _cabinCruiseCheckStart = DateTime.MinValue;
                }
            }

            // ── Approach track capture ────────────────────────────────────────────
            if (_flightManager?.CurrentPhase == FlightPhase.Approach)
            {
                if (_approachThreshold == null && _navDataService.IsAvailable)
                {
                    string dest = _flightManager.ActivePlan?.Destination;
                    string alt  = _flightManager.ActivePlan?.Alternate;

                    _approachThreshold = !string.IsNullOrEmpty(dest)
                        ? _navDataService.GetRunwayThreshold(dest, e.Latitude, e.Longitude, e.HeadingDeg)
                        : null;

                    if (_approachThreshold != null)
                    {
                        _approachDestination = dest;
                    }
                    else if (!string.IsNullOrEmpty(alt))
                    {
                        _approachThreshold = _navDataService.GetRunwayThreshold(
                            alt, e.Latitude, e.Longitude, e.HeadingDeg);
                        if (_approachThreshold != null)
                        {
                            _approachDestination = alt;
                            _cb.Log?.Invoke($"⚠️ Approaching ALTERNATE — {alt}", Theme.Warning);
                            _flightManager.SetEffectiveDestination(alt);
                        }
                    }

                    if (_approachThreshold != null)
                        Task.Run(() => LoadApproachData(_approachDestination, _approachThreshold.RunwayName));
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

                    if (ApproachBuffer.Count == 0)
                    {
                        _cb.Log?.Invoke(
                            string.Format(_("Lnm_ApproachCaptureStart"),
                                _approachThreshold.RunwayName,
                                (int)computedAgl,
                                distNm.ToString("F1")),
                            Theme.Success);
                    }

                    ApproachBuffer.Add(new ApproachTrackPoint
                    {
                        SeqNo      = ApproachBuffer.Count,
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

        // ── Telemetry handler ─────────────────────────────────────────────────────

        private void OnTelemetryUpdated(object sender, TelemetryData e)
        {
            _cb.PositionUpdate?.Invoke($"{e.Latitude:F3}/{e.Longitude:F3}");
            _cb.PhaseChanged?.Invoke(_flightManager.CurrentPhase);
            _cb.AirStatusChanged?.Invoke(_flightManager.CurrentPhase);
            if (string.IsNullOrEmpty(_flightManager.ActivePirepId))
                _flightManager.UpdatePositionValidation(e.Latitude, e.Longitude);
            _cb.ValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
            PrepareTelemetry(e);
        }

        private void PrepareTelemetry(TelemetryData e)
        {
            if (string.IsNullOrEmpty(_flightManager?.ActivePirepId)) return;

            _lastPosition = (e.Latitude, e.Longitude);

            double refElevation = FlightPhaseHelper.GetTerrainElevation(_flightManager.CurrentPhase, _flightManager.ActivePlan);
            double aglRelative  = e.AltitudeFeet - refElevation;
            bool radarAvailable = !e.IsOnGround && e.RadarAltitudeFeet > 0.0;
            double aglFinal     = radarAvailable ? e.RadarAltitudeFeet : Math.Max(0.0, aglRelative);

            var position = new AcarsPosition
            {
                type         = 0,
                nav_type     = e.NavType,
                order        = e.Order,
                name         = GetPhaseName(_flightManager.CurrentPhase),
                status       = FlightPhaseHelper.GetStatusCode(_flightManager.CurrentPhase),
                lat          = e.Latitude,
                lon          = e.Longitude,
                distance     = Math.Round(_flightManager.TotalDistanceKm * 0.539957, 2),
                heading      = (int)Math.Round(e.HeadingDeg, 0),
                altitude     = Math.Round(e.AltitudeFeet, 0),
                altitude_agl = Math.Round(aglFinal, 0),
                altitude_msl = Math.Round(e.AltitudeFeet, 0),
                vs           = Math.Round(e.VerticalSpeedFpm, 0),
                gs           = (int)Math.Round(e.GroundSpeedKt, 0),
                ias          = (int)Math.Round(e.IndicatedAirspeedKt, 0),
                transponder  = e.Transponder,
                autopilot    = e.AutopilotEngaged,
                fuel         = Math.Round(e.FuelLbs, 1),
                pitch        = e.PitchDeg,
                bank         = e.BankDeg,
                sim_time     = DateTime.UtcNow,
                source       = "vmsOpenAcars"
            };

            if (HasSignificantChange(position))
            {
                _lastSentPosition = position;
                LastTelemetry     = new AcarsPositionUpdate { positions = new[] { position } };
            }
        }

        private bool HasSignificantChange(AcarsPosition newPos)
        {
            if (_lastSentPosition == null) return true;
            const double posThreshold = 0.0003;
            const int    hdgThreshold = 5;
            const int    altThreshold = 30;
            const int    spdThreshold = 5;
            const int    vsThreshold  = 100;
            bool posChanged   = Math.Abs(newPos.lat - _lastSentPosition.lat) > posThreshold
                             || Math.Abs(newPos.lon - _lastSentPosition.lon) > posThreshold;
            bool hdgChanged   = Math.Abs((newPos.heading ?? 0) - (_lastSentPosition.heading ?? 0)) > hdgThreshold;
            bool altChanged   = Math.Abs((newPos.altitude ?? 0) - (_lastSentPosition.altitude ?? 0)) > altThreshold;
            bool spdChanged   = Math.Abs((newPos.gs ?? 0) - (_lastSentPosition.gs ?? 0)) > spdThreshold;
            bool vsChanged    = Math.Abs((newPos.vs ?? 0) - (_lastSentPosition.vs ?? 0)) > vsThreshold;
            bool phaseChanged = newPos.status != _lastSentPosition.status;
            return posChanged || hdgChanged || altChanged || spdChanged || vsChanged || phaseChanged;
        }

        // ── FSUIPC connection ─────────────────────────────────────────────────────

        private void OnFsuipcConnected(object sender, EventArgs e)
        {
            double lat = _fsuipc.CurrentLatitude;
            double lon = _fsuipc.CurrentLongitude;
            _flightManager.SetSimulatorConnected(true, lat, lon);
            _cb.SimulatorNameChanged?.Invoke(_fsuipc.SimulatorName);
            _cb.AcarsStatusChanged?.Invoke(true);
            _cb.Log?.Invoke(_("Log_SimulatorConnected", _fsuipc.SimulatorName), Theme.SecondaryText);
            SystemInfoHelper.SetSimVersion(_fsuipc.SimulatorName);
            if (!string.IsNullOrEmpty(SystemInfoHelper.SimSummary))
                _cb.Log?.Invoke(SystemInfoHelper.SimSummary, Theme.SecondaryText);
            if (_flightManager.ActivePilot != null)
            {
                _flightManager.UpdatePositionValidation(lat, lon);
                _cb.ValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
            }
        }

        private void OnFsuipcDisconnected(object sender, EventArgs e)
        {
            _cb.SimulatorNameChanged?.Invoke("AWAITING SIM");
            _cb.AcarsStatusChanged?.Invoke(false);
            _cb.ValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
        }

        // ── Aircraft info ─────────────────────────────────────────────────────────

        private void OnAircraftInfoReady()
        {
            if (_aircraftInfoShown) return;
            _aircraftInfoShown = true;

            if (_fsuipc.AircraftManufacturer != "Unknown")
                _cb.Log?.Invoke(_("Log_Manufacturer", _fsuipc.AircraftManufacturer), Theme.SecondaryText);
            if (_fsuipc.AircraftIcao != "????")
                _cb.Log?.Invoke(_("Log_ICAO", _fsuipc.AircraftIcao), Theme.SecondaryText);
            if (!string.IsNullOrEmpty(_fsuipc.AircraftTitle) && _fsuipc.AircraftTitle != "Unknown")
                _cb.Log?.Invoke(_("Log_Aircraft", _fsuipc.AircraftTitle), Theme.MainText);
            string livery = _fsuipc.GetAircraftLivery();
            if (livery != "Unknown" && livery != _fsuipc.AircraftIcao)
                _cb.Log?.Invoke(_("Log_Livery", livery), Theme.SecondaryText);
        }

        // ── Hardware event handlers ───────────────────────────────────────────────

        private void OnTakeoffDetectedEvent(object sender, TakeoffData data)
        {
            _cb.Log?.Invoke(_("Log_AccurateTakeoff"), Theme.Success);
            _cb.Log?.Invoke(_("Log_TakeoffRotation",    $"{data.RotationIasKt:F0}"),        Theme.MainText);
            _cb.Log?.Invoke(_("Log_TakeoffGroundSpeed", $"{data.GroundSpeedKt:F0}"),        Theme.MainText);
            _cb.Log?.Invoke(_("Log_PitchBank",          $"{data.PitchDeg:F1}", $"{data.BankDeg:F1}"), Theme.MainText);
            _cb.Log?.Invoke(_("Log_TakeoffHeading",     $"{data.HeadingDeg:F0}"),           Theme.MainText);
            if (data.EngineType == "N1")
                _cb.Log?.Invoke(_("Log_TakeoffN1",      $"{data.Eng1N1Pct:F0}", $"{data.Eng2N1Pct:F0}"), Theme.MainText);
            else if (data.EngineType == "PROP RPM")
                _cb.Log?.Invoke(_("Log_TakeoffPropRpm", $"{data.Eng1Rpm:F0}", $"{data.Eng2Rpm:F0}"), Theme.MainText);
            else if (data.EngineType == "PISTON RPM")
                _cb.Log?.Invoke(_("Log_TakeoffRpm",     $"{data.Eng1Rpm:F0}", $"{data.Eng2Rpm:F0}"), Theme.MainText);
            _cb.Log?.Invoke(_("Log_TakeoffFlaps",  $"{data.FlapsPosition * 100:F0}"), Theme.MainText);
            _cb.Log?.Invoke(_("Log_OatWind", $"{data.OatCelsius:F0}", $"{data.WindSpeedKt:F0}", $"{data.WindDirDeg:F0}"), Theme.MainText);
        }

        private void OnTouchdownDetectedEvent(object sender, TouchdownData data)
        {
            string rating = data.GForcePeak < 1.3 ? _("Score_Perfect")
                          : data.GForcePeak < 1.8 ? _("Score_Normal")
                          : data.GForcePeak < 2.5 ? _("Score_Hard")
                          :                          _("Score_Crash");
            _cb.Log?.Invoke(_("Log_AccurateTouchdown"), Theme.Success);
            _cb.Log?.Invoke(_("Log_TouchdownVs",        $"{data.VerticalSpeedFpm:F0}"),     Theme.MainText);
            _cb.Log?.Invoke(_("Log_TouchdownGForce",    $"{data.GForcePeak:F2}", rating),   Theme.MainText);
            _cb.Log?.Invoke(_("Log_TouchdownSpeed",     $"{data.IasKt:F0}", $"{data.GroundSpeedKt:F0}"), Theme.MainText);
            _cb.Log?.Invoke(_("Log_PitchBank",          $"{data.PitchDeg:F1}", $"{data.BankDeg:F1}"), Theme.MainText);
            _cb.Log?.Invoke(_("Log_TouchdownFlapsSpoilers", $"{data.FlapsPosition * 100:F0}", $"{data.SpoilersPosition * 100:F0}"), Theme.MainText);
            _cb.Log?.Invoke(_("Log_TouchdownReversers", $"{data.Eng1ReverserPct:F0}", $"{data.Eng2ReverserPct:F0}"), Theme.MainText);
            _cb.Log?.Invoke(_("Log_TouchdownBrakes",    $"{data.BrakeLeft * 100:F0}", $"{data.BrakeRight * 100:F0}", GetAutobrakeName(data.AutobrakeSetting)), Theme.MainText);
            _cb.Log?.Invoke(_("Log_OatWind", $"{data.OatCelsius:F0}", $"{data.WindSpeedKt:F0}", $"{data.WindDirDeg:F0}"), Theme.MainText);

            if (_navDataService.IsAvailable)
                Task.Run(() => LookupRunwayData(data));
        }

        private void OnGearChanged(int oldPos, int newPos)
        {
            string status = newPos == 1 ? "DOWN" : "UP";
            double msl    = _fsuipc.CurrentAltitudeFeet;
            double elev   = newPos == 0
                ? (_flightManager.ActivePlan?.OriginElevation      ?? 0)
                : (_flightManager.ActivePlan?.DestinationElevation ?? 0);
            int    agl    = (int)(msl - elev);
            string aglStr = agl > 50 ? $" ({agl} ft AGL)" : "";
            _cb.Log?.Invoke(_("Log_GearChanged", status, aglStr), Theme.MainText);
        }

        private void OnFlapsChanged(double oldPercent, double newPercent) =>
            _cb.Log?.Invoke(_("Log_FlapsChanged", $"{oldPercent:F0}", $"{newPercent:F0}"), Theme.SecondaryText);

        private void OnSpoilersChanged(bool deployed) =>
            _cb.Log?.Invoke(deployed ? _("Log_SpoilersDeployed") : _("Log_SpoilersRetracted"), Theme.Warning);

        private void OnParkingBrakeChanged(bool engaged) =>
            _cb.Log?.Invoke(engaged ? _("Log_ParkingBrakeSet") : _("Log_ParkingBrakeReleased"), Theme.MainText);

        private void OnEnginesChanged(bool running) =>
            _cb.Log?.Invoke(running ? _("Log_EnginesStarted") : _("Log_EnginesShutdown"),
                running ? Theme.Success : Theme.Warning);

        // ── Light change handlers ─────────────────────────────────────────────────

        private void OnNavLightChanged(bool on) =>
            _cb.Log?.Invoke(on ? _("Log_NavLightsOn", AglSuffix()) : _("Log_NavLightsOff", AglSuffix()),
                Theme.MainText);

        private void OnStrobeLightChanged(bool on)
        {
            _cb.Log?.Invoke(on ? _("Log_StrobeLightsOn", AglSuffix()) : _("Log_StrobeLightsOff", AglSuffix()),
                Theme.MainText);
            if (on && !_cabinOnRunwaySent && LastGroundSpeedKt <= 40
                && _flightManager?.CurrentPhase != FlightPhase.Idle)
            {
                _cabinOnRunwaySent = true;
                _cabinAnnouncements.QueueAnnouncement("on_runway");
            }
        }

        private void OnLandingLightChanged(bool on)
        {
            _cb.Log?.Invoke(on ? _("Log_LandingLightsOn", AglSuffix()) : _("Log_LandingLightsOff", AglSuffix()),
                Theme.MainText);
            if (on && !_cabinOnRunwaySent && LastGroundSpeedKt <= 40
                && _flightManager?.CurrentPhase != FlightPhase.Idle)
            {
                _cabinOnRunwaySent = true;
                _cabinAnnouncements.QueueAnnouncement("on_runway");
            }
        }

        private void OnBeaconChanged(bool on) =>
            _cb.Log?.Invoke(on ? _("Log_BeaconOn", AglSuffix()) : _("Log_BeaconOff", AglSuffix()),
                Theme.MainText);

        // ── Taxi position tracking ─────────────────────────────────────────────────

        internal void HandleTaxiPositionUpdate(
            double lat, double lon, double heading, string airport, bool isTaxiIn)
        {
            if (!_navDataService.IsAvailable || string.IsNullOrEmpty(airport)) return;

            Task.Run(() =>
            {
                var  entry     = _navDataService.FindRunwayEntry(airport, lat, lon, heading);
                bool onRunway  = entry != null;

                if (onRunway) _pendingRunwayOnCount++;
                else          _pendingRunwayOnCount = 0;
                bool confirmedOnRunway = onRunway && _pendingRunwayOnCount >= 2;

                if (!isTaxiIn)
                {
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
                                _cb.Log?.Invoke(string.Format(_("Lnm_RunwayBacktrackTwy"), entry.RunwayName, entry.TaxiwayName), Theme.Warning);
                            else
                                _cb.Log?.Invoke(string.Format(_("Lnm_RunwayBacktrack"), entry.RunwayName), Theme.Warning);
                            _cb.OsdMessage?.Invoke($"BACKTRACK  RWY {entry.RunwayName}", OsdSeverity.Warning);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(entry.TaxiwayName))
                                _cb.Log?.Invoke(string.Format(_("Lnm_RunwayEntered"), entry.RunwayName, entry.TaxiwayName), Theme.Takeoff);
                            else
                                _cb.Log?.Invoke(string.Format(_("Lnm_RunwayEnteredNoTwy"), entry.RunwayName), Theme.Takeoff);
                            _cb.OsdMessage?.Invoke($"ENTERING RWY {entry.RunwayName}", OsdSeverity.Warning);
                        }
                    }
                    _wasOnRunwayForEntry = confirmedOnRunway;
                }
                else
                {
                    if (confirmedOnRunway && !_wasOnRunwayForExit && entry?.IsBacktrack == true)
                    {
                        _lastLoggedTaxiway   = null;
                        _pendingTaxiway      = null;
                        _pendingTaxiwayCount = 0;
                        _cb.Log?.Invoke(string.Format(_("Lnm_RunwayBacktrack"), entry.RunwayName), Theme.Warning);
                        _cb.OsdMessage?.Invoke($"BACKTRACK  RWY {entry.RunwayName}", OsdSeverity.Warning);
                    }
                    if (!confirmedOnRunway && _wasOnRunwayForExit)
                    {
                        string twy = _navDataService.FindNearestTaxiway(airport, lat, lon, heading);
                        if (!string.IsNullOrEmpty(twy))
                            _cb.Log?.Invoke(string.Format(_("Lnm_RunwayVacated"), twy), Theme.Success);
                        else
                            _cb.Log?.Invoke(_("Lnm_RunwayVacatedNoTwy"), Theme.Success);
                        _cb.OsdMessage?.Invoke("RWY VACATED", OsdSeverity.Info);
                    }
                    _wasOnRunwayForExit = confirmedOnRunway;
                }

                if (!onRunway)
                {
                    string twy  = _navDataService.FindNearestTaxiway(airport, lat, lon, heading);
                    string next = _navDataService.FindNextIntersection(airport, lat, lon, heading);

                    if (!string.IsNullOrEmpty(twy))
                    {
                        if (twy != _lastLoggedTaxiway)
                        {
                            bool headingDiverged = true;
                            if (!string.IsNullOrEmpty(_lastLoggedTaxiway))
                            {
                                double curBrg = _navDataService.FindTaxiwaySegmentBearing(
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
                                if (twy == _pendingTaxiway) _pendingTaxiwayCount++;
                                else { _pendingTaxiway = twy; _pendingTaxiwayCount = 1; }

                                if (_pendingTaxiwayCount >= 3)
                                {
                                    _pendingTaxiway      = null;
                                    _pendingTaxiwayCount = 0;
                                    _lastLoggedTaxiway   = twy;
                                    string msg = !string.IsNullOrEmpty(next)
                                        ? string.Format(_("Lnm_TaxiPosition"), twy, next)
                                        : string.Format(_("Lnm_TaxiwayChange"), twy);
                                    _lastTaxiPositionMsg = msg;
                                    _cb.Log?.Invoke(msg, Theme.Taxi);
                                }
                            }
                            else { _pendingTaxiway = null; _pendingTaxiwayCount = 0; }
                        }
                        else
                        {
                            _pendingTaxiway      = null;
                            _pendingTaxiwayCount = 0;
                            string msg = !string.IsNullOrEmpty(next)
                                ? string.Format(_("Lnm_TaxiPosition"), twy, next)
                                : string.Format(_("Lnm_TaxiwayChange"), twy);
                            if (msg != _lastTaxiPositionMsg)
                            {
                                _lastTaxiPositionMsg = msg;
                                _cb.Log?.Invoke(msg, Theme.Taxi);
                            }
                        }
                    }

                    if (!isTaxiIn)
                    {
                        var hp = _navDataService.FindHoldingPoint(airport, lat, lon, heading);
                        if (hp != null && hp.RunwayName != _lastHoldingShortRwy)
                        {
                            _lastHoldingShortRwy = hp.RunwayName;
                            if (!string.IsNullOrEmpty(hp.TaxiwayName))
                                _cb.Log?.Invoke(string.Format(_("Lnm_HoldingShort"), hp.RunwayName, hp.TaxiwayName), Theme.Taxi);
                            else
                                _cb.Log?.Invoke(string.Format(_("Lnm_HoldingShortNoTwy"), hp.RunwayName), Theme.Taxi);
                        }
                        else if (hp == null && _lastHoldingShortRwy != null)
                        {
                            _lastHoldingShortRwy = null;
                        }
                    }
                }
            });
        }

        // ── NavData lookups ───────────────────────────────────────────────────────

        private void LookupRunwayData(TouchdownData data)
        {
            string airport = _approachDestination ?? _flightManager.ActivePlan?.Destination;
            if (string.IsNullOrEmpty(airport)) return;

            var result = _navDataService.FindTouchdownRunway(
                airport, data.LatitudeDeg, data.LongitudeDeg, data.HeadingDeg);

            if (result == null)
            {
                _cb.Log?.Invoke(
                    string.Format(_("Lnm_RunwayNotFound"), airport, (int)data.HeadingDeg),
                    Theme.Warning);
                return;
            }

            _flightManager.SetRunwayTouchdownData(
                result.ThresholdDistanceFt, result.CenterlineDeviationFt, result.RunwayName);

            _cb.Log?.Invoke(
                string.Format(_("Lnm_TouchdownInfo"),
                    result.RunwayName,
                    (int)result.ThresholdDistanceFt,
                    (int)result.CenterlineDeviationFt),
                Theme.Success);
        }

        private void LookupTakeoffRunwayData(string airport, double lat, double lon, double heading)
        {
            if (string.IsNullOrEmpty(airport)) return;
            var result = _navDataService.FindTakeoffRunway(airport, lat, lon, heading);
            if (result == null)
            {
                _cb.Log?.Invoke(
                    string.Format(_("Lnm_TakeoffRunwayNotFound"), airport, (int)heading),
                    Theme.Warning);
                return;
            }
            _cb.Log?.Invoke(
                string.Format(_("Lnm_TakeoffInfo"),
                    result.RunwayName,
                    (int)result.ThresholdDistanceFt,
                    (int)result.CenterlineDeviationFt),
                Theme.Success);
        }

        internal void LoadApproachData(string airport, string runwayName)
        {
            if (string.IsNullOrEmpty(airport)) return;
            var ils      = _navDataService.GetIlsForRunway(airport, runwayName);
            var approach = _navDataService.GetApproachType(airport, runwayName);
            var fixes    = approach != null ? _navDataService.GetApproachFixes(airport, runwayName) : null;
            _flightManager?.SetApproachData(ils, approach, fixes);
        }

        private void LookupDepartureParking(string airport, double lat, double lon)
        {
            var spot = _navDataService.FindNearestParking(airport, lat, lon);
            if (spot != null)
                _cb.Log?.Invoke(string.Format(_("Lnm_DepartureParking"), spot.DisplayName), Theme.Taxi);
        }

        private void LookupArrivalParking(string airport, double lat, double lon)
        {
            var spot = _navDataService.FindNearestParking(airport, lat, lon);
            if (spot != null)
                _cb.Log?.Invoke(string.Format(_("Lnm_ArrivalParking"), spot.DisplayName), Theme.Success);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string AglSuffix()
        {
            int agl = (int)(_flightManager?.CurrentAGL ?? 0);
            return agl > 50 ? $" ({agl} ft AGL)" : "";
        }

        private static string GetPhaseName(FlightPhase phase)
        {
            switch (phase)
            {
                case FlightPhase.Boarding:     return "Boarding";
                case FlightPhase.TaxiOut:      return "TaxiOut";
                case FlightPhase.TakeoffRoll:  return "Takeoff";
                case FlightPhase.Climb:        return "Climbing";
                case FlightPhase.Enroute:      return "Cruise";
                case FlightPhase.Descent:      return "Descent";
                case FlightPhase.Approach:     return "Approach";
                case FlightPhase.AfterLanding: return "Landing";
                case FlightPhase.TaxiIn:       return "TaxiIn";
                case FlightPhase.OnBlock:      return "OnBlock";
                default:                       return "Other";
            }
        }

        private static string GetAutobrakeName(int setting)
        {
            switch (setting)
            {
                case 0:  return "RTO";
                case 1:  return "OFF";
                case 2:  return "1";
                case 3:  return "2";
                case 4:  return "3";
                case 5:  return "MAX";
                default: return setting.ToString();
            }
        }
    }
}
