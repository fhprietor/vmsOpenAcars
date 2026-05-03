using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.UI.Forms
{
    public class LandingAnalysisForm : Form
    {
        private readonly IList<(FlightRecord Record, List<ApproachTrackPoint> Track)> _flights;

        private static readonly Color[] TrackColors =
        {
            Color.FromArgb( 30, 144, 255),   // DodgerBlue
            Color.FromArgb(255, 140,   0),   // DarkOrange
            Color.FromArgb( 50, 205,  50),   // LimeGreen
            Color.FromArgb(186,  85, 211),   // MediumOrchid
            Color.FromArgb(255, 215,   0),   // Gold
        };

        private bool IsComparison => _flights.Count > 1;

        public LandingAnalysisForm(IList<(FlightRecord Record, List<ApproachTrackPoint> Track)> flights)
        {
            _flights = flights;
            BuildUI();
            PopulateCharts();
        }

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
            }

            // 2×2 chart grid
            var chartLayout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 2,
                BackColor   = Color.Transparent
            };
            chartLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            chartLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            chartLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            chartLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            chartLayout.Controls.Add(MakeChart("Vertical Profile (AGL)",  "Distance to threshold (NM)", "AGL (ft)",       "VERTICAL"), 0, 0);
            chartLayout.Controls.Add(MakeChart("Lateral Deviation",        "Distance to threshold (NM)", "Deviation (ft)", "LATERAL"),  1, 0);
            chartLayout.Controls.Add(MakeChart("Indicated Airspeed",       "Distance to threshold (NM)", "IAS (kt)",       "IAS"),      0, 1);
            chartLayout.Controls.Add(MakeChart("Vertical Speed",           "Distance to threshold (NM)", "VS (fpm)",       "VS"),       1, 1);

            Controls.Add(chartLayout);
            if (pnlMetar != null) Controls.Add(pnlMetar);
            Controls.Add(pnlHeader);
            Controls.Add(pnlTitle);
        }

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
                Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1, BackColor = Color.Transparent
            };
            for (int i = 0; i < 7; i++)
                stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
            stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            stats.Controls.Add(StatCell("DATE",   rec.DisplayDate),              0, 0);
            stats.Controls.Add(StatCell("VS",     $"{rec.LandingRateFpm} fpm"),  1, 0);
            stats.Controls.Add(StatCell("G",      $"{rec.GForce:F2}g"),          2, 0);
            stats.Controls.Add(StatCell("DIST",   $"{rec.TouchdownDistFt:F0} ft"), 3, 0);
            stats.Controls.Add(StatCell("CL DEV", $"{rec.CenterlineDevFt:F0} ft"), 4, 0);

            var scoreCell = StatCell("SCORE", rec.DisplayScore);
            // Controls[1] is the value label (Controls[0] is the caption)
            if (scoreCell.Controls.Count > 1 && scoreCell.Controls[1] is Label sv)
                sv.ForeColor = scoreClr;
            stats.Controls.Add(scoreCell, 5, 0);
            stats.Controls.Add(StatCell("RUNWAY", rec.RunwayName), 6, 0);

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
                              $"RWY {rec.RunwayName,-4}  {rec.LandingRateFpm,5} fpm  " +
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

            // Global max distance across all flights
            double maxDist = 0;
            foreach (var (_, track) in _flights)
                foreach (var pt in track)
                    if (pt.DistNm > maxDist) maxDist = pt.DistNm;
            maxDist = Math.Ceiling(maxDist * 10) / 10.0;
            if (maxDist <= 0) return;

            // Overall Vref (average IAS across all tracks)
            double vref = ComputeOverallVref();

            // Collect charts by tag
            var charts = new Dictionary<string, Chart>();
            foreach (Control c in FindChartControls(this))
                if (c is Chart ch && ch.Tag is string t)
                    charts[t] = ch;

            // X axis bounds
            foreach (var ch in charts.Values)
                ch.ChartAreas["main"].AxisX.Maximum = maxDist;

            // Shared reference lines (added first so they appear behind track data)
            AddRefSeries(charts["VERTICAL"], "3° ref",
                Color.FromArgb(0, 200, 100), ChartDashStyle.Dash,
                new[] { (maxDist, maxDist * 319.0), (0.001, 0.0) }, visible: true);

            AddRefSeries(charts["LATERAL"], "CL",
                Color.FromArgb(80, 130, 80), ChartDashStyle.Dot,
                new[] { (maxDist, 0.0), (0.001, 0.0) }, visible: false);

            AddRefSeries(charts["IAS"], $"Avg {vref:F0} kt",
                Color.FromArgb(255, 180, 0), ChartDashStyle.Dash,
                new[] { (maxDist, vref), (0.001, vref) }, visible: true);

            AddRefSeries(charts["VS"], "zero",
                Color.FromArgb(80, 130, 80), ChartDashStyle.Dot,
                new[] { (maxDist, 0.0), (0.001, 0.0) }, visible: false);

            // One series per flight per chart
            for (int i = 0; i < _flights.Count; i++)
            {
                var (rec, track) = _flights[i];
                Color color = TrackColors[i % TrackColors.Length];
                string name = IsComparison ? $"{rec.FlightNumber} #{i + 1}" : "Actual";

                AddTrackSeries(charts["VERTICAL"], name, color, track, pt => pt.AglFt,     smooth: false);
                AddTrackSeries(charts["LATERAL"],  name, color, track, pt => pt.LateralFt, smooth: true);
                AddTrackSeries(charts["IAS"],      name, color, track, pt => pt.IasKt,     smooth: true);
                AddTrackSeries(charts["VS"],       name, color, track, pt => pt.VsFpm,     smooth: true);
            }

            // Y axis post-tuning
            charts["VERTICAL"].ChartAreas["main"].AxisY.Minimum = 0;
            charts["LATERAL"].ChartAreas["main"].AxisY.Title    = "Dev (ft)  + right  – left";
            charts["IAS"].ChartAreas["main"].AxisY.Minimum      = Math.Floor((vref - 20) / 10) * 10;
            charts["IAS"].ChartAreas["main"].AxisY.Maximum      = Math.Ceiling((vref + 20) / 10) * 10;
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
            Func<ApproachTrackPoint, double> selector, bool smooth)
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
                series.Points.AddXY(track[i].DistNm, values[i]);
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
