using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.Projections;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Models.NavData;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI.Forms
{
    public class MapForm : Form
    {
        private GMapControl        _map;
        private MapOverlayManager  _overlayManager;
        private MapRouteController _routeController;
        private Label          _lblStatus;
        private CheckBox       _chkFollow;
        private ComboBox       _cmbProvider;
        private bool           _dragging;
        private Point          _dragStart;
        private SpinnerOverlay _spinner;
        private ToolTip        _atcToolTip;

        // ── Layer toggles ─────────────────────────────────────────────────────────
        private CheckBox     _chkLayerTiles;
        private CheckBox     _chkLayerRoute;
        private CheckBox     _chkLayerSpaces;
        private CheckBox     _chkLayerIvao;
        private GMapProvider _savedProvider;

        // ── Sidebar ───────────────────────────────────────────────────────────────
        private SidebarController _sidebar;

        private IList<SimbriefWaypoint> _currentWaypoints;
        private string _currentOriginIcao, _currentDestIcao, _currentAltIcao;

        public event Action<string, string, string, string> OnProcedureChanged;

        public MapForm()
        {
            Text            = "vmsOpenAcars — MAP";
            Size            = new Size(920, 660);
            MinimumSize     = new Size(600, 420);
            BackColor       = Color.FromArgb(15, 20, 25);
            ForeColor       = Color.White;
            Font            = new Font("Consolas", 9);
            StartPosition   = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            ResizeRedraw    = true;

            Paint += (s, e) =>
            {
                if (WindowState == FormWindowState.Normal)
                    e.Graphics.DrawRectangle(
                        new Pen(Color.FromArgb(60, 80, 100), 2),
                        1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
            };

            Padding = new Padding(6);

            BuildLayout();
            InitMap();

            // Icono de barra de tareas — mismo logo.png que MainForm
            try
            {
                string iconPath = Path.Combine(Application.StartupPath, "logo.png");
                if (File.Exists(iconPath))
                {
                    using (Bitmap bitmap = new Bitmap(iconPath))
                    {
                        IntPtr hIcon = bitmap.GetHicon();
                        this.Icon = Icon.FromHandle(hIcon);
                    }
                }
            }
            catch { }

            _spinner = new SpinnerOverlay();
            Controls.Add(_spinner);
            Resize += (s, e) => CenterSpinner();
            Load   += (s, e) => CenterSpinner();
        }

        private void CenterSpinner()
        {
            if (_map == null || _spinner == null) return;
            _spinner.Location = new Point(
                _map.Left + (_map.Width  - _spinner.Width)  / 2,
                _map.Top  + (_map.Height - _spinner.Height) / 2);
        }

        // ── Layout ────────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            // ── Title bar ────────────────────────────────────────────────────────
            var titleBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 35,
                BackColor = Color.FromArgb(20, 28, 36),
            };
            var lblTitle = new Label
            {
                Text      = "vmsOpenAcars — MAP",
                ForeColor = Color.Cyan,
                Font      = new Font("Consolas", 9, FontStyle.Bold),
                Location  = new Point(10, 9),
                AutoSize  = true,
            };
            var btnClose = MakeTitleBtn("✕", Color.FromArgb(110, 20, 20));
            var btnMax   = MakeTitleBtn("□", Color.FromArgb(40, 55, 70));
            var btnMin   = MakeTitleBtn("─", Color.FromArgb(40, 55, 70));

            btnClose.Click += (s, e) => Close();
            btnMin.Click   += (s, e) => WindowState = FormWindowState.Minimized;
            btnMax.Click   += (s, e) =>
            {
                WindowState  = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
                btnMax.Text  = WindowState == FormWindowState.Maximized ? "❐" : "□";
            };
            SizeChanged += (s, e) =>
                btnMax.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";

            // Posicionamiento derecha — se recalcula al redimensionar
            Action reposButtons = () =>
            {
                int r = titleBar.Width - 4;
                btnClose.Location = new Point(r - 30, 5); r -= 34;
                btnMax.Location   = new Point(r - 30, 5); r -= 34;
                btnMin.Location   = new Point(r - 30, 5);
            };
            titleBar.Resize += (s, e) => reposButtons();
            Load            += (s, e) => reposButtons();

            // Drag para mover la ventana
            MouseEventHandler onDown = (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                _dragging  = true;
                _dragStart = ((Control)s).PointToScreen(e.Location);
            };
            MouseEventHandler onMove = (s, e) =>
            {
                if (!_dragging) return;
                var cur  = ((Control)s).PointToScreen(e.Location);
                Location = new Point(Location.X + cur.X - _dragStart.X,
                                     Location.Y + cur.Y - _dragStart.Y);
                _dragStart = cur;
            };
            MouseEventHandler onUp = (s, e) => _dragging = false;

            foreach (Control ctl in new Control[] { titleBar, lblTitle })
            {
                ctl.MouseDown += onDown;
                ctl.MouseMove += onMove;
                ctl.MouseUp   += onUp;
            }
            titleBar.DoubleClick += (s, e) =>
            {
                WindowState  = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
                btnMax.Text  = WindowState == FormWindowState.Maximized ? "❐" : "□";
            };

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);
            titleBar.Controls.Add(btnMax);
            titleBar.Controls.Add(btnMin);

            // ── Status bar ───────────────────────────────────────────────────────
            var bar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 34,
                BackColor = Color.FromArgb(20, 28, 36),
                Padding   = new Padding(4, 0, 4, 0),
            };

            _lblStatus = new Label
            {
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(150, 195, 220),
                Font      = new Font("Consolas", 8),
                Text      = "  Waiting for simulator...",
            };

            _chkFollow = new CheckBox
            {
                Text      = "FOLLOW",
                Checked   = true,
                Dock      = DockStyle.Right,
                Width     = 80,
                ForeColor = Color.FromArgb(0, 180, 255),
                Font      = new Font("Consolas", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _chkFollow.CheckedChanged += (s, e) => { if (_routeController != null) _routeController.FollowAircraft = _chkFollow.Checked; };

            _cmbProvider = new ComboBox
            {
                Dock          = DockStyle.Right,
                Width         = 155,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Color.FromArgb(30, 40, 50),
                ForeColor     = Color.White,
                Font          = new Font("Consolas", 8),
            };
            _cmbProvider.Items.AddRange(new object[] { "Street (Carto)", "Dark (Carto)", "Satellite (ESRI)" });
            _cmbProvider.SelectedIndex = LoadMapProviderIndex();
            _cmbProvider.SelectedIndexChanged += OnProviderChanged;

            var btnZoomIn  = MakeZoomBtn("+");
            var btnZoomOut = MakeZoomBtn("−");
            btnZoomIn.Dock  = DockStyle.Right;
            btnZoomOut.Dock = DockStyle.Right;
            btnZoomIn.Click  += (s, e) => { if (_map.Zoom < _map.MaxZoom) _map.Zoom++; };
            btnZoomOut.Click += (s, e) => { if (_map.Zoom > _map.MinZoom) _map.Zoom--; };

            _chkLayerTiles  = MakeLayerChk("TILES");
            _chkLayerRoute  = MakeLayerChk("ROUTE");
            _chkLayerSpaces = MakeLayerChk("SPACES");
            _chkLayerIvao   = MakeLayerChk("IVAO");

            _chkLayerTiles.CheckedChanged += (s, e) =>
            {
                if (_chkLayerTiles.Checked)
                    _map.MapProvider = _savedProvider ?? ProviderForIndex(LoadMapProviderIndex());
                else
                {
                    _savedProvider   = _map.MapProvider;
                    _map.MapProvider = GMap.NET.MapProviders.EmptyProvider.Instance;
                }
                _map.Refresh();
            };
            _chkLayerRoute.CheckedChanged += (s, e) =>
            {
                if (_routeController != null) _routeController.RouteLayerVisible = _chkLayerRoute.Checked;
                _map.Refresh();
            };
            _chkLayerSpaces.CheckedChanged += (s, e) =>
            {
                _overlayManager.AirspaceOverlay.IsVisibile = _chkLayerSpaces.Checked;
                _map.Refresh();
            };
            _chkLayerIvao.CheckedChanged += (s, e) =>
            {
                _overlayManager.AtcOverlay.IsVisibile = _chkLayerIvao.Checked;
                _map.Refresh();
            };

            bar.Controls.Add(_chkFollow);
            bar.Controls.Add(_cmbProvider);
            bar.Controls.Add(btnZoomOut);
            bar.Controls.Add(btnZoomIn);
            // Layer toggles: added in reverse visual order (right→left)
            bar.Controls.Add(_chkLayerIvao);
            bar.Controls.Add(_chkLayerSpaces);
            bar.Controls.Add(_chkLayerRoute);
            bar.Controls.Add(_chkLayerTiles);
            // Fill label added last → takes remaining space after Right controls
            bar.Controls.Add(_lblStatus);

            _map = new GMapControl { Dock = DockStyle.Fill };

            Controls.Add(_map);
            Controls.Add(bar);
            _sidebar = new SidebarController(
                this, RedrawRoute, ClearApproachOverlay, DrawApproachOverlay, OpenApproachChart);
            _sidebar.Build();
            Controls.Add(_sidebar.SidebarPanel);
            Controls.Add(titleBar);   // Top — mayor prioridad de docking
        }

        private static Button MakeTitleBtn(string text, Color backColor)
        {
            var btn = new Button
            {
                Text      = text,
                Size      = new Size(30, 25),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 11, FontStyle.Bold),
                TabStop   = false,
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private static Button MakeZoomBtn(string text) => new Button
        {
            Text      = text,
            Width     = 32,
            BackColor = Color.FromArgb(40, 55, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 13, FontStyle.Bold),
        };

        private static CheckBox MakeLayerChk(string text) => new CheckBox
        {
            Text      = text,
            Checked   = true,
            Dock      = DockStyle.Right,
            Width     = 62,
            ForeColor = Color.FromArgb(120, 165, 190),
            Font      = new Font("Consolas", 7, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        // ── GMap initialization ───────────────────────────────────────────────────

        private void InitMap()
        {
            GMaps.Instance.Mode = AccessMode.ServerAndCache;

            // Required by OSM and most CDN-backed tile servers
            GMapProvider.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0";

            _map.MapProvider = ProviderForIndex(LoadMapProviderIndex());
            _map.MinZoom     = 2;
            _map.MaxZoom     = 19;
            _map.Zoom        = 14;
            _map.ShowCenter  = false;
            _map.DragButton  = MouseButtons.Left;
            _map.BackColor   = Color.FromArgb(30, 40, 50);

            _overlayManager  = new MapOverlayManager(_map);
            _routeController = new MapRouteController(_map, _spinner, _lblStatus);

            _map.Overlays.Add(_overlayManager.AirspaceOverlay);
            _map.Overlays.Add(_routeController.RouteShadowOverlay);
            _map.Overlays.Add(_routeController.RouteOverlay);
            _map.Overlays.Add(_routeController.AmbientOverlay);
            _map.Overlays.Add(_routeController.WaypointOverlay);
            _map.Overlays.Add(_overlayManager.AtcOverlay);
            _map.Overlays.Add(_routeController.ApproachOverlay);
            _map.Overlays.Add(_routeController.AircraftOverlay);

            _map.OnMapZoomChanged += () => UpdateZoomInStatus();

            _atcToolTip = new ToolTip
            {
                AutoPopDelay = 0,
                InitialDelay = 0,
                ReshowDelay  = 0,
                BackColor    = Color.FromArgb(15, 22, 35),
                ForeColor    = Color.FromArgb(210, 220, 235),
                IsBalloon    = false,
            };
            _map.OnMarkerEnter += m =>
            {
                if (!(m is AtcLabelMarker lbl)) return;
                var pt = _map.PointToClient(Cursor.Position);
                _atcToolTip.Show(lbl.TooltipContent, _map, pt.X + 14, pt.Y + 14, int.MaxValue);
            };
            _map.OnMarkerLeave += m =>
            {
                if (m is AtcLabelMarker) _atcToolTip.Hide(_map);
            };
        }

        private void UpdateZoomInStatus()
        {
            if (_lblStatus.IsDisposed) return;
            string t = _lblStatus.Text;
            int zIdx = t.IndexOf("  Z:");
            if (zIdx >= 0) t = t.Substring(0, zIdx);
            _lblStatus.Text = t + $"  Z:{(int)_map.Zoom}";
            _routeController?.UpdateAmbientVisibility((int)_map.Zoom);
        }

        private void OnProviderChanged(object sender, EventArgs e)
        {
            _map.MapProvider = ProviderForIndex(_cmbProvider.SelectedIndex);
            SaveMapProviderPref(_cmbProvider.SelectedIndex);
        }

        private static int LoadMapProviderIndex()
        {
            if (int.TryParse(
                    System.Configuration.ConfigurationManager.AppSettings["map_provider_index"],
                    out int stored) && stored >= 0 && stored <= 2)
                return stored;
            return 1;   // default: Dark (Carto)
        }

        private static GMapProvider ProviderForIndex(int index)
        {
            switch (index)
            {
                case 0:  return CartoLightProvider.Instance;
                case 2:  return EsriSatelliteProvider.Instance;
                default: return CartoDarkProvider.Instance;   // 1 or any unknown
            }
        }

        private static void SaveMapProviderPref(int index)
        {
            try
            {
                var config = System.Configuration.ConfigurationManager
                    .OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                var settings = config.AppSettings.Settings;
                const string key = "map_provider_index";
                if (settings[key] != null) settings[key].Value = index.ToString();
                else                       settings.Add(key, index.ToString());
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                System.Configuration.ConfigurationManager.RefreshSection("appSettings");
            }
            catch { /* non-critical */ }
        }

        // ── Resize borderless ─────────────────────────────────────────────────────
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST    = 0x0084;
            const int HTLEFT          = 10, HTRIGHT    = 11;
            const int HTTOP           = 12, HTTOPLEFT  = 13, HTTOPRIGHT = 14;
            const int HTBOTTOM        = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
            const int Border          = 6;

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                var p = PointToClient(new Point(
                    (int)(m.LParam.ToInt64() & 0xFFFF),
                    (int)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
                bool l = p.X < Border, r = p.X >= ClientSize.Width  - Border;
                bool t = p.Y < Border, b = p.Y >= ClientSize.Height - Border;
                if (t && l)  { m.Result = (IntPtr)HTTOPLEFT;     return; }
                if (t && r)  { m.Result = (IntPtr)HTTOPRIGHT;    return; }
                if (b && l)  { m.Result = (IntPtr)HTBOTTOMLEFT;  return; }
                if (b && r)  { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                if (l)       { m.Result = (IntPtr)HTLEFT;        return; }
                if (r)       { m.Result = (IntPtr)HTRIGHT;       return; }
                if (t)       { m.Result = (IntPtr)HTTOP;         return; }
                if (b)       { m.Result = (IntPtr)HTBOTTOM;      return; }
            }
            base.WndProc(ref m);
        }


        public void LoadRoute(IList<SimbriefWaypoint> waypoints,
            string originIcao = null, string originRunway = null,
            string destIcao   = null, string destRunway   = null,
            string altIcao    = null,
            string sidName    = null, string starName     = null)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (waypoints == null || waypoints.Count < 2) return;

            bool airportChanged = originIcao != _currentOriginIcao || destIcao != _currentDestIcao;
            _currentWaypoints  = waypoints;
            _currentOriginIcao = originIcao;
            _currentDestIcao   = destIcao;
            _currentAltIcao    = altIcao;
            if (airportChanged)
                _sidebar.ResetSelections(originRunway, destRunway, sidName, starName);

            _routeController.LoadRoute(
                waypoints,
                originIcao, _sidebar.SelOriginRunway,
                destIcao,   _sidebar.SelDestRunway,
                altIcao,
                _sidebar.SelSidName, _sidebar.SelStarName,
                result =>
                {
                    if (!IsDisposed)
                        _sidebar.Populate(
                            result.OriginRunways, result.DestRunways,
                            result.Sids, result.Stars, result.Approaches, result.Ils,
                            result.OriginInfo, result.DestInfo,
                            _currentOriginIcao, _currentDestIcao);
                });
        }



        // ── Center on airport ─────────────────────────────────────────────────────

        public void CenterOnAirport(string icao)
        {
            if (string.IsNullOrEmpty(icao) || IsDisposed || !IsHandleCreated) return;
            System.Threading.Tasks.Task.Run(() =>
            {
                NavDataClient.PrefetchAirport(icao);
                var runways = NavDataClient.GetRunways(icao);
                if (runways?.Count > 0)
                {
                    double lat = runways.Average(r => (r.ThresholdLat + r.EndLat) / 2.0);
                    double lon = runways.Average(r => (r.ThresholdLon + r.EndLon) / 2.0);
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke((Action)(() =>
                    {
                        if (IsDisposed) return;
                        _map.Position = new PointLatLng(lat, lon);
                        _map.Zoom = 13;
                    }));
                }
            });
        }

        // ── Position update ───────────────────────────────────────────────────────

        public void UpdatePosition(double lat, double lon, double heading)
            => _routeController.UpdatePosition(lat, lon, heading);

        // ── Approach overlay ─────────────────────────────────────────────────────────

        private void ClearApproachOverlay() => _routeController.ClearApproachOverlay();

        private void DrawApproachOverlay(
            NavApproach app, NavApproachTransition trans, NavRunway rwy, NavIls ils)
            => _routeController.DrawApproachOverlay(app, trans, rwy, ils);

        private void OpenApproachChart()
        {
            if (string.IsNullOrEmpty(_currentDestIcao)) return;
            new ApproachChartForm(
                _currentDestIcao,
                _sidebar.GetSelectedApproach(),
                _overlayManager.LastAtcStations).Show(this);
        }

        internal void SetAirspaces(IList<NavAirspace> airspaces) =>
            _overlayManager.SetAirspaces(airspaces);

        internal void SetAircraftCategory(FsuipcService.AircraftCategory cat)
            => _routeController.SetAircraftCategory(cat);

        internal void SetAtcStations(IList<IvaoAtcStation> stations) =>
            _overlayManager.SetAtcStations(stations);

        public void SetMetarData(int? originWindDir, int? originWindSpeedKt,
                                 int? destWindDir,   int? destWindSpeedKt)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed) return;
                _sidebar.UpdateMetarWind(
                    originWindDir, originWindSpeedKt, destWindDir, destWindSpeedKt);
            }));
        }

        private void RedrawRoute()
        {
            if (_currentWaypoints == null) return;
            OnProcedureChanged?.Invoke(
                _sidebar.SelOriginRunway, _sidebar.SelSidName,
                _sidebar.SelDestRunway,   _sidebar.SelStarName);
            LoadRoute(_currentWaypoints,
                _currentOriginIcao, _sidebar.SelOriginRunway,
                _currentDestIcao,   _sidebar.SelDestRunway,
                _currentAltIcao,
                _sidebar.SelSidName, _sidebar.SelStarName);
        }

    }

    // ── Custom tile providers ─────────────────────────────────────────────────────
    //
    // GMap.NET 2.x built-in providers use deprecated tile URLs that are now blocked.
    // These custom providers use current CDN URLs that work without API keys or Referer.

    internal sealed class CartoLightProvider : GMapProvider
    {
        private static readonly Guid _id =
            new Guid("dcb67184-fb8f-4403-afc3-c95fa03428bc");

        public static readonly CartoLightProvider Instance = new CartoLightProvider();

        private CartoLightProvider() { }

        public override Guid Id         => _id;
        public override string Name     => "Carto Light";
        public override PureProjection Projection => MercatorProjection.Instance;
        public override GMapProvider[] Overlays   => new GMapProvider[] { this };

        public override PureImage GetTileImage(GPoint pos, int zoom)
            => GetTileImageUsingHttp(
                $"https://a.basemaps.cartocdn.com/light_all/{zoom}/{pos.X}/{pos.Y}.png");
    }

    internal sealed class CartoDarkProvider : GMapProvider
    {
        private static readonly Guid _id =
            new Guid("a3c91e2f-7d45-4b38-8f2a-1e6b09d4c573");

        public static readonly CartoDarkProvider Instance = new CartoDarkProvider();

        private CartoDarkProvider() { }

        public override Guid Id         => _id;
        public override string Name     => "Carto Dark";
        public override PureProjection Projection => MercatorProjection.Instance;
        public override GMapProvider[] Overlays   => new GMapProvider[] { this };

        public override PureImage GetTileImage(GPoint pos, int zoom)
        {
            try   { return GetTileImageUsingHttp($"https://a.basemaps.cartocdn.com/dark_all/{zoom}/{pos.X}/{pos.Y}.png"); }
            catch { return null; }
        }
    }

    internal sealed class EsriSatelliteProvider : GMapProvider
    {
        private static readonly Guid _id =
            new Guid("d1f1643b-79c6-4af1-861d-9abad044ce91");

        public static readonly EsriSatelliteProvider Instance = new EsriSatelliteProvider();

        private EsriSatelliteProvider() { }

        public override Guid Id         => _id;
        public override string Name     => "ESRI World Imagery";
        public override PureProjection Projection => MercatorProjection.Instance;
        public override GMapProvider[] Overlays   => new GMapProvider[] { this };

        // ESRI tile order: Z / Y / X  (row/col, not col/row)
        public override PureImage GetTileImage(GPoint pos, int zoom)
        {
            try   { return GetTileImageUsingHttp($"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{zoom}/{pos.Y}/{pos.X}"); }
            catch { return null; }
        }
    }

    // ── Fix / waypoint marker ─────────────────────────────────────────────────────

    internal sealed class FixMarker : GMapMarker
    {
        public string Ident { get; }
        private readonly string _type;
        private readonly string _freq;
        private readonly FixRestriction _restriction;
        private readonly bool   _dimmed;
        private readonly string _role;       // "origin"|"dest"|"sid"|"star"|"enroute"|"vor_route" | null
        private readonly string _labelText;  // texto visible en pill-box (e.g. "SKSM/01", "PIE/116.80")

        // Shared resources — allocated once
        // Magenta (#FF14DC): contrasta sobre Carto claro, ESRI satélite y rutas de cualquier color
        private static readonly Font  _font        = new Font("Consolas", 14f);
        private static readonly Font  _fontSmall   = new Font("Consolas", 10f);
        private static readonly Font  _fontRestr   = new Font("Consolas", 9f);
        private static readonly Font  _boxFont     = new Font("Consolas", 9f, FontStyle.Bold);
        private static readonly Brush _textBrush   = new SolidBrush(Color.FromArgb(255, 20, 220));
        private static readonly Brush _shadowBrush = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
        private static readonly Brush _aptFill     = new SolidBrush(Color.FromArgb(80, 255, 20, 220));
        private static readonly Pen   _symPen      = new Pen(Color.FromArgb(255, 20, 220), 1.6f);
        private static readonly Pen   _pseudoPen   = new Pen(Color.Cyan, 1.4f);
        private static readonly Brush _pseudoBrush = new SolidBrush(Color.Cyan);
        private static readonly Pen   _apfxPen     = new Pen(Color.FromArgb(255, 185, 50), 1.5f);
        private static readonly Brush _apfxBrush   = new SolidBrush(Color.FromArgb(255, 185, 50));
        private static readonly Brush _restrBrush  = new SolidBrush(Color.FromArgb(255, 220, 120));
        private static readonly Pen   _restrPen    = new Pen(Color.FromArgb(255, 220, 120), 1.2f);
        // Pill-box colors por rol
        private static readonly Color _cOrigin  = Color.FromArgb(220, 220,  55,  55);
        private static readonly Color _cDest    = Color.FromArgb(220, 210, 110,   0);
        private static readonly Color _cEnroute = Color.FromArgb(220, 155,  45, 230);
        private static readonly Color _cVor     = Color.FromArgb(220,  30, 160,  80);
        private static readonly Color _cSid     = Color.FromArgb(220,   0, 200, 175);
        private static readonly Color _cStar    = Color.FromArgb(220, 235, 165,  20);

        // Dimmed (ambient) resources — steel-blue muted, for background context fixes
        private static readonly Pen   _dimPen      = new Pen(Color.FromArgb(85, 115, 160), 1.2f);
        private static readonly Brush _dimBrush    = new SolidBrush(Color.FromArgb(85, 115, 160));
        private static readonly Brush _dimAptFill  = new SolidBrush(Color.FromArgb(35, 85, 115, 160));
        private static readonly Brush _dimLabelBg  = new SolidBrush(Color.FromArgb(130, 8, 12, 22));

        public FixMarker(PointLatLng pos, string ident, string type, string freq = null,
                         FixRestriction restriction = null, bool dimmed = false,
                         string role = null, string labelText = null)
            : base(pos)
        {
            Ident        = ident;
            _type        = type?.ToLower() ?? "wpt";
            _freq        = freq;
            _restriction = restriction;
            _dimmed      = dimmed;
            _role        = role;
            _labelText   = labelText;
            Offset = new Point(0, 0);
            Size   = new Size(14, 14);
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cx = LocalPosition.X;
            float cy = LocalPosition.Y;

            Pen   pen   = _dimmed ? _dimPen   : _symPen;
            Brush brush = _dimmed ? _dimBrush : _textBrush;

            switch (_type)
            {
                case "apt":
                    // Círculo semi-transparente + línea horizontal diámetro
                    g.FillEllipse(_dimmed ? _dimAptFill : _aptFill, cx - 5, cy - 5, 10, 10);
                    g.DrawEllipse(pen, cx - 5, cy - 5, 10, 10);
                    g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                    break;

                case "vor":
                {
                    const float VR    = 12f;  // radio exterior (compass rose)
                    const float HexR  =  5f;  // radio hexágono interior
                    const float TLong =  8f;  // extensión tick cardinal (N/S/E/W)
                    const float TShrt =  4f;  // extensión tick intercardinal
                    const float InR   =  6f;  // radio interior de la cruz cardinal

                    // Círculo exterior
                    g.DrawEllipse(pen, cx - VR, cy - VR, VR * 2, VR * 2);

                    // 8 ticks radiales: cardinales más largos, intercardinales más cortos
                    for (int k = 0; k < 8; k++)
                    {
                        double ang  = k * 45.0 * Math.PI / 180.0;
                        float  sinA = (float)Math.Sin(ang);
                        float  cosA = (float)Math.Cos(ang);
                        float  tLen = (k % 2 == 0) ? TLong : TShrt;
                        g.DrawLine(pen,
                            cx + VR          * sinA, cy - VR          * cosA,
                            cx + (VR + tLen) * sinA, cy - (VR + tLen) * cosA);
                    }

                    // Cruz cardinal interior (4 líneas desde InR hasta VR)
                    for (int k = 0; k < 4; k++)
                    {
                        double ang  = k * 90.0 * Math.PI / 180.0;
                        float  sinA = (float)Math.Sin(ang);
                        float  cosA = (float)Math.Cos(ang);
                        g.DrawLine(pen,
                            cx + InR * sinA, cy - InR * cosA,
                            cx + VR  * sinA, cy - VR  * cosA);
                    }

                    // Hexágono interior
                    var vhex = new PointF[6];
                    for (int i = 0; i < 6; i++)
                    {
                        double a = i * 60.0 * Math.PI / 180.0;
                        vhex[i] = new PointF(cx + (float)(HexR * Math.Sin(a)),
                                             cy - (float)(HexR * Math.Cos(a)));
                    }
                    g.DrawPolygon(pen, vhex);

                    // Punto central
                    g.FillEllipse(brush, cx - 2.5f, cy - 2.5f, 5f, 5f);
                    break;
                }

                case "dme":
                {
                    // Solo hexágono (DME sin guía azimutal)
                    var dhex = new PointF[6];
                    for (int i = 0; i < 6; i++)
                    {
                        double a = i * 60.0 * Math.PI / 180.0;
                        dhex[i] = new PointF(cx + (float)(6.5 * Math.Sin(a)),
                                             cy - (float)(6.5 * Math.Cos(a)));
                    }
                    g.DrawPolygon(pen, dhex);
                    g.FillEllipse(brush, cx - 2, cy - 2, 4, 4);
                    break;
                }

                case "ndb":
                    // Círculo exterior + punto central + 4 bigotes diagonales
                    g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
                    g.FillEllipse(brush, cx - 2, cy - 2, 4, 4);
                    foreach (int deg in new[] { 45, 135, 225, 315 })
                    {
                        double rad = deg * Math.PI / 180.0;
                        float ix = cx + (float)(6  * Math.Sin(rad));
                        float iy = cy - (float)(6  * Math.Cos(rad));
                        float ox = cx + (float)(10 * Math.Sin(rad));
                        float oy = cy - (float)(10 * Math.Cos(rad));
                        g.DrawLine(pen, ix, iy, ox, oy);
                    }
                    break;

                case "rwy":
                    // Pequeño rectángulo relleno (umbral de pista)
                    g.FillRectangle(brush, cx - 3, cy - 5, 6, 10);
                    break;

                case "pseudo":
                    // Círculo hueco cyan — TOD / TOC
                    g.DrawEllipse(_pseudoPen, cx - 4, cy - 4, 8, 8);
                    break;

                case "apfx":
                {
                    // Diamante abierto ámbar — punto de referencia de aproximación (FAP/5NM)
                    const float DR = 6f;
                    var diamond = new PointF[]
                    {
                        new PointF(cx,      cy - DR),
                        new PointF(cx + DR, cy),
                        new PointF(cx,      cy + DR),
                        new PointF(cx - DR, cy),
                    };
                    g.DrawPolygon(_apfxPen, diamond);
                    break;
                }

                default: // wpt y otros
                    // Triángulo equilátero apuntando arriba
                    var tri = new PointF[]
                    {
                        new PointF(cx,         cy - 6),
                        new PointF(cx + 5.2f,  cy + 3),
                        new PointF(cx - 5.2f,  cy + 3),
                    };
                    g.DrawPolygon(pen, tri);
                    break;
            }

            // Etiqueta — dimmed aparece a mayor zoom para no saturar la vista
            int  zoom      = (int)(Overlay?.Control?.Zoom ?? 0);
            int  zoomShift = _dimmed ? 2 : 0;
            bool showLabel = (_type == "apt" || _type == "vor" || _type == "ndb" || _type == "dme" || _type == "rwy" || _type == "apfx")
                ? zoom >= 6  + zoomShift
                : _type == "pseudo"
                ? zoom >= 10 + zoomShift
                : zoom >= 9  + zoomShift;

            if (showLabel && !string.IsNullOrEmpty(Ident))
            {
                if (_role != null && !_dimmed)
                {
                    DrawPillLabel(g, cx, cy);
                }
                else if (_type == "pseudo")
                {
                    float lx = cx + 8f;
                    DrawHaloWithBrush(g, Ident, _font, lx, cy - _font.Height / 2f, _pseudoBrush);
                }
                else if (_type == "apfx")
                {
                    float lx = cx + 8f;
                    DrawHaloWithBrush(g, Ident, _fontSmall, lx, cy - _fontSmall.Height / 2f, _apfxBrush);
                }
                else if (_dimmed)
                {
                    // Etiqueta compacta sin halo para fixes ambient: rect oscuro + texto pequeño sólido
                    float lx = (_type == "vor") ? cx + 22f : cx + 8f;
                    float ly = cy - _fontSmall.Height / 2f;
                    SizeF tsz = g.MeasureString(Ident, _fontSmall);
                    g.FillRectangle(_dimLabelBg, lx - 2f, ly - 1f, tsz.Width + 4f, _fontSmall.Height + 2f);
                    g.DrawString(Ident, _fontSmall, _dimBrush, lx, ly);
                    if (!string.IsNullOrEmpty(_freq) &&
                        (_type == "vor" || _type == "ndb" || _type == "dme"))
                    {
                        float nextY = ly + _fontSmall.Height + 1f;
                        SizeF fsz = g.MeasureString(_freq, _fontSmall);
                        g.FillRectangle(_dimLabelBg, lx - 2f, nextY - 1f, fsz.Width + 4f, _fontSmall.Height + 2f);
                        g.DrawString(_freq, _fontSmall, _dimBrush, lx, nextY);
                    }
                }
                else
                {
                    float lx = (_type == "vor") ? cx + 22f : cx + 8f;
                    float ly = cy - _font.Height / 2f;
                    DrawHaloWithBrush(g, Ident, _font, lx, ly, brush);
                    float nextY = ly + _font.Height;
                    if (!string.IsNullOrEmpty(_freq) &&
                        (_type == "vor" || _type == "ndb" || _type == "dme"))
                    {
                        DrawHaloWithBrush(g, _freq, _fontSmall, lx, nextY, brush);
                        nextY += _fontSmall.Height;
                    }
                    if (_restriction != null && zoom >= 9)
                        DrawRestriction(g, lx, nextY);
                }
            }
        }

        private static void AddRoundedRect(
            System.Drawing.Drawing2D.GraphicsPath path,
            float x, float y, float w, float h, float r)
        {
            path.AddArc(x,             y,             r * 2, r * 2, 180,  90);
            path.AddArc(x + w - r * 2, y,             r * 2, r * 2, 270,  90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2,   0,  90);
            path.AddArc(x,             y + h - r * 2, r * 2, r * 2,  90,  90);
            path.CloseFigure();
        }

        private void DrawPillLabel(Graphics g, float cx, float cy)
        {
            Color boxColor;
            switch (_role)
            {
                case "origin":    boxColor = _cOrigin;  break;
                case "dest":      boxColor = _cDest;    break;
                case "vor_route": boxColor = _cVor;     break;
                case "sid":       boxColor = _cSid;     break;
                case "star":      boxColor = _cStar;    break;
                default:          boxColor = _cEnroute; break;
            }

            string text = _labelText ?? Ident ?? "";
            if (string.IsNullOrEmpty(text)) return;

            SizeF sz = g.MeasureString(text, _boxFont);
            float bh = sz.Height + 4f;
            float bw = sz.Width  + 8f;
            float r  = bh / 2f;

            float symR = (_type == "vor") ? 22f : 8f;
            float bx   = cx + symR + 3f;
            float by   = cy - bh / 2f;

            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            using (var fill = new SolidBrush(boxColor))
            {
                AddRoundedRect(path, bx, by, bw, bh, r);
                g.FillPath(fill, path);
            }

            using (var textB = new SolidBrush(Color.White))
                g.DrawString(text, _boxFont, textB, bx + 4f, by + 2f);
        }

        private static void DrawHalo(Graphics g, string text, Font font, float x, float y)
        {
            g.DrawString(text, font, _shadowBrush, x - 1, y - 1);
            g.DrawString(text, font, _shadowBrush, x + 1, y - 1);
            g.DrawString(text, font, _shadowBrush, x - 1, y + 1);
            g.DrawString(text, font, _shadowBrush, x + 1, y + 1);
            g.DrawString(text, font, _textBrush, x, y);
        }

        private static void DrawHaloWithBrush(Graphics g, string text, Font font, float x, float y, Brush brush)
        {
            g.DrawString(text, font, _shadowBrush, x - 1, y - 1);
            g.DrawString(text, font, _shadowBrush, x + 1, y - 1);
            g.DrawString(text, font, _shadowBrush, x - 1, y + 1);
            g.DrawString(text, font, _shadowBrush, x + 1, y + 1);
            g.DrawString(text, font, brush, x, y);
        }

        /// <summary>
        /// Draws altitude restriction (with standard aeronautical lines) and optional speed.
        /// At-or-above (+): line below the text. At-or-below (-): line above. At exactly: both lines.
        /// Between (B): two stacked lines with range. Speed in yellow below altitude.
        /// </summary>
        private void DrawRestriction(Graphics g, float lx, float baseY)
        {
            float cy = baseY;

            if (_restriction.AltFt.HasValue)
            {
                string altText = _restriction.AltText();
                // Measure text width for underline/overline
                SizeF sz = g.MeasureString(altText, _fontRestr);

                bool lineAbove = _restriction.AltDescr == "-" || _restriction.AltDescr == "A" || _restriction.AltDescr == "@";
                bool lineBelow = _restriction.AltDescr == "+" || _restriction.AltDescr == "A" || _restriction.AltDescr == "@";

                // Shadow + colored text
                g.DrawString(altText, _fontRestr, _shadowBrush, lx - 1, cy - 1);
                g.DrawString(altText, _fontRestr, _shadowBrush, lx + 1, cy + 1);
                g.DrawString(altText, _fontRestr, _restrBrush, lx, cy);

                float lineW = sz.Width - 2f;
                if (lineAbove)
                    g.DrawLine(_restrPen, lx, cy,            lx + lineW, cy);
                if (lineBelow)
                    g.DrawLine(_restrPen, lx, cy + sz.Height, lx + lineW, cy + sz.Height);

                cy += sz.Height + 1f;
            }

            if (_restriction.SpeedKts.HasValue)
            {
                string spdText = _restriction.SpdText();
                g.DrawString(spdText, _fontRestr, _shadowBrush, lx - 1, cy - 1);
                g.DrawString(spdText, _fontRestr, _shadowBrush, lx + 1, cy + 1);
                g.DrawString(spdText, _fontRestr, _restrBrush, lx, cy);
            }
        }
    }

    // ── SID / STAR route label ────────────────────────────────────────────────────

    internal sealed class RouteLabelMarker : GMapMarker
    {
        private readonly string _name;
        private readonly float  _angleDeg;

        private static readonly Font  _lblFont    = new Font("Consolas", 7f, FontStyle.Bold);
        private static readonly Brush _shadowBrush = new SolidBrush(Color.FromArgb(90, 0, 0, 0));

        public RouteLabelMarker(PointLatLng pos, string name, float angleDeg) : base(pos)
        {
            _name     = name;
            _angleDeg = angleDeg;
            Offset    = new Point(0, 0);
            Size      = new Size(120, 20);
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var   sz = g.MeasureString(_name, _lblFont);
            float hw = sz.Width  / 2f;
            float hh = sz.Height / 2f;

            var state = g.Save();
            g.TranslateTransform(LocalPosition.X, LocalPosition.Y);
            g.RotateTransform(_angleDeg);

            g.DrawString(_name, _lblFont, _shadowBrush, -hw - 1f, -hh - 1f);
            g.DrawString(_name, _lblFont, _shadowBrush, -hw + 1f, -hh - 1f);
            g.DrawString(_name, _lblFont, _shadowBrush, -hw - 1f, -hh + 1f);
            g.DrawString(_name, _lblFont, _shadowBrush, -hw + 1f, -hh + 1f);
            g.DrawString(_name, _lblFont, Brushes.White, -hw, -hh);

            g.Restore(state);
        }
    }

    // ── Leg distance + bearing label ────────────────────────────────────────────

    internal sealed class LegInfoMarker : GMapMarker
    {
        private readonly string _text;
        private readonly float  _angleDeg;
        private readonly double _distNm;

        private static readonly Font  _f        = new Font("Consolas", 7f, FontStyle.Regular);
        private static readonly Brush _bgBrush   = new SolidBrush(Color.FromArgb(230, 248, 248, 245));
        private static readonly Pen   _border    = new Pen(Color.FromArgb(180, 130, 130, 145), 0.8f);
        private static readonly Brush _fgBrush   = new SolidBrush(Color.FromArgb(255, 28, 28, 38));
        private static readonly Brush _arrowBrush = new SolidBrush(Color.FromArgb(200, 28, 28, 38));

        public LegInfoMarker(PointLatLng pos, string text, float angleDeg, double distNm)
            : base(pos)
        {
            _text     = text;
            _angleDeg = angleDeg;
            _distNm   = distNm;
            Offset    = new Point(0, 0);
            Size      = new Size(96, 16);
        }

        public override void OnRender(Graphics g)
        {
            int zoom = (int)(Overlay?.Control?.Zoom ?? 0);
            if (zoom < 7) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            SizeF sz  = g.MeasureString(_text, _f);
            float hw  = sz.Width  / 2f;
            float hh  = sz.Height / 2f;
            const float Pad = 2f;
            float boxW = sz.Width + Pad * 2;

            // Regla del 60%: estimar longitud del segmento en píxeles a partir del zoom
            // pixelsPerNm ≈ 256 * 2^zoom * 111.32 * cos(lat) / (360 * 1.852)
            double cosLat  = Math.Cos(Position.Lat * Math.PI / 180.0);
            double pxPerNm = 256.0 * Math.Pow(2, zoom) * 111.32 * cosLat / (360.0 * 1.852);
            double segPx   = _distNm * pxPerNm;
            if (boxW > segPx * 0.60) return;

            var state = g.Save();
            g.TranslateTransform(LocalPosition.X, LocalPosition.Y);
            g.RotateTransform(_angleDeg);

            float rx = -hw - Pad, ry = -hh - Pad;
            float rw = boxW, rh = sz.Height + Pad * 2;
            g.FillRectangle(_bgBrush, rx, ry, rw, rh);
            g.DrawRectangle(_border,  rx, ry, rw, rh);
            g.DrawString(_text, _f, _fgBrush, -hw, -hh);

            // Flecha direccional a la derecha del box (apunta en dirección de viaje)
            const float AW = 5f, AH = 3.5f;
            float ax = hw + Pad + 2f;
            g.FillPolygon(_arrowBrush, new PointF[]
            {
                new PointF(ax + AW, 0f),
                new PointF(ax,  AH),
                new PointF(ax, -AH),
            });

            g.Restore(state);
        }
    }

    // ── ATC label marker — center dot + ICAO text for local positions (DEL/GND/TWR) ──

    internal sealed class AtcLabelMarker : GMapMarker
    {
        private static readonly Font  _f      = new Font("Consolas", 7f, FontStyle.Bold);
        private static readonly Brush _shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
        private static readonly Brush _text   = new SolidBrush(Color.FromArgb(230, 230, 240, 255));

        private readonly string _icao;
        public  readonly string TooltipContent;

        public AtcLabelMarker(PointLatLng pos, string icao,
                              IList<IvaoAtcStation> local,
                              IList<IvaoAtcStation> atis)
            : base(pos)
        {
            _icao = icao;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(icao);
            sb.AppendLine(new string('─', 16));
            foreach (var s in local.OrderBy(s => AtcStationMarker.PosOrder(s.Position)))
            {
                var f = s.Frequency > 0 ? s.Frequency.ToString("F3") : "---";
                sb.AppendLine($"{s.Position,-4} {f}");
            }
            foreach (var s in atis)
            {
                var f = s.Frequency > 0 ? s.Frequency.ToString("F3") : "---";
                sb.Append($"ATIS {f}");
                if (!string.IsNullOrEmpty(s.AtisText))
                    sb.Append($"  {s.AtisText}");
                sb.AppendLine();
            }
            TooltipContent = sb.ToString().TrimEnd();

            Size   = new Size(48, 14);
            Offset = new Point(-24, -7);
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = LocalArea;

            // Center dot
            using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                g.FillEllipse(b, r.X + r.Width / 2 - 2, r.Y + r.Height / 2 - 2, 4, 4);

            // ICAO label with shadow
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                float tx = r.X + r.Width / 2f, ty = r.Y;
                g.DrawString(_icao, _f, _shadow, tx - 1, ty - 1, sf);
                g.DrawString(_icao, _f, _shadow, tx + 1, ty - 1, sf);
                g.DrawString(_icao, _f, _shadow, tx - 1, ty + 1, sf);
                g.DrawString(_icao, _f, _shadow, tx + 1, ty + 1, sf);
                g.DrawString(_icao, _f, _text,   tx,     ty,     sf);
            }
        }
    }

    // ── ATC station marker — area positions (APP / CTR / DEP / FSS) ───────────────

    internal sealed class AtcStationMarker : GMapMarker
    {
        private static readonly Font  _fHead = new Font("Consolas", 7f,   FontStyle.Bold);
        private static readonly Font  _fRow  = new Font("Consolas", 6.5f, FontStyle.Regular);
        private static readonly Brush _bg    = new SolidBrush(Color.FromArgb(210, 12, 18, 30));
        private static readonly Brush _white = new SolidBrush(Color.FromArgb(230, 230, 240));

        private readonly string _icao;
        private readonly List<(string Pos, string Freq, Color Col)> _rows;

        public AtcStationMarker(PointLatLng pos, string icao, IList<IvaoAtcStation> stations)
            : base(pos)
        {
            _icao = icao;
            _rows = stations
                .OrderBy(s => PosOrder(s.Position))
                .Select(s => (s.Position,
                              s.Frequency > 0 ? s.Frequency.ToString("F3") : "---",
                              PosColor(s.Position)))
                .ToList();

            int w = 90;
            int h = 17 + _rows.Count * 13 + 2;
            Size   = new Size(w, h);
            Offset = new Point(-w / 2, -h - 5);
        }

        public override void OnRender(Graphics g)
        {
            var r = LocalArea;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.FillRectangle(_bg, r);

            var borderCol = _rows.Count > 0 ? _rows[0].Col : Color.DimGray;
            using (var pen = new Pen(Color.FromArgb(180, borderCol.R, borderCol.G, borderCol.B), 1f))
                g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);

            g.DrawString(_icao, _fHead, _white, r.X + 4, r.Y + 1);

            using (var sep = new Pen(Color.FromArgb(55, 160, 160, 180)))
                g.DrawLine(sep, r.X + 2, r.Y + 14, r.Right - 2, r.Y + 14);

            int y = r.Y + 16;
            foreach (var (pos, freq, col) in _rows)
            {
                using (var dot = new SolidBrush(col))
                    g.FillEllipse(dot, r.X + 4, y + 3, 5, 5);
                using (var br = new SolidBrush(Color.FromArgb(210, 210, 225)))
                    g.DrawString($"{pos,-4} {freq}", _fRow, br, r.X + 12, y);
                y += 13;
            }
        }

        internal static int PosOrder(string p)
        {
            switch (p)
            {
                case "DEL": return 0; case "GND": return 1; case "TWR": return 2;
                case "DEP": return 3; case "APP": return 4; case "CTR": return 5;
                case "FSS": return 6; default: return 7;
            }
        }

        internal static Color PosColor(string p)
        {
            switch (p)
            {
                case "DEL": return Color.FromArgb(255, 215,   0);
                case "GND": return Color.FromArgb(165, 130,  45);
                case "TWR": return Color.FromArgb(220,  50,  50);
                case "DEP": return Color.FromArgb(255,  80, 200);
                case "APP": return Color.FromArgb(170,  60, 255);
                case "CTR": return Color.FromArgb( 30, 145, 255);
                case "FSS": return Color.FromArgb( 30, 190, 175);
                default:    return Color.FromArgb(155, 155, 165);
            }
        }
    }

    // ── Aircraft marker ───────────────────────────────────────────────────────────

    internal sealed class AircraftMarker : GMapMarker
    {
        public double            Heading  { get; set; }
        public FsuipcService.AircraftCategory  Category { get; set; } = FsuipcService.AircraftCategory.Unknown;

        private static readonly Brush _body   = new SolidBrush(Color.FromArgb(255, 215, 40));
        private static readonly Pen   _edge   = new Pen(Color.FromArgb(100, 70, 0), 1.5f);
        private static readonly Brush _shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0));

        public AircraftMarker(PointLatLng pos, double heading) : base(pos)
        {
            Heading   = heading;
            Offset    = new Point(-16, -16);
            this.Size = new Size(32, 32);
        }

        public override void OnRender(Graphics g)
        {
            var state = g.Save();
            g.TranslateTransform(LocalPosition.X, LocalPosition.Y);
            g.RotateTransform((float)Heading);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            PointF[] body = GetShape(Category);

            g.TranslateTransform(1.5f, 1.5f);
            g.FillPolygon(_shadow, body);
            g.TranslateTransform(-1.5f, -1.5f);

            g.FillPolygon(_body, body);
            g.DrawPolygon(_edge, body);

            g.Restore(state);
        }

        // Top-down aircraft silhouettes. All shapes face up (heading 0 = North).
        // Coordinates relative to marker center. Larger S = larger icon.
        private static PointF[] GetShape(FsuipcService.AircraftCategory cat)
        {
            switch (cat)
            {
                case FsuipcService.AircraftCategory.Jet:
                {   // Swept-wing airliner — large wingspan, tapered fuselage
                    const float S = 13f;
                    return new PointF[]
                    {
                        new PointF( 0,           -S),           // nose
                        new PointF( 0.10f*S, -0.40f*S),
                        new PointF( 1.05f*S,  0.20f*S),        // R wingtip leading
                        new PointF( 0.90f*S,  0.50f*S),        // R wingtip trailing
                        new PointF( 0.20f*S,  0.30f*S),        // R wing root
                        new PointF( 0.40f*S,  1.00f*S),        // R stabilizer tip
                        new PointF( 0.28f*S,  1.12f*S),
                        new PointF( 0,         0.92f*S),        // tail center
                        new PointF(-0.28f*S,  1.12f*S),
                        new PointF(-0.40f*S,  1.00f*S),        // L stabilizer tip
                        new PointF(-0.20f*S,  0.30f*S),        // L wing root
                        new PointF(-0.90f*S,  0.50f*S),        // L wingtip trailing
                        new PointF(-1.05f*S,  0.20f*S),        // L wingtip leading
                        new PointF(-0.10f*S, -0.40f*S),
                    };
                }
                case FsuipcService.AircraftCategory.Turboprop:
                {   // Straight-wing regional — shorter, wider chord
                    const float S = 11f;
                    return new PointF[]
                    {
                        new PointF( 0,           -S),
                        new PointF( 0.10f*S, -0.30f*S),
                        new PointF( 0.88f*S,  0.05f*S),        // R wingtip (straight leading edge)
                        new PointF( 0.78f*S,  0.42f*S),        // R wingtip trailing
                        new PointF( 0.14f*S,  0.35f*S),
                        new PointF( 0.30f*S,  0.88f*S),        // R stabilizer
                        new PointF( 0.18f*S,  0.98f*S),
                        new PointF( 0,         0.82f*S),
                        new PointF(-0.18f*S,  0.98f*S),
                        new PointF(-0.30f*S,  0.88f*S),
                        new PointF(-0.14f*S,  0.35f*S),
                        new PointF(-0.78f*S,  0.42f*S),
                        new PointF(-0.88f*S,  0.05f*S),
                        new PointF(-0.10f*S, -0.30f*S),
                    };
                }
                case FsuipcService.AircraftCategory.Piston:
                {   // Small GA — compact, wide straight wings relative to body
                    const float S = 8f;
                    return new PointF[]
                    {
                        new PointF( 0,           -S),
                        new PointF( 0.12f*S, -0.20f*S),
                        new PointF( 0.95f*S,  0.08f*S),        // R wingtip (proportionally wide)
                        new PointF( 0.82f*S,  0.45f*S),
                        new PointF( 0.12f*S,  0.35f*S),
                        new PointF( 0.24f*S,  0.80f*S),
                        new PointF( 0.14f*S,  0.92f*S),
                        new PointF( 0,         0.75f*S),
                        new PointF(-0.14f*S,  0.92f*S),
                        new PointF(-0.24f*S,  0.80f*S),
                        new PointF(-0.12f*S,  0.35f*S),
                        new PointF(-0.82f*S,  0.45f*S),
                        new PointF(-0.95f*S,  0.08f*S),
                        new PointF(-0.12f*S, -0.20f*S),
                    };
                }
                case FsuipcService.AircraftCategory.Helicopter:
                {   // Helicopter — oval body with rotor suggestion
                    const float S = 9f;
                    return new PointF[]
                    {
                        new PointF( 0,          -S * 0.45f),   // nose
                        new PointF( S * 0.28f,  -S * 0.20f),
                        new PointF( S * 0.22f,   S * 0.35f),
                        new PointF( S * 0.08f,   S * 0.70f),   // tail boom R
                        new PointF( 0,           S * 0.78f),   // tail
                        new PointF(-S * 0.08f,   S * 0.70f),
                        new PointF(-S * 0.22f,   S * 0.35f),
                        new PointF(-S * 0.28f,  -S * 0.20f),
                    };
                }
                default:
                {   // Unknown — original triangle arrow
                    const float S = 11f;
                    return new PointF[]
                    {
                        new PointF(  0,         -S),
                        new PointF(  S * 0.55f,  S * 0.65f),
                        new PointF(  0,          S * 0.30f),
                        new PointF( -S * 0.55f,  S * 0.65f),
                    };
                }
            }
        }
    }

    // ── Spinner de carga estilo macOS ─────────────────────────────────────────────

    internal sealed class SpinnerOverlay : Control
    {
        private readonly Timer _timer;
        private int _frame;
        private const int Spokes = 12;

        public SpinnerOverlay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            Size    = new Size(72, 72);
            Visible = false;
            _timer  = new Timer { Interval = 83 };   // ~12 fps
            _timer.Tick += (s, e) => { _frame = (_frame + 1) % Spokes; Invalidate(); };
        }

        public void StartSpin()
        {
            _frame = 0;
            Visible = true;
            BringToFront();
            _timer.Start();
        }

        public void StopSpin()
        {
            _timer.Stop();
            Visible = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Fondo oscuro con esquinas redondeadas
            using (var path = RoundedRect(ClientRectangle, 16))
            using (var bg   = new SolidBrush(Color.FromArgb(195, 18, 24, 30)))
                g.FillPath(bg, path);

            float cx = Width / 2f, cy = Height / 2f;

            for (int i = 0; i < Spokes; i++)
            {
                int   age   = (_frame - i + Spokes) % Spokes;
                int   alpha = (int)(255 - age * (215.0 / Spokes));
                if (alpha < 28) alpha = 28;

                double ang = 2.0 * Math.PI * i / Spokes - Math.PI / 2.0;
                float  cos = (float)Math.Cos(ang), sin = (float)Math.Sin(ang);

                using (var pen = new Pen(Color.FromArgb(alpha, 228, 234, 248), 3.2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap   = LineCap.Round;
                    g.DrawLine(pen,
                        cx + 11f * cos, cy + 11f * sin,
                        cx + 22f * cos, cy + 22f * sin);
                }
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int rad)
        {
            int d = rad * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X,         r.Y,          d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            p.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
            p.CloseAllFigures();
            return p;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
