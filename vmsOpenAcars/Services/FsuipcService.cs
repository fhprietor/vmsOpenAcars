using System;
using System.Windows.Forms;
using FSUIPC;

namespace vmsOpenAcars.Services
{
    public class FsuipcService
    {
        // --- EVENTOS PUSH ---
        public event Action OnConnected;
        public event Action<double> OnLandingDetected;
        public event Action<bool> OnParkingBrakeChanged;
        public event Action<string, bool> OnLightChanged;
        public event Action<bool> OnGearChanged;
        public event Action<bool> OnBatteryChanged;
        public event Action<bool> OnAvionicsChanged;
        public event Action<bool> OnExternalPowerChanged;
        public event Action<bool> OnAPUChanged;

        // --- MOTOR INTERNO ---
        private Timer _fastTimer;
        private bool _wasOnGround = true;
        private bool _lastParkingBrake;
        private bool _lastGearDown;

        // --- ESTADOS PREVIOS ---
        private short _lastLightsMask = 0;
        private bool _lastBattery;
        private bool _lastAvionics;
        private bool _lastExtPower;
        private bool _lastAPURunning;

        // --- OFFSETS ---
        private Offset<int> simVersion = new Offset<int>(0x3308);
        private Offset<string> aircraftTitle = new Offset<string>(0x3D00, 256);
        private Offset<long> playerLatitude = new Offset<long>(0x0560);
        private Offset<long> playerLongitude = new Offset<long>(0x0568);
        private Offset<long> playerAltitude = new Offset<long>(0x0570);
        private Offset<uint> playerHeading = new Offset<uint>(0x0580);
        private Offset<int> groundSpeedRaw = new Offset<int>(0x02B4);
        private Offset<int> verticalSpeed = new Offset<int>(0x02C8);
        private Offset<short> onGround = new Offset<short>(0x0366);
        private Offset<int> flapsPos = new Offset<int>(0x0BDC);
        private Offset<int> gearPos = new Offset<int>(0x0BE8);
        private Offset<short> parkingBrake = new Offset<short>(0x0BC8);
        private Offset<short> lightsMask = new Offset<short>(0x0D0C);
        private Offset<int> fuelTotalWeight = new Offset<int>(0x126C);
        private Offset<int> vsAtTouchdown = new Offset<int>(0x030C);
        private Offset<int> batterySwitch = new Offset<int>(0x281C);
        private Offset<int> avionicsSwitch = new Offset<int>(0x2E80);
        private Offset<int> externalPower = new Offset<int>(0x0B4C);
        private Offset<int> apuN1 = new Offset<int>(0x0B54);
        private Offset<string> aircraftICAO = new Offset<string>(0x0601, 8);
        private Offset<string> aircraftModel = new Offset<string>(0x3160, 24);

        public bool IsConnected { get; private set; }
        public double LastLandingRate { get; private set; }

        public FsuipcService()
        {
            _fastTimer = new Timer();
            _fastTimer.Interval = 200;
            _fastTimer.Tick += FastTimer_Tick;
        }

        public void StartMonitoring() { if (IsConnected) _fastTimer.Start(); }
        public void StopMonitoring() => _fastTimer.Stop();

        public void Connect()
        {
            if (IsConnected) return;

            try
            {
                FSUIPCConnection.Open();
                IsConnected = true;
                // --- TRUCO PARA MSFS 2024 ---
                // Forzamos un proceso inicial para llenar los offsets estáticos
                FSUIPCConnection.Process();

                // Si el título sigue vacío, el evento OnConnected podría dispararse 
                // antes de que FSUIPC lea la memoria del avión.
                OnConnected?.Invoke();
                StartMonitoring();
            }
            catch
            {
                IsConnected = false;
            }
        }

        private void FastTimer_Tick(object sender, EventArgs e)
        {
            if (!IsConnected) return;

            try
            {
                FSUIPCConnection.Process();

                // 1. Lógica de Aterrizaje
                bool currentlyOnGround = (onGround.Value == 1);
                if (!_wasOnGround && currentlyOnGround)
                {
                    LastLandingRate = Math.Round((double)vsAtTouchdown.Value / 256.0);
                    OnLandingDetected?.Invoke(LastLandingRate);
                }
                _wasOnGround = currentlyOnGround;

                // 2. Parking Brake
                bool currentPB = (parkingBrake.Value == 32767);
                if (currentPB != _lastParkingBrake)
                {
                    _lastParkingBrake = currentPB;
                    OnParkingBrakeChanged?.Invoke(currentPB);
                }

                // 3. Gear
                bool currentGear = (gearPos.Value > 0);
                if (currentGear != _lastGearDown)
                {
                    _lastGearDown = currentGear;
                    OnGearChanged?.Invoke(currentGear);
                }

                ProcessLights();

                // 5. Battery
                bool currentBattery = (batterySwitch.Value == 1);
                if (currentBattery != _lastBattery)
                {
                    _lastBattery = currentBattery;
                    OnBatteryChanged?.Invoke(currentBattery);
                }

                // 6. Avionics
                bool currentAvi = (avionicsSwitch.Value == 1);
                if (currentAvi != _lastAvionics)
                {
                    _lastAvionics = currentAvi;
                    OnAvionicsChanged?.Invoke(currentAvi);
                }

                // 7. GPU
                bool currentGPU = (externalPower.Value == 1);
                if (currentGPU != _lastExtPower)
                {
                    _lastExtPower = currentGPU;
                    OnExternalPowerChanged?.Invoke(currentGPU);
                }

                // 8. APU
                bool currentAPURunning = (apuN1.Value > 9000);
                if (currentAPURunning != _lastAPURunning)
                {
                    _lastAPURunning = currentAPURunning;
                    OnAPUChanged?.Invoke(currentAPURunning);
                }
            }
            catch
            {
                IsConnected = false;
                StopMonitoring();
                try { FSUIPCConnection.Close(); } catch { }
            }
        }

