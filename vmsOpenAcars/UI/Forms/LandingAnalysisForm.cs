using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.UI.Forms
{
    public class LandingAnalysisForm : Form
    {
        private readonly IList<(FlightRecord Record, List<ApproachTrackPoint> Track)> _flights;
        private readonly Func<int, List<FlareTrackPoint>> _flareLoader;
        private readonly Func<int, bool>                  _flareStartedLoader;

        private static readonly Color[] TrackColors =
        {
            Color.FromArgb( 30, 144, 255),   // DodgerBlue
            Color.FromArgb(255, 140,   0),   // DarkOrange
            Color.FromArgb( 50, 205,  50),   // LimeGreen
            Color.FromArgb(186,  85, 211),   // MediumOrchid
            Color.FromArgb(255, 215,   0),   // Gold
        };

        private bool IsComparison => _flights.Count > 1;

        // ── Closeup del aterrizaje ────────────────────────────────────────────────
        // El perfil vertical puede ampliar el tramo final. El encuadre, las marcas y las bandas los
        // decide `Helpers/TouchdownCloseupGeometry` (puro, con test); aquí solo se traduce a píxeles.
        private TableLayoutPanel _chartLayout;
        private Chart            _verticalChart;
        private Panel            _pnlCloseup;
        private CheckBox         _chkCloseup;
        private ComboBox         _cboCloseupScale;
        private Label            _lblCloseupSummary;
        private Label            _lblCloseupSource;

        /// <summary>
        /// Suelo del eje Y en el closeup **cuando no hay dato con el que ajustarlo**: unos pies por
        /// debajo de AGL 0 para que la barra de pista —que va **en** AGL 0, porque ahí está el
        /// suelo— no quede cortada por el borde inferior. Con datos, el suelo y el techo los decide
        /// `Helpers/CloseupVerticalAxis` (puro, con test).
        /// </summary>
        private const double VerticalFloorFt = -50.0;

        /// <summary>Las dos escalas de siempre del closeup, de más ancha a más estrecha. El porqué de
        /// cada número —muestras por tramo y toques que caben— está en `TouchdownCloseupGeometry`.</summary>
        private static readonly (string Label, double BeforeFt, double AfterFt)[] BaseCloseupScales =
        {
            ("Last 5 000 ft", TouchdownCloseupGeometry.WideBeforeFt,  TouchdownCloseupGeometry.WideAfterFt),
            ("Last 2 500 ft", TouchdownCloseupGeometry.CloseBeforeFt, TouchdownCloseupGeometry.CloseAfterFt),
        };

        /// <summary>
        /// La escala fina (**±1 000 ft**), que **solo se ofrece cuando el vuelo tiene traza del flare**
        /// (10 Hz): con la traza de 2 s ese encuadre se queda en dos o tres muestras y sería una
        /// escala que promete un detalle que la base no tiene. Los vuelos anteriores a `flare_track`
        /// ven exactamente el mismo desplegable que veían.
        /// </summary>
        private static readonly (string Label, double BeforeFt, double AfterFt) FineCloseupScale =
            ("Last 1 000 ft", TouchdownCloseupGeometry.FineBeforeFt, TouchdownCloseupGeometry.FineAfterFt);

        /// <summary>Las escalas que ofrece **este** formulario, según la traza disponible.</summary>
        private readonly List<(string Label, double BeforeFt, double AfterFt)> _closeupScales =
            new List<(string Label, double BeforeFt, double AfterFt)>();

        /// <summary>
        /// La **traza fina del flare** del vuelo, si la tiene. Se carga una sola vez al construir el
        /// formulario porque de ella dependen tres cosas que se deciden antes de pintar: si se ofrece
        /// la escala de ±1 000 ft, qué traza pinta el closeup y cómo se rotula su origen. Sin
        /// cargador (comparación, o los tests que no lo inyectan) se queda en nulo y todo es como
        /// antes de `flare_track`.
        /// </summary>
        private readonly List<FlareTrackPoint> _flareSamples;

        public LandingAnalysisForm(IList<(FlightRecord Record, List<ApproachTrackPoint> Track)> flights)
            : this(flights, null, null)
        {
        }

        /// <summary>
        /// El mismo formulario, con acceso a la **traza fina del flare** (`flare_track`) para el botón
        /// FLARE. Se inyecta como delegado y no como servicio para que el formulario siga sin conocer
        /// `ILandingLogService` — y para poder instanciarlo en los tests sin base de datos: sin
        /// cargador, el botón FLARE no aparece y nada más cambia.
        /// </summary>
        public LandingAnalysisForm(IList<(FlightRecord Record, List<ApproachTrackPoint> Track)> flights,
                                   Func<int, List<FlareTrackPoint>> flareLoader,
                                   Func<int, bool> flareCaptureStartedLoader)
        {
            _flights              = flights;
            _flareLoader          = flareLoader;
            _flareStartedLoader   = flareCaptureStartedLoader;
            _flareSamples         = LoadFlareSamples();
            BuildCloseupScaleList();
            BuildUI();
            PopulateCharts();
        }

        /// <summary>
        /// Lee la traza fina del vuelo **una vez**, degradando sin datos: sin cargador, en comparación
        /// (el closeup es de un vuelo y una pista) o si la lectura falla, se queda en nulo y el
        /// formulario enseña lo de siempre. Una base que no responde no puede tumbar el análisis.
        /// </summary>
        private List<FlareTrackPoint> LoadFlareSamples()
        {
            if (_flareLoader == null || _flights == null || _flights.Count != 1) return null;
            try
            {
                return _flareLoader(_flights[0].Record.Id) ?? new List<FlareTrackPoint>();
            }
            catch
            {
                return null;
            }
        }

        private void BuildCloseupScaleList()
        {
            _closeupScales.Clear();
            _closeupScales.AddRange(BaseCloseupScales);
            // La escala fina solo con la traza que la sostiene: ver `FineCloseupScale`.
            if (HasFlareTrack) _closeupScales.Add(FineCloseupScale);
        }

        /// <summary>¿Este vuelo trae traza fina del flare? Sin muestras, nada cambia.</summary>
        private bool HasFlareTrack => _flareSamples != null && _flareSamples.Count > 0;

        // ── Layout ────────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            var first = _flights[0].Record;

            Text            = IsComparison
                ? $"Comparison — {_flights.Count} flights"
                : $"Landing Analysis — {first.FlightNumber} {first.DisplayRoute}";
            Size            = new Size(1000, 740);
            MinimumSize     = new Size(800, 600);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor       = Color.FromArgb(18, 26, 36);
            Padding         = new Padding(2);

            Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(100, 180, 255), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0,
                        ClientSize.Width - 1, ClientSize.Height - 1);
            };

            var pnlTitle  = BuildTitleBar(first);
            var pnlHeader = IsComparison ? BuildComparisonHeader() : BuildSingleHeader(first);

            // METAR strip — single flight only
            Panel pnlMetar = null;
            Panel pnlWind  = null;
            if (!IsComparison)
            {
                pnlMetar = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Color.FromArgb(15, 22, 32) };
                pnlMetar.Controls.Add(new Label
                {
                    Text      = string.IsNullOrEmpty(first.MetarRaw) ? "(no METAR)" : first.MetarRaw,
                    Font      = new Font("Consolas", 9),
                    ForeColor = Color.FromArgb(180, 200, 220),
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding   = new Padding(8, 0, 0, 0)
                });

                // Franja del viento del aterrizaje: el METAR de arriba dice de dónde soplaba en el
                // aeropuerto; esto dice **el viento que el avión tenía en el momento del contacto**
                // (FSUIPC) y sus componentes contra el eje **verdadero** de la pista
                // (Helpers/WindComponents). Se pinta **debajo** del METAR: el orden de apilado de
                // `Dock.Top` va del último añadido al primero, así que el METAR se añade después.
                //
                // Es una línea de texto y no un gráfico a propósito: la traza de la aproximación
                // (`approach_track`) no guarda viento por punto —solo hay una medida, la del
                // touchdown—, así que un gráfico tendría un único valor y no aportaría nada que la
                // línea no diga ya.
                pnlWind = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.FromArgb(15, 22, 32) };
                pnlWind.Controls.Add(new Label
                {
                    Text      = LandingWeatherLine.Display(first.RunwayName, first.WindAtLanding),
                    Font      = new Font("Consolas", 9),
                    ForeColor = Color.FromArgb(170, 215, 185),
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding   = new Padding(8, 0, 0, 0)
                });
            }

            // 2×2 chart grid
            _chartLayout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 2,
                BackColor   = Color.Transparent
            };
            _chartLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _chartLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _chartLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _chartLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            _verticalChart = MakeChart("Vertical Profile (AGL)", "Distance to threshold (NM)", "AGL (ft)", "VERTICAL");

            _chartLayout.Controls.Add(_verticalChart, 0, 0);
            _chartLayout.Controls.Add(MakeChart("Lateral Deviation",        "Distance to threshold (NM)", "Deviation (ft)", "LATERAL"),  1, 0);
            _chartLayout.Controls.Add(MakeChart("Indicated Airspeed",       "Distance to threshold (NM)", "IAS (kt)",       "IAS"),      0, 1);
            _chartLayout.Controls.Add(MakeChart("Vertical Speed",           "Distance to threshold (NM)", "VS (fpm)",       "VS"),       1, 1);

            Controls.Add(_chartLayout);
            // La barra del closeup vive en su **propio** panel acoplado abajo, no en una celda de la
            // rejilla: en un `TableLayoutPanel` dos controles en la misma celda no dan error (el
            // último se pinta encima). Es la trampa que la guía documenta para `SettingsForm`.
            //
            // No se ofrece en modo comparación: el closeup es de **una** pista y un **un** toque, y
            // con tres vuelos de pistas distintas el encuadre no significaría nada.
            _pnlCloseup = IsComparison ? null : BuildCloseupBar();
            if (_pnlCloseup != null) Controls.Add(_pnlCloseup);
            // Orden de apilado de `Dock.Top`: el último añadido queda arriba. Así el METAR va encima
            // de la franja del viento, y las dos debajo de la cabecera del vuelo.
            if (pnlWind  != null) Controls.Add(pnlWind);
            if (pnlMetar != null) Controls.Add(pnlMetar);
            Controls.Add(pnlHeader);
            Controls.Add(pnlTitle);
        }

        // ── Closeup ───────────────────────────────────────────────────────────────

        /// <summary>
        /// La barra del closeup: un interruptor y la escala. Va en un `FlowLayoutPanel` dentro de su
        /// panel acoplado abajo, así que no comparte celda con nada y no puede tapar los gráficos.
        /// </summary>
        private Panel BuildCloseupBar()
        {
            var pnl = new Panel
            {
                Dock      = DockStyle.Bottom,
                // 32 px y no 28: la barra lleva ahora el botón FLARE, que con `AutoSize` mide ~24 px
                // más su margen. Con 28 el botón quedaba recortado por el borde del panel —el mismo
                // tipo de recorte silencioso que la guía avisa para el reparto de `SettingsForm`—.
                Height    = 32,
                BackColor = Color.FromArgb(15, 22, 32)
            };

            var flow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                Padding       = new Padding(6, 3, 0, 0),
                BackColor     = Color.Transparent
            };

            _chkCloseup = new CheckBox
            {
                Text      = "Closeup",
                Font      = new Font("Consolas", 9, FontStyle.Bold),
                ForeColor = Color.Cyan,
                AutoSize  = true,
                Margin    = new Padding(0, 5, 10, 0)
            };

            _cboCloseupScale = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Consolas", 9),
                Width         = 130,
                Enabled       = false,
                Margin        = new Padding(0, 2, 0, 0),
                FlatStyle     = FlatStyle.Flat,
                BackColor     = Color.FromArgb(28, 40, 52),
                ForeColor     = Color.White
            };
            foreach (var scale in _closeupScales) _cboCloseupScale.Items.Add(scale.Label);
            _cboCloseupScale.SelectedIndex = 0;

            _lblCloseupSummary = new Label
            {
                AutoSize  = true,
                Font      = new Font("Consolas", 8),
                ForeColor = Color.FromArgb(170, 215, 185),
                Margin    = new Padding(14, 7, 0, 0)
            };

            // ── El origen de la traza ─────────────────────────────────────────────
            // El closeup puede pintar dos cosas **que no son lo mismo**: la traza fina del flare
            // (10 Hz) o la de aproximación (2 s). Se dice aquí, en su propio rótulo, para que nadie
            // lea una resolución por otra. El texto sale del idioma (clave en los dos `.json`), no
            // del literal.
            _lblCloseupSource = new Label
            {
                AutoSize  = false,
                Font      = new Font("Consolas", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 200, 255),
                Margin    = new Padding(14, 8, 0, 0),
                // Ancho y alto **fijos**: un `FlowLayoutPanel` sin `WrapContents` no avisa de que un
                // control no cabe —el que sobra simplemente no se ve—, y con `AutoSize` en falso un
                // `Label` mide 23 px de alto por defecto, más que la franja de 29 útiles de la barra
                // (32 px menos el `Padding` de arriba). Se quedaba fuera por abajo.
                Width     = 190,
                Height    = 15,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            // ── El botón FLARE ────────────────────────────────────────────────────
            // Abre el gráfico del flare en **su propia ventana** (`FlareAnalysisForm`): la rejilla
            // 2×2 de los cuatro gráficos no se toca, que es lo que exige la guía —dos controles en
            // una celda de `TableLayoutPanel` se pintan uno sobre otro **sin dar error**—. Solo
            // aparece si quien construye el formulario sabe leer `flare_track` (`_flareLoader`); sin
            // esa fuente no hay nada que enseñar y el botón sería un botón muerto.
            Button btnFlare = null;
            if (_flareLoader != null && _flights.Count == 1)
            {
                btnFlare = new Button
                {
                    Text      = "FLARE",
                    Font      = new Font("Consolas", 9, FontStyle.Bold),
                    ForeColor = Color.FromArgb(255, 215, 0),
                    BackColor = Color.FromArgb(28, 40, 52),
                    FlatStyle = FlatStyle.Flat,
                    AutoSize  = true,
                    Margin    = new Padding(0, 1, 0, 0)
                };
                btnFlare.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 130);
                btnFlare.Click += (s, e) => OpenFlareAnalysis();
            }

            _chkCloseup.CheckedChanged += (s, e) =>
            {
                _cboCloseupScale.Enabled = _chkCloseup.Checked;
                PopulateCharts();
            };
            _cboCloseupScale.SelectedIndexChanged += (s, e) =>
            {
                if (_chkCloseup.Checked) PopulateCharts();
            };

            flow.Controls.Add(_chkCloseup);
            flow.Controls.Add(_cboCloseupScale);
            // El origen va **antes** del resumen: el resumen es un texto largo y de ancho variable, y
            // un control que no quepa en un `FlowLayoutPanel` sin `WrapContents` simplemente no se ve.
            flow.Controls.Add(_lblCloseupSource);
            flow.Controls.Add(_lblCloseupSummary);
            if (btnFlare != null) flow.Controls.Add(btnFlare);
            pnl.Controls.Add(flow);
            return pnl;
        }

        /// <summary>
        /// Abre la ventana del gráfico del flare con la traza fina del vuelo. Si el vuelo no tiene
        /// `flare_track` (los anteriores a esta traza), el formulario lo dice: **no se rellena con
        /// `approach_track`**, que sería vender 2 s de muestreo como si fueran 0,1 s.
        /// </summary>
        private void OpenFlareAnalysis()
        {
            if (_flights.Count != 1 || _flareLoader == null) return;

            var rec      = _flights[0].Record;
            var samples  = _flareLoader(rec.Id) ?? new List<FlareTrackPoint>();
            bool started = _flareStartedLoader != null && _flareStartedLoader(rec.Id);

            new FlareAnalysisForm(rec, samples, started).Show(this);
        }

        /// <summary>El encuadre pedido, o `null` si el closeup está apagado o no aplica.</summary>
        private TouchdownCloseup ComputeCloseup()
        {
            if (_chkCloseup == null || !_chkCloseup.Checked || _flights.Count != 1) return null;

            var scale = CurrentCloseupScale();
            var rec   = _flights[0].Record;

            return TouchdownCloseupGeometry.Compute(
                rec.TouchdownDistFt,
                rec.RunwayLengthFt ?? 0.0,
                scale.BeforeFt, scale.AfterFt);
        }

        /// <summary>La escala elegida en el desplegable, acotada a las que ofrece este vuelo.</summary>
        private (string Label, double BeforeFt, double AfterFt) CurrentCloseupScale()
        {
            int index = Math.Max(0, Math.Min(_closeupScales.Count - 1, _cboCloseupScale.SelectedIndex));
            return _closeupScales[index];
        }

        // ── Puntos de medida para los tests (el formulario se instancia sin enseñarlo, como
        //    `EcamDialogTests`, que es como este proyecto comprueba el reparto de un formulario) ──

        internal TableLayoutPanel ChartGrid          => _chartLayout;
        internal Panel            CloseupBar         => _pnlCloseup;
        internal CheckBox         CloseupToggle      => _chkCloseup;
        internal ComboBox         CloseupScaleSelect => _cboCloseupScale;
        internal Chart            VerticalProfile    => _verticalChart;
        internal Label            CloseupSummaryLabel => _lblCloseupSummary;
        internal Label            CloseupSourceLabel  => _lblCloseupSource;

        // ── Title bar ─────────────────────────────────────────────────────────────

        private Panel BuildTitleBar(FlightRecord first)
        {
            var pnl = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = Color.FromArgb(28, 40, 52) };

            string txt = IsComparison
                ? $"COMPARISON  ·  {_flights.Count} FLIGHTS"
                : $"LANDING ANALYSIS  ·  {first.FlightNumber}  {first.DisplayRoute}  ·  RWY {first.RunwayName}";

            pnl.Controls.Add(new Label
            {
                Text     = txt,
                Font     = new Font("Consolas", 11, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location  = new Point(10, 8),
                AutoSize  = true
            });

            var btnX = new Button
            {
                Text      = "✕",
                Font      = new Font("Arial", 12, FontStyle.Bold),
                Size      = new Size(30, 25),
                Location  = new Point(ClientSize.Width - 35, 5),
                BackColor = Color.FromArgb(150, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            btnX.FlatAppearance.BorderSize = 0;
            btnX.Click += (s, e) => Close();
            pnl.Controls.Add(btnX);
            Resize += (s, e) => btnX.Location = new Point(ClientSize.Width - 35, 5);

            bool dragging = false; Point dragStart = Point.Empty;
            pnl.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { dragging = true; dragStart = new Point(e.X, e.Y); } };
            pnl.MouseMove += (s, e) => { if (dragging) { var p = PointToScreen(e.Location); Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y); } };
            pnl.MouseUp   += (s, e) => dragging = false;

            return pnl;
        }

        // ── Single-flight header ──────────────────────────────────────────────────

        private Panel BuildSingleHeader(FlightRecord rec)
        {
            var pnl = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(22, 32, 44) };

            Color scoreClr = rec.Score >= 90 ? Color.LightGreen
                           : rec.Score >= 75 ? Color.Yellow
                           : Color.OrangeRed;

            var stats = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, BackColor = Color.Transparent
            };
            for (int i = 0; i < 8; i++)
                stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 8));
            stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            stats.Controls.Add(StatCell("DATE",   rec.DisplayDate),              0, 0);
            stats.Controls.Add(StatCell("VS",     rec.DisplayLandingRate),        1, 0);
            stats.Controls.Add(StatCell("G",      $"{rec.GForce:F2}g"),          2, 0);
            stats.Controls.Add(StatCell("DIST",   $"{rec.TouchdownDistFt:F0} ft"), 3, 0);
            stats.Controls.Add(StatCell("CL DEV", $"{rec.CenterlineDevFt:F0} ft"), 4, 0);

            var scoreCell = StatCell("SCORE", rec.DisplayScore);
            // Controls[1] is the value label (Controls[0] is the caption)
            if (scoreCell.Controls.Count > 1 && scoreCell.Controls[1] is Label sv)
                sv.ForeColor = scoreClr;
            stats.Controls.Add(scoreCell, 5, 0);
            stats.Controls.Add(StatCell("RUNWAY", rec.RunwayName), 6, 0);
            // El viento del aterrizaje, también en el detalle del vuelo: el METAR y las componentes
            // tienen su franja debajo, pero aquí queda a la vista sin buscarla (y junto al resto del
            // detalle, que es donde el piloto compara «qué tal fue la toma»).
            stats.Controls.Add(StatCell("WIND", WindComponents.FormatRaw(rec.WindAtLanding)), 7, 0);

            pnl.Controls.Add(stats);
            return pnl;
        }

        // ── Comparison header ─────────────────────────────────────────────────────

        private Panel BuildComparisonHeader()
        {
            const int rowH = 24;
            var pnl = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = _flights.Count * rowH + 10,
                BackColor = Color.FromArgb(22, 32, 44)
            };

            for (int i = 0; i < _flights.Count; i++)
            {
                var rec = _flights[i].Record;
                Color c  = TrackColors[i % TrackColors.Length];

                pnl.Controls.Add(new Panel
                {
                    BackColor = c,
                    Size      = new Size(10, 10),
                    Location  = new Point(6, 11 + i * rowH)
                });

                string scoreTag = rec.Score >= 90 ? "★★★" : rec.Score >= 75 ? "★★" : "★";
                string date    = rec.FlightDate.ToLocalTime().ToString("MM-dd HH:mm");
                string line = $"  {rec.FlightNumber,-10}  {date}  {rec.Origin}→{rec.Destination}  " +
                              $"RWY {rec.RunwayName,-4}  {rec.DisplayLandingRate,9}  " +
                              $"{rec.GForce:F2}g  " +
                              $"DIST {rec.TouchdownDistFt:F0} ft  CL {rec.CenterlineDevFt:F0} ft  " +
                              $"SCORE {rec.DisplayScore} {scoreTag}";

                pnl.Controls.Add(new Label
                {
                    Text      = line,
                    Font      = new Font("Consolas", 9, FontStyle.Bold),
                    ForeColor = c,
                    Location  = new Point(22, 4 + i * rowH),
                    AutoSize  = true
                });
            }

            return pnl;
        }

        // ── Chart factory ─────────────────────────────────────────────────────────

        private Chart MakeChart(string title, string xLabel, string yLabel, string tag)
        {
            var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 30, 42), Tag = tag };

            var area = new ChartArea("main") { BackColor = Color.FromArgb(20, 30, 42) };
            StyleAxis(area.AxisX, xLabel, Color.FromArgb(150, 170, 190));
            StyleAxis(area.AxisY, yLabel, Color.FromArgb(150, 170, 190));
            area.AxisX.IsReversed              = true;
            area.AxisX.Minimum                 = 0;
            area.AxisX.MajorGrid.LineColor     = Color.FromArgb(40, 55, 70);
            area.AxisY.MajorGrid.LineColor     = Color.FromArgb(40, 55, 70);
            area.AxisX.LineColor               = Color.FromArgb(80, 100, 120);
            area.AxisY.LineColor               = Color.FromArgb(80, 100, 120);
            chart.ChartAreas.Add(area);

            chart.Titles.Add(new Title(title)
            {
                Font      = new Font("Consolas", 10, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Docking   = Docking.Top,
                Alignment = ContentAlignment.TopLeft
            });

            chart.Legends.Add(new Legend
            {
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font      = new Font("Consolas", 8),
                Docking   = Docking.Bottom
            });

            return chart;
        }

        // ── Data population ───────────────────────────────────────────────────────

        private void PopulateCharts()
        {
            if (_flights == null || _flights.Count == 0) return;

            var closeup = ComputeCloseup();

            // Collect charts by tag
            var charts = new Dictionary<string, Chart>();
            foreach (Control c in FindChartControls(this))
                if (c is Chart ch && ch.Tag is string t)
                    charts[t] = ch;

            // Global max distance across all flights
            double maxDist = 0;
            foreach (var (_, track) in _flights)
                foreach (var pt in track)
                    if (pt.DistNm > maxDist) maxDist = pt.DistNm;
            maxDist = Math.Ceiling(maxDist * 10) / 10.0;

            // **Sin una sola distancia no hay nada que pintar, pero hay que salir bien.** Los cuatro
            // gráficos se quedan con su área y sin ninguna serie: es el estado en el que el motor no
            // puede estimar el intervalo de sus ejes y lanza `InvalidOperationException` al pintarse
            // (la pila `EstimateAxis ← SetDefaultAxesValues ← Chart.SetData`, la del popup). Se cierran
            // las cuatro antes de salir, y así un vuelo sin traza de aproximación se abre y se ve.
            if (maxDist <= 0)
            {
                foreach (var ch in charts.Values) ChartAxisSafety.EnsureExplicit(ch);
                return;
            }

            // Overall Vref (average IAS across all tracks)
            double vref = ComputeOverallVref();

            // Repintado limpio: el closeup reconstruye el perfil cada vez que se cambia de escala, y
            // las series y las bandas de la pasada anterior no pueden quedarse detrás.
            foreach (var ch in charts.Values)
            {
                ch.Series.Clear();
                ch.Annotations.Clear();
                foreach (var area in ch.ChartAreas)
                    area.AxisX.StripLines.Clear();
            }

            // El perfil vertical cambia de unidad con el closeup: con 9 500 ft de encuadre, las
            // etiquetas en NM («0,4 / 0,2 / 0,0») no dicen nada, y en pies se lee de un vistazo. Los
            // otros tres gráficos siguen en NM, que es su escala natural.
            double vx = closeup != null ? TouchdownCloseupGeometry.FeetPerNm : 1.0;

            // X axis bounds
            foreach (var ch in charts.Values)
                ch.ChartAreas["main"].AxisX.Maximum = maxDist;

            var vArea = charts["VERTICAL"].ChartAreas["main"];
            // Con el closeup manda el encuadre pedido, no hasta dónde llega la traza: la escala es
            // una ventana fija y no cambia de tamaño vuelo a vuelo (si la traza es más corta, lo que
            // queda a la izquierda se ve vacío, que es la verdad).
            double vMax = closeup != null ? closeup.BeforeFt : maxDist;
            vArea.AxisX.Maximum = vMax;
            vArea.AxisX.Minimum = closeup != null ? -closeup.AfterFt : 0.0;
            vArea.AxisX.Title   = closeup != null ? "Distance to threshold (ft)"
                                                  : "Distance to threshold (NM)";

            // ── El eje X del closeup, en pies enteros ─────────────────────────────
            // Sin esto el motor de gráficos reparte los 9 500 ft del encuadre en cuatro marcas y
            // saca −2 250 · −750 · 750 · 2 250 · 3 750, más las fracciones que se veían en los
            // bordes. El paso y los rótulos los decide `Helpers/CloseupAxis` (puro, con test): THR
            // clavado en el umbral y los miles separados. Al volver al eje en NM se restauran los
            // valores automáticos, que allí sí son los buenos.
            if (closeup != null)
                CloseupAxis.ApplyTo(vArea.AxisX, closeup.BeforeFt, closeup.AfterFt);
            else
                CloseupAxis.Reset(vArea.AxisX);

            // ── Qué traza pinta el perfil vertical ────────────────────────────────
            // Con el closeup encendido, si el vuelo tiene traza fina del flare (10 Hz) se pinta
            // **esa**; si no, la de 2 s, que es lo que había. Fuera del closeup el perfil vertical
            // es el descenso entero en NM y siempre sale de `approach_track`: el flare solo captura
            // los últimos 1 500 ft.
            var source = closeup != null
                ? CloseupTrackSource.Pick(HasFlareTrack ? _flareSamples.Count : 0)
                : CloseupTraceSource.Approach;

            bool useFlare = closeup != null && source == CloseupTraceSource.Flare;
            var verticalPoints = closeup != null
                ? (useFlare ? CloseupTrackSource.FlarePoints(_flareSamples)
                            : CloseupTrackSource.ApproachPoints(_flights[0].Track))
                : null;

            // ── El eje Y: la escala del tramo que se ve ───────────────────────────
            // Lo decide `Helpers/CloseupVerticalAxis` (puro, con test) sobre las muestras **que
            // entran en el encuadre**: la Y del perfil entero (miles de pies, la traza arranca a
            // 2 563 ft AGL en el vuelo 41) aplastaba el tramo final contra la línea del suelo.
            // Sin dato con el que ajustar, la Y se queda como estaba.
            var fit = closeup != null
                ? CloseupVerticalAxis.Fit(verticalPoints, closeup.BeforeFt, closeup.AfterFt)
                : CloseupVerticalFit.None();

            ApplyVerticalAxis(vArea, closeup, fit);

            // Shared reference lines (added first so they appear behind track data)
            AddRefSeries(charts["VERTICAL"], "3° ref",
                Color.FromArgb(0, 200, 100), ChartDashStyle.Dash,
                new[] { (vMax, (vMax / vx) * 319.0), (0.001, 0.0) }, visible: true);

            AddRefSeries(charts["LATERAL"], "CL",
                Color.FromArgb(80, 130, 80), ChartDashStyle.Dot,
                new[] { (maxDist, 0.0), (0.001, 0.0) }, visible: false);

            AddRefSeries(charts["IAS"], $"Avg {vref:F0} kt",
                Color.FromArgb(255, 180, 0), ChartDashStyle.Dash,
                new[] { (maxDist, vref), (0.001, vref) }, visible: true);

            AddRefSeries(charts["VS"], "zero",
                Color.FromArgb(80, 130, 80), ChartDashStyle.Dot,
                new[] { (maxDist, 0.0), (0.001, 0.0) }, visible: false);

            // Las marcas del closeup van **antes** de la traza: la barra de pista y las líneas del
            // umbral y del toque no deben tapar la curva, que es lo que se viene a mirar.
            if (closeup != null)
                AddCloseupDecorations(charts["VERTICAL"], vArea, closeup,
                                      fit.HasData ? fit.MinimumFt : VerticalFloorFt,
                                      fit.HasData ? fit.MaximumFt : CloseupTopFt(closeup));

            // One series per flight per chart
            for (int i = 0; i < _flights.Count; i++)
            {
                var (rec, track) = _flights[i];
                Color color = TrackColors[i % TrackColors.Length];
                // Con el closeup encendido, el nombre de la serie lo dice **en la leyenda**: la traza
                // fina (10 Hz) y la de 2 s no son lo mismo y la leyenda viaja en el propio gráfico.
                string name = IsComparison ? $"{rec.FlightNumber} #{i + 1}"
                            : closeup != null ? L._(CloseupTrackSource.LabelKey(source))
                            : "Actual";

                if (useFlare)
                    AddFlareSeries(charts["VERTICAL"], name, color, verticalPoints);
                else
                    AddTrackSeries(charts["VERTICAL"], name, color, track, pt => pt.AglFt, smooth: false, xFactor: vx);

                AddTrackSeries(charts["LATERAL"],  name, color, track, pt => pt.LateralFt, smooth: true);
                AddTrackSeries(charts["IAS"],      name, color, track, pt => pt.IasKt,     smooth: true);
                AddTrackSeries(charts["VS"],       name, color, track, pt => pt.VsFpm,     smooth: true);
            }

            if (_lblCloseupSource != null)
                _lblCloseupSource.Text = L._(CloseupTrackSource.LabelKey(source));

            // Y axis post-tuning de los otros tres gráficos. La Y del perfil vertical ya la dejó
            // puesta `ApplyVerticalAxis` antes de las marcas.
            charts["LATERAL"].ChartAreas["main"].AxisY.Title    = "Dev (ft)  + right  – left";
            charts["IAS"].ChartAreas["main"].AxisY.Minimum      = Math.Floor((vref - 20) / 10) * 10;
            charts["IAS"].ChartAreas["main"].AxisY.Maximum      = Math.Ceiling((vref + 20) / 10) * 10;

            if (_lblCloseupSummary != null)
                _lblCloseupSummary.Text = closeup != null
                    ? closeup.Summary + LandingTrackNote()
                    : "";

            // ── El blindaje del pintado ───────────────────────────────────────────
            // Último paso: con todas las series y las bandas puestas, cada área queda en un estado
            // explícito. Un área sin datos —el perfil lateral de un vuelo sin desviación, o cualquier
            // gráfico cuando la traza no trae esa magnitud— se marca «sin datos» en vez de dejar el
            // eje en automático, que es lo que lanza al pintar (ver `ChartAxisSafety`).
            foreach (var ch in charts.Values) ChartAxisSafety.EnsureExplicit(ch);
        }

        /// <summary>
        /// **Lo que dice la traza fina de este aterrizaje**, en el resumen del closeup: el tiempo
        /// umbral→toma, los flaps con los que se tomó —y si cambiaron en el flare— y dónde se cortó la
        /// potencia. Son los **mismos tres helpers puros** que lo publican en la ventana del flare, en
        /// la línea del aterrizaje y en el logbook, así que las tres pantallas no pueden discrepar.
        ///
        /// Sale de la traza **fina** (`_flareSamples`, 10 Hz), que es la única que sostiene los tres
        /// datos. **Sin dato no se añade nada** —ni un cero ni un guion—: el resumen del closeup se
        /// queda exactamente como estaba, y quien quiera el detalle abre la ventana FLARE.
        ///
        /// La familia del avión sale del **registro del vuelo** (`FlightRecord.AircraftIcao`), no del
        /// OFP: es la que de verdad voló, y sin ella los flaps se enseñan en porcentaje.
        /// </summary>
        private string LandingTrackNote()
        {
            var timeline = ThresholdToTouchdown.Compute(_flareSamples);

            string family = _flights.Count > 0 ? _flights[0].Record.AircraftIcao : null;
            var flaps = FlapTrackSummary.Compute(_flareSamples, family);
            var power = PowerCut.Compute(_flareSamples);

            var sb = new System.Text.StringBuilder();
            if (timeline.HasValue)
                sb.Append("  ·  ").Append(L._("Landing_ThrToTd", timeline.Seconds));

            if (flaps.HasTrack && flaps.AtThreshold != null)
            {
                sb.Append("  ·  ").Append(L._("Landing_FlapsHeader")).Append(" ").Append(flaps.AtThreshold.Text);
                if (flaps.AtTouchdown != null && flaps.Changed)
                    sb.Append("  ").Append(L._("Landing_FlapsChanged",
                                               flaps.AtThreshold.ShortText, flaps.AtTouchdown.ShortText));
            }

            if (power.HasValue)
                sb.Append("  ·  ").Append(L._(power.EngineCount == 2 ? "Landing_PowerCut"
                                                                    : "Landing_PowerCutEngine1",
                                              power.SecondsBeforeTouchdown));

            return sb.ToString();
        }

        /// <summary>
        /// **El eje Y del perfil vertical.** Lo decide <see cref="CloseupVerticalAxis"/> sobre las
        /// muestras que entran en el encuadre: el suelo, el techo y el paso salen de ahí, con el cero
        /// del terreno dentro para que la altura real se entienda.
        ///
        /// **Degradar sin datos**: si el helper no tiene con qué ajustar (tramo sin muestras) el eje
        /// se queda como estaba —suelo en `VerticalFloorFt` y el resto automático—, que es lo que
        /// pide la regla de la casa. Y al apagar el closeup se restauran los valores automáticos:
        /// este mismo `AxisY` se reusa para el perfil en NM y una escala de 150 ft ahí no pinta nada.
        /// </summary>
        private static void ApplyVerticalAxis(ChartArea area, TouchdownCloseup cu, CloseupVerticalFit fit)
        {
            var y = area.AxisY;

            if (cu == null || !fit.HasData)
            {
                y.Minimum        = cu != null ? VerticalFloorFt : 0.0;
                y.Maximum        = double.NaN;   // automático: lo elige el motor con lo que hay
                y.Interval       = 0.0;
                y.IntervalOffset = 0.0;
                y.LabelStyle.Format = "";
                return;
            }

            y.Minimum           = fit.MinimumFt;
            y.Maximum           = fit.MaximumFt;
            y.Interval          = fit.StepFt;
            // Las marcas caen en los múltiplos del paso contados desde el suelo, que es múltiplo de
            // él: 0 queda siempre en una marca y de ahí sale una rejilla legible en pies enteros.
            y.IntervalOffset    = 0.0;
            y.IntervalType      = DateTimeIntervalType.Number;
            y.IntervalOffsetType = DateTimeIntervalType.Number;
            y.LabelStyle.Format = "0";
            y.IsStartedFromZero = false;
        }

        /// <summary>
        /// La traza fina, ya en el sistema del closeup (`CloseupTrackSource.FlarePoints`): **sin
        /// suavizado**, que es justo lo que se viene a mirar —el muestreo real de 10 Hz—, y con los
        /// mismos 2 px de grosor que la de 2 s para que el cambio de fuente no cambie el trazo.
        /// </summary>
        private static void AddFlareSeries(Chart chart, string name, Color color,
                                           List<(double XFt, double AltFt)> points)
        {
            var series = new Series(name)
            {
                ChartType         = SeriesChartType.Line,
                Color             = color,
                BorderWidth       = 2,
                IsVisibleInLegend = true
            };
            foreach (var p in points) series.Points.AddXY(p.XFt, p.AltFt);
            chart.Series.Add(series);
        }

        /// <summary>
        /// Las cuatro marcas del closeup, con lo que decide <see cref="TouchdownCloseup"/>: las
        /// bandas de la zona de toma (lo que se puntúa), la pista, el umbral y el punto de toque.
        /// Nada se coloca aquí: si el helper dice que el toque no cabe, no se dibuja.
        ///
        /// El suelo y el techo de las líneas verticales son **los del eje** (`CloseupVerticalFit`),
        /// no una segunda cuenta: así el umbral y el toque llegan justo a los bordes del área y no
        /// quedan cortos ni se salen cuando la Y se autoescala.
        /// </summary>
        private void AddCloseupDecorations(Chart chart, ChartArea area, TouchdownCloseup cu,
                                           double yBottom, double yTop)
        {
            // ── Bandas de la zona de toma: los mismos tramos que puntúa `TouchdownZonePolicy` ──
            if (cu.ZeroBandFt > 0.0)
                area.AxisX.StripLines.Add(MakeBand(-cu.ZeroBandFt, cu.ZeroBandFt, "0 pts",
                    Color.FromArgb(42, 70, 160, 90), Color.FromArgb(130, 70, 200, 120)));

            if (cu.ThreeBandFt > cu.ZeroBandFt)
                area.AxisX.StripLines.Add(MakeBand(-cu.ThreeBandFt, cu.ThreeBandFt - cu.ZeroBandFt, "3 pts",
                    Color.FromArgb(34, 190, 150, 40), Color.FromArgb(120, 215, 175, 60)));

            // ── La pista: una línea horizontal a AGL 0 desde el umbral hacia dentro del encuadre ──
            // Sale del encuadre por la derecha cuando el encuadre es más corto que la pista, que es
            // lo correcto: la pista no se recorta ni se dibuja más corta de lo que es.
            if (cu.RunwayVisible)
            {
                var runway = new Series(cu.RunwayLabel)
                {
                    ChartType         = SeriesChartType.Line,
                    Color             = Color.FromArgb(200, 206, 216),
                    BorderWidth       = 8,
                    IsVisibleInLegend = true
                };
                runway.Points.AddXY(-cu.RunwayVisibleFt, 0.0);
                runway.Points.AddXY(0.0, 0.0);
                chart.Series.Add(runway);
            }

            // ── El umbral y el punto de toque ──
            // El «THR» se ancla **sobre el cero del terreno**, un 12 % del eje más arriba: pegado al
            // borde inferior se solapaba con las etiquetas del eje X, y anclado a una fracción del
            // alto (lo que hacía antes) caía **encima de la banda de pista** en cuanto la Y se
            // autoescaló —visto en el PNG: el rótulo salía embutido en la barra blanca—. El «TD» va
            // arriba, y así los dos no se pisan aunque el toque caiga a 500 ft del umbral.
            double groundY = Math.Max(yBottom, Math.Min(yTop, 0.0));
            double thrLabelY = Math.Min(groundY + (yTop - yBottom) * 0.12, yTop);

            AddVerticalMark(chart, "THR", 0.0, yBottom, yTop, thrLabelY,
                            Color.FromArgb(255, 200, 60), "THR");

            if (cu.TouchdownInView)
                AddVerticalMark(chart, "TD", cu.TouchdownX, yBottom, yTop, yTop,
                                Color.FromArgb(255, 80, 80), cu.TouchdownLabel);
        }

        /// <summary>
        /// Alto de las líneas verticales **cuando el eje no se pudo ajustar** (el helper no tenía
        /// muestras dentro del encuadre): un poco por encima del AGL más alto del tramo. Sin ninguna
        /// muestra dentro se usa el alto de la banda de 3 puntos, que siempre existe, para no dibujar
        /// una marca de altura cero.
        /// </summary>
        private double CloseupTopFt(TouchdownCloseup cu)
        {
            double top = 0.0;
            foreach (var pt in _flights[0].Track)
            {
                double x = pt.DistNm * TouchdownCloseupGeometry.FeetPerNm;
                if (x <= cu.BeforeFt && x >= -cu.AfterFt && pt.AglFt > top) top = pt.AglFt;
            }
            if (top <= 0.0) top = cu.ThreeBandFt;
            return top * 1.05;
        }

        private static StripLine MakeBand(double offsetFt, double widthFt, string text,
                                          Color back, Color border)
        {
            return new StripLine
            {
                Interval           = 0,      // una sola banda: no se repite a lo largo del eje
                IntervalOffset     = offsetFt,
                IntervalOffsetType = DateTimeIntervalType.Number,
                StripWidth         = widthFt,
                StripWidthType     = DateTimeIntervalType.Number,
                BackColor          = back,
                BorderColor        = border,
                BorderWidth        = 1,
                Text               = text,
                // El texto se centra **en la banda**, no en su borde, y en **horizontal**: sin la
                // orientación explícita el motor lo gira 90° cuando la banda es estrecha y «0 pts» y
                // «3 pts» salían en vertical, ilegibles —lo destapó el PNG del closeup; la ventana del
                // flare ya lo tenía fijado y aquí faltaba—.
                TextAlignment      = StringAlignment.Center,
                TextLineAlignment  = StringAlignment.Center,
                TextOrientation    = TextOrientation.Horizontal,
                Font               = new Font("Consolas", 8, FontStyle.Bold),
                ForeColor          = Color.FromArgb(210, 225, 240, 250)
            };
        }

        private static void AddVerticalMark(Chart chart, string name, double x,
                                            double yBottom, double yTop, double labelY,
                                            Color color, string label)
        {
            var s = new Series(name)
            {
                ChartType         = SeriesChartType.Line,
                Color             = color,
                BorderWidth       = 1,
                BorderDashStyle   = ChartDashStyle.Dash,
                IsVisibleInLegend = false
            };
            // El rótulo se ancla en el punto intermedio, no en los extremos: así cada marca decide a
            // qué altura se lee su texto sin dejar de ser una sola línea vertical.
            s.SmartLabelStyle.Enabled = false;
            s.Points.AddXY(x, yBottom);
            s.Points.AddXY(x, labelY);
            s.Points.AddXY(x, yTop);

            var anchor = s.Points[1];
            anchor.Label          = label;
            anchor.LabelForeColor = color;
            anchor.Font           = new Font("Consolas", 8, FontStyle.Bold);

            chart.Series.Add(s);
        }

        private static void AddRefSeries(Chart chart, string name, Color color,
            ChartDashStyle dash, (double x, double y)[] points, bool visible)
        {
            var s = new Series(name)
            {
                ChartType         = SeriesChartType.Line,
                Color             = color,
                BorderWidth       = 1,
                BorderDashStyle   = dash,
                IsVisibleInLegend = visible
            };
            chart.Series.Add(s);
            foreach (var (x, y) in points)
                s.Points.AddXY(x, y);
        }

        private static void AddTrackSeries(Chart chart, string name, Color color,
            List<ApproachTrackPoint> track,
            Func<ApproachTrackPoint, double> selector, bool smooth, double xFactor = 1.0)
        {
            var series = new Series(name)
            {
                ChartType         = SeriesChartType.Line,
                Color             = color,
                BorderWidth       = 2,
                IsVisibleInLegend = true
            };
            chart.Series.Add(series);

            double[] values;
            if (smooth)
            {
                values = SmoothGaussian(track, selector, window: 7);
            }
            else
            {
                values = new double[track.Count];
                for (int j = 0; j < track.Count; j++)
                    values[j] = selector(track[j]);
            }

            for (int i = 0; i < track.Count; i++)
                series.Points.AddXY(track[i].DistNm * xFactor, values[i]);
        }

        private double ComputeOverallVref()
        {
            double sum = 0; int count = 0;
            foreach (var (_, track) in _flights)
                foreach (var pt in track) { sum += pt.IasKt; count++; }
            return count > 0 ? sum / count : 140.0;
        }

        // ── Static helpers ────────────────────────────────────────────────────────

        private static Panel StatCell(string label, string value)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            pnl.Controls.Add(new Label
            {
                Text      = label,
                Font      = new Font("Consolas", 8),
                ForeColor = Color.FromArgb(120, 160, 200),
                Location  = new Point(4, 8),
                AutoSize  = true
            });
            pnl.Controls.Add(new Label
            {
                Text      = value,
                Font      = new Font("Consolas", 13, FontStyle.Bold),
                ForeColor = Color.White,
                Location  = new Point(4, 24),
                AutoSize  = true
            });
            return pnl;
        }

        private static void StyleAxis(Axis axis, string title, Color labelColor)
        {
            axis.Title                   = title;
            axis.TitleFont               = new Font("Consolas", 8);
            axis.TitleForeColor          = labelColor;
            axis.LabelStyle.Font         = new Font("Consolas", 8);
            axis.LabelStyle.ForeColor    = labelColor;
            axis.MajorTickMark.LineColor = labelColor;
        }

        private static double[] SmoothGaussian(
            IList<ApproachTrackPoint> pts,
            Func<ApproachTrackPoint, double> selector,
            int window = 7)
        {
            int    n     = pts.Count;
            int    half  = window / 2;
            double sigma = window / 4.0;

            double[] w = new double[window];
            for (int k = 0; k < window; k++)
            {
                double x = k - half;
                w[k] = Math.Exp(-(x * x) / (2.0 * sigma * sigma));
            }

            double[] result = new double[n];
            for (int i = 0; i < n; i++)
            {
                double sum = 0, wSum = 0;
                for (int k = 0; k < window; k++)
                {
                    int idx = i - half + k;
                    if (idx < 0 || idx >= n) continue;
                    sum  += w[k] * selector(pts[idx]);
                    wSum += w[k];
                }
                result[i] = wSum > 0 ? sum / wSum : selector(pts[i]);
            }
            return result;
        }

        private static IEnumerable<Control> FindChartControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is Chart) yield return c;
                foreach (Control inner in FindChartControls(c))
                    yield return inner;
            }
        }
    }
}
