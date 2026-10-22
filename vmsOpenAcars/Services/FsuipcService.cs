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
using System.IO;
using System.Threading;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using FSUIPC;

// =============================================================================
// Enums y Decoder para Autobrake
// =============================================================================

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
        /// <summary>0x31E4 · INT32 · Radio/radar altitude in metres × 65536. Divide by 65536 to get metres.</summary>
        private readonly Offset<int> _radarAltitudeOffset = new Offset<int>(0x31E4);
        private readonly Offset<int> _groundAltitudeOffset = new Offset<int>(0x0020);

        // ---- Estado de vuelo ----
        private readonly Offset<short> _onGroundOffset = new Offset<short>(0x0366);
        private readonly Offset<int> _simTimeOffset = new Offset<int>(0x023A);

        // ---- Combustible ----
        /// <summary>
        /// 0x126C · INT32 · peso total de combustible, **en LIBRAS** (la tabla de FSUIPC no documenta
        /// este offset en el proyecto, así que la unidad está fijada por medición): el valor se usa
        /// tal cual como `fuel` de las posiciones ACARS —phpVMS también guarda la masa en libras— y
        /// se convierte a kg para el PIREP (`PirepMassUnits`). Comprobado con el PIREP real
        /// `9E20We81wlBgp3Nj`: 9797 en el arranque = 4444 kg de rampa, que es lo que aceptó la
        /// validación contra el OFP en kg; si el offset fuese kg, esa validación (× 0,453592) habría
        /// bloqueado todos los vuelos.
        /// </summary>
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
        /// <summary>0x2220 · DWORD · N2 bobina de alta presión motor 1 (0–16384 = 0–100%)</summary>
        private readonly Offset<int> _eng1N2Offset = new Offset<int>(0x2220);
        /// <summary>0x2420 · DWORD · N2 bobina de alta presión motor 2 (0–16384 = 0–100%)</summary>
        private readonly Offset<int> _eng2N2Offset = new Offset<int>(0x2420);

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
        /// <summary>0x2020/0x2120 · FLOAT64 · Torque % (0–100). Nota: 0x2068 es fuel flow lb/hr, no torque.</summary>
        private readonly Offset<double> _eng1TorquePctF64 = new Offset<double>(0x2020);
        private readonly Offset<double> _eng2TorquePctF64 = new Offset<double>(0x2120);
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
        // 0x0BD8 · INT32 · SPOILERS RIGHT POSITION — **posición** 0–16383 según la tabla de FSUIPC7, no
        // un booleano: leerla con `> 0` hacía que el 0,006 % de recorrido (una cuenta del indicador)
        // contara como «EXTENDIDOS».
        private readonly Offset<int> _spoilersOffset = new Offset<int>(0x0BD8);
        // 0x0BCC · INT32 · SPOILERS ARMED — «arm for auto deployment». **No** se usa para decidir si
        // están desplegados: armar antes de aterrizar es lo normal, y contarlo como despliegue era la
        // otra mitad del parpadeo (v0.9.50).
        private readonly Offset<int> _spoilersArmedOffset = new Offset<int>(0x0BCC);

        // ---- Flaps ----
        private readonly Offset<byte> _flapsHandleIndex = new Offset<byte>(0x0BFC);     // Notch actual (0,1,2,3...)
        private readonly Offset<int> _flapsHandlePercent = new Offset<int>(0x0BDC);    // Posición del handle 0-16383
        private readonly Offset<int> _flapsActualLeft = new Offset<int>(0x0BE0);       // Posición física real izquierda
        private readonly Offset<int> _flapsActualRight = new Offset<int>(0x0BE4);      // Posición física real derecha

        // ---- Autopiloto (todos Responded en MSFS; ver Helpers/AutopilotPanel) ----
        // 0x07BC · 4 bytes · AUTOPILOT MASTER
        private readonly Offset<int> _apMasterOffset = new Offset<int>(0x07BC);
        // 0x07CC · 2 bytes · AUTOPILOT HEADING LOCK DIR: **el rumbo**, no una bandera (grados*65536/360).
        // Hasta v0.9.50 se usaba como «modo» y por eso el AP salía enganchado siempre.
        private readonly Offset<short> _apHeadingBugOffset = new Offset<short>(0x07CC);
        private readonly Offset<int> _apAvailableOffset = new Offset<int>(0x0764);
        private readonly Offset<int> _apNavLockOffset = new Offset<int>(0x07C4);
        private readonly Offset<int> _apHeadingLockOffset = new Offset<int>(0x07C8);
        private readonly Offset<int> _apAltitudeLockOffset = new Offset<int>(0x07D0);
        private readonly Offset<int> _apAirspeedHoldOffset = new Offset<int>(0x07DC);
        private readonly Offset<int> _apMachHoldOffset = new Offset<int>(0x07E4);
        private readonly Offset<int> _apVerticalHoldOffset = new Offset<int>(0x07EC);
        private readonly Offset<int> _apGlideslopeHoldOffset = new Offset<int>(0x07FC);
        private readonly Offset<int> _apApproachHoldOffset = new Offset<int>(0x0800);
        private readonly Offset<int> _apBackCourseOffset = new Offset<int>(0x0804);
        private readonly Offset<int> _apFlightDirectorOffset = new Offset<int>(0x2EE0);
        private readonly Offset<short> _transponderOffset = new Offset<short>(0x0354);
        /// <summary>0x034E · INT16 · COM1 active frequency, BCD encoded (decode → MHz).</summary>
        private readonly Offset<short> _com1FreqOffset = new Offset<short>(0x034E);
        /// <summary>0x0350 · INT16 · NAV1 active frequency, BCD encoded (decode → MHz).</summary>
        private readonly Offset<short> _nav1FreqOffset = new Offset<short>(0x0350);
        /// <summary>0x0C4E · INT16 · NAV1 OBS / ILS course 0–359°.</summary>
        private readonly Offset<short> _nav1ObsCourseOffset = new Offset<short>(0x0C4E);

        // ---- Frenos ----
        private readonly Offset<short> _parkingBrakeOffset = new Offset<short>(0x0BC8);
        /// <summary>0x0330 · INT16 · Altímetro Kohlsman setting. QNH_hPa = value / 16.0</summary>
        private readonly Offset<short> _kohlsmanOffset = new Offset<short>(0x0330);
        private readonly Offset<short> _brakeLeftOffset = new Offset<short>(0x0BC4);
        private readonly Offset<short> _brakeRightOffset = new Offset<short>(0x0BC6);

        // ---- Autobrake y carteles de cabina ----
        private readonly Offset<string> _aircraftTitleOffset = new Offset<string>(0x3D00, 256);
        // 0x2F80 · 1 byte · AUTO BRAKE SWITCH CB (Responded): el **selector** (0=RTO, 1=Off, 2..5).
        private readonly Offset<byte> _autobrakePanelOffset = new Offset<byte>(0x2F80);
        // 0x0260 · 2 bytes · autobrake del A320neo de MSFS (0=Off, 1=Lo, 2=Med, 3=Max). No Responded.
        private readonly Offset<short> _autobrakeAirbusOffset = new Offset<short>(0x0260);
        // 0x0C46 · 1 byte · AUTOBRAKES ACTIVE: si está **actuando**, no cuál está puesto. No Responded.
        private readonly Offset<byte> _autobrakeActiveOffset = new Offset<byte>(0x0C46);
        // 0x341C / 0x341D · 1 byte · CABIN NO SMOKING / SEATBELTS ALERT SWITCH (Responded, lectura y
        // escritura). Se leen desde v0.9.51: antes el proyecto decía —y era verdad— que no se leían.
        private readonly Offset<byte> _noSmokingSignOffset = new Offset<byte>(0x341C);
        private readonly Offset<byte> _seatbeltSignOffset = new Offset<byte>(0x341D);

        // ---- Bloque SDK de PMDG (`Helpers/PmdgSdk`) ----
        // Solo se puebla con `[SDK] EnableDataBroadcast=1` en 737_Options.ini / 777_Options.ini. El 737
        // y el 777 no comparten direcciones (en el 737, 0x6584 es `AIR_FltAltWindow`), asi que se
        // registran las dos familias pero **solo se usan los offsets de la detectada**.
        // 737: MCP_annunCMD_A/B, CWS, FD, modos, autobrake y carteles.
        private readonly Offset<byte> _p737Powered    = new Offset<byte>(0x65E9);  // MCP_indication_powered
        private readonly Offset<byte> _p737CmdA       = new Offset<byte>(0x65E5);
        private readonly Offset<byte> _p737CmdB       = new Offset<byte>(0x65E7);
        private readonly Offset<byte> _p737CwsA       = new Offset<byte>(0x65E6);
        private readonly Offset<byte> _p737CwsB       = new Offset<byte>(0x65E8);
        private readonly Offset<byte> _p737Fd1        = new Offset<byte>(0x65D8);  // MCP_annunFD[2]
        private readonly Offset<byte> _p737Fd2        = new Offset<byte>(0x65D9);
        private readonly Offset<byte> _p737Lnav       = new Offset<byte>(0x65E0);
        private readonly Offset<byte> _p737Vnav       = new Offset<byte>(0x65DD);
        private readonly Offset<byte> _p737LvlChg     = new Offset<byte>(0x65DE);
        private readonly Offset<byte> _p737HdgSel     = new Offset<byte>(0x65DF);
        private readonly Offset<byte> _p737AltHold    = new Offset<byte>(0x65E3);
        private readonly Offset<byte> _p737Vs         = new Offset<byte>(0x65E4);
        private readonly Offset<byte> _p737VorLoc     = new Offset<byte>(0x65E1);
        private readonly Offset<byte> _p737App        = new Offset<byte>(0x65E2);
        private readonly Offset<byte> _p737N1         = new Offset<byte>(0x65DB);
        private readonly Offset<byte> _p737Speed      = new Offset<byte>(0x65DC);
        private readonly Offset<byte> _p737AtArm      = new Offset<byte>(0x65DA);
        private readonly Offset<byte> _p737Autobrake  = new Offset<byte>(0x6607);  // MAIN_AutobrakeSelector
        private readonly Offset<byte> _p737SeatBelts  = new Offset<byte>(0x64FB);  // COMM_FastenBeltsSelector
        private readonly Offset<byte> _p737NoSmoking  = new Offset<byte>(0x64FA);  // COMM_NoSmokingSelector
        // 777: MCP_annunAP[2], FD, modos, autobrake y carteles.
        private readonly Offset<byte> _p777Ap1        = new Offset<byte>(0x6584);  // MCP_annunAP[2]
        private readonly Offset<byte> _p777Ap2        = new Offset<byte>(0x6585);
        private readonly Offset<byte> _p777At         = new Offset<byte>(0x6586);
        private readonly Offset<byte> _p777Lnav       = new Offset<byte>(0x6587);
        private readonly Offset<byte> _p777Vnav       = new Offset<byte>(0x6588);
        private readonly Offset<byte> _p777Flch       = new Offset<byte>(0x6589);
        private readonly Offset<byte> _p777HdgHold    = new Offset<byte>(0x658A);
        private readonly Offset<byte> _p777Vs         = new Offset<byte>(0x658B);
        private readonly Offset<byte> _p777AltHold    = new Offset<byte>(0x658C);
        private readonly Offset<byte> _p777Loc        = new Offset<byte>(0x658D);
        private readonly Offset<byte> _p777App        = new Offset<byte>(0x658E);
        private readonly Offset<byte> _p777Fd1        = new Offset<byte>(0x6565);  // MCP_FD_Sw_On[2]
        private readonly Offset<byte> _p777Fd2        = new Offset<byte>(0x6566);
        private readonly Offset<byte> _p777Autobrake   = new Offset<byte>(0x64FE);  // BRAKES_AutobrakeSelector
        private readonly Offset<byte> _p777SeatBelts   = new Offset<byte>(0x6483);  // SIGNS_SeatBeltsSelector
        private readonly Offset<byte> _p777NoSmoking   = new Offset<byte>(0x6482);  // SIGNS_NoSmokingSelector

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
        /// <summary>0x0EC6 · BYTE · Fasten Seat Belts sign (0 = off, non-zero = on)</summary>

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
        private double _lastFlapsPercent;
        private int _lastParkingBrake;
        private int _lastEngineRpm;
        private bool _lastBeaconState;
        private bool _lastLandingState;
        private bool _lastTaxiState;
        private bool _pendingBeaconState;
        // Tren y spoilers: el offset rebota (1-2 s) y cada rebote era una línea del registro. El
        // evento solo sale cuando el valor nuevo se sostiene (`Helpers/StateChangeGate`, 2,5 s, el
        // mismo criterio que las luces). El estado que consume la telemetría no cambia.
        private readonly Helpers.StateChangeGate<int>  _gearGate     = new Helpers.StateChangeGate<int>(StateHoldSeconds);
        private readonly Helpers.StateChangeGate<bool> _spoilersGate = new Helpers.StateChangeGate<bool>(StateHoldSeconds);
        // Prueba de que el addon **publica** cada offset nuevo: sin un cambio en todo el vuelo se
        // publica «sin dato» en vez de inventarlo (`Helpers/SignalAvailability`).
        private readonly Helpers.SignalAvailability<int> _autobrakePanelSeen  = new Helpers.SignalAvailability<int>();
        private readonly Helpers.SignalAvailability<int> _autobrakeAirbusSeen = new Helpers.SignalAvailability<int>();
        private readonly Helpers.SignalAvailability<int> _cabinSignsSeen      = new Helpers.SignalAvailability<int>();
        // Bloque SDK de PMDG: familia detectada, última lectura y prueba de vida (la huella cambia).
        private readonly Helpers.SignalAvailability<int> _pmdgSdkSeen = new Helpers.SignalAvailability<int>();
        private Helpers.PmdgSdkRaw _pmdgRaw;
        /// <summary>Familia PMDG del avión cargado (`None` si no es PMDG).</summary>
        internal Helpers.PmdgFamily PmdgFamily { get; private set; } = Helpers.PmdgFamily.None;
        /// <summary>El bloque SDK de PMDG está alimentado (bandera `powered` o algo se movió).</summary>
        public bool PmdgSdkAlive { get; private set; }
        /// <summary>`nav_type` de la última posición, para no deducirlo dos veces.</summary>
        private int _lastNavType;
        // Últimas banderas de AP, para el payload de posición y el `nav_type` (`DetermineNavType`).
        private Helpers.AutopilotFlags _lastApFlags;
        private bool _lastApEngaged;
        private bool _pendingLandingState;
        private bool _pendingTaxiState;
        private DateTime _pendingBeaconAt = DateTime.MinValue;
        private DateTime _pendingLandingAt = DateTime.MinValue;
        private DateTime _pendingTaxiAt = DateTime.MinValue;

        // -- Debounce flaps --
        private DateTime _lastFlapsChangeTime = DateTime.MinValue;
        private const double FLAPS_DEBOUNCE_MS = 500;
        private const double FLAPS_HYSTERESIS = 1.0;

        // -- Categoría de planta motriz (detectada por título del avión) --
        private AircraftCategory _currentEngineCategory = AircraftCategory.Unknown;

        // Aeronaves con switch único beacon/strobe (encender strobe apaga beacon por diseño) —
        // deben mantener el guard NAV-o-BEACON en vez de exigir BEACON. Debe reflejar el mismo
        // set que Core/Flight/FlightManager.cs::BeaconStrobeSharedAircraft.
        private static readonly HashSet<string> _singleSwitchBeaconStrobeAircraft = new HashSet<string> { "DH8D" };

        // -- Debounce liftoff / touchdown --
        /// <summary>
        /// Confirmación de la bandera de suelo: ver <see cref="Helpers.GroundContactGate"/>. Sustituye a
        /// `GROUND_CONFIRM_FRAMES = 1` (que no confirmaba nada) y añade la confirmación del **despegue**,
        /// que no existía.
        /// </summary>
        private readonly Helpers.GroundContactGate _groundContactGate = new Helpers.GroundContactGate();
        private DateTime _lastTakeoffTime = DateTime.MinValue;
        private DateTime _lastTouchdownTime = DateTime.MinValue;

        // -- Reentrancy guard: prevents duplicate events if the timer fires before the previous tick completes --
        private int _isPolling = 0;

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
        // La confirmación de la bandera de suelo vive en `Helpers/GroundContactGate.cs`, que la exige
        // en **las dos** direcciones: aquí estaba en `GROUND_CONFIRM_FRAMES = 1` (o sea, ninguna) y el
        // paso a «aire» no tenía comprobación alguna, que es de donde salió el falso touch-and-go del
        // PIREP `RBmrBApLOjWlYmO2`.
        private const double EVENT_DEBOUNCE_SECONDS = 2.0;
        // Debounce largo para touchdown: evita doble disparo por rebote del avión en los
        // primeros segundos de rodadura (sim reporta OnGround=0 brevemente tras el primer contacto)
        private const double TOUCHDOWN_DEBOUNCE_SECONDS = 10.0;
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
        /// <summary>Terrain elevation below the aircraft in feet (FSUIPC 0x0020).</summary>
        public double CurrentGroundAltitudeFeet { get; private set; }

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

        // ---- COM1 / NAV1 ----
        /// <summary>COM1 active frequency in MHz (e.g. 118.5). 0 when not connected.</summary>
        public double Com1FrequencyMhz { get; private set; }
        /// <summary>NAV1 active frequency in MHz (e.g. 111.3). 0 when not connected.</summary>
        public double Nav1FrequencyMhz { get; private set; }
        /// <summary>NAV1 OBS / ILS course in degrees (0–359).</summary>
        public int Nav1ObsCourse { get; private set; }

        // ---- Controles ----
        public int CurrentGearPosition { get; private set; }
        public double CurrentFlapsPercent { get; private set; }
        public bool CurrentSpoilersDeployed { get; private set; }
        /// <summary>Posición real de los spoilers (0,0–1,0) según 0x0BD8.</summary>
        public double CurrentSpoilersPosition { get; private set; }
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
        public string AutobrakeLabel { get; private set; } = Helpers.AutobrakeSelector.NoData;
        /// <summary>El addon demostró que publica el selector de autobrake (ver `ReadAutobrake`).</summary>
        public bool AutobrakeAvailable { get; private set; }
        /// <summary>`0x0C46 AUTOBRAKES ACTIVE`: está **actuando** (no dice cuál está seleccionado).</summary>
        public bool AutobrakeActuating { get; private set; }

        // ---- Carteles de cabina (0x341C / 0x341D) ----
        /// <summary>Señal de cinturones encendida.</summary>
        public bool SeatbeltSignOn { get; private set; }
        /// <summary>Señal de no fumar encendida.</summary>
        public bool NoSmokingSignOn { get; private set; }
        /// <summary>El addon demostró que publica los carteles (alguno cambió en el vuelo).</summary>
        public bool CabinSignsAvailable { get; private set; }

        // ---- Motores ----
        public EnginePower Engine1Power { get; private set; }
        public EnginePower Engine2Power { get; private set; }
        public AircraftCategory EngineCategory => _currentEngineCategory;

        // ---- Aeronave ----
        public string AircraftTitle { get; private set; } = "Unknown";
        public string AircraftIcao { get; private set; } = "????";
        public string AircraftManufacturer { get; private set; } = "Unknown";
        public string AircraftModel { get; private set; } = "Unknown";

        /// <summary>
        /// **De dónde salió <see cref="AircraftIcao"/>**, que decide cuánto se puede afirmar de él.
        ///
        /// Importa porque no todos los simuladores publican el tipo: en X-Plane, XPUIPC deja vacío el
        /// modelo ATC (`0x0618`) y su `0x3D00` es el nombre de la **carpeta**. Medido el 20/10/2026 con
        /// el ToLiss A320neo cargado: `0x0618` = "", `0x3D00` = `ToLissA320_V1p2p1`, y el `.acf` del
        /// avión dice `A20N`. Antes de esto el cliente publicaba `A320` —deducido de la carpeta— y
        /// avisaba de discrepancia contra el OFP de A20N del propio piloto.
        /// </summary>
        public AircraftTypeSource AircraftIcaoSource { get; private set; } = AircraftTypeSource.Unknown;

        /// <summary>Raíz de la instalación de X-Plane, una vez resuelta (no se busca en cada ciclo).</summary>
        private string _xplaneRoot;

        /// <summary>Última consulta al `.acf`, cacheada por (raíz + carpeta): un vuelo no cambia de avión.</summary>
        private string _acfProbeKey;
        private string _acfProbeIcao = "";

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
            // Puede ser otra sesión y otro avión: la prueba de «este addon publica el offset» se vuelve
            // a exigir en cada arranque de la telemetría (`Helpers/SignalAvailability`).
            _autobrakePanelSeen.Reset();
            _autobrakeAirbusSeen.Reset();
            _cabinSignsSeen.Reset();
            _pmdgSdkSeen.Reset();
            _pollingTimer = new Timer(OnPollingTick, null, 500, _pollingIntervalMs);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _pollingTimer?.Dispose();
            _pollingTimer = null;
            // Wait for any in-flight OnPollingTick to finish before closing the FSUIPC
            // connection. Without this, Process() and Close() could race on the DLL's
            // internal offset list, causing "Collection was modified" at exit.
            while (Volatile.Read(ref _isPolling) != 0)
                Thread.SpinWait(10);
            Disconnect();
        }

        public void Dispose() => Stop();

        #endregion

        // =====================================================================
        #region Intervalo Adaptativo
        // =====================================================================

        /// <summary>
        /// Cadencia de reporte de posición según la fase. En rodaje la decide
        /// <see cref="UpdateIntervalPolicy.TaxiSeconds"/>: **5 s con la guía de rodaje activa** (es lo
        /// que hace medible la cobertura de la traza para NavData: con 30 s y 9–34 puntos por rodaje
        /// su métrica da mediana 65,0 % y mínima 22,2 %) y **el valor configurado sin ella**, para no
        /// multiplicar por 30 el tráfico del piloto que no usa la guía. La regla vive en el helper
        /// puro, no aquí. Ojo: esta es **la primera de tres puertas** — el suelo del envío y el
        /// deduplicador de posiciones de `TelemetryCoordinator` también capan la cadencia, y los tres
        /// los decide el mismo helper.
        /// </summary>
        public void SetUpdateIntervalForPhase(FlightPhase phase, bool taxiGuidanceActive)
        {
            switch (phase)
            {
                case FlightPhase.TaxiOut:
                case FlightPhase.TaxiIn:
                    _currentPhaseInterval =
                        UpdateIntervalPolicy.TaxiSeconds(taxiGuidanceActive, AppConfig.UpdateIntervalTaxi);
                    break;
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
            if (Interlocked.CompareExchange(ref _isPolling, 1, 0) != 0) return;
            try
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
                catch
                {
                    _connectionState = ConnectionState.Disconnected;
                    SimulatorName = "Desconocido";
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                _isPolling = 0;
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
            // El bit de STROBE puede quedar "pegado" en 1 en algunos addons de estudio tan pronto se
            // enciende cualquier luz exterior (confirmado en iFly B38M: NAV sola ON, BEACON físicamente
            // OFF, ya lee strobeRaw=true). BEACON es un prerequisito operativo real de STROBE (se
            // enciende antes o junto con él, nunca después) — exigirlo filtra el falso positivo sin
            // perder detecciones reales el resto del vuelo. NAV-o-BEACON (guard de v0.3.0, pensado solo
            // para el caso "todo apagado") se mantiene únicamente para aeronaves de switch compartido.
            bool strobeLightOn = _singleSwitchBeaconStrobeAircraft.Contains(AircraftIcao ?? "")
                ? strobeRaw && (beaconLightOn || navLightOn)
                : strobeRaw && beaconLightOn;
            // ── Autopiloto: las banderas de verdad ───────────────────────────
            // `0x07CC` es **el rumbo del bug** (grados*65536/360), no una bandera. Usarlo como «modo»
            // —como se hacía hasta v0.9.50— hacía que el AP saliera enganchado prácticamente siempre y
            // que los modos se dedujeran comparando un rumbo con 1, 2 y 3 (`Helpers/AutopilotPanel`).
            // Ahora se lee el maestro y los modos, que el documento marca `Responded` en MSFS, y el
            // rumbo solo se registra para el diagnóstico.
            // ── Autopiloto: manda el SDK de PMDG si está vivo ────────────────
            // La tabla estándar es del **AP del simulador**, y un PMDG vuela con su propio AFDS: en el
            // vuelo real `4Vz9nwaKKgQpYrEl` (738) las doce banderas salieron a 0 con el avión en
            // crucero manteniendo 23 990 ft. Con el bloque SDK alimentado se usan **sus** anunciadores
            // (`Helpers/PmdgSdk`); si no, las banderas estándar, que es lo que sirve a los demás addons.
            bool pmdgAp = PmdgSdkAlive && PmdgFamily != Helpers.PmdgFamily.None;

            bool   apMaster;
            string apNavMode;
            string apVertMode;
            string apPanel;

            if (pmdgAp)
            {
                apMaster   = Helpers.PmdgSdk.IsEngaged(_pmdgRaw);
                apNavMode  = Helpers.PmdgSdk.NavMode(_pmdgRaw);
                apVertMode = Helpers.PmdgSdk.VertMode(_pmdgRaw);
                apPanel    = Helpers.PmdgSdk.Describe(_pmdgRaw);
                _lastApFlags = new Helpers.AutopilotFlags
                {
                    Master         = apMaster,
                    NavLock        = _pmdgRaw.Lnav,
                    HeadingLock    = _pmdgRaw.HdgSel,
                    AltitudeLock   = _pmdgRaw.AltHold,
                    VerticalHold   = _pmdgRaw.Vs,
                    AirspeedHold   = _pmdgRaw.Speed,
                    GlideslopeHold = _pmdgRaw.Loc,
                    ApproachHold   = _pmdgRaw.App,
                    FlightDirector = _pmdgRaw.Fd1 || _pmdgRaw.Fd2,
                };
            }
            else
            {
                var apFlags = new Helpers.AutopilotFlags
                {
                    Available      = _apAvailableOffset.Value != 0,
                    Master         = _apMasterOffset.Value != 0,
                    NavLock        = _apNavLockOffset.Value != 0,
                    HeadingLock    = _apHeadingLockOffset.Value != 0,
                    AltitudeLock   = _apAltitudeLockOffset.Value != 0,
                    AirspeedHold   = _apAirspeedHoldOffset.Value != 0,
                    MachHold       = _apMachHoldOffset.Value != 0,
                    VerticalHold   = _apVerticalHoldOffset.Value != 0,
                    GlideslopeHold = _apGlideslopeHoldOffset.Value != 0,
                    ApproachHold   = _apApproachHoldOffset.Value != 0,
                    BackCourse     = _apBackCourseOffset.Value != 0,
                    FlightDirector = _apFlightDirectorOffset.Value != 0,
                };
                apMaster     = Helpers.AutopilotPanel.IsEngaged(apFlags);
                apNavMode    = Helpers.AutopilotPanel.NavMode(apFlags);
                apVertMode   = Helpers.AutopilotPanel.VertMode(apFlags);
                apPanel      = Helpers.AutopilotPanel.Describe(apFlags);
                _lastApFlags = apFlags;
            }

            _lastApEngaged = apMaster;
            // `nav_type` de la posición: 0 nada, 1 navegación lateral, 3 ILS/GS. Se decide aquí, con la
            // misma información que el modo, y no en `DetermineNavType` mirando banderas sueltas.
            _lastNavType = !apMaster ? 0
                         : (_lastApFlags.GlideslopeHold || _lastApFlags.ApproachHold) ? 3
                         : _lastApFlags.NavLock ? 1
                         : 0;

            int apHeadingBugDeg = (int)Math.Round((((int)_apHeadingBugOffset.Value) & 0xFFFF) * 360.0 / 65536.0) % 360;

            // ── Autobrake y carteles de cabina ────────────────────────────────
            string autobrakeSetting = AutobrakeLabel;
            int seatbeltRaw;
            int noSmokingRaw;
            string cabinPanel;
            if (pmdgAp)
            {
                // El bloque SDK vivo implica que PMDG publica los selectores de carteles.
                seatbeltRaw  = _pmdgRaw.SeatBeltsRaw;
                noSmokingRaw = _pmdgRaw.NoSmokingRaw;
                CabinSignsAvailable = true;
                cabinPanel = $"CAB cint={Helpers.PmdgSdk.SignLabel(seatbeltRaw)} nofum={Helpers.PmdgSdk.SignLabel(noSmokingRaw)} publicado=si (SDK)";
            }
            else
            {
                seatbeltRaw  = _seatbeltSignOffset.Value;
                noSmokingRaw = _noSmokingSignOffset.Value;
                // Los carteles solo se dan por publicados cuando el piloto **toca** alguno en el vuelo
                // (`Helpers/SignalAvailability`): si el addon no los escribe, se callan en vez de suponer.
                _cabinSignsSeen.Update(seatbeltRaw * 2 + noSmokingRaw);
                CabinSignsAvailable = _cabinSignsSeen.Available;
                cabinPanel = HelperSigns(seatbeltRaw, noSmokingRaw);
            }
            // `AUTO` (1) no es «encendido»: lo decide el avión según la fase.
            SeatbeltSignOn  = pmdgAp ? Helpers.PmdgSdk.SeatbeltsOn(_pmdgRaw)  : seatbeltRaw != 0;
            NoSmokingSignOn = pmdgAp ? Helpers.PmdgSdk.NoSmokingOn(_pmdgRaw)  : noSmokingRaw != 0;
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

            // N2 (DWORD 0–16384 → %)
            float n2_1 = (float)(_eng1N2Offset.Value / 16384.0 * 100.0);
            float n2_2 = (float)(_eng2N2Offset.Value / 16384.0 * 100.0);
            // Reverser ratio (FLOAT64 0.0–1.0 → %)
            float rev1Pct = (float)(_eng1ReverserOffset.Value * 100.0);
            float rev2Pct = (float)(_eng2ReverserOffset.Value * 100.0);

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

            // Hotel mode: gas generator running but propeller locked (e.g. ATR72 Hotel/Ground power)
            bool hotelModeActive = _currentEngineCategory == AircraftCategory.Turboprop
                && ((eng1Running && propRpm_1 < 50f) || (eng2Running && propRpm_2 < 50f));

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
                GForce = CurrentGForce,
                IndicatedAirspeedKt = CurrentIndicatedAirspeed,
                IsOnGround = _lastOnGround,
                FuelLbs = CurrentFuelLbs,
                Transponder = _transponderOffset.Value,
                RadarAltitudeFeet = CurrentRadarAltitudeFeet,
                GroundAltitudeFeet = CurrentGroundAltitudeFeet,
                PitchDeg = CurrentPitch,
                BankDeg = CurrentBank,
                SpoilersDeployed = CurrentSpoilersDeployed,
                FlapsPercent = FlapsPercent,
                // El detente real, ya cruzado con el porcentaje: `0x0BFC` a 0 es ambiguo («arriba» o
                // «el addon no lo escribe»), y `FlapSetting.DetentOrNull` solo lo da por bueno cuando
                // el mando no lo desmiente. Sin dato va `null`, no 0.
                FlapsIndex = FlapSetting.DetentOrNull(FlapsIndex, FlapsPercent),
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

                // ── Autopilot ─────────────────────────────────────────────────
                                AutopilotEngaged = apMaster,
                ApPanel          = apPanel,
                CabinPanel       = cabinPanel,
                PmdgSdkAlive     = PmdgSdkAlive,
                ApHeadingBugDeg  = apHeadingBugDeg,
                SeatbeltSignOn   = SeatbeltSignOn,
                NoSmokingSignOn  = NoSmokingSignOn,
                CabinSignsAvailable  = CabinSignsAvailable,
                AutobrakeAvailable   = AutobrakeAvailable,
                AutobrakeActuating   = AutobrakeActuating,
                ApMaster = apMaster,
                ApNavMode = apNavMode,
                ApVertMode = apVertMode,

                // ── Meteorología ──────────────────────────────────────────────
                AircraftQnhMb = aircraftQnhMb,
                OatCelsius    = CurrentOatCelsius,

                // ── COM1 / NAV1 ───────────────────────────────────────────────
                Com1FrequencyMhz = Com1FrequencyMhz,
                Nav1FrequencyMhz = Nav1FrequencyMhz,
                Nav1ObsCourse    = Nav1ObsCourse,

                // ── Categoría y estado de motores ─────────────────────────────
                EngineCategory = _currentEngineCategory,
                Eng1Running = eng1Running,
                Eng2Running = eng2Running,
                TakeoffPowerSet = takeoffPowerSet,
                HotelModeActive = hotelModeActive,

                // Jet (N1 / N2 / Reverser)
                N1_1    = n1_1,
                N1_2    = n1_2,
                N2_1    = n2_1,
                N2_2    = n2_2,
                Rev1Pct = rev1Pct,
                Rev2Pct = rev2Pct,

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

            // 0x31E4: radio altitude in metres × 65536 → divide by 65536 first, then metres → feet
            int radarRaw = _radarAltitudeOffset.Value;
            CurrentRadarAltitudeFeet = radarRaw > 0 ? (radarRaw / 65536.0) * 3.28084 : 0.0;

            // 0x0020: terrain elevation in metres × 65536 → feet
            int groundRaw = _groundAltitudeOffset.Value;
            CurrentGroundAltitudeFeet = groundRaw > 0 ? (groundRaw / 65536.0) * 3.28084 : 0.0;

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

            // ---- COM1 / NAV1 frequency (BCD) and OBS course ----
            Com1FrequencyMhz = DecodeNav1Bcd(_com1FreqOffset.Value);
            Nav1FrequencyMhz = DecodeNav1Bcd(_nav1FreqOffset.Value);
            Nav1ObsCourse    = _nav1ObsCourseOffset.Value & 0x1FF; // 9-bit, 0–359

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
            // La posición, no el booleano: `> 0` contaba como desplegado el 0,006 % de recorrido y el temblor
// del indicador escribía una línea por rebote (`Helpers/SpoilersPosition`).
            CurrentSpoilersPosition = Helpers.SpoilersPosition.FromRaw(_spoilersOffset.Value);
            CurrentSpoilersDeployed = Helpers.SpoilersPosition.IsDeployed(CurrentSpoilersPosition);

            // ---- Motores ----
            Engine1Power = DecodeEnginePower(_eng1N1Int.Value, (short)(_engRpmOffset.Value & 0xFFFF));
            Engine2Power = DecodeEnginePower(_eng2N1Int.Value, 0);

            // ---- Bloque SDK de PMDG y autobrake ----
            // El SDK va **antes** que el autobrake porque, si está vivo, es él quien manda sobre
            // `0x2F80` (que PMDG no escribe: ver `Helpers/PmdgSdk`).
            ReadPmdgSdk();
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



        /// <summary>
        /// Lee el bloque SDK de PMDG (`Helpers/PmdgSdk`). Se leen **solo** los offsets de la familia
        /// detectada: el 737 y el 777 no comparten direcciones, y leer «la otra» daría basura.
        ///
        /// **La prueba de vida.** El 737 publica `0x65E9 MCP_indication_powered`, que es un sí o un no
        /// directo; el 777 no tiene nada equivalente, así que sirve la huella: si algo del bloque cambia
        /// alguna vez, está alimentado. Sin ninguna de las dos el bloque está **apagado** —falta
        /// `EnableDataBroadcast`— y no se usa: se sigue con los offsets estándar, que es lo que había.
        /// </summary>
        private void ReadPmdgSdk()
        {
            PmdgFamily = Helpers.PmdgSdk.FamilyFrom(AircraftTitle, AircraftIcao);

            if (PmdgFamily == Helpers.PmdgFamily.None)
            {
                _pmdgSdkSeen.Reset();
                _pmdgRaw = default(Helpers.PmdgSdkRaw);
                PmdgSdkAlive = false;
                return;
            }

            var raw = new Helpers.PmdgSdkRaw { Family = PmdgFamily };

            if (PmdgFamily == Helpers.PmdgFamily.B737)
            {
                raw.IndicationPowered = _p737Powered.Value != 0;
                raw.CmdA     = _p737CmdA.Value != 0;
                raw.CmdB     = _p737CmdB.Value != 0;
                raw.CwsA     = _p737CwsA.Value != 0;
                raw.CwsB     = _p737CwsB.Value != 0;
                raw.Fd1      = _p737Fd1.Value != 0;
                raw.Fd2      = _p737Fd2.Value != 0;
                raw.Lnav     = _p737Lnav.Value != 0;
                raw.Vnav     = _p737Vnav.Value != 0;
                raw.Flch     = _p737LvlChg.Value != 0;
                raw.HdgSel   = _p737HdgSel.Value != 0;
                raw.AltHold  = _p737AltHold.Value != 0;
                raw.Vs       = _p737Vs.Value != 0;
                raw.Loc      = _p737VorLoc.Value != 0;
                raw.App      = _p737App.Value != 0;
                raw.N1       = _p737N1.Value != 0;
                raw.Speed    = _p737Speed.Value != 0;
                raw.AtArm    = _p737AtArm.Value != 0;
                raw.AutobrakeRaw  = _p737Autobrake.Value;
                raw.SeatBeltsRaw  = _p737SeatBelts.Value;
                raw.NoSmokingRaw  = _p737NoSmoking.Value;
            }
            else
            {
                raw.Ap1      = _p777Ap1.Value != 0;
                raw.Ap2      = _p777Ap2.Value != 0;
                raw.AtArm    = _p777At.Value != 0;
                raw.Lnav     = _p777Lnav.Value != 0;
                raw.Vnav     = _p777Vnav.Value != 0;
                raw.Flch     = _p777Flch.Value != 0;
                raw.HdgSel   = _p777HdgHold.Value != 0;
                raw.Vs       = _p777Vs.Value != 0;
                raw.AltHold  = _p777AltHold.Value != 0;
                raw.Loc      = _p777Loc.Value != 0;
                raw.App      = _p777App.Value != 0;
                raw.Fd1      = _p777Fd1.Value != 0;
                raw.Fd2      = _p777Fd2.Value != 0;
                raw.AutobrakeRaw  = _p777Autobrake.Value;
                raw.SeatBeltsRaw  = _p777SeatBelts.Value;
                raw.NoSmokingRaw  = _p777NoSmoking.Value;
            }

            _pmdgRaw = raw;
            _pmdgSdkSeen.Update(Helpers.PmdgSdk.Fingerprint(raw));
            PmdgSdkAlive = raw.IndicationPowered || _pmdgSdkSeen.Available;
        }


        /// <summary>
        /// Lee el **selector** de autobrake con la tabla del documento (`Helpers/AutobrakeSelector`) y
        /// publica «sin dato» mientras el addon no demuestre que lo escribe (`SignalAvailability`).
        ///
        /// Hasta v0.9.50 se leía `0x0C46` (`AUTOBRAKES ACTIVE`, una bandera de «está actuando», sin
        /// `Responded` en MSFS) y se decodificaba con la tabla del selector; encima el enum y
        /// `GetAutobrakeName` tenían el 0 y el 1 cambiados, así que el registro decía «RTO» en todos
        /// los vuelos. Ahora el ajuste sale de `0x2F80` (el único `Responded`) o, en el A320neo de MSFS,
        /// de `0x0260`; `0x0C46` queda solo como «está actuando».
        /// </summary>
        private void ReadAutobrake()
        {
            // Si el bloque SDK de PMDG está vivo, el selector bueno es el suyo: PMDG **no** escribe
            // `0x2F80` (el piloto lo movió en `4Vz9nwaKKgQpYrEl` y el offset no cambió en todo el vuelo).
            if (PmdgSdkAlive && PmdgFamily != Helpers.PmdgFamily.None)
            {
                AutobrakeLabel      = Helpers.PmdgSdk.AutobrakeLabel(_pmdgRaw);
                AutobrakeAvailable  = true;
                AutobrakeActuating  = _autobrakeActiveOffset.Value != 0;
                return;
            }

            int panel  = _autobrakePanelOffset.Value;
            int airbus = _autobrakeAirbusOffset.Value;

            _autobrakePanelSeen.Update(panel);
            _autobrakeAirbusSeen.Update(airbus);

            if (_autobrakePanelSeen.Available)
                AutobrakeLabel = Helpers.AutobrakeSelector.PanelLabel(panel);
            else if (_autobrakeAirbusSeen.Available)
                AutobrakeLabel = Helpers.AutobrakeSelector.AirbusLabel(airbus);
            else
                AutobrakeLabel = Helpers.AutobrakeSelector.NoData;

            AutobrakeAvailable = _autobrakePanelSeen.Available || _autobrakeAirbusSeen.Available;
            AutobrakeActuating = _autobrakeActiveOffset.Value != 0;
        }

        /// <summary>Línea del diagnóstico de cabina: los dos carteles y si el addon los publica.</summary>
        private string HelperSigns(int seatbeltRaw, int noSmokingRaw) =>
            $"CAB: seatbelt={seatbeltRaw} nosmoking={noSmokingRaw} " +
            $"publicado={(CabinSignsAvailable ? "si" : "no")}";
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
        /// Decodes a BCD-encoded VHF radio frequency (FSUIPC 0x034E/0x0350) to MHz.
        /// Format: (freq - 100) × 100 stored as 4 BCD digits D3 D2 D1 D0.
        /// e.g. 110.70 MHz → 1070 → 0x1070 → 100 + 1×10 + 0 + 7×0.1 + 0×0.01 = 110.70
        ///      111.30 MHz → 1130 → 0x1130 → 100 + 1×10 + 1 + 3×0.1 + 0×0.01 = 111.30
        /// </summary>
        private static double DecodeNav1Bcd(short raw)
        {
            if (raw == 0) return 0.0;
            int d3 = (raw >> 12) & 0xF;
            int d2 = (raw >>  8) & 0xF;
            int d1 = (raw >>  4) & 0xF;
            int d0 =  raw        & 0xF;
            return 100.0 + d3 * 10.0 + d2 + d1 * 0.1 + d0 * 0.01;
        }

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
            bool landingOn = (lights & 0x04) != 0;
            bool taxiOn = (lights & 0x08) != 0;
            bool strobeOnRaw = (lights & 0x10) != 0;
            bool strobeOn = _singleSwitchBeaconStrobeAircraft.Contains(AircraftIcao ?? "")
                ? strobeOnRaw && (beaconOn || navOn)
                : strobeOnRaw && beaconOn;

            DetectNavChange(navOn);
            DetectStrobeChange(strobeOn);
            DetectLandingLightChange(landingOn);
            DetectTaxiLightChange(taxiOn);
        }

        /// <summary>
        /// Traduce la bandera de suelo del simulador a toque / despegue **confirmados**
        /// (<see cref="Helpers.GroundContactGate"/>). Hasta v0.9.42 el paso a «aire» no tenía
        /// confirmación ninguna: un solo frame con la bandera en falso ponía `_lastOnGround` en falso, y
        /// esa bandera es la que llega a la máquina de fases. El PIREP real `RBmrBApLOjWlYmO2` se fue a
        /// `Climb` por eso y el piloto se quedó sin poder enviar el vuelo.
        /// </summary>
        private void DetectGroundTransition()
        {
            switch (_groundContactGate.Update(_onGroundOffset.Value == 1))
            {
                case Helpers.GroundContactStep.Touchdown: HandleTouchdown(); break;
                case Helpers.GroundContactStep.Liftoff:   HandleTakeoff();   break;
            }

            // El estado de la telemetría es el **confirmado**, no el del último frame.
            _lastOnGround = _groundContactGate.IsOnGround;
        }

        private void DetectGearChange()
        {
            int gear = CurrentGearPosition;
            int previous;
            if (_gearGate.Update(DateTime.UtcNow, gear, out previous))
                GearChanged?.Invoke(previous, gear);
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
            bool deployed = CurrentSpoilersDeployed;
            bool previous;
            if (_spoilersGate.Update(DateTime.UtcNow, deployed, out previous))
                SpoilersChanged?.Invoke(deployed);
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

        // ── Nav y Strobe: debounce y eventos ─────────────────────────────────────
        private bool _lastNavState;
        private bool _lastStrobeState;
        private bool _pendingNavState;
        private bool _pendingStrobeState;
        private DateTime _pendingNavAt = DateTime.MinValue;
        private DateTime _pendingStrobeAt = DateTime.MinValue;
        private const double LightHoldSeconds = 2.5;

        /// <summary>
        /// Sostenido para dar por bueno un cambio de tren o de spoilers. El offset de spoilers del
        /// 777 rebota con rachas de 1-2 s (PIREP `5ow69vzDNW5ZMayB`: `EXTENDIDOS` 13:17:46 →
        /// `RETRAÍDOS` 13:17:49 → `RETRAÍDOS` 13:17:50), así que se exige el mismo sostenido que a
        /// las luces antes de escribir la línea del registro.
        /// </summary>
        private const double StateHoldSeconds = 2.5;

        public event Action<bool> NavLightChanged;
        public event Action<bool> StrobeLightChanged;
        public event Action<bool> LandingLightChanged;
        public event Action<bool> TaxiLightChanged;

        private void DetectNavChange(bool navOn)
        {
            if (navOn == _lastNavState) { _pendingNavAt = DateTime.MinValue; return; }
            if (navOn != _pendingNavState || _pendingNavAt == DateTime.MinValue)
            { _pendingNavState = navOn; _pendingNavAt = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - _pendingNavAt).TotalSeconds < LightHoldSeconds) return;
            _lastNavState = navOn; _pendingNavAt = DateTime.MinValue;
            NavLightChanged?.Invoke(navOn);
        }

        private void DetectStrobeChange(bool strobeOn)
        {
            if (strobeOn == _lastStrobeState) { _pendingStrobeAt = DateTime.MinValue; return; }
            if (strobeOn != _pendingStrobeState || _pendingStrobeAt == DateTime.MinValue)
            { _pendingStrobeState = strobeOn; _pendingStrobeAt = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - _pendingStrobeAt).TotalSeconds < LightHoldSeconds) return;
            _lastStrobeState = strobeOn; _pendingStrobeAt = DateTime.MinValue;
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
            bool current = IsBeaconOn;
            if (current == _lastBeaconState) { _pendingBeaconAt = DateTime.MinValue; return; }
            if (current != _pendingBeaconState || _pendingBeaconAt == DateTime.MinValue)
            { _pendingBeaconState = current; _pendingBeaconAt = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - _pendingBeaconAt).TotalSeconds < LightHoldSeconds) return;
            _lastBeaconState = current; _pendingBeaconAt = DateTime.MinValue;
            BeaconChanged?.Invoke(current);
        }

        private void DetectLandingLightChange(bool landingOn)
        {
            if (landingOn == _lastLandingState) { _pendingLandingAt = DateTime.MinValue; return; }
            if (landingOn != _pendingLandingState || _pendingLandingAt == DateTime.MinValue)
            { _pendingLandingState = landingOn; _pendingLandingAt = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - _pendingLandingAt).TotalSeconds < LightHoldSeconds) return;
            _lastLandingState = landingOn; _pendingLandingAt = DateTime.MinValue;
            LandingLightChanged?.Invoke(landingOn);
        }

        private void DetectTaxiLightChange(bool taxiOn)
        {
            if (taxiOn == _lastTaxiState) { _pendingTaxiAt = DateTime.MinValue; return; }
            if (taxiOn != _pendingTaxiState || _pendingTaxiAt == DateTime.MinValue)
            { _pendingTaxiState = taxiOn; _pendingTaxiAt = DateTime.UtcNow; return; }
            if ((DateTime.UtcNow - _pendingTaxiAt).TotalSeconds < LightHoldSeconds) return;
            _lastTaxiState = taxiOn; _pendingTaxiAt = DateTime.MinValue;
            TaxiLightChanged?.Invoke(taxiOn);
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
            if ((DateTime.UtcNow - _lastTouchdownTime).TotalSeconds < 30) return;
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
            if ((DateTime.UtcNow - _lastTouchdownTime).TotalSeconds < TOUCHDOWN_DEBOUNCE_SECONDS) return;
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

                // ← G pico del approach completo; Math.Max incluye el frame de impacto
                // en caso de que el pico ocurra exactamente en la transición OnGround
                GForcePeak = Math.Max(_peakGforceApproach, CurrentGForce),
                GForceAtTouch = CurrentGForce,

                FlapsPosition = CurrentFlapsPercent / 100.0,
                SpoilersPosition = CurrentSpoilersPosition,
                AutoBrakeLabel   = AutobrakeLabel,
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
                AutopilotEngaged = _lastApEngaged,
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
        /// `nav_type` que viaja en la posicion. Se deduce de las **banderas de modo** reales y no del
        /// rumbo del bug (`0x07CC`), que es lo que se hacia hasta v0.9.50 y devolvia 0 salvo que el
        /// rumbo estuviera entre 1 y 3 grados: 1 = navegacion lateral (NAV/LNAV), 3 = ILS/GS, 0 = nada.
        /// </summary>
        private int DetermineNavType() => _lastNavType;

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

                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                _connectionState = ConnectionState.Disconnected;
                _isReconnecting = false;
                _connectionRetryCount++;

                _currentBackoffMs = Math.Min(
                    30000,
                    1000 * (int)Math.Pow(2, Math.Min(5, _connectionRetryCount)));

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
                }

                // ¿El simulador **publica** el tipo? El modelo ATC (0x0618) es la respuesta de MSFS y
                // P3D; en X-Plane/XPUIPC llega vacío. Saber cuál de las dos cosas pasó es la mitad del
                // arreglo: un designador publicado se puede comparar exacto, uno deducido no.
                bool published = false;
                TryReadStringOffset(() => _icaoDesignator.Value,
                    val => { if (val != "????") { AircraftIcao = val; published = true; } });
                TryReadStringOffset(() => _icaoManufacturer.Value,
                    val => AircraftManufacturer = val);
                TryReadStringOffset(() => _icaoModel.Value,
                    val => AircraftModel = val);

                if (published)
                {
                    AircraftIcaoSource = AircraftTypeSource.AtcModel;
                }
                else
                {
                    // X-Plane: el tipo real está en el `_ICAO` del `.acf` del avión cargado. El `0x3D00`
                    // de XPUIPC es el nombre de la carpeta (`ToLissA320_V1p2p1`), así que sin esto el
                    // avión se identificaba con lo que su carpeta parece decir —y un A320neo salía
                    // como A320—. Si la lectura falla, se degrada al título, pero marcado como
                    // suposición: entonces la comparación contra el OFP se queda en la familia.
                    string fromAcf = TryReadXPlaneAcfIcao();
                    if (fromAcf.Length > 0)
                    {
                        AircraftIcao = fromAcf;
                        AircraftIcaoSource = AircraftTypeSource.AcfFile;
                    }
                }

                if (AircraftIcao == "????" && !string.IsNullOrEmpty(AircraftTitle))
                {
                    AircraftIcao = ExtractIcaoFromTitle(AircraftTitle);
                    AircraftIcaoSource = AircraftTypeSource.TitleGuess;
                }

                OnAircraftInfoReady?.Invoke();
            }
            catch { }
        }

        /// <summary>
        /// **El designador ICAO del avión cargado en X-Plane, leído de su `.acf`.**
        ///
        /// Existe porque XPUIPC no expone el dato en ningún offset (medido: `0x0600`, `0x0618`,
        /// `0x0630`, `0x09D2`, `0x0B26` y `0x3138` devuelven cadena vacía, y su binario ni siquiera
        /// referencia el dataref `acf_ICAO`). Lo que sí llega por `0x3D00` es el nombre de la
        /// **carpeta** del avión, que es lo que se usa para localizarlo: `<X-Plane>\Aircraft\&lt;carpeta&gt;`.
        ///
        /// La lectura es **acotada y del avión que el simulador dice tener cargado**: dos rutas
        /// construidas con ese nombre y, si ninguna existe, ninguna otra —no se recorre la carpeta de
        /// aviones ni, por supuesto, la de community—. Devuelve `""` sin dato, sin simulador de
        /// X-Plane o si los `.acf` de la carpeta no coinciden en su `_ICAO`; y entonces decide la
        /// degradación de `ReadAircraftInfo`, nunca una excepción.
        /// </summary>
        private string TryReadXPlaneAcfIcao()
        {
            try
            {
                string root = XPlaneInstallPath();
                if (string.IsNullOrEmpty(root)) return "";

                string key = root + "|" + AircraftTitle;
                if (string.Equals(_acfProbeKey, key, StringComparison.Ordinal)) return _acfProbeIcao;

                _acfProbeKey = key;
                _acfProbeIcao = "";

                string aircraftRoot = Path.Combine(root, "Aircraft");
                foreach (string directory in XPlaneAcfIdentity.CandidateDirectories(aircraftRoot, AircraftTitle))
                {
                    if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) continue;

                    string[] acfFiles;
                    try { acfFiles = Directory.GetFiles(directory, "*.acf"); }
                    catch { continue; }
                    if (acfFiles.Length == 0) continue;

                    var texts = new List<string>(acfFiles.Length);
                    foreach (string acf in acfFiles)
                    {
                        try { texts.Add(ReadAcfHeader(acf)); }
                        catch { texts.Add(""); }          // un .acf ilegible invalida la carpeta, no el vuelo
                    }

                    string icao = XPlaneAcfIdentity.IcaoFromAcfTexts(texts);
                    if (icao.Length > 0)
                    {
                        _acfProbeIcao = icao;
                        return icao;
                    }
                }

                return "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// El `.acf` es un volcado de texto de ~2,3 MB y la clave está al final (línea 63.505 de
        /// `a320.acf`), así que se lee entero pero **solo lo que hay entre la clave y esa línea**: no
        /// se guarda nada más. De la carpeta se leen todos los `.acf` porque X-Plane carga uno según la
        /// versión, y el designador solo se acepta si coinciden (ver `XPlaneAcfIdentity`).
        /// </summary>
        private static string ReadAcfHeader(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.TrimStart().StartsWith(XPlaneAcfIdentity.IcaoKey, StringComparison.Ordinal))
                        return line;                       // la clave y su valor van en la misma línea
                }
            }

            return "";
        }

        /// <summary>
        /// **La raíz de X-Plane, del proceso del simulador.** Es la única forma de llegar al `.acf` sin
        /// escanear el disco: el `0x3D00` da el nombre de la carpeta y el proceso da dónde está su
        /// `Aircraft\`. Se resuelve una vez; si el proceso no está o su ruta no se deja leer, se
        /// devuelve `null` y el tipo se queda sin leer (degradación, no excepción).
        /// </summary>
        private string XPlaneInstallPath()
        {
            if (!string.IsNullOrEmpty(_xplaneRoot)) return _xplaneRoot;

            try
            {
                var processes = Process.GetProcessesByName("X-Plane");
                if (processes.Length == 0) return null;

                string exe = processes[0].MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exe)) return null;

                string directory = Path.GetDirectoryName(exe);
                if (string.IsNullOrWhiteSpace(directory)) return null;

                _xplaneRoot = directory;
                return _xplaneRoot;
            }
            catch { return null; }
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
            // **Esto es una suposición, no un dato**: el título de X-Plane es el nombre de la carpeta
            // del avión, y aquí solo hay subcadenas. Sirve para que el log diga algo y para comparar
            // por familia (`AircraftTypeSource.TitleGuess`), nunca para decidir una variante: al
            // ToLiss A320neo le sale `A320` porque su carpeta se llama `ToLissA320_V1p2p1`, y su
            // `_ICAO` de verdad es `A20N` (lo lee `TryReadXPlaneAcfIcao`). Y ojo con el orden: se
            // recorre el diccionario tal cual, así que `A321` gana a `A320` cuando están las dos.
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

        public string GetAircraftLivery() => Helpers.AircraftLivery.FromTitle(AircraftTitle);

        /// <summary>
        /// El desarrollador del addon según el **título**, o vacío si el título no lo nombra.
        ///
        /// La tabla y la regla viven en <see cref="Helpers.AircraftIdentity.AddonFromTitle"/>, que es
        /// la **misma** que usa la cabecera del gráfico del flare: dos listas separadas —una para el
        /// «✈️ B738 [PMDG]» del log y otra para el gráfico— se habrían separado en cuanto alguien
        /// añadiera un addon en una sola.
        /// </summary>
        public string GetAircraftDeveloper()
            => Helpers.AircraftIdentity.AddonFromTitle(AircraftTitle) ?? string.Empty;

        /// <summary>
        /// **El peso bruto del avión en libras** (`FSUIPC.PayloadServices.GrossWeightLbs`, «The total
        /// current weight of the aircraft in Pounds»), o `null` si no se puede leer.
        ///
        /// Es el **LTOW** cuando se llama en el instante del contacto, y por eso **no se sondea a
        /// 20 Hz**: el dato solo hace falta una vez por vuelo, y `RefreshData()` recorre las
        /// estaciones de carga de la aeronave. Se pide en el aterrizaje y no en cada ciclo.
        ///
        /// **No se inventa nada**: sin simulador conectado, si el servicio no está disponible o si el
        /// resultado no es un peso positivo, devuelve `null` y el bloque de datos omite la línea. Un
        /// addon que no publica sus estaciones de carga da 0, y un 0 ahí sería un avión sin peso.
        /// </summary>
        public double? ReadGrossWeightLbs()
        {
            if (_connectionState != ConnectionState.Connected) return null;

            try
            {
                var payload = FSUIPCConnection.PayloadServices;
                if (payload == null) return null;

                payload.RefreshData();
                double lbs = payload.GrossWeightLbs;
                return lbs > 0.0 && !double.IsNaN(lbs) && !double.IsInfinity(lbs) ? (double?)lbs : null;
            }
            catch
            {
                // Sin lectura no hay dato: no se propaga la excepción al hilo de telemetría por un
                // adorno del gráfico.
                return null;
            }
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
        /// <summary>
        /// Factor de carga actual (`0x11BA` / 625). Ya se leía para el pico de la aproximación;
        /// ahora también alimenta la detección de turbulencia (`Helpers/TurbulenceAdvisor`).
        /// </summary>
        public double GForce { get; set; }
        public double IndicatedAirspeedKt { get; set; }
        public bool IsOnGround { get; set; }
        public double FuelLbs { get; set; }
        public int Transponder { get; set; }
        public bool AutopilotEngaged { get; set; }
        public double RadarAltitudeFeet { get; set; }
        /// <summary>Terrain elevation below the aircraft in feet (FSUIPC 0x0020). Used for AGL in Enroute.</summary>
        public double GroundAltitudeFeet { get; set; }
        public double PitchDeg { get; set; }
        public double BankDeg { get; set; }
        public bool SpoilersDeployed { get; set; }
        public double FlapsPercent { get; set; }
        /// <summary>
        /// **El detente real** (`0x0BFC`, «Notch actual»), o `null` si el avión no lo publica. No es
        /// lo mismo que <see cref="FlapsPercent"/>: ese es el recorrido del mando y este la compuerta.
        /// Se decide con `FlapSetting.DetentOrNull`, que descarta el 0 de los addons que no escriben
        /// el offset en vez de guardarlo como «flaps arriba».
        /// </summary>
        public int? FlapsIndex { get; set; }
        public string FlapsLabel { get; set; } = "UP";
        public bool FlapsInTransit { get; set; }
        public bool GearDown { get; set; }
        public int Order { get; set; }

        // Frenos y motores
        public bool ParkingBrakeOn { get; set; }
        public bool EnginesRunning { get; set; }
        public string AutobrakeSetting { get; set; } = Helpers.AutobrakeSelector.NoData;

        // Luces
        public bool NavLightOn { get; set; }
        public bool BeaconLightOn { get; set; }
        public bool LandingLightOn { get; set; }
        public bool TaxiLightOn { get; set; }
        public bool StrobeLightOn { get; set; }

        // Motores Jet (N1 / N2 / Reverser)
        public float N1_1    { get; set; }
        public float N1_2    { get; set; }
        /// <summary>N2 high-pressure spool % for engine 1; 0 when addon does not write offset 0x2220.</summary>
        public float N2_1    { get; set; }
        /// <summary>N2 high-pressure spool % for engine 2; 0 when addon does not write offset 0x2420.</summary>
        public float N2_2    { get; set; }
        /// <summary>Thrust reverser deployment % for engine 1 (0–100); from FLOAT64 offset 0x207C.</summary>
        public float Rev1Pct { get; set; }
        /// <summary>Thrust reverser deployment % for engine 2 (0–100); from FLOAT64 offset 0x217C.</summary>
        public float Rev2Pct { get; set; }

        // Identificación de planta motriz
        public FsuipcService.AircraftCategory EngineCategory { get; set; }
        public bool Eng1Running { get; set; }
        public bool Eng2Running { get; set; }
        public bool TakeoffPowerSet { get; set; }
        public bool HotelModeActive { get; set; }

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
        /// <summary>QNH seleccionado en el altímetro del avión, en hPa. 0 si FSUIPC no conectado.</summary>
        public double AircraftQnhMb { get; set; }
        /// <summary>Outside Air Temperature in °C (FSUIPC 0x0E8C).</summary>
        public double OatCelsius { get; set; }
        /// <summary>COM1 active frequency in MHz (e.g. 118.5). 0 when not connected.</summary>
        public double Com1FrequencyMhz { get; set; }
        /// <summary>NAV1 active frequency in MHz (e.g. 111.3). 0 when not connected.</summary>
        public double Nav1FrequencyMhz { get; set; }
        /// <summary>NAV1 OBS / ILS course in degrees (0–359).</summary>
        public int Nav1ObsCourse { get; set; }
        public bool ApMaster { get; set; }
        public string ApNavMode { get; set; } = "----";   // ILS / APP / NAV / HDG / BC
        public string ApVertMode { get; set; } = "----";  // GS / VS / ALT
        /// <summary>Linea de diagnostico con **todas** las banderas del AP (`Helpers/AutopilotPanel`).</summary>
        public string ApPanel { get; set; } = "";
        /// <summary>Rumbo del bug del AP (0-359 grados, de `0x07CC`). Solo informativo.</summary>
        public int ApHeadingBugDeg { get; set; }
        /// <summary>Senal de cinturones encendida (`0x341D`).</summary>
        public bool SeatbeltSignOn { get; set; }
        /// <summary>Senal de no fumar encendida (`0x341C`).</summary>
        public bool NoSmokingSignOn { get; set; }
        /// <summary>El addon demostro que publica los carteles de cabina.</summary>
        public bool CabinSignsAvailable { get; set; }
        /// <summary>El addon demostro que publica el selector de autobrake.</summary>
        public bool AutobrakeAvailable { get; set; }
        /// <summary>`0x0C46 AUTOBRAKES ACTIVE`: esta actuando.</summary>
        public bool AutobrakeActuating { get; set; }
        /// <summary>Línea de cabina ya armada (`CAB cint=… nofum=… publicado=…`).</summary>
        public string CabinPanel { get; set; } = "";
        /// <summary>El bloque SDK de PMDG está alimentado.</summary>
        public bool PmdgSdkAlive { get; set; }
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
