using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI.Forms
{
    public class FlightHistoryForm : Form
    {
        private readonly LandingLogService _svc;

        private DataGridView _grid;
        private Button _btnAnalyse;
        private Button _btnCompare;
        private Button _btnDelete;
#if DEBUG
        private Button _btnSeed;
#endif
        private Button _btnClose;

        public FlightRecord SelectedFlight { get; private set; }

        public FlightHistoryForm(LandingLogService svc)
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
            _grid.Columns["Rate"].DefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["Score"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
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

#if DEBUG
            _btnSeed = MakeBtn("SEED DEMO DATA", Color.FromArgb(60, 80, 40));
            _btnSeed.Click  += BtnSeed_Click;
            _btnSeed.Anchor  = AnchorStyles.Top | AnchorStyles.Left;
#endif

            _btnClose = MakeBtn("CLOSE", Color.FromArgb(100, 0, 0));
            _btnClose.Click += (s, e) => Close();
            _btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;

#if DEBUG
            pnlBottom.Controls.Add(_btnSeed);
#endif
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
#if DEBUG
                _btnSeed.Location    = new Point(6, 6);
#endif
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
                int row = _grid.Rows.Add(
                    f.DisplayDate,
                    f.FlightNumber,
                    f.DisplayRoute,
                    f.RunwayName,
                    f.LandingRateFpm.ToString(),
                    $"{f.GForce:F2}g",
                    f.DisplayScore);
                _grid.Rows[row].Tag = f;

                // Colour score
                Color scoreColor = f.Score >= 90 ? Color.LightGreen
                                 : f.Score >= 75 ? Color.Yellow
                                 : Color.OrangeRed;
                _grid.Rows[row].Cells["Score"].Style.ForeColor = scoreColor;
            }
            UpdateButtonStates();
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
            new LandingAnalysisForm(flights).Show(this);
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

#if DEBUG
        private void BtnSeed_Click(object sender, EventArgs e)
        {
            _svc.SeedMockData();
            Reload();
        }
#endif
    }
}
