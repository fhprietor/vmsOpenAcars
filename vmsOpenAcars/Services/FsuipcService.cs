// FsuipcServices.cs
using System;
using System.Diagnostics;
using System.Windows.Forms;
using FSUIPC;

namespace vmsOpenAcars.Services
{
    public class FsuipcService : IDisposable
    {
        // Eventos públicos (los mismos)
        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<DataUpdatedEventArgs> DataUpdated;
        public event EventHandler<LandingEventArgs> LandingDetected;
        public event EventHandler<LightChangedEventArgs> LightChanged;
        public event EventHandler<BoolEventArgs> ParkingBrakeChanged;
        public event EventHandler<BoolEventArgs> GearChanged;
        public event EventHandler<BoolEventArgs> BatteryChanged;
        public event EventHandler<BoolEventArgs> AvionicsChanged;
        public event EventHandler<BoolEventArgs> ExternalPowerChanged;
        public event EventHandler<BoolEventArgs> APUChanged;

        // Estado interno
        private Timer _heartbeatTimer;
        private bool _lastConnectionState;
        private ConnectionState _currentState;

        private double _verticalSpeedScale = 256;
        private bool _verticalSpeedScaleDetected = false;
        private double _verticalSpeedMultiplier = 1.0;

        // Contador para el orden de posiciones
        private int _positionOrder = 0; // <--- AÑADIR AQUÍ

        // Offsets (los mismos)
        private Offset<long> playerLatitude = new Offset<long>(0x0560);
        private Offset<long> playerLongitude = new Offset<long>(0x0568);
        private Offset<long> playerAltitude = new Offset<long>(0x0570);
        private Offset<uint> playerHeading = new Offset<uint>(0x0580);
        private Offset<int> groundSpeedRaw = new Offset<int>(0x02B4);
        private Offset<int> verticalSpeed = new Offset<int>(0x02C8);  // VS en pies/minuto * 256 (FSX/P3D)
        private Offset<int> verticalSpeedRaw = new Offset<int>(0x030C);  // VS en pies/minuto directo (MSFS)
        private Offset<int> verticalSpeedFSX = new Offset<int>(0x02C8);   // FSX/P3D: pies/min × 256
        private Offset<int> verticalSpeedMSFS = new Offset<int>(0x030C);  // MSFS: pies/min directo
        private Offset<short> verticalSpeedSimple = new Offset<short>(0x02C8); // Alternativo


        private Offset<short> onGround = new Offset<short>(0x0366);
        private Offset<int> gearPos = new Offset<int>(0x0BE8);
        private Offset<short> parkingBrake = new Offset<short>(0x0BC8);
        private Offset<short> lightsMask = new Offset<short>(0x0D0C);
        private Offset<int> fuelTotalWeight = new Offset<int>(0x126C);
        private Offset<int> vsAtTouchdown = new Offset<int>(0x030C);
        private Offset<int> batterySwitch = new Offset<int>(0x281C);
        private Offset<int> avionicsSwitch = new Offset<int>(0x2E80);
        private Offset<int> externalPower = new Offset<int>(0x0B4C);
        private Offset<int> apuN1 = new Offset<int>(0x0B54);
        private Offset<int> simulationTime = new Offset<int>(0x023A); // Tiempo sim (segundos)
        private Offset<short> aircraftPitch = new Offset<short>(0x0578);  // Pitch en grados * 65536
        private Offset<short> aircraftBank = new Offset<short>(0x057C);   // Bank en grados * 65536
        // Velocidades
        private Offset<int> indicatedAirspeed = new Offset<int>(0x02BC); // IAS en nudos * 128

        // Combustible
        private Offset<int> fuelFlow = new Offset<int>(0x0B54); // Fuel flow en libras/hora * 256? (verificar)

        // Transponder
        private Offset<short> transponderCode = new Offset<short>(0x0354); // Código transponder

        // Autopilot
        private Offset<short> autopilotMaster = new Offset<short>(0x07BC); // 0 = off, 1 = on
        private Offset<short> autopilotNavMode = new Offset<short>(0x07CC); // Modo navegación

