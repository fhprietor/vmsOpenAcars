using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        private bool     _wasOnRunwayForEntry;
        private bool     _wasOnRunwayForExit;
        private int      _pendingRunwayOnCount;
        private int      _pendingRunwayOffCount;
        private DateTime _lastRunwayVacatedTime = DateTime.MinValue;
        private string   _lastLoggedTaxiway;
        private string   _lastHoldingShortRwy;
        private string   _lastTaxiPositionMsg;
        private string   _pendingTaxiway;
        private int      _pendingTaxiwayCount;
        private const double TaxiwayChangeHeadingThreshold = 25.0;

        // ── RAAS: guía de rodaje giro a giro (v0.9.14) ────────────────────────────
        // La guía se activa desde el popup que sale al encender la luz de taxi o al detectarse
        // el TaxiOut; a partir de ahí los avisos se evalúan sobre la **telemetría cruda** (1 Hz),
        // no sobre el envío de posiciones a phpVMS: ese va cada 30 s en rodaje, y a 15 kt son
        // 230 m entre muestra y muestra — demasiado para avisar de un hold-short a 150 m.
        private readonly RaasAdvisor   _raas = new RaasAdvisor();
        private TaxiRoutePlan          _raasPlan;
        private string                 _raasAirport;
        private string                 _raasRunway;
        private bool                   _raasGuidanceActive;
        private bool                   _raasPromptShown;
        // Segundo aviso del vuelo, ya en el punto de inicio del rodaje (fin del pushback o TaxiOut
        // de un puesto remoto), y lo que el grafo propuso la primera vez para poder comparar.
        private bool                   _raasRepromptShown;
        private string                 _raasSuggestedText;
        // Umbral de la pista elegida, resuelto una vez al empezar la guía: el motor de avisos
        // necesita saber si el avión se ACERCA a ella, y eso se pregunta 1 vez por segundo.
        private double                 _raasThresholdLat = double.NaN;
        private double                 _raasThresholdLon = double.NaN;
        private DateTime               _lastRaasEval = DateTime.MinValue;
        private static readonly TimeSpan RaasInterval = TimeSpan.FromSeconds(1);

        /// <summary>Pide al UI que muestre el popup de rodaje (una vez por vuelo).</summary>
        internal event Action<TaxiRoutePrompt> OnTaxiRoutePromptRequested;

        // ── Approach track capture ────────────────────────────────────────────────
        private RunwayTouchdownResult _approachThreshold;
        private string                _approachDestination;
        private DateTime              _lastApproachCapture = DateTime.MinValue;
        private DateTime              _lastApproachAirportQuery = DateTime.MinValue;
        private bool                  _reconfirmingApproachAirport;

        // Diversion gating (v0.9.9). NavData matches an airport on heading + cross-track
        // against an infinite centerline, so an arrival that merely overflies an aligned
        // airfield keeps matching it. Committing a diversion needs three filters: the
        // lateral tolerance below, "established on a final" (SelectApproachThreshold, which
        // also requires being before the threshold), and this many consecutive polls
        // agreeing — roughly DiversionConfirmPolls × the 5 s reconfirmation throttle.
        private const double MaxCrossTrackNm      = 3.0;
        private const int    DiversionConfirmPolls = 2;
        private string _pendingDiversionIcao;
        private int    _pendingDiversionIcaoCount;

        // Last alternate airport the endpoint proposed that passed the lateral filter, even
        // if it never qualified as a final. Used as a touchdown-runway candidate so a real
        // diversion is still caught when the approach itself was not a straight-in final.
        private string _lastAlternateCandidateIcao;

        // Deduplication key of the last discarded match ("SKTL|ct", "SKTL|final"), so the
        // per-poll diagnostics don't repeat one identical line down the whole descent.
        private string _lastRejectedMatchKey;

        // Approach track points, captured during Descent/Approach for the landing log.
        // Guarded by _approachBufferLock because it is genuinely touched from two threads:
        // points are appended on the FSUIPC polling thread (ProcessRawData) while
        // ReconfirmApproachRunway clears it from a Task.Run (runway change / diversion).
        // A bare List<T> mutated from both was the kind of corruption that shows up as
        // dropped points or an IndexOutOfRange deep inside List.Add.
        private readonly List<ApproachTrackPoint> _approachBuffer = new List<ApproachTrackPoint>();
        private readonly object _approachBufferLock = new object();

        /// <summary>Number of captured approach points. Safe to read from any thread.</summary>
        internal int ApproachBufferCount
        {
            get { lock (_approachBufferLock) return _approachBuffer.Count; }
        }

        /// <summary>Copy of the captured track, for consumers that need to iterate it.</summary>
        internal List<ApproachTrackPoint> SnapshotApproachBuffer()
        {
            lock (_approachBufferLock) return new List<ApproachTrackPoint>(_approachBuffer);
        }

        internal void ClearApproachBuffer()
        {
            lock (_approachBufferLock) _approachBuffer.Clear();
        }

        private void AddApproachPoint(ApproachTrackPoint point)
        {
            lock (_approachBufferLock)
            {
                point.SeqNo = _approachBuffer.Count;
                _approachBuffer.Add(point);
            }
        }

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
            _fsuipc.TaxiLightChanged    += OnTaxiLightChanged;
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
            _fsuipc.TaxiLightChanged    -= OnTaxiLightChanged;
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

            // RAAS: vuelo nuevo → se vuelve a preguntar la pista y se suelta la guía anterior.
            _raasGuidanceActive = false;
            _raasPlan           = null;
            _raasAirport        = null;
            _raasRunway         = null;
            _raasPromptShown    = false;
            _raasRepromptShown  = false;
            _raasSuggestedText  = null;
            _raasThresholdLat   = double.NaN;
            _raasThresholdLon   = double.NaN;
            _raas.ResetRouteState();
            _lastRaasEval       = DateTime.MinValue;
            RaasVoice.Cancel();
        }

        // ── Phase change ──────────────────────────────────────────────────────────

        internal void OnPhaseChanged(FlightPhase phase, FlightPhase prevPhase)
        {
            // RAAS: el TaxiOut es la otra señal de «voy a rodar» (hay quien no enciende la luz de
            // taxi, o la enciende antes de arrancar). El popup sale una sola vez por vuelo.
            if (phase == FlightPhase.TaxiOut && prevPhase != FlightPhase.TaxiOut)
                RequestTaxiRoutePrompt("taxi out");

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
                string arrAp = _approachDestination ?? _flightManager.ActivePlan?.Destination ?? _flightManager.CurrentAirport;
                Task.Run(() => LookupArrivalParking(arrAp, aLat, aLon));
            }

            // {Descent, Approach} treated as one logical "approaching" superstate now
            // that diversion detection runs in both — reset only when entering the pair
            // from outside it, not on every Descent→Approach transition, so a runway
            // already resolved during Descent survives that transition instead of being
            // wiped and reacquired.
            bool enteringApproachSuperstate =
                (phase == FlightPhase.Descent || phase == FlightPhase.Approach) &&
                prevPhase != FlightPhase.Descent && prevPhase != FlightPhase.Approach;
            bool leavingApproachSuperstate =
                (prevPhase == FlightPhase.Descent || prevPhase == FlightPhase.Approach) &&
                phase != FlightPhase.Descent && phase != FlightPhase.Approach;

            if (enteringApproachSuperstate && _navDataService.IsAvailable)
            {
                ClearApproachBuffer();
                _lastApproachCapture      = DateTime.MinValue;
                _lastApproachAirportQuery = DateTime.MinValue;
                _approachThreshold        = null;
                _approachDestination      = null;
                _lastAlternateCandidateIcao = null;
                _lastRejectedMatchKey     = null;
                ResetPendingDiversion();
            }
            else if (leavingApproachSuperstate)
            {
                _approachThreshold = null;
                _lastAlternateCandidateIcao = null;
                _lastRejectedMatchKey = null;
                ResetPendingDiversion();
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

            EvaluateRaas(e);

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
            // Runs during Descent too, not just Approach — a severe diversion (e.g. to
            // an airport much higher/lower than the planned destination) can keep the
            // phase machine from ever reaching FlightPhase.Approach at all (see
            // FlightPhaseStateMachine's Descent case: both its transition conditions are
            // computed relative to the PLANNED destination). Without this, diversion
            // detection would never run for that flight.
            var currentPhase = _flightManager?.CurrentPhase;
            if (currentPhase == FlightPhase.Approach || currentPhase == FlightPhase.Descent)
            {
                double computedAgl = _flightManager.CurrentAGL;

                // Keep re-confirming the approaching airport/runway via NavData's
                // nearest/approach-airport match — not just resolving once — until the
                // 1000 ft AGL stabilized-approach gate fires. Parallel runways sharing an
                // initial fix (e.g. SKBO 14L/14R via AMVES) are geometrically ambiguous
                // right at that fix; the match only becomes reliable once the aircraft
                // diverges onto a specific final course, so a single early resolution can
                // lock in the wrong runway for the rest of the approach. The endpoint's
                // score is cross-track-dominated (validated by NavData for SKBO 14L/14R,
                // ~355 m separation) — trusted directly, no local tie-break needed.
                if (_navDataService.IsAvailable && !_reconfirmingApproachAirport
                    && computedAgl > 1000
                    && (DateTime.UtcNow - _lastApproachAirportQuery).TotalSeconds >= 5.0)
                {
                    _lastApproachAirportQuery = DateTime.UtcNow;
                    Task.Run(() => ReconfirmApproachRunway(
                        e.Latitude, e.Longitude, e.HeadingDeg, e.AltitudeFeet));
                }

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

                    if (ApproachBufferCount == 0)
                    {
                        _cb.Log?.Invoke(
                            string.Format(_("Lnm_ApproachCaptureStart"),
                                _approachThreshold.RunwayName,
                                (int)computedAgl,
                                distNm.ToString("F1")),
                            Theme.Success);
                    }

                    AddApproachPoint(new ApproachTrackPoint
                    {
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

        // Resolves/re-confirms the airport+runway being approached via NavData's
        // nearest/approach-airport endpoint. Switches _approachThreshold whenever the
        // matched runway or airport differs from the currently resolved one (parallel-
        // runway correction, or a genuine diversion). Runs off the RawDataUpdated thread
        // via Task.Run — network I/O, must not block telemetry processing.
        //
        // A diversion is only committed once six independent filters agree, because
        // "the endpoint returned another airport" is on its own a very weak signal (it
        // matches on heading and lateral offset only, against an *infinite* centerline):
        //   1. lateral plausibility — within MaxCrossTrackNm of the extended centerline;
        //   2. angular plausibility — within MaxFinalConeAngleDeg at the threshold;
        //   3. vertical plausibility — a usable descent gradient down to that field;
        //   4. nearer than the planned destination — you don't divert past where you were
        //      already going;
        //   5. established on final — SelectApproachThreshold, which also requires being
        //      BEFORE the threshold;
        //   6. persistence — consecutive polls agreeing, so a single sample can't fire the
        //      OSD alert and repoint the effective destination.
        private async Task ReconfirmApproachRunway(
            double lat, double lon, double heading, double altitudeMslFt)
        {
            _reconfirmingApproachAirport = true;
            try
            {
                var match = await _navDataService.FindApproachAirport(lat, lon, heading);
                if (match == null) return;

                string plannedDest = _flightManager.ActivePlan?.Destination;
                bool diverted = !string.IsNullOrEmpty(plannedDest) &&
                    !match.Icao.Equals(plannedDest, StringComparison.OrdinalIgnoreCase);

                // ── Pre-filter: el propio plan de vuelo ──────────────────────────
                // Si el avión está dentro del corredor de la llegada que él mismo presentó,
                // entonces está exactamente donde su plan dice, y que el matcher nombre otro
                // aeródromo solo puede ser la llegada pasando cerca de él. Es la comprobación
                // más directa de todas y la única que usa lo que el piloto planificó, no solo
                // geometría. Medido con el OFP real del vuelo SKRG→SKBQ: los puntos de su
                // llegada dan 0 NM y el avión estuvo a 25–33 NM de esa traza durante todo el
                // descenso del falso SKTL (se había ido hacia SKCG), así que esta regla no
                // toca ese caso — actúa en el opuesto, el de la aproximación a KBOS.
                //
                // Un desvío real empieza precisamente por salirse de la llegada, así que como
                // mucho retrasa la detección lo que tardes en abandonar el corredor (~5 NM).
                if (diverted && RouteCorridor.IsOnArrival(
                        _flightManager.ActivePlan?.Waypoints, lat, lon))
                {
                    RevertDiversionIfAny(match.Icao);
                    LogRejectedMatchOnce(match.Icao + "|onArrival", string.Format(
                        _("Lnm_DiversionRejectedOnArrival"), match.Icao, plannedDest));
                    return;
                }

                // ── Filter 1: lateral plausibility ────────────────────────────────
                // A STAR turn can transiently point the aircraft at a nearby airfield's
                // runway while it is still far off that runway's extended centerline (real
                // case: SKGY matched with heading_diff_deg 3.1° but cross_track_nm 11.36).
                if (match.CrossTrackNm.HasValue && match.CrossTrackNm.Value > MaxCrossTrackNm)
                {
                    if (diverted)
                    {
                        ResetPendingDiversion();
                        LogRejectedMatchOnce(match.Icao + "|ct", string.Format(
                            _("Lnm_DiversionRejectedCrossTrack"),
                            match.Icao, FormatNm(match.CrossTrackNm)));
                    }
                    else
                    {
                        // Endpoint says "planned destination" — contradicts any diversion flag.
                        RevertDiversionIfAny(match.Icao);
                    }
                    return;
                }

                // Remember the last alternate the endpoint considered plausible (lateral filter
                // passed) even if we don't act on it: if the flight later lands there without
                // ever satisfying the final-approach tests (circling approach, short final),
                // LookupRunwayData uses this as a touchdown candidate.
                if (diverted) _lastAlternateCandidateIcao = match.Icao;

                // ── Filter 2: angular plausibility ────────────────────────────────
                // A fixed lateral cut-off cannot separate a real final from a coincidental
                // alignment, because both get matched ~19 NM out: the genuine diversion to
                // SKCG was 0.70 NM off (2.1°) while the false SKTL match was 2.99 NM off
                // (9.0°) — and the old 3 NM cut-off passed the false one by 0.007 NM, for a
                // single 5 s poll before it drifted out of range. As an angle the two are a
                // factor of four apart, which is the margin that actually holds.
                if (!NavDataService.IsWithinFinalCone(match.CrossTrackNm, match.DistToThresholdNm))
                {
                    if (diverted)
                    {
                        ResetPendingDiversion();
                        LogRejectedMatchOnce(match.Icao + "|cone", string.Format(
                            _("Lnm_DiversionRejectedCone"), match.Icao,
                            FormatNm(match.CrossTrackNm), FormatNm(match.DistToThresholdNm)));
                    }
                    else
                    {
                        RevertDiversionIfAny(match.Icao);
                    }
                    return;
                }

                // ── Filter 3: vertical plausibility ──────────────────────────────
                // Landing somewhere means descending toward it on a usable gradient. The
                // false SKTL match ran 906–2 224 ft/NM (8.5°–20.1°) and got WORSE as the
                // aircraft approached, because it was descending past a field it was never
                // going to; the real diversion to SKCG held 260 ft/NM (2.4°). Measured
                // against the matched airport's own elevation, so a high-plateau destination
                // can't mask a sea-level alternate.
                if (diverted)
                {
                    double? matchedElevFt = _navDataService.GetAirportElevationFt(match.Icao);
                    if (!NavDataService.IsPlausibleDiversionDescent(
                            altitudeMslFt, matchedElevFt, match.DistToThresholdNm))
                    {
                        ResetPendingDiversion();
                        double aglFt = altitudeMslFt - (matchedElevFt ?? 0);
                        LogRejectedMatchOnce(match.Icao + "|grad", string.Format(
                            _("Lnm_DiversionRejectedDescent"), match.Icao,
                            aglFt.ToString("F0"), FormatNm(match.DistToThresholdNm)));
                        return;
                    }
                }

                // ── Filter 4: the alternate must be nearer than the planned destination ──
                // Otherwise the matcher can name a small airfield that merely has a runway
                // better aligned with the current heading while the real destination is right
                // there. Real case (KBOS): 28M was named as the diversion 15.1 NM away while
                // the aircraft was 6.5 NM from Logan — the destination was inside the 20 NM
                // match radius, it just had no runway within 15° of the heading during the
                // turn. Diverting to something two miles past your destination makes no sense.
                if (diverted)
                {
                    double? plannedDistNm = _navDataService.GetAirportDistanceNm(plannedDest, lat, lon);
                    if (!NavDataService.IsPlausibleDiversionDistance(match.AirportDistanceNm, plannedDistNm))
                    {
                        ResetPendingDiversion();
                        LogRejectedMatchOnce(match.Icao + "|far", string.Format(
                            _("Lnm_DiversionRejectedFarther"), match.Icao,
                            FormatNm(match.AirportDistanceNm), plannedDest, FormatNm(plannedDistNm)));
                        return;
                    }
                }

                // ── Filter 5: established on a final for the matched runway ───────
                // NavData's cross-track is measured against an infinite centerline, so an
                // aircraft can sit right on that line while being tens of NM PAST the
                // threshold and thousands of feet up. SelectApproachThreshold rejects those
                // positions (along > 0 = past the threshold), and projects on the runway's
                // TRUE bearing, which the endpoint's own metric agrees with.
                var newThreshold = _navDataService.GetRunwayThreshold(match.Icao, lat, lon, heading);
                if (newThreshold == null)
                {
                    if (diverted)
                    {
                        ResetPendingDiversion();
                        LogRejectedMatchOnce(match.Icao + "|final", string.Format(
                            _("Lnm_DiversionRejectedNotOnFinal"),
                            match.Icao, FormatNm(match.DistToThresholdNm),
                            FormatNm(match.AirportDistanceNm)));
                    }
                    else
                    {
                        // Planned destination reconfirmed (if only by ICAO) → a diversion
                        // flag already set can only be wrong. Reverting is the safe
                        // direction: a genuine diversion keeps matching the alternate.
                        RevertDiversionIfAny(match.Icao);
                    }
                    return;
                }

                // ── Filter 6: persistence ────────────────────────────────────────
                // Only for committing a diversion. Requires the same alternate to be
                // confirmed by consecutive polls (~5 s apart), which no fly-by can do.
                if (diverted && !ConfirmPendingDiversion(match.Icao)) return;

                // Accepted: a later rejection is new information worth logging again.
                _lastRejectedMatchKey = null;

                bool revertedNow = false;
                if (diverted)
                {
                    // Flag the diversion as soon as it is genuinely established, which is
                    // still early enough for the QNH/ILS gates: those fire at TL−1000 ft /
                    // 1000 ft AGL, i.e. after the aircraft is on the final we just proved.
                    if (!match.Icao.Equals(_flightManager.DivertedAirport, StringComparison.OrdinalIgnoreCase))
                    {
                        _approachDestination = match.Icao;
                        _flightManager.SetEffectiveDestination(match.Icao);
                        _flightManager.SetDivertedAirport(match.Icao);

                        double? divertedElevFt = _navDataService.GetAirportElevationFt(match.Icao);
                        if (divertedElevFt.HasValue)
                            _flightManager.SetArrivalAirportElevation(divertedElevFt.Value);

                        _cb.Log?.Invoke(
                            string.Format(_("Lnm_DiversionDetected"), plannedDest, match.Icao),
                            Theme.Danger);
                        _cb.OsdMessage?.Invoke($"DIVERTING TO {match.Icao}", OsdSeverity.Critical);
                    }
                }
                else
                {
                    revertedNow = RevertDiversionIfAny(match.Icao);
                }

                bool runwayChanged = _approachThreshold == null
                    || !string.Equals(_approachDestination, match.Icao, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(_approachThreshold.RunwayName, match.RunwayName, StringComparison.OrdinalIgnoreCase);

                if (!runwayChanged) return;

                bool wasResolved = _approachThreshold != null;
                _approachThreshold   = newThreshold;
                _approachDestination = match.Icao;
                ClearApproachBuffer();
                _lastApproachCapture = DateTime.MinValue;

                // Tras un desvío revertido el cambio de pista es la misma noticia, no otra.
                if (!diverted && wasResolved && !revertedNow)
                    _cb.Log?.Invoke($"↻ RUNWAY UPDATED — {match.RunwayName} ({match.Icao})", Theme.Warning);

                // Recargar el ILS/approach implica llamadas a NavData: no debe bloquear el
                // hilo de telemetría, y sus fallos deben quedar registrados en vez de
                // perderse sin observar.
                FireAndForget.Run(
                    () =>
                    {
                        LoadApproachData(_approachDestination, match.RunwayName);
                        return Task.CompletedTask;
                    },
                    ex => _cb.Log?.Invoke($"⚠️ No se pudo recargar el approach: {ex.Message}", Theme.Warning),
                    "carga de approach");
            }
            finally { _reconfirmingApproachAirport = false; }
        }

        /// <summary>
        /// Reverts a diversion flag once the endpoint reconfirms the planned destination.
        /// The planned destination is the status quo ante, so this is the safe direction:
        /// if the diversion were genuine the endpoint would keep returning the alternate.
        /// Returns true when a flag was actually cleared.
        /// </summary>
        private bool RevertDiversionIfAny(string confirmedIcao)
        {
            ResetPendingDiversion();
            if (_flightManager.DivertedAirport == null) return false;

            _flightManager.ClearDivertedAirport();
            _lastAlternateCandidateIcao = null;
            _cb.Log?.Invoke(string.Format(_("Lnm_DiversionReverted"), confirmedIcao), Theme.Warning);
            return true;
        }

        /// <summary>
        /// Logs why an endpoint match was discarded, suppressing repeats: the endpoint keeps
        /// returning the same airfield on every 5 s poll while the aircraft overflies it, so
        /// without this the log would repeat one identical line all the way down the descent.
        /// </summary>
        private void LogRejectedMatchOnce(string key, string message)
        {
            if (string.Equals(_lastRejectedMatchKey, key, StringComparison.Ordinal)) return;
            _lastRejectedMatchKey = key;
            _cb.Log?.Invoke(message, Theme.SecondaryText);
        }

        /// <summary>
        /// Requires <see cref="DiversionConfirmPolls"/> consecutive polls to name the same
        /// alternate before a diversion is committed. Returns true once that is satisfied.
        /// </summary>
        private bool ConfirmPendingDiversion(string icao)
        {
            if (string.Equals(_pendingDiversionIcao, icao, StringComparison.OrdinalIgnoreCase))
                _pendingDiversionIcaoCount++;
            else
            {
                _pendingDiversionIcao      = icao;
                _pendingDiversionIcaoCount = 1;
            }
            return _pendingDiversionIcaoCount >= DiversionConfirmPolls;
        }

        private void ResetPendingDiversion()
        {
            _pendingDiversionIcao      = null;
            _pendingDiversionIcaoCount = 0;
        }

        /// <summary>Formats a nullable NM value for the diagnostic log ("?" when absent).</summary>
        private static string FormatNm(double? value)
            => value.HasValue ? value.Value.ToString("F1") : "?";


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

            double refElevation = _flightManager.ArrivalAirportElevationFt
                ?? FlightPhaseHelper.GetTerrainElevation(_flightManager.CurrentPhase, _flightManager.ActivePlan);
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

        private void OnParkingBrakeChanged(bool engaged)
        {
            _cb.Log?.Invoke(engaged ? _("Log_ParkingBrakeSet") : _("Log_ParkingBrakeReleased"), Theme.MainText);

            // Freno puesto en plena fase de pushback = fin del empuje (el remolque suelta ahí).
            // El avión queda parado, con las manos libres: es el mejor momento para revisar la
            // ruta si el puesto de destino del pushback cambia la primera calle del rodaje.
            if (engaged && _flightManager != null && _flightManager.CurrentPhase == FlightPhase.Pushback)
                RequestTaxiRoutePrompt("after pushback");
        }

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

                if (onRunway) { _pendingRunwayOnCount++;  _pendingRunwayOffCount = 0; }
                else         { _pendingRunwayOnCount = 0; _pendingRunwayOffCount++; }
                bool confirmedOnRunway  = onRunway  && _pendingRunwayOnCount  >= 2;
                bool confirmedOffRunway = !onRunway && _pendingRunwayOffCount >= 3;

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
                    if (confirmedOffRunway && _wasOnRunwayForExit
                        && (DateTime.UtcNow - _lastRunwayVacatedTime).TotalSeconds > 30)
                    {
                        _lastRunwayVacatedTime = DateTime.UtcNow;
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
                        var hp = _flightManager.CurrentGroundSpeed <= 1.5
                            ? _navDataService.FindHoldingPoint(airport, lat, lon, heading)
                            : null;
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

        // ── RAAS: activación, avisos y guía giro a giro ───────────────────────────

        /// <summary>
        /// Enciende la guía con la pista y la ruta que eligió el piloto en el popup. La ruta es
        /// texto libre separado por espacios; si queda vacía, no hay guía (pero sí avisos de
        /// hold-short, que no dependen de la ruta).
        /// </summary>
        internal void StartTaxiGuidance(string airport, string runway, string routeText,
                                        bool raasEnabled, bool voiceEnabled, int volume)
        {
            _raasAirport        = airport;
            _raasRunway         = runway;
            _raasPlan           = TaxiRoutePlan.Parse(routeText);
            _raasGuidanceActive = raasEnabled;
            _raas.ResetRouteState();

            ResolveRunwayThreshold(airport, runway, out double tLat, out double tLon);
            _raasThresholdLat = tLat;
            _raasThresholdLon = tLon;

            AppConfig.RaasVoiceEnabled = voiceEnabled;
            AppConfig.RaasVolume       = volume;
            RaasVoice.Log = msg => _cb.Log?.Invoke("⚠️ RAAS: " + msg, Theme.Warning);
            RaasVoice.Configure(raasEnabled && voiceEnabled, volume, AppConfig.Language);

            if (raasEnabled)
            {
                _cb.Log?.Invoke($"🎙️ GUÍA DE RODAJE: PISTA {runway}" +
                                (_raasPlan.Names.Count > 0 ? $"  VÍA {_raasPlan.ToText()}" : ""),
                                Theme.Taxi);
                if (voiceEnabled && !RaasVoice.Available)
                    _cb.Log?.Invoke("⚠️ RAAS: sin voz SAPI en este equipo — avisos solo en pantalla",
                                    Theme.Warning);
            }
        }

        internal void StopTaxiGuidance()
        {
            _raasGuidanceActive = false;
            _raasPlan           = null;
            _raas.ResetRouteState();
            RaasVoice.Cancel();
        }

        /// <summary>
        /// Umbral de la pista elegida, para poder medir si el avión se acerca a ella. Sin dato
        /// (pista que no está en el dataset) se devuelve NaN y el motor decide por insistencia.
        /// </summary>
        private static void ResolveRunwayThreshold(string airport, string runway,
                                                   out double lat, out double lon)
        {
            lat = double.NaN;
            lon = double.NaN;
            try
            {
                var rwy = NavDataClient.GetRunways(airport)
                    .FirstOrDefault(r => string.Equals(r.Name, runway,
                                                       StringComparison.OrdinalIgnoreCase));
                if (rwy == null) return;
                lat = rwy.ThresholdLat;
                lon = rwy.ThresholdLon;
            }
            catch { }
        }

        /// <summary>Ruta sugerida por el grafo para una pista, desde donde está el avión ahora.</summary>
        internal string SuggestTaxiRoute(string airport, string runway) =>
            SuggestTaxiRoute(airport, runway, _flightManager?.CurrentLat ?? 0.0,
                                              _flightManager?.CurrentLon ?? 0.0);

        /// <summary>Ruta sugerida por el grafo para una pista, desde un punto concreto.</summary>
        internal string SuggestTaxiRoute(string airport, string runway, double lat, double lon)
        {
            try
            {
                var rwy = NavDataClient.GetRunways(airport)
                    .FirstOrDefault(r => string.Equals(r.Name, runway,
                                                       StringComparison.OrdinalIgnoreCase));
                if (rwy == null) return "";
                var suggestion = TaxiGraph.Suggest(TaxiSegments(airport),
                                                   lat, lon, rwy.ThresholdLat, rwy.ThresholdLon);
                return suggestion.Found ? suggestion.Text : "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// Los segmentos de calle de NavData como grafo puro. Se mapea aquí, y no en el servicio,
        /// porque `TaxiGraph` es `internal` y `INavDataService` es público: exponer el tipo en la
        /// interfaz obligaría a hacerlo público solo por eso.
        /// </summary>
        private static List<TaxiGraph.Segment> TaxiSegments(string airport)
        {
            var result = new List<TaxiGraph.Segment>();
            try
            {
                foreach (var t in NavDataClient.GetTaxiways(airport))
                {
                    if (t == null || string.IsNullOrWhiteSpace(t.Name)) continue;
                    result.Add(new TaxiGraph.Segment
                    {
                        Name = t.Name.Trim(),
                        Lat1 = t.StartLat, Lon1 = t.StartLon,
                        Lat2 = t.EndLat,   Lon2 = t.EndLon
                    });
                }
            }
            catch { }
            return result;
        }

        private void RequestTaxiRoutePrompt(string reason)
        {
            if (!AppConfig.RaasEnabled || _flightManager == null) return;

            // El popup sale al encender la luz de taxi o al entrar en TaxiOut. Después del
            // pushback puede salir **una segunda vez**: el avión ya no está en el puesto, y el
            // punto de inicio del rodaje decide qué calle se toma primero. Se pide desde el fin
            // del pushback (freno de parqueo puesto, avión parado: buen momento) o, si no hubo
            // pushback —puesto remoto—, al entrar en TaxiOut.
            bool first = !_raasPromptShown;
            if (!first && (_raasRepromptShown || !_raasGuidanceActive)) return;
            if (!first) _raasRepromptShown = true;

            var plan = _flightManager.ActivePlan;
            string airport = plan?.Origin ?? _flightManager.CurrentAirport;
            if (string.IsNullOrEmpty(airport) || !_navDataService.IsAvailable) return;

            _raasPromptShown = true;

            double lat = _flightManager.CurrentLat;
            double lon = _flightManager.CurrentLon;
            string defaultRunway = plan?.OriginRunway ?? "";

            Task.Run(() =>
            {
                var runways = NavDataClient.GetRunways(airport)
                                          .Select(r => r.Name)
                                          .Where(n => !string.IsNullOrEmpty(n))
                                          .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                                          .ToList();
                if (runways.Count == 0) return;

                string suggested = SuggestTaxiRoute(airport, defaultRunway, lat, lon);

                // Un segundo aviso que propone exactamente lo mismo es ruido: si el grafo no
                // cambia de idea con el punto de inicio nuevo, no hay nada que contar. Medido:
                // desde el puesto G49 y desde el fin de su pushback la propuesta es la misma.
                if (!first && TaxiRoutePlan.SameRoute(_raasSuggestedText, suggested)) return;

                if (first) _raasSuggestedText = suggested;
                else _cb.Log?.Invoke("🎙️ Nuevo punto de rodaje: ruta recalculada desde la posición actual",
                                     Theme.Taxi);

                var prompt = new TaxiRoutePrompt
                {
                    Icao          = airport,
                    DefaultRunway = runways.Contains(defaultRunway) ? defaultRunway : runways[0],
                    Reason        = reason,
                    Recalculated  = !first,
                    SuggestedRoute = suggested,
                    // La posición se lee al pulsar RECALCULAR, no al abrir el popup: si el piloto
                    // cambia de pista, la ruta se propone desde donde está en ese momento.
                    SuggestRoute   = rwy => SuggestTaxiRoute(airport, rwy)
                };
                prompt.Runways.AddRange(runways);
                OnTaxiRoutePromptRequested?.Invoke(prompt);
            });
        }

        private void OnTaxiLightChanged(bool on)
        {
            // La luz de taxi es el gesto con el que el piloto dice «empiezo a rodar»: es el
            // momento natural para preguntar por la pista, antes de que la guía pueda servir.
            if (on) RequestTaxiRoutePrompt("taxi light");
        }

        private void EvaluateRaas(RawTelemetryData e)
        {
            if (!_raasGuidanceActive || string.IsNullOrEmpty(_raasAirport)) return;

            var phase = _flightManager?.CurrentPhase ?? FlightPhase.Idle;
            bool taxiPhase = phase == FlightPhase.Pushback || phase == FlightPhase.TaxiOut
                          || phase == FlightPhase.TakeoffRoll || phase == FlightPhase.TaxiIn
                          || phase == FlightPhase.AfterLanding || phase == FlightPhase.Boarding;
            if (!taxiPhase) return;
            if (e.GroundSpeedKt > 60) return;      // despegue/aterrizaje: aquí no opinamos

            if ((DateTime.UtcNow - _lastRaasEval) < RaasInterval) return;
            _lastRaasEval = DateTime.UtcNow;

            try
            {
                string active  = _navDataService.FindNearestTaxiway(_raasAirport, e.Latitude,
                                                                    e.Longitude, e.HeadingDeg);
                bool   onRwy   = _navDataService.FindRunwayEntry(_raasAirport, e.Latitude,
                                                                 e.Longitude, e.HeadingDeg) != null;
                var    hs      = _navDataService.FindHoldingPoint(_raasAirport, e.Latitude,
                                                                  e.Longitude, e.HeadingDeg);
                var guidance = _raasPlan != null && _raasPlan.Names.Count > 0
                    ? RaasAdvisor.ResolveGuidance(TaxiSegments(_raasAirport),
                                                  _raasPlan, e.Latitude, e.Longitude, active)
                    : null;

                var callout = _raas.Evaluate(new RaasAdvisor.Inputs
                {
                    OnRunway               = onRwy,
                    GroundSpeedKt          = e.GroundSpeedKt,
                    HoldShortRunway        = hs?.RunwayName,
                    HoldShortDistanceM     = hs != null ? hs.DistanceM : double.NaN,
                    HeadingTowardHoldShort = hs?.HeadingToward ?? false,
                    ActiveTaxiway          = active,
                    Guidance               = guidance,
                    DistanceToRunwayM      = double.IsNaN(_raasThresholdLat)
                        ? double.NaN
                        : GeoMath.DistanceNm(e.Latitude, e.Longitude,
                                             _raasThresholdLat, _raasThresholdLon)
                          * GeoMath.MetersPerNm
                }, DateTime.UtcNow);

                if (callout.Type == RaasCalloutType.None) return;

                string text = FormatCallout(callout);
                _cb.Log?.Invoke("🎙️ " + text, Theme.Taxi);
                _cb.OsdMessage?.Invoke(text.ToUpperInvariant(), OsdSeverity.Info);
                RaasVoice.Speak(text);
            }
            catch { /* la guía nunca debe tumbar el hilo de telemetría */ }
        }

        private static string FormatCallout(RaasCallout c)
        {
            int m = (int)Math.Round(c.DistanceM / 10.0) * 10;   // «en 120 m», no «en 117 m»
            switch (c.Type)
            {
                case RaasCalloutType.HoldShortApproaching:
                    return string.Format(_("Raas_HoldShortApproaching"), c.Runway);
                case RaasCalloutType.HoldShortStop:
                    return string.Format(_("Raas_HoldShortStop"), c.Runway);
                case RaasCalloutType.OffRoute:
                    return string.Format(_("Raas_OffRoute"), c.Taxiway);
                case RaasCalloutType.RouteComplete:
                    return _("Raas_RouteComplete");
                case RaasCalloutType.TurnNow:
                    if (c.Side == TurnSide.Right)  return string.Format(_("Raas_TurnNowRight"), c.Taxiway);
                    if (c.Side == TurnSide.Left)   return string.Format(_("Raas_TurnNowLeft"), c.Taxiway);
                    return string.Format(_("Raas_TurnNowStraight"), c.Taxiway);
                case RaasCalloutType.TurnAhead:
                    if (c.Side == TurnSide.Right)  return string.Format(_("Raas_TurnAheadRight"), c.Taxiway, m);
                    if (c.Side == TurnSide.Left)   return string.Format(_("Raas_TurnAheadLeft"), c.Taxiway, m);
                    return string.Format(_("Raas_TurnAheadStraight"), c.Taxiway, m);
                default:
                    return "";
            }
        }

        private void LookupRunwayData(TouchdownData data)
        {
            string plannedDest = _flightManager.ActivePlan?.Destination;
            string airport     = _approachDestination ?? plannedDest;
            if (string.IsNullOrEmpty(airport)) return;

            string resolvedAirport = airport;

            var result = _navDataService.FindTouchdownRunway(
                airport, data.LatitudeDeg, data.LongitudeDeg, data.HeadingDeg);

            if (result == null)
            {
                // The airport the approach phase resolved (the planned destination, or an
                // alternate it committed to) doesn't match the touchdown position/heading.
                // The touchdown footprint is physical evidence, so try every other airport
                // that could plausibly be the real one before giving up.
                foreach (string candidate in TouchdownFallbacks(plannedDest, resolvedAirport))
                {
                    result = _navDataService.FindTouchdownRunway(
                        candidate, data.LatitudeDeg, data.LongitudeDeg, data.HeadingDeg);
                    if (result == null) continue;

                    airport = candidate;
                    break;
                }

                if (result == null)
                {
                    _cb.Log?.Invoke(
                        string.Format(_("Lnm_RunwayNotFound"), resolvedAirport, (int)data.HeadingDeg),
                        Theme.Warning);
                    CheckFlownDistance(plannedDest);
                    return;
                }

                // Redirect the effective destination so QNH checks, the arrival parking
                // lookup and the filed PIREP all use the airport we just found on the
                // ground instead of the one the approach phase had resolved.
                _approachDestination = airport;
                _flightManager.SetEffectiveDestination(airport);
                _lastAlternateCandidateIcao = null;

                if (!airport.Equals(plannedDest, StringComparison.OrdinalIgnoreCase))
                {
                    _flightManager.SetDivertedAirport(airport);

                    double? divertedElevFt = _navDataService.GetAirportElevationFt(airport);
                    if (divertedElevFt.HasValue)
                        _flightManager.SetArrivalAirportElevation(divertedElevFt.Value);

                    _cb.Log?.Invoke(
                        string.Format(_("Lnm_ArrivalAirportMismatch"), plannedDest, airport),
                        Theme.Danger);
                    _cb.OsdMessage?.Invoke($"LANDED AT {airport} — NOT {plannedDest}", OsdSeverity.Critical);
                }
                else
                {
                    // Landed at the planned destination after all: drop any diversion flag
                    // still pointing at an alternate (and its elevation override) so the
                    // PIREP and the reference AGL come from the planned airport.
                    _flightManager.ClearDivertedAirport();
                }
            }

            _flightManager.SetRunwayTouchdownData(
                result.ThresholdDistanceFt, result.CenterlineDeviationFt, result.RunwayName);

            _cb.Log?.Invoke(
                string.Format(_("Lnm_TouchdownInfo"),
                    result.RunwayName,
                    (int)result.ThresholdDistanceFt,
                    (int)result.CenterlineDeviationFt),
                Theme.Success);

            CheckFlownDistance(plannedDest);
        }

        /// <summary>
        /// Airports to test against the touchdown footprint when the airport the approach
        /// phase resolved doesn't match, in order:
        /// <list type="number">
        /// <item>the departure airport — its NavData is always pre-loaded at flight start,
        /// and an aborted flight or short diversion lands back there;</item>
        /// <item>the last alternate the approach matcher proposed, which covers a genuine
        /// diversion whose approach was never a straight-in final (circling approach, short
        /// final) and therefore never passed the established-on-final gate;</item>
        /// <item>the planned destination, which the approach phase may have replaced with a
        /// diversion that turned out to be wrong.</item>
        /// </list>
        /// Duplicates (including the already-tried resolved airport) are skipped.
        /// </summary>
        private IEnumerable<string> TouchdownFallbacks(string plannedDest, string resolved)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(resolved)) seen.Add(resolved);

            string[] order =
            {
                _flightManager.ActivePlan?.Origin,
                _lastAlternateCandidateIcao,
                plannedDest,
            };

            foreach (string icao in order)
            {
                if (string.IsNullOrEmpty(icao)) continue;
                if (!seen.Add(icao)) continue;
                yield return icao;
            }
        }

        // Secondary heuristic: even when the runway matched the planned destination,
        // a flight that covered well under the planned distance likely never actually
        // reached it (e.g. it matched by coincidental heading alignment). Flag it for
        // manual review rather than silently filing a PIREP that looks legitimate.
        private void CheckFlownDistance(string plannedDest)
        {
            double plannedNm = _flightManager.PlannedDistanceNm;
            if (plannedNm <= 0) return;

            double actualNm = _flightManager.TotalDistanceKm * 0.539957;
            if (actualNm < plannedNm * 0.6)
            {
                _cb.Log?.Invoke(
                    string.Format(_("Lnm_DistanceMismatch"), actualNm.ToString("F0"), plannedNm.ToString("F0")),
                    Theme.Warning);
            }
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