        private void ProcessLights()
        {
            short currentMask = lightsMask.Value;
            if (currentMask == _lastLightsMask) return;

            string[] lightNames = {
                "Nav Lights", "Beacon Lights", "Landing Lights", "Taxi Lights",
                "Strobe Lights", "Panel Lights", "Recognition", "Wing Lights",
                "Logo Lights", "Cabin Lights"
            };

            for (int i = 0; i < lightNames.Length; i++)
            {
                int bitValue = 1 << i;
                bool wasOn = (_lastLightsMask & bitValue) != 0;
                bool isOn = (currentMask & bitValue) != 0;

                if (isOn != wasOn)
                {
                    OnLightChanged?.Invoke(lightNames[i], isOn);
                }
            }
            _lastLightsMask = currentMask;
        }

        // --- MÉTODOS DE DATOS (RESTAURADOS Y SEGUROS) ---
        public bool IsOnGround => IsConnected && onGround.Value == 1;
        public string GetAircraftTitle() => IsConnected ? aircraftTitle.Value.Trim() : "Unknown";
        public string GetAircraftICAO()
        {
            if (!IsConnected) return "ZZZZ";

            // 1. OBTENER EL TÍTULO (Es lo más fiable en MSFS 2024 ahora mismo)
            string title = GetAircraftTitle().ToUpper();

            // 2. DICCIONARIO DE MAPEO POR TÍTULO (Prioridad máxima)
            if (title.Contains("BARON") || title.Contains("B58")) return "B58";
            if (title.Contains("BONANZA") || title.Contains("G36")) return "G36";
            if (title.Contains("C172") || title.Contains("SKYHAWK")) return "C172";
            if (title.Contains("208") || title.Contains("CARAVAN")) return "C208";
            if (title.Contains("KING AIR") || title.Contains("BE35")) return "BE35";
            if (title.Contains("TBM")) return "TBM9";
            if (title.Contains("A320") || title.Contains("A20N")) return "A20N";
            if (title.Contains("737")) return "B738";
            if (title.Contains("C510") || title.Contains("MUSTANG")) return "C510";
            if (title.Contains("A5")) return "ICON";

            // 3. FALLBACK: Si no hay palabras clave, intentar leer Offsets
            string icao = aircraftICAO.Value.Split('\0')[0].Trim().ToUpper();

            // Si el offset tiene basura "ATCCOM" o está vacío, probar el otro offset
            if (string.IsNullOrEmpty(icao) || icao.Contains("ATCCOM"))
            {
                icao = aircraftModel.Value.Split('\0')[0].Trim().ToUpper();
            }

            // 4. LIMPIEZA FINAL DE BASURA ATCCOM
            if (icao.Contains("ATCCOM"))
            {
                // Si dice ATCCOM.ATC_NAME_BEECHCR, extraemos BEECHCR y tomamos 4 letras
                var parts = icao.Split('_');
                string lastPart = parts[parts.Length - 1];
                return lastPart.Length >= 4 ? lastPart.Substring(0, 4) : lastPart;
            }

            return string.IsNullOrEmpty(icao) ? "ZZZZ" : icao;
        }
        public double GetLatitude() => IsConnected ? (double)playerLatitude.Value * 90.0 / (10001750.0 * 65536.0 * 65536.0) : 0;
        public double GetLongitude() => IsConnected ? (double)playerLongitude.Value * 360.0 / (65536.0 * 65536.0 * 65536.0 * 65536.0) : 0;
        public double GetAltitude() => IsConnected ? ((double)playerAltitude.Value / (65536.0 * 65536.0)) * 3.28084 : 0;
        public double GetHeading() => IsConnected ? (double)playerHeading.Value * 360.0 / (65536.0 * 65536.0) : 0;
        public double GetGroundSpeed() => IsConnected ? ((double)groundSpeedRaw.Value / 65536.0) * 1.94384 : 0;
        public double GetVerticalSpeed() => IsConnected ? (double)verticalSpeed.Value * 3.28084 * 60.0 / 256.0 : 0;
        public double GetTotalFuel() => IsConnected ? (double)fuelTotalWeight.Value : 0;

        public string GetSimName()
        {
            if (!IsConnected) return "Disconnected";
            if (System.Diagnostics.Process.GetProcessesByName("FlightSimulator2024").Length > 0) return "MSFS 2024";

            string versionStr = FSUIPCConnection.FlightSimVersionConnected.ToString().ToUpper();
            if (versionStr.Contains("MSFS")) return "MSFS 2020";
            if (versionStr.Contains("P3D")) return "Prepar3D";
            if (versionStr.Contains("FSX")) return "FSX";

            return versionStr;
        }
    }
}