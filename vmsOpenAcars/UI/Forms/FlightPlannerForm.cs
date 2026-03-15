using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;

namespace vmsOpenAcars.UI
{
    public class FlightPlannerForm : Form
    {
        // Controles
        private TabControl tabControl;
        private TabPage tabAvailable;
        private TabPage tabBids;
        private ListView lvBids; // ListView para las reservas
        private ListView lvAvailableFlights; // Renombrado para claridad

        private bool _hasPlanned = false;

        private Label lblTitle;
        private Label lblAirport;
        private ListBox lstAircraft;
        private Button btnPlanWithSimbrief;
        private Button btnFetchOFP;
        private RichTextBox txtOFPPreview;
        private Button btnClose;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Label lblSummary;

        // Servicios
        private readonly ApiService _apiService;
        private readonly PhpVmsFlightService _flightService;
        private readonly SimbriefEnhancedService _simbriefService;
        private readonly FlightManager _flightManager;
        private readonly Pilot _currentPilot;
        private readonly string _currentAirport;

        // Datos seleccionados
        private Flight _selectedFlight;
        private Aircraft _selectedAircraft;
        private SimbriefPlan _loadedPlan;
        private List<Flight> _bids;

        public FlightPlannerForm(
            ApiService apiService,
            PhpVmsFlightService flightService,
            SimbriefEnhancedService simbriefService,
            FlightManager flightManager,
            Pilot currentPilot,
            string currentAirport,
            List<Flight> bids = null)
        {
            _apiService = apiService;
            _flightService = flightService;
            _simbriefService = simbriefService;
            _flightManager = flightManager;
            _currentPilot = currentPilot;
            _currentAirport = currentAirport;
            _bids = bids;

            InitializeComponents();

            this.Load += async (s, e) => await LoadAllDataAsync();
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
                Text = $"✈️ FLIGHT PLANNER - Airport: {_currentAirport}",
                Location = new Point(20, 20),
                Size = new Size(600, 30),
                Font = Theme.LargeFont,
                ForeColor = Theme.MainText
            };

            // TabControl para separar vuelos disponibles y reservas
            tabControl = new TabControl
            {
                Location = new Point(20, 60),
                Size = new Size(840, 350),
                BackColor = Theme.PanelBackground
            };

            // Pestaña de vuelos disponibles
            tabAvailable = new TabPage("Available Flights");
            InitializeAvailableFlightsTab();

            // Pestaña de reservas (bids)
            tabBids = new TabPage("My Bids");
            InitializeBidsTab();

            tabControl.Controls.Add(tabAvailable);
            tabControl.Controls.Add(tabBids);

            // Panel de selección de aeronave
            Label lblAircraftTitle = new Label
            {
                Text = "AVAILABLE AIRCRAFT:",
                Location = new Point(20, 420),
                Size = new Size(200, 20),
                ForeColor = Theme.SecondaryText
            };

            lstAircraft = new ListBox
            {
                Location = new Point(20, 445),
                Size = new Size(400, 100),
                BackColor = Theme.PanelBackground,
                ForeColor = Theme.MainText,
                Font = Theme.MainFont
            };
            lstAircraft.SelectedIndexChanged += LstAircraft_SelectedIndexChanged;

