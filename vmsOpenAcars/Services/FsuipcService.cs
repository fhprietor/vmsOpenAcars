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
//   Fechas / horas         : UTC  (DateTime.UtcNow, invariante de zona horaria)
//   Cultura de formato     : CultureInfo.InvariantCulture (independiente del PC)
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
    /// <summary>
    /// Servicio de integración con FSUIPC/WASM.
    /// Gestiona conexión, polling, decodificación de offsets y emisión de eventos
    /// de telemetría, despegue, aterrizaje y cambios en sistemas de la aeronave.
    /// </summary>
    public class FsuipcService : IDisposable
    {
        // =====================================================================
        #region Offsets FSUIPC
        // Cada campo declara el offset, el tipo nativo de FSUIPC y la fórmula de
        // conversión que se aplica en la región "Decodificadores".
        // =====================================================================

        // ------------------------------------------------------------------
        // Posición geográfica
        // ------------------------------------------------------------------

        /// <summary>0x0560 · 8 bytes signed · raw = lat × (10001750 × 65536²) / 90</summary>
        private readonly Offset<long> _latOffset = new Offset<long>(0x0560);

        /// <summary>0x0568 · 8 bytes signed · raw = lon × 2⁶⁴ / 360 (rango completo)</summary>
        private readonly Offset<long> _lonOffset = new Offset<long>(0x0568);

        /// <summary>0x0570 · 8 bytes signed · raw = metros × 65536 (altitud MSL)</summary>
        private readonly Offset<long> _altOffset = new Offset<long>(0x0570);

        /// <summary>
        /// 0x3324 · 8 bytes double · altitud MSL directamente en feet.
        /// Disponible en MSFS 2020/2024 y P3D v5+. Se prioriza sobre 0x0570.
        /// </summary>
        private readonly Offset<double> _altitudeFeetOffset = new Offset<double>(0x3324);

        /// <summary>0x0580 · 4 bytes unsigned · raw = hdg × 2³² / 360</summary>
        private readonly Offset<uint> _headingOffset = new Offset<uint>(0x0580);

        // ------------------------------------------------------------------
        // Velocidades
        // ------------------------------------------------------------------

        /// <summary>0x02B4 · 4 bytes signed · raw = m/s × 65536</summary>
        private readonly Offset<int> _groundSpeedOffset = new Offset<int>(0x02B4);

        /// <summary>0x02C8 · 4 bytes signed · raw = m/s × 256</summary>
        private readonly Offset<int> _verticalSpeedOffset = new Offset<int>(0x02C8);

        /// <summary>0x02BC · 4 bytes signed · raw = kt × 128</summary>
        private readonly Offset<int> _iasOffset = new Offset<int>(0x02BC);

        // ------------------------------------------------------------------
        // Actitud
        // ------------------------------------------------------------------

        /// <summary>
        /// 0x0578 · 4 bytes signed
        /// Convención FSUIPC: nose-UP = valor NEGATIVO → el decoder invierte el signo.
        /// </summary>
        private Offset<int> _pitchOffset = new Offset<int>(0x0578);

        /// <summary>
        /// 0x057C · 2 bytes signed · rango −16384 = −90° (izquierda) .. +16384 = +90° (derecha).
        /// </summary>
        private Offset<int> _bankOffset = new Offset<int>(0x057C);

        // ------------------------------------------------------------------
        // Altimetría de radio / terreno
        // ------------------------------------------------------------------

        /// <summary>
        /// 0x31E4 · 2 bytes signed · AGL en METROS enteros (sin escala).
        /// Se convierte a feet en ReadAllOffsets(). Puede ser 0 si el sim no lo popula.
        /// </summary>
        private readonly Offset<short> _radarAltitudeOffset = new Offset<short>(0x31E4);

        /// <summary>0x0020 · 4 bytes signed · terreno MSL en metros × 256</summary>
        private readonly Offset<int> _groundAltitudeOffset = new Offset<int>(0x0020);

        // ------------------------------------------------------------------
        // Estado de vuelo
        // ------------------------------------------------------------------

        /// <summary>0x0366 · 2 bytes · 1 = en tierra, 0 = en el aire</summary>
        private readonly Offset<short> _onGroundOffset = new Offset<short>(0x0366);

        /// <summary>0x023A · 4 bytes signed · segundos desde medianoche Zulu</summary>
        private readonly Offset<int> _simTimeOffset = new Offset<int>(0x023A);

        // ------------------------------------------------------------------
        // Combustible
        // ------------------------------------------------------------------

        /// <summary>0x126C · 4 bytes signed · raw = lbs_totales × 128 (todos los tanques)</summary>
        private readonly Offset<int> _fuelWeightOffset = new Offset<int>(0x126C);

        /// <summary>0x0B54 · 4 bytes signed · raw = lbs/hr × 128 (motor 1 únicamente)</summary>
        private readonly Offset<int> _fuelFlowOffset = new Offset<int>(0x0B54);

        // ------------------------------------------------------------------
        // Motores
        // ------------------------------------------------------------------

        /// <summary>0x2000 · 8 bytes double · N1 motor 1 como fracción 0.0 .. 1.0</summary>
        private readonly Offset<double> _eng1N1Offset = new Offset<double>(0x2000);

        /// <summary>0x2100 · 8 bytes double · N1 motor 2 como fracción 0.0 .. 1.0</summary>
        private readonly Offset<double> _eng2N1Offset = new Offset<double>(0x2100);

        /// <summary>0x207C · 8 bytes double · apertura reversores motor 1 (0.0 .. 1.0)</summary>
        private readonly Offset<double> _eng1ReverserOffset = new Offset<double>(0x207C);

        /// <summary>0x217C · 8 bytes double · apertura reversores motor 2 (0.0 .. 1.0)</summary>
        private readonly Offset<double> _eng2ReverserOffset = new Offset<double>(0x217C);

        /// <summary>0x0898 · 4 bytes signed · RPM del motor 1, valor directo sin escala</summary>
        private readonly Offset<int> _engRpmOffset = new Offset<int>(0x0898);

        // ===== MOTORES - OFFSETS UNIVERSALES =====

        // N1 para jets (int16) - offsets correctos
        private Offset<short> _eng1N1 = new Offset<short>(0x0898);
        private Offset<short> _eng2N1 = new Offset<short>(0x0930);

        // RPM para pistón/turboprop (int16)
        private Offset<short> _eng1Rpm = new Offset<short>(0x0890);
        private Offset<short> _eng2Rpm = new Offset<short>(0x0928);

        // ------------------------------------------------------------------
        // Controles de vuelo
        // ------------------------------------------------------------------

        /// <summary>0x0BE8 · 4 bytes · posición del tren 0 (subido) .. 16383 (abajo)</summary>
        private readonly Offset<int> _gearOffset = new Offset<int>(0x0BE8);

        /// <summary>0x0BDC · 2 bytes signed · palanca de flaps 0 (arriba) .. 16383 (fondo)</summary>
        private readonly Offset<short> _flapsOffset = new Offset<short>(0x0BDC);

        /// <summary>0x0BD8 · 2 bytes signed · spoilers extendidos (0 = retractados)</summary>
        private readonly Offset<short> _spoilersOffset = new Offset<short>(0x0BD8);

        /// <summary>0x0BCC · 2 bytes signed · spoilers armados</summary>
        private readonly Offset<short> _spoilersArmedOffset = new Offset<short>(0x0BCC);

        // ------------------------------------------------------------------
        // Autopiloto / navegación
        // ------------------------------------------------------------------

        /// <summary>0x07BC · 2 bytes · autopiloto maestro: 1 = enganchado</summary>
        private readonly Offset<short> _autopilotOffset = new Offset<short>(0x07BC);

        /// <summary>0x07CC · 2 bytes · modo de navegación activo</summary>
        private readonly Offset<short> _navModeOffset = new Offset<short>(0x07CC);

        /// <summary>0x0354 · 2 bytes signed · código transponder en BCD (valor directo)</summary>
        private readonly Offset<short> _transponderOffset = new Offset<short>(0x0354);

        // ------------------------------------------------------------------
        // Frenos
        // ------------------------------------------------------------------

        /// <summary>
        /// 0x0BC8 · 2 bytes signed · freno de estacionamiento: 0 = libre, 32767 = puesto.
        /// NOTA: 0x0B88 y 0x0B8C son frenos de rueda hidráulicos, NO parking brake;
        /// se excluyeron deliberadamente para evitar falsos positivos durante el frenado.
        /// </summary>
        private readonly Offset<short> _parkingBrakeOffset = new Offset<short>(0x0BC8);

        /// <summary>0x0BC4 · 2 bytes signed · freno rueda izquierda 0 .. 32767</summary>
        private readonly Offset<short> _brakeLeftOffset = new Offset<short>(0x0BC4);

        /// <summary>0x0BC6 · 2 bytes signed · freno rueda derecha 0 .. 32767</summary>
        private readonly Offset<short> _brakeRightOffset = new Offset<short>(0x0BC6);

        /// <summary>0x2F80 · 1 byte · autobrake: 0=RTO, 1=OFF, 2=1, 3=2, 4=3, 5=MAX</summary>
        private readonly Offset<byte> _autobrakeOffset = new Offset<byte>(0x2F80);

        // ------------------------------------------------------------------
        // Meteorología
        // ------------------------------------------------------------------

        /// <summary>0x0E92 · 2 bytes signed · dirección del viento en grados (valor directo)</summary>
        private readonly Offset<short> _windDirOffset = new Offset<short>(0x0E92);

        /// <summary>0x0E90 · 2 bytes signed · velocidad del viento en knots (valor directo)</summary>
        private readonly Offset<short> _windSpeedOffset = new Offset<short>(0x0E90);

        /// <summary>0x0E8C · 2 bytes signed · OAT = °C × 256</summary>
        private readonly Offset<short> _oatOffset = new Offset<short>(0x0E8C);

        // ------------------------------------------------------------------
        // Acelerometría
        // ------------------------------------------------------------------

        /// <summary>0x11BA · 2 bytes signed · G = raw / 625.0</summary>
        private readonly Offset<short> _gforceOffset = new Offset<short>(0x11BA);

        // ------------------------------------------------------------------
        // Luces (bitmap 0x0D0C)
        //   bit 0  0x01  Navigation lights
        //   bit 1  0x02  Beacon            ← IsBeaconOn
        //   bit 2  0x04  Landing lights
        //   bit 3  0x08  Taxi lights
        //   bit 4  0x10  Strobe lights
        // ------------------------------------------------------------------

        /// <summary>0x0D0C · 2 bytes · bitmap de luces externas</summary>
        private readonly Offset<short> _lightsOffset = new Offset<short>(0x0D0C);

        // ------------------------------------------------------------------
        // Identificación de aeronave (lectura única al conectar)
        // ------------------------------------------------------------------

        /// <summary>0x3D00 · string 256 bytes · título completo del avión (más fiable en MSFS)</summary>
        private readonly Offset<string> _aircraftTitle = new Offset<string>(0x3D00, 256);

        /// <summary>0x0618 · string 16 bytes · designador ICAO (puede no funcionar en MSFS)</summary>
        private readonly Offset<string> _icaoDesignator = new Offset<string>(0x0618, 16);

        /// <summary>0x09D2 · string 16 bytes · fabricante ICAO</summary>
        private readonly Offset<string> _icaoManufacturer = new Offset<string>(0x09D2, 16);

        /// <summary>0x0B26 · string 32 bytes · modelo ICAO</summary>
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

        // -- Estado anterior para detección de cambios por comparación --
        private bool _lastOnGround = true;
        private int _lastGearPosition;
        private double _lastFlapsPercent;
        private bool _lastSpoilersDeployed;
        private int _lastParkingBrake;
        private int _lastEngineRpm;
        private bool _lastBeaconState;

        // -- Debounce para liftoff / touchdown --
        private int _groundConsecutiveCounter;
        private DateTime _lastTakeoffTime = DateTime.MinValue;
        private DateTime _lastTouchdownTime = DateTime.MinValue;

        // -- Constantes de detección --
        /// <summary>Frames consecutivos en tierra antes de confirmar touchdown.</summary>
        private const int GROUND_CONFIRM_FRAMES = 1;

        /// <summary>Segundos mínimos entre eventos de liftoff o touchdown consecutivos.</summary>
        private const double EVENT_DEBOUNCE_SECONDS = 2.0;

        /// <summary>GS mínima en kt para disparar HandleTakeoff (umbral bajo: cubre BE58/C172).</summary>
        private const double TAKEOFF_MIN_SPEED_KT = 40.0;

        /// <summary>GS mínima en kt para disparar HandleTouchdown.</summary>
        private const double TOUCHDOWN_MIN_SPEED_KT = 30.0;

        /// <summary>VS máxima en fpm para disparar HandleTouchdown (debe ser descendente).</summary>
        private const double TOUCHDOWN_MIN_VS_FPM = -50.0;

        #endregion

        // =====================================================================
        #region Propiedades Públicas
        // Todas siguen las unidades estándar descritas en la cabecera del archivo.
        // =====================================================================


        /// <summary>Indica si hay conexión activa con FSUIPC.</summary>
        public bool IsConnected => _connectionState == ConnectionState.Connected;

        /// <summary>Nombre del simulador detectado (ej. "MSFS 2020", "Prepar3D").</summary>
        public string SimulatorName { get; private set; } = "Desconocido";

        // ---- Posición ----

        /// <summary>Latitud en grados decimales. Positivo = Norte.</summary>
        public double CurrentLatitude { get; private set; }

        /// <summary>Longitud en grados decimales. Positivo = Este.</summary>
        public double CurrentLongitude { get; private set; }

        /// <summary>Altitud MSL en feet.</summary>
        public double CurrentAltitudeFeet { get; private set; }

        /// <summary>
        /// AGL según el radio altímetro del simulador, en feet.
        /// Puede ser 0 si el simulador no reporta este dato; en ese caso
        /// MainViewModel calcula el AGL relativo al aeropuerto de referencia.
        /// </summary>
        public double CurrentRadarAltitudeFeet { get; private set; }

        // ---- Velocidades ----

        /// <summary>Velocidad de desplazamiento sobre tierra en knots.</summary>
        public double CurrentGroundSpeedKt { get; private set; }

        /// <summary>Velocidad vertical en feet por minuto. Positivo = ascenso.</summary>
        public double CurrentVerticalSpeedFpm { get; private set; }

        /// <summary>Velocidad indicada (IAS) en knots.</summary>
        public double CurrentIndicatedAirspeed { get; private set; }

        // ---- Actitud ----

        /// <summary>Cabeceo en grados. Positivo = nose up.</summary>
        public double CurrentPitch { get; private set; }

        /// <summary>Alabeo en grados. Positivo = ala derecha abajo.</summary>
        public double CurrentBank { get; private set; }

        /// <summary>Heading en grados.</summary>
        public double CurrentHeading { get; private set; }

        // ---- Combustible ----

        /// <summary>Combustible total en libras (todos los tanques).</summary>
        public double CurrentFuelLbs { get; private set; }

        /// <summary>Combustible total en kilogramos. Derivado: <see cref="CurrentFuelLbs"/> × 0.453592.</summary>
        public double CurrentFuelKg => CurrentFuelLbs * 0.453592;

        /// <summary>Flujo de combustible del motor 1 en lbs/hr.</summary>
        public double CurrentFuelFlowLbsHr { get; private set; }

        // ---- Controles y sistemas ----

        /// <summary>Posición del tren: 1 = abajo/anclado, 0 = subido o en tránsito.</summary>
        public int CurrentGearPosition { get; private set; }

        /// <summary>Posición de los flaps como porcentaje 0..100.</summary>
        public double CurrentFlapsPercent { get; private set; }

        /// <summary>True si los spoilers están extendidos o armados.</summary>
        public bool CurrentSpoilersDeployed { get; private set; }

        /// <summary>Factor de carga actual en G.</summary>
        public double CurrentGForce { get; private set; }

        /// <summary>True si el beacon está encendido (bit 1 del bitmap de luces 0x0D0C).</summary>
        public bool IsBeaconOn { get; private set; }

        // ---- Motores (acceso directo a los offsets, sin conversión adicional) ----

        /// <summary>N1 del motor 1 como fracción 0.0 .. 1.0. Multiplicar × 100 para porcentaje.</summary>
        public double CurrentN1 => _eng1N1Offset.Value;

        /// <summary>RPM del motor 1 para aviones de pistón. Valor directo sin escala.</summary>
        public int CurrentEngineRpm => _engRpmOffset.Value;

        public EnginePower Engine1Power { get; private set; }
        public EnginePower Engine2Power { get; private set; }


        // ---- Identificación de aeronave ----

        /// <summary>Título completo del avión tal como aparece en el simulador.</summary>
        public string AircraftTitle { get; private set; } = "Unknown";

        /// <summary>Designador ICAO del tipo (ej. "B738", "A320", "BE58").</summary>
        public string AircraftIcao { get; private set; } = "????";

        /// <summary>Fabricante de la aeronave (ej. "Boeing", "Airbus").</summary>
        public string AircraftManufacturer { get; private set; } = "Unknown";

        /// <summary>Modelo de la aeronave (ej. "737-800", "A320-200").</summary>
        public string AircraftModel { get; private set; } = "Unknown";

        #endregion

        // =====================================================================
        #region Eventos Públicos
        // =====================================================================

        /// <summary>Se dispara cuando la conexión con FSUIPC se establece correctamente.</summary>
        public event EventHandler Connected;

        /// <summary>Se dispara cuando se pierde la conexión con FSUIPC.</summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Emitido en cada ciclo según el intervalo de fase configurado.
        /// Contiene un snapshot completo del estado del simulador en unidades estándar.
        /// </summary>
        public event EventHandler<TelemetryData> TelemetryUpdated;

        /// <summary>
        /// Emitido en cada ciclo según el polling_interval.
        /// </summary>
        public event EventHandler<RawTelemetryData> RawDataUpdated;

        /// <summary>Emitido en el momento exacto del liftoff (ruedas dejan el suelo).</summary>
        public event EventHandler<TakeoffData> TakeoffDetected;

        /// <summary>Emitido en el momento exacto del touchdown (ruedas tocan el suelo).</summary>
        public event EventHandler<TouchdownData> TouchdownDetected;

        /// <summary>
        /// Emitido cuando cambia la posición del tren de aterrizaje.
        /// Parámetros: (posición anterior, posición nueva).
        /// </summary>
        public event Action<int, int> GearChanged;

        /// <summary>
        /// Emitido cuando la posición de flaps cambia más de 2%.
        /// Parámetros: (porcentaje anterior, porcentaje nuevo).
        /// </summary>
        public event Action<double, double> FlapsChanged;

        /// <summary>Emitido cuando los spoilers se extienden o retraen.</summary>
        public event Action<bool> SpoilersChanged;

        /// <summary>Emitido cuando se pone o se suelta el freno de estacionamiento.</summary>
        public event Action<bool> ParkingBrakeChanged;

        /// <summary>Emitido cuando los motores arrancan o se apagan.</summary>
        public event Action<bool> EnginesChanged;

        /// <summary>Emitido cuando el beacon se enciende o apaga.</summary>
        public event Action<bool> BeaconChanged;

        /// <summary>
        /// Emitido una sola vez tras conectar, cuando AircraftTitle, AircraftIcao, etc.
        /// ya tienen valor. Permite que el ViewModel muestre la info de aeronave.
        /// </summary>
        public event Action OnAircraftInfoReady;

        #endregion

        // =====================================================================
        #region Ciclo de Vida
        // =====================================================================

        /// <summary>
        /// Inicializa el servicio leyendo el intervalo de polling desde <see cref="AppConfig"/>.
        /// No conecta automáticamente; llamar a <see cref="Start"/> cuando sea necesario.
        /// </summary>
        public FsuipcService()
        {
            _pollingIntervalMs = AppConfig.PollingIntervalMs;
        }

        /// <summary>
        /// Inicia el timer de polling con un retardo inicial de 500 ms.
        /// Si FSUIPC no está disponible, reintentará con backoff exponencial
        /// hasta un máximo de 30 segundos entre intentos.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _pollingTimer = new Timer(OnPollingTick, null, 500, _pollingIntervalMs);
            Debug.WriteLine("FsuipcService: polling started.");
        }

        /// <summary>
        /// Detiene el polling y cierra la conexión con FSUIPC limpiamente.
        /// Seguro llamarlo múltiples veces.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _pollingTimer?.Dispose();
            _pollingTimer = null;
            Disconnect();
            Debug.WriteLine("FsuipcService: polling stopped.");
        }

        /// <inheritdoc/>
        public void Dispose() => Stop();

        #endregion

        // =====================================================================
        #region Intervalo Adaptativo por Fase de Vuelo
        // =====================================================================

        /// <summary>
        /// Ajusta el intervalo de emisión de telemetría según la fase de vuelo activa.
        /// Las fases críticas (despegue, aproximación) usan intervalos cortos para
        /// mayor resolución; el crucero usa intervalos largos para reducir la carga
        /// en el servidor phpVMS. Los valores se configuran en <see cref="AppConfig"/>.
        /// </summary>
        /// <param name="phase">Fase de vuelo actual reportada por <see cref="FlightManager"/>.</param>
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

        /// <summary>
        /// Callback del timer de polling. Único punto de entrada al ciclo de lectura.
        /// <para>Secuencia por ciclo:</para>
        /// <list type="number">
        ///   <item>Si desconectado → <see cref="TryConnect"/> y salir.</item>
        ///   <item><c>FSUIPCConnection.Process()</c> — sincroniza todos los offsets en memoria.</item>
        ///   <item><see cref="ReadAircraftInfo"/> — solo en el primer tick tras conectar.</item>
        ///   <item><see cref="ReadAllOffsets"/> — decodifica offsets → propiedades públicas.</item>
        ///   <item><see cref="DetectEvents"/> — compara con estado anterior, dispara eventos.</item>
        ///   <item><see cref="SendTelemetry"/> — emite <see cref="TelemetryUpdated"/> según intervalo.</item>
        /// </list>
        /// </summary>
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
                // Un único Process() por ciclo garantiza coherencia entre todos los offsets
                FSUIPCConnection.Process();

                // Info de aeronave: una sola vez por sesión de conexión
                if (!_aircraftInfoRead)
                {
                    ReadAircraftInfo();
                    _aircraftInfoRead = true;
                    ReadAllOffsets();
                    OnAircraftInfoReady?.Invoke();
                }

                // Restablecer contadores de backoff tras un ciclo exitoso
                _connectionRetryCount = 0;
                _currentBackoffMs = 1000;
                _isReconnecting = false;

                // Pipeline principal: leer → detectar → emitir
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
            var args = new RawTelemetryData
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
            };

            RawDataUpdated?.Invoke(this, args);
        }
        #endregion

        // =====================================================================
        #region Lectura de Offsets → Propiedades Públicas
        // =====================================================================

        /// <summary>
        /// Decodifica todos los offsets cargados por el último <c>Process()</c>
        /// y actualiza las propiedades públicas del servicio.
        /// Solo debe llamarse después de <c>FSUIPCConnection.Process()</c>.
        /// </summary>
        private void ReadAllOffsets()
        {
            if (_connectionState != ConnectionState.Connected) return;

            // ---- Posición geográfica ----
            CurrentLatitude = DecodeLatitude(_latOffset.Value);
            CurrentLongitude = DecodeLongitude(_lonOffset.Value);

            // altitud real AMSL independiente de la presión
            CurrentAltitudeFeet = DecodeAltitude(_altOffset.Value);

            // Radar altímetro: metros → feet.
            // Si el simulador no lo reporta (≤ 0), queda en 0; MainViewModel calculará
            // el AGL relativo al aeropuerto de referencia de la fase actual.
            double radarMeters = _radarAltitudeOffset.Value;
            CurrentRadarAltitudeFeet = radarMeters > 0 ? radarMeters * 3.28084 : 0.0;

            // ---- Velocidades ----
            CurrentHeading = DecodeHeading(_headingOffset.Value);
            CurrentGroundSpeedKt = DecodeGroundSpeed(_groundSpeedOffset.Value);
            CurrentVerticalSpeedFpm = DecodeVerticalSpeed(_verticalSpeedOffset.Value);
            CurrentIndicatedAirspeed = DecodeIndicatedAirspeed(_iasOffset.Value);

            // ---- Actitud ----
            // Pitch: FSUIPC nose-up = negativo → el decoder invierte el signo
            CurrentPitch = DecodePitch(_pitchOffset.Value);
            CurrentBank = DecodeBank(_bankOffset.Value);

            // ---- Combustible ----
            CurrentFuelLbs = _fuelWeightOffset.Value;   // raw = lbs × 128
            CurrentFuelFlowLbsHr = _fuelFlowOffset.Value / 128.0;   // raw = lbs/hr × 128

            // ---- Acelerometría ----
            CurrentGForce = _gforceOffset.Value / 625.0;               // raw = G × 625

            // ---- Luces ----
            // Beacon = bit 1 (0x02). El bit 0 (0x01) corresponde a nav lights, no beacon.
            IsBeaconOn = (_lightsOffset.Value & 0x02) != 0;

            // ---- Controles de vuelo ----
            CurrentGearPosition = DecodeGear(_gearOffset.Value);
            CurrentFlapsPercent = DecodeFlaps(_flapsOffset.Value);
            CurrentSpoilersDeployed = _spoilersOffset.Value > 0 || _spoilersArmedOffset.Value > 0;

            // Leer motores
            short eng1N1Raw = _eng1N1.Value;
            short eng2N1Raw = _eng2N1.Value;
            short eng1RpmRaw = _eng1Rpm.Value;
            short eng2RpmRaw = _eng2Rpm.Value;

            Engine1Power = DecodeEnginePower(eng1N1Raw, eng1RpmRaw);
            Engine2Power = DecodeEnginePower(eng2N1Raw, eng2RpmRaw);
        }

        #endregion

        // =====================================================================
        #region Decodificadores de Offsets
        // Un método estático por variable, con offset, tipo FSUIPC y fórmula documentados.
        // =====================================================================

        private EnginePower DecodeEnginePower(short n1Raw, short rpmRaw)
        {
            // N1 = raw * 100 / 16384
            double n1 = n1Raw * 100.0 / 16384.0;

            // RPM = raw / 4 (simplificado de raw * 16384 / 65536)
            double rpm = rpmRaw / 4.0;

            // Jet detectado por N1 > 5% (ralentí)
            if (n1 > 5)
            {
                return new EnginePower
                {
                    Value = n1,
                    Type = "N1"
                };
            }

            // Turboprop vs Piston por RPM
            if (rpm > 0)
            {
                if (rpm < 1200)
                {
                    return new EnginePower
                    {
                        Value = rpm,
                        Type = "PROP RPM"
                    };
                }
                else
                {
                    return new EnginePower
                    {
                        Value = rpm,
                        Type = "PISTON RPM"
                    };
                }
            }

            return new EnginePower { Value = 0, Type = "OFF" };
        }

        /// <summary>
        /// Convierte el offset de latitud a grados decimales.
        /// <para>Offset 0x0560 · signed 64-bit · raw = lat × (10001750 × 65536²) / 90</para>
        /// </summary>
        private static double DecodeLatitude(long raw)
            => raw * 90.0 / (10001750.0 * 65536.0 * 65536.0);

        /// <summary>
        /// Convierte el offset de longitud a grados decimales.
        /// <para>Offset 0x0568 · signed 64-bit · raw = lon × 2⁶⁴ / 360</para>
        /// </summary>
        private static double DecodeLongitude(long raw)
            => raw * 360.0 / (65536.0 * 65536.0 * 65536.0 * 65536.0);

        /// <summary>
        /// Convierte el offset de altitud 0x0570 a feet MSL.
        /// <para>Offset 0x0570 · signed 64-bit · raw = metros × 65536</para>
        /// <para>Conversión: metros = raw / 65536 → feet = metros × 3.28084</para>
        /// </summary>
        private static double DecodeAltitude(long raw)
            => (raw / 65536.0 / 65536.0) * 3.28084;

        /// <summary>
        /// Convierte el offset de heading a grados 0..360.
        /// <para>Offset 0x0580 · unsigned 32-bit · raw = hdg × 2³² / 360</para>
        /// </summary>
        private static double DecodeHeading(uint raw)
            => raw * 360.0 / (65536.0 * 65536.0);

        /// <summary>
        /// Convierte el offset de ground speed a knots.
        /// <para>Offset 0x02B4 · signed 32-bit · raw = m/s × 65536</para>
        /// <para>Conversión: m/s = raw / 65536 → kt = m/s × 1.94384</para>
        /// </summary>
        private static double DecodeGroundSpeed(int raw)
            => (raw / 65536.0) * 1.94384;

        /// <summary>
        /// Convierte el offset de velocidad vertical a feet por minuto.
        /// <para>Offset 0x02C8 · signed 32-bit · raw = m/s × 256</para>
        /// <para>Conversión: m/s = raw / 256 → fpm = m/s × 196.85</para>
        /// </summary>
        private static double DecodeVerticalSpeed(int raw)
            => (raw / 256.0) * 196.85;

        /// <summary>
        /// Convierte el offset de IAS a knots.
        /// <para>Offset 0x02BC · signed 32-bit · raw = kt × 128</para>
        /// </summary>
        private static double DecodeIndicatedAirspeed(int raw)
            => raw / 128.0;

        /// <summary>
        /// Convierte el offset de pitch a grados con convención nose-up positivo.
        /// </summary>
        private static double DecodePitch(int raw)
        {
            // Fórmula correcta para 32 bits
            // raw = grados × 2³² / 360
            double pitch = raw * 360.0 / 4294967296.0;

            // Limitar a rango realista
            if (pitch > 30) pitch = 30;
            if (pitch < -30) pitch = -30;

            return Math.Round(pitch, 1);
        }

        /// <summary>
        /// Convierte el offset de bank a grados con convención bank-derecha positivo.
        /// </summary>
        private static double DecodeBank(int raw)
        {
            double bank = raw * 360.0 / 4294967296.0;

            if (bank > 45) bank = 45;
            if (bank < -45) bank = -45;

            return Math.Round(bank, 1);
        }

        /// <summary>
        /// Convierte el offset del tren de aterrizaje a estado binario.
        /// <para>Offset 0x0BE8 · 32-bit · rango 0 (subido) .. 16383 (abajo/anclado)</para>
        /// <para>Retorna 1 si supera el 50% del recorrido (> 8000), 0 en caso contrario.</para>
        /// </summary>
        private static int DecodeGear(int raw)
            => raw > 8000 ? 1 : 0;

        /// <summary>
        /// Convierte el offset de flaps a porcentaje 0..100.
        /// <para>Offset 0x0BDC · signed 16-bit · rango 0 (arriba) .. 16383 (fondo)</para>
        /// <para>Factor de escala: 16383 / 100 = 163.83</para>
        /// </summary>

        private static double DecodeFlaps(short raw)
        {
            double p = raw / 16383.0;

            int detent;

            // 9 detents (Boeing)
            if (p < 0.02) detent = 0;
            else if (p < 0.08) detent = 1;
            else if (p < 0.14) detent = 2;
            else if (p < 0.22) detent = 5;
            else if (p < 0.32) detent = 10;
            else if (p < 0.45) detent = 15;
            else if (p < 0.62) detent = 25;
            else if (p < 0.78) detent = 30;
            else detent = 40;

            return detent;
        }

        #endregion

        // =====================================================================
        #region Detección de Eventos de Vuelo
        // =====================================================================

        /// <summary>
        /// Orquesta la detección de todos los eventos de vuelo en cada ciclo.
        /// Cada sub-método compara el valor actual con el estado anterior y dispara
        /// el evento correspondiente solo cuando hay un cambio real.
        /// </summary>
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

        /// <summary>
        /// Detecta la transición tierra ↔ aire usando el offset 0x0366.
        /// Requiere <see cref="GROUND_CONFIRM_FRAMES"/> lecturas consecutivas en tierra
        /// antes de confirmar un touchdown, evitando falsos positivos en pistas irregulares.
        /// </summary>
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

        private const double FLAPS_HYSTERESIS = 1.0;
        private DateTime _lastFlapsChangeTime = DateTime.MinValue;
        private const double FLAPS_DEBOUNCE_MS = 500;

        private void DetectFlapsChange()
        {
            double flaps = CurrentFlapsPercent;

            // Calcular diferencia real
            double diff = Math.Abs(flaps - _lastFlapsPercent);

            // Solo considerar cambio si supera la histéresis O ha pasado el debounce
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

        /// <summary>
        /// Solo usa el offset 0x0BC8 (parking brake real).
        /// Los offsets 0x0B88/0x0B8C son frenos de rueda hidráulicos y se excluyen
        /// deliberadamente para evitar falsos positivos durante el frenado normal.
        /// </summary>
        private void DetectParkingBrakeChange()
        {
            int parking = _parkingBrakeOffset.Value != 0 ? 1 : 0;
            if (parking == _lastParkingBrake) return;
            ParkingBrakeChanged?.Invoke(parking == 1);
            _lastParkingBrake = parking;
        }

        /// <summary>
        /// Detección híbrida para cubrir turbinas (N1) y pistones (RPM).
        /// Umbral: N1 &gt; 5% o RPM &gt; 800.
        /// </summary>
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
        #region Manejo de Liftoff y Touchdown
        // =====================================================================

        /// <summary>
        /// Captura el snapshot de datos en el momento del liftoff y dispara
        /// <see cref="TakeoffDetected"/>. Aplica debounce temporal y umbral mínimo
        /// de GS para evitar disparos por timbres de pista o aviones estacionados.
        /// </summary>
        private void HandleTakeoff()
        {
            if ((DateTime.UtcNow - _lastTakeoffTime).TotalSeconds < EVENT_DEBOUNCE_SECONDS) return;
            if (CurrentGroundSpeedKt < TAKEOFF_MIN_SPEED_KT) return;

            _lastTakeoffTime = DateTime.UtcNow;

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

                OatCelsius = _oatOffset.Value / 256.0,
                WindSpeedKt = _windSpeedOffset.Value,
                WindDirDeg = _windDirOffset.Value
            });
        }

        /// <summary>
        /// Captura el snapshot de datos en el momento del touchdown y dispara
        /// <see cref="TouchdownDetected"/>. Verifica que la GS sea suficiente y que
        /// el VS sea descendente para distinguir un aterrizaje de una simple parada.
        /// </summary>
        private void HandleTouchdown()
        {
            if ((DateTime.UtcNow - _lastTouchdownTime).TotalSeconds < EVENT_DEBOUNCE_SECONDS) return;
            if (CurrentGroundSpeedKt < TOUCHDOWN_MIN_SPEED_KT) return;
            if (CurrentVerticalSpeedFpm > TOUCHDOWN_MIN_VS_FPM) return;

            _lastTouchdownTime = DateTime.UtcNow;

            TouchdownDetected?.Invoke(this, new TouchdownData
            {
                Timestamp = DateTime.UtcNow,
                LatitudeDeg = CurrentLatitude,
                LongitudeDeg = CurrentLongitude,
                AltitudeMeters = CurrentAltitudeFeet / 3.28084,
                VerticalSpeedFpm = CurrentVerticalSpeedFpm,
                GroundSpeedKt = CurrentGroundSpeedKt,
                IasKt = CurrentIndicatedAirspeed,
                HeadingDeg = CurrentHeading,
                PitchDeg = CurrentPitch,
                BankDeg = CurrentBank,
                GForceAtTouch = CurrentGForce,
                FlapsPosition = CurrentFlapsPercent / 100.0,
                SpoilersPosition = CurrentSpoilersDeployed ? 1.0 : 0.0,
                GearPosition = CurrentGearPosition,
                Eng1N1Pct = _eng1N1Offset.Value * 100.0,
                Eng2N1Pct = _eng2N1Offset.Value * 100.0,
                Eng1ReverserPct = _eng1ReverserOffset.Value * 100.0,
                Eng2ReverserPct = _eng2ReverserOffset.Value * 100.0,
                BrakeLeft = _brakeLeftOffset.Value / 32767.0,
                BrakeRight = _brakeRightOffset.Value / 32767.0,
                AutobrakeSetting = _autobrakeOffset.Value,
                OatCelsius = _oatOffset.Value / 256.0,
                WindSpeedKt = _windSpeedOffset.Value,
                WindDirDeg = _windDirOffset.Value
            });
        }

        #endregion

        // =====================================================================
        #region Telemetría Periódica
        // =====================================================================

        /// <summary>
        /// Emite un <see cref="TelemetryData"/> si ha transcurrido el intervalo
        /// configurado para la fase actual. Llamar en cada ciclo de polling.
        /// </summary>
        private void SendTelemetry()
        {
            if (DateTime.UtcNow - _lastTelemetrySend < TimeSpan.FromSeconds(_currentPhaseInterval))
                return;

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
                FuelFlow = CurrentFuelFlowLbsHr,     // lbs/hr, unidad phpVMS
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

        /// <summary>
        /// Determina el tipo de navegación activo según el modo del autopiloto.
        /// <list type="bullet">
        ///   <item>0 = manual (AP desenganchado)</item>
        ///   <item>1 = LNAV / VOR</item>
        ///   <item>3 = ILS / GPS</item>
        /// </list>
        /// </summary>
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
        /// Construye el string de viento para el campo ACARS.
        /// Formato: "270° at 15 kts" o "Calm" si no hay viento.
        /// Usa <see cref="CultureInfo.InvariantCulture"/> para garantizar que el
        /// separador decimal y el formato de número sean independientes del PC.
        /// </summary>
        private string BuildWindString()
        {
            int dir = _windDirOffset.Value;
            int spd = _windSpeedOffset.Value;
            if (dir == 0 && spd == 0) return "Calm";
            return string.Format(CultureInfo.InvariantCulture, "{0:000}° at {1} kts", dir, spd);
        }

        #endregion

        // =====================================================================
        #region Conexión y Reconexión
        // =====================================================================

        /// <summary>
        /// Intenta abrir la conexión con FSUIPC. En caso de fallo aplica backoff
        /// exponencial: 1s → 2s → 4s → 8s → 16s → máximo 30s entre reintentos.
        /// </summary>
        private void TryConnect()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            try
            {
                Debug.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "FsuipcService: connecting (attempt {0})...", _connectionRetryCount + 1));

                FSUIPCConnection.Open();

                _connectionState = ConnectionState.Connected;
                SimulatorName = DetectSimulator();
                _aircraftInfoRead = false;            // releer info de aeronave tras reconectar
                _connectionRetryCount = 0;
                _currentBackoffMs = 1000;
                _isReconnecting = false;
                _lastTelemetrySend = DateTime.MinValue; // emitir posición inmediatamente

                Debug.WriteLine($"FsuipcService: connected to {SimulatorName}");
                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _connectionState = ConnectionState.Disconnected;
                _isReconnecting = false;
                _connectionRetryCount++;

                // Backoff exponencial, tope en 30 s
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
            try { FSUIPCConnection.Close(); } catch { /* ignorar si ya estaba cerrado */ }
            _connectionState = ConnectionState.Disconnected;
        }

        /// <summary>
        /// Detecta el simulador activo inspeccionando los procesos del sistema.
        /// Si ninguno coincide, consulta la propiedad de versión de FSUIPC como fallback.
        /// </summary>
        private static string DetectSimulator()
        {
            if (Process.GetProcessesByName("FlightSimulator2024").Length > 0) return "MSFS 2024";
            if (Process.GetProcessesByName("FlightSimulator").Length > 0) return "MSFS 2020";
            if (Process.GetProcessesByName("X-Plane").Length > 0) return "X-Plane";
            if (Process.GetProcessesByName("Prepar3D").Length > 0) return "Prepar3D";
            return FSUIPCConnection.FlightSimVersionConnected.ToString();
        }

        #endregion

        // =====================================================================
        #region Identificación de Aeronave
        // =====================================================================

        /// <summary>
        /// Lee los offsets de identificación de aeronave. Se ejecuta una sola vez
        /// tras conectar (o reconectar). Si los offsets ICAO no están disponibles
        /// (común en MSFS sin WASim), extrae el tipo del título mediante
        /// <see cref="ExtractIcaoFromTitle"/>. Dispara <see cref="OnAircraftInfoReady"/>
        /// al finalizar la lectura.
        /// </summary>
        private void ReadAircraftInfo()
        {
            if (!IsConnected) return;
            try
            {
                // Título: el offset más fiable; disponible en todos los simuladores
                string title = _aircraftTitle.Value;
                if (!string.IsNullOrWhiteSpace(title) && title != "\0")
                {
                    AircraftTitle = title.Trim();
                    Debug.WriteLine($"FsuipcService: aircraft title = '{AircraftTitle}'");
                }

                // Designador ICAO (puede fallar en MSFS)
                TryReadStringOffset(
                    () => _icaoDesignator.Value,
                    val => { if (val != "????") AircraftIcao = val; });

                // Fabricante
                TryReadStringOffset(
                    () => _icaoManufacturer.Value,
                    val => AircraftManufacturer = val);

                // Modelo
                TryReadStringOffset(
                    () => _icaoModel.Value,
                    val => AircraftModel = val);

                // Fallback: extraer ICAO del título si el offset no lo entregó
                if (AircraftIcao == "????" && !string.IsNullOrEmpty(AircraftTitle))
                    AircraftIcao = ExtractIcaoFromTitle(AircraftTitle);

                OnAircraftInfoReady?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FsuipcService: error reading aircraft info — {ex.Message}");
            }
        }

        /// <summary>
        /// Lee un offset de string de forma segura, ignorando excepciones por offsets
        /// no disponibles en el simulador activo. Solo llama al setter si el valor
        /// es no nulo, no vacío y no es el carácter nulo.
        /// </summary>
        private static void TryReadStringOffset(Func<string> reader, Action<string> setter)
        {
            try
            {
                string val = reader()?.Trim();
                if (!string.IsNullOrWhiteSpace(val) && val != "\0")
                    setter(val);
            }
            catch { /* offset no disponible en este simulador — ignorar */ }
        }

        /// <summary>
        /// Extrae el designador ICAO más probable del título completo de la aeronave.
        /// Usado como fallback cuando el offset 0x0618 no está disponible.
        /// El orden del diccionario importa: las entradas más específicas van primero.
        /// </summary>
        /// <param name="title">Título tal como lo reporta el simulador.</param>
        /// <returns>Designador ICAO (ej. "B738") o "????" si no se reconoce.</returns>
        private static string ExtractIcaoFromTitle(string title)
        {
            // Más específico primero para evitar colisiones (ej. "737-800" antes de "737-")
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
        #region Clasificación y Librea de Aeronave
        // =====================================================================

        /// <summary>
        /// Categorías de aeronave usadas para ajustar umbrales de detección de fases
        /// (velocidades de rotación, tasas de descenso esperadas, etc.).
        /// </summary>
        public enum AircraftCategory
        {
            /// <summary>Tipo no reconocido o sin datos.</summary>
            Unknown,
            /// <summary>Aeronave a reacción (B738, A320, etc.).</summary>
            Jet,
            /// <summary>Turbohélice (ATR, Q400, etc.).</summary>
            Turboprop,
            /// <summary>Pistón (C172, BE58, PA28, etc.).</summary>
            Piston,
            /// <summary>Helicóptero (reservado para uso futuro).</summary>
            Helicopter
        }

        /// <summary>
        /// Clasifica la aeronave actual según su designador ICAO.
        /// Útil para ajustar umbrales de velocidad y lógica de detección de fases.
        /// </summary>
        /// <returns>La categoría de la aeronave o <see cref="AircraftCategory.Unknown"/>.</returns>
        public AircraftCategory GetAircraftCategory()
        {
            if (string.IsNullOrEmpty(AircraftIcao)) return AircraftCategory.Unknown;

            string icao = AircraftIcao.ToUpperInvariant();

            string[] jets = {
                "B73","B74","B75","B76","B77","B78","B79",
                "A3","A4","A5","A6","A7","A8","A9",
                "E17","E19","E45","E70","E75",
                "CRJ","C700","C750","GLF","F2TH","DA62"
            };
            string[] turboprops = {
                "AT43","AT45","AT72",
                "DH8A","DH8B","DH8C","DH8D",
                "E120","F50","JS32","JS41","SB20","SW4"
            };
            string[] pistons = {
                "C172","C182","C206","C208",
                "PA28","PA32","PA34",
                "BE33","BE35","BE36","BE55","BE58",
                "P28A","P28B","P28R"
            };

            foreach (var p in jets) if (icao.Contains(p)) return AircraftCategory.Jet;
            foreach (var p in turboprops) if (icao.Contains(p)) return AircraftCategory.Turboprop;
            foreach (var p in pistons) if (icao.Contains(p)) return AircraftCategory.Piston;

            return AircraftCategory.Unknown;
        }

        /// <summary>
        /// Intenta extraer el nombre de la aerolínea desde el título de la aeronave.
        /// Primero busca en una lista curada de aerolíneas; si no encuentra, busca
        /// una abreviatura de 3-4 letras mayúsculas que no sea un designador ICAO conocido.
        /// </summary>
        /// <returns>Nombre de la aerolínea o "Unknown" si no se puede determinar.</returns>
        public string GetAircraftLivery()
        {
            if (string.IsNullOrEmpty(AircraftTitle)) return "Unknown";

            string[] airlines = {
                "United","American","Delta","Iberia","Lufthansa",
                "British","Air France","KLM","Emirates","Qatar",
                "Avianca","LATAM","Viva","EasyJet","Ryanair",
                "Southwest","JetBlue","Spirit","Frontier","Alaska",
                "Copa","Aeromexico","Air Canada","WestJet",
                "Virgin","Etihad","Turkish","Singapore","Cathay",
                "VHR"
            };

            foreach (var a in airlines)
                if (AircraftTitle.Contains(a)) return a;

            // Fallback: sigla de 3-4 letras mayúsculas que no sea un designador ICAO conocido
            string[] knownIcaos = {
                "B38M","B738","A320","A319","A321","B737","B747","B777","B787"
            };
            var parts = AircraftTitle.Split(
                new[] { ' ', '-', '_', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries);

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

    /// <summary>
    /// Snapshot completo del estado del simulador, emitido por
    /// <see cref="FsuipcService.TelemetryUpdated"/> en cada ciclo de polling.
    /// <para>
    /// <b>Unidades (estándar phpVMS/ACARS):</b>
    /// <list type="table">
    ///   <item><term>Altitudes</term><description>feet</description></item>
    ///   <item><term>Velocidades horizontales</term><description>knots</description></item>
    ///   <item><term>Velocidad vertical</term><description>feet por minuto (fpm)</description></item>
    ///   <item><term>Ángulos</term><description>grados decimales</description></item>
    ///   <item><term>Combustible total</term><description>lbs</description></item>
    ///   <item><term>Flujo de combustible</term><description>lbs/hr</description></item>
    ///   <item><term>Fecha/hora</term><description>UTC</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class TelemetryData : EventArgs
    {
        // ---- Posición ----

        /// <summary>Latitud en grados decimales. Positivo = Norte.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitud en grados decimales. Positivo = Este.</summary>
        public double Longitude { get; set; }

        // ---- Altitudes ----

        /// <summary>Altitud MSL en feet.</summary>
        public double AltitudeFeet { get; set; }

        /// <summary>
        /// AGL según el radio altímetro del simulador (offset 0x31E4), en feet.
        /// Es 0 cuando el simulador no reporta este dato; en ese caso
        /// MainViewModel calcula el AGL relativo al aeropuerto de referencia de la fase.
        /// </summary>
        public double RadarAltitudeFeet { get; set; }

        // ---- Velocidades ----

        /// <summary>Ground speed en knots.</summary>
        public double GroundSpeedKt { get; set; }

        /// <summary>Heading magnético en grados 0..360.</summary>
        public double HeadingDeg { get; set; }

        /// <summary>Velocidad vertical en fpm. Positivo = ascenso.</summary>
        public double VerticalSpeedFpm { get; set; }

        /// <summary>Indicated Airspeed (IAS) en knots.</summary>
        public double IndicatedAirspeedKt { get; set; }

        // ---- Estado de vuelo ----

        /// <summary>True si el avión está en tierra según el offset 0x0366.</summary>
        public bool IsOnGround { get; set; }

        // ---- Combustible ----

        /// <summary>Combustible total en lbs (todos los tanques).</summary>
        public double FuelLbs { get; set; }

        /// <summary>Flujo de combustible del motor 1 en lbs/hr.</summary>
        public double FuelFlow { get; set; }

        // ---- Actitud ----

        /// <summary>Cabeceo en grados. Positivo = nose up.</summary>
        public double PitchDeg { get; set; }

        /// <summary>Alabeo en grados. Positivo = ala derecha abajo.</summary>
        public double BankDeg { get; set; }

        // ---- Sistemas ----

        /// <summary>Código transponder en formato BCD (valor directo del offset).</summary>
        public int Transponder { get; set; }

        /// <summary>True si el autopiloto maestro está enganchado.</summary>
        public bool AutopilotEngaged { get; set; }

        /// <summary>
        /// Tipo de navegación activo.
        /// 0 = manual, 1 = LNAV/VOR, 3 = ILS/GPS.
        /// </summary>
        public int NavType { get; set; }

        /// <summary>Número de orden secuencial de esta posición ACARS.</summary>
        public int Order { get; set; }

        /// <summary>True si los spoilers están extendidos o armados.</summary>
        public bool SpoilersDeployed { get; set; }

        /// <summary>Posición de los flaps como porcentaje 0..100.</summary>
        public double FlapsPercent { get; set; }

        /// <summary>True si el tren de aterrizaje está abajo y anclado.</summary>
        public bool GearDown { get; set; }

        /// <summary>
        /// Viento en formato "270° at 15 kts" o "Calm".
        /// Siempre formateado con <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public string Wind { get; set; }
    }

    /// <summary>Estado de la conexión FSUIPC.</summary>
    public enum ConnectionState
    {
        /// <summary>Sin conexión activa con el simulador.</summary>
        Disconnected,

        /// <summary>Conexión activa; offsets disponibles para lectura.</summary>
        Connected
    }

    #endregion
    // Clase para datos crudos (se emite en cada polling)
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

    public class EnginePower
    {
        public double Value { get; set; }
        public string Type { get; set; }  // "N1", "PROP RPM", "PISTON RPM"

        public string Display => $"{Value:F0} {Type}";

        public double GetN1()
        {
            return Type == "N1" ? Value : 0;
        }

        public double GetRpm()
        {
            return (Type == "PROP RPM" || Type == "PISTON RPM") ? Value : 0;
        }
    }
}
