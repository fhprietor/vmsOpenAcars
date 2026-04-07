// En Services/FsuipcService.cs - reemplazar el contenido

using System;
using System.Diagnostics;
using System.Threading;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using FSUIPC;

namespace vmsOpenAcars.Services
{
    public class FsuipcService : IDisposable
    {
        // Offsets (mantener los mismos)
        private Offset<long> playerLatitude = new Offset<long>(0x0560);
        private Offset<long> playerLongitude = new Offset<long>(0x0568);
        private Offset<long> playerAltitude = new Offset<long>(0x0570);
        private Offset<uint> playerHeading = new Offset<uint>(0x0580);
        private Offset<int> groundSpeedRaw = new Offset<int>(0x02B4);
        private Offset<int> verticalSpeed = new Offset<int>(0x02C8);
        private Offset<int> verticalSpeedRaw = new Offset<int>(0x030C);
        private Offset<short> onGround = new Offset<short>(0x0366);
        private Offset<int> gearPos = new Offset<int>(0x0BE8);
        private Offset<short> parkingBrake = new Offset<short>(0x0BC8);
        private Offset<int> fuelTotalWeight = new Offset<int>(0x126C);
        private Offset<int> indicatedAirspeed = new Offset<int>(0x02BC);
        private Offset<int> fuelFlow = new Offset<int>(0x0B54);
        private Offset<short> transponderCode = new Offset<short>(0x0354);
        private Offset<short> autopilotMaster = new Offset<short>(0x07BC);
        private Offset<short> autopilotNavMode = new Offset<short>(0x07CC);
        private Offset<int> simulationZuluTime = new Offset<int>(0x023A);
        private Offset<short> radarAltitude = new Offset<short>(0x31E4);
        private Offset<short> aircraftPitch = new Offset<short>(0x0578);
        private Offset<short> aircraftBank = new Offset<short>(0x057C);
        private Offset<short> spoilersDeployed = new Offset<short>(0x0BD8);
        private Offset<short> spoilersArmed = new Offset<short>(0x0BCC);
        private Offset<short> flapsHandle = new Offset<short>(0x0BDC);
        private Offset<short> windDirection = new Offset<short>(0x0E90);
        private Offset<short> windSpeed = new Offset<short>(0x0E92);
        private Offset<int> engineRpm1 = new Offset<int>(0x0898);

        // Polling con backoff
        private Timer _highFreqTimer;
        private int _pollingIntervalMs;
        private bool _isRunning = false;
        private ConnectionState _currentState = ConnectionState.Disconnected;
        private int _positionOrder = 0;
        private int _connectionRetryCount = 0;
        private int _currentBackoffMs = 1000;
        private bool _isReconnecting = false;

        private DateTime _lastParkingBrakeChange = DateTime.MinValue;
        private bool _lastParkingBrakeValue = false;

        // Estado anterior para detección de eventos
        private bool _lastOnGround = true;
        private int _lastGearPosition = 0;
        private double _lastFlapsPosition = 0;
        private bool _lastSpoilersDeployed = false;
        private int _lastEngineRpm = 0;
        private int _lastParkingBrake = 0;

        // Buffer para envío periódico
        private DateTime _lastTelemetrySend = DateTime.MinValue;
        private double _currentPhaseInterval = AppConfig.UpdateIntervalOther;

        // Propiedades públicas
        public bool IsConnected => _currentState == ConnectionState.Connected;
        public string SimulatorName { get; private set; } = "Desconocido";

        // Eventos
        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<DataUpdatedEventArgs> DataUpdated;

        // Eventos de alta precisión
        public event Action<int, int, int, double, double, double, double> OnTakeoffDetected;
        public event Action<int, int, int, double, double, double, bool, double, int> OnTouchdownDetected;
        public event Action<int, int> OnGearChanged;
        public event Action<double, double> OnFlapsChanged;
        public event Action<bool> OnSpoilersChanged;
        public event Action<bool> OnParkingBrakeChanged;
        public event Action<int> OnEngineChanged;

        public FsuipcService()
        {
            _pollingIntervalMs = AppConfig.PollingIntervalMs;
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _connectionRetryCount = 0;
            _currentBackoffMs = 1000;

            // Iniciar con un intervalo más largo para la primera conexión
            _highFreqTimer = new Timer(OnHighFreqTick, null, 500, _pollingIntervalMs);
            Debug.WriteLine($"FSUIPC Polling started");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _isReconnecting = false;

            if (_highFreqTimer != null)
            {
                _highFreqTimer.Dispose();
                _highFreqTimer = null;
            }

            Disconnect();
            Debug.WriteLine("FSUIPC Polling stopped");
        }

