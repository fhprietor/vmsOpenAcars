// =============================================================================
// Propósito : Gestión completa de la conexión FSUIPC con el simulador de vuelo.
//             Lee offsets en bruto, los decodifica a unidades estándar y emite
//             eventos a los consumidores (MainViewModel, FlightManager).
//
// Unidades de salida (todas las propiedades públicas y TelemetryData):
//   Distancias / altitudes : feet
//   Velocidades            : knots  (GS, IAS)  |  fpm  (VS)
//   Ángulos                : grados decimales
//   Combustible            : lbs  (total)  |  lbs/hr  (flujo)
//   Temperatura            : °C
//   Fechas / horas         : UTC  (DateTime.UtcNow)
//   Cultura de formato     : CultureInfo.InvariantCulture
//
// Referencia de offsets: "FSUIPC for Programmers" – Pete Dowson, v4.975
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using FSUIPC;

// =============================================================================
// Enums y Decoder para Autobrake
// =============================================================================

public enum AutobrakeMode
{
    Unknown = -1,
    Off = 0,
    RTO = 1,   // Boeing
    Low = 2,
    Med = 3,
    High = 4,  // Boeing 777/787
    Max = 5    // Airbus / Boeing 737
}

public enum AircraftFamily
{
    Unknown,
    Airbus,        // A318, A319, A320, A321, A330, A340, A350, A380
    Boeing737,     // 737 NG, 737 MAX, iFly, PMDG
    Boeing747,     // 747, 757, 767
    Boeing777,     // 777, 787
    Embraer,       // E170, E175, E190, E195
    Cessna,        // C172, C182, C208
    Piston         // Otros aviones pequeños
}

namespace vmsOpenAcars.Services
{
    public class FsuipcService : IDisposable
    {
        // =====================================================================
        #region Offsets FSUIPC
        // =====================================================================

        // ---- Posición ----
        private readonly Offset<long> _latOffset = new Offset<long>(0x0560);
        private readonly Offset<long> _lonOffset = new Offset<long>(0x0568);
        private readonly Offset<long> _altOffset = new Offset<long>(0x0570);
        private readonly Offset<uint> _headingOffset = new Offset<uint>(0x0580);

        // ---- Velocidades ----
        private readonly Offset<int> _groundSpeedOffset = new Offset<int>(0x02B4);
        private readonly Offset<int> _verticalSpeedOffset = new Offset<int>(0x02C8);
        private readonly Offset<int> _iasOffset = new Offset<int>(0x02BC);

        // ---- Actitud ----
        private readonly Offset<int> _pitchOffset = new Offset<int>(0x0578);
        private readonly Offset<int> _bankOffset = new Offset<int>(0x057C);

        // ---- Altimetría ----
        private readonly Offset<int> _radarAltitudeOffset = new Offset<int>(0x31E4);
        private readonly Offset<int> _groundAltitudeOffset = new Offset<int>(0x0020);

        // ---- Estado de vuelo ----
        private readonly Offset<short> _onGroundOffset = new Offset<short>(0x0366);
        private readonly Offset<int> _simTimeOffset = new Offset<int>(0x023A);

        // ---- Combustible ----
        private readonly Offset<int> _fuelWeightOffset = new Offset<int>(0x126C);

        // ---- Motores (FLOAT64, los más precisos para jets/turboprops) ----
        /// <summary>0x2000 · FLOAT64 · N1 motor 1 en % (0.0–100.0)</summary>
        private readonly Offset<double> _eng1N1Offset = new Offset<double>(0x2000);
        /// <summary>0x2100 · FLOAT64 · N1 motor 2 en % (0.0–100.0)</summary>
        private readonly Offset<double> _eng2N1Offset = new Offset<double>(0x2100);
        /// <summary>0x207C · FLOAT64 · reversor motor 1 (0.0–1.0)</summary>
        private readonly Offset<double> _eng1ReverserOffset = new Offset<double>(0x207C);
        /// <summary>0x217C · FLOAT64 · reversor motor 2 (0.0–1.0)</summary>
        private readonly Offset<double> _eng2ReverserOffset = new Offset<double>(0x217C);

        // ---- Turboprop / Piston — bloque FLOAT64 (FSUIPC7/MSFS) ----
        /// <summary>0x2008/0x2108 · FLOAT64 · RPM eje motor</summary>
        private readonly Offset<double> _eng1RpmF64 = new Offset<double>(0x2008);
        private readonly Offset<double> _eng2RpmF64 = new Offset<double>(0x2108);
        /// <summary>0x2028/0x2128 · FLOAT64 · Throttle lever % (0–100)</summary>
        private readonly Offset<double> _eng1ThrottleF64 = new Offset<double>(0x2028);
        private readonly Offset<double> _eng2ThrottleF64 = new Offset<double>(0x2128);
        /// <summary>0x2038/0x2138 · FLOAT64 · Torque ft·lb absoluto</summary>
        private readonly Offset<double> _eng1TorqueF64 = new Offset<double>(0x2038);
        private readonly Offset<double> _eng2TorqueF64 = new Offset<double>(0x2138);
        /// <summary>0x2040/0x2140 · FLOAT64 · Prop RPM (hélice)</summary>
        private readonly Offset<double> _eng1PropRpmF64 = new Offset<double>(0x2040);
        private readonly Offset<double> _eng2PropRpmF64 = new Offset<double>(0x2140);
        /// <summary>0x2068/0x2168 · FLOAT64 · Torque % (0–100)</summary>
        private readonly Offset<double> _eng1TorquePctF64 = new Offset<double>(0x2068);
        private readonly Offset<double> _eng2TorquePctF64 = new Offset<double>(0x2168);
        /// <summary>0x2048/0x2148 · FLOAT64 · Manifold Absolute Pressure (inHg)</summary>
        private readonly Offset<double> _eng1MapF64 = new Offset<double>(0x2048);
        private readonly Offset<double> _eng2MapF64 = new Offset<double>(0x2148);
        /// <summary>0x2060/0x2160 · FLOAT64 · Cylinder Head Temp (°C)</summary>
        private readonly Offset<double> _eng1ChtF64 = new Offset<double>(0x2060);
        private readonly Offset<double> _eng2ChtF64 = new Offset<double>(0x2160);
        /// <summary>0x2050/0x2150 · FLOAT64 · Oil Temperature (°C)</summary>
        private readonly Offset<double> _eng1OilTempF64 = new Offset<double>(0x2050);
        private readonly Offset<double> _eng2OilTempF64 = new Offset<double>(0x2150);
        /// <summary>0x2058/0x2158 · FLOAT64 · Oil Pressure (PSI)</summary>
        private readonly Offset<double> _eng1OilPressF64 = new Offset<double>(0x2058);
        private readonly Offset<double> _eng2OilPressF64 = new Offset<double>(0x2158);

        // ---- Motores INT16 (pistón / turboprop, fallback) ----
        /// <summary>0x0898 · INT16 · N1/RPM motor 1 (0–16384 = 0–100%)</summary>
        private readonly Offset<short> _eng1N1Int = new Offset<short>(0x0898);
        /// <summary>0x0930 · INT16 · N1/RPM motor 2 (0–16384 = 0–100%)</summary>
        private readonly Offset<short> _eng2N1Int = new Offset<short>(0x0930);
        /// <summary>0x0898 · INT16 · RPM motor 1 (raw directo)</summary>
        private readonly Offset<int> _engRpmOffset = new Offset<int>(0x0898);

        // ---- Controles ----
        private readonly Offset<int> _gearOffset = new Offset<int>(0x0BE8);
        private readonly Offset<short> _flapsOffset = new Offset<short>(0x0BDC);
        private readonly Offset<short> _spoilersOffset = new Offset<short>(0x0BD8);
        private readonly Offset<short> _spoilersArmedOffset = new Offset<short>(0x0BCC);

        // ---- Flaps ----
        private readonly Offset<byte> _flapsHandleIndex = new Offset<byte>(0x0BFC);     // Notch actual (0,1,2,3...)
        private readonly Offset<int> _flapsHandlePercent = new Offset<int>(0x0BDC);    // Posición del handle 0-16383
        private readonly Offset<int> _flapsActualLeft = new Offset<int>(0x0BE0);       // Posición física real izquierda
        private readonly Offset<int> _flapsActualRight = new Offset<int>(0x0BE4);      // Posición física real derecha

        // ---- Autopiloto / Nav ----
        private readonly Offset<short> _autopilotOffset = new Offset<short>(0x07BC);
        private readonly Offset<short> _navModeOffset = new Offset<short>(0x07CC);
        private readonly Offset<short> _transponderOffset = new Offset<short>(0x0354);

