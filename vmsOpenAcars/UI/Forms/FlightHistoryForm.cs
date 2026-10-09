using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.Services.Interfaces;

namespace vmsOpenAcars.UI.Forms
{
    public class FlightHistoryForm : Form
    {
        private readonly ILandingLogService _svc;

        private DataGridView _grid;
        private Button _btnAnalyse;
        private Button _btnCompare;
        private Button _btnDelete;
        private Button _btnClose;

        public FlightRecord SelectedFlight { get; private set; }

        public FlightHistoryForm(ILandingLogService svc)
        {
            _svc = svc;
            BuildUI();
            Reload();
        }

        private void BuildUI()
        {
            Text            = "Landing Log — Flight History";
            Size            = new Size(820, 480);
            MinimumSize     = new Size(640, 360);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            BackColor       = Color.FromArgb(20, 30, 40);
            Padding         = new Padding(2);

            // Outer border
            Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(100, 180, 255), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0,
                        ClientSize.Width - 1, ClientSize.Height - 1);
            };

            // Title bar
            var pnlTitle = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 35,
                BackColor = Color.FromArgb(30, 40, 50)
            };
            var lblTitle = new Label
            {
                Text      = "LANDING LOG",
                Font      = new Font("Consolas", 12, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location  = new Point(10, 8),
                AutoSize  = true
            };
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
            pnlTitle.Controls.Add(lblTitle);
            pnlTitle.Controls.Add(btnX);
            Resize += (s, e) => btnX.Location = new Point(ClientSize.Width - 35, 5);

            // Drag
            bool dragging = false;
            Point dragStart = Point.Empty;
            pnlTitle.MouseDown += (s, e) =>
            { if (e.Button == MouseButtons.Left) { dragging = true; dragStart = new Point(e.X, e.Y); } };
            pnlTitle.MouseMove += (s, e) =>
            { if (dragging) { var p = PointToScreen(e.Location); Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y); } };
            pnlTitle.MouseUp   += (s, e) => dragging = false;

            // Grid
            _grid = new DataGridView
            {
                Dock                 = DockStyle.Fill,
                ReadOnly             = true,
                AllowUserToAddRows   = false,
                AllowUserToResizeRows= false,
                SelectionMode        = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect          = true,
                BackgroundColor      = Color.FromArgb(25, 35, 45),
                ForeColor            = Color.White,
                GridColor            = Color.FromArgb(50, 60, 70),
                BorderStyle          = BorderStyle.None,
                RowHeadersVisible    = false,
                Font                 = new Font("Consolas", 10),
                AutoSizeColumnsMode  = DataGridViewAutoSizeColumnsMode.Fill
            };
            _grid.DefaultCellStyle.BackColor      = Color.FromArgb(25, 35, 45);
            _grid.DefaultCellStyle.ForeColor      = Color.White;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 90, 160);
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 50, 70);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Cyan;
            _grid.ColumnHeadersDefaultCellStyle.Font      = new Font("Consolas", 10, FontStyle.Bold);
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            _grid.EnableHeadersVisualStyles = false;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",   HeaderText = "Date",    FillWeight = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Flight",  HeaderText = "Flight",  FillWeight = 90  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Route",   HeaderText = "Route",   FillWeight = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Runway",  HeaderText = "RWY",     FillWeight = 60  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate",    HeaderText = "VS (fpm)",FillWeight = 90  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "GForce",  HeaderText = "G",       FillWeight = 60  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Score",   HeaderText = "Score",   FillWeight = 70  });
            // Viento del aterrizaje: dirección/intensidad (y racha si el METAR la trae) del momento
            // del contacto. Las componentes en cara/cruzada y el METAR en crudo están en el detalle
            // (VIEW ANALYSIS), pero aquí se ve de un vistazo en qué condiciones se tomó cada pista.
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wind",    HeaderText = "Wind",    FillWeight = 90  });
            // El tiempo umbral→toma del vuelo: sale de la traza **fina** del flare, así que un vuelo
            // anterior a esa traza —o con la captura perdida— se queda en «—». Nunca se calcula sobre
            // la traza de 2 s (`ThresholdToTouchdown.Compute` lo señala y no devuelve número).
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ThrToTd", HeaderText = L._("Landing_ThrToTdHeader"), FillWeight = 110 });
            // Los flaps con los que se tomó: el mismo dato que ya publica la ventana del flare, aquí
            // para poder comparar vuelos de un vistazo. Sale de la traza fina y de la **familia del
            // avión** guardada con el vuelo: sin ella, o en un avión cuya escala no está en el marcado
            // del cliente, la columna enseña el **porcentaje** y ninguna compuerta inventada.
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Flaps",    HeaderText = L._("Landing_FlapsHeader"), FillWeight = 100 });
            // Y dónde se cortó la potencia, en segundos antes de la toma. Mismo criterio que la
            // anterior: `—` cuando la traza no da para publicarlo (sin N1, traza de 2 s, sin caída).
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PowerCut", HeaderText = L._("Landing_PowerCutHeader"), FillWeight = 110 });
            _grid.Columns["Rate"].DefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["Score"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["Wind"].DefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["ThrToTd"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["Flaps"].DefaultCellStyle.Alignment    = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["PowerCut"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.CellDoubleClick    += (s, e) => OpenAnalysis();
            _grid.SelectionChanged   += (s, e) => UpdateButtonStates();

            // Bottom bar
            var pnlBottom = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 44,
                BackColor = Color.FromArgb(30, 40, 50),
                Padding   = new Padding(6, 6, 6, 6)
            };

            _btnAnalyse = MakeBtn("VIEW ANALYSIS", Color.FromArgb(0, 100, 200));
            _btnAnalyse.Click   += (s, e) => OpenAnalysis();
            _btnAnalyse.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _btnAnalyse.Enabled  = false;

            _btnCompare = MakeBtn("COMPARE", Color.FromArgb(0, 120, 80));
            _btnCompare.Click   += BtnCompare_Click;
            _btnCompare.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _btnCompare.Enabled  = false;

            _btnDelete = MakeBtn("DELETE", Color.FromArgb(140, 30, 30));
            _btnDelete.Click   += BtnDelete_Click;
            _btnDelete.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _btnDelete.Enabled  = false;

            _btnClose = MakeBtn("CLOSE", Color.FromArgb(100, 0, 0));
            _btnClose.Click += (s, e) => Close();
            _btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            pnlBottom.Controls.Add(_btnDelete);
            pnlBottom.Controls.Add(_btnCompare);
            pnlBottom.Controls.Add(_btnAnalyse);
            pnlBottom.Controls.Add(_btnClose);
            pnlBottom.Resize += (s, e) =>
            {
                int right = pnlBottom.Width - 6;
                _btnClose.Location   = new Point(right - _btnClose.Width, 6);
                right -= _btnClose.Width + 6;
                _btnAnalyse.Location = new Point(right - _btnAnalyse.Width, 6);
                right -= _btnAnalyse.Width + 6;
                _btnCompare.Location = new Point(right - _btnCompare.Width, 6);
                right -= _btnCompare.Width + 6;
                _btnDelete.Location  = new Point(right - _btnDelete.Width, 6);
            };

            Controls.Add(_grid);
            Controls.Add(pnlBottom);
            Controls.Add(pnlTitle);
        }

        private Button MakeBtn(string text, Color back)
        {
            var btn = new Button
            {
                Text      = text,
                Size      = new Size(140, 30),
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 9, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void Reload()
        {
            _grid.Rows.Clear();
            var flights = _svc.GetFlights();
            foreach (var f in flights)
            {
                // La traza del flare se lee **una vez por vuelo** y la comparten las tres columnas que
                // salen de ella: el tiempo umbral→toma, los flaps y el corte de potencia. Leerla tres
                // veces sería triplicar la consulta para el mismo dato.
                var flare = FlareTrack(f);

                int row = _grid.Rows.Add(
                    f.DisplayDate,
                    f.FlightNumber,
                    f.DisplayRoute,
                    f.RunwayName,
                    f.DisplayLandingRate,
                    $"{f.GForce:F2}g",
                    f.DisplayScore,
                    // El texto lo arma el helper puro para que la columna diga lo mismo que el
                    // detalle: `320/12G20`, `CALM`… y `—` cuando no se capturó viento.
                    WindComponents.FormatRaw(f.WindAtLanding),
                    // El tiempo umbral→toma, del mismo helper puro que lo publica en vuelo y en el
                    // PIREP: `THR-TD 6.8 s`, o `—` si este vuelo no tiene traza fina del flare.
                    ThresholdToTouchdownText(flare),
                    // Los flaps en el umbral, con la familia del avión que voló el vuelo.
                    FlapsText(flare, f),
                    // Y el corte de potencia, con el mismo helper que lo publica en el PIREP.
                    PowerCutText(flare));
                _grid.Rows[row].Tag = f;

                // Colour score
                Color scoreColor = f.Score >= 90 ? Color.LightGreen
                                 : f.Score >= 75 ? Color.Yellow
                                 : Color.OrangeRed;
                _grid.Rows[row].Cells["Score"].Style.ForeColor = scoreColor;
            }
            UpdateButtonStates();
        }

        /// <summary>
        /// La traza fina del flare de un vuelo, o una lista vacía si la base falla: una lectura rota no
        /// puede tumbar el historial, y las tres columnas que la usan degradan a `—`.
        /// </summary>
        private List<FlareTrackPoint> FlareTrack(FlightRecord f)
        {
            try { return _svc.GetFlareTrack(f.Id); }
            catch { return new List<FlareTrackPoint>(); }
        }

        /// <summary>
        /// **El tiempo umbral→toma de un vuelo del historial**, o `—` cuando no hay dato.
        ///
        /// Sale de la traza **fina** del flare (`flare_track`) de ese vuelo y del helper puro
        /// `ThresholdToTouchdown`, el mismo que lo publica en vuelo y en el PIREP. Un vuelo anterior a
        /// la traza, o con la captura armada pero sin muestras, se queda en `—`: el helper devuelve el
        /// motivo, nunca un cero.
        /// </summary>
        private static string ThresholdToTouchdownText(List<FlareTrackPoint> flare)
        {
            var result = ThresholdToTouchdown.Compute(flare);
            return result.HasValue ? L._("Landing_ThrToTd", result.Seconds) : "—";
        }

        /// <summary>
        /// **Los flaps con los que se tomó el vuelo**, o `—`. El ajuste es el del **cruce del umbral**
        /// —el que el piloto tenía puesto al llegar—, y se traduce a la compuerta del avión con la
        /// **familia que se guardó con el vuelo** (`FlightRecord.AircraftIcao`, el modelo ATC del
        /// simulador). Sin familia —vuelos anteriores a la columna, o un simulador que no la publicaba—
        /// se enseña el **porcentaje**: `FlapSetting` no le supone a un avión un marcado que no conoce.
        /// </summary>
        private static string FlapsText(List<FlareTrackPoint> flare, FlightRecord f)
        {
            var flaps = FlapTrackSummary.Compute(flare, f.AircraftIcao);
            return flaps.HasTrack && flaps.AtThreshold != null ? flaps.AtThreshold.Text : "—";
        }

        /// <summary>
        /// **Dónde se cortó la potencia**, en segundos antes de la toma (`4.1 s`), o `—` cuando no hay
        /// dato: sin N1 en la traza, con la traza de 2 s o si la potencia nunca cayó de forma sostenida,
        /// `PowerCut` devuelve el motivo en vez de un número.
        /// </summary>
        private static string PowerCutText(List<FlareTrackPoint> flare)
        {
            var cut = PowerCut.Compute(flare);
            return cut.HasValue ? PowerCut.FormatSeconds(cut.SecondsBeforeTouchdown) : "—";
        }

        private void UpdateButtonStates()
        {
            int n = _grid.SelectedRows.Count;
            _btnAnalyse.Enabled = n == 1;
            _btnCompare.Enabled = n >= 2 && n <= 5;
            _btnDelete.Enabled  = n >= 1;
        }

        private void OpenAnalysis()
        {
            if (_grid.SelectedRows.Count != 1) return;
            var record = _grid.SelectedRows[0].Tag as FlightRecord;
            if (record == null) return;

            var track   = _svc.GetTrackPoints(record.Id);
            var flights = new List<(FlightRecord Record, List<ApproachTrackPoint> Track)>
            {
                (record, track)
            };
            // El cargador de la traza fina del flare se inyecta aquí: el formulario de análisis no
            // conoce `ILandingLogService`, y el botón FLARE solo aparece cuando hay de dónde leer.
            // Un vuelo anterior a `flare_track` abre la ventana del flare y en ella se dice que no
            // hay datos, sin rellenarla con la traza de 2 s.
            //
            // El segundo delegado **no** es «hay filas»: es «la captura se armó», que viaja con el
            // vuelo (`flights.flare_capture_armed`, leído del propio registro). Con cero filas, es lo
            // único que distingue «esta captura se perdió» de «este vuelo es anterior a la traza», y
            // con el recuento de filas la ventana acusaba al vuelo de lo primero.
            new LandingAnalysisForm(flights,
                id => _svc.GetFlareTrack(id),
                id => record.FlareCaptureArmed).Show(this);
        }

        private void BtnCompare_Click(object sender, EventArgs e)
        {
            var selected = _grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.Tag as FlightRecord)
                .Where(f => f != null)
                .OrderBy(f => f.FlightDate)
                .ToList();

            if (selected.Count < 2) return;

            var flights = new List<(FlightRecord Record, List<ApproachTrackPoint> Track)>();
            foreach (var rec in selected)
                flights.Add((rec, _svc.GetTrackPoints(rec.Id)));

            new LandingAnalysisForm(flights).Show(this);
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            int n = _grid.SelectedRows.Count;
            if (n == 0) return;

            string msg = n == 1
                ? "Delete this flight record?"
                : $"Delete {n} flight records?";

            if (EcamDialog.Show(this, msg, "CONFIRM DELETE", EcamDialogButtons.YesNo) != DialogResult.Yes)
                return;

            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                var rec = row.Tag as FlightRecord;
                if (rec != null)
                    _svc.DeleteFlight(rec.Id);
            }
            Reload();
        }
    }
}