        public void SetUpdateIntervalForPhase(FlightPhase phase)
        {
            if (phase == FlightPhase.TaxiOut || phase == FlightPhase.TaxiIn)
            {
                _currentPhaseInterval = AppConfig.UpdateIntervalTaxi;
            }
            else if (phase == FlightPhase.Takeoff)
            {
                _currentPhaseInterval = AppConfig.UpdateIntervalTakeoff;
            }
            else if (phase == FlightPhase.Climb)
            {
                _currentPhaseInterval = AppConfig.UpdateIntervalClimb;
            }
            else if (phase == FlightPhase.Enroute)
            {
                _currentPhaseInterval = AppConfig.UpdateIntervalCruise;
            }
            else if (phase == FlightPhase.Descent)
            {
                _currentPhaseInterval = AppConfig.UpdateIntervalDescent;
            }
            else if (phase == FlightPhase.Approach)
            {
                _currentPhaseInterval = AppConfig.UpdateIntervalApproach;
            }
            else
            {
                _currentPhaseInterval = AppConfig.UpdateIntervalOther;
            }
        }

        private void OnHighFreqTick(object state)
        {
            if (!_isRunning) return;

            // Intentar conectar si está desconectado
            if (_currentState == ConnectionState.Disconnected)
            {
                TryConnect();
                return;
            }

            // Si está conectado, procesar datos
            if (_currentState == ConnectionState.Connected)
            {
                try
                {
                    FSUIPCConnection.Process();
                    // Resetear backoff en caso de éxito
                    _connectionRetryCount = 0;
                    _currentBackoffMs = 1000;
                    _isReconnecting = false;

                    DetectEvents();
                    UpdateTelemetryBuffer();
                }
                catch (Exception ex)
                {
                    // Error de conexión, marcar como desconectado
                    Debug.WriteLine($"FSUIPC Process error: {ex.Message}");
                    _currentState = ConnectionState.Disconnected;
                    SimulatorName = "Desconocido";

                    if (Disconnected != null)
                    {
                        Disconnected(this, EventArgs.Empty);
                    }
                }
            }
        }

