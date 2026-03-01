using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Services;
using vmsOpenAcars.Models;

namespace vmsOpenAcars
{
    public partial class Form1 : Form
    {
        private ApiService _apiService;
        private MockSimulator _mockSimulator = new MockSimulator();
        private SimbriefPlan _activePlan; // Aquí guardaremos el plan de SimBrief
        private string _activePirepId = "";
        private FlightPhase _currentPhase = FlightPhase.Boarding;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            telemetryTimer.Interval = 10000; // 10 segundos es un estándar sano para empezar
            telemetryTimer.Enabled = false;  // Empieza apagado
            _apiService = new ApiService("https://vholar.solardomotica.com", "d3f1372520323bf89903");
        }

        private async void TelemetryTimer_Tick(object sender, EventArgs e)
        {
            telemetryTimer.Stop(); // Pause to avoid overlapping

            try
            {
                // 1. Get data from Simulator (Mock or FSUIPC later)
                _mockSimulator.UpdatePosition();
                UpdateFlightPhase(_mockSimulator.CurrentAlt, _mockSimulator.CurrentGS, _mockSimulator.IsOnGround);

                var telemetry = _mockSimulator.GetTelemetry();
                
                // 2. Logic to determine Flight Phase (Simple version)
                

                // 3. Prepare the Payload for phpVMS
                // Note: phpVMS 7 expects specific fields in the 'updates' endpoint

                // 4. Creamos el objeto de posición individual
                var singlePosition = new
                {
                    lat = _mockSimulator.CurrentLat,
                    lon = _mockSimulator.CurrentLon,
                    alt = _mockSimulator.CurrentAlt,
                    gs = _mockSimulator.CurrentGS,
                    heading = _mockSimulator.CurrentHeading,
                    vs = 0,
                    state = "enroute",
                    fuel = 100,
                    source_name = "vmsOpenAcars",
                    log = "Normal update" // Opcional pero recomendado
                };

                // 5. Envolvemos el objeto en la estructura que pide phpVMS
                var positionReport = new
                {
                    positions = new[] { singlePosition } // ESTO es lo que pide el error "positions field is required"
                };


                System.Diagnostics.Debug.WriteLine($"[MOCK] Lat: {singlePosition.lat}, Lon: {singlePosition.lon}, Alt: {singlePosition.alt}ft, GS: {singlePosition.gs}kt");
                // 6. Send to your HomeServer VPS via ApiService
                if (!string.IsNullOrEmpty(_activePirepId))
                {
                    bool success = await _apiService.SendPositionUpdate(_activePirepId, positionReport);
                    UpdateNetworkUI(success);
                    // Actualizamos el Label con tus variables reales
                    lblStatus.Text = $"Live! Alt: {_mockSimulator.CurrentAlt} | Lat: {_mockSimulator.CurrentLat:F4} | Lon: {_mockSimulator.CurrentLon:F4} | GS: {_mockSimulator.CurrentGS} | H: { _mockSimulator.CurrentHeading}";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                telemetryTimer.Start();
            }
        }
        private void UpdateFlightPhase(int altitude, int groundSpeed, bool isOnGround)
        {
            if (isOnGround)
            {
                if (groundSpeed < 5) _currentPhase = FlightPhase.Boarding;
                else if (groundSpeed >= 5 && groundSpeed < 30) _currentPhase = FlightPhase.TaxiOut;
            }
            else
            {
                if (altitude < 1000) _currentPhase = FlightPhase.Takeoff;
                else _currentPhase = FlightPhase.Enroute;

                // Add more logic here for Approach/Landing later
            }

            lblCurrentPhase.Text = $"Phase: {_currentPhase}";
        }
        private async void StartFlight()
        {
            if (_activePlan == null)
            {
                MessageBox.Show("Please fetch SimBrief data first.");
                return;
            }

            // Llamamos al servicio con el plan que ya descargamos
            // Usamos PascalCase para el método según las convenciones de C#
            _activePirepId = await _apiService.PrefileFlight(_activePlan);

            if (!string.IsNullOrEmpty(_activePirepId))
            {
                lblStatus.Text = "Flight Prefiled: ID " + _activePirepId;

                // Iniciamos el timer de telemetría ahora que tenemos un ID válido
                telemetryTimer.Start();
            }
            else
            {
                lblStatus.Text = "Error prefiling flight to phpVMS";
            }
        }
        private async void btnStartFlight_Click(object sender, EventArgs e)
        {
            // Reseteamos el estado visual
            lblStatus.ForeColor = Color.Black;
            lblStatus.Text = "Status: Registering PIREP on phpVMS...";
            btnStartFlight.Enabled = false;

            try
            {
                // This calls the POST /api/pireps/prefile endpoint
                _activePirepId = await _apiService.PrefileFlight(_activePlan);
                if (!string.IsNullOrEmpty(_activePirepId))
                {
                    lblServerStatus.Text = $"Server: Connected to Vholar | Flight: {_activePlan.FlightNumber}";
                    lblServerStatus.ForeColor = Color.Blue;
                }
                // Si todo sale bien:
                lblStatus.ForeColor = Color.Green;
                lblStatus.Text = $"Status: Live! PIREP ID: {_activePirepId}";
                telemetryTimer.Start();
            }
            catch (Exception ex)
            {
                // AQUÍ mostramos el error de phpVMS (Permission Denied, etc.)
                lblStatus.ForeColor = Color.Red;
                lblStatus.Text = $"Error: {ex.Message}";

                // Rehabilitamos el botón para que el piloto intente corregirlo
                btnStartFlight.Enabled = false;
                btnFetchSimbrief.Enabled = true;
            }
        }

        private async void btnFetchSimbrief_Click(object sender, EventArgs e)
        {
            // Basic validation
            string pilotId = txtSimbriefId.Text.Trim();
            if (string.IsNullOrEmpty(pilotId))
            {
                MessageBox.Show("Please enter a valid SimBrief Pilot ID.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Update UI Status
            btnFetchSimbrief.Enabled = false;
            lblStatus.Text = "Status: Fetching from SimBrief API...";

            // Call the Service
            _activePlan = await _apiService.FetchSimbrief(pilotId);

            if (_activePlan != null)
            {
                // Update labels in the GroupBox
                lblFlightNumber.Text = $"Flight: {_activePlan.Airline}{_activePlan.FlightNumber}";
                lblRoute.Text = $"Route: {_activePlan.Origin} -> {_activePlan.Destination}";

                // Enable the next step
                btnStartFlight.Enabled = true;
                lblStatus.Text = "Status: Flight plan loaded.";
            }
            else
            {
                lblStatus.Text = "Status: Fetch failed. Check Pilot ID.";
                btnFetchSimbrief.Enabled = true;
            }
        }

        private void UpdateNetworkUI(bool isSuccessful)
        {
            if (isSuccessful)
            {
                lblNetworkStatus.Text = "📡 Connection: Stable";
                lblNetworkStatus.ForeColor = Color.Green;

                // Optional: Update a "Last Sync" timestamp
                lblLastUpdate.Text = $"Last Sync: {DateTime.Now:HH:mm:ss}";
            }
            else
            {
                lblNetworkStatus.Text = "⚠️ Connection: Latency/Offline";
                lblNetworkStatus.ForeColor = Color.Red;
            }
        }
    }
}