        // ---- Frenos ----
        private readonly Offset<short> _parkingBrakeOffset = new Offset<short>(0x0BC8);
        /// <summary>0x0330 · INT16 · Altímetro Kohlsman setting. QNH_hPa = value / 16.0</summary>
        private readonly Offset<short> _kohlsmanOffset = new Offset<short>(0x0330);
        private readonly Offset<short> _brakeLeftOffset = new Offset<short>(0x0BC4);
        private readonly Offset<short> _brakeRightOffset = new Offset<short>(0x0BC6);
        //private readonly Offset<byte> _autobrakeOffset = new Offset<byte>(0x2F80);

        // ---- Autobrake ----
        private readonly Offset<string> _aircraftTitleOffset = new Offset<string>(0x3D00, 256);
        private readonly Offset<short> _abAirbusOffset = new Offset<short>(0x0260);
        private readonly Offset<byte> _abGenericOffset = new Offset<byte>(0x0C46);

        // ---- Meteorología ----
        /// <summary>
        /// 0x0E90 · INT16 · velocidad del viento en knots (valor directo).
        /// </summary>
        private readonly Offset<short> _windSpeedOffset = new Offset<short>(0x0E90);

        /// <summary>
        /// 0x0E92 · USHORT · dirección del viento raw sin signo.
        /// Conversión: degrees = raw * 360.0 / 65536.0
        /// CRÍTICO: debe ser ushort, NO short. Con short el valor negativo da -31521° etc.
        /// </summary>
        private readonly Offset<ushort> _windDirOffset = new Offset<ushort>(0x0E92);

        /// <summary>0x0E8C · INT16 · OAT = °C × 256</summary>
        private readonly Offset<short> _oatOffset = new Offset<short>(0x0E8C);

        // ---- Acelerometría ----
        /// <summary>0x11BA · INT16 · G = raw / 625.0</summary>
        private readonly Offset<short> _gforceOffset = new Offset<short>(0x11BA);

        // ---- Luces ----
        private readonly Offset<short> _lightsOffset = new Offset<short>(0x0D0C);

        // ---- Aeronave ----
        private readonly Offset<string> _aircraftTitle = new Offset<string>(0x3D00, 256);
        private readonly Offset<string> _icaoDesignator = new Offset<string>(0x0618, 16);
        private readonly Offset<string> _icaoManufacturer = new Offset<string>(0x09D2, 16);
        private readonly Offset<string> _icaoModel = new Offset<string>(0x0B26, 32);

        #endregion

        // =====================================================================
        #region Estado Interno
        // =====================================================================

        // -- Polling y conexión --
        private Timer _pollingTimer;
        private int _pollingIntervalMs;
        private bool _isRunning;
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private int _connectionRetryCount;
        private int _currentBackoffMs = 1000;
        private bool _isReconnecting;

        // -- Telemetría --
        private int _positionOrder;
        private DateTime _lastTelemetrySend = DateTime.MinValue;
        private double _currentPhaseInterval = AppConfig.UpdateIntervalOther;

        // -- Aeronave --
        private bool _aircraftInfoRead;

        // -- Estado anterior (detección de cambios) --
        private bool _lastOnGround = true;
        private int _lastGearPosition;
        private double _lastFlapsPercent;
        private bool _lastSpoilersDeployed;
        private int _lastParkingBrake;
        private int _lastEngineRpm;
        private bool _lastBeaconState;

        // -- Debounce flaps --
        private DateTime _lastFlapsChangeTime = DateTime.MinValue;
        private const double FLAPS_DEBOUNCE_MS = 500;
        private const double FLAPS_HYSTERESIS = 1.0;

        // -- Categoría de planta motriz (detectada por título del avión) --
        private AircraftCategory _currentEngineCategory = AircraftCategory.Unknown;

        // -- Debounce liftoff / touchdown --
        private int _groundConsecutiveCounter;
        private DateTime _lastTakeoffTime = DateTime.MinValue;
        private DateTime _lastTouchdownTime = DateTime.MinValue;

        // -- VS tracking: capturar el último VS aéreo para touchdown correcto --
        /// <summary>
        /// VS del último frame en el que el avión estaba airborne (isOnGround == false).
        /// Se usa en HandleTouchdown para reportar el VS real del momento de contacto,
        /// no el VS del primer frame en tierra (que ya puede ser 0).
        /// </summary>
        private double _lastAirborneVsFpm = 0.0;

        // -- G-Force peak durante approach --
        private double _peakGforceApproach = 1.0;

        // -- Constantes de detección --
        private const int GROUND_CONFIRM_FRAMES = 1;
        private const double EVENT_DEBOUNCE_SECONDS = 2.0;
        private const double TAKEOFF_MIN_SPEED_KT = 40.0;
        private const double TOUCHDOWN_MIN_SPEED_KT = 30.0;
        private const double TOUCHDOWN_MIN_VS_FPM = -50.0;

        #endregion

        // =====================================================================
        #region Propiedades Públicas
        // =====================================================================

        public bool IsConnected => _connectionState == ConnectionState.Connected;
        public string SimulatorName { get; private set; } = "Desconocido";

        // ---- Posición ----
        public double CurrentLatitude { get; private set; }
        public double CurrentLongitude { get; private set; }
        public double CurrentAltitudeFeet { get; private set; }
        public double CurrentRadarAltitudeFeet { get; private set; }

        // ---- Velocidades ----
        public double CurrentGroundSpeedKt { get; private set; }
        public double CurrentVerticalSpeedFpm { get; private set; }
        public double CurrentIndicatedAirspeed { get; private set; }

        // ---- Actitud ----
        public double CurrentPitch { get; private set; }
        public double CurrentBank { get; private set; }
        public double CurrentHeading { get; private set; }

        // ---- Combustible ----
        public double CurrentFuelLbs { get; private set; }
        /// <summary>QNH seleccionado en el altímetro del avión, en hPa. Calculado desde offset 0x0330.</summary>
        public double AircraftQnhMb { get; private set; }

        // ---- Controles ----
        public int CurrentGearPosition { get; private set; }
        public double CurrentFlapsPercent { get; private set; }
        public bool CurrentSpoilersDeployed { get; private set; }
        public string FlapsLabel { get; private set; } = "UP";
        public int FlapsHandleRaw { get; private set; }
        public int FlapsActualRaw { get; private set; }
        public byte FlapsIndex { get; private set; }
        public bool FlapsInTransit { get; private set; }
        public double FlapsPercent { get; private set; }

        // ---- Entorno ----
        public double CurrentGForce { get; private set; }
        public bool IsBeaconOn { get; private set; }

        // ---- Meteorología (convertida) ----
        /// <summary>Velocidad del viento en knots.</summary>
        public int CurrentWindSpeedKt { get; private set; }
        /// <summary>Dirección del viento en grados 0..360 (ya convertida).</summary>
        public double CurrentWindDirDeg { get; private set; }
        /// <summary>OAT en grados Celsius.</summary>
        public double CurrentOatCelsius { get; private set; }

        // ---- Autobrake ----
        public string AutobrakeLabel { get; private set; } = "---";
        public AutobrakeMode AutobrakeMode { get; private set; } = AutobrakeMode.Unknown;

        // ---- Motores ----
        public EnginePower Engine1Power { get; private set; }
        public EnginePower Engine2Power { get; private set; }
        public AircraftCategory EngineCategory => _currentEngineCategory;

        // ---- Aeronave ----
        public string AircraftTitle { get; private set; } = "Unknown";
        public string AircraftIcao { get; private set; } = "????";
        public string AircraftManufacturer { get; private set; } = "Unknown";
        public string AircraftModel { get; private set; } = "Unknown";

        #endregion

        // =====================================================================
        #region Eventos Públicos
        // =====================================================================

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<TelemetryData> TelemetryUpdated;
        public event EventHandler<RawTelemetryData> RawDataUpdated;
        public event EventHandler<TakeoffData> TakeoffDetected;
        public event EventHandler<TouchdownData> TouchdownDetected;
        public event Action<int, int> GearChanged;
        public event Action<double, double> FlapsChanged;
        public event Action<bool> SpoilersChanged;
        public event Action<bool> ParkingBrakeChanged;
        public event Action<bool> EnginesChanged;
        public event Action<bool> BeaconChanged;
        public event Action OnAircraftInfoReady;

        #endregion

        // =====================================================================
        #region Ciclo de Vida
        // =====================================================================

        public FsuipcService()
        {
            _pollingIntervalMs = AppConfig.PollingIntervalMs;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _pollingTimer = new Timer(OnPollingTick, null, 500, _pollingIntervalMs);
            Debug.WriteLine("FsuipcService: polling started.");
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _pollingTimer?.Dispose();
            _pollingTimer = null;
            Disconnect();
            Debug.WriteLine("FsuipcService: polling stopped.");
        }

