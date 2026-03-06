using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Services;
using vmsOpenAcars.Models;
using System.Configuration;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.UI;
using vmsOpenAcars.Models;

namespace vmsOpenAcars
{
    public partial class MainForm : Form
    {
        // Paneles principales
        private Panel pnlTop;      // FMA style
        private Label lblPhase;
        private Label lblSpeed;
        private Label lblAltitude;
        private Label lblAir;

        private Label lblRoute;
        private Panel pnlCenter;   // ND / Map placeholder
        private Panel pnlBottom;   // Info de vuelo / ACARS

        private ApiService _apiService;
        private MockSimulator _mockSimulator = new MockSimulator();
        private SimbriefPlan _activePlan;
        private Pilot _activePilot;
        private FsuipcService _fsuipc = new FsuipcService();
        private FlightManager _flightManager;
        private bool _routeLoaded = false;

        private System.Windows.Forms.Timer _uiTimer;

        public MainForm()
        {
            InitializeComponent();

            SetupFsuipcEvents();

            InitializeCockpitPanels();  // crea pnlTop, pnlCenter, pnlBottom
            InitializeFmaPanel();       // usa el pnlTop ya creado
            _routeLoaded = false;
            SetRouteDisplay();

            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 1000;
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
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
                    _flightManager = new FlightManager(_apiService);
                    AddLog($"Configuración cargada: {vms_api_url}");
                    _flightManager.PhaseChanged += (newPhase) =>
                    {
                        this.Invoke((MethodInvoker)(() =>
                        {
                            UpdatePhase(newPhase);
                            UpdateAirStatus(newPhase);
                        }));
                    };

                    _flightManager.OnLog += AddLog;
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

        private void InitializeCockpitPanels()
        {
            // Panel Superior - FMA style
            pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Theme.FMAPanelBackground
            };
            this.Controls.Add(pnlTop);

            // Panel Central - ND placeholder
            pnlCenter = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.NDBackground
            };
            this.Controls.Add(pnlCenter);

