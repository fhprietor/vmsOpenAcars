using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Services;
using vmsOpenAcars.Models;
using System.Configuration;

namespace vmsOpenAcars
{
    public partial class MainForm : Form
    {
        private ApiService _apiService;
        private MockSimulator _mockSimulator = new MockSimulator();
        private SimbriefPlan _activePlan;
        private string _activePirepId = "";
        private FlightPhase _currentPhase = FlightPhase.Boarding;
        private DateTime _flightStartTime;
        private Pilot _activePilot;
        private FsuipcService _fsuipc = new FsuipcService();

        public MainForm()
        {
            InitializeComponent();
            // Suscribimos a los eventos del nuevo FsuipcService
            SetupFsuipcEvents();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            telemetryTimer.Interval = 10000;
            telemetryTimer.Enabled = false;

            try
            {
                string vms_api_url = ConfigurationManager.AppSettings["vms_api_url"];
                string vms_api_key = ConfigurationManager.AppSettings["vms_api_key"];

                if (!string.IsNullOrEmpty(vms_api_url) && !string.IsNullOrEmpty(vms_api_key))
                {
                    _apiService = new ApiService(vms_api_url, vms_api_key);
                    AddLog($"Configuración cargada: {vms_api_url}");
                }
                else
                {
                    AddLog("ERROR: Configuración incompleta en App.config");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error Load: {ex.Message}");
            }
        }

        private void SetupFsuipcEvents()
        {
            // Estos eventos disparan automáticamente AddLog cuando algo cambia en el sim
            _fsuipc.OnLandingDetected += (rate) => {
                this.Invoke((MethodInvoker)delegate {
                    AddLog($"✈️ TOUCHDOWN: {rate} FPM");
                    lblServerStatus.Text = $"LANDING: {rate} FPM";
                });
            };

            _fsuipc.OnParkingBrakeChanged += (isSet) => {
                this.Invoke((MethodInvoker)delegate {
                    AddLog(isSet ? "🅿️ Parking Brake: SET" : "🅿️ Parking Brake: RELEASED");
                });
            };

            _fsuipc.OnLightChanged += (name, isOn) => {
                this.Invoke((MethodInvoker)delegate {
                    AddLog($"💡 {name}: {(isOn ? "ON" : "OFF")}");
                });
            };

            _fsuipc.OnGearChanged += (isDown) => {
                this.Invoke((MethodInvoker)delegate {
                    AddLog(isDown ? "⚙️ Gear: DOWN" : "⚙️ Gear: UP");
                });
            };

            _fsuipc.OnBatteryChanged += (isOn) => {
                this.Invoke((MethodInvoker)delegate {
                    AddLog(isOn ? "⚡ Master Battery: ON" : "🌑 Master Battery: OFF");
                });
            };

            _fsuipc.OnAvionicsChanged += (isOn) => {
                AddLog(isOn ? "📡 Sistemas de Aviónica: ACTIVADOS" : "📡 Sistemas de Aviónica: APAGADOS");
            };

            _fsuipc.OnExternalPowerChanged += (isOn) => {
                AddLog(isOn ? "🔌 Corriente Externa (GPU): CONECTADA" : "🔌 Corriente Externa (GPU): DESCONECTADA");
            };

            _fsuipc.OnConnected += () => {
                this.Invoke((MethodInvoker)delegate {
                    AddLog("✅ FSUIPC: Conexión establecida con éxito.");

                    // Solo enviamos el WelcomeLog si ya hay un vuelo activo (PirepId)
                    if (!string.IsNullOrEmpty(_activePirepId))
                    {
                        SendWelcomeLog();
                    }
                });
            };
        }

        private async void TelemetryTimer_Tick(object sender, EventArgs e)
        {
            telemetryTimer.Stop();
            try
            {
                double lat = 0, lon = 0, alt = 0, gs = 0, hdg = 0, vs = 0, fuel = 0;
                bool isOnGround = true;

                if (chkMockMode.Checked)
                {
                    _mockSimulator.UpdatePosition();
                    lat = _mockSimulator.CurrentLat; lon = _mockSimulator.CurrentLon;
                    alt = _mockSimulator.CurrentAlt; gs = _mockSimulator.CurrentGS;
                    hdg = _mockSimulator.CurrentHeading; isOnGround = _mockSimulator.IsOnGround;
                }
                else
                {
                    if (!_fsuipc.IsConnected) _fsuipc.Connect();

                    if (_fsuipc.IsConnected)
                    {
                        // NOTA: Ya no llamamos a Process() porque el hilo de FsuipcService lo hace solo.
                        lat = _fsuipc.GetLatitude();
                        lon = _fsuipc.GetLongitude();
                        alt = _fsuipc.GetAltitude();
                        gs = _fsuipc.GetGroundSpeed();
                        hdg = _fsuipc.GetHeading();
                        vs = _fsuipc.GetVerticalSpeed();
                        isOnGround = _fsuipc.IsOnGround;
                        fuel = _fsuipc.GetTotalFuel();
                    }
                    else
                    {
                        lblStatus.Text = "FSUIPC: Not Connected";
                        telemetryTimer.Start();
                        return;
                    }
                }

                UpdateFlightPhase((int)alt, (int)gs, isOnGround);

                if (!string.IsNullOrEmpty(_activePirepId))
                {
                    var positionReport = new
                    {
                        positions = new[] {
                            new {
                                lat = lat, lon = lon, alt = Math.Round(alt), gs = Math.Round(gs),
                                heading = Math.Round(hdg), vs = Math.Round(vs),
                                state = _currentPhase.ToString().ToLower(),
                                fuel = fuel, source_name = "vmsOpenAcars"
                            }
                        }
                    };

                    bool success = await _apiService.SendPositionUpdate(_activePirepId, positionReport);
                    UpdateNetworkUI(success);

                    lblStatus.Text = $"{_currentPhase.ToString().ToUpper()} | Alt: {alt:F0}ft | GS: {gs:F0}kt";
                }
            }
            catch (Exception ex) { lblStatus.Text = $"Error: {ex.Message}"; }
            finally { telemetryTimer.Start(); }
        }

        private void UpdateFlightPhase(int altitude, int groundSpeed, bool isOnGround)
        {
            if (isOnGround)
            {
                if (_currentPhase == FlightPhase.Boarding && groundSpeed >= 5) _currentPhase = FlightPhase.TaxiOut;
                if (_currentPhase == FlightPhase.Landing && groundSpeed < 30) _currentPhase = FlightPhase.TaxiIn;
                if (_currentPhase == FlightPhase.TaxiIn && groundSpeed < 2) _currentPhase = FlightPhase.Arrived;
            }
            else
            {
                if (_currentPhase == FlightPhase.TaxiOut || _currentPhase == FlightPhase.Takeoff) _currentPhase = FlightPhase.Takeoff;
                if (altitude > 1500 && _currentPhase == FlightPhase.Takeoff) _currentPhase = FlightPhase.Enroute;
                if (altitude < 3000 && _currentPhase == FlightPhase.Enroute) _currentPhase = FlightPhase.Approach;
            }
            lblCurrentPhase.Text = $"Phase: {_currentPhase.ToString().ToUpper()}";
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            if (_apiService == null) return;
            AddLog("Iniciando sesión...");
            var result = await _apiService.GetPilotData();

            if (result.Data != null)
            {
                _activePilot = result.Data;
                lblPilotInfo.Text = $"Welcome, {_activePilot.Name}";
                lblServerStatus.ForeColor = Color.Green;
                lblServerStatus.Text = "Server: Online";
            }
            else { MessageBox.Show(result.Error); }
        }

        private async void btnFetchSimbrief_Click(object sender, EventArgs e)
        {
            string pilotId = txtSimbriefId.Text.Trim();
            if (string.IsNullOrEmpty(pilotId)) return;

            _activePlan = await _apiService.FetchSimbrief(pilotId);
            if (_activePlan != null)
            {
                lblFlightNumber.Text = $"Flight: {_activePlan.Airline}{_activePlan.FlightNumber}";
                lblRoute.Text = $"Route: {_activePlan.Origin} -> {_activePlan.Destination}";
                btnStartFlight.Enabled = true;
                AddLog("Plan de SimBrief cargado.");
            }
        }

        private async void btnStartFlight_Click(object sender, EventArgs e)
        {
            if (_activePilot == null) return;
            try
            {
                _activePirepId = await _apiService.PrefileFlight(_activePlan, _activePilot);
                if (!string.IsNullOrEmpty(_activePirepId))
                {
                    _currentPhase = FlightPhase.Boarding;
                    _flightStartTime = DateTime.Now;
                    telemetryTimer.Start();
                    btnFinishFlight.Enabled = true;
                    btnFetchSimbrief.Enabled = false;
                    AddLog("Vuelo iniciado en phpVMS. Esperando conexión con el simulador...");

                    // Si ya estaba conectado de antes, el evento no se disparará de nuevo, 
                    // así que lo llamamos manualmente una vez:
                    if (_fsuipc.IsConnected) SendWelcomeLog();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            // Usamos Invoke porque los eventos de FSUIPC vienen de otro hilo
            if (lstLogs.InvokeRequired)
            {
                lstLogs.Invoke(new Action(() => lstLogs.Items.Insert(0, $"{timestamp} - {message}")));
            }
            else
            {
                lstLogs.Items.Insert(0, $"{timestamp} - {message}");
            }
        }

        private void UpdateNetworkUI(bool isSuccessful)
        {
            lblNetworkStatus.Text = isSuccessful ? "📡 Stable" : "⚠️ Latency";
            lblNetworkStatus.ForeColor = isSuccessful ? Color.Green : Color.Red;
        }

        private void SendWelcomeLog()
        {
            if (chkMockMode.Checked) AddLog("--- MODO MOCK ACTIVO ---");
            else if (_fsuipc.IsConnected)
            {
                string title = _fsuipc.GetAircraftTitle();
                string icao = _fsuipc.GetAircraftICAO();

                AddLog($"--- CONECTADO A {_fsuipc.GetSimName()} ---");
                AddLog($"AVIÓN: {title} [{icao}]");

                // Ejemplo de validación simple
                if (_activePlan != null && icao != _activePlan.AircraftIcao)
                {
                    AddLog($"⚠️ ALERTA: El avión del Sim ({icao}) no coincide con SimBrief ({_activePlan.AircraftIcao})");
                }
            }
        }

        private async void FinishFlight()
        {
            if (string.IsNullOrEmpty(_activePirepId)) return;
            telemetryTimer.Stop();

            double fuelAtArrival = chkMockMode.Checked ? 2000 : _fsuipc.GetTotalFuel();
            double fuelUsed = (_activePlan != null) ? Math.Max(0, _activePlan.DepartureFuel - fuelAtArrival) : 0;
            int totalMinutes = Math.Max(1, (int)(DateTime.Now - _flightStartTime).TotalMinutes);

            var finalData = new
            {
                state = "submitted",
                fuel_used = (int)fuelUsed,
                landing_rate = (int)_fsuipc.LastLandingRate,
                flight_time = totalMinutes,
                notes = "vmsOpenAcars Report"
            };

            if (await _apiService.FilePirep(_activePirepId, finalData))
            {
                AddLog("¡Vuelo reportado con éxito!");
                btnStartFlight.Enabled = false;
                btnFetchSimbrief.Enabled = true;
                _activePirepId = "";
            }
        }

        private void btnFinishFlight_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("¿Finalizar vuelo?", "Confirmar", MessageBoxButtons.YesNo) == DialogResult.Yes)
                FinishFlight();
        }
    }
}