        public void Dispose() => Stop();

        #endregion

        // =====================================================================
        #region Intervalo Adaptativo
        // =====================================================================

        public void SetUpdateIntervalForPhase(FlightPhase phase)
        {
            switch (phase)
            {
                case FlightPhase.TaxiOut:
                case FlightPhase.TaxiIn:
                    _currentPhaseInterval = AppConfig.UpdateIntervalTaxi; break;
                case FlightPhase.TakeoffRoll:
                case FlightPhase.Takeoff:
                    _currentPhaseInterval = AppConfig.UpdateIntervalTakeoff; break;
                case FlightPhase.Climb:
                    _currentPhaseInterval = AppConfig.UpdateIntervalClimb; break;
                case FlightPhase.Enroute:
                    _currentPhaseInterval = AppConfig.UpdateIntervalCruise; break;
                case FlightPhase.Descent:
                    _currentPhaseInterval = AppConfig.UpdateIntervalDescent; break;
                case FlightPhase.Approach:
                case FlightPhase.Landing:
                    _currentPhaseInterval = AppConfig.UpdateIntervalApproach; break;
                default:
                    _currentPhaseInterval = AppConfig.UpdateIntervalOther; break;
            }
        }

        #endregion

        // =====================================================================
        #region Polling Principal
        // =====================================================================

