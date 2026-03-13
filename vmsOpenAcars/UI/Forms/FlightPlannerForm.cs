// UI/FlightPlannerForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI
{
    public class FlightPlannerForm : Form
    {
        // Controles
        private Label lblTitle;
        private Label lblAirport;
        private ListBox lstFlights;
        private ListBox lstAircraft;
        private Button btnPlanWithSimbrief;
        private Button btnFetchOFP;
        private RichTextBox txtOFPPreview;
        private Button btnClose;
        private ProgressBar progressBar;
        private Label lblStatus;

        // Servicios
        private readonly PhpVmsFlightService _flightService;
        private readonly SimbriefEnhancedService _simbriefService;
        private readonly FlightManager _flightManager;
        private readonly Pilot _currentPilot;
        private readonly string _currentAirport;

        // Datos seleccionados
        private Flight _selectedFlight;
        private Aircraft _selectedAircraft;
        private SimbriefPlan _loadedPlan;

        public FlightPlannerForm(
            PhpVmsFlightService flightService,
            SimbriefEnhancedService simbriefService,
            FlightManager flightManager,
            Pilot currentPilot,
            string currentAirport)
        {
            _flightService = flightService;
            _simbriefService = simbriefService;
            _flightManager = flightManager;
            _currentPilot = currentPilot;
            _currentAirport = currentAirport;

            InitializeComponents();
            this.Load += async (s, e) => await LoadFlightsAsync();
        }

        private void InitializeComponents()
        {
            this.Text = "Flight Planner - vmsOpenAcars";
            this.Size = new Size(900, 700);
            this.BackColor = Theme.CockpitBackground;
            this.ForeColor = Theme.MainText;
            this.Font = Theme.MainFont;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Título
            lblTitle = new Label
            {
                Text = $"✈️ PLANIFICADOR DE VUELO - Aeropuerto: {_currentAirport}",
                Location = new Point(20, 20),
                Size = new Size(600, 30),
                Font = Theme.LargeFont,
                ForeColor = Theme.MainText
            };

            // Label vuelos
            Label lblFlightsTitle = new Label
            {
                Text = "VUELOS DISPONIBLES:",
                Location = new Point(20, 70),
                Size = new Size(200, 20),
                ForeColor = Theme.SecondaryText
            };

            // Lista de vuelos
            lstFlights = new ListBox
            {
                Location = new Point(20, 100),
                Size = new Size(400, 150),
                BackColor = Theme.PanelBackground,
                ForeColor = Theme.MainText,
                Font = Theme.MainFont
            };
            lstFlights.SelectedIndexChanged += LstFlights_SelectedIndexChanged;

            // Label aviones
            Label lblAircraftTitle = new Label
            {
                Text = "AVIONES DISPONIBLES:",
                Location = new Point(20, 270),
                Size = new Size(200, 20),
                ForeColor = Theme.SecondaryText
            };

            // Lista de aviones
            lstAircraft = new ListBox
            {
                Location = new Point(20, 300),
                Size = new Size(400, 100),
                BackColor = Theme.PanelBackground,
                ForeColor = Theme.MainText,
                Font = Theme.MainFont
            };
            lstAircraft.SelectedIndexChanged += LstAircraft_SelectedIndexChanged;

            // Botón Planificar en SimBrief
            btnPlanWithSimbrief = new Button
            {
                Text = "📋 PLANIFICAR EN SIMBRIEF",
                Location = new Point(20, 420),
                Size = new Size(250, 40),
                BackColor = Color.FromArgb(0, 100, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnPlanWithSimbrief.Click += BtnPlanWithSimbrief_Click;

            // Botón Recuperar OFP
            btnFetchOFP = new Button
            {
                Text = "🔄 RECUPERAR OFP",
                Location = new Point(280, 420),
                Size = new Size(140, 40),
                BackColor = Color.FromArgb(0, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnFetchOFP.Click += BtnFetchOFP_Click;

            // Progress Bar
            progressBar = new ProgressBar
            {
                Location = new Point(20, 480),
                Size = new Size(400, 20),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            // Label estado
            lblStatus = new Label
            {
                Location = new Point(20, 510),
                Size = new Size(400, 25),
                ForeColor = Theme.Warning
            };

            // Preview OFP
            Label lblPreview = new Label
            {
                Text = "VISTA PREVIA OFP:",
                Location = new Point(440, 70),
                Size = new Size(200, 20),
                ForeColor = Theme.SecondaryText
            };

            txtOFPPreview = new RichTextBox
            {
                Location = new Point(440, 100),
                Size = new Size(420, 400),
                BackColor = Color.Black,
                ForeColor = Theme.MainText,
                Font = new Font("Consolas", 10),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Botón Cerrar
            btnClose = new Button
            {
                Text = "CERRAR",
                Location = new Point(750, 600),
                Size = new Size(120, 40),
                BackColor = Theme.PanelBackground,
                ForeColor = Theme.MainText,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.Click += (s, e) => this.Close();

            // Agregar controles
            this.Controls.AddRange(new Control[] {
                lblTitle, lblFlightsTitle, lstFlights,
                lblAircraftTitle, lstAircraft,
                btnPlanWithSimbrief, btnFetchOFP,
                progressBar, lblStatus, lblPreview, txtOFPPreview,
                btnClose
            });
        }

        private async Task LoadFlightsAsync()
        {
            progressBar.Visible = true;
            lblStatus.Text = "Cargando vuelos disponibles...";

            try
            {
                var flights = await _flightService.GetAvailableFlightsFromAirport(_currentAirport, _currentPilot);

                lstFlights.Items.Clear();
                foreach (var flight in flights)
                {
                    lstFlights.Items.Add(flight);
                }

                if (flights.Count == 0)
                    lblStatus.Text = "⚠️ No hay vuelos disponibles desde este aeropuerto.";
                else
                    lblStatus.Text = $"✅ {flights.Count} vuelos encontrados.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Error: {ex.Message}";
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async void LstFlights_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstFlights.SelectedItem is Flight selected)
            {
                _selectedFlight = selected;
                btnPlanWithSimbrief.Enabled = false;

                progressBar.Visible = true;
                lblStatus.Text = "Buscando aviones disponibles...";

                try
                {
                    var aircraft = await _flightService.GetAvailableAircraftAtAirport(
                        _currentAirport, _selectedFlight.AircraftType);

                    lstAircraft.Items.Clear();
                    if (aircraft != null)
                    {
                        lstAircraft.Items.Add(aircraft);
                        _selectedAircraft = aircraft;
                        lstAircraft.SelectedIndex = 0;
                        btnPlanWithSimbrief.Enabled = true;
                        lblStatus.Text = "✅ Vuelo y avión seleccionados.";
                    }
                    else
                    {
                        lblStatus.Text = $"⚠️ No hay aviones {_selectedFlight.AircraftType} disponibles.";
                    }
                }
                catch (Exception ex)
                {
                    lblStatus.Text = $"❌ Error: {ex.Message}";
                }
                finally
                {
                    progressBar.Visible = false;
                }
            }
        }

        private void LstAircraft_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstAircraft.SelectedItem is Aircraft aircraft)
            {
                _selectedAircraft = aircraft;
                btnPlanWithSimbrief.Enabled = _selectedFlight != null;
            }
        }

        private async void BtnPlanWithSimbrief_Click(object sender, EventArgs e)
        {
            if (_selectedFlight == null || _selectedAircraft == null) return;

            try
            {
                progressBar.Visible = true;
                lblStatus.Text = "Asignando vuelo...";

                // Asignar vuelo en phpVMS (bid)
                bool assigned = await _flightService.AssignFlightToPilot(_selectedFlight.Id, _currentPilot.Id.ToString());

                if (assigned)
                {
                    lblStatus.Text = "✅ Vuelo asignado. Abriendo SimBrief...";

                    // Generar URL y abrir navegador
                    string url = _simbriefService.GenerateDispatchUrl(_selectedFlight, _currentPilot, _selectedAircraft);
                    System.Diagnostics.Process.Start(url);

                    lblStatus.Text = "✈️ Planifica en SimBrief, luego presiona 'RECUPERAR OFP'";
                }
                else
                {
                    lblStatus.Text = "❌ No se pudo asignar el vuelo.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Error: {ex.Message}";
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async void BtnFetchOFP_Click(object sender, EventArgs e)
        {
            string simbriefUser = System.Configuration.ConfigurationManager.AppSettings["simbrief_user"];

            if (string.IsNullOrEmpty(simbriefUser))
            {
                MessageBox.Show("Configura 'simbrief_user' en App.config", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            progressBar.Visible = true;
            lblStatus.Text = "Recuperando OFP desde SimBrief...";

            try
            {
                var plan = await _simbriefService.FetchAndParseOFP(simbriefUser);

                if (plan != null)
                {
                    _loadedPlan = plan;
                    DisplayOFP(plan);
                    lblStatus.Text = $"✅ OFP cargado: {plan.FlightNumber}";

                    // Actualizar _activePlan en MainForm
                    // Necesitamos una forma de pasar el plan de vuelta
                    this.DialogResult = DialogResult.OK;
                }
                else
                {
                    lblStatus.Text = "❌ No se encontró OFP. Genera un plan en SimBrief primero.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Error: {ex.Message}";
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private void DisplayOFP(SimbriefPlan plan)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"╔════════════════════════════════════════╗");
            sb.AppendLine($"║     OPERATIONAL FLIGHT PLAN           ║");
            sb.AppendLine($"╠════════════════════════════════════════╣");
            sb.AppendLine($"║ Flight: {plan.Airline}{plan.FlightNumber,-15}        ║");
            sb.AppendLine($"║ Route:  {plan.Origin} → {plan.Destination}             ║");
            sb.AppendLine($"║ Aircraft: {plan.AircraftIcao} ({plan.Registration})   ║");
            sb.AppendLine($"║ Fuel: {plan.BlockFuel,5:F0} kg                        ║");
            sb.AppendLine($"║ Pax: {plan.PaxCount,3}                               ║");
            sb.AppendLine($"║ Route: {plan.Route} ║");
            sb.AppendLine($"╚════════════════════════════════════════╝");

            txtOFPPreview.Text = sb.ToString();
        }

        public SimbriefPlan GetLoadedPlan() => _loadedPlan;
    }
}