        // Tiempo de simulación
        private Offset<int> simulationZuluTime = new Offset<int>(0x023A); // Tiempo Zulu en segundos desde medianoche
        private Offset<int> simulationLocalTime = new Offset<int>(0x0238); // Tiempo local

        // AGL (necesita elevación del terreno)
        private Offset<short> radarAltitude = new Offset<short>(0x31E4); // Altitud radar en pies (AGL)
        // Propiedades públicas
        public bool IsConnected => _currentState == ConnectionState.Connected;
        public string SimulatorName { get; private set; } = "Desconocido";
        public double LastLandingRate { get; private set; }

        public FsuipcService()
        {
            _heartbeatTimer = new Timer { Interval = 1000 };
            _heartbeatTimer.Tick += Heartbeat_Tick;
        }

        public void Start()
        {
            _heartbeatTimer.Start();
        }

        public void Stop()
        {
            _heartbeatTimer.Stop();
            Disconnect();
        }
        public void TryReconnect()
        {
            try
            {
                if (!IsConnected)
                {
                    CheckConnection();
                }
            }
            catch { } // Silenciar excepciones en reconexión
        }

        public double GetPitch()
        {
            if (!IsConnected) return 0;
            return (double)aircraftPitch.Value / 65536.0;
        }

        public double GetBank()
        {
            if (!IsConnected) return 0;
            return (double)aircraftBank.Value / 65536.0;
        }

        public string GetSimName()
        {
            if (!IsConnected) return "Disconnected";
            return SimulatorName;
        }

        private void Heartbeat_Tick(object sender, EventArgs e)
        {
            bool wasConnected = _lastConnectionState;
            bool isConnected = CheckConnection();

            // Detectar cambio de estado
            if (wasConnected != isConnected)
            {
                if (isConnected)
                {
                    OnConnected();
                }
                else
                {
                    OnDisconnected();
                }
                _lastConnectionState = isConnected;
            }

            // Si estamos conectados, actualizar datos
            if (isConnected)
            {
                UpdateSimData();
            }
        }

        private bool CheckConnection()
        {
            try
            {
                // Intentar abrir si no está conectado
                if (_currentState == ConnectionState.Disconnected)
                {
                    FSUIPCConnection.Open();
                    _currentState = ConnectionState.Connected;
                    SimulatorName = DetectSimulator();
                    return true;
                }

                // Verificar que sigue conectado
                FSUIPCConnection.Process();
                return true;
            }
            catch
            {
                // Error al procesar → desconectado
                if (_currentState == ConnectionState.Connected)
                {
                    _currentState = ConnectionState.Disconnected;
                    SimulatorName = "Desconocido";
                    try { FSUIPCConnection.Close(); } catch { }
                }
                return false;
            }
        }

        private void Disconnect()
        {
            try
            {
                FSUIPCConnection.Close();
            }
            catch { }
            _currentState = ConnectionState.Disconnected;
            SimulatorName = "Desconocido";
        }

        private void OnConnected()
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        private void OnDisconnected()
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateSimData()
        {
            try
            {
                // Incrementar orden para cada actualización
                _positionOrder++;
                double verticalSpeedFpm = GetVerticalSpeed();

                var args = new DataUpdatedEventArgs
                {
                    // Campos existentes
                    Latitude = GetLatitude(),
                    Longitude = GetLongitude(),
                    Altitude = GetAltitude(),
                    GroundSpeed = GetGroundSpeed(),
                    Heading = GetHeading(),
                    VerticalSpeed = verticalSpeedFpm,
                    IsOnGround = IsOnGround,
                    FuelTotal = GetTotalFuel(),

                    IndicatedAirspeed = GetIndicatedAirspeed(),
                    FuelFlow = GetFuelFlow(),
                    TransponderCode = GetTransponderCode(),
                    AutopilotMaster = GetAutopilotMaster(),
                    AutopilotMode = GetAutopilotMode(),
                    SimulationZuluTime = GetSimulationZuluTime(),
                    SimulationLocalTime = GetSimulationLocalTime(),
                    RadarAltitude = GetRadarAltitude(),
                    Order = _positionOrder,
                    NavType = DetermineNavType(), // Según fase de vuelo o modo de navegación
                    Pitch = GetPitch(),
                    Bank = GetBank()
                };

                DataUpdated?.Invoke(this, args);
            }
            catch { }
        }

