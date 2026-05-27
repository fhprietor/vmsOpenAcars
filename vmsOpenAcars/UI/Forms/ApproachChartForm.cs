using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Models.NavData;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI.Forms
{
    internal sealed class ApproachChartForm : Form
    {
        // ── UI controls ──────────────────────────────────────────────────────────────
        private Panel          _toolbar;
        private Label          _lblApproach;
        private SplitContainer _split;
        private Label          _lblLoading;
        private ComboBox       _cmbApproach;
        private Panel          _pnlHeader;
        private Panel          _pnlPlan;
        private Panel          _pnlProfile;
        private Panel          _pnlLoading;
        private Label          _lblHeader;

        // ── DPI scaling ──────────────────────────────────────────────────────────────
        private float _scale = 1f;
        private float S(float px) => px * _scale;

        // ── Data ─────────────────────────────────────────────────────────────────────
        private readonly string         _icao;
        private List<NavApproach>       _approaches  = new List<NavApproach>();
        private List<NavIls>            _ilsList     = new List<NavIls>();
        private List<NavRunway>         _runways     = new List<NavRunway>();
        private NavAirportInfo          _airportInfo;
        private NavApproach             _selected;
        private readonly NavApproach    _preselected;

        // ── Colors ───────────────────────────────────────────────────────────────────
        private static readonly Color BgColor     = Color.FromArgb(15, 22, 32);
        private static readonly Color LegColor    = Color.FromArgb(220, 220, 220);
        private static readonly Color MissedColor = Color.FromArgb(0, 200, 255);
        private static readonly Color FafColor    = Color.FromArgb(0, 210, 100);
        private static readonly Color GsColor     = Color.FromArgb(255, 160, 40);
        private static readonly Color VnavColor   = Color.FromArgb(80, 220, 80);
        private static readonly Color DaColor     = Color.FromArgb(255, 80, 80);
        private static readonly Color LabelColor  = Color.FromArgb(255, 220, 80);
        private static readonly Color AxisColor   = Color.FromArgb(80, 100, 120);
        private static readonly Color RwyColor    = Color.FromArgb(180, 180, 180);

        // ── Constructor ──────────────────────────────────────────────────────────────

        internal ApproachChartForm(string icao, NavApproach preselected)
        {
            _icao        = icao;
            _preselected = preselected;
            InitLayout();
            this.Shown += (s, e) => { UpdateScale(DeviceDpi); _ = LoadDataAsync(); };
        }

        // ── Layout ───────────────────────────────────────────────────────────────────

        private void InitLayout()
        {
            Text            = $"Approach Chart — {_icao}";
            Size            = new Size(960, 720);
            MinimumSize     = new Size(720, 560);
            BackColor       = BgColor;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;

            // Toolbar
            _toolbar     = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(20, 30, 42) };
            _lblApproach = new Label { Text = "Approach:", ForeColor = Color.FromArgb(140, 160, 180),
                Font = new Font("Consolas", 8), AutoSize = true, Top = 9, Left = 8 };
            _cmbApproach = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(25, 35, 48),
                ForeColor = Color.White, Font = new Font("Consolas", 8),
                Top = 6, Left = 80, Width = 340,
            };
            _cmbApproach.SelectedIndexChanged += (s, e) => OnApproachSelected();
            _toolbar.Controls.Add(_lblApproach);
            _toolbar.Controls.Add(_cmbApproach);

            // Header
            _pnlHeader = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.FromArgb(18, 26, 38) };
            _lblHeader = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Color.FromArgb(200, 220, 240),
                Font = new Font("Consolas", 7.5f), TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
            };
            _pnlHeader.Controls.Add(_lblHeader);

            // Split: plan (top) + profile (bottom)
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                BackColor = BgColor, BorderStyle = BorderStyle.None,
                SplitterDistance = 400, Panel1MinSize = 200, Panel2MinSize = 120,
            };

            _pnlPlan = new Panel { Dock = DockStyle.Fill, BackColor = BgColor };
            _pnlPlan.Paint += PaintPlanView;

            _pnlProfile = new Panel { Dock = DockStyle.Fill, BackColor = BgColor };
            _pnlProfile.Paint += PaintProfileView;

            _split.Panel1.Controls.Add(_pnlPlan);
            _split.Panel2.Controls.Add(_pnlProfile);

            // Loading overlay
            _pnlLoading = new Panel { Dock = DockStyle.Fill, BackColor = BgColor, Visible = true };
            _lblLoading  = new Label
            {
                Text = "Loading NavData…", ForeColor = Color.FromArgb(100, 140, 180),
                Font = new Font("Consolas", 11), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            };
            _pnlLoading.Controls.Add(_lblLoading);

            Controls.Add(_pnlLoading);
            Controls.Add(_split);
            Controls.Add(_pnlHeader);
            Controls.Add(_toolbar);

            SizeChanged += (s, e) => { _pnlPlan.Invalidate(); _pnlProfile.Invalidate(); };
        }

        // ── DPI handling ─────────────────────────────────────────────────────────────

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            UpdateScale(e.DeviceDpiNew);
        }

        private void UpdateScale(int dpi)
        {
            _scale = dpi / 96f;
            RelayoutControls();
            _pnlPlan.Invalidate();
            _pnlProfile.Invalidate();
        }

        private void RelayoutControls()
        {
            _toolbar.Height    = (int)S(36);
            _lblApproach.Top   = (int)S(9);
            _lblApproach.Left  = (int)S(8);
            _lblApproach.Font  = new Font("Consolas", S(8));
            _cmbApproach.Top   = (int)S(6);
            _cmbApproach.Left  = (int)S(80);
            _cmbApproach.Width = (int)S(340);
            _cmbApproach.Font  = new Font("Consolas", S(8));

            _pnlHeader.Height = (int)S(54);

            var oldHdr = _lblHeader.Font;
            _lblHeader.Font   = new Font("Consolas", S(7.5f));
            oldHdr?.Dispose();

            var oldLoad = _lblLoading.Font;
            _lblLoading.Font  = new Font("Consolas", S(11));
            oldLoad?.Dispose();

            int min1 = (int)S(200), min2 = (int)S(120);
            _split.Panel1MinSize = min1;
            _split.Panel2MinSize = min2;
            int available = _split.Height - _split.SplitterWidth;
            if (available > min1 + min2)
                _split.SplitterDistance = Math.Max(min1, Math.Min((int)S(400), available - min2));
        }

        // ── Data loading ─────────────────────────────────────────────────────────────

        private async Task LoadDataAsync()
        {
            await Task.Run(() => NavDataClient.PrefetchAirport(_icao));

            _approaches  = NavDataClient.GetApproaches(_icao) ?? new List<NavApproach>();
            _ilsList     = NavDataClient.GetIls(_icao)        ?? new List<NavIls>();
            _runways     = NavDataClient.GetRunways(_icao)    ?? new List<NavRunway>();
            _airportInfo = NavDataClient.GetAirportInfo(_icao);

            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(PopulateCombo));
        }

        private void PopulateCombo()
        {
            _cmbApproach.Items.Clear();
            foreach (var a in _approaches.OrderBy(a => a.Runway).ThenBy(a => a.Type))
                _cmbApproach.Items.Add(new ApproachItem(a));

            // Pre-select
            NavApproach target = _preselected;
            if (target == null && _approaches.Count > 0) target = _approaches[0];

            if (target != null)
            {
                string key = ApproachKey(target);
                for (int i = 0; i < _cmbApproach.Items.Count; i++)
                    if (((ApproachItem)_cmbApproach.Items[i]).Key == key)
                    { _cmbApproach.SelectedIndex = i; break; }
                if (_cmbApproach.SelectedIndex < 0 && _cmbApproach.Items.Count > 0)
                    _cmbApproach.SelectedIndex = 0;
            }

            _pnlLoading.Visible = false;
            OnApproachSelected();
        }

        private void OnApproachSelected()
        {
            _selected = (_cmbApproach.SelectedItem as ApproachItem)?.Approach;
            UpdateHeader();
            _pnlPlan.Invalidate();
            _pnlProfile.Invalidate();
        }

        private void UpdateHeader()
        {
            if (_selected == null) { _lblHeader.Text = "—"; return; }
            var ils  = GetIls(_selected);
            var info = _airportInfo;

            var parts = new List<string>();
            parts.Add(_selected.DisplayName.ToUpperInvariant());
            if (info != null)
                parts.Add($"{_icao}  {info.Name}");
            if (ils != null)
                parts.Add($"ILS {ils.FrequencyMhz:F2} MHz");
            else if (_selected.Navaid != null)
            {
                var n = _selected.Navaid;
                if (n.FrequencyMhz.HasValue)
                    parts.Add($"{n.Type} {n.Ident}  {n.FrequencyMhz:F1} MHz");
                else if (n.FrequencyKhz.HasValue)
                    parts.Add($"{n.Type} {n.Ident}  {n.FrequencyKhz:F0} kHz");
            }
            string line1 = string.Join("  ·  ", parts);

            var parts2 = new List<string>();
            if (info != null)
            {
                parts2.Add($"Elev {info.ElevationFt:F0} ft");
                if (info.TransitionAltitudeFt.HasValue)
                    parts2.Add($"TA {info.TransitionAltitudeFt:F0} ft");
                if (info.TransitionLevelFt.HasValue)
                    parts2.Add($"TL FL{info.TransitionLevelFt / 100:F0}");
            }
            var rwy = GetRunway(_selected);
            if (rwy != null) parts2.Add($"RWY {rwy.Name}  Elev {rwy.ElevationFt:F0} ft");
            parts2.Add($"AIRAC {NavDataClient.AiracCycle ?? "—"}");
            string line2 = string.Join("  ·  ", parts2);

            _lblHeader.Text = line1 + "\n" + line2;
        }

        // ── Plan view ────────────────────────────────────────────────────────────────

        private void PaintPlanView(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BgColor);

            if (_selected == null) return;

            var app = _selected;
            var rwy = GetRunway(app);
            var ils = GetIls(app);

            // Collect all geo points
            var allLegs    = app.Legs.Concat(app.MissedLegs).ToList();
            var geoPoints  = allLegs.Where(l => l.Lat.HasValue && l.Lon.HasValue)
                                    .Select(l => new PointF((float)l.Lon.Value, (float)l.Lat.Value))
                                    .ToList();
            if (rwy != null)
            {
                geoPoints.Add(new PointF((float)rwy.ThresholdLon, (float)rwy.ThresholdLat));
                geoPoints.Add(new PointF((float)rwy.EndLon, (float)rwy.EndLat));
            }
            if (geoPoints.Count < 2) return;

            // 5nm extension before threshold for centerline
            if (rwy != null)
            {
                double crsRad = TrueRunwayBearing(rwy) * Math.PI / 180.0;
                double oppRad = crsRad + Math.PI;
                double d5     = 5.0 / 60.0;
                geoPoints.Add(new PointF(
                    (float)(rwy.ThresholdLon + d5 / Math.Cos(rwy.ThresholdLat * Math.PI / 180) * Math.Sin(oppRad)),
                    (float)(rwy.ThresholdLat + d5 * Math.Cos(oppRad))));
            }

            var bounds = ComputeBounds(geoPoints, 0.12f);
            var w = _pnlPlan.ClientSize.Width;
            var h = _pnlPlan.ClientSize.Height;

            Func<double, double, PointF> toScreen = (lat, lon) =>
            {
                float sx = (float)((lon - bounds.Left) / bounds.Width  * w);
                float sy = (float)((bounds.Bottom - lat) / bounds.Height * h);
                return new PointF(sx, sy);
            };

            // Extended centerline
            if (rwy != null)
            {
                double crsRad = TrueRunwayBearing(rwy) * Math.PI / 180.0;
                double oppRad = crsRad + Math.PI;
                double cosLat = Math.Cos(rwy.ThresholdLat * Math.PI / 180);
                var thr = new PointF((float)rwy.ThresholdLon, (float)rwy.ThresholdLat);
                double d5 = 5.0 / 60.0, dp = 0.5 / 60.0;
                var extFar   = new PointF(
                    (float)(thr.X + d5 / cosLat * Math.Sin(oppRad)),
                    (float)(thr.Y + d5 * Math.Cos(oppRad)));
                var extPast  = new PointF(
                    (float)(thr.X + dp / cosLat * Math.Sin(crsRad)),
                    (float)(thr.Y + dp * Math.Cos(crsRad)));

                using (var p = new Pen(Color.FromArgb(80, 180, 180, 180), 1) { DashStyle = DashStyle.Dash })
                    g.DrawLine(p, toScreen(extFar.Y, extFar.X), toScreen(extPast.Y, extPast.X));

                // Runway rectangle
                float rwyLen   = (float)(rwy.LengthFt / 6076.0 / 60.0);
                float rwyWidth = (float)(rwy.WidthFt  / 6076.0 / 60.0 / 4);
                var rwyEnd = new PointF(
                    (float)(thr.X + rwyLen / cosLat * Math.Sin(crsRad)),
                    (float)(thr.Y + rwyLen * Math.Cos(crsRad)));
                double perpRad = crsRad + Math.PI / 2;
                var corners = new[]
                {
                    toScreen(thr.Y + rwyWidth * Math.Cos(perpRad), thr.X + rwyWidth / cosLat * Math.Sin(perpRad)),
                    toScreen(thr.Y - rwyWidth * Math.Cos(perpRad), thr.X - rwyWidth / cosLat * Math.Sin(perpRad)),
                    toScreen(rwyEnd.Y - rwyWidth * Math.Cos(perpRad), rwyEnd.X - rwyWidth / cosLat * Math.Sin(perpRad)),
                    toScreen(rwyEnd.Y + rwyWidth * Math.Cos(perpRad), rwyEnd.X + rwyWidth / cosLat * Math.Sin(perpRad)),
                };
                using (var brush = new SolidBrush(Color.FromArgb(60, RwyColor)))
                    g.FillPolygon(brush, corners);
                using (var p = new Pen(RwyColor, 1.5f))
                    g.DrawPolygon(p, corners);
            }

            // ── Draw approach legs ────────────────────────────────────────────────────
            int fafIdx  = app.FafIndex ?? -1;
            var mapPt   = app.MissedLegs.FirstOrDefault(l => l.Lat.HasValue && l.Lon.HasValue);

            DrawLegPath(g, toScreen, app.Legs,      new Pen(LegColor,    1.5f),  false);
            DrawLegPath(g, toScreen, app.MissedLegs, new Pen(MissedColor, 1.5f) { DashStyle = DashStyle.Dash }, false);

            // ── Fix symbols & labels ──────────────────────────────────────────────────
            for (int i = 0; i < app.Legs.Count; i++)
            {
                var leg = app.Legs[i];
                if (!leg.Lat.HasValue || !leg.Lon.HasValue) continue;
                var pt = toScreen(leg.Lat.Value, leg.Lon.Value);

                bool isIaf = (i == 0);
                bool isFaf = (i == fafIdx);

                DrawFixSymbol(g, pt, isIaf, isFaf, false);
                DrawFixLabel(g, pt, leg, isIaf || isFaf);
            }

            // MAP symbol
            if (app.MissedLegs.Count > 0)
            {
                var ml = app.MissedLegs[0];
                if (ml.Lat.HasValue && ml.Lon.HasValue)
                {
                    var pt = toScreen(ml.Lat.Value, ml.Lon.Value);
                    DrawFixSymbol(g, pt, false, false, true);
                    using (var br = new SolidBrush(MissedColor))
                    using (var fn = new Font("Consolas", S(6.5f), FontStyle.Bold))
                        g.DrawString("MAP", fn, br, pt.X + S(5), pt.Y - S(10));
                }
            }

            // North arrow
            DrawNorthArrow(g, w, h);
        }

        private void DrawLegPath(Graphics g, Func<double, double, PointF> toScreen,
                                  List<NavApproachLeg> legs, Pen pen, bool dashed)
        {
            using (pen)
            {
                PointF? prev = null;
                foreach (var leg in legs)
                {
                    if (!leg.Lat.HasValue || !leg.Lon.HasValue) { prev = null; continue; }
                    var cur = toScreen(leg.Lat.Value, leg.Lon.Value);

                    if (prev.HasValue)
                    {
                        if (leg.Type == "AF" && leg.CenterLat.HasValue && leg.CenterLon.HasValue)
                            DrawDmeArc(g, toScreen, prev.Value, cur, leg, pen);
                        else
                            g.DrawLine(pen, prev.Value, cur);
                    }
                    prev = cur;
                }
            }
        }

        private static void DrawDmeArc(Graphics g, Func<double, double, PointF> toScreen,
                                        PointF from, PointF to, NavApproachLeg leg, Pen pen)
        {
            // Centre in screen coords — use average of from/to for rough radius
            var cScreen = toScreen(leg.CenterLat.Value, leg.CenterLon.Value);
            float r = (float)Math.Sqrt(Math.Pow(from.X - cScreen.X, 2) + Math.Pow(from.Y - cScreen.Y, 2));
            if (r < 2) { g.DrawLine(pen, from, to); return; }

            float startAngle = (float)(Math.Atan2(from.Y - cScreen.Y, from.X - cScreen.X) * 180 / Math.PI);
            float endAngle   = (float)(Math.Atan2(to.Y   - cScreen.Y, to.X   - cScreen.X) * 180 / Math.PI);
            float sweep      = endAngle - startAngle;

            bool cw = (leg.TurnDirection ?? "R") == "R";
            if (cw  && sweep < 0) sweep += 360;
            if (!cw && sweep > 0) sweep -= 360;
            if (Math.Abs(sweep) < 1) sweep = cw ? 360 : -360;

            var rect = new RectangleF(cScreen.X - r, cScreen.Y - r, r * 2, r * 2);
            g.DrawArc(pen, rect, startAngle, sweep);
        }

        private void DrawFixSymbol(Graphics g, PointF pt, bool isIaf, bool isFaf, bool isMap)
        {
            if (isFaf)
            {
                float r = S(5);
                using (var p = new Pen(FafColor, 1.5f))
                    g.DrawEllipse(p, pt.X - r, pt.Y - r, r * 2, r * 2);
            }
            else if (isIaf)
            {
                float s = S(5);
                using (var p = new Pen(LegColor, 1.5f))
                    g.DrawPolygon(p, new[] {
                        new PointF(pt.X, pt.Y - s),
                        new PointF(pt.X + s, pt.Y + s),
                        new PointF(pt.X - s, pt.Y + s),
                    });
            }
            else if (isMap)
            {
                float s = S(4);
                using (var p = new Pen(MissedColor, 1.5f))
                    g.DrawRectangle(p, pt.X - s, pt.Y - s, s * 2, s * 2);
            }
            else
            {
                float r = S(2.5f);
                using (var br = new SolidBrush(Color.FromArgb(160, LegColor)))
                    g.FillEllipse(br, pt.X - r, pt.Y - r, r * 2, r * 2);
            }
        }

        private void DrawFixLabel(Graphics g, PointF pt, NavApproachLeg leg, bool important)
        {
            if (string.IsNullOrEmpty(leg.Fix)) return;
            Color c = important ? Color.White : Color.FromArgb(190, 190, 190);
            using (var br = new SolidBrush(c))
            using (var fn = new Font("Consolas", S(7f), FontStyle.Bold))
                g.DrawString(leg.Fix, fn, br, pt.X + S(6), pt.Y - S(10));

            if (leg.AltitudeFt > 0)
            {
                string alt = FormatAlt(leg.AltDescriptor, leg.AltitudeFt);
                using (var br = new SolidBrush(LabelColor))
                using (var fn = new Font("Consolas", S(6.5f)))
                    g.DrawString(alt, fn, br, pt.X + S(6), pt.Y + S(2));
            }
            if (leg.SpeedKts.HasValue)
            {
                using (var br = new SolidBrush(Color.FromArgb(160, 200, 255)))
                using (var fn = new Font("Consolas", S(6.5f)))
                    g.DrawString($"{leg.SpeedKts}kt", fn, br, pt.X + S(6), pt.Y + S(12));
            }
        }

        private void DrawNorthArrow(Graphics g, int w, int h)
        {
            float x = w - S(28), y = h - S(44), len = S(16);
            using (var p = new Pen(Color.FromArgb(160, 200, 200, 200), 1.5f))
                g.DrawLine(p, x, y + len, x, y);
            using (var br = new SolidBrush(Color.FromArgb(180, 200, 200, 200)))
            using (var fn = new Font("Consolas", S(7f), FontStyle.Bold))
                g.DrawString("N", fn, br, x - S(4), y - S(12));
        }

        // ── Profile view ─────────────────────────────────────────────────────────────

        private void PaintProfileView(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(10, 16, 24));

            if (_selected == null) return;

            var app = _selected;
            var rwy = GetRunway(app);
            var ils = GetIls(app);

            double rwyElev = rwy?.ElevationFt ?? _airportInfo?.ElevationFt ?? 0;

            // Build cumulative distances from threshold (reverse walk)
            var legs = app.Legs;
            int n    = legs.Count;
            if (n == 0) return;

            double[] distFromThr = new double[n];
            distFromThr[n - 1] = 0;
            for (int i = n - 2; i >= 0; i--)
                distFromThr[i] = distFromThr[i + 1] + legs[i + 1].DistanceNm;

            double maxDist = distFromThr[0];
            if (maxDist < 1) maxDist = 20;

            // Altitude range
            double maxAlt = legs.Where(l => l.AltitudeFt > 0).Select(l => l.AltitudeFt).DefaultIfEmpty(rwyElev + 5000).Max();
            double minAlt = rwyElev - 300;
            maxAlt = maxAlt * 1.08;

            // Margins
            int ml = (int)S(55), mr = (int)S(20), mt = (int)S(16), mb = (int)S(30);
            int pw = _pnlProfile.ClientSize.Width  - ml - mr;
            int ph = _pnlProfile.ClientSize.Height - mt - mb;
            if (pw < 10 || ph < 10) return;

            Func<double, float> toX = dist => ml + (float)((1.0 - dist / maxDist) * pw);
            Func<double, float> toY = alt  => mt + ph - (float)((alt - minAlt) / (maxAlt - minAlt) * ph);

            // Grid
            using (var gridPen = new Pen(Color.FromArgb(30, AxisColor), 1) { DashStyle = DashStyle.Dot })
            {
                for (double d = 0; d <= maxDist; d += NiceStep(maxDist, 6))
                    g.DrawLine(gridPen, toX(d), mt, toX(d), mt + ph);
                for (double a = Math.Ceiling(minAlt / 500) * 500; a <= maxAlt; a += NiceAltStep(maxAlt - minAlt))
                    g.DrawLine(gridPen, ml, toY(a), ml + pw, toY(a));
            }

            // Axes
            using (var axisPen = new Pen(AxisColor, 1))
            {
                g.DrawLine(axisPen, ml, mt,      ml,      mt + ph);
                g.DrawLine(axisPen, ml, mt + ph, ml + pw, mt + ph);
            }

            // Axis labels
            using (var br    = new SolidBrush(AxisColor))
            using (var axFn  = new Font("Consolas", S(6.5f)))
            {
                for (double d = 0; d <= maxDist + 0.01; d += NiceStep(maxDist, 6))
                    g.DrawString($"{d:F0}", axFn, br, toX(d) - S(6), mt + ph + S(4));
                for (double a = Math.Ceiling(minAlt / 500) * 500; a <= maxAlt; a += NiceAltStep(maxAlt - minAlt))
                    g.DrawString($"{a:F0}", axFn, br, S(2), toY(a) - S(7));
            }

            // Glideslope / glidepath / staircase
            int fafIdx = app.FafIndex.HasValue && app.FafIndex.Value < n ? app.FafIndex.Value : FindFafFallback(legs);

            string vg = app.VerticalGuidance;
            if (vg == "ils_gs" && ils?.Glideslope != null)
            {
                DrawGlidepath(g, toX, toY, fafIdx, distFromThr, legs, ils.Glideslope.PitchDeg, rwyElev, GsColor, false);
            }
            else if (vg == "vnav_path" && fafIdx >= 0 && legs[fafIdx].VerticalAngle.HasValue)
            {
                double angle = Math.Abs(legs[fafIdx].VerticalAngle.Value);
                DrawGlidepath(g, toX, toY, fafIdx, distFromThr, legs, angle, rwyElev, VnavColor, false);
            }
            else if (vg == "advisory" && fafIdx >= 0 && legs[fafIdx].VerticalAngle.HasValue)
            {
                double angle = Math.Abs(legs[fafIdx].VerticalAngle.Value);
                DrawGlidepath(g, toX, toY, fafIdx, distFromThr, legs, angle, rwyElev, AxisColor, true);
            }
            else
            {
                DrawStaircase(g, toX, toY, fafIdx, n - 1, distFromThr, legs);
            }

            // DA / MDA line
            double da = FindDaMda(legs);
            using (var profFn = new Font("Consolas", S(6.5f)))
            {
                if (da > rwyElev)
                {
                    using (var p = new Pen(DaColor, 1) { DashStyle = DashStyle.Dash })
                        g.DrawLine(p, ml, toY(da), ml + pw, toY(da));
                    using (var br = new SolidBrush(DaColor))
                        g.DrawString($"DA/MDA {da:F0}", profFn, br, ml + S(3), toY(da) - S(11));
                }

                // Fix ticks
                for (int i = 0; i < n; i++)
                {
                    var leg = legs[i];
                    float x = toX(distFromThr[i]);

                    using (var p = new Pen(Color.FromArgb(100, AxisColor), 1))
                        g.DrawLine(p, x, mt, x, mt + ph);

                    if (!string.IsNullOrEmpty(leg.Fix))
                    {
                        Color lc = (i == 0) ? LegColor : (i == fafIdx) ? FafColor : Color.FromArgb(170, 170, 170);
                        using (var br = new SolidBrush(lc))
                            g.DrawString(leg.Fix, profFn, br, x - S(10), mt + S(2));
                    }

                    if (leg.AltitudeFt > 0)
                    {
                        string alt = FormatAlt(leg.AltDescriptor, leg.AltitudeFt);
                        using (var br = new SolidBrush(LabelColor))
                            g.DrawString(alt, profFn, br, x - S(14), toY(leg.AltitudeFt) - S(13));

                        using (var p = new Pen(Color.FromArgb(50, LabelColor), 1) { DashStyle = DashStyle.Dot })
                            g.DrawLine(p, x - S(10), toY(leg.AltitudeFt), x + S(10), toY(leg.AltitudeFt));
                    }
                }

                // Profile axis titles
                using (var br = new SolidBrush(AxisColor))
                {
                    g.DrawString("ft MSL", profFn, br, S(2), mt);
                    g.DrawString("NM from threshold →", profFn, br, ml + pw / 2 - S(50), mt + ph + S(16));
                }
            }
        }

        private void DrawGlidepath(Graphics g, Func<double, float> toX, Func<double, float> toY,
            int fafIdx, double[] dist, List<NavApproachLeg> legs, double angleDeg, double rwyElev,
            Color color, bool dashed)
        {
            if (fafIdx < 0 || fafIdx >= legs.Count) return;
            double fafAlt  = legs[fafIdx].AltitudeFt > 0 ? legs[fafIdx].AltitudeFt : rwyElev + dist[fafIdx] * 6076 * Math.Tan(angleDeg * Math.PI / 180);
            double fafDist = dist[fafIdx];

            var pt1 = new PointF(toX(fafDist), toY(fafAlt));
            var pt2 = new PointF(toX(0),       toY(rwyElev + 50));

            using (var p = new Pen(color, 1.5f) { DashStyle = dashed ? DashStyle.Dash : DashStyle.Solid })
                g.DrawLine(p, pt1, pt2);

            using (var br = new SolidBrush(color))
            using (var fn = new Font("Consolas", S(6.5f)))
                g.DrawString($"{angleDeg:F1}°", fn, br, (pt1.X + pt2.X) / 2 + S(3), (pt1.Y + pt2.Y) / 2 - S(10));
        }

        private static void DrawStaircase(Graphics g, Func<double, float> toX, Func<double, float> toY,
            int startIdx, int endIdx, double[] dist, List<NavApproachLeg> legs)
        {
            using (var p = new Pen(Color.FromArgb(160, 160, 160), 1.5f))
            {
                for (int i = startIdx; i < endIdx; i++)
                {
                    if (legs[i].AltitudeFt <= 0 || legs[i + 1].AltitudeFt <= 0) continue;
                    float x1 = toX(dist[i]), y1 = toY(legs[i].AltitudeFt);
                    float x2 = toX(dist[i + 1]);
                    float y2 = toY(legs[i + 1].AltitudeFt);
                    g.DrawLine(p, x1, y1, x2, y1);   // horizontal
                    g.DrawLine(p, x2, y1, x2, y2);   // step down
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private NavIls GetIls(NavApproach app)
            => _ilsList?.FirstOrDefault(i =>
                string.Equals(i.Runway, app?.Runway, StringComparison.OrdinalIgnoreCase));

        private NavRunway GetRunway(NavApproach app)
            => _runways?.FirstOrDefault(r =>
                string.Equals(r.Name, app?.Runway, StringComparison.OrdinalIgnoreCase));

        private static double TrueRunwayBearing(NavRunway rwy)
        {
            double dLat = rwy.EndLat - rwy.ThresholdLat;
            double dLon = (rwy.EndLon - rwy.ThresholdLon) * Math.Cos(rwy.ThresholdLat * Math.PI / 180);
            double b = Math.Atan2(dLon, dLat) * 180 / Math.PI;
            return (b + 360) % 360;
        }

        private static RectangleF ComputeBounds(List<PointF> pts, float margin)
        {
            float minLat = pts.Min(p => p.Y), maxLat = pts.Max(p => p.Y);
            float minLon = pts.Min(p => p.X), maxLon = pts.Max(p => p.X);
            float dLat = maxLat - minLat, dLon = maxLon - minLon;
            float span = Math.Max(dLat, dLon / 1.5f);
            if (span < 0.01f) span = 0.5f;
            float cx = (minLon + maxLon) / 2, cy = (minLat + maxLat) / 2;
            float half = span * (1 + margin) / 2;
            return new RectangleF(cx - half * 1.5f, cy - half, half * 3, half * 2);
        }

        private static double FindDaMda(List<NavApproachLeg> legs)
        {
            for (int i = legs.Count - 1; i >= 0; i--)
                if (legs[i].AltitudeFt > 0 && legs[i].AltDescriptor == "A")
                    return legs[i].AltitudeFt;
            return 0;
        }

        private static int FindFafFallback(List<NavApproachLeg> legs)
        {
            for (int i = 0; i < legs.Count; i++)
                if (legs[i].VerticalAngle.HasValue) return i;
            for (int i = legs.Count - 2; i >= 0; i--)
                if (legs[i].AltitudeFt > 0 && legs[i].FixType != "R") return i;
            return 0;
        }

        private static string FormatAlt(string desc, double ft)
        {
            switch (desc)
            {
                case "+": return $"+{ft:F0}";
                case "-": return $"-{ft:F0}";
                case "B": return $"~{ft:F0}";
                default:  return $"{ft:F0}";
            }
        }

        private static double NiceStep(double range, int targetSteps)
        {
            double step = range / targetSteps;
            double[] nice = { 1, 2, 5, 10, 20, 50 };
            return nice.OrderBy(s => Math.Abs(s - step)).First();
        }

        private static double NiceAltStep(double range)
        {
            if (range > 15000) return 3000;
            if (range > 8000)  return 2000;
            if (range > 4000)  return 1000;
            return 500;
        }

        // ── ApproachItem wrapper ─────────────────────────────────────────────────────

        private sealed class ApproachItem
        {
            public NavApproach Approach { get; }
            public string Key { get; }
            public ApproachItem(NavApproach a)
            {
                Approach = a;
                Key = ApproachKey(a);
            }
            public override string ToString() => Approach.DisplayName;
        }

        private static string ApproachKey(NavApproach a)
            => $"{a.Type}{a.Suffix ?? ""}_{a.Runway ?? ""}";
    }
}