        private void OnPollingTick(object state)
        {
            if (!_isRunning) return;

            if (_connectionState == ConnectionState.Disconnected)
            {
                TryConnect();
                return;
            }

            try
            {
                FSUIPCConnection.Process();

                if (!_aircraftInfoRead)
                {
                    ReadAircraftInfo();
                    _aircraftInfoRead = true;
                    ReadAllOffsets();
                    OnAircraftInfoReady?.Invoke();
                }

                _connectionRetryCount = 0;
                _currentBackoffMs = 1000;
                _isReconnecting = false;

                ReadAllOffsets();
                DetectEvents();
                EmitRawData();
                SendTelemetry();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FsuipcService: Process error — {ex.Message}");
                _connectionState = ConnectionState.Disconnected;
                SimulatorName = "Desconocido";
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }


        private void EmitRawData()
        {
            // ── Luces (0x0D0C) ────────────────────────────────────────────────
            short lights = _lightsOffset.Value;
            bool navLightOn = (lights & 0x01) != 0;  // bit 0: NAV
            bool beaconLightOn = (lights & 0x02) != 0;  // bit 1: BEACON
            bool landingLightOn = (lights & 0x04) != 0;  // bit 2: LANDING
            bool taxiLightOn = (lights & 0x08) != 0;  // bit 3: TAXI
            bool strobeRaw = (lights & 0x10) != 0;  // bit 4: STROBE
            bool seatBeltSign = (lights & 0x20) != 0;  // bit 5: SEAT BELT
            // B737/B38M: switch NAV+STROBE combinado → strobe real solo si beacon ON
            bool strobeLightOn = strobeRaw && (beaconLightOn || navLightOn);

            // ── Autopilot (0x07BC) — bitfield ─────────────────────────────────
            short apBits = _autopilotOffset.Value;
            bool apMaster = (apBits & 0x01) != 0;  // bit 0: master AP
            bool apLnav = (apBits & 0x04) != 0;  // bit 2: LNAV
            bool apVnav = (apBits & 0x08) != 0;  // bit 3: VNAV
            bool apLoc = (apBits & 0x10) != 0;  // bit 4: LOC
            bool apGs = (apBits & 0x20) != 0;  // bit 5: GS
            string apNavMode = apGs ? "ILS" : apLoc ? "LOC" : apLnav ? "LNAV" : "HDG";
            string apVertMode = apGs ? "GS" : apVnav ? "VNAV" : "ALT";

            // ── Autobrake y parking brake ─────────────────────────────────────
            string autobrakeSetting = AutobrakeLabel;
            // Umbral 60% (9830/16383) para filtrar ruido analógico de X-Plane
            bool parkingBrakeOn = _parkingBrakeOffset.Value > ParkingBrakeThreshold;

            // ── QNH del altímetro (Kohlsman 0x0330) ──────────────────────────
            double aircraftQnhMb = AircraftQnhMb;

            // ── Actualizar categoría de planta motriz por título ──────────────
            var detectedCat = DetectEngineCategoryFromTitle();
            if (detectedCat != AircraftCategory.Unknown)
                _currentEngineCategory = detectedCat;

            // ── Leer valores crudos FLOAT64 ───────────────────────────────────
            float n1_1 = (float)_eng1N1Offset.Value;
            float n1_2 = (float)_eng2N1Offset.Value;

            float rpm_1 = (float)_eng1RpmF64.Value;
            float rpm_2 = (float)_eng2RpmF64.Value;
            float torqPct_1 = (float)_eng1TorquePctF64.Value;
            float torqPct_2 = (float)_eng2TorquePctF64.Value;
            float propRpm_1 = (float)_eng1PropRpmF64.Value;
            float propRpm_2 = (float)_eng2PropRpmF64.Value;
            float map_1 = (float)_eng1MapF64.Value;
            float map_2 = (float)_eng2MapF64.Value;
            float cht_1 = (float)_eng1ChtF64.Value;
            float cht_2 = (float)_eng2ChtF64.Value;
            float oilT_1 = (float)_eng1OilTempF64.Value;
            float oilT_2 = (float)_eng2OilTempF64.Value;
            float oilP_1 = (float)_eng1OilPressF64.Value;
            float oilP_2 = (float)_eng2OilPressF64.Value;
            float throt_1 = (float)_eng1ThrottleF64.Value;
            float throt_2 = (float)_eng2ThrottleF64.Value;

            // ── ¿Motores encendidos? — umbral por categoría ───────────────────
            bool eng1Running, eng2Running;
            switch (_currentEngineCategory)
            {
                case AircraftCategory.Piston:
                    eng1Running = rpm_1 > 400;
                    eng2Running = rpm_2 > 400;
                    break;
                case AircraftCategory.Turboprop:
                    eng1Running = n1_1 > 10 || torqPct_1 > 5;
                    eng2Running = n1_2 > 10 || torqPct_2 > 5;
                    break;
                default: // Jet / Unknown
                    eng1Running = n1_1 > 15;
                    eng2Running = n1_2 > 15;
                    break;
            }
            bool enginesRunning = eng1Running || eng2Running;

            // ── Potencia de despegue aplicada ─────────────────────────────────
            bool takeoffPowerSet;
            switch (_currentEngineCategory)
            {
                case AircraftCategory.Piston:
                    takeoffPowerSet = throt_1 > 80 || throt_2 > 80;
                    break;
                case AircraftCategory.Turboprop:
                    takeoffPowerSet = torqPct_1 > 60 || torqPct_2 > 60;
                    break;
                default: // Jet
                    takeoffPowerSet = n1_1 > 70 || n1_2 > 70;
                    break;
            }

            RawDataUpdated?.Invoke(this, new RawTelemetryData
            {
                // ── Datos básicos ─────────────────────────────────────────────
                Latitude = CurrentLatitude,
                Longitude = CurrentLongitude,
                AltitudeFeet = CurrentAltitudeFeet,
                GroundSpeedKt = CurrentGroundSpeedKt,
                HeadingDeg = CurrentHeading,
                VerticalSpeedFpm = CurrentVerticalSpeedFpm,
                IndicatedAirspeedKt = CurrentIndicatedAirspeed,
                IsOnGround = _lastOnGround,
                FuelLbs = CurrentFuelLbs,
                Transponder = _transponderOffset.Value,
                RadarAltitudeFeet = CurrentRadarAltitudeFeet,
                PitchDeg = CurrentPitch,
                BankDeg = CurrentBank,
                SpoilersDeployed = CurrentSpoilersDeployed,
                FlapsPercent = FlapsPercent,
                FlapsLabel = this.FlapsLabel,
                FlapsInTransit = FlapsInTransit,
                GearDown = CurrentGearPosition == 1,
                Order = _positionOrder,

                // ── Frenos, motores, autobrake ────────────────────────────────
                ParkingBrakeOn = parkingBrakeOn,
                EnginesRunning = enginesRunning,
                AutobrakeSetting = autobrakeSetting,

                // ── Luces ─────────────────────────────────────────────────────
                NavLightOn = navLightOn,
                BeaconLightOn = beaconLightOn,
                LandingLightOn = landingLightOn,
                TaxiLightOn = taxiLightOn,
                StrobeLightOn = strobeLightOn,
                SeatBeltSign = seatBeltSign,

                // ── Autopilot ─────────────────────────────────────────────────
                AutopilotEngaged = apMaster,
                ApMaster = apMaster,
                ApNavMode = apNavMode,
                ApVertMode = apVertMode,

                // ── Meteorología ──────────────────────────────────────────────
                AircraftQnhMb = aircraftQnhMb,

                // ── Categoría y estado de motores ─────────────────────────────
                EngineCategory = _currentEngineCategory,
                Eng1Running = eng1Running,
                Eng2Running = eng2Running,
                TakeoffPowerSet = takeoffPowerSet,

                // Jet (N1)
                N1_1 = n1_1,
                N1_2 = n1_2,

                // Turboprop
                TorquePct_1 = torqPct_1,
                TorquePct_2 = torqPct_2,
                PropRpm_1 = propRpm_1,
                PropRpm_2 = propRpm_2,

                // Piston
                Rpm_1 = rpm_1,
                Rpm_2 = rpm_2,
                Map_1 = map_1,
                Map_2 = map_2,
                Cht_1 = cht_1,
                Cht_2 = cht_2,
                OilTemp_1 = oilT_1,
                OilTemp_2 = oilT_2,
                OilPress_1 = oilP_1,
                OilPress_2 = oilP_2,
                Throttle_1 = throt_1,
                Throttle_2 = throt_2,
            });
        }

        #endregion

        // =====================================================================
        #region Lectura de Offsets → Propiedades
        // =====================================================================

        private void ReadAllOffsets()
        {
            if (_connectionState != ConnectionState.Connected) return;

            // ---- Posición ----
            CurrentLatitude = DecodeLatitude(_latOffset.Value);
            CurrentLongitude = DecodeLongitude(_lonOffset.Value);
            CurrentAltitudeFeet = DecodeAltitude(_altOffset.Value);
            CurrentHeading = DecodeHeading(_headingOffset.Value);

            // Radar altímetro: metros → feet
            double radarMeters = _radarAltitudeOffset.Value;
            CurrentRadarAltitudeFeet = radarMeters > 0 ? radarMeters * 3.28084 : 0.0;

            // ---- Velocidades ----
            CurrentGroundSpeedKt = DecodeGroundSpeed(_groundSpeedOffset.Value);
            CurrentVerticalSpeedFpm = DecodeVerticalSpeed(_verticalSpeedOffset.Value);
            CurrentIndicatedAirspeed = DecodeIndicatedAirspeed(_iasOffset.Value);

            // ---- Actitud ----
            CurrentPitch = DecodePitch(_pitchOffset.Value);
            CurrentBank = DecodeBank(_bankOffset.Value);

            // ---- Combustible ----
            CurrentFuelLbs = _fuelWeightOffset.Value;
            // Kohlsman: raw INT16 → hPa (raw / 16.0)
            AircraftQnhMb = _kohlsmanOffset.Value / 16.0;

            // ---- G-Force ----
            CurrentGForce = _gforceOffset.Value / 625.0;

            // ---- Meteorología (conversiones correctas) ----
            CurrentWindSpeedKt = _windSpeedOffset.Value;                           // kt directo
            CurrentWindDirDeg = _windDirOffset.Value * 360.0 / 65536.0;           // ushort → grados
            CurrentOatCelsius = _oatOffset.Value / 256.0;                         // °C × 256

            // ---- Luces ----
            IsBeaconOn = (_lightsOffset.Value & 0x02) != 0;

            // ---- Controles ----
            CurrentGearPosition = DecodeGear(_gearOffset.Value);
            CurrentFlapsPercent = DecodeFlaps(_flapsOffset.Value);
            CurrentSpoilersDeployed = _spoilersOffset.Value > 0 || _spoilersArmedOffset.Value > 0;

            // ---- Motores ----
            Engine1Power = DecodeEnginePower(_eng1N1Int.Value, (short)(_engRpmOffset.Value & 0xFFFF));
            Engine2Power = DecodeEnginePower(_eng2N1Int.Value, 0);

            // ---- Autobrake ----
            ReadAutobrake();
            // ---- Flaps ----
            ReadFlaps();
            // ---- Tracking para touchdown ----
            // Guardar VS del último frame aéreo: este es el valor correcto para
            // reportar en el landing (no el de cuando ya estamos frenando en tierra)
            if (_onGroundOffset.Value == 0)
            {
                _lastAirborneVsFpm = CurrentVerticalSpeedFpm;

                // Acumular G-Force peak durante el vuelo (para landing rating)
                if (CurrentGForce > _peakGforceApproach)
                    _peakGforceApproach = CurrentGForce;
            }
        }

        #endregion

        // =====================================================================
        #region Decodificadores
        // =====================================================================

        private void ReadFlaps()
        {
            // Leer valores raw
            FlapsIndex = _flapsHandleIndex.Value;
            FlapsHandleRaw = _flapsHandlePercent.Value;
            FlapsActualRaw = _flapsActualLeft.Value;

            // Detectar si los flaps están en tránsito (diferencia entre handle y posición real)
            FlapsInTransit = Math.Abs(FlapsHandleRaw - FlapsActualRaw) > 200;

            // Calcular porcentaje simple (0-100%)
            FlapsPercent = (FlapsHandleRaw / 16383.0) * 100.0;

            // Decodificar el label según la familia del avión
            var family = DetectFlapsFamily();
            FlapsLabel = DecodeFlapsByFamily(FlapsHandleRaw, family);
        }

        private AircraftFamily DetectFlapsFamily()
        {
            string title = _aircraftTitleOffset.Value;
            if (string.IsNullOrEmpty(title)) return AircraftFamily.Unknown;

            var t = title.ToUpperInvariant();

            // ===== AIRBUS =====
            if (t.Contains("A318") || t.Contains("A319") || t.Contains("A320") ||
                t.Contains("A321") || t.Contains("A330") || t.Contains("A340") ||
                t.Contains("A350") || t.Contains("A380") ||
                t.Contains("FENIX") || t.Contains("TOLISS") || t.Contains("FLYBYWIRE") ||
                t.Contains("FBW"))
                return AircraftFamily.Airbus;

            // ===== BOEING 737 =====
            if ((t.Contains("737") || t.Contains("IFLY")) &&
                (t.Contains("PMDG") || t.Contains("IFLY") || t.Contains("B737") || t.Contains("737-800")))
                return AircraftFamily.Boeing737;

            // ===== BOEING 747 =====
            if (t.Contains("747") || t.Contains("757") || t.Contains("767"))
                return AircraftFamily.Boeing747;

            // ===== BOEING 777 / 787 =====
            if (t.Contains("777") || t.Contains("787"))
                return AircraftFamily.Boeing777;

            // ===== EMBRAER =====
            if (t.Contains("E170") || t.Contains("E175") || t.Contains("E190") || t.Contains("E195") ||
                t.Contains("EMBRAER"))
                return AircraftFamily.Embraer;

            // ===== CESSNA =====
            if (t.Contains("C172") || t.Contains("C182") || t.Contains("C208") ||
                t.Contains("CESSNA"))
                return AircraftFamily.Cessna;

            return AircraftFamily.Unknown;
        }

        private string DecodeFlapsByFamily(int raw, AircraftFamily family)
        {
            // Airbus A318/319/320/321/330/350
            // Detents: 0 | 1+F | 2 | 3 | FULL (5 posiciones)
            if (family == AircraftFamily.Airbus)
            {
                if (raw < 400) return "0";
                if (raw < 4500) return "1+F";
                if (raw < 8600) return "2";
                if (raw < 12700) return "3";
                return "FULL";
            }

            // Boeing 737 (-700/-800/-900/MAX)
            // Detents: 0 | 1 | 2 | 5 | 10 | 15 | 25 | 30 | 40 (9 posiciones)
            if (family == AircraftFamily.Boeing737)
            {
                if (raw < 200) return "0";
                if (raw < 2248) return "1";
                if (raw < 4296) return "2";
                if (raw < 6344) return "5";
                if (raw < 8392) return "10";
                if (raw < 10240) return "15";
                if (raw < 12288) return "25";
                if (raw < 14336) return "30";
                return "40";
            }

            // Boeing 747
            // Detents: 0 | 1 | 5 | 10 | 20 | 25 | 30 (7 posiciones)
            if (family == AircraftFamily.Boeing747)
            {
                if (raw < 300) return "0";
                if (raw < 2730) return "1";
                if (raw < 5460) return "5";
                if (raw < 8190) return "10";
                if (raw < 10920) return "20";
                if (raw < 13650) return "25";
                return "30";
            }

            // Boeing 777/787
            // Detents: 0 | 1 | 5 | 15 | 20 | 25 | 30 (7 posiciones, similar a 747)
            if (family == AircraftFamily.Boeing777)
            {
                if (raw < 300) return "0";
                if (raw < 2730) return "1";
                if (raw < 5460) return "5";
                if (raw < 8190) return "15";
                if (raw < 10920) return "20";
                if (raw < 13650) return "25";
                return "30";
            }

            // Embraer E-Jets
            // Detents: 0 | 1 | 2 | 3 | 4 | 5 | 6 | FULL (aproximado)
            if (family == AircraftFamily.Embraer)
            {
                if (raw < 300) return "0";
                if (raw < 2048) return "1";
                if (raw < 4096) return "2";
                if (raw < 6144) return "3";
                if (raw < 8192) return "4";
                if (raw < 10240) return "5";
                if (raw < 12288) return "6";
                return "FULL";
            }

            // Cessna (C172, etc.)
            // Notches: UP | 10° | 20° | 30° | 40°
            if (family == AircraftFamily.Cessna)
            {
                if (raw < 300) return "UP";
                if (raw < 4000) return "10°";
                if (raw < 8000) return "20°";
                if (raw < 12000) return "30°";
                return "40°";
            }

            // Fallback: porcentaje simple
            return $"{FlapsPercent:F0}%";
        }



        private void ReadAutobrake()
        {
            var family = DetectAircraftFamily();
            AutobrakeMode mode;
            string label;

            switch (family)
            {
                case AircraftFamily.Airbus:
                    (mode, label) = DecodeAirbusAutobrake(_abAirbusOffset.Value);
                    break;
                case AircraftFamily.Boeing737:
                    (mode, label) = DecodeBoeingAutobrake(_abGenericOffset.Value);
                    break;
                default:
                    mode = AutobrakeMode.Unknown;
                    label = "N/A";
                    break;
            }

            AutobrakeMode = mode;
            AutobrakeLabel = label;
        }

        private AircraftFamily DetectAircraftFamily()
        {
            string title = _aircraftTitleOffset.Value;
            if (string.IsNullOrEmpty(title)) return AircraftFamily.Unknown;

            var t = title.ToUpperInvariant();

            // Airbus
            if (t.Contains("A318") || t.Contains("A319") || t.Contains("A320") ||
                t.Contains("A321") || t.Contains("A330") || t.Contains("A340") ||
                t.Contains("A350") || t.Contains("A380") || t.Contains("FENIX") ||
                t.Contains("TOLISS") || t.Contains("FLYBYWIRE") || t.Contains("FBW"))
                return AircraftFamily.Airbus;

            // Boeing
            if (t.Contains("737") || t.Contains("747") || t.Contains("757") ||
                t.Contains("767") || t.Contains("777") || t.Contains("787") ||
                t.Contains("PMDG") || t.Contains("TFDI") || t.Contains("INIBUILDS"))
                return AircraftFamily.Boeing737;

            return AircraftFamily.Unknown;
        }

        private (AutobrakeMode Mode, string Label) DecodeAirbusAutobrake(short val)
        {
            switch (val)
            {
                case 0: return (AutobrakeMode.Off, "OFF");
                case 1: return (AutobrakeMode.Low, "LO");
                case 2: return (AutobrakeMode.Med, "MED");
                case 3: return (AutobrakeMode.Max, "MAX");
                default: return (AutobrakeMode.Unknown, $"RAW={val}");
            }
        }

        private (AutobrakeMode Mode, string Label) DecodeBoeingAutobrake(byte val)
        {
            switch (val)
            {
                case 0: return (AutobrakeMode.Off, "OFF");
                case 1: return (AutobrakeMode.RTO, "RTO");
                case 2: return (AutobrakeMode.Low, "1");
                case 3: return (AutobrakeMode.Med, "2");
                case 4: return (AutobrakeMode.High, "3");
                case 5: return (AutobrakeMode.Max, "MAX");
                default: return (AutobrakeMode.Unknown, $"RAW={val}");
            }
        }


        private static string GetAutobrakeName(byte setting)
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
        private static EnginePower DecodeEnginePower(short n1Raw, short rpmRaw)
        {
            double n1 = n1Raw * 100.0 / 16384.0;
            double rpm = rpmRaw / 4.0;

            if (n1 > 5)
                return new EnginePower { Value = n1, Type = "N1" };
            if (rpm > 800)
                return new EnginePower { Value = rpm, Type = rpm < 1200 ? "PROP RPM" : "PISTON RPM" };

            return new EnginePower { Value = 0, Type = "OFF" };
        }

        /// <summary>
        /// 0x0560 · INT64 · raw = lat × (10001750 × 65536²) / 90
        /// </summary>
        private static double DecodeLatitude(long raw)
            => raw * 90.0 / (10001750.0 * 65536.0 * 65536.0);

        /// <summary>
        /// 0x0568 · INT64 · raw = lon × 2⁶⁴ / 360
        /// </summary>
        private static double DecodeLongitude(long raw)
            => raw * 360.0 / (65536.0 * 65536.0 * 65536.0 * 65536.0);

        /// <summary>
        /// 0x0570 · INT64 · raw = metros × 65536 × 65536 (los 32 bits altos = metros enteros)
        /// Fórmula correcta: metros = raw / 65536 / 65536, luego × 3.28084 para feet.
        /// </summary>
        private static double DecodeAltitude(long raw)
        {
            // Los 32 bits superiores contienen los metros enteros,
            // los 32 inferiores la parte fraccionaria.
            // raw / (65536.0 * 65536.0) = metros con decimales
            double metros = raw / (65536.0 * 65536.0);
            return metros * 3.28084;
        }

        /// <summary>
        /// 0x0580 · UINT32 · raw = hdg × 2³² / 360
        /// </summary>
        private static double DecodeHeading(uint raw)
            => raw * 360.0 / (65536.0 * 65536.0);

        /// <summary>
        /// 0x02B4 · INT32 · raw = m/s × 65536 → kt = raw / 65536 × 1.94384
        /// </summary>
        private static double DecodeGroundSpeed(int raw)
            => (raw / 65536.0) * 1.94384;

        /// <summary>
        /// 0x02C8 · INT32 · raw = m/s × 256
        /// fpm = (raw / 256.0) × 196.85
        /// Positivo = ascenso (FSUIPC usa el mismo signo que la convención aeronáutica).
        /// </summary>
        private static double DecodeVerticalSpeed(int raw)
            => (raw / 256.0) * 196.85;

        /// <summary>
        /// 0x02BC · INT32 · raw = kt × 128
        /// </summary>
        private static double DecodeIndicatedAirspeed(int raw)
            => raw / 128.0;

        /// <summary>
        /// 0x0578 · INT32 · raw = grados × 2³² / 360
        /// Convención FSUIPC: nose-up = negativo → se invierte el signo.
        /// </summary>
        private static double DecodePitch(int raw)
        {
            double pitch = -(raw * 360.0 / 4294967296.0);  // inversión de signo
            return Math.Round(Math.Max(-30.0, Math.Min(30.0, pitch)), 1);
        }

        /// <summary>
        /// 0x057C · INT32 · raw = grados × 2³² / 360
        /// Positivo = bank derecha.
        /// </summary>
        private static double DecodeBank(int raw)
        {
            double bank = raw * 360.0 / 4294967296.0;
            return Math.Round(Math.Max(-90.0, Math.Min(90.0, bank)), 1);
        }

        /// <summary>
        /// 0x0BE8 · INT32 · 0 = subido, 16383 = abajo/anclado
        /// Retorna 1 si supera el 50% del recorrido.
        /// </summary>
        private static int DecodeGear(int raw)
            => raw > 8000 ? 1 : 0;

        /// <summary>
        /// 0x0BDC · INT16 · 0 = arriba, 16383 = fondo.
        /// Mapea a detents Boeing (0,1,2,5,10,15,25,30,40).
        /// </summary>
        private static double DecodeFlaps(short raw)
        {
            double p = raw / 16383.0;
            if (p < 0.02) return 0;
            if (p < 0.13) return 1;
            if (p < 0.26) return 2;
            if (p < 0.22) return 5;
            if (p < 0.32) return 10;
            if (p < 0.45) return 15;
            if (p < 0.62) return 25;
            if (p < 0.78) return 30;
            return 40;
        }

        #endregion

        // =====================================================================
        #region Detección de Eventos
        // =====================================================================

        private void DetectEvents()
        {
            DetectGroundTransition();
            DetectGearChange();
            DetectFlapsChange();
            DetectSpoilersChange();
            DetectParkingBrakeChange();
            DetectEnginesChange();
            DetectBeaconChange();

            int lights = _lightsOffset.Value;
            bool navOn = (lights & 0x01) != 0;
            bool beaconOn = (lights & 0x02) != 0;
            bool strobeOn = (lights & 0x10) != 0 && (beaconOn || navOn);

            DetectNavChange(navOn);
            DetectStrobeChange(strobeOn);
        }

        private void DetectGroundTransition()
        {
            bool isOnGroundNow = _onGroundOffset.Value == 1;

            if (isOnGroundNow)
            {
                _groundConsecutiveCounter++;
                if (_groundConsecutiveCounter >= GROUND_CONFIRM_FRAMES)
                {
                    if (!_lastOnGround) HandleTouchdown();
                    _lastOnGround = true;
                }
            }
            else
            {
                _groundConsecutiveCounter = 0;
                if (_lastOnGround) HandleTakeoff();
                _lastOnGround = false;
            }
        }

        private void DetectGearChange()
        {
            int gear = CurrentGearPosition;
            if (gear == _lastGearPosition) return;
            GearChanged?.Invoke(_lastGearPosition, gear);
            _lastGearPosition = gear;
        }

        private void DetectFlapsChange()
        {
            double flaps = CurrentFlapsPercent;
            double diff = Math.Abs(flaps - _lastFlapsPercent);

            if (diff > FLAPS_HYSTERESIS ||
                (diff > 0 && (DateTime.UtcNow - _lastFlapsChangeTime).TotalMilliseconds > FLAPS_DEBOUNCE_MS))
            {
                if (diff > 0)
                {
                    FlapsChanged?.Invoke(_lastFlapsPercent, flaps);
                    _lastFlapsChangeTime = DateTime.UtcNow;
                }
                _lastFlapsPercent = flaps;
            }
        }

        private void DetectSpoilersChange()
        {
            if (CurrentSpoilersDeployed == _lastSpoilersDeployed) return;
            SpoilersChanged?.Invoke(CurrentSpoilersDeployed);
            _lastSpoilersDeployed = CurrentSpoilersDeployed;
        }

        // ── Parking brake ─────────────────────────────────────────────────────────
        // Umbral al 60% del rango (0–16383) para filtrar vibración de pedales XPlane
        private const int ParkingBrakeThreshold = 9830;  // 16383 * 0.6
        private DateTime _lastParkingBrakeChange = DateTime.MinValue;
        private const double ParkingBrakeDebounceSeconds = 2.0;

        private void DetectParkingBrakeChange()
        {
            // Debounce: ignorar cambios más rápidos que el umbral
            if ((DateTime.UtcNow - _lastParkingBrakeChange).TotalSeconds < ParkingBrakeDebounceSeconds)
                return;

            int raw = _parkingBrakeOffset.Value;
            int parking = raw > ParkingBrakeThreshold ? 1 : 0;
            if (parking == _lastParkingBrake) return;

            _lastParkingBrakeChange = DateTime.UtcNow;
            ParkingBrakeChanged?.Invoke(parking == 1);
            _lastParkingBrake = parking;
        }

        // ── Nav y Strobe: añadir debounce y eventos ────────────────────────────────
        private bool _lastNavState;
        private bool _lastStrobeState;
        private DateTime _lastNavChange = DateTime.MinValue;
        private DateTime _lastStrobeChange = DateTime.MinValue;
        private const double LightDebounceSeconds = 1.5;

        public event Action<bool> NavLightChanged;
        public event Action<bool> StrobeLightChanged;

        private void DetectNavChange(bool navOn)
        {
            if (navOn == _lastNavState) return;
            if ((DateTime.UtcNow - _lastNavChange).TotalSeconds < LightDebounceSeconds) return;
            _lastNavChange = DateTime.UtcNow;
            _lastNavState = navOn;
            NavLightChanged?.Invoke(navOn);
        }

        private void DetectStrobeChange(bool strobeOn)
        {
            if (strobeOn == _lastStrobeState) return;
            if ((DateTime.UtcNow - _lastStrobeChange).TotalSeconds < LightDebounceSeconds) return;
            _lastStrobeChange = DateTime.UtcNow;
            _lastStrobeState = strobeOn;
            StrobeLightChanged?.Invoke(strobeOn);
        }
        private void DetectEnginesChange()
        {
            bool running;
            switch (_currentEngineCategory)
            {
                case AircraftCategory.Piston:
                    running = _eng1RpmF64.Value > 400 || _eng2RpmF64.Value > 400;
                    break;
                case AircraftCategory.Turboprop:
                    running = _eng1N1Offset.Value > 10 || _eng1TorquePctF64.Value > 5 ||
                              _eng2N1Offset.Value > 10 || _eng2TorquePctF64.Value > 5;
                    break;
                default: // Jet / Unknown
                    running = _eng1N1Offset.Value > 15 || _eng2N1Offset.Value > 15;
                    break;
            }
            if (running == (_lastEngineRpm > 0)) return;
            EnginesChanged?.Invoke(running);
            _lastEngineRpm = running ? 1 : 0;
        }

        private void DetectBeaconChange()
        {
            if (IsBeaconOn == _lastBeaconState) return;
            BeaconChanged?.Invoke(IsBeaconOn);
            _lastBeaconState = IsBeaconOn;
        }

        #endregion

        // =====================================================================
        #region Liftoff y Touchdown
        // =====================================================================

        /// <summary>
        /// Snapshot en el momento del liftoff (OnGround 1→0).
        /// La GS y el IAS aquí son los del primer frame en el aire.
        /// </summary>
        private void HandleTakeoff()
        {
            if ((DateTime.UtcNow - _lastTakeoffTime).TotalSeconds < EVENT_DEBOUNCE_SECONDS) return;
            if (CurrentGroundSpeedKt < TAKEOFF_MIN_SPEED_KT) return;

            _lastTakeoffTime = DateTime.UtcNow;
            _peakGforceApproach = 1.0; // reset para este vuelo

            TakeoffDetected?.Invoke(this, new TakeoffData
            {
                Timestamp = DateTime.UtcNow,
                LatitudeDeg = CurrentLatitude,
                LongitudeDeg = CurrentLongitude,
                AltitudeMeters = CurrentAltitudeFeet / 3.28084,
                RotationIasKt = CurrentIndicatedAirspeed,
                GroundSpeedKt = CurrentGroundSpeedKt,
                HeadingDeg = CurrentHeading,
                PitchDeg = CurrentPitch,
                BankDeg = CurrentBank,
                FlapsPosition = CurrentFlapsPercent / 100.0,

                Eng1N1Pct = Engine1Power.GetN1(),
                Eng2N1Pct = Engine2Power.GetN1(),
                Eng1Rpm = Engine1Power.GetRpm(),
                Eng2Rpm = Engine2Power.GetRpm(),
                EngineType = Engine1Power.Type,

                // Meteorología ya convertida
                OatCelsius = CurrentOatCelsius,
                WindSpeedKt = CurrentWindSpeedKt,
                WindDirDeg = CurrentWindDirDeg   // ← grados reales, no raw
            });
        }

        /// <summary>
        /// Snapshot en el momento del touchdown (OnGround 0→1).
        /// Usa <see cref="_lastAirborneVsFpm"/> para el VS real de contacto y
        /// <see cref="_peakGforceApproach"/> para el G-Force pico del approach.
        /// </summary>
        private void HandleTouchdown()
        {
            if ((DateTime.UtcNow - _lastTouchdownTime).TotalSeconds < EVENT_DEBOUNCE_SECONDS) return;
            if (CurrentGroundSpeedKt < TOUCHDOWN_MIN_SPEED_KT) return;

            // Usar el VS del último frame aéreo, no el del frame en tierra
            // (en tierra el VS puede ya ser 0 o cercano a 0)
            if (_lastAirborneVsFpm > TOUCHDOWN_MIN_VS_FPM) return;

            _lastTouchdownTime = DateTime.UtcNow;

            TouchdownDetected?.Invoke(this, new TouchdownData
            {
                Timestamp = DateTime.UtcNow,
                LatitudeDeg = CurrentLatitude,
                LongitudeDeg = CurrentLongitude,
                AltitudeMeters = CurrentAltitudeFeet / 3.28084,

                // ← VS del ÚLTIMO frame aéreo (no del frame en tierra)
                VerticalSpeedFpm = _lastAirborneVsFpm,

                GroundSpeedKt = CurrentGroundSpeedKt,
                IasKt = CurrentIndicatedAirspeed,
                HeadingDeg = CurrentHeading,
                PitchDeg = CurrentPitch,
                BankDeg = CurrentBank,

                // ← G pico del approach completo
                GForcePeak = _peakGforceApproach,
                GForceAtTouch = CurrentGForce,

                FlapsPosition = CurrentFlapsPercent / 100.0,
                SpoilersPosition = CurrentSpoilersDeployed ? 1.0 : 0.0,
                GearPosition = CurrentGearPosition,

                // N1 del FLOAT64 offset (más preciso para jets)
                Eng1N1Pct = _eng1N1Offset.Value * 100.0,
                Eng2N1Pct = _eng2N1Offset.Value * 100.0,
                Eng1ReverserPct = _eng1ReverserOffset.Value * 100.0,
                Eng2ReverserPct = _eng2ReverserOffset.Value * 100.0,

                BrakeLeft = _brakeLeftOffset.Value / 32767.0,
                BrakeRight = _brakeRightOffset.Value / 32767.0,


                // Meteorología convertida
                OatCelsius = CurrentOatCelsius,
                WindSpeedKt = CurrentWindSpeedKt,
                WindDirDeg = CurrentWindDirDeg  // ← grados reales
            });

            // Reset peak para el siguiente ciclo
            _peakGforceApproach = 1.0;
        }

        #endregion

        // =====================================================================
        #region Telemetría Periódica
        // =====================================================================

        private void SendTelemetry()
        {
            if (DateTime.UtcNow - _lastTelemetrySend <
                TimeSpan.FromSeconds(_currentPhaseInterval)) return;

            _lastTelemetrySend = DateTime.UtcNow;
            _positionOrder++;

            TelemetryUpdated?.Invoke(this, new TelemetryData
            {
                Latitude = CurrentLatitude,
                Longitude = CurrentLongitude,
                AltitudeFeet = CurrentAltitudeFeet,
                RadarAltitudeFeet = CurrentRadarAltitudeFeet,
                GroundSpeedKt = CurrentGroundSpeedKt,
                HeadingDeg = CurrentHeading,
                VerticalSpeedFpm = CurrentVerticalSpeedFpm,
                IndicatedAirspeedKt = CurrentIndicatedAirspeed,
                IsOnGround = _lastOnGround,
                FuelLbs = CurrentFuelLbs,
                Transponder = _transponderOffset.Value,
                AutopilotEngaged = (_autopilotOffset.Value & 0x01) != 0,
                Order = _positionOrder,
                PitchDeg = CurrentPitch,
                BankDeg = CurrentBank,
                SpoilersDeployed = CurrentSpoilersDeployed,
                FlapsPercent = CurrentFlapsPercent,
                GearDown = CurrentGearPosition == 1,
                Wind = BuildWindString(),
                NavType = DetermineNavType()
            });
        }

        private int DetermineNavType()
        {
            if ((_autopilotOffset.Value & 0x01) == 0) return 0;
            switch (_navModeOffset.Value)
            {
                case 1: return 1;
                case 2: return 3;
                case 3: return 1;
                default: return 0;
            }
        }

        /// <summary>
        /// "270° at 15 kts" o "Calm".
        /// Usa las propiedades ya convertidas (no el raw del offset).
        /// </summary>
        private string BuildWindString()
        {
            if (CurrentWindDirDeg == 0 && CurrentWindSpeedKt == 0) return "Calm";
            return string.Format(CultureInfo.InvariantCulture,
                "{0:000}° at {1} kts",
                (int)Math.Round(CurrentWindDirDeg),
                CurrentWindSpeedKt);
        }

        #endregion

        // =====================================================================
        #region Conexión y Reconexión
        // =====================================================================

        private void TryConnect()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            try
            {
                FSUIPCConnection.Open();

                _connectionState = ConnectionState.Connected;
                SimulatorName = DetectSimulator();
                _aircraftInfoRead = false;
                _connectionRetryCount = 0;
                _currentBackoffMs = 1000;
                _isReconnecting = false;
                _lastTelemetrySend = DateTime.MinValue;

                Debug.WriteLine($"FsuipcService: connected to {SimulatorName}");
                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _connectionState = ConnectionState.Disconnected;
                _isReconnecting = false;
                _connectionRetryCount++;

                _currentBackoffMs = Math.Min(
                    30000,
                    1000 * (int)Math.Pow(2, Math.Min(5, _connectionRetryCount)));

                Debug.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "FsuipcService: connection failed ({0}). Retry in {1} ms",
                    ex.Message, _currentBackoffMs));

