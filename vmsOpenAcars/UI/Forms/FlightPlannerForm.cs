using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;

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

            tabControl.Controls.Add(tabBids);
            tabControl.Controls.Add(tabAvailable);

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
                Font = new Font("Consolas", 10, FontStyle.Regular),
                WordWrap = false,           
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
                var bids = await _apiService.GetPilotBids();

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
                // Si el vuelo tiene un aircraft_id específico (es un bid con avión asignado)
                if (!string.IsNullOrEmpty(_selectedFlight.AircraftId))
                {
                    // Buscar TODAS las aeronaves disponibles primero
                    var allAircraft = await _flightService.GetAvailableAircraftAtAirport(
                        _currentAirport, _selectedFlight.AllowedAircraftTypes);

                    // Filtrar la que tenga el ID específico
                    var specificAircraft = allAircraft.FirstOrDefault(a => a.Id == _selectedFlight.AircraftId);

                    lstAircraft.Items.Clear();
                    if (specificAircraft != null)
                    {
                        lstAircraft.Items.Add(specificAircraft);
                        lstAircraft.SelectedIndex = 0;
                        _selectedAircraft = specificAircraft;
                        btnPlanWithSimbrief.Enabled = true;
                        lblStatus.Text = $"✅ Assigned aircraft: {specificAircraft.Registration} ({specificAircraft.Type})";
                    }
                    else
                    {
                        lblStatus.Text = $"⚠️ Assigned aircraft not found at {_currentAirport}.";
                    }
                }
                else
                {
                    // Comportamiento normal: mostrar todas las aeronaves disponibles del tipo permitido
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
                EcamDialog.Show(this,
                    "Please select a flight and aircraft first.",
                    "WARNING",
                    EcamDialogButtons.OK);
                return;
            }

            string simbriefUser = System.Configuration.ConfigurationManager.AppSettings["simbrief_user"];
            if (string.IsNullOrEmpty(simbriefUser))
            {
                EcamDialog.Show(this,
                    "Configure 'simbrief_user' in App.config",
                    "ERROR",
                    EcamDialogButtons.OK);
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
                        string errorMsg = $"The fetched OFP does not match the selected flight/aircraft.\n\n" +
                            $"Expected: {_selectedFlight.Departure} → {_selectedFlight.Arrival} ({_selectedAircraft.Type})\n" +
                            $"Got: {plan.Origin} → {plan.Destination} ({plan.AircraftIcao})";

                        EcamDialog.Show(this, errorMsg, "OFP MISMATCH", EcamDialogButtons.OK);
                        lblStatus.Text = "❌ OFP mismatch. Please generate the correct plan in SimBrief.";
                        return;
                    }

                    // 2. Validar antigüedad (máximo 2 horas)
                    DateTime planTime = DateTimeOffset.FromUnixTimeSeconds(plan.TimeGenerated).UtcDateTime;
                    if (DateTime.UtcNow - planTime > TimeSpan.FromHours(2))
                    {
                        EcamDialog.Show(this,
                            "The plan is more than 2 hours old. Please generate a new one.",
                            "EXPIRED PLAN",
                            EcamDialogButtons.OK);
                        return;
                    }

                    // 3. Validar que la fecha de salida sea hoy o futura
                    DateTime scheduledOff = DateTimeOffset.FromUnixTimeSeconds(plan.ScheduledOffTime).UtcDateTime.Date;
                    if (scheduledOff < DateTime.UtcNow.Date)
                    {
                        EcamDialog.Show(this,
                            "The scheduled departure date is in the past.",
                            "INVALID DATE",
                            EcamDialogButtons.OK);
                        return;
                    }

                    // 4. Validar que la matrícula coincida
                    if (!plan.Registration.Equals(_selectedAircraft.Registration, StringComparison.OrdinalIgnoreCase))
                    {
                        string errorMsg = $"The fetched OFP does not match the selected aircraft registration.\n\n" +
                            $"Expected: {_selectedAircraft.Registration}\n" +
                            $"Got: {plan.Registration}";

                        EcamDialog.Show(this, errorMsg, "REGISTRATION MISMATCH", EcamDialogButtons.OK);
                        lblStatus.Text = "❌ Aircraft registration mismatch. Please generate the correct plan in SimBrief.";
                        return;
                    }
                    plan.AircraftId = int.Parse(_selectedAircraft.Id);
                    // Si todo está bien
                    plan.FlightId = _selectedFlight.Id;
                    plan.BidId = _selectedFlight.BidId;
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
            const int width = 42; // Ancho total de la línea (incluyendo bordes)
            const int contentWidth = width - 4; // Espacio disponible para texto (restando "║ " y " ║")

            var sb = new StringBuilder();

            // Línea superior
            sb.AppendLine("╔" + new string('═', width - 2) + "╗");

            // Título centrado
            string title = "OPERATIONAL FLIGHT PLAN";
            int padding = (width - 2 - title.Length) / 2;
            sb.AppendLine("║" + new string(' ', padding) + title + new string(' ', width - 2 - title.Length - padding) + "║");

            // Línea separadora
            sb.AppendLine("╠" + new string('═', width - 2) + "╣");

            // Líneas de contenido con formato fijo
            AddContentLine(sb, $"Flight: {plan.Airline}{plan.FlightNumber}", width);
            AddContentLine(sb, $"Route:  {plan.Origin} → {plan.Destination}", width);
            if (!string.IsNullOrEmpty(plan.Alternate))
            {
                AddContentLine(sb, $"Alternate: {plan.Alternate}", width);
            }
            AddContentLine(sb, $"Aircraft: {plan.AircraftIcao} ({plan.Registration})", width);
            AddContentLine(sb, $"Fuel: {plan.BlockFuel:F0} {plan.Units ?? "kg"}", width);
            AddContentLine(sb, $"ZFW: {plan.ZeroFuelWeight:F0} {plan.Units ?? "kg"}", width);
            AddContentLine(sb, $"Distance: {plan.Distance:F1} NM", width);

            // Mostrar tiempo en formato HH:MM
            int hours = plan.EstTimeEnroute / 3600;
            int minutes = (plan.EstTimeEnroute % 3600) / 60;
            AddContentLine(sb, $"Est Time: {hours}h {minutes}m", width);

            AddContentLine(sb, $"Route: {plan.Route}", width);
            // Línea inferior
            sb.AppendLine("╚" + new string('═', width - 2) + "╝");

            txtOFPPreview.Text = sb.ToString();
        }

        private void AddContentLine(StringBuilder sb, string text, int width)
        {
            // Calcular espacios a la derecha para que la línea tenga exactamente 'width' caracteres
            int padding = width - 2 - text.Length; // -2 por los bordes "║ " y " ║"
            if (padding < 0) padding = 0; // Si el texto es más largo, no añadimos espacios
            var linea = "║ " + text + new string(' ', padding) + " ║";
            sb.AppendLine("║ " + text + new string(' ', padding) + " ║");
        }

        public SimbriefPlan GetLoadedPlan() => _loadedPlan;
        public Flight GetSelectedFlight() => _selectedFlight;
    }
}