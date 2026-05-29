using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
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
        private Panel          _pnlBriefing;
        private Panel          _pnlPlan;
        private Panel          _pnlProfile;
        private Panel          _pnlLoading;

        // ── DPI scaling ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")] private static extern int GetDpiForWindow(IntPtr hwnd);
        private int   _lastDpi = 96;
        private float _scale   = 1f;
        private float S(float px) => px * _scale;
        private Font  CF(float pt, FontStyle style = FontStyle.Regular)
            => new Font("Consolas", S(pt * 1.3f), style);

        // ── Data ─────────────────────────────────────────────────────────────────────
        private readonly string         _icao;
        private List<NavApproach>       _approaches  = new List<NavApproach>();
        private List<NavIls>            _ilsList     = new List<NavIls>();
        private List<NavRunway>         _runways     = new List<NavRunway>();
        private NavAirportInfo          _airportInfo;
        private NavApproach             _selected;
        private readonly NavApproach    _preselected;
        private List<IvaoAtcStation>        _atcStations = new List<IvaoAtcStation>();
        private readonly Dictionary<string, double> _freqCache = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // ── Colors ───────────────────────────────────────────────────────────────────
        private static readonly Color BgColor     = Color.FromArgb(1, 2, 3);
        private static readonly Color LegColor    = Color.FromArgb(220, 220, 220);
        private static readonly Color MissedColor = Color.FromArgb(0, 200, 255);
        private static readonly Color FafColor    = Color.FromArgb(0, 210, 100);
        private static readonly Color GsColor     = Color.FromArgb(255, 160, 40);
        private static readonly Color VnavColor   = Color.FromArgb(80, 220, 80);
        private static readonly Color DaColor     = Color.FromArgb(255, 80, 80);
        private static readonly Color LabelColor  = Color.FromArgb(255, 220, 80);
        private static readonly Color AxisColor   = Color.FromArgb(80, 100, 120);
        private static readonly Color RwyColor     = Color.FromArgb(180, 180, 180);
        private static readonly Color TransLegColor = Color.FromArgb(120, 150, 200);
        private static readonly Color FinalLegColor = Color.FromArgb(235, 235, 255);
        private static readonly Color GroundColor   = Color.FromArgb(55, 45, 20);

        // ── Constructor ──────────────────────────────────────────────────────────────

        internal ApproachChartForm(string icao, NavApproach preselected, IList<IvaoAtcStation> initialAtc = null)
        {
            _icao        = icao;
            _preselected = preselected;
            if (initialAtc != null) ApplyAtcInternal(initialAtc);
            InitLayout();
            this.Shown += (s, e) =>
            {
                _lastDpi = GetDpiForWindow(Handle);
                if (_lastDpi > 0) UpdateScale(_lastDpi);
                _ = LoadDataAsync();
            };
        }

        // ── ATC data ─────────────────────────────────────────────────────────────────

        private void ApplyAtcInternal(IList<IvaoAtcStation> stations)
        {
            // Exact-ICAO match for local positions (DEL/GND/TWR/ATIS).
            // Area positions (APP/DEP/CTR/FSS) also accept same 2-char country prefix
            // so e.g. SKMD_APP is recognised as the APP controller for SKRG.
            // Exact matches are sorted first so FirstOrDefault prefers them.
            string prefix2 = _icao?.Length >= 2 ? _icao.Substring(0, 2) : null;
            _atcStations = stations
                .Where(s =>
                {
                    if (string.Equals(s.Icao, _icao, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (prefix2 != null && IsAreaAtcPos(s.Position)
                        && s.Icao.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase))
                        return true;
                    return false;
                })
                .OrderBy(s => string.Equals(s.Icao, _icao, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
            foreach (var st in _atcStations)
                if (st.Frequency > 0)
                    _freqCache[st.Position] = st.Frequency;
        }

        private static bool IsAreaAtcPos(string pos) =>
            pos == "APP" || pos == "DEP" || pos == "CTR" || pos == "FSS";

        public void SetAtcData(IList<IvaoAtcStation> stations)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => SetAtcData(stations))); return; }
            ApplyAtcInternal(stations);
            _pnlBriefing.Invalidate();
        }

        // Seeds _freqCache with static VHF comm frequencies from the airport info API.
        // Only fills positions not already populated by live IVAO data.
        private void SeedFreqCacheFromAirportInfo(NavAirportInfo info)
        {
            if (info?.Freqs == null) return;
            foreach (var f in info.Freqs)
            {
                if (!double.TryParse(f.FrequencyMhz,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double mhz)) continue;
                if (mhz < 118.0 || mhz > 137.0) continue;   // VHF comm band only

                string pos = MapAirportFreqType(f.Type);
                if (pos == null) continue;
                if (!_freqCache.ContainsKey(pos))
                    _freqCache[pos] = mhz;
            }
            _pnlBriefing?.Invalidate();
        }

        private static string MapAirportFreqType(string type)
        {
            if (string.IsNullOrEmpty(type)) return null;
            switch (type.ToUpperInvariant())
            {
                case "GND":
                case "GROUND":
                    return "GND";
                case "TWR":
                case "TOWER":
                    return "TWR";
                case "APP":
                case "APPR":
                case "APPROACH":
                    return "APP";
                case "DEL":
                case "CLNC":
                case "CLNC DEL":
                case "CLEARANCE":
                case "DELIVERY":
                    return "DEL";
                default:
                    return null;
            }
        }

        // ── Layout ───────────────────────────────────────────────────────────────────

        private void InitLayout()
        {
            Text            = $"Approach Chart — {_icao}";
            Size            = new Size(820, 1020);
            MinimumSize     = new Size(600, 800);
            BackColor       = BgColor;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;

            // Toolbar
            _toolbar     = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(3, 4, 6) };
            _lblApproach = new Label { Text = "Approach:", ForeColor = Color.FromArgb(140, 160, 180),
                Font = new Font("Consolas", 8), AutoSize = true, Top = 9, Left = 8 };
            _cmbApproach = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(4, 5, 8),
                ForeColor = Color.White, Font = new Font("Consolas", 8),
                Top = 6, Left = 80, Width = 340,
            };
            _cmbApproach.SelectedIndexChanged += (s, e) => OnApproachSelected();
            _toolbar.Controls.Add(_lblApproach);
            _toolbar.Controls.Add(_cmbApproach);

            // Briefing strip
            _pnlBriefing = new Panel { Dock = DockStyle.Top, Height = 230, BackColor = Color.FromArgb(2, 3, 5) };
            _pnlBriefing.Paint += PaintBriefingStrip;

            // Split: plan (top) + profile (bottom)
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                BackColor = BgColor, BorderStyle = BorderStyle.None,
                SplitterDistance = 500, Panel1MinSize = 200, Panel2MinSize = 120,
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
            Controls.Add(_pnlBriefing);
            Controls.Add(_toolbar);

            SizeChanged += (s, e) => { _pnlPlan.Invalidate(); _pnlProfile.Invalidate(); _pnlBriefing.Invalidate(); };
        }

        // ── DPI handling ─────────────────────────────────────────────────────────────

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x0003) // WM_MOVE — fires whenever the window is dragged
            {
                int dpi = GetDpiForWindow(Handle);
                if (dpi > 0 && dpi != _lastDpi)
                {
                    _lastDpi = dpi;
                    UpdateScale(dpi);
                }
            }
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

            _pnlBriefing.Height = (int)S(230);
            _pnlBriefing.Invalidate();

            var oldLoad = _lblLoading.Font;
            _lblLoading.Font  = new Font("Consolas", S(11));
            oldLoad?.Dispose();

            int min1 = (int)S(200), min2 = (int)S(120);
            _split.Panel1MinSize = min1;
            _split.Panel2MinSize = min2;
            int available = _split.Height - _split.SplitterWidth;
            if (available > min1 + min2)
                _split.SplitterDistance = Math.Max(min1, Math.Min((int)S(500), available - min2));
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
            BeginInvoke(new Action(() =>
            {
                SeedFreqCacheFromAirportInfo(_airportInfo);
                PopulateCombo();
            }));
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
            _pnlPlan.Invalidate();
            _pnlProfile.Invalidate();
            _pnlBriefing.Invalidate();
        }

        // ── Plan view ────────────────────────────────────────────────────────────────

        private void PaintPlanView(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BgColor);

            if (_selected == null) return;

            // Approach name — top-left
            using (var br = new SolidBrush(Color.FromArgb(210, 225, 255)))
            using (var fn = CF(8f, FontStyle.Bold))
                g.DrawString(_selected.DisplayName.ToUpperInvariant(), fn, br, S(10), S(8));

            var app = _selected;
            var rwy = GetRunway(app);
            var ils = GetIls(app);

            // Collect geo points — only approach legs (not missed approach) drive the viewport
            var geoPoints  = app.Legs.Where(l => l.Lat.HasValue && l.Lon.HasValue)
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

            // ── Approach cone + distance tick marks ───────────────────────────────────
            if (rwy != null)
                DrawApproachCone(g, toScreen, rwy, app, ils);

            // ── Draw approach legs ────────────────────────────────────────────────────
            int fafIdx  = app.FafIndex ?? -1;
            var mapPt   = app.MissedLegs.FirstOrDefault(l => l.Lat.HasValue && l.Lon.HasValue);

            DrawApproachLegs(g, toScreen, app, fafIdx);
            using (var p = new Pen(MissedColor, 1.5f) { DashStyle = DashStyle.Dash })
                DrawLegRange(g, toScreen, app.MissedLegs, 0, app.MissedLegs.Count, p, false);

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

            // Section border
            using (var p = new Pen(Color.FromArgb(18, 28, 48), 1))
                g.DrawRectangle(p, 0, 0, w - 1, h - 1);
        }

        private void DrawApproachLegs(Graphics g, Func<double, double, PointF> toScreen, NavApproach app, int fafIdx)
        {
            int n = app.Legs.Count;
            if (n == 0) return;
            int splitAt = (fafIdx >= 0 && fafIdx < n) ? fafIdx : n;
            if (splitAt > 0)
            {
                using (var p = new Pen(TransLegColor, 1f))
                    DrawLegRange(g, toScreen, app.Legs, 0, splitAt + 1, p, true);
            }
            if (splitAt < n)
            {
                using (var p = new Pen(FinalLegColor, 2f))
                    DrawLegRange(g, toScreen, app.Legs, splitAt, n, p, true);
            }
        }

        private void DrawLegRange(Graphics g, Func<double, double, PointF> toScreen,
                                   List<NavApproachLeg> legs, int startIdx, int endIdx, Pen pen, bool drawLabels)
        {
            PointF? prev = null;
            for (int i = startIdx; i < endIdx && i < legs.Count; i++)
            {
                var leg = legs[i];
                if (!leg.Lat.HasValue || !leg.Lon.HasValue) { prev = null; continue; }
                var cur = toScreen(leg.Lat.Value, leg.Lon.Value);
                if (prev.HasValue)
                {
                    if (leg.Type == "AF" && leg.CenterLat.HasValue && leg.CenterLon.HasValue)
                        DrawDmeArc(g, toScreen, prev.Value, cur, leg, pen);
                    else
                    {
                        g.DrawLine(pen, prev.Value, cur);
                        if (drawLabels)
                        {
                            DrawDirectionArrow(g, prev.Value, cur, pen.Color);
                            if (leg.DistanceNm > 0.1)
                                DrawSegmentLabel(g, prev.Value, cur, leg.DistanceNm, leg.Course, pen.Color);
                        }
                    }
                }
                prev = cur;
            }
        }

        private void DrawDirectionArrow(Graphics g, PointF from, PointF to, Color c)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < S(25)) return;
            float ux = dx / len, uy = dy / len;
            float mx = (from.X + to.X) / 2, my = (from.Y + to.Y) / 2;
            float al = S(5f), aw = S(2.5f);
            using (var br = new SolidBrush(Color.FromArgb(180, c)))
                g.FillPolygon(br, new PointF[] {
                    new PointF(mx + ux * al, my + uy * al),
                    new PointF(mx - uy * aw, my + ux * aw),
                    new PointF(mx + uy * aw, my - ux * aw),
                });
        }

        private void DrawSegmentLabel(Graphics g, PointF from, PointF to, double distNm, double course, Color lineColor)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < S(28)) return;
            float mx = (from.X + to.X) / 2, my = (from.Y + to.Y) / 2;
            float nx = -dy / len, ny = dx / len;
            float off = S(9);
            using (var br = new SolidBrush(Color.FromArgb(200, lineColor)))
            using (var fn = new Font("Consolas", S(5.5f)))
            {
                g.DrawString($"{distNm:F1}", fn, br, mx + nx * off - S(8), my + ny * off - S(5));
                if (course > 0)
                    g.DrawString($"{course:F0}°", fn, br, mx + nx * off - S(8), my + ny * off + S(3));
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
                {
                    g.DrawEllipse(p, pt.X - r, pt.Y - r, r * 2, r * 2);
                    g.DrawLine(p, pt.X - r, pt.Y, pt.X + r, pt.Y);
                    g.DrawLine(p, pt.X, pt.Y - r, pt.X, pt.Y + r);
                }
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
            g.SmoothingMode           = SmoothingMode.AntiAlias;
            g.TextRenderingHint       = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.FromArgb(1, 1, 3));

            if (_selected == null) return;

            var app = _selected;
            var rwy = GetRunway(app);
            var ils = GetIls(app);
            double rwyElev = rwy?.ElevationFt ?? _airportInfo?.ElevationFt ?? 0;

            var legs = app.Legs;
            int n    = legs.Count;
            if (n == 0) return;

            // ── Cumulative distances from threshold ───────────────────────────────
            double[] distFromThr = new double[n];
            for (int i = n - 2; i >= 0; i--)
                distFromThr[i] = distFromThr[i + 1] + legs[i + 1].DistanceNm;
            double maxDist = distFromThr[0];
            if (maxDist < 1) maxDist = 20;
            // Add ~7% left padding so the first fix never lands on the Y-axis
            maxDist *= 1.07;

            // ── Glidepath source ──────────────────────────────────────────────────
            // Only trust app.FafIndex when it points to a leg that actually has a
            // VerticalAngle.  Many SKBO approaches ship faf_index=0 (first leg, no VA)
            // which is never the true FAF.
            int fafFallback = FindFafFallback(legs);
            int fafIdx = (app.FafIndex.HasValue
                          && app.FafIndex.Value < n
                          && legs[app.FafIndex.Value].VerticalAngle.HasValue)
                ? app.FafIndex.Value
                : fafFallback;
            // IF legs are never the FAF (ARINC 424)
            while (fafIdx < n - 1 && legs[fafIdx].Type == "IF")
                fafIdx++;
            double? gpAngle = null;
            Color   gpColor = GsColor;
            bool    gpDashed = false;
            if (ils?.Glideslope != null)
            {
                gpAngle = ils.Glideslope.PitchDeg;
            }
            else if (fafIdx >= 0 && fafIdx < n && legs[fafIdx].VerticalAngle.HasValue)
            {
                gpAngle  = Math.Abs(legs[fafIdx].VerticalAngle.Value);
                string vg = app.VerticalGuidance ?? "";
                if (vg == "advisory") { gpColor = AxisColor; gpDashed = true; }
                else gpColor = VnavColor;
            }
            else
            {
                foreach (var lg in legs)
                    if (lg.VerticalAngle.HasValue) { gpAngle = Math.Abs(lg.VerticalAngle.Value); gpColor = VnavColor; break; }
            }
            bool hasGP = gpAngle.HasValue;

            // ── Layout zones ──────────────────────────────────────────────────────
            int PW = _pnlProfile.ClientSize.Width;
            int PH = _pnlProfile.ClientSize.Height;

            float hdrH   = S(62f);                 // fix-label header band
            float gsH    = hasGP ? S(32f) : 0f;    // ground-speed table
            float ml     = S(52f);                  // left  (altitude axis labels)
            float mr     = S(68f);                  // right (runway annotation)
            float mbAxis = S(22f);                  // x-axis labels row
            float mt     = hdrH;
            float mb     = mbAxis + gsH;

            float pw = PW - ml - mr;
            float ph = PH - mt - mb;
            if (pw < 20 || ph < 20) return;

            // ── Altitude range ────────────────────────────────────────────────────
            double maxAlt = legs.Where(l => l.AltitudeFt > 0).Select(l => l.AltitudeFt)
                               .DefaultIfEmpty(rwyElev + 5000).Max() * 1.08;
            double minAlt = rwyElev - 200;

            Func<double, float> toX = dist => ml + (float)((1.0 - dist / maxDist) * pw);
            Func<double, float> toY = alt  => mt + ph - (float)((alt - minAlt) / (maxAlt - minAlt) * ph);

            // ── Ground fill ───────────────────────────────────────────────────────
            float groundY = toY(rwyElev);
            float baseY   = mt + ph;
            if (groundY < baseY)
            {
                using (var br = new SolidBrush(Color.FromArgb(130, GroundColor)))
                    g.FillRectangle(br, ml, groundY, pw, baseY - groundY);
                using (var p = new Pen(Color.FromArgb(200, 140, 110, 45), 1.5f))
                    g.DrawLine(p, ml, groundY, ml + pw, groundY);
            }

            // ── Grid ──────────────────────────────────────────────────────────────
            using (var gridPen = new Pen(Color.FromArgb(28, AxisColor), 1) { DashStyle = DashStyle.Dot })
            {
                for (double d = 0; d <= maxDist + 0.01; d += NiceStep(maxDist, 6))
                    g.DrawLine(gridPen, toX(d), mt, toX(d), mt + ph);
                for (double a = Math.Ceiling(minAlt / 500) * 500; a <= maxAlt; a += NiceAltStep(maxAlt - minAlt))
                    g.DrawLine(gridPen, ml, toY(a), ml + pw, toY(a));
            }

            // ── Axes ──────────────────────────────────────────────────────────────
            using (var axisPen = new Pen(AxisColor, 1))
            {
                g.DrawLine(axisPen, ml, mt,      ml,      mt + ph);
                g.DrawLine(axisPen, ml, mt + ph, ml + pw, mt + ph);
            }
            using (var br   = new SolidBrush(AxisColor))
            using (var axFn = new Font("Consolas", S(6f)))
            {
                double xStep = NiceStep(maxDist, 6);
                for (double d = 0; d <= maxDist + 0.01; d += xStep)
                    g.DrawString($"{d:F0}", axFn, br, toX(d) - S(5), mt + ph + S(4));
                for (double a = Math.Ceiling(minAlt / 500) * 500; a <= maxAlt; a += NiceAltStep(maxAlt - minAlt))
                    g.DrawString($"{a:F0}", axFn, br, S(2), toY(a) - S(6));
                g.DrawString("ft MSL", axFn, br, S(2), mt + S(2));
                g.DrawString("NM from threshold →", axFn, br, ml + pw / 2f - S(44), mt + ph + S(14));
            }

            // ── Glidepath cone + line (or staircase) ─────────────────────────────
            if (hasGP)
            {
                DrawGlidepathCone(g, toX, toY, fafIdx, distFromThr, legs, gpAngle.Value, rwyElev, gpColor);
                DrawGlidepath(g, toX, toY, fafIdx, distFromThr, legs, gpAngle.Value, rwyElev, gpColor, gpDashed);
                // Level-off / staircase before the FAF (e.g. IF→FAF at same altitude)
                if (fafIdx > 0)
                    DrawPreFafPath(g, toX, toY, fafIdx, distFromThr, legs);
            }
            else
            {
                if (fafIdx > 0)
                    DrawPreFafPath(g, toX, toY, fafIdx, distFromThr, legs);
                DrawStaircase(g, toX, toY, fafIdx, n - 1, distFromThr, legs);
            }

            // ── DA / MDA dashed line ──────────────────────────────────────────────
            double da = FindDaMda(legs);
            if (da > rwyElev)
            {
                float daY = toY(da);
                using (var p = new Pen(DaColor, 1.2f) { DashStyle = DashStyle.Dash })
                    g.DrawLine(p, ml, daY, ml + pw + S(8), daY);
                string daLbl = $"DA/MDA {da:F0}'";
                using (var fn = new Font("Consolas", S(6.5f), FontStyle.Bold))
                using (var br = new SolidBrush(DaColor))
                {
                    g.DrawString(daLbl, fn, br, ml + S(3), daY - S(12));
                    var sz = g.MeasureString(daLbl, fn);
                    g.DrawString(daLbl, fn, br, ml + pw - sz.Width + S(6), daY - S(12));
                }
            }

            // ── Fix vertical ticks (in chart area, drawn before header overlay) ──
            for (int i = 0; i < n; i++)
            {
                float x = toX(distFromThr[i]);
                Color tc = (i == fafIdx) ? FafColor : Color.FromArgb(60, 90, 130);
                float tw = (i == fafIdx) ? 1.5f : 1f;
                using (var p = new Pen(tc, tw) { DashStyle = DashStyle.Dash })
                    g.DrawLine(p, x, mt, x, mt + ph);

                // Altitude tick mark and label next to the fix line in the chart
                if (legs[i].AltitudeFt > 0)
                {
                    float ay = toY(legs[i].AltitudeFt);
                    using (var p = new Pen(Color.FromArgb(90, LabelColor), 1) { DashStyle = DashStyle.Dot })
                        g.DrawLine(p, x - S(12), ay, x + S(12), ay);
                }
            }

            // ── ILS ident + freq at FAF ───────────────────────────────────────────
            if (ils != null && fafIdx >= 0 && fafIdx < n)
            {
                float fafX   = toX(distFromThr[fafIdx]);
                double fafAlt = legs[fafIdx].AltitudeFt > 0
                    ? legs[fafIdx].AltitudeFt
                    : rwyElev + distFromThr[fafIdx] * 6076 * Math.Tan(gpAngle.GetValueOrDefault(3) * Math.PI / 180);
                float fafY = toY(fafAlt);
                string ilsTxt = $"{ils.Ident}  {ils.FrequencyMhz:F2}";
                using (var fn = new Font("Consolas", S(6f)))
                using (var br = new SolidBrush(Color.FromArgb(160, GsColor)))
                {
                    var sz = g.MeasureString(ilsTxt, fn);
                    g.DrawString(ilsTxt, fn, br, fafX - sz.Width / 2f, fafY - S(16));
                }
            }

            // ── Threshold / runway annotation (right margin) ──────────────────────
            float thrX = toX(0);
            using (var p = new Pen(Color.FromArgb(200, 200, 200), 2f))
                g.DrawLine(p, thrX, mt + ph * 0.35f, thrX, mt + ph);  // bold threshold line
            {
                string rwLabel = "RW" + (app.Runway ?? rwy?.Name ?? "");
                float  rwX     = ml + pw + S(6);
                float  rwY     = mt + S(4);
                using (var fn = new Font("Consolas", S(7.5f), FontStyle.Bold))
                using (var br = new SolidBrush(Color.FromArgb(220, 225, 235)))
                    g.DrawString(rwLabel, fn, br, rwX, rwY);
                if (rwy != null && rwy.LengthFt > 0)
                    using (var fn = new Font("Consolas", S(6f)))
                    using (var br = new SolidBrush(Color.FromArgb(150, 165, 190)))
                        g.DrawString($"Rwy {rwy.LengthFt:F0}'", fn, br, rwX, rwY + S(13));
            }

            // ── Header band ───────────────────────────────────────────────────────
            DrawProfileHeader(g, toX, toY, legs, distFromThr, fafIdx, n, mt, hdrH, rwyElev,
                              app.Runway ?? rwy?.Name ?? "");

            // ── Ground speed table ────────────────────────────────────────────────
            if (hasGP)
                DrawGsTable(g, gpAngle.Value, PW, PH, gsH, mbAxis, ml, mr, pw);

            // ── Panel border ──────────────────────────────────────────────────────
            using (var p = new Pen(Color.FromArgb(55, 75, 105), 1))
                g.DrawRectangle(p, 0, 0, PW - 1, PH - 1);
        }

        private void DrawProfileHeader(Graphics g,
            Func<double, float> toX, Func<double, float> toY,
            List<NavApproachLeg> legs, double[] distFromThr, int fafIdx, int n,
            float chartTop, float hdrH, double rwyElev, string rwName)
        {
            // Tinted background strip
            using (var br = new SolidBrush(Color.FromArgb(2, 5, 10)))
                g.FillRectangle(br, 0, 0, _pnlProfile.ClientSize.Width, hdrH);
            using (var p = new Pen(Color.FromArgb(14, 28, 52), 1))
                g.DrawLine(p, 0, hdrH - 1, _pnlProfile.ClientSize.Width, hdrH - 1);

            // ── Fix labels ────────────────────────────────────────────────────────
            for (int i = 0; i < n; i++)
            {
                var   leg = legs[i];
                float x   = toX(distFromThr[i]);
                bool  isFaf = (i == fafIdx);

                // Vertical drop-line from header band top to chart area
                using (var p = new Pen(isFaf ? FafColor : Color.FromArgb(55, 82, 120), isFaf ? 1.5f : 1f))
                    g.DrawLine(p, x, 0, x, hdrH - 1);

                if (string.IsNullOrEmpty(leg.Fix)) continue;

                Color nameColor = (i == 0)   ? LegColor
                               : isFaf        ? FafColor
                               :                Color.FromArgb(200, 215, 235);

                // Fix name — row 1
                using (var fn = new Font("Consolas", S(7f), isFaf ? FontStyle.Bold : FontStyle.Regular))
                using (var br = new SolidBrush(nameColor))
                {
                    var sz = g.MeasureString(leg.Fix, fn);
                    g.DrawString(leg.Fix, fn, br, x - sz.Width / 2f, S(3));
                }

                // Altitude constraint — row 2
                if (leg.AltitudeFt > 0)
                {
                    string altStr   = FormatAlt(leg.AltDescriptor, leg.AltitudeFt) + "'";
                    Color  altColor = isFaf ? Color.FromArgb(255, 215, 100) : LabelColor;
                    using (var fn = new Font("Consolas", S(7f), FontStyle.Bold))
                    using (var br = new SolidBrush(altColor))
                    {
                        var sz = g.MeasureString(altStr, fn);
                        g.DrawString(altStr, fn, br, x - sz.Width / 2f, S(14));
                    }
                }

                // Speed restriction — row 3
                if (leg.SpeedKts.HasValue && leg.SpeedKts.Value > 0)
                {
                    string spdStr = $"AT {leg.SpeedKts}KT";
                    using (var fn = new Font("Consolas", S(6f)))
                    using (var br = new SolidBrush(Color.FromArgb(150, 215, 155)))
                    {
                        var sz = g.MeasureString(spdStr, fn);
                        g.DrawString(spdStr, fn, br, x - sz.Width / 2f, S(26));
                    }
                }
            }

            // ── Threshold label in header ─────────────────────────────────────────
            {
                float thrX = toX(0);
                using (var fn = new Font("Consolas", S(7f), FontStyle.Bold))
                using (var br = new SolidBrush(Color.FromArgb(200, 210, 225)))
                {
                    string thr = "[RW" + rwName + "]";
                    var sz = g.MeasureString(thr, fn);
                    g.DrawString(thr, fn, br, thrX - sz.Width / 2f, S(3));
                }
                using (var p = new Pen(Color.FromArgb(120, 140, 170), 1))
                    g.DrawLine(p, thrX, 0, thrX, hdrH - 1);
            }

            // ── Course and distance labels between consecutive fixes ───────────────
            for (int i = 0; i < n - 1; i++)
            {
                float x1   = toX(distFromThr[i]);
                float x2   = toX(distFromThr[i + 1]);
                float midX = (x1 + x2) / 2f;
                float segW = Math.Abs(x1 - x2);
                if (segW < S(18)) continue;  // too narrow to annotate

                // Course badge
                double crs = legs[i + 1].Course;
                if (crs > 0.5)
                {
                    string crsStr = $"{crs:F0}°";
                    using (var fn = new Font("Consolas", S(6.5f)))
                    {
                        var   sz  = g.MeasureString(crsStr, fn);
                        float bx  = midX - sz.Width / 2f - S(3);
                        float by  = S(38);
                        float bw  = sz.Width + S(6);
                        float bh  = S(12);
                        using (var p = new Pen(Color.FromArgb(60, 90, 130), 1))
                            g.DrawRectangle(p, bx, by, bw, bh);
                        using (var br = new SolidBrush(Color.FromArgb(160, 178, 210)))
                            g.DrawString(crsStr, fn, br, bx + S(3), by + S(1));
                    }
                }

                // Distance label
                double segDist = distFromThr[i] - distFromThr[i + 1];
                if (segDist > 0.05)
                {
                    string dStr = $"{segDist:F1}";
                    using (var fn = new Font("Consolas", S(6f)))
                    using (var br = new SolidBrush(Color.FromArgb(125, 145, 178)))
                    {
                        var sz = g.MeasureString(dStr, fn);
                        g.DrawString(dStr, fn, br, midX - sz.Width / 2f, S(51));
                    }
                }
            }

            // Distance from last fix to threshold
            if (n > 0 && distFromThr[n - 1] < 0.5 && n >= 2)
            {
                double lastSeg = distFromThr[n - 2] - distFromThr[n - 1];
                if (lastSeg > 0.05)
                {
                    float x1   = toX(distFromThr[n - 1]);
                    float x2   = toX(0);
                    float midX = (x1 + x2) / 2f;
                    if (Math.Abs(x2 - x1) >= S(14))
                    {
                        string dStr = $"{lastSeg:F1}";
                        using (var fn = new Font("Consolas", S(6f)))
                        using (var br = new SolidBrush(Color.FromArgb(125, 145, 178)))
                        {
                            var sz = g.MeasureString(dStr, fn);
                            g.DrawString(dStr, fn, br, midX - sz.Width / 2f, S(51));
                        }
                    }
                }
            }
        }

        private void DrawGsTable(Graphics g, double angleDeg, int PW, int PH,
                                  float gsH, float mbAxis, float ml, float mr, float pw)
        {
            float tableY = PH - mbAxis - gsH + S(2);
            float tableH = gsH - S(4);

            using (var br = new SolidBrush(Color.FromArgb(1, 3, 6)))
                g.FillRectangle(br, ml, tableY, pw, tableH);
            using (var p = new Pen(Color.FromArgb(14, 22, 38), 1))
                g.DrawRectangle(p, ml, tableY, pw, tableH);

            // GS% label (gradient percentage ≈ tan × 100)
            double gradPct = Math.Tan(angleDeg * Math.PI / 180.0) * 100.0;
            int[] speeds = { 70, 90, 100, 120, 140, 160 };
            float lblW   = S(52f);
            float colW   = (pw - lblW) / speeds.Length;

            using (var fn = new Font("Consolas", S(5.5f)))
            using (var br = new SolidBrush(Color.FromArgb(110, 130, 160)))
            {
                g.DrawString($"GS {gradPct:F2}%", fn, br, ml + S(3), tableY + S(2));
                g.DrawString("GS-Kts", fn, br, ml + S(3), tableY + tableH / 2f - S(4));
            }

            using (var divPen = new Pen(Color.FromArgb(10, 18, 32), 1))
                g.DrawLine(divPen, ml + lblW, tableY, ml + lblW, tableY + tableH);

            for (int j = 0; j < speeds.Length; j++)
            {
                float cx  = ml + lblW + j * colW;
                int   spd = speeds[j];
                double rod = spd * 101.27 * Math.Tan(angleDeg * Math.PI / 180.0);

                // Column divider
                if (j > 0)
                    using (var p = new Pen(Color.FromArgb(10, 18, 32), 1))
                        g.DrawLine(p, cx, tableY, cx, tableY + tableH);

                using (var fn = new Font("Consolas", S(6.5f), FontStyle.Bold))
                using (var br = new SolidBrush(Color.FromArgb(190, 205, 230)))
                    g.DrawString($"{spd}", fn, br, cx + S(3), tableY + S(2));

                using (var fn = new Font("Consolas", S(6.5f)))
                using (var br = new SolidBrush(Color.FromArgb(150, 170, 205)))
                    g.DrawString($"{rod:F0}", fn, br, cx + S(3), tableY + tableH / 2f - S(4));
            }

            // Horizontal divider between speed and ROD rows
            float midRow = tableY + tableH / 2f;
            using (var p = new Pen(Color.FromArgb(10, 18, 32), 1))
                g.DrawLine(p, ml, midRow, ml + pw, midRow);
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

        // Draws the approach path for legs before the FAF as direct fix-to-fix diagonals.
        // Only fixes with a known altitude are connected; fixes without altitude are
        // bridged over (no discontinuity).  Same altitude → horizontal; different → slope.
        private void DrawPreFafPath(Graphics g, Func<double, float> toX, Func<double, float> toY,
            int fafIdx, double[] dist, List<NavApproachLeg> legs)
        {
            // Collect (x, y) for every fix from index 0 through fafIdx that has an altitude.
            // Always include the FAF as the closing endpoint.
            var pts      = new List<PointF>();
            var dotIdxs  = new List<int>();
            for (int i = 0; i <= fafIdx; i++)
            {
                if (legs[i].AltitudeFt <= 0) continue;
                pts.Add(new PointF(toX(dist[i]), toY(legs[i].AltitudeFt)));
                if (i < fafIdx) dotIdxs.Add(i);
            }
            if (pts.Count < 2) return;

            using (var pen = new Pen(Color.FromArgb(200, TransLegColor), 1.5f))
                for (int i = 0; i + 1 < pts.Count; i++)
                    g.DrawLine(pen, pts[i], pts[i + 1]);

            float r = S(3f);
            foreach (int di in dotIdxs)
            {
                float x = toX(dist[di]);
                float y = toY(legs[di].AltitudeFt);
                using (var br = new SolidBrush(TransLegColor))
                    g.FillEllipse(br, x - r, y - r, r * 2, r * 2);
            }
        }

        // ── Approach cone (plan view) ─────────────────────────────────────────────────

        private void DrawApproachCone(Graphics g, Func<double, double, PointF> toScreen,
                                       NavRunway rwy, NavApproach app, NavIls ils)
        {
            double crsRad  = TrueRunwayBearing(rwy) * Math.PI / 180.0;
            double oppRad  = crsRad + Math.PI;
            double cosLat  = Math.Cos(rwy.ThresholdLat * Math.PI / 180);
            double thrLon  = rwy.ThresholdLon, thrLat = rwy.ThresholdLat;

            // Use actual loc_width / 2 when available, else default by approach type
            double halfDeg = ils?.LocWidth != null ? ils.LocWidth.Value / 2.0
                           : (app.Type == "ILS" || app.Type == "LOC") ? 2.0 : 2.5;
            double halfRad    = halfDeg * Math.PI / 180.0;
            double d5         = 5.0 / 60.0;

            var thrScreen  = toScreen(thrLat, thrLon);
            var leftFarPt  = new { Lat = thrLat + d5 * Math.Cos(oppRad - halfRad),
                                   Lon = thrLon + d5 / cosLat * Math.Sin(oppRad - halfRad) };
            var rightFarPt = new { Lat = thrLat + d5 * Math.Cos(oppRad + halfRad),
                                   Lon = thrLon + d5 / cosLat * Math.Sin(oppRad + halfRad) };
            var lScreen = toScreen(leftFarPt.Lat,  leftFarPt.Lon);
            var rScreen = toScreen(rightFarPt.Lat, rightFarPt.Lon);

            // Filled cone
            using (var br = new SolidBrush(Color.FromArgb(20, 100, 160, 255)))
                g.FillPolygon(br, new[] { thrScreen, lScreen, rScreen });
            // Cone edges
            using (var p = new Pen(Color.FromArgb(55, 110, 160, 220), 1) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(p, thrScreen, lScreen);
                g.DrawLine(p, thrScreen, rScreen);
            }

            // Distance tick marks: 1–5 NM along extended centerline
            for (int nm = 1; nm <= 5; nm++)
            {
                double d = nm / 60.0;
                double tLon = thrLon + d / cosLat * Math.Sin(oppRad);
                double tLat = thrLat + d           * Math.Cos(oppRad);
                var tickSc  = toScreen(tLat, tLon);

                // Perpendicular direction via a 0.5 NM offset along the centerline
                double nLon = thrLon + (d + 0.5 / 60.0) / cosLat * Math.Sin(oppRad);
                double nLat = thrLat + (d + 0.5 / 60.0)           * Math.Cos(oppRad);
                var    nSc  = toScreen(nLat, nLon);
                float ddx = nSc.X - tickSc.X, ddy = nSc.Y - tickSc.Y;
                float dlen = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
                if (dlen < 0.5f) continue;
                float perpX = -ddy / dlen, perpY = ddx / dlen;
                float tlen  = S(6);

                using (var p = new Pen(Color.FromArgb(100, 180, 180, 180), 1))
                    g.DrawLine(p,
                        tickSc.X - perpX * tlen, tickSc.Y - perpY * tlen,
                        tickSc.X + perpX * tlen, tickSc.Y + perpY * tlen);
                using (var br = new SolidBrush(Color.FromArgb(140, 180, 200, 200)))
                using (var fn = new Font("Consolas", S(5.5f)))
                    g.DrawString($"{nm}", fn, br, tickSc.X + S(3), tickSc.Y - S(7));
            }
        }

        // ── Glidepath cone (profile view) ────────────────────────────────────────────

        private void DrawGlidepathCone(Graphics g, Func<double, float> toX, Func<double, float> toY,
            int fafIdx, double[] dist, List<NavApproachLeg> legs, double angleDeg, double rwyElev, Color color)
        {
            if (fafIdx < 0 || fafIdx >= legs.Count) return;
            double fafDist = dist[fafIdx];
            double fafAlt  = legs[fafIdx].AltitudeFt > 0
                ? legs[fafIdx].AltitudeFt
                : rwyElev + fafDist * 6076 * Math.Tan(angleDeg * Math.PI / 180);

            // Anchor cone to the ACTUAL drawn line angle (fafAlt → rwyElev) so the
            // ±0.5° band stays centred on the line even when fafAlt exceeds the
            // theoretical altitude for the declared angle (common in SKBO ILS X/Z).
            double lineDeg = fafDist > 0
                ? Math.Atan2(fafAlt - rwyElev, fafDist * 6076) * 180.0 / Math.PI
                : angleDeg;
            double hiAngle = lineDeg + 0.5;
            double loAngle = Math.Max(lineDeg - 0.5, 0.3);
            double hiAlt   = rwyElev + fafDist * 6076 * Math.Tan(hiAngle * Math.PI / 180);
            double loAlt   = rwyElev + fafDist * 6076 * Math.Tan(loAngle * Math.PI / 180);

            var cone = new PointF[]
            {
                new PointF(toX(fafDist), toY(hiAlt)),
                new PointF(toX(fafDist), toY(loAlt)),
                new PointF(toX(0),       toY(rwyElev + 40)),
                new PointF(toX(0),       toY(rwyElev + 80)),
            };
            using (var br = new SolidBrush(Color.FromArgb(28, color)))
                g.FillPolygon(br, cone);
        }

        // ── Briefing strip ────────────────────────────────────────────────────────────

        private void PaintBriefingStrip(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.FromArgb(2, 3, 5));

            int W = _pnlBriefing.ClientSize.Width;
            int H = _pnlBriefing.ClientSize.Height;

            var app    = _selected;
            var rwy    = app != null ? GetRunway(app) : null;
            var ils    = app != null ? GetIls(app)    : null;
            var info   = _airportInfo;
            double rwyElev = rwy?.ElevationFt ?? info?.ElevationFt ?? 0;

            Color cBorder   = Color.FromArgb(30, 42, 65);
            Color cHdr      = Color.FromArgb(120, 140, 165);
            Color cVal      = Color.FromArgb(230, 235, 255);
            Color cAccYel   = Color.FromArgb(255, 215, 70);
            Color cAccRed   = Color.FromArgb(255, 90,  90);
            Color cAccGreen = Color.FromArgb(80,  210, 120);
            Color cAccOrng  = Color.FromArgb(255, 175, 60);

            float rTitle  = S(40f);
            float rFreq   = S(34f);
            float rData   = S(96f);
            float rInfo   = S(28f);
            float yFreq   = rTitle;
            float yData   = yFreq + rFreq;
            float yInfo   = yData + rData;
            float yMissed = yInfo + rInfo;

            // ── Row 1: Title bar ──────────────────────────────────────────────────────
            {
                // Left: ICAO/IATA bold + airport name below
                string iataStr = !string.IsNullOrEmpty(info?.IataCode)
                    ? $"{_icao}/{info.IataCode}" : (_icao ?? "");
                string aptName = info?.Name?.ToUpperInvariant() ?? "";

                using (var fn = new Font("Consolas", S(11f), FontStyle.Bold))
                using (var br = new SolidBrush(cVal))
                    g.DrawString(iataStr, fn, br, S(8), S(5));

                if (aptName.Length > 0)
                    using (var fn = new Font("Consolas", S(8.5f)))
                    using (var br = new SolidBrush(Color.FromArgb(175, 190, 215)))
                        g.DrawString(aptName, fn, br, S(8), S(22));

                // Right: city/country + approach name below
                {
                    string city    = info?.City;
                    string country = info?.Country ?? IcaoToCountry(_icao);
                    var cc = new System.Text.StringBuilder();
                    if (!string.IsNullOrEmpty(city))    cc.Append(city.ToUpperInvariant());
                    if (!string.IsNullOrEmpty(country)) { if (cc.Length > 0) cc.Append(", "); cc.Append(country.ToUpperInvariant()); }
                    if (cc.Length > 0)
                        using (var fn = new Font("Consolas", S(8.5f)))
                        using (var br = new SolidBrush(Color.FromArgb(200, 215, 240)))
                        {
                            var sz = g.MeasureString(cc.ToString(), fn);
                            g.DrawString(cc.ToString(), fn, br, W - sz.Width - S(8), S(5));
                        }
                }

                if (app != null)
                {
                    string appName = app.DisplayName.ToUpperInvariant();
                    using (var fn = new Font("Consolas", S(12f), FontStyle.Bold))
                    using (var br = new SolidBrush(cAccYel))
                    {
                        var sz = g.MeasureString(appName, fn);
                        g.DrawString(appName, fn, br, W - sz.Width - S(8), S(20));
                    }
                }
                using (var p = new Pen(cBorder, 1))
                    g.DrawLine(p, 0, rTitle - 1, W, rTitle - 1);
            }

            if (app == null)
            {
                using (var p = new Pen(cBorder, 1))
                    g.DrawRectangle(p, 0, 0, W - 1, H - 1);
                return;
            }

            // ── Collect approach data ─────────────────────────────────────────────────
            double faCourse = 0;
            for (int i = app.Legs.Count - 1; i >= 0; i--)
                if (app.Legs[i].Course > 0) { faCourse = app.Legs[i].Course; break; }

            int fafIdxB = (app.FafIndex.HasValue
                           && app.FafIndex.Value < app.Legs.Count
                           && app.Legs[app.FafIndex.Value].VerticalAngle.HasValue)
                ? app.FafIndex.Value
                : FindFafFallback(app.Legs);
            while (fafIdxB < app.Legs.Count - 1 && app.Legs[fafIdxB].Type == "IF")
                fafIdxB++;
            int fafIdx = fafIdxB;
            string fafFix = fafIdx >= 0 && fafIdx < app.Legs.Count ? (app.Legs[fafIdx].Fix ?? "") : "";
            double fafAlt = fafIdx >= 0 && fafIdx < app.Legs.Count ? app.Legs[fafIdx].AltitudeFt : 0;
            double fafAgl = fafAlt > 0 ? fafAlt - rwyElev : 0;

            double? gsAngle = ils?.Glideslope?.PitchDeg;
            if (!gsAngle.HasValue && fafIdx >= 0 && fafIdx < app.Legs.Count && app.Legs[fafIdx].VerticalAngle.HasValue)
                gsAngle = Math.Abs(app.Legs[fafIdx].VerticalAngle.Value);

            double da    = FindDaMda(app.Legs);
            double daAgl = da > 0 ? da - rwyElev : 0;
            string daLabel = ils?.Glideslope != null ? "DA(H)" : "MDA(H)";

            string navLabel = app.Type ?? "";
            string navIdent = ils != null ? "ILS" : (app.Navaid?.Ident ?? "");
            string navFreq  = ils != null ? $"{ils.FrequencyMhz:F2}"
                            : app.Navaid?.FrequencyMhz.HasValue == true ? $"{app.Navaid.FrequencyMhz:F2}"
                            : app.Navaid?.FrequencyKhz.HasValue == true ? $"{app.Navaid.FrequencyKhz:F0} KHz"
                            : "";

            // ── Row 2: Frequencies ───────────────────────────────────────────────────
            {
                string[] freqPos = { "DEL", "GND", "TWR", "APP" };
                float fCellW = W / 4f;

                using (var p = new Pen(cBorder, 1))
                    g.DrawLine(p, 0, yFreq + rFreq - 1, W, yFreq + rFreq - 1);

                for (int pi = 0; pi < 4; pi++)
                {
                    string pos = freqPos[pi];
                    float  fcx = pi * fCellW;

                    if (pi > 0)
                        using (var p = new Pen(Color.FromArgb(14, 22, 38), 1))
                            g.DrawLine(p, fcx, yFreq, fcx, yFreq + rFreq - 1);

                    var online = _atcStations.FirstOrDefault(s =>
                        string.Equals(s.Position, pos, StringComparison.OrdinalIgnoreCase));
                    bool live = online != null;
                    _freqCache.TryGetValue(pos, out double cached);
                    double freq  = live ? online.Frequency : cached;
                    string fStr  = freq > 0 ? $"{freq:F3}" : "—";
                    Color fClr   = live   ? Color.FromArgb(55, 230, 85)
                                 : freq > 0 ? Color.FromArgb(110, 120, 145)
                                 : Color.FromArgb(65, 75, 95);
                    Color lblClr = live ? Color.FromArgb(70, 200, 95) : cHdr;

                    float ftx = fcx + S(7), fty = yFreq + S(5);
                    using (var fn = new Font("Consolas", S(7f)))
                    using (var br = new SolidBrush(lblClr))
                        g.DrawString(pos, fn, br, ftx, fty);
                    fty += S(13);
                    using (var fn = new Font("Consolas", S(9f), FontStyle.Bold))
                    using (var br = new SolidBrush(fClr))
                        g.DrawString(fStr, fn, br, ftx, fty);

                    if (live)
                        using (var br = new SolidBrush(Color.FromArgb(55, 230, 85)))
                            g.FillEllipse(br, fcx + fCellW - S(14), yFreq + S(6), S(5), S(5));
                }
            }

            // ── Row 3: Data cells ─────────────────────────────────────────────────────
            {
                float w0 = S(115), w1 = S(115), w2 = S(140), w3 = S(145);
                float w4 = Math.Max(S(90), W - w0 - w1 - w2 - w3);
                float[] cx2 = { 0, w0, w0 + w1, w0 + w1 + w2, w0 + w1 + w2 + w3 };

                using (var p = new Pen(cBorder, 1))
                {
                    g.DrawLine(p, 0, yData,      W, yData);
                    g.DrawLine(p, 0, yInfo - 1,  W, yInfo - 1);
                    for (int ci = 1; ci <= 4; ci++)
                        if (cx2[ci] < W - S(5))
                            g.DrawLine(p, cx2[ci], yData, cx2[ci], yInfo - 1);
                }

                // FAF cell subtle highlight
                using (var br = new SolidBrush(Color.FromArgb(28, 255, 215, 60)))
                    g.FillRectangle(br, cx2[2] + 1, yData + 1, w2 - 2, rData - 2);

                // DrawCell: hdr label + up to 3 value lines
                void Cell(float x, string hdr, string l1, Color c1, string l2, Color c2, string l3, Color c3)
                {
                    float tx = x + S(7), ty = yData + S(6);
                    using (var fn = new Font("Consolas", S(7.5f)))
                    using (var br = new SolidBrush(cHdr))
                        g.DrawString(hdr, fn, br, tx, ty);
                    ty += S(14);
                    if (!string.IsNullOrEmpty(l1))
                        using (var fn = new Font("Consolas", S(11f), FontStyle.Bold))
                        using (var br = new SolidBrush(c1))
                            g.DrawString(l1, fn, br, tx, ty);
                    ty += S(19);
                    if (!string.IsNullOrEmpty(l2))
                        using (var fn = new Font("Consolas", S(9f)))
                        using (var br = new SolidBrush(c2))
                            g.DrawString(l2, fn, br, tx, ty);
                    ty += S(14);
                    if (!string.IsNullOrEmpty(l3))
                        using (var fn = new Font("Consolas", S(8f)))
                        using (var br = new SolidBrush(c3))
                            g.DrawString(l3, fn, br, tx, ty);
                }

                Cell(cx2[0], navLabel,
                     string.IsNullOrEmpty(navFreq) ? "—" : navFreq, cVal,
                     navIdent, cHdr,
                     "", cHdr);

                Cell(cx2[1], "Final Apch Crs",
                     faCourse > 0 ? $"{faCourse:F0}°" : "—", cVal,
                     gsAngle.HasValue ? $"GS  {gsAngle:F2}°" : "", cAccGreen,
                     "", cHdr);

                Cell(cx2[2], "FAF",
                     fafFix, Color.White,
                     fafAlt > 0 ? $"{fafAlt:F0}'" : "—", cAccYel,
                     fafAgl > 0 ? $"({fafAgl:F0}' AGL)" : "", cHdr);

                Cell(cx2[3], daLabel,
                     da > 0 ? $"{da:F0}'" : "—", cAccRed,
                     daAgl > 0 ? $"({daAgl:F0}' AGL)" : "", Color.FromArgb(200, 150, 150),
                     "", cHdr);

                // Cell 4: Elevations (two rows)
                {
                    float tx = cx2[4] + S(7), ty = yData + S(6);
                    using (var fn = new Font("Consolas", S(7.5f)))
                    using (var br = new SolidBrush(cHdr))
                        g.DrawString("Apt Elev", fn, br, tx, ty);
                    ty += S(14);
                    using (var fn = new Font("Consolas", S(11f), FontStyle.Bold))
                    using (var br = new SolidBrush(cVal))
                        g.DrawString(info != null ? $"{info.ElevationFt:F0}'" : "—", fn, br, tx, ty);
                    ty += S(19);
                    if (rwy != null)
                    {
                        using (var fn = new Font("Consolas", S(7.5f)))
                        using (var br = new SolidBrush(cHdr))
                            g.DrawString($"Rwy {rwy.Name} Elev", fn, br, tx, ty);
                        ty += S(14);
                        using (var fn = new Font("Consolas", S(9f)))
                        using (var br = new SolidBrush(cVal))
                            g.DrawString($"{rwy.ElevationFt:F0}'", fn, br, tx, ty);
                    }
                }
            }

            // ── Row 3: Info bar (TA / TL / Rwy / AIRAC) ──────────────────────────────
            {
                using (var bg = new SolidBrush(Color.FromArgb(1, 2, 4)))
                    g.FillRectangle(bg, 0, yInfo, W, rInfo);

                float tx = S(10), ty = yInfo + S(7);
                using (var fn = new Font("Consolas", S(8.5f)))
                {
                    if (info?.TransitionAltitudeFt.HasValue == true)
                    {
                        using (var br = new SolidBrush(cHdr))   g.DrawString("TA", fn, br, tx, ty);
                        tx += S(22);
                        using (var br = new SolidBrush(cAccOrng))
                            g.DrawString($"{info.TransitionAltitudeFt:F0}'", fn, br, tx, ty);
                        tx += S(70);
                    }
                    if (info?.TransitionLevelFt.HasValue == true)
                    {
                        using (var br = new SolidBrush(cHdr))   g.DrawString("TL", fn, br, tx, ty);
                        tx += S(22);
                        using (var br = new SolidBrush(cAccOrng))
                            g.DrawString($"FL{info.TransitionLevelFt / 100:F0}", fn, br, tx, ty);
                        tx += S(55);
                    }
                    if (rwy != null)
                    {
                        using (var br = new SolidBrush(cHdr))   g.DrawString("Rwy", fn, br, tx, ty);
                        tx += S(30);
                        using (var br = new SolidBrush(cVal))
                            g.DrawString($"{rwy.Name}  {((int)(rwy.LengthFt / 100)) * 100}'", fn, br, tx, ty);
                    }
                    string airac = "AIRAC " + (NavDataClient.AiracCycle ?? "—");
                    using (var br = new SolidBrush(Color.FromArgb(90, 110, 140)))
                    {
                        var sz = g.MeasureString(airac, fn);
                        g.DrawString(airac, fn, br, W - sz.Width - S(10), ty);
                    }
                }
                using (var p = new Pen(cBorder, 1))
                    g.DrawLine(p, 0, yMissed - 1, W, yMissed - 1);
            }

            // ── Row 4: Missed approach ────────────────────────────────────────────────
            {
                using (var bg = new SolidBrush(Color.FromArgb(5, 4, 1)))
                    g.FillRectangle(bg, 0, yMissed, W, H - yMissed);

                float tx = S(10), ty = yMissed + S(8);
                using (var fnB = new Font("Consolas", S(8.5f), FontStyle.Bold))
                using (var fnR = new Font("Consolas", S(8.5f)))
                {
                    using (var br = new SolidBrush(Color.FromArgb(255, 200, 60)))
                        g.DrawString("MISSED APCH:", fnB, br, tx, ty);
                    tx += g.MeasureString("MISSED APCH:", fnB).Width + S(4);
                    using (var br = new SolidBrush(Color.FromArgb(210, 205, 175)))
                        g.DrawString(BuildMissedApchText(app), fnR, br, tx, ty);
                }
            }

            // Outer border
            using (var p = new Pen(cBorder, 1))
                g.DrawRectangle(p, 0, 0, W - 1, H - 1);
        }

        private static string BuildMissedApchText(NavApproach app)
        {
            if (app?.MissedLegs == null || app.MissedLegs.Count == 0)
                return "See procedure chart.";

            var sb = new System.Text.StringBuilder();
            double topAlt = 0;
            foreach (var leg in app.MissedLegs)
                if (leg.AltitudeFt > topAlt) topAlt = leg.AltitudeFt;

            if (topAlt > 0) sb.Append($"Climb to {topAlt:F0}'");

            var fixLegs = app.MissedLegs.Where(l => !string.IsNullOrEmpty(l.Fix)).ToList();
            if (fixLegs.Count > 0) sb.Append(sb.Length > 0 ? $", then to {fixLegs[0].Fix}" : $"To {fixLegs[0].Fix}");
            if (fixLegs.Count > 1) sb.Append($" via {fixLegs[1].Fix}");

            return sb.Length > 0 ? sb.ToString() + "." : "See procedure chart.";
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
            // IF legs are never the FAF; prefer the first non-IF leg with a vertical angle
            for (int i = 0; i < legs.Count; i++)
                if (legs[i].VerticalAngle.HasValue && legs[i].Type != "IF") return i;
            for (int i = 0; i < legs.Count; i++)
                if (legs[i].VerticalAngle.HasValue) return i;
            for (int i = legs.Count - 2; i >= 0; i--)
                if (legs[i].AltitudeFt > 0 && legs[i].FixType != "R" && legs[i].Type != "IF") return i;
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

        // ── ICAO prefix → country lookup ────────────────────────────────────────────

        private static string IcaoToCountry(string icao)
        {
            if (string.IsNullOrEmpty(icao) || icao.Length < 2) return null;
            var p2 = icao.Substring(0, 2).ToUpperInvariant();

            // Two-char prefix table (covers most of the world)
            switch (p2)
            {
                // ── North America ──
                case "MM": return "Mexico";
                case "MG": return "Guatemala";
                case "MH": return "Honduras";
                case "MS": return "El Salvador";
                case "MN": return "Nicaragua";
                case "MR": return "Costa Rica";
                case "MP": return "Panama";
                case "MK": return "Jamaica";
                case "MU": return "Cuba";
                case "MT": return "Haiti";
                case "MD": return "Dominican Republic";
                case "MW": return "Cayman Islands";
                case "TB": return "Barbados";
                case "TD": return "Dominica";
                case "TF": return "Martinique";
                case "TG": return "Grenada";
                case "TI": return "U.S. Virgin Islands";
                case "TJ": return "Puerto Rico";
                case "TK": return "Saint Kitts and Nevis";
                case "TL": return "Saint Lucia";
                case "TN": return "Aruba / Bonaire / Curaçao";
                case "TQ": return "Anguilla";
                case "TT": return "Trinidad and Tobago";
                case "TV": return "Saint Vincent";
                case "TX": return "Bermuda";
                case "PA": return "United States (Alaska)";
                case "PH": return "United States (Hawaii)";
                case "PG": return "Guam";
                // ── South America ──
                case "SA": return "Argentina";
                case "SB": case "SD": case "SI":
                case "SJ": case "SN": case "SS":
                case "SW": return "Brazil";
                case "SC": return "Chile";
                case "SE": return "Ecuador";
                case "SF": return "Falkland Islands";
                case "SG": return "Paraguay";
                case "SK": return "Colombia";
                case "SL": return "Bolivia";
                case "SM": return "Suriname";
                case "SO": return "French Guiana";
                case "SP": return "Peru";
                case "SU": return "Uruguay";
                case "SV": return "Venezuela";
                case "SY": return "Guyana";
                // ── Europe ──
                case "EB": return "Belgium";
                case "ED": case "ET": return "Germany";
                case "EE": return "Estonia";
                case "EF": return "Finland";
                case "EG": return "United Kingdom";
                case "EH": return "Netherlands";
                case "EI": return "Ireland";
                case "EK": return "Denmark";
                case "EL": return "Luxembourg";
                case "EN": return "Norway";
                case "EP": return "Poland";
                case "ES": return "Sweden";
                case "EV": return "Latvia";
                case "EY": return "Lithuania";
                case "LB": return "Bulgaria";
                case "LC": return "Cyprus";
                case "LD": return "Croatia";
                case "LE": return "Spain";
                case "LF": return "France";
                case "LG": return "Greece";
                case "LH": return "Hungary";
                case "LI": return "Italy";
                case "LJ": return "Slovenia";
                case "LK": return "Czech Republic";
                case "LL": return "Israel";
                case "LM": return "Malta";
                case "LN": return "Monaco";
                case "LO": return "Austria";
                case "LP": return "Portugal";
                case "LQ": return "Bosnia and Herzegovina";
                case "LR": return "Romania";
                case "LS": return "Switzerland";
                case "LT": return "Turkey";
                case "LU": return "Moldova";
                case "LW": return "North Macedonia";
                case "LX": return "Gibraltar";
                case "LY": return "Serbia";
                case "LZ": return "Slovakia";
                // ── Russia / CIS ──
                case "UA": return "Kazakhstan";
                case "UB": return "Azerbaijan";
                case "UC": return "Kyrgyzstan";
                case "UD": return "Armenia";
                case "UG": return "Georgia";
                case "UK": return "Ukraine";
                case "UM": return "Belarus";
                case "UT": return "Uzbekistan";
                // ── Middle East ──
                case "OA": return "Afghanistan";
                case "OB": return "Bahrain";
                case "OE": return "Saudi Arabia";
                case "OI": return "Iran";
                case "OJ": return "Jordan";
                case "OK": return "Kuwait";
                case "OL": return "Lebanon";
                case "OM": return "United Arab Emirates";
                case "OO": return "Oman";
                case "OP": return "Pakistan";
                case "OR": return "Iraq";
                case "OS": return "Syria";
                case "OT": return "Qatar";
                case "OY": return "Yemen";
                // ── Asia ──
                case "BG": return "Greenland";
                case "BI": return "Iceland";
                case "RJ": case "RO": return "Japan";
                case "RK": return "South Korea";
                case "RP": return "Philippines";
                case "VB": case "VY": return "Myanmar";
                case "VC": return "Sri Lanka";
                case "VD": return "Cambodia";
                case "VG": return "Bangladesh";
                case "VH": return "Hong Kong";
                case "VL": return "Laos";
                case "VM": return "Macau";
                case "VN": return "Nepal";
                case "VR": return "Maldives";
                case "VT": return "Thailand";
                case "VV": return "Vietnam";
                case "WB": return "Malaysia / Brunei";
                case "WM": return "Malaysia";
                case "WS": return "Singapore";
                case "WA": case "WI": case "WQ": case "WR": return "Indonesia";
                // ── China / Mongolia ──
                case "ZB": case "ZG": case "ZH": case "ZJ": case "ZL":
                case "ZP": case "ZS": case "ZT": case "ZU": case "ZW": case "ZY": return "China";
                case "ZK": return "North Korea";
                case "ZM": return "Mongolia";
                // ── Africa ──
                case "DA": return "Algeria";
                case "DG": return "Ghana";
                case "DI": return "Côte d'Ivoire";
                case "DN": return "Nigeria";
                case "DT": return "Tunisia";
                case "FA": return "South Africa";
                case "FE": return "Central African Republic";
                case "FK": return "Cameroon";
                case "FL": return "Zambia";
                case "FM": return "Madagascar";
                case "FN": return "Angola";
                case "FO": return "Gabon";
                case "FQ": return "Mozambique";
                case "FT": return "Chad";
                case "FV": return "Zimbabwe";
                case "FW": return "Malawi";
                case "FY": return "Namibia";
                case "FZ": return "DR Congo";
                case "GA": return "Mali";
                case "GB": return "Gambia";
                case "GC": return "Spain (Canary Islands)";
                case "GF": return "Sierra Leone";
                case "GL": return "Liberia";
                case "GM": return "Morocco";
                case "GO": return "Senegal";
                case "GQ": return "Mauritania";
                case "GU": return "Guinea";
                case "GV": return "Cape Verde";
                case "HA": return "Ethiopia";
                case "HE": return "Egypt";
                case "HH": return "Eritrea";
                case "HK": return "Kenya";
                case "HL": return "Libya";
                case "HR": return "Rwanda";
                case "HS": return "Sudan";
                case "HT": return "Tanzania";
                case "HU": return "Uganda";
                // ── Pacific / Oceania ──
                case "AG": return "Solomon Islands";
                case "AY": return "Papua New Guinea";
                case "NF": return "Fiji";
                case "NT": return "French Polynesia";
                case "NW": return "New Caledonia";
                case "NZ": return "New Zealand";
                case "NS": return "Samoa";
                case "NV": return "Vanuatu";
            }

            // Single-char prefix fallbacks
            char p1 = icao[0];
            switch (p1)
            {
                case 'K': return "United States";
                case 'C': return "Canada";
                case 'Y': return "Australia";
                case 'U': return "Russia";
                case 'Z': return "China";
                case 'V': return "India";
                case 'S': return "South America";
            }
            return null;
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