        private void TryConnect()
        {
            if (_isReconnecting) return;

            _isReconnecting = true;

            try
            {
                Debug.WriteLine($"Attempting to connect to FSUIPC (attempt {_connectionRetryCount + 1})...");
                FSUIPCConnection.Open();
                _currentState = ConnectionState.Connected;
                SimulatorName = DetectSimulator();
                _connectionRetryCount = 0;
                _currentBackoffMs = 1000;
                _isReconnecting = false;

                Debug.WriteLine($"Connected to {SimulatorName}");

                if (Connected != null)
                {
                    Connected(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                _currentState = ConnectionState.Disconnected;
                _isReconnecting = false;
                _connectionRetryCount++;

                // Backoff exponencial: 1s, 2s, 4s, 8s, 16s, max 30s
                _currentBackoffMs = Math.Min(30000, 1000 * (int)Math.Pow(2, Math.Min(5, _connectionRetryCount)));

                Debug.WriteLine($"FSUIPC connection failed: {ex.Message}. Retry in {_currentBackoffMs}ms");

                // Programar reconexión con backoff
                if (_isRunning && _highFreqTimer != null)
                {
                    _highFreqTimer.Change(_currentBackoffMs, _pollingIntervalMs);
                }
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
        }

        private void DetectEvents()
        {
            bool isOnGround = IsOnGround;
            int gearPosition = GetGearPosition();
            double flapsPosition = GetFlapsPosition();
            bool spoilersDeployed = IsSpoilersDeployed();
            int engineRpm = GetEngineRpm();
            int parkingBrake = GetParkingBrake();

            // Takeoff detection - solo si realmente está despegando
            if (_lastOnGround && !isOnGround && GetGroundSpeed() > 60)
            {
                int speed = (int)GetGroundSpeed();
                int altitude = (int)GetAltitude();
                int vs = (int)GetVerticalSpeed();
                double pitch = GetPitch();
                double bank = GetBank();
                double heading = GetHeading();
                double flaps = GetFlapsPosition();

                // No mostrar VS negativo en despegue
                if (vs < 0) vs = 0;

                if (OnTakeoffDetected != null)
                {
                    OnTakeoffDetected(speed, altitude, vs, pitch, bank, heading, flaps);
                }
            }

            // Touchdown detection
            if (!_lastOnGround && isOnGround)
            {
                int vs = (int)GetVerticalSpeed();
                int speed = (int)GetGroundSpeed();
                int altitude = (int)GetAltitude();
                double pitch = GetPitch();
                double bank = GetBank();
                double heading = GetHeading();
                bool spoilers = IsSpoilersDeployed();
                double flaps = GetFlapsPosition();
                int gear = GetGearPosition();

                if (OnTouchdownDetected != null)
                {
                    OnTouchdownDetected(vs, speed, altitude, pitch, bank, heading, spoilers, flaps, gear);
                }
            }

            // Gear changes
            if (AppConfig.ReportGearChanges && gearPosition != _lastGearPosition)
            {
                if (OnGearChanged != null)
                {
                    OnGearChanged(_lastGearPosition, gearPosition);
                }
            }

            // Flaps changes
            if (AppConfig.ReportFlapChanges && Math.Abs(flapsPosition - _lastFlapsPosition) > 1.0)
            {
                if (OnFlapsChanged != null)
                {
                    OnFlapsChanged(_lastFlapsPosition, flapsPosition);
                }
            }

            // Spoilers changes
            if (AppConfig.ReportSpoilerChanges && spoilersDeployed != _lastSpoilersDeployed)
            {
                if (OnSpoilersChanged != null)
                {
                    OnSpoilersChanged(spoilersDeployed);
                }
            }

            // Engine changes
            if (AppConfig.ReportEngineChanges && Math.Abs(engineRpm - _lastEngineRpm) > 100)
            {
                if (OnEngineChanged != null)
                {
                    OnEngineChanged(engineRpm);
                }
            }

            if (parkingBrake != _lastParkingBrake)
            {
                // Debounce: solo reportar si ha pasado al menos 1 segundo
                if ((DateTime.UtcNow - _lastParkingBrakeChange).TotalSeconds >= 1)
                {
                    _lastParkingBrakeChange = DateTime.UtcNow;
                    if (OnParkingBrakeChanged != null)
                    {
                        OnParkingBrakeChanged(parkingBrake == 1);
                    }
                }
                _lastParkingBrake = parkingBrake;
            }

            // Update state
            _lastOnGround = isOnGround;
            _lastGearPosition = gearPosition;
            _lastFlapsPosition = flapsPosition;
            _lastSpoilersDeployed = spoilersDeployed;
            _lastEngineRpm = engineRpm;
            _lastParkingBrake = parkingBrake;
        }

        private void UpdateTelemetryBuffer()
        {
            if (DateTime.UtcNow - _lastTelemetrySend >= TimeSpan.FromSeconds(_currentPhaseInterval))
            {
                _lastTelemetrySend = DateTime.UtcNow;
                _positionOrder++;

                DataUpdatedEventArgs args = new DataUpdatedEventArgs();
                args.Latitude = GetLatitude();
                args.Longitude = GetLongitude();
                args.Altitude = GetAltitude();
                args.GroundSpeed = (int)GetGroundSpeed();
                args.Heading = GetHeading();
                args.VerticalSpeed = GetVerticalSpeed();
                args.IsOnGround = IsOnGround;
                args.FuelTotal = GetTotalFuel();
                args.IndicatedAirspeed = GetIndicatedAirspeed();
                args.FuelFlow = GetFuelFlow();
                args.TransponderCode = GetTransponderCode();
                args.AutopilotMaster = GetAutopilotMaster();
                args.SimulationZuluTime = GetSimulationZuluTime();
                args.RadarAltitude = GetRadarAltitude();
                args.Order = _positionOrder;
                args.Pitch = GetPitch();
                args.Bank = GetBank();
                args.SpoilersDeployed = IsSpoilersDeployed();
                args.FlapsPosition = GetFlapsPosition();
                args.GearPosition = GetGearPosition();
                args.Wind = GetWindDisplay();
                args.NavType = DetermineNavType();

                if (DataUpdated != null)
                {
                    DataUpdated(this, args);
                }
            }
        }

        private int DetermineNavType()
        {
            if (!IsConnected) return 0;

            if (autopilotMaster.Value == 1)
            {
                int navMode = autopilotNavMode.Value;
                if (navMode == 1) return 1;
                if (navMode == 2) return 3;
                if (navMode == 3) return 1;
                return 0;
            }

            return 0;
        }

        // Métodos de lectura (mantener igual)
        public bool IsOnGround => IsConnected && onGround.Value == 1;

        public double GetLatitude()
        {
            if (!IsConnected) return 0;
            return (double)playerLatitude.Value * 90.0 / (10001750.0 * 65536.0 * 65536.0);
        }

        public double GetLongitude()
        {
            if (!IsConnected) return 0;
            return (double)playerLongitude.Value * 360.0 / (65536.0 * 65536.0 * 65536.0 * 65536.0);
        }

        public double GetAltitude()
        {
            if (!IsConnected) return 0;
            return ((double)playerAltitude.Value / (65536.0 * 65536.0)) * 3.28084;
        }

        public double GetHeading()
        {
            if (!IsConnected) return 0;
            return (double)playerHeading.Value * 360.0 / (65536.0 * 65536.0);
        }

        public double GetGroundSpeed()
        {
            if (!IsConnected) return 0;
            return ((double)groundSpeedRaw.Value / 65536.0) * 1.94384;
        }

        public double GetVerticalSpeed()
        {
            if (!IsConnected) return 0;
            int rawVs = verticalSpeedRaw.Value;
            if (rawVs != 0 && Math.Abs(rawVs) < 10000) return rawVs;
            return (double)verticalSpeed.Value / 256.0;
        }

        public double GetTotalFuel()
        {
            if (!IsConnected) return 0;
            return (double)fuelTotalWeight.Value;
        }

        public double GetIndicatedAirspeed()
        {
            if (!IsConnected) return 0;
            return (double)indicatedAirspeed.Value / 128.0;
        }

        public double GetFuelFlow()
        {
            if (!IsConnected) return 0;
            return (double)fuelFlow.Value / 256.0;
        }

        public int GetTransponderCode()
        {
            if (!IsConnected) return 1200;
            return transponderCode.Value;
        }

        public bool GetAutopilotMaster()
        {
            if (!IsConnected) return false;
            return autopilotMaster.Value == 1;
        }

        public DateTime GetSimulationZuluTime()
        {
            if (!IsConnected) return DateTime.UtcNow;
            return DateTime.UtcNow.Date.AddSeconds(simulationZuluTime.Value);
        }

        public double GetRadarAltitude()
        {
            if (!IsConnected) return 0;
            return radarAltitude.Value;
        }

        public double GetPitch()
        {
            if (!IsConnected) return 0;
            double pitch = (double)aircraftPitch.Value / 65536.0;
            if (IsOnGround && pitch > 0) pitch = -pitch;
            return Math.Round(pitch, 2);
        }

        public double GetBank()
        {
            if (!IsConnected) return 0;
            return Math.Round((double)aircraftBank.Value / 65536.0, 2);
        }

        public bool IsSpoilersDeployed()
        {
            if (!IsConnected) return false;
            return spoilersDeployed.Value > 0 || spoilersArmed.Value > 0;
        }

        public double GetFlapsPosition()
        {
            if (!IsConnected) return 0;
            int flaps = flapsHandle.Value;
            if (flaps <= 0) return 0;
            if (flaps >= 16383) return 100;

            // Mapeo específico para Boeing 737 (valores típicos)
            // 0=0%, 1=~15%, 2=~25%, 5=~50%, 10=~63%, 15=~75%, 25=~88%, 30=100%
            double percent = (double)flaps / 163.83;

            // Redondear a valores discretos de flaps para B737
            if (percent < 2) return 0;
            if (percent < 10) return 15;      // Flaps 1
            if (percent < 18) return 25;      // Flaps 2
            if (percent < 35) return 50;      // Flaps 5
            if (percent < 55) return 63;      // Flaps 10
            if (percent < 70) return 75;      // Flaps 15
            if (percent < 85) return 88;      // Flaps 25
            return 100;                        // Flaps 30/40
        }

        public int GetGearPosition()
        {
            if (!IsConnected) return 0;
            return gearPos.Value > 8000 ? 1 : 0;
        }

        public string GetWindDisplay()
        {
            if (!IsConnected) return "Unknown";
            int dir = windDirection.Value;
            int spd = windSpeed.Value;
            if (dir == 0 && spd == 0) return "Calm";
            return $"{dir:000}° at {spd} kts";
        }

        private int GetEngineRpm()
        {
            if (!IsConnected) return 0;
            return engineRpm1.Value;
        }

        private int GetParkingBrake()
        {
            if (!IsConnected) return 0;
            return parkingBrake.Value;
        }

        private string DetectSimulator()
        {
            if (Process.GetProcessesByName("FlightSimulator2024").Length > 0) return "MSFS 2024";
            if (Process.GetProcessesByName("FlightSimulator").Length > 0) return "MSFS 2020";
            if (Process.GetProcessesByName("X-Plane").Length > 0) return "X-Plane";
            if (Process.GetProcessesByName("Prepar3D").Length > 0) return "Prepar3D";
            return FSUIPCConnection.FlightSimVersionConnected.ToString();
        }

        public string GetSimName()
        {
            return IsConnected ? SimulatorName : "Disconnected";
        }

        public void Dispose()
        {
            Stop();
        }
    }

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
        public double IndicatedAirspeed { get; set; }
        public double FuelFlow { get; set; }
        public int TransponderCode { get; set; }
        public bool AutopilotMaster { get; set; }
        public DateTime SimulationZuluTime { get; set; }
        public double RadarAltitude { get; set; }
        public int Order { get; set; }
        public double Pitch { get; set; }
        public double Bank { get; set; }
        public bool SpoilersDeployed { get; set; }
        public double FlapsPosition { get; set; }
        public int GearPosition { get; set; }
        public string Wind { get; set; }
        public int NavType { get; set; }
    }

    public enum ConnectionState
    {
        Disconnected,
        Connected
    }
}