            // Panel Inferior - ACARS, info de vuelo
            pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 150,
                BackColor = Theme.ACARSBackground
            };
            this.Controls.Add(pnlBottom);
        }
        private void InitializeFmaPanel()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Theme.FMAPanelBackground
            };

            for (int i = 0; i < 5; i++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            pnlTop.Controls.Add(layout);

            lblPhase = CreateFmaLabel("PHASE", "STANDBY");
            lblSpeed = CreateFmaLabel("SPD", "--- KT");
            lblAltitude = CreateFmaLabel("ALT", "---- FT");
            lblAir = CreateFmaLabel("AIR", "GROUND");
            lblRoute = CreateFmaLabel("ROUTE", "----/----");

            layout.Controls.Add(lblPhase, 0, 0);
            layout.Controls.Add(lblSpeed, 1, 0);
            layout.Controls.Add(lblAltitude, 2, 0);
            layout.Controls.Add(lblAir, 3, 0);
            layout.Controls.Add(lblRoute, 4, 0);
        }
        private Label CreateFmaLabel(string title, string value)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.LargeFont,
                ForeColor = Theme.MainText,
                BackColor = Theme.FMAPanelBackground,
                Text = $"{title}\n{value}"
            };
        }
        private void UpdateSpeed(int speed)
        {
            lblSpeed.Text = $"SPD\n{speed} KT";

            if (speed > 3)
                lblSpeed.ForeColor = Theme.Enroute;
            else
                lblSpeed.ForeColor = Color.Gray;
        }

        private void UpdateAltitude(int altitude)
        {
            lblAltitude.Text = $"ALT\n{altitude} FT";

            if (_flightManager.CurrentPhase.IsAirborne())
                lblAltitude.ForeColor = Theme.Enroute;   // Verde
            else
                lblAltitude.ForeColor = Color.Gray;
        }

        public void UpdatePhase(FlightPhase phase)
        {
            lblPhase.Text = "PHASE\n" + phase.ToString().ToUpper();

            Color phaseColor;

            switch (phase)
            {
                case FlightPhase.Idle:
                    phaseColor = Color.Gray;
                    break;
                case FlightPhase.Boarding:
                case FlightPhase.Pushback:
                case FlightPhase.TaxiOut:
                case FlightPhase.TaxiIn:
                    phaseColor = Theme.Taxi;
                    break;

                case FlightPhase.Takeoff:
                    phaseColor = Theme.Takeoff;
                    break;

                case FlightPhase.Climb:
                case FlightPhase.Enroute:
                    phaseColor = Theme.Enroute;
                    break;

                case FlightPhase.Descent:
                case FlightPhase.Approach:
                    phaseColor = Theme.Approach;
                    break;

                case FlightPhase.Completed:
                    phaseColor = Theme.Arrived;
                    break;

                default:
                    phaseColor = Theme.MainText;
                    break;
            }

            lblPhase.ForeColor = phaseColor;
        }

        public void UpdateAirStatus(FlightPhase phase)
        {
            if (phase == FlightPhase.Idle)
            {
                lblAir.Text = "AIR\n---";
                lblAir.ForeColor = Color.Gray;
                return;
            }

            if (phase.IsAirborne())
            {
                lblAir.Text = "AIR\nAIRBORNE";
                lblAir.ForeColor = Theme.Enroute;
            }
            else
            {
                lblAir.Text = "AIR\nGROUND";
                lblAir.ForeColor = Theme.Taxi;
            }
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (_flightManager == null)
                return;

            if (_flightManager.CurrentPhase == FlightPhase.Idle)
            {
                lblPhase.Text = "PHASE\nSTANDBY";
                lblPhase.ForeColor = Color.Gray;

                lblSpeed.Text = "SPD\n--- KT";
                lblSpeed.ForeColor = Color.Gray;

                lblAltitude.Text = "ALT\n---- FT";
                lblAltitude.ForeColor = Color.Gray;

                lblAir.Text = "AIR\n---";
                lblAir.ForeColor = Color.Gray;

                return;
            }

            // Si NO está en Idle → comportamiento normal
            UpdateSpeed(_flightManager.CurrentGroundSpeed);
            UpdateAltitude(_flightManager.CurrentAltitude);
            UpdatePhase(_flightManager.CurrentPhase);
            UpdateAirStatus(_flightManager.CurrentPhase);
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
                    if (!string.IsNullOrEmpty(_flightManager?.ActivePirepId))
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

                    if (!_fsuipc.IsConnected)
                        _fsuipc.Connect();

                    if (_fsuipc.IsConnected)
                    {
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
                

                // 🔹 Actualizamos el FlightManager (esto ahora también actualiza la fase)
                _flightManager?.UpdateTelemetry(
                    (int)alt,
                    (int)gs,
                    (int)vs,
                    isOnGround,
                    fuel
                );

                // 🔹 Enviar posición si hay PIREP activo
                if (!string.IsNullOrEmpty(_flightManager?.ActivePirepId))
                {
                    var positionReport = new
                    {
                        positions = new[]
                        {
                    new
                    {
                        lat = lat,
                        lon = lon,
                        alt = Math.Round(alt),
                        gs = Math.Round(gs),
                        heading = Math.Round(hdg),
                        vs = Math.Round(vs),
                        state = _flightManager.CurrentPhase.ToString().ToLower(),
                        fuel = fuel,
                        source_name = "vmsOpenAcars"
                    }
                }
                    };

                    bool success = await _apiService.SendPositionUpdate(
                        _flightManager.ActivePirepId,
                        positionReport
                    );

                    UpdateNetworkUI(success);
                }

                // 🔹 Actualización de UI
                lblStatus.Text = string.Format(
                    "{0} | Alt: {1:F0}ft | GS: {2:F0}kt | VS: {3:F0}",
                    _flightManager.CurrentPhase.ToString().ToUpper(),
                    alt,
                    gs,
                    vs
                );
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error: " + ex.Message;
            }
            finally
            {
                telemetryTimer.Start();
            }
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
                _routeLoaded = true;
                SetRouteDisplay();
                btnStartFlight.Enabled = true;
                AddLog("Plan de SimBrief cargado.");
            }
        }

        private async void btnStartFlight_Click(object sender, EventArgs e)
        {
            if (_activePilot == null || _activePlan == null) return;

            if (_flightManager == null) return;

            bool started = await _flightManager.StartFlight(_activePlan, _activePilot);

            if (started)
            {
                telemetryTimer.Start();
                btnFinishFlight.Enabled = true;
                btnFetchSimbrief.Enabled = false;

                AddLog("Vuelo iniciado en phpVMS. Esperando conexión con el simulador...");

                if (_fsuipc.IsConnected)
                    SendWelcomeLog();
            }
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
            if (string.IsNullOrEmpty(_flightManager?.ActivePirepId)) return;
            telemetryTimer.Stop();

            double fuelAtArrival = chkMockMode.Checked ? 2000 : _fsuipc.GetTotalFuel();
            double fuelUsed = (_activePlan != null) ? Math.Max(0, _activePlan.DepartureFuel - fuelAtArrival) : 0;
            int totalMinutes = Math.Max(1, (int)(DateTime.Now - _flightManager.FlightStartTime).TotalMinutes);

            var finalData = new
            {
                state = "submitted",
                fuel_used = (int)fuelUsed,
                landing_rate = (int)_fsuipc.LastLandingRate,
                flight_time = totalMinutes,
                notes = "vmsOpenAcars Report"
            };

            if (await _apiService.FilePirep(_flightManager?.ActivePirepId, finalData))
            {
                AddLog("¡Vuelo reportado con éxito!");
                btnStartFlight.Enabled = false;
                btnFetchSimbrief.Enabled = true;
            }
        }

        private void btnFinishFlight_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("¿Finalizar vuelo?", "Confirmar", MessageBoxButtons.YesNo) == DialogResult.Yes)
                FinishFlight();
        }

        private void SetRouteDisplay()
        {
            if (!_routeLoaded)
            {
                lblRoute.Text = "ROUTE\n----/----";
                lblRoute.ForeColor = Color.Gray;
                return;
            }

            lblRoute.Text = $"ROUTE\n{_activePlan.Origin}/{_activePlan.Destination}";
            lblRoute.ForeColor = Theme.MainText; // Cyan
        }
    }

}