            // Botón Planificar en SimBrief
            btnPlanWithSimbrief = new Button
            {
                Text = "📋 PLAN IN SIMBRIEF",
                Location = new Point(20, 555),
                Size = new Size(200, 40),
                BackColor = Color.FromArgb(0, 100, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnPlanWithSimbrief.Click += BtnPlanWithSimbrief_Click;

            // Botón Recuperar OFP
            btnFetchOFP = new Button
            {
                Text = "🔄 FETCH OFP",
                Location = new Point(230, 555),
                Size = new Size(140, 40),
                BackColor = Color.FromArgb(0, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnFetchOFP.Click += BtnFetchOFP_Click;

            // Progress Bar
            progressBar = new ProgressBar
            {
                Location = new Point(20, 605),
                Size = new Size(400, 20),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            // Label estado
            lblStatus = new Label
            {
                Location = new Point(20, 630),
                Size = new Size(400, 25),
                ForeColor = Theme.Warning
            };

            // Preview OFP
            Label lblPreview = new Label
            {
                Text = "OFP PREVIEW:",
                Location = new Point(440, 420),
                Size = new Size(200, 20),
                ForeColor = Theme.SecondaryText
            };

            txtOFPPreview = new RichTextBox
            {
                Location = new Point(440, 445),
                Size = new Size(420, 180),
                BackColor = Color.Black,
                ForeColor = Theme.MainText,
                Font = new Font("Consolas", 10),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Botón Cerrar
            btnClose = new Button
            {
                Text = "CLOSE",
                //Location = new Point(750, 630),
                Location = new Point(740, 630),
                Size = new Size(100, 30),
                BackColor = Theme.PanelBackground,
                ForeColor = Theme.MainText,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.Click += (s, e) => this.Close();

            // Botón Aceptar (inicialmente deshabilitado)
            Button btnAccept = new Button
            {
                Text = "ACCEPT",
                Location = new Point(630, 630), // Ajusta según tu layout
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 100, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnAccept.Click += BtnAccept_Click;
            this.Controls.Add(btnAccept);

            // Agregar controles
            this.Controls.AddRange(new Control[] {
                lblTitle,
                tabControl,
                lblAircraftTitle,
                lstAircraft,
                btnPlanWithSimbrief,
                btnFetchOFP,
                progressBar,
                lblStatus,
                lblPreview,
                txtOFPPreview,
                btnClose
            });
        }

        private void InitializeAvailableFlightsTab()
        {
            // ListView para vuelos disponibles
            lvAvailableFlights = new ListView
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.PanelBackground,
                ForeColor = Theme.MainText,
                Font = Theme.MainFont,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            lvAvailableFlights.Columns.Add("Flight", 100);
            lvAvailableFlights.Columns.Add("From → To", 130);
            lvAvailableFlights.Columns.Add("Aircraft", 100);
            lvAvailableFlights.Columns.Add("Distance (NM)", 100);
            lvAvailableFlights.Columns.Add("Flight Time", 90);
            lvAvailableFlights.Columns.Add("Route", 200);

            lvAvailableFlights.SelectedIndexChanged += LvAvailableFlights_SelectedIndexChanged;
            lvAvailableFlights.DoubleClick += (s, e) => BtnPlanWithSimbrief_Click(s, e);

            tabAvailable.Controls.Add(lvAvailableFlights);
        }

        private void InitializeBidsTab()
        {
            // ListView para reservas
            lvBids = new ListView
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.PanelBackground,
                ForeColor = Theme.MainText,
                Font = Theme.MainFont,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            lvBids.Columns.Add("Flight", 100);
            lvBids.Columns.Add("From → To", 130);
            lvBids.Columns.Add("Aircraft", 100);
            lvBids.Columns.Add("Distance (NM)", 100);
            lvBids.Columns.Add("Flight Time", 90);
            lvBids.Columns.Add("Route", 200);

            lvBids.SelectedIndexChanged += LvBids_SelectedIndexChanged;
            lvBids.DoubleClick += (s, e) => BtnPlanWithSimbrief_Click(s, e);

            tabBids.Controls.Add(lvBids);
        }

        private async Task LoadAllDataAsync()
        {
            await Task.WhenAll(LoadAvailableFlightsAsync(), LoadBidsAsync());
        }

        private async Task LoadAvailableFlightsAsync()
        {
            try
            {
                progressBar.Visible = true;
                lblStatus.Text = "Loading available flights...";

                // 1. Obtener todas las aeronaves disponibles en el aeropuerto actual
                var availableAircraft = await _flightService.GetAvailableAircraftAtAirport(_currentAirport, null);
                var availableTypes = availableAircraft.Select(a => a.Type).Distinct().ToList();

                // 2. Obtener todos los vuelos desde el aeropuerto
                var flights = await _flightService.GetAvailableFlightsFromAirport(_currentAirport, _currentPilot);

                // 3. Filtrar los vuelos que tengan al menos un tipo de aeronave en común con los disponibles
                var validFlights = flights.Where(f =>
                    f.AllowedAircraftTypes.Any(type => availableTypes.Contains(type))
                ).ToList();

                // 4. Mostrar los vuelos filtrados
                lvAvailableFlights.Items.Clear();
                foreach (var flight in validFlights)
                {
                    var item = CreateFlightListViewItem(flight);
                    lvAvailableFlights.Items.Add(item);
                }

                lvAvailableFlights.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                lblStatus.Text = $"✅ {validFlights.Count} flights available (from {flights.Count} total).";
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

        private async Task LoadBidsAsync()
        {
            try
            {
                var bids = await _apiService.GetPilotBids(); // ← CORREGIDO: sin parámetro

                // Filtrar por aeropuerto actual
                var availableBids = bids.Where(b =>
                    b.Departure?.Equals(_currentAirport, StringComparison.OrdinalIgnoreCase) ?? false)
                    .ToList();

                lvBids.Items.Clear();
                foreach (var bid in availableBids)
                {
                    var item = CreateFlightListViewItem(bid);
                    lvBids.Items.Add(item);
                }

                lvBids.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading bids: {ex}");
            }
        }

        private ListViewItem CreateFlightListViewItem(Flight flight)
        {
            var item = new ListViewItem($"{flight.Airline}{flight.FlightNumber}");

            string routeDisplay = flight.Route;
            if (routeDisplay?.Length > 30)
                routeDisplay = routeDisplay.Substring(0, 27) + "...";

            string flightTimeDisplay = "N/A";
            if (flight.FlightTime > 0)
            {
                int hours = flight.FlightTime / 60;
                int minutes = flight.FlightTime % 60;
                flightTimeDisplay = $"{hours}h {minutes}m";
            }

            string distanceDisplay = flight.Distance > 0 ? $"{flight.Distance} NM" : "N/A";

            item.SubItems.Add($"{flight.Departure} → {flight.Arrival}");
            item.SubItems.Add(flight.AllowedAircraftTypesDisplay ?? "Unknown");
            item.SubItems.Add(distanceDisplay);
            item.SubItems.Add(flightTimeDisplay);
            item.SubItems.Add(routeDisplay ?? "DIRECT");

            item.Tag = flight;
            return item;
        }

        private async void LvAvailableFlights_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvAvailableFlights.SelectedItems.Count > 0)
            {
                var item = lvAvailableFlights.SelectedItems[0];
                _selectedFlight = item.Tag as Flight;
                await LoadAircraftForSelectedFlight();
            }
        }

        private async void LvBids_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvBids.SelectedItems.Count > 0)
            {
                var item = lvBids.SelectedItems[0];
                _selectedFlight = item.Tag as Flight;
                await LoadAircraftForSelectedFlight();
            }
        }

        private async Task LoadAircraftForSelectedFlight()
        {
            if (_selectedFlight == null) return;

            btnPlanWithSimbrief.Enabled = false;
            progressBar.Visible = true;
            lblStatus.Text = "Searching for available aircraft...";

            try
            {
                var aircrafts = await _flightService.GetAvailableAircraftAtAirport(
                    _currentAirport, _selectedFlight.AllowedAircraftTypes);

                lstAircraft.Items.Clear();
                if (aircrafts != null && aircrafts.Any())
                {
                    foreach (var aircraft in aircrafts)
                    {
                        lstAircraft.Items.Add(aircraft);
                    }
                    lstAircraft.SelectedIndex = 0;
                    _selectedAircraft = aircrafts.First();
                    btnPlanWithSimbrief.Enabled = true;
                    lblStatus.Text = $"✅ {aircrafts.Count} aircraft available.";
                }
                else
                {
                    lblStatus.Text = $"⚠️ No suitable aircraft available at {_currentAirport}.";
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
                lblStatus.Text = "Assigning flight...";

                bool assigned = await _flightService.AssignFlightToPilot(_selectedFlight.Id, _currentPilot.Id.ToString());

                if (assigned)
                {
                    lblStatus.Text = "✅ Flight assigned. Opening SimBrief...";
                    string url = _simbriefService.GenerateDispatchUrl(_selectedFlight, _currentPilot, _selectedAircraft);
                    System.Diagnostics.Process.Start(url);
                    lblStatus.Text = "✈️ Plan in SimBrief, then click 'FETCH OFP'";
                    _hasPlanned = true;
                }
                else
                {
                    lblStatus.Text = "❌ Could not assign flight.";
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
        private void BtnAccept_Click(object sender, EventArgs e)
        {
            if (_loadedPlan != null)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private async void BtnFetchOFP_Click(object sender, EventArgs e)
        {
            if (_selectedFlight == null || _selectedAircraft == null)
            {
                MessageBox.Show("Please select a flight and aircraft first.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string simbriefUser = System.Configuration.ConfigurationManager.AppSettings["simbrief_user"];
            if (string.IsNullOrEmpty(simbriefUser))
            {
                MessageBox.Show("Configure 'simbrief_user' in App.config", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            progressBar.Visible = true;
            lblStatus.Text = "Fetching OFP from SimBrief...";

            try
            {
                var plan = await _simbriefService.FetchAndParseOFP(simbriefUser);

                if (plan != null)
                {
                    // ===== VALIDACIONES =====
                    // 1. Validar que el plan coincida con el vuelo seleccionado
                    bool originMatch = plan.Origin.Equals(_selectedFlight.Departure, StringComparison.OrdinalIgnoreCase);
                    bool destMatch = plan.Destination.Equals(_selectedFlight.Arrival, StringComparison.OrdinalIgnoreCase);
                    bool aircraftMatch = plan.AircraftIcao.Equals(_selectedAircraft.Type, StringComparison.OrdinalIgnoreCase);

                    if (!originMatch || !destMatch || !aircraftMatch)
                    {
                        string errorMsg = "The fetched OFP does not match the selected flight/aircraft.\n\n";
                        errorMsg += $"Expected: {_selectedFlight.Departure} → {_selectedFlight.Arrival} ({_selectedAircraft.Type})\n";
                        errorMsg += $"Got: {plan.Origin} → {plan.Destination} ({plan.AircraftIcao})";
                        MessageBox.Show(errorMsg, "OFP Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblStatus.Text = "❌ OFP mismatch. Please generate the correct plan in SimBrief.";
                        return;
                    }

                    // 1. Validar antigüedad (máximo 2 horas)
                    DateTime planTime = DateTimeOffset.FromUnixTimeSeconds(plan.TimeGenerated).UtcDateTime;
                    if (DateTime.UtcNow - planTime > TimeSpan.FromHours(2))
                    {
                        MessageBox.Show("El plan tiene más de 2 horas. Genera uno nuevo.", "Plan expirado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // 2. Validar que la fecha de salida sea hoy o futura (comparando con la fecha actual UTC)
                    DateTime scheduledOff = DateTimeOffset.FromUnixTimeSeconds(plan.ScheduledOffTime).UtcDateTime.Date;
                    if (scheduledOff < DateTime.UtcNow.Date)
                    {
                        MessageBox.Show("La fecha de salida programada es anterior a hoy.", "Fecha inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    // Validar que la matrícula coincida
                    if (!plan.Registration.Equals(_selectedAircraft.Registration, StringComparison.OrdinalIgnoreCase))
                    {
                        string errorMsg = "The fetched OFP does not match the selected aircraft registration.\n\n";
                        errorMsg += $"Expected: {_selectedAircraft.Registration}\n";
                        errorMsg += $"Got: {plan.Registration}";
                        MessageBox.Show(errorMsg, "Aircraft Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblStatus.Text = "❌ Aircraft registration mismatch. Please generate the correct plan in SimBrief.";
                        return;
                    }
                    // Si todo está bien
                    _loadedPlan = plan;
                    DisplayOFP(plan);
                    lblStatus.Text = $"✅ OFP loaded: {plan.FlightNumber}";

                    var btnAccept = this.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "ACCEPT");
                    if (btnAccept != null) btnAccept.Enabled = true;
                }
                else
                {
                    lblStatus.Text = "❌ No OFP found. Generate a plan in SimBrief first.";
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
        public Flight GetSelectedFlight() => _selectedFlight;
    }
}