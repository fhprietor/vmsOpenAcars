// =============================================================================
// vmsOpenAcars/Services/FsuipcService.cs
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
        private readonly Offset<short> _radarAltitudeOffset = new Offset<short>(0x31E4);
        private readonly Offset<int> _groundAltitudeOffset = new Offset<int>(0x0020);

        // ---- Estado de vuelo ----
        private readonly Offset<short> _onGroundOffset = new Offset<short>(0x0366);
        private readonly Offset<int> _simTimeOffset = new Offset<int>(0x023A);

        // ---- Combustible ----
        private readonly Offset<int> _fuelWeightOffset = new Offset<int>(0x126C);
        private readonly Offset<int> _fuelFlowOffset = new Offset<int>(0x0B54);

        // ---- Motores (FLOAT64, los más precisos para jets/turboprops) ----
        /// <summary>0x2000 · FLOAT64 · N1 motor 1 en % (0.0–100.0)</summary>
        private readonly Offset<double> _eng1N1Offset = new Offset<double>(0x2000);
        /// <summary>0x2100 · FLOAT64 · N1 motor 2 en % (0.0–100.0)</summary>
        private readonly Offset<double> _eng2N1Offset = new Offset<double>(0x2100);
        /// <summary>0x207C · FLOAT64 · reversor motor 1 (0.0–1.0)</summary>
        private readonly Offset<double> _eng1ReverserOffset = new Offset<double>(0x207C);
        /// <summary>0x217C · FLOAT64 · reversor motor 2 (0.0–1.0)</summary>
        private readonly Offset<double> _eng2ReverserOffset = new Offset<double>(0x217C);

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

        // ---- Autopiloto / Nav ----
        private readonly Offset<short> _autopilotOffset = new Offset<short>(0x07BC);
        private readonly Offset<short> _navModeOffset = new Offset<short>(0x07CC);
        private readonly Offset<short> _transponderOffset = new Offset<short>(0x0354);

        // ---- Frenos ----
        private readonly Offset<short> _parkingBrakeOffset = new Offset<short>(0x0BC8);
        private readonly Offset<short> _brakeLeftOffset = new Offset<short>(0x0BC4);
        private readonly Offset<short> _brakeRightOffset = new Offset<short>(0x0BC6);
        private readonly Offset<byte> _autobrakeOffset = new Offset<byte>(0x2F80);

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
        public double CurrentFuelKg => CurrentFuelLbs * 0.453592;
        public double CurrentFuelFlowLbsHr { get; private set; }

        // ---- Controles ----
        public int CurrentGearPosition { get; private set; }
        public double CurrentFlapsPercent { get; private set; }
        public bool CurrentSpoilersDeployed { get; private set; }

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

        // ---- Motores ----
        public double CurrentN1 => _eng1N1Offset.Value;
        public int CurrentEngineRpm => _engRpmOffset.Value;
        public EnginePower Engine1Power { get; private set; }
        public EnginePower Engine2Power { get; private set; }

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
            RawDataUpdated?.Invoke(this, new RawTelemetryData
            {
                Latitude = CurrentLatitude,
                Longitude = CurrentLongitude,
                AltitudeFeet = CurrentAltitudeFeet,
                GroundSpeedKt = CurrentGroundSpeedKt,
                HeadingDeg = CurrentHeading,
                VerticalSpeedFpm = CurrentVerticalSpeedFpm,
                IndicatedAirspeedKt = CurrentIndicatedAirspeed,
                IsOnGround = _lastOnGround,
                FuelLbs = CurrentFuelLbs,
                FuelFlow = CurrentFuelFlowLbsHr,
                Transponder = _transponderOffset.Value,
                AutopilotEngaged = _autopilotOffset.Value == 1,
                RadarAltitudeFeet = CurrentRadarAltitudeFeet,
                PitchDeg = CurrentPitch,
                BankDeg = CurrentBank,
                SpoilersDeployed = CurrentSpoilersDeployed,
                FlapsPercent = CurrentFlapsPercent,
                GearDown = CurrentGearPosition == 1,
                Order = _positionOrder
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
            CurrentFuelFlowLbsHr = _fuelFlowOffset.Value / 128.0;

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
            if (p < 0.08) return 1;
            if (p < 0.14) return 2;
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

        private void DetectParkingBrakeChange()
        {
            int parking = _parkingBrakeOffset.Value != 0 ? 1 : 0;
            if (parking == _lastParkingBrake) return;
            ParkingBrakeChanged?.Invoke(parking == 1);
            _lastParkingBrake = parking;
        }

        private void DetectEnginesChange()
        {
            double n1 = Math.Max(_eng1N1Offset.Value, _eng2N1Offset.Value);
            int rpm = _engRpmOffset.Value;
            bool running = n1 > 0.05 || rpm > 800;
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
                AutobrakeSetting = _autobrakeOffset.Value,

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
                FuelFlow = CurrentFuelFlowLbsHr,
                Transponder = _transponderOffset.Value,
                AutopilotEngaged = _autopilotOffset.Value == 1,
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
            if (_autopilotOffset.Value != 1) return 0;
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
        public double FuelFlow { get; set; }
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
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double AltitudeFeet { get; set; }
        public double GroundSpeedKt { get; set; }
        public double HeadingDeg { get; set; }
        public double VerticalSpeedFpm { get; set; }
        public double IndicatedAirspeedKt { get; set; }
        public bool IsOnGround { get; set; }
        public double FuelLbs { get; set; }
        public double FuelFlow { get; set; }
        public int Transponder { get; set; }
        public bool AutopilotEngaged { get; set; }
        public double RadarAltitudeFeet { get; set; }
        public double PitchDeg { get; set; }
        public double BankDeg { get; set; }
        public bool SpoilersDeployed { get; set; }
        public double FlapsPercent { get; set; }
        public bool GearDown { get; set; }
        public int Order { get; set; }
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