                _pollingTimer?.Change(_currentBackoffMs, _pollingIntervalMs);
            }
        }

        private void Disconnect()
        {
            try { FSUIPCConnection.Close(); } catch { }
            _connectionState = ConnectionState.Disconnected;
        }

        private static string DetectSimulator()
        {
            if (System.Diagnostics.Process.GetProcessesByName("FlightSimulator2024").Length > 0) return "MSFS 2024";
            if (System.Diagnostics.Process.GetProcessesByName("FlightSimulator").Length > 0) return "MSFS 2020";
            if (System.Diagnostics.Process.GetProcessesByName("X-Plane").Length > 0) return "X-Plane";
            if (System.Diagnostics.Process.GetProcessesByName("Prepar3D").Length > 0) return "Prepar3D";
            return FSUIPCConnection.FlightSimVersionConnected.ToString();
        }

        #endregion

        // =====================================================================
        #region Identificación de Aeronave
        // =====================================================================

        private void ReadAircraftInfo()
        {
            if (!IsConnected) return;
            try
            {
                string title = _aircraftTitle.Value;
                if (!string.IsNullOrWhiteSpace(title) && title != "\0")
                {
                    AircraftTitle = title.Trim();
                    Debug.WriteLine($"FsuipcService: aircraft title = '{AircraftTitle}'");
                }

                TryReadStringOffset(() => _icaoDesignator.Value,
                    val => { if (val != "????") AircraftIcao = val; });
                TryReadStringOffset(() => _icaoManufacturer.Value,
                    val => AircraftManufacturer = val);
                TryReadStringOffset(() => _icaoModel.Value,
                    val => AircraftModel = val);

                if (AircraftIcao == "????" && !string.IsNullOrEmpty(AircraftTitle))
                    AircraftIcao = ExtractIcaoFromTitle(AircraftTitle);

                OnAircraftInfoReady?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FsuipcService: error reading aircraft info — {ex.Message}");
            }
        }

        private static void TryReadStringOffset(Func<string> reader, Action<string> setter)
        {
            try
            {
                string val = reader()?.Trim();
                if (!string.IsNullOrWhiteSpace(val) && val != "\0")
                    setter(val);
            }
            catch { }
        }

        private static string ExtractIcaoFromTitle(string title)
        {
            var patterns = new Dictionary<string, string>
            {
                { "737-800", "B738" }, { "B738",  "B738" },
                { "B737",    "B737" }, { "737-",  "B737" },
                { "A321",    "A321" }, { "A320",  "A320" },
                { "A319",    "A319" }, { "A318",  "A318" },
                { "B747",    "B747" }, { "747-",  "B747" },
                { "B777",    "B777" }, { "777-",  "B777" },
                { "B787",    "B787" }, { "787-",  "B787" },
                { "C172",    "C172" }, { "172",   "C172" },
                { "C152",    "C152" }, { "C150",  "C150" },
                { "BE58",    "BE58" }, { "B58",   "BE58" }, { "Baron", "BE58" },
                { "A350",    "A350" }, { "A340",  "A340" }, { "A330",  "A330" },
                { "E195",    "E195" }, { "E190",  "E190" },
                { "E175",    "E175" }, { "E170",  "E170" },
                { "CRJX",    "CRJX" }, { "CRJ9",  "CRJ9" }, { "CRJ7", "CRJ7" },
                { "Q400",    "DH8D" }, { "DH8D",  "DH8D" },
                { "AT76",    "AT76" }, { "ATR",   "AT76" }
            };

            foreach (var kv in patterns)
                if (title.Contains(kv.Key)) return kv.Value;

            return "????";
        }

        #endregion

        // =====================================================================
        #region Clasificación de Aeronave
        // =====================================================================

        public enum AircraftCategory { Unknown, Jet, Turboprop, Piston, Helicopter }

        /// <summary>
        /// Detecta la categoría de planta motriz por el título del avión en el simulador.
        /// Se llama en cada ciclo de EmitRawData para actualizar _currentEngineCategory.
        /// Tiene prioridad sobre GetAircraftCategory() (basado en ICAO) porque el título
        /// del simulador es más específico para modelos addon.
        /// </summary>
        private AircraftCategory DetectEngineCategoryFromTitle()
        {
            string title = _aircraftTitleOffset.Value?.ToUpperInvariant() ?? "";
            if (string.IsNullOrEmpty(title)) return AircraftCategory.Unknown;

            // Pistón
            if (title.Contains("BARON") || title.Contains("BE58") || title.Contains("BE55") ||
                title.Contains("BONANZA") || title.Contains("BE36") || title.Contains("BE33") ||
                title.Contains("CESSNA 172") || title.Contains("C172") ||
                title.Contains("CESSNA 182") || title.Contains("C182") ||
                title.Contains("PIPER") || title.Contains("PA28") || title.Contains("PA34") ||
                title.Contains("CESSNA 206") || title.Contains("C206") ||
                title.Contains("SEMINOLE") || title.Contains("PA44"))
                return AircraftCategory.Piston;

            // Turboprop
            if (title.Contains("ATR") || title.Contains("DASH 8") || title.Contains("DHC") ||
                title.Contains("KING AIR") || title.Contains("PC-12") || title.Contains("PC12") ||
                title.Contains("SAAB") || title.Contains("EMB 120") || title.Contains("CARAVAN") ||
                title.Contains("TWIN OTTER") || title.Contains("C208") ||
                title.Contains("Q400") || title.Contains("DASH8") || title.Contains("DASH-8") ||
                title.Contains("SF340") || title.Contains("SAAB 340"))
                return AircraftCategory.Turboprop;

            // Jet — comercial y GA jet
            if (title.Contains("737") || title.Contains("747") || title.Contains("757") ||
                title.Contains("767") || title.Contains("777") || title.Contains("787") ||
                title.Contains("A318") || title.Contains("A319") || title.Contains("A320") ||
                title.Contains("A321") || title.Contains("A330") || title.Contains("A340") ||
                title.Contains("A350") || title.Contains("A380") ||
                title.Contains("CRJ") || title.Contains("E170") || title.Contains("E175") ||
                title.Contains("E190") || title.Contains("E195") || title.Contains("ERJ") ||
                title.Contains("PMDG") || title.Contains("FENIX") || title.Contains("TOLISS") ||
                title.Contains("FLYBYWIRE") || title.Contains("FBW") || title.Contains("IFLY") ||
                title.Contains("CITATION") || title.Contains("GULFSTREAM") || title.Contains("LEARJET"))
                return AircraftCategory.Jet;

            return AircraftCategory.Unknown;
        }

        public AircraftCategory GetAircraftCategory()
        {
            if (string.IsNullOrEmpty(AircraftIcao)) return AircraftCategory.Unknown;
            string icao = AircraftIcao.ToUpperInvariant();

            string[] jets = { "B73", "B74", "B75", "B76", "B77", "B78", "B79", "A3", "A4", "A5", "A6", "A7", "A8", "A9", "E17", "E19", "E45", "E70", "E75", "CRJ", "C700", "C750", "GLF", "F2TH", "DA62" };
            string[] turboprops = { "AT43", "AT45", "AT72", "DH8A", "DH8B", "DH8C", "DH8D", "E120", "F50", "JS32", "JS41", "SB20", "SW4" };
            string[] pistons = { "C172", "C182", "C206", "C208", "PA28", "PA32", "PA34", "BE33", "BE35", "BE36", "BE55", "BE58", "P28A", "P28B", "P28R" };

            foreach (var p in jets) if (icao.Contains(p)) return AircraftCategory.Jet;
            foreach (var p in turboprops) if (icao.Contains(p)) return AircraftCategory.Turboprop;
            foreach (var p in pistons) if (icao.Contains(p)) return AircraftCategory.Piston;

            return AircraftCategory.Unknown;
        }

        public string GetAircraftLivery()
        {
            if (string.IsNullOrEmpty(AircraftTitle)) return "Unknown";

            string[] airlines = {
                "United","American","Delta","Iberia","Lufthansa",
                "British","Air France","KLM","Emirates","Qatar",
                "Avianca","LATAM","Viva","EasyJet","Ryanair",
                "Southwest","JetBlue","Spirit","Frontier","Alaska",
                "Copa","Aeromexico","Air Canada","WestJet",
                "Virgin","Etihad","Turkish","Singapore","Cathay","VHR"
            };

            foreach (var a in airlines)
                if (AircraftTitle.Contains(a)) return a;

            string[] knownIcaos = { "B38M", "B738", "A320", "A319", "A321", "B737", "B747", "B777", "B787" };
            var parts = AircraftTitle.Split(new[] { ' ', '-', '_', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                if (part.Length < 3 || part.Length > 4) continue;
                if (part != part.ToUpperInvariant()) continue;
                if (Array.IndexOf(knownIcaos, part) >= 0) continue;
                return part;
            }

            return "Unknown";
        }

        #endregion
    }

    // =========================================================================
    #region Clases de Datos
    // =========================================================================

    public class TelemetryData : EventArgs
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double AltitudeFeet { get; set; }
        public double RadarAltitudeFeet { get; set; }
        public double GroundSpeedKt { get; set; }
        public double HeadingDeg { get; set; }
        public double VerticalSpeedFpm { get; set; }
        public double IndicatedAirspeedKt { get; set; }
        public bool IsOnGround { get; set; }
        public double FuelLbs { get; set; }
        public double PitchDeg { get; set; }
        public double BankDeg { get; set; }
        public int Transponder { get; set; }
        public bool AutopilotEngaged { get; set; }
        public int NavType { get; set; }
        public int Order { get; set; }
        public bool SpoilersDeployed { get; set; }
        public double FlapsPercent { get; set; }
        public bool GearDown { get; set; }
        public string Wind { get; set; }
    }

    public class RawTelemetryData : EventArgs
    {
        // Propiedades existentes
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double AltitudeFeet { get; set; }
        public double GroundSpeedKt { get; set; }
        public double HeadingDeg { get; set; }
        public double VerticalSpeedFpm { get; set; }
        public double IndicatedAirspeedKt { get; set; }
        public bool IsOnGround { get; set; }
        public double FuelLbs { get; set; }
        public int Transponder { get; set; }
        public bool AutopilotEngaged { get; set; }
        public double RadarAltitudeFeet { get; set; }
        public double PitchDeg { get; set; }
        public double BankDeg { get; set; }
        public bool SpoilersDeployed { get; set; }
        public double FlapsPercent { get; set; }
        public string FlapsLabel { get; set; } = "UP";
        public bool FlapsInTransit { get; set; }
        public bool GearDown { get; set; }
        public int Order { get; set; }

        // Frenos y motores
        public bool ParkingBrakeOn { get; set; }
        public bool EnginesRunning { get; set; }
        public string AutobrakeSetting { get; set; } = "RTO";

        // Luces
        public bool NavLightOn { get; set; }
        public bool BeaconLightOn { get; set; }
        public bool LandingLightOn { get; set; }
        public bool TaxiLightOn { get; set; }
        public bool StrobeLightOn { get; set; }

        // Motores Jet (N1)
        public float N1_1 { get; set; }
        public float N1_2 { get; set; }

        // Identificación de planta motriz
        public FsuipcService.AircraftCategory EngineCategory { get; set; }
        public bool Eng1Running { get; set; }
        public bool Eng2Running { get; set; }
        public bool TakeoffPowerSet { get; set; }

        // Turboprop
        public float TorquePct_1 { get; set; }
        public float TorquePct_2 { get; set; }
        public float PropRpm_1 { get; set; }
        public float PropRpm_2 { get; set; }

        // Piston
        public float Rpm_1 { get; set; }
        public float Rpm_2 { get; set; }
        public float Map_1 { get; set; }    // Manifold Absolute Pressure (inHg)
        public float Map_2 { get; set; }
        public float Cht_1 { get; set; }    // Cylinder Head Temp (°C)
        public float Cht_2 { get; set; }
        public float OilTemp_1 { get; set; }
        public float OilTemp_2 { get; set; }
        public float OilPress_1 { get; set; }
        public float OilPress_2 { get; set; }
        public float Throttle_1 { get; set; }
        public float Throttle_2 { get; set; }
        // Seat Belt sign y AP
        public bool SeatBeltSign { get; set; }
        /// <summary>QNH seleccionado en el altímetro del avión, en hPa. 0 si FSUIPC no conectado.</summary>
        public double AircraftQnhMb { get; set; }
        public bool ApMaster { get; set; }
        public string ApNavMode { get; set; } = "HDG";  // ILS / LOC / LNAV / HDG
        public string ApVertMode { get; set; } = "ALT";  // GS  / VNAV / ALT
    }

    public enum ConnectionState { Disconnected, Connected }

    public class EnginePower
    {
        public double Value { get; set; }
        public string Type { get; set; }
        public string Display => $"{Value:F0} {Type}";
        public double GetN1() => Type == "N1" ? Value : 0;
        public double GetRpm() => (Type == "PROP RPM" || Type == "PISTON RPM") ? Value : 0;
    }

    #endregion
}