        // ===== MÉTODOS DE DATOS =====
        public bool IsOnGround => IsConnected && onGround.Value == 1;

        public double GetLatitude() => IsConnected ? (double)playerLatitude.Value * 90.0 / (10001750.0 * 65536.0 * 65536.0) : 0;

        public double GetLongitude() => IsConnected ? (double)playerLongitude.Value * 360.0 / (65536.0 * 65536.0 * 65536.0 * 65536.0) : 0;

        public double GetAltitude() => IsConnected ? ((double)playerAltitude.Value / (65536.0 * 65536.0)) * 3.28084 : 0;

        public double GetHeading() => IsConnected ? (double)playerHeading.Value * 360.0 / (65536.0 * 65536.0) : 0;

        public double GetGroundSpeed() => IsConnected ? ((double)groundSpeedRaw.Value / 65536.0) * 1.94384 : 0;

        /// <summary>
        /// Obtiene el vertical speed en pies por minuto (fpm)
        /// Detecta automáticamente la escala correcta
        /// </summary>
        public double GetVerticalSpeed()
        {
            if (!IsConnected) return 0;

            try
            {
                DetectVerticalSpeedScale();

                // Intentar primero con MSFS directo
                int msfsValue = verticalSpeedMSFS.Value;
                if (Math.Abs(msfsValue) > 0 && Math.Abs(msfsValue) < 10000)
                {
                    return msfsValue;
                }

                // Usar el offset escalado
                int scaledValue = verticalSpeedFSX.Value;
                return scaledValue * _verticalSpeedMultiplier;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting vertical speed: {ex.Message}");
                return 0;
            }
        }
        
        public double GetTotalFuel() => IsConnected ? (double)fuelTotalWeight.Value : 0;
        /// <summary>
        /// Detecta automáticamente la escala correcta del vertical speed
        /// </summary>
        private void DetectVerticalSpeedScale()
        {
            if (_verticalSpeedScaleDetected) return;

            try
            {
                // Probar con el offset directo de MSFS (0x030C)
                int msfsValue = verticalSpeedMSFS.Value;
                if (Math.Abs(msfsValue) > 0 && Math.Abs(msfsValue) < 10000)
                {
                    _verticalSpeedMultiplier = 1.0;
                    _verticalSpeedScaleDetected = true;
                    System.Diagnostics.Debug.WriteLine($"VS Scale detected: MSFS direct (multiplier=1)");
                    return;
                }

                // Probar con el offset escalado (0x02C8 como int)
                int scaledValue = verticalSpeedFSX.Value;
                if (Math.Abs(scaledValue) > 0 && Math.Abs(scaledValue) < 1000000)
                {
                    // Si el valor es grande (>1000), probablemente está escalado
                    if (Math.Abs(scaledValue) > 1000)
                    {
                        _verticalSpeedMultiplier = 1.0 / 256.0;
                        System.Diagnostics.Debug.WriteLine($"VS Scale detected: FSX/P3D scaled (multiplier=1/256)");
                    }
                    else
                    {
                        _verticalSpeedMultiplier = 1.0;
                        System.Diagnostics.Debug.WriteLine($"VS Scale detected: Direct (multiplier=1)");
                    }
                    _verticalSpeedScaleDetected = true;
                    return;
                }

                // Fallback: asumir escala 1/256 (más común)
                _verticalSpeedMultiplier = 1.0 / 256.0;
                _verticalSpeedScaleDetected = true;
                System.Diagnostics.Debug.WriteLine($"VS Scale: Using default (1/256)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting VS scale: {ex.Message}");
                _verticalSpeedMultiplier = 1.0 / 256.0;
                _verticalSpeedScaleDetected = true;
            }
        }

        private double GetIndicatedAirspeed()
        {
            if (!IsConnected) return 0;
            // El offset 0x02BC da IAS en nudos * 128
            return (double)indicatedAirspeed.Value / 128.0;
        }

