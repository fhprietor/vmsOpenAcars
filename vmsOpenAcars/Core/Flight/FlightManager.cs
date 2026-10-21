using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using vmsOpenAcars.Core.Helpers;
using vmsOpenAcars.Db;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.Services.Interfaces;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Core.Flight
{
    public partial class FlightManager
    {
        private readonly IApiService _apiService;
        private readonly IWeatherService _weatherService;
        private readonly PositionValidator _positionValidator;
        private readonly FlightPhaseStateMachine _phaseMachine = new FlightPhaseStateMachine();
        private readonly ApproachValidator _approachValidator;
        private readonly TouchdownState        _td               = new TouchdownState();
        private readonly PenaltyState          _pen              = new PenaltyState();
        private readonly EngineStartMonitor    _engStartMonitor  = new EngineStartMonitor();
        private readonly ThrustReverserMonitor _reverserMonitor  = new ThrustReverserMonitor();
        private readonly FlightTimer      _timer = new FlightTimer();
        private string _currentAirport = "";
        private Pilot _activePilot;
        private SimbriefPlan _activePlan;
        private int _destinationElevation = 0;
        private double _totalDistanceKm = 0;
        private DateTime? _lastAirborneTime = null;
        private DateTime? _lastPositionTime = null;
        private (double lat, double lon)? _lastPosition = null;
        private double _currentPitch = 0;
        private double _currentBank = 0;
        private bool _isParkingBrakeSet;
        private bool _areEnginesOn;
        private bool _engStabilizedOsdFired;

        private double _initialFuel = 0;
        private double _totalFuelUsed = 0;
        private double _fuelAtTakeoffRoll = 0;
        private double _fuelAtTaxiInStart = 0;
        private double _groundAltitudeFeet = 0;
        public int LastFlightScore { get; private set; } = 0;
        private int _apEngagedCounter = 0;
        private const int ApEngageDebounce = 6;
        // El umbral del motor único vive en `Helpers/SingleEngineTaxiPolicy.MinPercent`: estaba aquí
        // y duplicado en la comparación del rodaje de llegada, y ahora la regla está en un solo sitio.
        // Escritos desde Task.Run (TelemetryCoordinator.ReconfirmApproachRunway /
        // LookupRunwayData) y leídos desde el hilo de polling (BuildPhaseInput,
        // BuildScoreData) y desde el de UI (FilePirep). volatile garantiza que el hilo
        // lector vea el valor publicado y no una copia en caché de registro.
        private volatile string _effectiveDestination = null;
        private volatile string _divertedAirport = null;
        private const double UnknownElevation = double.NaN;

        // Elevación real del aeropuerto de llegada una vez confirmado un desvío.
        // Se guarda como su patrón de bits y se accede con Interlocked (double.NaN =
        // "sin dato") porque C# no permite `volatile` sobre double ni sobre long —solo
        // tipos de hasta 32 bits— y este campo se publica desde Task.Run mientras el hilo
        // de polling lo lee en cada ciclo de telemetría. Interlocked da escritura y
        // lectura atómicas de 64 bits más la barrera de memoria necesaria.
        private long _arrivalAirportElevationBits = BitConverter.DoubleToInt64Bits(UnknownElevation);

        private double ArrivalAirportElevationRaw
            => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _arrivalAirportElevationBits));
        private bool _isNavOn, _isStrobeOn, _isTaxiLightOn, _isLandingLightOn, _isBeaconOn, _isSpoilersOn;
        private bool _pendingNavOn, _pendingBeaconOn, _pendingLandingOn, _pendingTaxiOn, _pendingStrobeOn, _pendingSpoilers;
        private DateTime _navPending, _beaconPending, _landingPending, _taxiPending, _strobePending, _spoilersPending;
        private bool _hotelModeActive;
        // Aeronaves con switch único beacon/strobe: encender strobes apaga beacon (DH8D Q400)
        private static readonly HashSet<string> BeaconStrobeSharedAircraft = new HashSet<string> { "DH8D" };

        // Debounce de las luces de cumplimiento. Convive con el hold de 2.5 s de
        // FsuipcService, pero NO son redundantes ni se suman: son dos cadenas paralelas
        // sobre el mismo dato crudo.
        //   · FsuipcService (2.5 s) debouncea solo la EMISIÓN de sus eventos
        //     *LightChanged, que se usan para log y anuncios de cabina.
        //   · Este (2.0 s) debouncea el ESTADO que consumen las penalizaciones
        //     (CheckPhaseEntryLights, beacon en CheckViolations, ApproachContext).
        // Los flags de RawTelemetryData son crudos: FsuipcService nunca los filtra, así
        // que sin esta capa el scoring se evaluaría contra el bitfield sin debouncear y
        // un parpadeo del sim (~1.6 s) penalizaría. Al revertir el valor antes de
        // cumplirse el plazo, el contador se descarta, que es justo el comportamiento
        // buscado. Mismo umbral que la capa de hardware para que ambas absorban el mismo
        // flicker.
        private const double LightDebounceSeconds = 2.5;
        private const double SpoilersDebounceSeconds = 1.5;
        private int _lastFlightTimeMinutesLogged = -1;
        private double _lastDistanceLogged = -1;

        #region Properties
        public double AircraftQnhMb { get; private set; }
        public string ApNavMode { get; private set; } = "HDG";
        public string ApVertMode { get; private set; } = "ALT";

        public string CurrentAirport
        {
            get => _currentAirport;
            private set { _currentAirport = value; OnAirportChanged?.Invoke(value); }
        }
        public double PlannedDistanceNm => _activePlan?.Distance ?? 0;
        /// <summary>
        /// **¿Se ha capturado algún toque en este vuelo?** Es la marca de «este avión ya está en el campo
        /// de llegada», y tiene dos propiedades que la hacen la buena: **sobrevive al touch-and-go** —que
        /// sólo rearma la geometría de pista y deja el `Fpm` para la puntuación— y se limpia con el
        /// vuelo.
        ///
        /// Hace falta porque la fase sí puede mentir: en el PIREP real `RBmrBApLOjWlYmO2` se quedó en
        /// `Climb` con el avión aparcado en el destino, y con la referencia colgada de la fase el AGL
        /// publicado salió de **264 ft** con el avión en el suelo.
        /// </summary>
        public bool HasTouchedDownThisFlight => _td.Fpm.HasValue;

        /// <summary>Elevación del campo de llegada: la real si el desvío ya se confirmó, si no la planeada.</summary>
        private double ArrivalElevationOrPlanned
            => double.IsNaN(ArrivalAirportElevationRaw) ? _activePlan.DestinationElevation
                                                        : ArrivalAirportElevationRaw;

        public double ReferenceAirportElevation
        {
            get
            {
                if (_activePlan == null) return 0;

                // **Ya aterrizó: el campo de debajo es el de llegada, diga lo que diga la fase.** Sin
                // esto, una fase equivocada (el `Climb` del PIREP `RBmrBApLOjWlYmO2`) devolvía la
                // elevación del **origen** y fabricaba un AGL de 264 ft con el avión aparcado.
                if (HasTouchedDownThisFlight) return ArrivalElevationOrPlanned;

                switch (CurrentPhase)
                {
                    case FlightPhase.Descent:
                    case FlightPhase.Approach:
                    case FlightPhase.Landing:
                    case FlightPhase.AfterLanding:
                    case FlightPhase.TaxiIn:
                    case FlightPhase.OnBlock:
                    case FlightPhase.Arrived:
                    case FlightPhase.Completed:
                        // Elevación real del aeropuerto de llegada si el desvío ya se
                        // confirmó; si no, la planeada del destino.
                        return ArrivalElevationOrPlanned;
                    default:
                        return _activePlan.OriginElevation;
                }
            }
        }
        public bool IsApproachStabilized
        {
            get
            {
                if (CurrentPhase != FlightPhase.Approach &&
                    CurrentPhase != FlightPhase.Landing) return false;

                double agl = CurrentAltitude - ReferenceAirportElevation;
                if (agl > 1000) return true;   // por encima del gate — aún no aplica

                // Misma ventana de Vapp que el gate de scoring: antes estaba fijada a
                // 100–160 kt, de modo que el indicador STABLE/UNSTABLE de la UI podía
                // marcar estable un avión que el gate sí habría penalizado (y al revés
                // en una aeronave ligera). Fuente única: la tabla de performance.
                var (vappMin, vappMax) = AircraftPerformanceTable.GetApproachSpeedRange(_approachValidator.AircraftIcao);
                bool speedOk = CurrentIndicatedAirspeed >= vappMin &&
                               CurrentIndicatedAirspeed <= vappMax;
                bool vsOk = CurrentVerticalSpeed >= -1000 &&
                            CurrentVerticalSpeed <= -100;
                bool bankOk = Math.Abs(_currentBank) <= 7.0;
                bool pitchOk = _currentPitch >= -2.5 && _currentPitch <= 10.0;
                bool gearOk = IsGearDown;
                bool configOk = CurrentFlapsPosition >= 50;

                return speedOk && vsOk && bankOk && pitchOk && gearOk && configOk;
            }
        }
        public double TotalDistanceKm => _totalDistanceKm;
        public double CurrentLat { get; private set; }
        public double CurrentLon { get; private set; }
        public double CurrentHeading { get; private set; }
        public double TouchdownLat        => _td.Lat;
        public double TouchdownLon        => _td.Lon;
        public double TouchdownHeadingDeg => _td.HeadingDeg;
        public int CurrentIndicatedAirspeed { get; private set; }
        public int CurrentAltitude { get; private set; }
        public int CurrentVerticalSpeed { get; private set; }
        public int?   TouchdownFpm          => _td.Fpm;
        /// <summary>True when a touchdown was actually captured this flight.</summary>
        public bool   TouchdownDataCaptured => _td.Captured && _td.Fpm.HasValue;
        public double TouchdownDistanceFt   => _td.DistanceFt;
        public double TouchdownCenterlineFt => _td.CenterlineDeviationFt;
        public string TouchdownRunwayName   => _td.RunwayName;
        /// <summary>Longitud de la pista de la toma, en pies; **0 = sin dato** (la puntuación
        /// «Touchdown Zone» degrada entonces a la regla de siempre).</summary>
        public double TouchdownRunwayLengthFt => _td.RunwayLengthFt;
        /// <summary>Rumbo **VERDADERO** del eje de la pista de la toma; **null = sin dato**. Es el
        /// eje contra el que se descomponen las componentes del viento del aterrizaje.</summary>
        public double? TouchdownRunwayTrueHeadingDeg => _td.RunwayTrueHeadingDeg;

        /// <summary>
        /// Umbral desplazado de la pista de la toma, en pies: **`0` es un valor** (alineado) y
        /// **`null` es «no se resolvió la pista»**. Va al payload como `offset_threshold_ft` para
        /// que phpVMS alinee la traza del flare con una **resta** en vez de deduciéndolo.
        /// </summary>
        public double? TouchdownOffsetThresholdFt => _td.OffsetThresholdFt;
        public double TouchdownGForce       => _td.GForce;

        /// <summary>
        /// **Congela el toque para el informe del vuelo.** Lo llama
        /// `AcarsReporter.SnapshotLandingRecord` al copiar el registro —después del último
        /// aterrizaje y antes de `FilePirep`—, y a partir de ahí la puntuación y el payload leen
        /// esa misma lectura en vez del toque vivo, que un rearme del detector puede sobrescribir.
        /// Motivo y medidas: `TouchdownState.FrozenTouchdown`.
        /// </summary>
        public void FreezeTouchdownForReport() => _td.FreezeIfNeeded();

        /// <summary>
        /// El toque que se **publica** (payload) y el que se **puntúa**: la lectura congelada del
        /// informe, o el toque vivo si nadie la congeló (el registro por pantalla, que no filea).
        /// </summary>
        private TouchdownState.FrozenTouchdown TouchdownForReport => _td.Frozen ?? _td.FreezeIfNeeded();

        public int  OverspeedCount               => _approachValidator.OverspeedCount;
        public int  OverspeedPenaltyCount        => _approachValidator.OverspeedPenaltyCount;
        public int  LightsViolationCount         => _approachValidator.LightsViolationCount;
        public int  StabilizedApproachDeductions => _approachValidator.StabilizedDeductions;
        public int  QnhViolationCount            => _approachValidator.QnhViolations;
        public bool IsOfflineFlight              => _pen.IsOfflineFlight;
        public bool DepartedLate                 => _pen.DepartedLate;
        public int  ProcedureSpdViolations       => _pen.ProcedureSpdViolations;
        public int  LocalizerViolations          => _approachValidator.LocalizerViolations;
        public bool BelowMinimums                => _approachValidator.BelowMinimums;
        public Func<bool> IsOnAtcFrequency
        {
            get => _approachValidator.IsOnAtcFrequency;
            set => _approachValidator.IsOnAtcFrequency = value;
        }
        public FlightPhase CurrentPhase => _phaseMachine.CurrentPhase;
        public int CurrentGroundSpeed { get; private set; }
        public double CurrentFuel { get; private set; }
        public bool IsOnGround { get; private set; }
        public string ActivePirepId { get; private set; } = "";
        public RawTelemetryData LastRawData { get; private set; }
        public DateTime FlightStartTime { get; private set; }
        public Pilot ActivePilot => _activePilot;
        public SimbriefPlan ActivePlan => _activePlan;
        public bool IsSimulatorConnected { get; private set; }
        public ValidationStatus PositionValidationStatus { get; private set; }
        public int CurrentTransponder { get; private set; }
        public bool AutopilotEngaged { get; private set; }
        public DateTime SimTime { get; private set; }
        public double RadarAltitude { get; private set; }
        public int PositionOrder { get; private set; }
        public double InitialFuel => _initialFuel;
        public double TotalFuelUsed => _totalFuelUsed;
        public bool IsBlockOffRecorded => _timer.BlockOffRecorded;
        public bool IsTimerStarted     => _timer.IsTimerStarted;
        public bool IsParkingBrakeSet => _isParkingBrakeSet;
        public bool AreEnginesOn => _areEnginesOn;

        /// <summary>
        /// El veredicto del rodaje **de salida**, con sus cifras. Lo consume el registro del
        /// aterrizaje (`AcarsReporter.SnapshotLandingRecord`) para que la base local guarde lo mismo
        /// que decidió la bonificación: la regla se evalúa en un solo sitio
        /// (<see cref="SingleEngineTaxiPolicy"/>) y aquí no se recalcula nada.
        /// </summary>
        internal SingleEngineTaxiVerdict TaxiOutSingleEngineVerdict()
            => SingleEngineTaxiPolicy.Evaluate(
                _pen.TaxiOutMovingSeconds, _pen.TaxiOutSingleEngineSeconds, _pen.BothEnginesRunning);

        /// <summary>El veredicto del rodaje **de llegada**, con sus cifras. Ver el de salida.</summary>
        internal SingleEngineTaxiVerdict TaxiInSingleEngineVerdict()
            => SingleEngineTaxiPolicy.Evaluate(
                _pen.TaxiInMovingSeconds, _pen.TaxiInSingleEngineSeconds, _pen.BothEnginesRunning);

        public bool HasSimulatorData { get; private set; }

        public double CurrentAGL
        {
            get
            {
                if (IsOnGround) return 0;
                switch (CurrentPhase)
                {
                    case FlightPhase.Enroute:
                        return Math.Max(0, CurrentAltitude - _groundAltitudeFeet);
                    default:
                        return CurrentAltitude - ReferenceAirportElevation;
                }
            }
        }

        public bool IsGearDown { get; private set; }
        public double CurrentFlapsPosition { get; private set; }
        public string FlapsLabel { get; private set; } = "UP";
        public bool AreSpoilersDeployed { get; private set; }
        public string AutobrakeSetting { get; private set; } = "RTO";
        public bool IsNavLightOn { get; private set; }
        public bool IsBeaconLightOn { get; private set; }
        public bool IsLandingLightOn { get; private set; }
        public bool IsTaxiLightOn { get; private set; }
        public bool IsStrobeLightOn { get; private set; }
        public float N1_1 { get; private set; }
        public float N1_2 { get; private set; }
        #endregion

        #region Events

        public event Action<string> OnPhaseChanged;
        public event Action<FlightPhase> PhaseChanged;
        public event Action<string> OnAirportChanged;
        public event Action<string, Color> OnLog;
        public event Action<string, OsdSeverity> OnOsdMessage;
        public event Action<ValidationStatus> OnPositionValidated;
        public event Action<int, double, double, double> OnLandingDetected;
        public event Action OnBlockDetected;
        public event Action<int, int, int> OnTakeoffDetected;
        public event Action<double, double, double, string, bool> OnTaxiPositionUpdate;

        #endregion

        public FlightManager(IApiService apiService, IWeatherService weatherService)
        {
            _apiService = apiService;
            _weatherService = weatherService;
            _positionValidator = new PositionValidator();
            PositionValidationStatus = new ValidationStatus();
            _approachValidator = new ApproachValidator(BeaconStrobeSharedAircraft);
            _approachValidator.SetWeatherService(weatherService);
            WirePhaseMachine();
            WireApproachValidator();
        }

        private void WirePhaseMachine()
        {
            _phaseMachine.OnTransitioned += (from, to) =>
            {
                if (to != FlightPhase.Idle)
                    OnLog?.Invoke(_("Log_Phase" + to), Theme.MainText);
                PhaseChanged?.Invoke(to);
                OnPhaseChanged?.Invoke(to.ToString());
                CheckProcedureAtPhaseEntry(to);

                // AfterLanding→TaxiIn: log trip fuel, record taxi-in start fuel
                if (to == FlightPhase.TaxiIn)
                {
                    if (_fuelAtTakeoffRoll > 0)
                    {
                        double tripFuel = _fuelAtTakeoffRoll - CurrentFuel;
                        if (tripFuel > 0)
                            OnLog?.Invoke(_("Log_FuelTrip", (int)Math.Round(tripFuel)), Theme.MainText);
                    }
                    _fuelAtTaxiInStart = CurrentFuel;
                }

                // phpVMS status update — AfterLanding uses "LAN" (touchdown); others via helper
                if (to != FlightPhase.OnBlock && to != FlightPhase.Completed)
                {
                    string code = to == FlightPhase.AfterLanding
                        ? "LAN"
                        : FlightPhaseHelper.GetStatusCode(to);
                    Task.Run(() => UpdatePirepStatus(code));
                }
            };

            _phaseMachine.OnTakeoffDetected  += (gs, alt, vs) => OnTakeoffDetected?.Invoke(gs, alt, vs);
            _phaseMachine.OnTaxiPositionUpdate += (lat, lon, hdg, ap, isTaxiIn) =>
                OnTaxiPositionUpdate?.Invoke(lat, lon, hdg, ap, isTaxiIn);

            _phaseMachine.OnTouchdownDetected += vs => RegisterTouchdown(vs);

            _phaseMachine.OnBlockOffNeeded += () => Task.Run(() => UpdateBlockOffTime());

            // El cierre del vuelo vive en un método y no en la lambda porque **hay dos caminos** que
            // tienen que hacer exactamente lo mismo: la detección (parado 90 s con los motores
            // apagados) y el cierre manual del piloto (`ForceOnBlock`, ver la Ola 0 y el PIREP
            // `RBmrBApLOjWlYmO2`).
            _phaseMachine.OnBlockOnDetected += CompleteBlockOn;

            _phaseMachine.OnApproachGateReset += () => _approachValidator.ResetGate();

            _phaseMachine.OnTouchAndGo += () =>
            {
                _td.ResetRunwayData();
                _approachValidator.ResetApproachData();
            };

            _phaseMachine.OnLog       += (msg, color) => OnLog?.Invoke(msg, color);
            _phaseMachine.OnOsdMessage += (msg, sev)  => OnOsdMessage?.Invoke(msg, sev);
        }

        private void WireApproachValidator()
        {
            _approachValidator.OnLog        += (msg, color) => OnLog?.Invoke(msg, color);
            _approachValidator.OnOsdMessage += (msg, sev)   => OnOsdMessage?.Invoke(msg, sev);
        }

        #region Private Methods

        /// <summary>
        /// Aplica debounce a un estado booleano: solo acepta un cambio de estado
        /// después de que haya sido estable durante el tiempo especificado.
        /// </summary>
        private static void DebounceState(bool raw, ref bool stable, ref bool pending, ref DateTime pendingSince, double debounceSeconds)
        {
            if (raw == stable) { pending = stable; return; }
            if (raw != pending) { pending = raw; pendingSince = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - pendingSince).TotalSeconds >= debounceSeconds)
                stable = pending;
        }

        /// <summary>
        /// Verifica compliance de procedimientos al entrar en una nueva fase.
        /// Penaliza y logea cada violación encontrada.
        /// Se llama una sola vez por transición de fase.
        /// </summary>
        private void CheckProcedureAtPhaseEntry(FlightPhase newPhase)
        {
            _approachValidator.CheckPhaseEntryLights(newPhase, _isNavOn, _isTaxiLightOn, _isStrobeOn, _isLandingLightOn);

            if (newPhase == FlightPhase.TakeoffRoll)
            {
                _apEngagedCounter  = 0;
                _fuelAtTakeoffRoll = CurrentFuel;

                // Engine idle-time check: warn if any engine started during taxi-out
                // without meeting the minimum warm-up period.
                double oat     = LastRawData?.OatCelsius ?? 15.0;
                bool eng1      = LastRawData?.Eng1Running ?? false;
                bool eng2      = LastRawData?.Eng2Running ?? false;
                var readiness  = _engStartMonitor.CheckPreTakeoff(oat, eng1, eng2);
                if (!readiness.Ready)
                {
                    _pen.EngineWarmupViolation = true;
                    OnLog?.Invoke($"⚠️ IDLE MOTOR INSUFICIENTE: {readiness.Reason}", Theme.Warning);
                    OnOsdMessage?.Invoke("ENGINE IDLE TIME  ⚠️", OsdSeverity.Warning);
                }
                else
                {
                    string e1 = _engStartMonitor.Eng1IdleTime > TimeSpan.Zero
                        ? $"ENG1 idle {(int)_engStartMonitor.Eng1IdleTime.TotalSeconds}s"
                        : $"ENG1 pre-arrancado";
                    string e2 = _engStartMonitor.Eng2IdleTime > TimeSpan.Zero
                        ? $"ENG2 idle {(int)_engStartMonitor.Eng2IdleTime.TotalSeconds}s"
                        : $"ENG2 pre-arrancado";
                    string stab1 = _engStartMonitor.Eng1Stabilized ? "STAB ✓" : "sin estabilizar";
                    string stab2 = _engStartMonitor.Eng2Stabilized ? "STAB ✓" : "sin estabilizar";
                    OnLog?.Invoke($"Motor: {e1} [{stab1}]  {e2} [{stab2}]  OAT {oat:F0}°C", Theme.MainText);
                }
                // Stabilization gate — oil temp + pressure + N2 must be in range for all running engines.
                bool eng1Stab = !eng1 || _engStartMonitor.Eng1Stabilized;
                bool eng2Stab = !eng2 || _engStartMonitor.Eng2Stabilized;
                if (!eng1Stab || !eng2Stab)
                {
                    _pen.EngineStabilizationViolation = true;
                    string which = (!eng1Stab && !eng2Stab) ? "ENG1+ENG2" : !eng1Stab ? "ENG1" : "ENG2";
                    OnLog?.Invoke($"⚠️ MOTORES NO ESTABILIZADOS al entrar en pista ({which}) — −5 pts", Theme.Warning);
                    OnOsdMessage?.Invoke("ENGINES NOT STABLE  −5 PTS", OsdSeverity.Warning);
                }

                double taxiOutFuel = _initialFuel - CurrentFuel;
                if (taxiOutFuel > 0)
                    OnLog?.Invoke(_("Log_FuelTaxiOut", (int)Math.Round(taxiOutFuel)), Theme.MainText);
                // Veredicto del criterio de motor único, **se otorgue o no**. Hasta v0.9.42 el piloto
                // que rodaba con un motor y no llegaba al 50 % no veía ni una línea: el PIREP real
                // `lA4ZVAzgxYzlG4vD` hizo 290 s de 630 (46 %) en la salida y el log quedó mudo. Ver
                // `Helpers/SingleEngineTaxiPolicy.cs`.
                var singleEngineOut = EvaluateAndPublishSingleEngineTaxi(
                    "TaxiPhaseOut", _pen.TaxiOutMovingSeconds, _pen.TaxiOutSingleEngineSeconds,
                    _pen.TaxiOutNoEngineSeconds, _pen.TaxiOutMetres);
                if (!_pen.SingleEngineTaxiDetected && singleEngineOut.Awarded)
                {
                    _pen.SingleEngineTaxiDetected = true;
                    OnOsdMessage?.Invoke("SINGLE ENGINE TAXI  +5 PTS", OsdSeverity.Success);
                }
                if (!string.IsNullOrEmpty(_activePlan?.Origin))
                    _approachValidator.CheckQnhAsync(_activePlan.Origin, AircraftQnhMb).ConfigureAwait(false);
            }

            if (newPhase == FlightPhase.TaxiIn)
            {
                if (_reverserMonitor.ReversersUsed)
                    OnLog?.Invoke($"Reversa: detectada  ENG1 {_reverserMonitor.MaxEng1RevPct:F1}%  ENG2 {_reverserMonitor.MaxEng2RevPct:F1}%  — cool-down {ThrustReverserMonitor.COOLDOWN_REQUIRED_SECONDS}s requeridos", Theme.MainText);
                else
                    OnLog?.Invoke("Reversa: no detectada" + (_reverserMonitor.ReverserDataAvailable ? "" : " (offset 0x207C sin datos — addon puede no soportarlo)"), Theme.MainText);
            }
        }


        /// <summary>
        /// Publica el veredicto del criterio de motor único y lo devuelve, para que el llamante decida
        /// con él la bonificación y el OSD.
        ///
        /// **Se publica se otorgue o no, y ahí está el cambio**: hasta v0.9.42 sólo se escribía algo
        /// cuando la bonificación se concedía, así que un rodaje con un motor por debajo del umbral era
        /// indistinguible de no haber rodado nunca con uno. El caso real `lA4ZVAzgxYzlG4vD` —**290 s
        /// de 630 (46 %)** en la salida— se quedó sin una sola línea en el log.
        ///
        /// Las cifras que salen son las mismas que deciden (`SingleEngineTaxiPolicy.Evaluate`), no una
        /// segunda cuenta que pudiera decir otra cosa.
        /// </summary>
        private SingleEngineTaxiVerdict EvaluateAndPublishSingleEngineTaxi(
            string phaseLabelKey, double movingSeconds, double singleEngineSeconds,
            double noEngineSeconds, double metres)
        {
            var verdict = SingleEngineTaxiPolicy.Evaluate(
                movingSeconds, singleEngineSeconds, _pen.BothEnginesRunning);
            PublishSingleEngineTaxiVerdict(phaseLabelKey, verdict, noEngineSeconds, metres);
            return verdict;
        }

        /// <summary>
        /// Publica el veredicto del **rodaje completo** —las dos fases sumadas en un solo porcentaje— y lo
        /// devuelve. Va aparte de <see cref="EvaluateAndPublishSingleEngineTaxi"/> porque el porcentaje
        /// total no es un camino a la bonificación, sino la cifra que la explica: ver el comentario del
        /// llamante y `SingleEngineTaxiPolicy.EvaluateWholeTaxi`.
        /// </summary>
        private SingleEngineTaxiVerdict EvaluateAndPublishWholeTaxi()
        {
            var verdict = SingleEngineTaxiPolicy.EvaluateWholeTaxi(
                _pen.TaxiOutMovingSeconds, _pen.TaxiOutSingleEngineSeconds,
                _pen.TaxiInMovingSeconds,  _pen.TaxiInSingleEngineSeconds,
                _pen.BothEnginesRunning);
            PublishSingleEngineTaxiVerdict("TaxiPhaseTotal", verdict,
                _pen.TaxiOutNoEngineSeconds + _pen.TaxiInNoEngineSeconds,
                _pen.TaxiOutMetres + _pen.TaxiInMetres);
            return verdict;
        }

        /// <summary>
        /// Escribe la línea del veredicto, **se otorgue o no**: las cifras que salen son las mismas que
        /// deciden, no una segunda cuenta que pudiera decir otra cosa.
        ///
        /// Lleva también los segundos que se **descartaron** por no tener ningún motor en marcha
        /// (`noEngineSeconds`) y los **metros realmente recorridos** (`metres`): lo primero son el
        /// remolque del pushback o ruido de GS, no rodaje; lo segundo es lo que el piloto puede
        /// comprobar por su cuenta. Sin esos dos números, el piloto del PIREP `29Z3A8r748rGPnzq` leyó un
        /// 33 % que en realidad era un 49 %.
        /// </summary>
        private void PublishSingleEngineTaxiVerdict(
            string phaseLabelKey, SingleEngineTaxiVerdict verdict, double noEngineSeconds, double metres)
        {
            OnLog?.Invoke(_("Log_SingleEngineTaxiVerdict",
                _(phaseLabelKey),
                (int)Math.Round(verdict.SingleEngineSeconds),
                (int)Math.Round(verdict.MovingSeconds),
                verdict.Percent,
                (int)Math.Round(noEngineSeconds),
                (int)Math.Round(metres),
                SingleEngineTaxiPolicy.MinPercent,
                _(SingleEngineTaxiPolicy.OutcomeKey(verdict.Outcome))), Theme.MainText);
        }

        private void ValidateAirportMatch()
        {
            if (_activePilot == null || _activePlan == null) return;
            bool match = _positionValidator.CompareIcaoCodes(_activePilot.CurrentAirport, _activePlan.Origin);
            PositionValidationStatus.IcaoMatch = match;
            PositionValidationStatus.PhpVmsAirport = _activePilot.CurrentAirport;
            PositionValidationStatus.SimbriefAirport = _activePlan.Origin;
            if (match) OnLog?.Invoke($"{_("DepartureAirportOk")} {_activePlan.Origin}", Theme.MainText);
            else OnLog?.Invoke($"{_("Warning")}: {_("YouAreAssigned")} {_activePilot.CurrentAirport}, {_("ButFlightDepartureIs")} {_activePlan.Origin}", Theme.Warning);
            OnPositionValidated?.Invoke(PositionValidationStatus);
        }

        private void ValidateSimulatorPosition(double currentLat, double currentLon)
        {
            if (_activePilot == null) return;
            var (isValid, distance, message, color) = _positionValidator.ValidatePosition(
                _activePilot.CurrentAirport, _activePilot.CurrentAirportLat, _activePilot.CurrentAirportLon, currentLat, currentLon);
            bool changed = (PositionValidationStatus.GpsValid != isValid) || (Math.Abs(PositionValidationStatus.DistanceFromAirport - distance) > 0.01);
            PositionValidationStatus.GpsValid = isValid;
            PositionValidationStatus.DistanceFromAirport = distance;
            if (changed) OnLog?.Invoke(message, color);
            OnPositionValidated?.Invoke(PositionValidationStatus);
        }

        #endregion

        #region Public Methods

        public double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
            => UnitConverter.CalculateDistanceKm(lat1, lon1, lat2, lon2);

        /// <summary>Returns a snapshot of engine lifecycle state for UI display.</summary>
        public EngineLifecycleSnapshot GetEngineLifecycleSnapshot()
        {
            double oat = LastRawData?.OatCelsius ?? 20.0;
            return new EngineLifecycleSnapshot
            {
                Eng1Running             = LastRawData?.Eng1Running ?? false,
                Eng2Running             = LastRawData?.Eng2Running ?? false,
                Eng1Stabilized          = _engStartMonitor.Eng1Stabilized,
                Eng2Stabilized          = _engStartMonitor.Eng2Stabilized,
                Eng1IdleTime            = _engStartMonitor.Eng1IdleTime,
                Eng2IdleTime            = _engStartMonitor.Eng2IdleTime,
                OilTemp1                = LastRawData?.OilTemp_1 ?? 0f,
                OilTemp2                = LastRawData?.OilTemp_2 ?? 0f,
                OilPress1               = LastRawData?.OilPress_1 ?? 0f,
                OilPress2               = LastRawData?.OilPress_2 ?? 0f,
                OatCelsius              = oat,
                RequiredIdleSeconds     = oat < EngineStartMonitor.COLD_OAT_THRESHOLD
                                            ? EngineStartMonitor.COLD_IDLE_SECONDS
                                            : EngineStartMonitor.WARM_IDLE_SECONDS,
                ReversersUsed           = _reverserMonitor.ReversersUsed,
                MaxEng1RevPct           = _reverserMonitor.MaxEng1RevPct,
                MaxEng2RevPct           = _reverserMonitor.MaxEng2RevPct,
                CooldownSecondsElapsed  = _reverserMonitor.SecondsSinceTouchdown,
                CooldownSecondsRequired = _reverserMonitor.ReversersUsed
                                            ? ThrustReverserMonitor.COOLDOWN_REQUIRED_SECONDS : 0,
                ReverserDataAvailable   = _reverserMonitor.ReverserDataAvailable,
            };
        }



        public string   CurrentTimerDisplay => _timer.CurrentTimerDisplay;
        public TimeSpan CurrentFlightTime   => _timer.CurrentFlightTime;

        public void SetActivePilot(Pilot pilot)
        {
            _activePilot = pilot;
            CurrentAirport = pilot?.CurrentAirport ?? "";
            if (_activePlan != null) ValidateAirportMatch();
        }

        public void SetActivePlan(SimbriefPlan plan)
        {
            _activePlan = plan;
            if (plan != null)
            {
                _destinationElevation = plan.DestinationElevation;
                OnLog?.Invoke(_("Log_PlanDestination", plan.Destination, _destinationElevation), Theme.MainText);
            }
            ValidateAirportMatch();
            if (CurrentLat != 0 && CurrentLon != 0) UpdatePositionValidation(CurrentLat, CurrentLon);
        }

        public void SetSimulatorConnected(bool connected, double? latitude = null, double? longitude = null)
        {
            IsSimulatorConnected = connected;
            if (connected && latitude.HasValue && longitude.HasValue && _activePilot != null)
            {
                if (_activePilot.CurrentAirportLat.HasValue && _activePilot.CurrentAirportLon.HasValue)
                    ValidateSimulatorPosition(latitude.Value, longitude.Value);
                else
                {
                    PositionValidationStatus.GpsValid = false;
                    OnPositionValidated?.Invoke(PositionValidationStatus);
                }
            }
            else
            {
                PositionValidationStatus.GpsValid = false;
                OnPositionValidated?.Invoke(PositionValidationStatus);
            }
        }

        public void UpdatePositionValidation(double lat, double lon)
        {
            if (_activePilot != null && IsSimulatorConnected) ValidateSimulatorPosition(lat, lon);
        }

        public bool IsPilotAtDepartureAirport(string requiredAirport) => CurrentAirport?.Equals(requiredAirport, StringComparison.OrdinalIgnoreCase) ?? false;

        public void MarkOfflineFlight() => _pen.IsOfflineFlight = true;

        public void SetRunwayTouchdownData(double thresholdDistFt, double centerlineDeviationFt,
                                           string runwayName, double runwayLengthFt,
                                           double? runwayTrueHeadingDeg, double? offsetThresholdFt)
            => _td.SetRunwayData(thresholdDistFt, centerlineDeviationFt, runwayName, runwayLengthFt,
                                 runwayTrueHeadingDeg, offsetThresholdFt);

        public void SetProcedureSpdViolations(int count)
            => _pen.ProcedureSpdViolations = count;

        public void SetApproachData(IlsData ils, ApproachInfo approach, IList<ApproachFix> fixes)
            => _approachValidator.SetApproachData(ils, approach, fixes);

        public void SetOriginTransitionAlt(double ft)
        {
            if (ft > 0) _approachValidator.OriginTransitionAltFt = ft;
        }
        public void SetDestTransitionLevel(double ft)
        {
            if (ft > 0) _approachValidator.DestTransitionLevelFt = ft;
        }
        public void SetEffectiveDestination(string icao)
        {
            _effectiveDestination = icao;
            _approachValidator.EffectiveDestination = icao;
        }

        /// <summary>
        /// Aeropuerto de llegada real confirmado (desvío detectado), o null si el vuelo
        /// sigue apuntando al destino planeado. Solo lectura: se fija desde
        /// <see cref="SetEffectiveDestination"/>.
        /// </summary>
        public string EffectiveDestination => _effectiveDestination;

        // Confirmed by NavData's nearest/approach-airport match (icao != planned
        // destination) — drives the `diversion-airport` field sent at file time.
        // Distinct from _effectiveDestination: that one also gets set from the
        // touchdown-time fallback chain, which is a lower-confidence guess.
        public void SetDivertedAirport(string icao) => _divertedAirport = icao;
        public string DivertedAirport => _divertedAirport;

        // Real elevation of the confirmed arrival airport (from NavData), once known —
        // overrides the originally-planned destination's elevation in
        // ReferenceAirportElevation and BuildPhaseInput()'s DestinationElevation. Set
        // alongside SetDivertedAirport at the same two call sites in TelemetryCoordinator.
        public void SetArrivalAirportElevation(double elevationFt)
            => Interlocked.Exchange(ref _arrivalAirportElevationBits, BitConverter.DoubleToInt64Bits(elevationFt));
        /// <summary>Elevación real del aeropuerto de llegada, o null si aún no se conoce.</summary>
        public double? ArrivalAirportElevationFt
        {
            get
            {
                double raw = ArrivalAirportElevationRaw;
                return double.IsNaN(raw) ? (double?)null : raw;
            }
        }

        // Revierte un desvío marcado por error (falso positivo geométrico transitorio) cuando
        // el tracking local vuelve a confirmar el destino planeado. No se llama en touchdown —
        // ahí el desvío, si lo hay, ya es definitivo.
        public void ClearDivertedAirport()
        {
            if (_divertedAirport == null) return;
            _divertedAirport = null;
            _effectiveDestination = null;
            _approachValidator.EffectiveDestination = null;
            Interlocked.Exchange(ref _arrivalAirportElevationBits, BitConverter.DoubleToInt64Bits(UnknownElevation));
        }

        /// <summary>
        /// ¿Tiene sentido ofrecerle al piloto el cierre manual? Hay vuelo abierto y la fase todavía no
        /// es `OnBlock`: exactamente la situación en la que `SEND PIREP` no está disponible y
        /// **cancelar es la única salida que ofrece la aplicación** — y borra el PIREP.
        /// </summary>
        public bool CanForceOnBlock
            => !string.IsNullOrEmpty(ActivePirepId)
            && CurrentPhase != FlightPhase.OnBlock
            && CurrentPhase != FlightPhase.Completed;

        /// <summary>
        /// **Cierra el vuelo a mano**, por si la detección no llegó. Hace lo mismo que el camino
        /// automático —el mismo <see cref="CompleteBlockOn"/>, no una copia— y mueve la fase a
        /// `OnBlock`, que es lo que habilita `SEND PIREP` en `MainViewModel`.
        ///
        /// Nace del PIREP real **`RBmrBApLOjWlYmO2`**: su BE58 se quedó clasificado en `Climb` desde el
        /// falso touch-and-go de las 03:36:02, `OnBlock` fue inalcanzable y al piloto **sólo le quedaba
        /// cancelar**, que borra el PIREP. Aquel vuelo lo tuvo que cerrar un administrador desde
        /// phpVMS.
        /// </summary>
        /// <returns><c>true</c> si cerró el vuelo; <c>false</c> si no había nada que cerrar.</returns>
        public bool ForceOnBlock()
        {
            if (!CanForceOnBlock) return false;

            OnLog?.Invoke(_("Log_ManualOnBlock"), Theme.Warning);
            _phaseMachine.ForceOnBlock();
            CompleteBlockOn();
            return true;
        }

        /// <summary>
        /// El cierre del vuelo, **idéntico por los dos caminos**: lo llama `HandleTaxiIn` a través de
        /// `OnBlockOnDetected` cuando se cumple la condición (parado 90 s con los motores apagados), o
        /// <see cref="ForceOnBlock"/> cuando el piloto lo pide desde la interfaz. Que sea el mismo
        /// método es el contrato: el cierre manual no puede cerrar «a medias».
        /// </summary>
        private void CompleteBlockOn()
        {
            // Taxi-in fuel log
            if (_fuelAtTaxiInStart > 0)
            {
                double taxiInFuel = _fuelAtTaxiInStart - CurrentFuel;
                if (taxiInFuel > 0)
                    OnLog?.Invoke(_("Log_FuelTaxiIn", (int)Math.Round(taxiInFuel)), Theme.MainText);
            }
            // Mismo veredicto para el rodaje de llegada, con sus propias cifras.
            var singleEngineIn = EvaluateAndPublishSingleEngineTaxi(
                "TaxiPhaseIn", _pen.TaxiInMovingSeconds, _pen.TaxiInSingleEngineSeconds,
                _pen.TaxiInNoEngineSeconds, _pen.TaxiInMetres);
            if (!_pen.SingleEngineTaxiDetected && singleEngineIn.Awarded)
            {
                _pen.SingleEngineTaxiDetected = true;
                OnOsdMessage?.Invoke("SINGLE ENGINE TAXI  +5 PTS", OsdSeverity.Success);
            }

            // Y el **rodaje completo**, con las dos fases sumadas en un solo porcentaje (Ola 4). Se
            // publica como **información**, no como un tercer camino a la bonificación, y no por
            // prudencia: es **matemáticamente imposible** que la suma apruebe lo que las fases no
            // aprueben. Con `a/w1 < 0,5` y `b/w2 < 0,5` se tiene `a + b < 0,5·(w1 + w2)`, así que
            // `(a+b)/(w1+w2) ≥ 0,5` obliga a que **alguna** fase llegue sola al umbral — y ésa ya habría
            // premiado arriba. Lo que sí aporta el número es el diagnóstico: el vuelo real
            // `lA4ZVAzgxYzlG4vD` hizo **290 s de 630** en la salida (46 %) y **26 %** en el rodaje
            // completo, que es la cifra que explica por qué no cobró. Ver `SingleEngineTaxiPolicyTests`.
            if (!_pen.SingleEngineTaxiDetected)
                EvaluateAndPublishWholeTaxi();

            _timer.RecordBlockOn(DateTime.UtcNow);
            OnLog?.Invoke(_("Log_BlockOn", _timer.ServerBlockOnTime.ToString("HH:mm:ss")), Theme.MainText);
            OnBlockDetected?.Invoke();
        }

        public bool CanStartFlight()
        {
            if (_activePilot == null) return false;
            if (_activePlan == null) return false;
            if (!PositionValidationStatus.IcaoMatch) return false;
            return true;
        }

        /// <summary>
        /// Marca del ciclo de telemetría anterior. **Un solo reloj para todos los contadores de tiempo
        /// del vuelo** —el rodaje con un motor y la fase atascada—: si cada regla llevara el suyo, dos
        /// reglas distintas podrían estar midiendo huecos distintos del mismo ciclo.
        /// </summary>
        private DateTime _lastTelemetrySampleUtc = DateTime.MinValue;

        /// <summary>
        /// Segundos creíbles desde el ciclo anterior. Un hueco negativo (reloj movido hacia atrás) o
        /// mayor que <see cref="SampleClock.MaxGapSeconds"/> —equipo suspendido a mitad de vuelo— vale
        /// cero: contar ese salto regalaría minutos de rodaje que no se volaron.
        /// </summary>
        private double ConsumeTelemetryDeltaSeconds()
        {
            double dt = SampleClock.CountableSeconds(
                _lastTelemetrySampleUtc == DateTime.MinValue
                    ? 0.0
                    : (DateTime.UtcNow - _lastTelemetrySampleUtc).TotalSeconds);
            _lastTelemetrySampleUtc = DateTime.UtcNow;
            return dt;
        }

        public void UpdateTelemetry(RawTelemetryData data)
        {
            if (data == null) return;
            ApplyRawData(data);
            CheckEngineEvents(data);

            // Los dos contadores de tiempo del vuelo comparten el mismo delta creíble.
            double dt = ConsumeTelemetryDeltaSeconds();
            UpdateSingleEngineTaxi(data, dt);
            UpdateStuckAirPhase(dt);

            UpdateFlightTracking(data);

            // Gracia del check de luces de `TaxiOut` (v0.9.40): la entrada en la fase **armó** el
            // check, y es este ciclo —que ve la fase viva— el que lo evalúa 5 s después, o lo
            // cancela si el rodaje no se sostuvo. Va fuera de `UpdateFlightTracking` a propósito:
            // aquel sale antes de tiempo cuando no hay PIREP activo.
            _approachValidator.TickDeferredTaxiOutLights(CurrentPhase, _isNavOn, _isTaxiLightOn);
        }

        public async Task UpdateFlightProgress()
        {
            if (string.IsNullOrEmpty(ActivePirepId) || !_timer.IsTimerStarted) return;
            DateTime reference = _timer.ServerBlockOffTime != default ? _timer.ServerBlockOffTime : _timer.ServerCreatedAt;
            int flightTimeMinutes = (int)(DateTime.UtcNow - reference).TotalMinutes;
            double currentDistance = _totalDistanceKm * 0.539957;

            bool timeChanged = Math.Abs(flightTimeMinutes - _lastFlightTimeMinutesLogged) >= 1;
            bool distanceChanged = Math.Abs(currentDistance - _lastDistanceLogged) >= 1;
            if (!timeChanged && !distanceChanged) return;

            try
            {
                bool success = await _apiService.UpdatePirep(ActivePirepId, new { flight_time = flightTimeMinutes, distance = currentDistance });
                if (success)
                {
                    _lastFlightTimeMinutesLogged = flightTimeMinutes;
                    _lastDistanceLogged = currentDistance;
                }
            }
            catch (Exception ex) { OnLog?.Invoke(_("Log_ErrorFlightProgress", ex.Message), Theme.Danger); }
        }

        /// <summary>
        /// Manda a phpVMS la ruta de rodaje que eligió el piloto, **tal cual la tecleó**, al campo
        /// personalizado `Taxi Route` (ver Helpers/PirepFields.cs). Es la autorización de ATC tal
        /// como la entendió él, y es la fuente buena de la observación de rutas: la traza tiene ruido
        /// de GPS y desvíos, el texto no. Se manda en segundo plano para no bloquear al que llama
        /// (el popup de rodaje, o el hilo de telemetría si algún día se reenvía desde ahí).
        /// </summary>
        public void SendTaxiRouteFields(string departureRunway, string taxiRoute)
        {
            string pirepId = ActivePirepId;              // capturado: el vuelo puede resetearse antes
            if (string.IsNullOrEmpty(pirepId)) return;

            var fields = PirepFields.Build(departureRunway, null, taxiRoute);
            if (fields.Count == 0) return;

            Task.Run(async () =>
            {
                try { await _apiService.UpdatePirep(pirepId, new { fields }); }
                catch { /* que el PIREP no lleve la ruta no puede tumbar el vuelo */ }
            });
        }

        /// <summary>
        /// Manda la meteo del aterrizaje al campo personalizado `Landing Weather`
        /// (<see cref="PirepFields.LandingWeatherName"/>), el **camino estructurado** que solo se puede
        /// usar cuando phpVMS ha creado ese campo: una clave desconocida no da error, se ignora en
        /// silencio, y por eso el llamante lo gatea con
        /// <see cref="AppConfig.PirepLandingWeatherFieldEnabled"/> (apagado por defecto).
        ///
        /// El camino que funciona hoy es el `notes` del PIREP, que lleva la misma línea y no depende
        /// de que nadie cree nada.
        /// </summary>
        public void SendLandingWeatherField(string landingWeatherLine)
        {
            string pirepId = ActivePirepId;              // capturado: el vuelo puede resetearse antes
            if (string.IsNullOrEmpty(pirepId) || string.IsNullOrEmpty(landingWeatherLine)) return;

            var fields = PirepFields.Build(null, null, null, landingWeatherLine);
            if (fields.Count == 0) return;

            Task.Run(async () =>
            {
                try { await _apiService.UpdatePirep(pirepId, new { fields }); }
                catch { /* que el PIREP no lleve la meteo estructurada no puede tumbar el vuelo */ }
            });
        }

        /// <summary>
        /// Deja constancia en el PIREP de un aviso del RAAS, con el prefijo `RAAS:` para que phpVMS
        /// pueda distinguirlo de las filas `CHK` (que llevan el desglose del score). Hasta ahora los
        /// avisos solo vivían en el fichero de log local del piloto, así que no eran auditables desde
        /// el PIREP. Va con la posición y en segundo plano, para no bloquear el hilo de telemetría, y
        /// se lee con `GET /acars/logs` —el endpoint que sirve los `type = 2`—, no con
        /// `/acars/position`: phpVMS añadió ese GET el 29/09/2026 al comprobar que nuestros avisos se
        /// guardaban sin que nadie pudiera leerlos.
        /// </summary>
        public void LogRaasCallout(string text, double lat, double lon, int heading)
        {
            string pirepId = ActivePirepId;
            if (string.IsNullOrEmpty(pirepId) || string.IsNullOrEmpty(text)) return;

            var entry = AcarsLogEntry.At("RAAS: " + text, lat, lon);

            Task.Run(async () =>
            {
                try
                {
                    await _apiService.SendAcarsLogs(pirepId, new[] { entry });
                }
                catch { /* idem: el aviso ya se dijo y se oyó, esto es solo el registro */ }
            });
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
        public override string ToString() => $"ICAO: {(IcaoMatch ? "✅" : "❌")} GPS: {(GpsValid ? "✅" : "⏳")} Dist: {DistanceFromAirport:F1}km";
    }
}
