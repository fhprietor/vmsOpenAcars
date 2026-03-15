using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using vmsOpenAcars.Models;
using vmsOpenAcars.UI;

namespace vmsOpenAcars.UI.Forms
{
    /// <summary>
    /// Formulario para mostrar y seleccionar las reservas (bids) del piloto.
    /// Solo muestra los vuelos que pueden ser operados desde el aeropuerto actual del piloto.
    /// </summary>
    public class FlightSelectorForm : Form
    {
        private ListView lvFlights;
        private Button btnSelect;
        private Button btnCancel;
        private Label lblTitle;
        private Label lblCurrentAirport;
        private Label lblSummary;
        private List<Flight> _availableFlights;
        private string _currentAirport;

        /// <summary>
        /// Vuelo seleccionado por el piloto
        /// </summary>
        public Flight SelectedFlight { get; private set; }

        /// <summary>
        /// Inicializa una nueva instancia del selector de vuelos
        /// </summary>
        /// <param name="flights">Lista completa de reservas del piloto</param>
        /// <param name="currentAirport">Aeropuerto actual del piloto (según phpVMS)</param>
        public FlightSelectorForm(List<Flight> flights, string currentAirport)
        {
            _currentAirport = currentAirport;

            // Filtrar vuelos que pueden ser operados desde el aeropuerto actual
            _availableFlights = flights
                .Where(f => f.Departure?.Equals(currentAirport, StringComparison.OrdinalIgnoreCase) ?? false)
                .ToList();

            InitializeComponent();
            LoadFlights();
        }

        private void InitializeComponent()
        {
            this.Text = "Select Flight";
            this.Size = new Size(800, 500);
            this.MinimumSize = new Size(750, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Theme.CockpitBackground;
            this.ForeColor = Theme.MainText;
            this.Font = Theme.MainFont;

            // Título
            lblTitle = new Label
            {
                Text = "AVAILABLE FLIGHTS",
                Font = Theme.LargeFont,
                ForeColor = Color.Yellow,
                Location = new Point(20, 15),
                AutoSize = true
            };

            // Información del aeropuerto actual
            lblCurrentAirport = new Label
            {
                Text = $"Current airport: {_currentAirport}",
                Font = Theme.SmallFont,
                ForeColor = Theme.SecondaryText,
                Location = new Point(20, 45),
                AutoSize = true
            };

            // Resumen de vuelos disponibles
            lblSummary = new Label
            {
                Text = $"{_availableFlights.Count} flight(s) available",
                Font = Theme.SmallFont,
                ForeColor = Theme.MainText,
                Location = new Point(20, 70),
                AutoSize = true
            };

            // ListView para mostrar los vuelos con columnas
            lvFlights = new ListView
            {
                Location = new Point(20, 100),
                Size = new Size(740, 300),
                BackColor = Theme.PanelBackground,
                ForeColor = Theme.MainText,
                Font = Theme.MainFont,
                BorderStyle = BorderStyle.FixedSingle,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                HideSelection = false
            };

            // Definir columnas
            lvFlights.Columns.Add("Flight", 100);
            lvFlights.Columns.Add("From → To", 130);
            lvFlights.Columns.Add("Aircraft", 100);
            lvFlights.Columns.Add("Distance", 80);
            lvFlights.Columns.Add("Flight Time", 90);
            lvFlights.Columns.Add("Route", 200);

            // Botón Seleccionar
            btnSelect = new Button
            {
                Text = "SELECT",
                Location = new Point(520, 415),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 100, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.MainFont,
                Enabled = false
            };
            btnSelect.Click += BtnSelect_Click;

            // Botón Cancelar
            btnCancel = new Button
            {
                Text = "CANCEL",
                Location = new Point(630, 415),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(100, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.MainFont
            };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            // Evento de selección en la lista
            lvFlights.SelectedIndexChanged += (s, e) =>
            {
                btnSelect.Enabled = lvFlights.SelectedItems.Count > 0;
            };

            // Doble clic en un vuelo también selecciona
            lvFlights.DoubleClick += (s, e) =>
            {
                if (lvFlights.SelectedItems.Count > 0)
                {
                    BtnSelect_Click(s, e);
                }
            };

            // Agregar controles
            this.Controls.AddRange(new Control[] {
                lblTitle, lblCurrentAirport, lblSummary, lvFlights, btnSelect, btnCancel
            });

            // Si no hay vuelos disponibles, mostrar mensaje
            if (_availableFlights.Count == 0)
            {
                Label lblNoFlights = new Label
                {
                    Text = "⚠️ No flights available from your current airport.",
                    Location = new Point(20, 200),
                    Size = new Size(740, 30),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Theme.Warning,
                    Font = Theme.LargeFont,
                    BackColor = Theme.PanelBackground
                };
                this.Controls.Add(lblNoFlights);
                lvFlights.Visible = false;
            }
        }

        private void LoadFlights()
        {
            lvFlights.Items.Clear();

            foreach (var flight in _availableFlights)
            {
                var item = new ListViewItem($"{flight.Airline}{flight.FlightNumber}");

                // Formatear ruta (acortar si es muy larga)
                string routeDisplay = flight.Route;
                if (routeDisplay?.Length > 30)
                    routeDisplay = routeDisplay.Substring(0, 27) + "...";

                // Calcular tiempo de vuelo en horas y minutos
                string flightTimeDisplay = "N/A";
                if (flight.FlightTime > 0)
                {
                    int hours = flight.FlightTime / 60;
                    int minutes = flight.FlightTime % 60;
                    flightTimeDisplay = $"{hours}h {minutes}m";
                }

                // Distancia en millas náuticas (más común en aviación)
                string distanceDisplay = "N/A";
                if (flight.Distance > 0)
                {
                    distanceDisplay = $"{flight.Distance:F0} NM";
                }

                item.SubItems.Add($"{flight.Departure} → {flight.Arrival}");
                item.SubItems.Add(flight.AircraftType ?? "Unknown");
                item.SubItems.Add(distanceDisplay);
                item.SubItems.Add(flightTimeDisplay);
                item.SubItems.Add(routeDisplay ?? "DIRECT");

                // Guardar el objeto Flight completo en el Tag del item
                item.Tag = flight;

                lvFlights.Items.Add(item);
            }

            // Ajustar ancho de columnas automáticamente
            lvFlights.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void BtnSelect_Click(object sender, EventArgs e)
        {
            if (lvFlights.SelectedItems.Count > 0)
            {
                var selectedItem = lvFlights.SelectedItems[0];
                SelectedFlight = selectedItem.Tag as Flight;

                if (SelectedFlight != null)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }
    }
}