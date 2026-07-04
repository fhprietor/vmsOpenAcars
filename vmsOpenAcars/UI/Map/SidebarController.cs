using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using vmsOpenAcars.Models.NavData;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI.Forms
{
    internal sealed class SidebarController
    {
        // ── Callbacks ────────────────────────────────────────────────────────────────
        private readonly Form   _owner;
        private readonly Action _redrawRoute;
        private readonly Action _clearApproachOverlay;
        private readonly Action<NavApproach, NavApproachTransition, NavRunway, NavIls> _drawApproachOverlay;
        private readonly Action _openApproachChart;

        // ── Sidebar panels / toggle ──────────────────────────────────────────────────
        internal Panel SidebarPanel    { get; private set; }
        private  Panel _sidebarContent;
        private  Button _btnToggle;
        private  bool   _expanded = true;

        // ── Combo boxes ──────────────────────────────────────────────────────────────
        private ComboBox _cmbOriginRwy, _cmbSid, _cmbSidTrans;
        private ComboBox _cmbDestRwy, _cmbStar, _cmbStarTrans, _cmbApproach, _cmbApproachTrans;

        // ── Labels ───────────────────────────────────────────────────────────────────
        private Label _lblOriginAirport, _lblDestAirport;
        private Label _lblOriginWind,    _lblDestWind, _lblApproachCount;

        // ── NavData cache ─────────────────────────────────────────────────────────────
        private List<NavRunway>    _sbOriginRunways, _sbDestRunways;
        private List<NavProcedure> _sbSids, _sbStars;
        private List<NavApproach>  _sbApproaches;
        private List<NavIls>       _sbIls;

        // ── METAR wind ───────────────────────────────────────────────────────────────
        private int? _metarOriginWindDir, _metarOriginWindSpd;
        private int? _metarDestWindDir,   _metarDestWindSpd;

        // ── Populate guard ────────────────────────────────────────────────────────────
        private bool _populating;

        // ── Selection backing fields (used with ref in FillXxxCombo) ──────────────────
        private string _selOriginRunway,       _selDestRunway;
        private string _selSidName,            _selSidTransition;
        private string _selStarName,           _selStarTransition;
        private string _selApproachKey,        _selApproachTransition;

        // ── Public read-only accessors ────────────────────────────────────────────────
        internal string SelOriginRunway       => _selOriginRunway;
        internal string SelDestRunway         => _selDestRunway;
        internal string SelSidName            => _selSidName;
        internal string SelSidTransition      => _selSidTransition;
        internal string SelStarName           => _selStarName;
        internal string SelStarTransition     => _selStarTransition;
        internal string SelApproachKey        => _selApproachKey;
        internal string SelApproachTransition => _selApproachTransition;

        // ─────────────────────────────────────────────────────────────────────────────

        internal SidebarController(
            Form owner,
            Action redrawRoute,
            Action clearApproachOverlay,
            Action<NavApproach, NavApproachTransition, NavRunway, NavIls> drawApproachOverlay,
            Action openApproachChart)
        {
            _owner                = owner;
            _redrawRoute          = redrawRoute;
            _clearApproachOverlay = clearApproachOverlay;
            _drawApproachOverlay  = drawApproachOverlay;
            _openApproachChart    = openApproachChart;
        }

        // ── Reset selections when route airports change ───────────────────────────────

        internal void ResetSelections(
            string originRunway, string destRunway,
            string sidName,      string starName)
        {
            _selOriginRunway       = originRunway;
            _selDestRunway         = destRunway;
            _selSidName            = sidName;
            _selSidTransition      = null;
            _selStarName           = starName;
            _selStarTransition     = null;
            _selApproachKey        = null;
            _selApproachTransition = null;
        }

        // ── Get the currently selected NavApproach ───────────────────────────────────

        internal NavApproach GetSelectedApproach()
        {
            if (_selApproachKey == null) return null;
            return _sbApproaches?.FirstOrDefault(a =>
                $"{a.Type}{a.Suffix ?? ""}_{a.Runway ?? ""}" == _selApproachKey);
        }

        // ── Build UI ──────────────────────────────────────────────────────────────────

        internal void Build()
        {
            const int W     = 230;
            const int TW    = 18;
            const int ItemW = W - TW - 12;

            SidebarPanel = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = W,
                BackColor = Color.FromArgb(14, 20, 28),
            };

            _btnToggle = new Button
            {
                Dock      = DockStyle.Right,
                Width     = TW,
                Text      = "◀",
                BackColor = Color.FromArgb(25, 35, 48),
                ForeColor = Color.FromArgb(120, 160, 200),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Consolas", 7, FontStyle.Bold),
                TabStop   = false,
            };
            _btnToggle.FlatAppearance.BorderSize = 0;
            _btnToggle.Click += (s, e) =>
            {
                _expanded               = !_expanded;
                _sidebarContent.Visible = _expanded;
                SidebarPanel.Width      = _expanded ? W : TW;
                _btnToggle.Text         = _expanded ? "◀" : "▶";
            };

            _sidebarContent = new Panel
            {
                Dock       = DockStyle.Fill,
                BackColor  = Color.FromArgb(14, 20, 28),
                AutoScroll = true,
            };

            int y = 8;
            Action<Control, int> place = (ctl, h) =>
            {
                ctl.Location = new Point(6, y);
                ctl.Width    = ItemW;
                if (h > 0) ctl.Height = h;
                _sidebarContent.Controls.Add(ctl);
                y += ctl.Height + 3;
            };

            var chartIcon = Properties.Resources.mapa;
            var chartTip  = new ToolTip { ShowAlways = true };
            Action<ComboBox, EventHandler, string> addChartIcon = (cmb, onClick, tip) =>
            {
                cmb.Width = ItemW - 24;
                var icon = new PictureBox
                {
                    Image     = chartIcon,
                    SizeMode  = PictureBoxSizeMode.Zoom,
                    Location  = new Point(6 + ItemW - 21, cmb.Top + 1),
                    Size      = new Size(20, 20),
                    BackColor = Color.FromArgb(14, 20, 28),
                    Cursor    = onClick != null ? Cursors.Hand : Cursors.Default,
                };
                if (onClick != null) icon.Click += onClick;
                if (!string.IsNullOrEmpty(tip)) chartTip.SetToolTip(icon, tip);
                _sidebarContent.Controls.Add(icon);
            };

            // ── ORIGIN ──────────────────────────────────────────────────────────────
            place(MakeSectionHeader("ORIGIN"), 18);
            _lblOriginAirport = new Label
            {
                Text = "—", ForeColor = Color.White,
                Font = new Font("Consolas", 8, FontStyle.Bold), AutoSize = false,
            };
            place(_lblOriginAirport, 16);

            _lblOriginWind = new Label
            {
                Text = "", ForeColor = Color.FromArgb(150, 200, 100),
                Font = new Font("Consolas", 7), AutoSize = false,
            };
            place(_lblOriginWind, 14);
            y += 2;

            place(MakeSideLabel("Runway"), 14);
            _cmbOriginRwy = MakeSideCombo();
            place(_cmbOriginRwy, 22);

            place(MakeSideLabel("SID"), 14);
            _cmbSid = MakeSideCombo();
            place(_cmbSid, 22);
            addChartIcon(_cmbSid, null, null);

            place(MakeSideLabel("Trans."), 14);
            _cmbSidTrans = MakeSideCombo();
            place(_cmbSidTrans, 22);

            y += 10;

            // ── DESTINATION ─────────────────────────────────────────────────────────
            place(MakeSectionHeader("DESTINATION"), 18);
            _lblDestAirport = new Label
            {
                Text = "—", ForeColor = Color.White,
                Font = new Font("Consolas", 8, FontStyle.Bold), AutoSize = false,
            };
            place(_lblDestAirport, 16);

            _lblDestWind = new Label
            {
                Text = "", ForeColor = Color.FromArgb(150, 200, 100),
                Font = new Font("Consolas", 7), AutoSize = false,
            };
            place(_lblDestWind, 14);
            y += 2;

            place(MakeSideLabel("Runway"), 14);
            _cmbDestRwy = MakeSideCombo();
            place(_cmbDestRwy, 22);

            place(MakeSideLabel("STAR"), 14);
            _cmbStar = MakeSideCombo();
            place(_cmbStar, 22);
            addChartIcon(_cmbStar, null, null);

            place(MakeSideLabel("Trans."), 14);
            _cmbStarTrans = MakeSideCombo();
            place(_cmbStarTrans, 22);

            place(MakeSideLabel("Approach"), 14);
            _cmbApproach = MakeSideCombo();
            place(_cmbApproach, 22);
            addChartIcon(_cmbApproach, (s, e) => _openApproachChart?.Invoke(),
                         "Open Approach Chart");

            place(MakeSideLabel("Trans."), 14);
            _cmbApproachTrans = MakeSideCombo();
            place(_cmbApproachTrans, 22);

            _lblApproachCount = new Label
            {
                Text = "", ForeColor = Color.FromArgb(130, 160, 195),
                Font = new Font("Consolas", 7), AutoSize = false,
            };
            place(_lblApproachCount, 14);

            _cmbOriginRwy.SelectedIndexChanged += OnOriginRunwayChanged;
            _cmbSid.SelectedIndexChanged       += OnSidChanged;
            _cmbSidTrans.SelectedIndexChanged  += OnSidTransChanged;
            _cmbDestRwy.SelectedIndexChanged   += OnDestRunwayChanged;
            _cmbStar.SelectedIndexChanged      += OnStarChanged;
            _cmbStarTrans.SelectedIndexChanged += OnStarTransChanged;
            _cmbApproach.SelectedIndexChanged      += OnApproachChanged;
            _cmbApproachTrans.SelectedIndexChanged += OnApproachTransChanged;

            SidebarPanel.Controls.Add(_sidebarContent);
            SidebarPanel.Controls.Add(_btnToggle);
        }

        // ── Populate ─────────────────────────────────────────────────────────────────

        internal void Populate(
            List<NavRunway>    originRunways, List<NavRunway>    destRunways,
            List<NavProcedure> sids,          List<NavProcedure> stars,
            List<NavApproach>  approaches,    List<NavIls>       ils,
            NavAirportInfo     originInfo,    NavAirportInfo     destInfo,
            string currentOriginIcao, string currentDestIcao)
        {
            if (SidebarPanel == null) return;

            _sbOriginRunways = originRunways;
            _sbDestRunways   = destRunways;
            _sbSids          = sids;
            _sbStars         = stars;
            _sbApproaches    = approaches;
            _sbIls           = ils;

            _populating = true;
            try
            {
                if (originInfo != null && !string.IsNullOrEmpty(currentOriginIcao))
                {
                    string n = originInfo.Name ?? "";
                    if (n.Length > 18) n = n.Substring(0, 18);
                    _lblOriginAirport.Text = $"{currentOriginIcao}  {n}";
                }
                else
                    _lblOriginAirport.Text = currentOriginIcao ?? "—";

                if (destInfo != null && !string.IsNullOrEmpty(currentDestIcao))
                {
                    string n = destInfo.Name ?? "";
                    if (n.Length > 18) n = n.Substring(0, 18);
                    _lblDestAirport.Text = $"{currentDestIcao}  {n}";
                }
                else
                    _lblDestAirport.Text = currentDestIcao ?? "—";

                FillRunwayCombo(_cmbOriginRwy, originRunways, ref _selOriginRunway);
                FillProcBaseCombo(_cmbSid, sids, _selOriginRunway, ref _selSidName);
                FillProcTransCombo(_cmbSidTrans, sids, _selSidName, ref _selSidTransition);

                FillRunwayCombo(_cmbDestRwy, destRunways, ref _selDestRunway);
                FillProcBaseCombo(_cmbStar, stars, _selDestRunway, ref _selStarName);
                FillProcTransCombo(_cmbStarTrans, stars, _selStarName, ref _selStarTransition);
                FillApproachCombo(_cmbApproach, approaches, _selDestRunway, ref _selApproachKey);
                var selApp = _selApproachKey == null ? null
                    : approaches?.FirstOrDefault(a =>
                        $"{a.Type}{a.Suffix ?? ""}_{a.Runway ?? ""}" == _selApproachKey);
                FillApproachTransCombo(_cmbApproachTrans, selApp, ref _selApproachTransition);

                int appCount = approaches?.Count(a => RunwayMatchesApproach(a, _selDestRunway)) ?? 0;
                _lblApproachCount.Text = appCount > 0
                    ? $"{appCount} approach{(appCount > 1 ? "es" : "")} available"
                    : "";

                UpdateWindLabel(_lblOriginWind, originRunways, _selOriginRunway,
                    _metarOriginWindDir, _metarOriginWindSpd);
                UpdateWindLabel(_lblDestWind, destRunways, _selDestRunway,
                    _metarDestWindDir, _metarDestWindSpd);
            }
            finally { _populating = false; }
        }

        // ── METAR wind update ────────────────────────────────────────────────────────

        internal void UpdateMetarWind(int? originDir, int? originSpd, int? destDir, int? destSpd)
        {
            _metarOriginWindDir = originDir;
            _metarOriginWindSpd = originSpd;
            _metarDestWindDir   = destDir;
            _metarDestWindSpd   = destSpd;
            if (SidebarPanel == null) return;
            UpdateWindLabel(_lblOriginWind, _sbOriginRunways, _selOriginRunway, originDir, originSpd);
            UpdateWindLabel(_lblDestWind,   _sbDestRunways,   _selDestRunway,   destDir,   destSpd);
        }

        // ── Combo event handlers ──────────────────────────────────────────────────────

        private void OnOriginRunwayChanged(object sender, EventArgs e)
        {
            if (_populating || _sbSids == null) return;
            string newRwy = SelectedRunwayName(_cmbOriginRwy);
            if (newRwy == _selOriginRunway) return;

            if (!string.IsNullOrEmpty(_selSidName) &&
                !GetProcBaseNames(_sbSids, newRwy)
                    .Contains(_selSidName, StringComparer.OrdinalIgnoreCase))
            {
                string msg = $"Changing to runway {newRwy ?? "(none)"} makes SID " +
                             $"[{_selSidName}] incompatible.\nClear SID and continue?";
                if (EcamDialog.Show(_owner, msg, "RUNWAY CHANGE", EcamDialogButtons.YesNo)
                    != DialogResult.Yes)
                {
                    _populating = true;
                    SelectOrDefault(_cmbOriginRwy, _selOriginRunway, 0);
                    _populating = false;
                    return;
                }
                _selSidName       = null;
                _selSidTransition = null;
            }

            _selOriginRunway = newRwy;
            _populating = true;
            FillProcBaseCombo(_cmbSid, _sbSids, _selOriginRunway, ref _selSidName);
            FillProcTransCombo(_cmbSidTrans, _sbSids, _selSidName, ref _selSidTransition);
            UpdateWindLabel(_lblOriginWind, _sbOriginRunways, _selOriginRunway,
                _metarOriginWindDir, _metarOriginWindSpd);
            _populating = false;
            _redrawRoute?.Invoke();
        }

        private void OnDestRunwayChanged(object sender, EventArgs e)
        {
            if (_populating || _sbStars == null) return;
            string newRwy = SelectedRunwayName(_cmbDestRwy);
            if (newRwy == _selDestRunway) return;

            if (!string.IsNullOrEmpty(_selStarName) &&
                !GetProcBaseNames(_sbStars, newRwy)
                    .Contains(_selStarName, StringComparer.OrdinalIgnoreCase))
            {
                string msg = $"Changing to runway {newRwy ?? "(none)"} makes STAR " +
                             $"[{_selStarName}] incompatible and clears the approach.\nContinue?";
                if (EcamDialog.Show(_owner, msg, "RUNWAY CHANGE", EcamDialogButtons.YesNo)
                    != DialogResult.Yes)
                {
                    _populating = true;
                    SelectOrDefault(_cmbDestRwy, _selDestRunway, 0);
                    _populating = false;
                    return;
                }
                _selStarName       = null;
                _selStarTransition = null;
            }

            _selDestRunway         = newRwy;
            _selApproachKey        = null;
            _selApproachTransition = null;
            _populating = true;
            FillProcBaseCombo(_cmbStar, _sbStars, _selDestRunway, ref _selStarName);
            FillProcTransCombo(_cmbStarTrans, _sbStars, _selStarName, ref _selStarTransition);
            FillApproachCombo(_cmbApproach, _sbApproaches, _selDestRunway, ref _selApproachKey);
            FillApproachTransCombo(_cmbApproachTrans, null, ref _selApproachTransition);
            int appCount = _sbApproaches?
                .Count(a => RunwayMatchesApproach(a, _selDestRunway)) ?? 0;
            _lblApproachCount.Text = appCount > 0
                ? $"{appCount} approach{(appCount > 1 ? "es" : "")} available"
                : "";
            UpdateWindLabel(_lblDestWind, _sbDestRunways, _selDestRunway,
                _metarDestWindDir, _metarDestWindSpd);
            _populating = false;
            _clearApproachOverlay?.Invoke();
            _redrawRoute?.Invoke();
        }

        private void OnSidChanged(object sender, EventArgs e)
        {
            if (_populating) return;
            string newSid = _cmbSid.SelectedIndex > 0 ? _cmbSid.SelectedItem as string : null;
            if (newSid == _selSidName) return;
            _selSidName       = newSid;
            _selSidTransition = null;
            _populating = true;
            FillProcTransCombo(_cmbSidTrans, _sbSids, _selSidName, ref _selSidTransition);
            var compatRwys = GetCompatibleRunways(_sbSids, _selSidName, _sbOriginRunways);
            FillRunwayCombo(_cmbOriginRwy, compatRwys, ref _selOriginRunway);
            UpdateWindLabel(_lblOriginWind, _sbOriginRunways, _selOriginRunway,
                _metarOriginWindDir, _metarOriginWindSpd);
            _populating = false;
            _redrawRoute?.Invoke();
        }

        private void OnSidTransChanged(object sender, EventArgs e)
        {
            if (_populating) return;
            string newTrans = _cmbSidTrans.SelectedIndex > 0
                ? _cmbSidTrans.SelectedItem as string : null;
            if (newTrans == _selSidTransition) return;
            _selSidTransition = newTrans;
            _redrawRoute?.Invoke();
        }

        private void OnStarChanged(object sender, EventArgs e)
        {
            if (_populating) return;
            string newStar = _cmbStar.SelectedIndex > 0 ? _cmbStar.SelectedItem as string : null;
            if (newStar == _selStarName) return;
            _selStarName           = newStar;
            _selStarTransition     = null;
            _populating = true;
            FillProcTransCombo(_cmbStarTrans, _sbStars, _selStarName, ref _selStarTransition);
            var compatRwys = GetCompatibleRunways(_sbStars, _selStarName, _sbDestRunways);
            FillRunwayCombo(_cmbDestRwy, compatRwys, ref _selDestRunway);
            _selApproachKey        = null;
            _selApproachTransition = null;
            FillApproachCombo(_cmbApproach, _sbApproaches, _selDestRunway, ref _selApproachKey);
            FillApproachTransCombo(_cmbApproachTrans, null, ref _selApproachTransition);
            int appCount = _sbApproaches?
                .Count(a => RunwayMatchesApproach(a, _selDestRunway)) ?? 0;
            _lblApproachCount.Text = appCount > 0
                ? $"{appCount} approach{(appCount > 1 ? "es" : "")} available"
                : "";
            UpdateWindLabel(_lblDestWind, _sbDestRunways, _selDestRunway,
                _metarDestWindDir, _metarDestWindSpd);
            _populating = false;
            _clearApproachOverlay?.Invoke();
            _redrawRoute?.Invoke();
        }

        private void OnStarTransChanged(object sender, EventArgs e)
        {
            if (_populating) return;
            string newTrans = _cmbStarTrans.SelectedIndex > 0
                ? _cmbStarTrans.SelectedItem as string : null;
            if (newTrans == _selStarTransition) return;
            _selStarTransition = newTrans;
            _redrawRoute?.Invoke();
        }

        private void OnApproachChanged(object sender, EventArgs e)
        {
            if (_populating) return;
            string newKey = (_cmbApproach.SelectedItem as ApproachItem)?.Key;
            if (string.IsNullOrEmpty(newKey)) newKey = null;
            if (newKey == _selApproachKey) return;
            _selApproachKey        = newKey;
            _selApproachTransition = null;

            _populating = true;
            var app = _selApproachKey == null ? null
                : _sbApproaches?.FirstOrDefault(a =>
                    $"{a.Type}{a.Suffix ?? ""}_{a.Runway ?? ""}" == _selApproachKey);
            FillApproachTransCombo(_cmbApproachTrans, app, ref _selApproachTransition);
            _populating = false;

            if (_selApproachKey == null) { _clearApproachOverlay?.Invoke(); return; }
            if (app == null) return;

            var destRwy = _sbDestRunways?.FirstOrDefault(r =>
                string.Equals(r.Name, _selDestRunway, StringComparison.OrdinalIgnoreCase));
            var ils = _sbIls?.FirstOrDefault(i =>
                string.Equals(i.Runway, _selDestRunway, StringComparison.OrdinalIgnoreCase));
            _drawApproachOverlay?.Invoke(app, null, destRwy, ils);
        }

        private void OnApproachTransChanged(object sender, EventArgs e)
        {
            if (_populating) return;
            string newTrans = _cmbApproachTrans.SelectedIndex > 0
                ? _cmbApproachTrans.SelectedItem as string : null;
            if (newTrans == _selApproachTransition) return;
            _selApproachTransition = newTrans;

            var app = _selApproachKey == null ? null
                : _sbApproaches?.FirstOrDefault(a =>
                    $"{a.Type}{a.Suffix ?? ""}_{a.Runway ?? ""}" == _selApproachKey);
            if (app == null) return;

            var trans = string.IsNullOrEmpty(_selApproachTransition) ? null
                : app.Transitions?.FirstOrDefault(t =>
                    string.Equals(t.Fix, _selApproachTransition,
                        StringComparison.OrdinalIgnoreCase));
            var destRwy = _sbDestRunways?.FirstOrDefault(r =>
                string.Equals(r.Name, _selDestRunway, StringComparison.OrdinalIgnoreCase));
            var ils = _sbIls?.FirstOrDefault(i =>
                string.Equals(i.Runway, _selDestRunway, StringComparison.OrdinalIgnoreCase));
            _drawApproachOverlay?.Invoke(app, trans, destRwy, ils);
        }

        // ── Static combo fill helpers ─────────────────────────────────────────────────

        private sealed class ApproachItem
        {
            public string Key   { get; }
            public string Label { get; }
            public ApproachItem(string key, string label) { Key = key; Label = label; }
            public override string ToString() => Label;
        }

        private static void FillRunwayCombo(
            ComboBox cmb, List<NavRunway> runways, ref string selection)
        {
            string cur = selection;
            cmb.Items.Clear();
            cmb.Items.Add("(none)");
            if (runways != null)
                foreach (var r in runways.OrderBy(r => r.Name))
                    cmb.Items.Add(r.Name);
            SelectOrDefault(cmb, cur, 0);
            selection = SelectedRunwayName(cmb);
        }

        private static IEnumerable<string> GetProcBaseNames(
            IEnumerable<NavProcedure> procs, string runwayFilter)
        {
            return (procs ?? Enumerable.Empty<NavProcedure>())
                .Where(p => string.IsNullOrEmpty(runwayFilter)
                         || string.IsNullOrEmpty(p.Runway)
                         || ProcedureAppliesToRunway(p.Runway, runwayFilter))
                .Select(p =>
                {
                    int dot = p.Name.IndexOf('.');
                    return dot > 0 ? p.Name.Substring(0, dot) : p.Name;
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n);
        }

        private static void FillProcBaseCombo(
            ComboBox cmb, List<NavProcedure> procs, string runwayFilter, ref string selection)
        {
            string cur = selection;
            cmb.Items.Clear();
            cmb.Items.Add("(none)");
            foreach (var n in GetProcBaseNames(procs, runwayFilter))
                cmb.Items.Add(n);
            SelectOrDefault(cmb, cur, 0);
            selection = cmb.SelectedIndex > 0 ? cmb.SelectedItem as string : null;
        }

        private static void FillProcTransCombo(
            ComboBox cmb, List<NavProcedure> procs, string baseName, ref string selection)
        {
            string cur = selection;
            cmb.Items.Clear();
            cmb.Items.Add("Direct");

            if (!string.IsNullOrEmpty(baseName) && procs != null)
            {
                var trans = procs
                    .Where(p =>
                    {
                        int dot = p.Name.IndexOf('.');
                        string bn = dot > 0 ? p.Name.Substring(0, dot) : p.Name;
                        return string.Equals(bn, baseName, StringComparison.OrdinalIgnoreCase)
                            && dot > 0;
                    })
                    .Select(p => p.Name.Substring(p.Name.IndexOf('.') + 1))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(t => t);

                foreach (var t in trans)
                    cmb.Items.Add(t);
            }

            SelectOrDefault(cmb, cur, 0);
            selection = cmb.SelectedIndex > 0 ? cmb.SelectedItem as string : null;
        }

        private static bool RunwayMatchesApproach(NavApproach app, string runway)
        {
            if (string.IsNullOrEmpty(runway) || string.IsNullOrEmpty(app.Runway)) return true;
            if (string.Equals(app.Runway, runway, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = runway.TrimEnd('L', 'R', 'C');
            return string.Equals(app.Runway, prefix + "B", StringComparison.OrdinalIgnoreCase);
        }

        private static void FillApproachCombo(
            ComboBox cmb, List<NavApproach> approaches, string runway, ref string selection)
        {
            string cur = selection;
            cmb.Items.Clear();
            cmb.Items.Add(new ApproachItem("", "(none)"));

            if (approaches != null)
            {
                foreach (var a in approaches
                    .Where(a => RunwayMatchesApproach(a, runway))
                    .OrderBy(a => a.Runway ?? "").ThenBy(a => a.Type).ThenBy(a => a.Suffix ?? ""))
                {
                    string key   = $"{a.Type}{a.Suffix ?? ""}_{a.Runway ?? ""}";
                    string label = string.IsNullOrEmpty(a.Suffix)
                        ? $"{a.Type} {a.Runway}"
                        : $"{a.Type} {a.Suffix} {a.Runway}";
                    cmb.Items.Add(new ApproachItem(key, label));
                }
            }

            if (!string.IsNullOrEmpty(cur))
                for (int i = 1; i < cmb.Items.Count; i++)
                    if ((cmb.Items[i] as ApproachItem)?.Key == cur)
                    { cmb.SelectedIndex = i; return; }
            cmb.SelectedIndex = 0;
            selection = null;
        }

        private static void FillApproachTransCombo(
            ComboBox cmb, NavApproach approach, ref string selection)
        {
            string cur = selection;
            cmb.Items.Clear();
            cmb.Items.Add("(none)");

            if (approach?.Transitions != null)
                foreach (var t in approach.Transitions
                    .Where(t => !string.IsNullOrEmpty(t.Fix))
                    .OrderBy(t => t.Fix))
                    cmb.Items.Add(t.Fix);

            if (!string.IsNullOrEmpty(cur))
                for (int i = 1; i < cmb.Items.Count; i++)
                    if (string.Equals(cmb.Items[i] as string, cur,
                            StringComparison.OrdinalIgnoreCase))
                    { cmb.SelectedIndex = i; return; }
            cmb.SelectedIndex = 0;
            selection = null;
        }

        private static void SelectOrDefault(ComboBox cmb, string value, int defaultIndex)
        {
            if (!string.IsNullOrEmpty(value))
                for (int i = 0; i < cmb.Items.Count; i++)
                    if (string.Equals(cmb.Items[i]?.ToString(), value,
                            StringComparison.OrdinalIgnoreCase))
                    { cmb.SelectedIndex = i; return; }
            cmb.SelectedIndex = defaultIndex;
        }

        private static string SelectedRunwayName(ComboBox cmb)
            => cmb.SelectedIndex > 0 ? cmb.SelectedItem as string : null;

        private static List<NavRunway> GetCompatibleRunways(
            List<NavProcedure> procs, string procName, List<NavRunway> allRunways)
        {
            if (string.IsNullOrEmpty(procName) || allRunways == null) return allRunways;

            var constraints = (procs ?? Enumerable.Empty<NavProcedure>())
                .Where(p =>
                {
                    int dot = p.Name.IndexOf('.');
                    string bn = dot > 0 ? p.Name.Substring(0, dot) : p.Name;
                    return string.Equals(bn, procName, StringComparison.OrdinalIgnoreCase);
                })
                .Select(p => p.Runway)
                .ToList();

            if (constraints.All(r => string.IsNullOrEmpty(r))) return allRunways;

            return allRunways
                .Where(r => constraints.Any(pr => ProcedureAppliesToRunway(pr, r.Name)))
                .ToList();
        }

        private static bool ProcedureAppliesToRunway(string procRunway, string runway)
        {
            if (string.IsNullOrEmpty(procRunway)) return true;
            if (string.Equals(procRunway, runway, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = runway.TrimEnd('L', 'R', 'C');
            return string.Equals(procRunway, prefix + "B", StringComparison.OrdinalIgnoreCase);
        }

        // ── Wind chip ──────────────────────────────────────────────────────────────────

        private static void UpdateWindLabel(
            Label lbl, List<NavRunway> runways, string runwayName,
            int? windDir, int? windSpd)
        {
            if (windDir == null || windSpd == null || windSpd == 0
                || string.IsNullOrEmpty(runwayName) || runways == null)
            {
                lbl.Text = ""; return;
            }
            var rwy = runways.FirstOrDefault(r =>
                string.Equals(r.Name, runwayName, StringComparison.OrdinalIgnoreCase));
            if (rwy == null) { lbl.Text = ""; return; }

            double rwyCourse = GeodesicBearing(
                rwy.ThresholdLat, rwy.ThresholdLon, rwy.EndLat, rwy.EndLon);
            double angle = (windDir.Value - rwyCourse + 360) % 360;
            double hw    = Math.Cos(angle * Math.PI / 180) * windSpd.Value;
            double xw    = Math.Sin(angle * Math.PI / 180) * windSpd.Value;

            string hwStr = hw >= 0
                ? $"HW {(int)Math.Round(Math.Abs(hw))}kt"
                : $"TW {(int)Math.Round(Math.Abs(hw))}kt";
            lbl.Text = $"{hwStr}  XW {(int)Math.Round(Math.Abs(xw))}kt";
        }

        private static double GeodesicBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double phi1 = lat1 * Math.PI / 180, phi2 = lat2 * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;
            double y    = Math.Sin(dLon) * Math.Cos(phi2);
            double x    = Math.Cos(phi1) * Math.Sin(phi2)
                        - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLon);
            return ((Math.Atan2(y, x) * 180 / Math.PI) + 360) % 360;
        }

        // ── UI factory helpers ────────────────────────────────────────────────────────

        private static Label MakeSectionHeader(string text) => new Label
        {
            Text      = text,
            ForeColor = Color.FromArgb(0, 180, 255),
            Font      = new Font("Consolas", 8, FontStyle.Bold),
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        private static Label MakeSideLabel(string text) => new Label
        {
            Text      = text,
            ForeColor = Color.FromArgb(140, 160, 180),
            Font      = new Font("Consolas", 7),
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        private static ComboBox MakeSideCombo() => new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.FromArgb(25, 35, 48),
            ForeColor     = Color.White,
            Font          = new Font("Consolas", 8),
        };
    }
}