        private double GetFuelFlow()
        {
            if (!IsConnected) return 0;
            // El offset puede variar según el avión, ajusta según necesites
            return (double)fuelFlow.Value / 256.0;
        }

        private int GetTransponderCode()
        {
            if (!IsConnected) return 1200; // Código por defecto
            return transponderCode.Value;
        }

        private bool GetAutopilotMaster()
        {
            if (!IsConnected) return false;
            return autopilotMaster.Value == 1;
        }

        private string GetAutopilotMode()
        {
            if (!IsConnected || autopilotMaster.Value == 0) return "OFF";

            // Interpretar modo de autopilot (simplificado)
            switch (autopilotNavMode.Value)
            {
                case 0: return "OFF";
                case 1: return "HDG";
                case 2: return "NAV";
                case 3: return "APR";
                case 4: return "REV";
                default: return "UNKNOWN";
            }
        }

        private DateTime GetSimulationZuluTime()
        {
            if (!IsConnected) return DateTime.UtcNow;
            // Convertir segundos desde medianoche a DateTime
            return DateTime.UtcNow.Date.AddSeconds(simulationZuluTime.Value);
        }

        private DateTime GetSimulationLocalTime()
        {
            if (!IsConnected) return DateTime.Now;
            return DateTime.Now.Date.AddSeconds(simulationLocalTime.Value);
        }

        private double GetRadarAltitude()
        {
            if (!IsConnected) return 0;
            return radarAltitude.Value; // En pies
        }

        private int DetermineNavType()
        {
            // 0 = normal, 1 = VOR, 2 = NDB, 3 = GPS, 4 = FMS
            // Puedes determinar esto según el modo de autopilot o fase de vuelo
            if (autopilotNavMode.Value == 2) return 3; // GPS
            if (autopilotNavMode.Value == 3) return 1; // VOR/ILS
            return 0;
        }
        private string DetectSimulator()
        {
            // Detectar por procesos
            if (Process.GetProcessesByName("FlightSimulator2024").Length > 0) return "MSFS 2024";
            if (Process.GetProcessesByName("FlightSimulator").Length > 0) return "MSFS 2020";
            if (Process.GetProcessesByName("X-Plane").Length > 0 ||
                Process.GetProcessesByName("X-Plane 12").Length > 0) return "X-Plane 12";
            if (Process.GetProcessesByName("Prepar3D").Length > 0) return "Prepar3D";

            // Fallback a FSUIPC
            string version = FSUIPCConnection.FlightSimVersionConnected.ToString();
            if (version.Contains("FSX")) return "FSX (o X-Plane via XUIPC)";
            return version;
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            Disconnect();
        }
    }

    // ===== CLASES DE EVENTOS =====
    public class DataUpdatedEventArgs : EventArgs
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double GroundSpeed { get; set; }
        public double Heading { get; set; }
        public double VerticalSpeed { get; set; }
        public bool IsOnGround { get; set; }
        public double FuelTotal { get; set; }
        public int Transponder { get; set; }
        public bool Autopilot { get; set; }
        public DateTime SimulationTime { get; set; }
        public double IndicatedAirspeed { get; set; }
        public double FuelFlow { get; set; }
        public int TransponderCode { get; set; }
        public bool AutopilotMaster { get; set; }
        public string AutopilotMode { get; set; }
        public DateTime SimulationZuluTime { get; set; }
        public DateTime SimulationLocalTime { get; set; }
        public double RadarAltitude { get; set; } // AGL
        public int Order { get; set; } // Secuencia de posiciones
        public int NavType { get; set; } // Tipo de navegación
        public double Pitch { get; set; }      // Pitch en grados
        public double Bank { get; set; }       // Bank en grados
    }

    public class LandingEventArgs : EventArgs
    {
        public double VerticalSpeed { get; set; }
    }

    public class LightChangedEventArgs : EventArgs
    {
        public string LightName { get; set; }
        public bool IsOn { get; set; }
    }

    public class BoolEventArgs : EventArgs
    {
        public bool Value { get; set; }
    }

    public enum ConnectionState
    {
        Disconnected,
        Connected
    }
}