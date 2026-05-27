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
        private GMapControl    _map;
        private GMapOverlay    _airspaceOverlay;
        private GMapOverlay    _atcOverlay;
        private GMapOverlay    _routeShadowOverlay;
        private GMapOverlay    _routeOverlay;
        private GMapOverlay    _ambientOverlay;
        private GMapOverlay    _waypointOverlay;
        private GMapOverlay    _approachOverlay;
        private GMapOverlay    _aircraftOverlay;
        private AircraftMarker _aircraftMarker;
        private Label          _lblStatus;
        private CheckBox       _chkFollow;
        private ComboBox       _cmbProvider;
        private bool           _followAircraft = true;
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
        private Panel    _sidebarPanel;
        private Panel    _sidebarContent;
        private Button   _btnToggleSidebar;
        private bool     _sidebarExpanded = true;
        private bool     _populatingSidebar;

        private ComboBox _cmbOriginRwy, _cmbSid, _cmbSidTrans;
        private ComboBox _cmbDestRwy,   _cmbStar, _cmbStarTrans, _cmbApproach;
        private Label    _lblOriginAirport, _lblDestAirport;
        private Label    _lblOriginWind,    _lblDestWind, _lblApproachCount;

        private string _selOriginRunway, _selDestRunway;
        private string _selSidName,      _selSidTransition;
        private string _selStarName,     _selStarTransition;
        private string _selApproachKey;

        private List<NavRunway>    _sbOriginRunways, _sbDestRunways;
        private List<NavProcedure> _sbSids, _sbStars;
        private List<NavApproach>  _sbApproaches;
        private List<NavIls>       _sbIls;

        private IList<SimbriefWaypoint> _currentWaypoints;
        private string _currentOriginIcao, _currentDestIcao, _currentAltIcao;

        private int? _metarOriginWindDir, _metarOriginWindSpd;
        private int? _metarDestWindDir,   _metarDestWindSpd;

        private static readonly Color _clrApproach = Color.FromArgb(255,  0, 200);
        private static readonly Color _clrMissed   = Color.FromArgb(  0, 200, 255);

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
                Dock      = DockStyle.Left,
                Width     = 380,
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
            _chkFollow.CheckedChanged += (s, e) => _followAircraft = _chkFollow.Checked;

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
                bool v = _chkLayerRoute.Checked;
                _routeOverlay.IsVisibile       = v;
                _routeShadowOverlay.IsVisibile = v;
                _waypointOverlay.IsVisibile    = v;
                _map.Refresh();
            };
            _chkLayerSpaces.CheckedChanged += (s, e) =>
            {
                _airspaceOverlay.IsVisibile = _chkLayerSpaces.Checked;
                _map.Refresh();
            };
            _chkLayerIvao.CheckedChanged += (s, e) =>
            {
                _atcOverlay.IsVisibile = _chkLayerIvao.Checked;
                _map.Refresh();
            };

            bar.Controls.Add(_lblStatus);
            bar.Controls.Add(_chkFollow);
            bar.Controls.Add(_cmbProvider);
            bar.Controls.Add(btnZoomOut);
            bar.Controls.Add(btnZoomIn);
            // Layer toggles: added last → appear leftmost of the right-docked group
            bar.Controls.Add(_chkLayerIvao);
            bar.Controls.Add(_chkLayerSpaces);
            bar.Controls.Add(_chkLayerRoute);
            bar.Controls.Add(_chkLayerTiles);

            _map = new GMapControl { Dock = DockStyle.Fill };

            Controls.Add(_map);
            Controls.Add(bar);
            BuildSidebar();
            Controls.Add(_sidebarPanel);
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

            // Overlays en orden de pintado: sombra → ruta → ambient → fixes → avión (encima de todo)
            _airspaceOverlay    = new GMapOverlay("airspaces");
            _atcOverlay         = new GMapOverlay("atc");
            _routeShadowOverlay = new GMapOverlay("route_shadow");
            _routeOverlay       = new GMapOverlay("route");
            _ambientOverlay     = new GMapOverlay("ambient");
            _waypointOverlay    = new GMapOverlay("waypoints");
            _approachOverlay    = new GMapOverlay("approach");
            _aircraftOverlay    = new GMapOverlay("aircraft");
            _map.Overlays.Add(_airspaceOverlay);
            _map.Overlays.Add(_routeShadowOverlay);
            _map.Overlays.Add(_routeOverlay);
            _map.Overlays.Add(_ambientOverlay);
            _map.Overlays.Add(_waypointOverlay);
            _map.Overlays.Add(_atcOverlay);
            _map.Overlays.Add(_approachOverlay);
            _map.Overlays.Add(_aircraftOverlay);

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
            if (_ambientOverlay != null)
                _ambientOverlay.IsVisibile = (int)_map.Zoom >= 10;
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

        // ── Route drawing ─────────────────────────────────────────────────────────

        // Stage colors:  CLB/SID = teal · CRZ = ice-blue · DSC/STAR = amber
        private static readonly Color _clrClb    = Color.FromArgb(  0, 210, 175);
        private static readonly Color _clrCrz    = Color.FromArgb(170, 200, 255);
        private static readonly Color _clrDsc    = Color.FromArgb(255, 185,  50);
        private static readonly Color _clrAlt    = Color.FromArgb(170, 150, 215);
        private static readonly Color _clrShadow = Color.FromArgb(140,   0,   0,  0);

        /// <param name="originIcao">ICAO del aeropuerto de salida — para buscar la pista en NavData.</param>
        /// <param name="originRunway">Pista asignada por SimBrief (p.ej. "13L"). Si es null se elige automáticamente.</param>
        /// <param name="destIcao">ICAO del aeropuerto de destino — para llegada sin STAR.</param>
        /// <param name="destRunway">Pista de aterrizaje asignada por SimBrief.</param>
        /// <param name="altIcao">ICAO del aeropuerto alterno — dibuja línea punteada desde destino.</param>
        public void LoadRoute(IList<SimbriefWaypoint> waypoints,
            string originIcao = null, string originRunway = null,
            string destIcao   = null, string destRunway   = null,
            string altIcao    = null,
            string sidName    = null, string starName     = null)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (waypoints == null || waypoints.Count < 2) return;

            // ── Guardar estado para RedrawRoute y sidebar ─────────────────────────────
            bool airportChanged = originIcao != _currentOriginIcao || destIcao != _currentDestIcao;
            _currentWaypoints  = waypoints;
            _currentOriginIcao = originIcao;
            _currentDestIcao   = destIcao;
            _currentAltIcao    = altIcao;
            if (airportChanged)
            {
                _selOriginRunway   = originRunway;
                _selDestRunway     = destRunway;
                _selSidName        = sidName;
                _selSidTransition  = null;
                _selStarName       = starName;
                _selStarTransition = null;
                _selApproachKey    = null;
            }

            var wps = waypoints.ToList();

            _spinner.StartSpin();
            System.Threading.Tasks.Task.Run(() =>
            {
                var shadowRoutes  = new List<GMapRoute>();
                var colorRoutes   = new List<GMapRoute>();
                var markers       = new List<GMapMarker>();
                var ambientMarkers = new List<GMapMarker>();

                // ── Salida ───────────────────────────────────────────────
                bool   hasDepRwy     = false;
                (double Lat, double Lon)? depEnd = null;
                bool   noSidStub     = false;
                double noSidStubLat  = 0, noSidStubLon  = 0;
                string noSidDepWpIdent = null;

                if (!string.IsNullOrEmpty(originIcao))
                {
                    try
                    {
                        NavDataClient.PrefetchAirport(originIcao);
                        var runways      = NavDataClient.GetRunways(originIcao);
                        var sidFixes     = wps
                            .Where(w => (w.Stage ?? "CRZ") == "CLB" && w.Type != "apt")
                            .ToList();
                        var firstPlanFix = wps.FirstOrDefault(w => w.Type != "apt");

                        // SimBrief marca Stage="CLB" en TODOS los waypoints de subida, no solo SID.
                        // El flag IsSidStar (is_sid_star en JSON) identifica fixes reales de SID.
                        bool hasSid = sidFixes.Any(w => w.IsSidStar);

                        if (runways?.Count > 0 && firstPlanFix != null)
                        {
                            NavRunway rwy;

                            if (hasSid)
                            {
                                // ── Con SID confirmado por NavData: pista física + End como primer punto del navlog
                                rwy = FindDepartureRunway(runways, originRunway, sidFixes[0]);
                                if (rwy != null)
                                {
                                    DrawRunwaySegment(rwy, shadowRoutes, colorRoutes, markers, _clrClb);
                                    hasDepRwy = true;
                                    depEnd    = (rwy.EndLat, rwy.EndLon);
                                }
                            }
                            else
                            {
                                // ── Sin SID confirmado: pista física + ext3nm + arco circular al primer fix
                                rwy = FindDepartureRunway(runways, originRunway, firstPlanFix);
                                if (rwy != null)
                                {
                                    DrawRunwaySegment(rwy, shadowRoutes, colorRoutes, markers, _clrClb);

                                    double depBrg = GeodesicBearing(
                                        rwy.ThresholdLat, rwy.ThresholdLon, rwy.EndLat, rwy.EndLon);
                                    if (HeadingDiff(depBrg, rwy.Heading) > 90)
                                        depBrg = (depBrg + 180) % 360;

                                    double rad    = depBrg * Math.PI / 180;
                                    double cosEnd = Math.Cos(rwy.EndLat * Math.PI / 180);
                                    double ext    = 3.0 * 1852.0;
                                    double extLat = rwy.EndLat + (ext * Math.Cos(rad)) / 111320;
                                    double extLon = rwy.EndLon + (ext * Math.Sin(rad)) / (111320 * cosEnd);

                                    // Buscar waypoint de salida alineado entre 2 y 5 NM del final de pista
                                    var nearbyDep  = NavDataClient.GetAirportWaypoints(originIcao);
                                    double depBest = double.MaxValue;
                                    foreach (var wp in nearbyDep ?? Enumerable.Empty<NavAirportWaypoint>())
                                    {
                                        double dKm = DistanceKm(wp.Lat, wp.Lon, rwy.EndLat, rwy.EndLon);
                                        if (dKm < 2.0 * 1.852 || dKm > 5.0 * 1.852) continue;
                                        double bFromEnd = GeodesicBearing(
                                            rwy.EndLat, rwy.EndLon, wp.Lat, wp.Lon);
                                        if (HeadingDiff(depBrg, bFromEnd) >= 25.0) continue;
                                        double sc = Math.Abs(dKm - 3.5 * 1.852);
                                        if (sc < depBest)
                                        {
                                            depBest         = sc;
                                            extLat          = wp.Lat;
                                            extLon          = wp.Lon;
                                            noSidDepWpIdent = wp.Ident;
                                        }
                                    }
                                    if (noSidDepWpIdent != null)
                                        markers.Add(new FixMarker(
                                            new PointLatLng(extLat, extLon),
                                            noSidDepWpIdent, "apfx"));

                                    // Extensión visible: rwy.End → punto de pivote de salida
                                    var depExt = new List<PointLatLng> {
                                        new PointLatLng(rwy.EndLat, rwy.EndLon),
                                        new PointLatLng(extLat, extLon)
                                    };
                                    shadowRoutes.Add(new GMapRoute(depExt, "s_depext")
                                        { Stroke = new Pen(_clrShadow, 4.5f) });
                                    colorRoutes.Add(new GMapRoute(depExt, "depext")
                                        { Stroke = new Pen(_clrClb, 2.5f) });

                                    // Arco de salida: arco circular de radio 2.5 NM desde ext3nm
                                    // hasta que la tangente apunta al primer fix del plan,
                                    // más recta de tangente al fix. Fallback a Bézier si
                                    // el fix está demasiado cerca del círculo.
                                    var curve = ComputeDepartureArc(
                                        extLat, extLon, depBrg,
                                        firstPlanFix.Lat, firstPlanFix.Lon);

                                    if (curve == null)
                                    {
                                        // Fallback Bézier para casos de fix muy cercano al arco
                                        int firstIdx = wps.FindIndex(w => w.Type != "apt");
                                        var fix2Dep  = firstIdx >= 0
                                            ? wps.Skip(firstIdx + 1).FirstOrDefault(w => w.Type != "apt")
                                            : null;
                                        double endBrgDep = fix2Dep != null
                                            ? GeodesicBearing(firstPlanFix.Lat, firstPlanFix.Lon,
                                                              fix2Dep.Lat, fix2Dep.Lon)
                                            : GeodesicBearing(extLat, extLon,
                                                              firstPlanFix.Lat, firstPlanFix.Lon);
                                        curve = ComputeTransitionCurve(
                                            extLat, extLon, depBrg,
                                            firstPlanFix.Lat, firstPlanFix.Lon, endBrgDep,
                                            armFraction: 0.40);
                                    }

                                    if (curve?.Count >= 2)
                                    {
                                        shadowRoutes.Add(new GMapRoute(curve, "s_nosid")
                                            { Stroke = new Pen(_clrShadow, 4.5f) });
                                        colorRoutes.Add(new GMapRoute(curve, "nosid")
                                            { Stroke = new Pen(_clrClb, 2.5f) });

                                        // Guardar el penúltimo punto del arco (punto T donde la
                                        // tangente apunta exactamente a firstPlanFix).
                                        // Se insertará como ancla antes de firstPlanFix en allPts
                                        // para que BuildSmoothedRoutes genere un arco fly-over en él.
                                        if (curve.Count >= 3)
                                        {
                                            noSidStub    = true;
                                            noSidStubLat = curve[curve.Count - 2].Lat;
                                            noSidStubLon = curve[curve.Count - 2].Lng;
                                        }
                                    }

                                    hasDepRwy = true;
                                    // depEnd no se asigna: allPts arranca en firstPlanFix.
                                    // El arco llega a firstPlanFix tangencialmente → unión continua.
                                }
                            }
                        }
                    }
                    catch { }
                }

                // ── Resolución SID / STAR desde NavData (necesaria antes del bloque de llegada) ──
                NavProcedure sidProc = null, starProc = null;
                if (!string.IsNullOrEmpty(originIcao))
                    sidProc  = MatchProcedure(
                        wps.Where(w => (w.Stage ?? "CRZ") == "CLB" && w.Type != "apt")
                           .Select(w => w.Ident).ToList(),
                        NavDataClient.GetSids(originIcao), originRunway, sidName);
                if (!string.IsNullOrEmpty(destIcao))
                    starProc = MatchProcedure(
                        wps.Where(w => (w.Stage ?? "CRZ") == "DSC" && w.Type != "apt")
                           .Select(w => w.Ident).ToList(),
                        NavDataClient.GetStars(destIcao), destRunway, starName);

                string resolvedSid  = sidProc?.Name;
                string resolvedStar = starProc?.Name;

                // Idents de fixes fly-over según legs de NavData
                var flyoverIdents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var proc in new[] { sidProc, starProc })
                {
                    if (proc?.Legs == null) continue;
                    foreach (var leg in proc.Legs)
                        if (leg.IsFlyover && !string.IsNullOrEmpty(leg.Fix))
                            flyoverIdents.Add(leg.Fix);
                }

                // ── Llegada ───────────────────────────────────────────────
                bool      hasArrRwy  = false;
                NavRunway destThrRwy = null;
                bool   noStarThr5   = false;
                double noStarThr5Lat = 0, noStarThr5Lon = 0;
                bool   noStarAligned    = false;
                double noStarAlignedLat = 0, noStarAlignedLon = 0;
                double noStarAlignedThrLat = 0, noStarAlignedThrLon = 0;
                string noStarAlignedIdent = null;
                bool   noStarNoAligned     = false;
                double noStarNoAlignedLat  = 0, noStarNoAlignedLon  = 0;
                string noStarNoAlignedIdent = null;

                if (!string.IsNullOrEmpty(destIcao))
                {
                    try
                    {
                        var starFixes = wps
                            .Where(w => (w.Stage ?? "CRZ") == "DSC" && w.Type != "apt")
                            .ToList();

                        // Igual que hasSid: usar IsSidStar para detectar STAR real de SimBrief.
                        bool hasStar = starFixes.Any(w => w.IsSidStar);
                        if (!hasStar)
                        {
                            NavDataClient.PrefetchAirport(destIcao);
                            var arrRunways = NavDataClient.GetRunways(destIcao);
                            var lastFix    = wps.LastOrDefault(w => w.Type != "apt");

                            if (arrRunways?.Count > 0 && lastFix != null)
                            {
                                var arrRwy = FindArrivalRunway(
                                    arrRunways, destRunway, lastFix.Lat, lastFix.Lon);

                                if (arrRwy != null)
                                {
                                    destThrRwy = arrRwy;
                                    double approachBrg = GeodesicBearing(
                                        arrRwy.ThresholdLat, arrRwy.ThresholdLon,
                                        arrRwy.EndLat,       arrRwy.EndLon);
                                    if (HeadingDiff(approachBrg, arrRwy.Heading) > 90)
                                        approachBrg = (approachBrg + 180) % 360;

                                    double oppBrg  = (approachBrg + 180) % 360;
                                    double rad5    = oppBrg * Math.PI / 180;
                                    double cos5    = Math.Cos(arrRwy.ThresholdLat * Math.PI / 180);
                                    double ext5    = 5.0 * 1852.0;
                                    double thr5Lat = arrRwy.ThresholdLat + (ext5 * Math.Cos(rad5)) / 111320;
                                    double thr5Lon = arrRwy.ThresholdLon + (ext5 * Math.Sin(rad5)) / (111320 * cos5);

                                    // El arco de transición al thr5nm lo genera BuildSmoothedRoutes
                                    // tratando lastFix como fly-over → llega a thr5nm ya alineado.
                                    noStarThr5    = true;
                                    noStarThr5Lat = thr5Lat;
                                    noStarThr5Lon = thr5Lon;

                                    // Buscar waypoint alineado más cercano a 10 NM del umbral
                                    var nearbyNS  = NavDataClient.GetAirportWaypoints(destIcao);
                                    double nsDKm  = DistanceKm(lastFix.Lat, lastFix.Lon,
                                                        arrRwy.ThresholdLat, arrRwy.ThresholdLon);
                                    double nsBest = double.MaxValue;
                                    NavAirportWaypoint bestNS = null;
                                    foreach (var wp in nearbyNS ?? Enumerable.Empty<NavAirportWaypoint>())
                                    {
                                        double dKm = DistanceKm(wp.Lat, wp.Lon,
                                            arrRwy.ThresholdLat, arrRwy.ThresholdLon);
                                        if (dKm < 3.0 * 1.852 || dKm > nsDKm) continue;
                                        double bToThr = GeodesicBearing(wp.Lat, wp.Lon,
                                            arrRwy.ThresholdLat, arrRwy.ThresholdLon);
                                        if (HeadingDiff(approachBrg, bToThr) >= 20.0) continue;
                                        double sc = Math.Abs(dKm - 10.0 * 1.852);
                                        if (sc < nsBest) { nsBest = sc; bestNS = wp; }
                                    }
                                    if (bestNS != null)
                                    {
                                        noStarNoAligned      = true;
                                        noStarNoAlignedLat   = bestNS.Lat;
                                        noStarNoAlignedLon   = bestNS.Lon;
                                        noStarNoAlignedIdent = bestNS.Ident;
                                        markers.Add(new FixMarker(
                                            new PointLatLng(bestNS.Lat, bestNS.Lon),
                                            bestNS.Ident, "apfx"));
                                    }

                                    // Tramo físico de llegada: thr5nm → umbral
                                    var arrStraight = new List<PointLatLng> {
                                        new PointLatLng(thr5Lat, thr5Lon),
                                        new PointLatLng(arrRwy.ThresholdLat, arrRwy.ThresholdLon)
                                    };
                                    shadowRoutes.Add(new GMapRoute(arrStraight, "s_arr")
                                        { Stroke = new Pen(_clrShadow, 4.5f) });
                                    colorRoutes.Add(new GMapRoute(arrStraight, "arr")
                                        { Stroke = new Pen(_clrDsc, 2.5f) });

                                    markers.Add(new FixMarker(
                                        new PointLatLng(thr5Lat, thr5Lon),
                                        arrRwy.Name, "apfx"));
                                    markers.Add(new FixMarker(
                                        new PointLatLng(arrRwy.ThresholdLat, arrRwy.ThresholdLon),
                                        arrRwy.Name, "rwy"));
                                    hasArrRwy = true;
                                }
                            }
                        }
                        else
                        {
                            // STAR confirmado — comprobar si el último fix de la STAR
                            // no está alineado con la pista. Si es así, buscar en la caché
                            // de waypoints ambient un fix alineado para completar la llegada.
                            NavDataClient.PrefetchAirport(destIcao);
                            var arrRunways2  = NavDataClient.GetRunways(destIcao);
                            var lastStarFix  = wps.LastOrDefault(w =>
                                (w.Stage ?? "CRZ") == "DSC" && w.Type != "apt");

                            if (arrRunways2?.Count > 0 && lastStarFix != null)
                            {
                                var arrRwy2 = FindArrivalRunway(
                                    arrRunways2, destRunway, lastStarFix.Lat, lastStarFix.Lon);

                                if (arrRwy2 != null)
                                {
                                    destThrRwy = arrRwy2;
                                    double apBrg2 = GeodesicBearing(
                                        arrRwy2.ThresholdLat, arrRwy2.ThresholdLon,
                                        arrRwy2.EndLat,       arrRwy2.EndLon);
                                    if (HeadingDiff(apBrg2, arrRwy2.Heading) > 90)
                                        apBrg2 = (apBrg2 + 180) % 360;

                                    double brgLastToThr = GeodesicBearing(
                                        lastStarFix.Lat, lastStarFix.Lon,
                                        arrRwy2.ThresholdLat, arrRwy2.ThresholdLon);

                                    // Solo actuar si el último fix de la STAR no está en final
                                    if (HeadingDiff(apBrg2, brgLastToThr) > 25.0)
                                    {
                                        var nearbyA = NavDataClient.GetAirportWaypoints(destIcao);
                                        double lastDistKm = DistanceKm(
                                            lastStarFix.Lat, lastStarFix.Lon,
                                            arrRwy2.ThresholdLat, arrRwy2.ThresholdLon);

                                        NavAirportWaypoint bestAligned = null;
                                        double bestScore = double.MaxValue;
                                        const double TargetKm    = 10.0 * 1.852; // preferencia: 10 NM
                                        const double MinKm       =  3.0 * 1.852; // mínimo: 3 NM
                                        const double MaxAlignErr = 20.0;         // tolerancia de alineación

                                        foreach (var wp in nearbyA
                                            ?? Enumerable.Empty<NavAirportWaypoint>())
                                        {
                                            double dKm = DistanceKm(
                                                wp.Lat, wp.Lon,
                                                arrRwy2.ThresholdLat, arrRwy2.ThresholdLon);
                                            // Entre 3 NM y la distancia del último fix STAR al umbral
                                            if (dKm < MinKm || dKm > lastDistKm) continue;

                                            double bToThr = GeodesicBearing(
                                                wp.Lat, wp.Lon,
                                                arrRwy2.ThresholdLat, arrRwy2.ThresholdLon);
                                            if (HeadingDiff(apBrg2, bToThr) >= MaxAlignErr) continue;

                                            // Preferir el más cercano a 10 NM del umbral
                                            double score = Math.Abs(dKm - TargetKm);
                                            if (score < bestScore)
                                            {
                                                bestScore   = score;
                                                bestAligned = wp;
                                            }
                                        }

                                        if (bestAligned != null)
                                        {
                                            markers.Add(new FixMarker(
                                                new PointLatLng(bestAligned.Lat, bestAligned.Lon),
                                                bestAligned.Ident, "apfx"));
                                            markers.Add(new FixMarker(
                                                new PointLatLng(arrRwy2.ThresholdLat, arrRwy2.ThresholdLon),
                                                arrRwy2.Name, "rwy"));

                                            noStarAligned        = true;
                                            noStarAlignedLat     = bestAligned.Lat;
                                            noStarAlignedLon     = bestAligned.Lon;
                                            noStarAlignedIdent   = bestAligned.Ident;
                                            noStarAlignedThrLat  = arrRwy2.ThresholdLat;
                                            noStarAlignedThrLon  = arrRwy2.ThresholdLon;
                                            hasArrRwy = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // ── Build master point list (lat, lon, stage, isFlyover) ──
                var allPts = new List<(double Lat, double Lon, string Stage, bool IsFlyover)>();

                // Navlog — omitir apts de salida/llegada cuando ya dibujamos la pista
                foreach (var wp in wps)
                {
                    if (hasDepRwy && wp.Type == "apt" && (wp.Stage ?? "CRZ") == "CLB") continue;
                    if (hasArrRwy && wp.Type == "apt" && (wp.Stage ?? "CRZ") == "DSC") continue;
                    bool fo = flyoverIdents.Contains(wp.Ident ?? "");
                    allPts.Add((wp.Lat, wp.Lon, wp.Stage ?? "CRZ", fo));
                }

                // No-STAR: el último fix del navlog actúa como fly-over.
                // Si hay un waypoint alineado a ~10 NM, se inserta como punto interior
                // para que BuildSmoothedRoutes genere la curva fly-by hacia él y luego
                // llegue a thr5nm ya en el eje de pista. Sin waypoint alineado el
                // comportamiento original se mantiene (last fix fly-over → thr5nm).
                if (noStarThr5 && allPts.Count > 0)
                {
                    int li = allPts.Count - 1;
                    var lp = allPts[li];
                    allPts[li] = (lp.Lat, lp.Lon, lp.Stage, true);   // último fix → fly-over
                    if (noStarNoAligned)
                        allPts.Add((noStarNoAlignedLat, noStarNoAlignedLon, "DSC", false)); // interior → recibe curva
                    allPts.Add((noStarThr5Lat, noStarThr5Lon, "DSC", false));               // thr5nm → endpoint
                }

                // STAR desalineada: último fix → fly-over; fix alineado → interior (recibe curva);
                // umbral → endpoint. BuildSmoothedRoutes aplica fly-by en el fix alineado.
                if (noStarAligned && allPts.Count > 0)
                {
                    int li = allPts.Count - 1;
                    var lp = allPts[li];
                    allPts[li] = (lp.Lat, lp.Lon, lp.Stage, true);
                    allPts.Add((noStarAlignedLat,    noStarAlignedLon,    "DSC", false));
                    allPts.Add((noStarAlignedThrLat, noStarAlignedThrLon, "DSC", false));
                }

                // No-SID: insertar el punto T (ancla de tangencia) antes del primer fix para
                // que BuildSmoothedRoutes pueda generar el arco fly-over con la tangente
                // correcta del arco de salida (inBrg = bearing T→firstFix ≈ tangente del arco).
                if (noSidStub && allPts.Count > 0)
                {
                    var f = allPts[0];
                    allPts[0] = (f.Lat, f.Lon, f.Stage, true);   // firstPlanFix = fly-over
                    allPts.Insert(0, (noSidStubLat, noSidStubLon, "CLB", false));
                }

                // El primer fix del navlog después de la pista es siempre fly-over:
                // se mantiene el rumbo de pista hasta cruzar el fix y luego se vira.
                if (depEnd.HasValue && allPts.Count > 0)
                {
                    var f = allPts[0];
                    allPts[0] = (f.Lat, f.Lon, f.Stage, true);
                }

                // Conectar el final de pista como punto de arranque del navlog suavizado
                if (depEnd.HasValue)
                    allPts.Insert(0, (depEnd.Value.Lat, depEnd.Value.Lon, "CLB", false));

                // ── Arcos DME/RF desde legs AF/RF del SID y la STAR ──────
                // Para cada leg de tipo AF o RF con center_lat/center_lon definidos,
                // reemplazar el segmento recto prevFix→endFix en allPts por los puntos
                // del arco circular, de modo que BuildSmoothedRoutes los dibuje
                // como una curva continua sin giros agudos.
                InterpolateArcLegs(allPts, sidProc, starProc);

                // ── Smooth polyline con giros fly-by / fly-over ───────────
                BuildSmoothedRoutes(allPts, shadowRoutes, colorRoutes);

                // ── Anillos de distancia al umbral de llegada (5 y 10 NM) ──
                if (destThrRwy != null)
                {
                    foreach (double rNm in new[] { 5.0, 10.0 })
                    {
                        var ringPen = new Pen(Color.FromArgb(110, 140, 165, 195), 1.0f)
                            { DashStyle = DashStyle.Dot };
                        colorRoutes.Add(new GMapRoute(
                            ComputeCircle(destThrRwy.ThresholdLat, destThrRwy.ThresholdLon, rNm),
                            $"ring{(int)rNm}") { Stroke = ringPen });
                    }
                }

                // ── Restricciones desde legs de NavData (SID + STAR) ─────
                var restrictions = BuildRestrictionDict(sidProc, starProc);

                // ── Línea punteada destino → alterno ──────────────────────
                double altLat = 0, altLon = 0;
                bool   hasAlt = false;
                if (!string.IsNullOrEmpty(altIcao))
                {
                    try
                    {
                        var destApt = wps.LastOrDefault(w => w.Type == "apt");
                        NavDataClient.PrefetchAirport(altIcao);
                        var altInfo = NavDataClient.GetAirportInfo(altIcao);
                        if (destApt != null && altInfo != null && (altInfo.Lat != 0 || altInfo.Lon != 0))
                        {
                            altLat = altInfo.Lat;
                            altLon = altInfo.Lon;
                            hasAlt = true;
                            var altLine = new List<PointLatLng>
                            {
                                new PointLatLng(destApt.Lat, destApt.Lon),
                                new PointLatLng(altLat, altLon),
                            };
                            var altPen = new Pen(_clrAlt, 1.5f)
                            {
                                DashStyle   = DashStyle.Custom,
                                DashPattern = new float[] { 8f, 5f },
                            };
                            colorRoutes.Add(new GMapRoute(altLine, "alt") { Stroke = altPen });
                            markers.Add(new FixMarker(
                                new PointLatLng(altLat, altLon), altIcao, "apt", null, null));
                        }
                    }
                    catch { }
                }

                // ── Marcadores de fix ─────────────────────────────────────
                foreach (var wp in wps)
                {
                    if (wp.Type == "latlon") continue;
                    string id     = wp.Ident?.ToUpper() ?? "";
                    bool isTodToc = id == "TOD" || id == "TOC" || id == "T/D" || id == "T/C";
                    restrictions.TryGetValue(id, out FixRestriction restr);
                    markers.Add(new FixMarker(
                        new PointLatLng(wp.Lat, wp.Lon), wp.Ident,
                        isTodToc ? "pseudo" : wp.Type, wp.Freq, restr));
                }

                // ── Labels SID / STAR desde NavData ──────────────────────
                AddProcedureLabel(wps, "CLB", resolvedSid,  markers);
                AddProcedureLabel(wps, "DSC", resolvedStar, markers);

                // ── Waypoints ambient cerca del destino y del origen ─────
                var routeIdents = new HashSet<string>(
                    wps.Select(w => w.Ident).Where(id => !string.IsNullOrEmpty(id)),
                    StringComparer.OrdinalIgnoreCase);
                // Fixes con marcador explícito (apfx/rwy) → excluir del layer ambient
                if (!string.IsNullOrEmpty(noStarAlignedIdent))    routeIdents.Add(noStarAlignedIdent);
                if (!string.IsNullOrEmpty(noStarNoAlignedIdent))  routeIdents.Add(noStarNoAlignedIdent);
                if (!string.IsNullOrEmpty(noSidDepWpIdent))       routeIdents.Add(noSidDepWpIdent);

                if (!string.IsNullOrEmpty(destIcao))
                {
                    try
                    {
                        var nearby = NavDataClient.GetAirportWaypoints(destIcao);
                        if (nearby != null)
                        {
                            foreach (var wp in nearby)
                            {
                                if (string.IsNullOrEmpty(wp.Ident)) continue;
                                if (routeIdents.Contains(wp.Ident)) continue;
                                string fixType = MapAmbientType(wp.Type);
                                string freq    = null;
                                if (fixType == "vor" || fixType == "ndb")
                                {
                                    if (wp.FrequencyMhz.HasValue && wp.FrequencyMhz > 0)
                                        freq = wp.FrequencyMhz.Value.ToString(
                                            "000.00", System.Globalization.CultureInfo.InvariantCulture);
                                    else if (wp.FrequencyKhz.HasValue && wp.FrequencyKhz > 0)
                                        freq = ((int)Math.Round(wp.FrequencyKhz.Value)).ToString();
                                }
                                ambientMarkers.Add(new FixMarker(
                                    new PointLatLng(wp.Lat, wp.Lon), wp.Ident, fixType, freq,
                                    null, dimmed: true));
                            }
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(originIcao) && originIcao != destIcao)
                {
                    try
                    {
                        var nearbyOrg = NavDataClient.GetAirportWaypoints(originIcao);
                        if (nearbyOrg != null)
                        {
                            foreach (var wp in nearbyOrg)
                            {
                                if (string.IsNullOrEmpty(wp.Ident)) continue;
                                if (routeIdents.Contains(wp.Ident)) continue;
                                if (wp.DistanceNm > 20.0) continue;            // solo 20 NM alrededor del origen
                                string fixType = MapAmbientType(wp.Type);
                                string freq    = null;
                                if (fixType == "vor" || fixType == "ndb")
                                {
                                    if (wp.FrequencyMhz.HasValue && wp.FrequencyMhz > 0)
                                        freq = wp.FrequencyMhz.Value.ToString(
                                            "000.00", System.Globalization.CultureInfo.InvariantCulture);
                                    else if (wp.FrequencyKhz.HasValue && wp.FrequencyKhz > 0)
                                        freq = ((int)Math.Round(wp.FrequencyKhz.Value)).ToString();
                                }
                                ambientMarkers.Add(new FixMarker(
                                    new PointLatLng(wp.Lat, wp.Lon), wp.Ident, fixType, freq,
                                    null, dimmed: true));
                            }
                        }
                    }
                    catch { }
                }

                // ── Recopilar datos para el sidebar (todos en caché) ─────────────────
                List<NavRunway>    sbOrgRwys = null, sbDstRwys = null;
                List<NavProcedure> sbSids = null, sbStars = null;
                List<NavApproach>  sbApps = null;
                List<NavIls>       sbIls  = null;
                NavAirportInfo     sbOrgInfo = null, sbDstInfo = null;
                try
                {
                    if (!string.IsNullOrEmpty(originIcao))
                    {
                        sbOrgRwys = NavDataClient.GetRunways(originIcao);
                        sbSids    = NavDataClient.GetSids(originIcao);
                        sbOrgInfo = NavDataClient.GetAirportInfo(originIcao);
                    }
                    if (!string.IsNullOrEmpty(destIcao))
                    {
                        sbDstRwys = NavDataClient.GetRunways(destIcao);
                        sbStars   = NavDataClient.GetStars(destIcao);
                        sbApps    = NavDataClient.GetApproaches(destIcao);
                        sbIls     = NavDataClient.GetIls(destIcao);
                        sbDstInfo = NavDataClient.GetAirportInfo(destIcao);
                    }
                }
                catch { }

                // ── Actualizar UI ─────────────────────────────────────────
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed) return;
                    _routeShadowOverlay.Routes.Clear();
                    _routeOverlay.Routes.Clear();
                    _ambientOverlay.Markers.Clear();
                    _waypointOverlay.Markers.Clear();

                    foreach (var r in shadowRoutes)   _routeShadowOverlay.Routes.Add(r);
                    foreach (var r in colorRoutes)    _routeOverlay.Routes.Add(r);
                    foreach (var m in ambientMarkers) _ambientOverlay.Markers.Add(m);
                    foreach (var m in markers)        _waypointOverlay.Markers.Add(m);

                    _ambientOverlay.IsVisibile = (int)_map.Zoom >= 10;
                    _map.Refresh();

                    if (_aircraftMarker == null)
                    {
                        // Zoom-to-fit: calcular bounding box de todos los waypoints
                        double minLat = double.MaxValue, maxLat = double.MinValue;
                        double minLon = double.MaxValue, maxLon = double.MinValue;
                        foreach (var w in wps)
                        {
                            if (w.Lat < minLat) minLat = w.Lat;
                            if (w.Lat > maxLat) maxLat = w.Lat;
                            if (w.Lon < minLon) minLon = w.Lon;
                            if (w.Lon > maxLon) maxLon = w.Lon;
                        }
                        if (hasAlt)
                        {
                            if (altLat < minLat) minLat = altLat;
                            if (altLat > maxLat) maxLat = altLat;
                            if (altLon < minLon) minLon = altLon;
                            if (altLon > maxLon) maxLon = altLon;
                        }
                        double padLat = Math.Max(0.05, (maxLat - minLat) * 0.08);
                        double padLon = Math.Max(0.05, (maxLon - minLon) * 0.08);
                        var fitRect = new RectLatLng(
                            maxLat + padLat,
                            minLon - padLon,
                            (maxLon - minLon) + 2 * padLon,
                            (maxLat - minLat) + 2 * padLat);
                        _map.SetZoomToFitRect(fitRect);
                    }

                    PopulateSidebar(sbOrgRwys, sbDstRwys, sbSids, sbStars,
                        sbApps, sbIls, sbOrgInfo, sbDstInfo);
                    _spinner.StopSpin();
                }));
            });
        }

        // ── SID / STAR procedure label placement ─────────────────────────────────

        private static NavProcedure MatchProcedure(
            IList<string> planIdents, IList<NavProcedure> procedures, string runwayHint,
            string nameHint = null)
        {
            if (procedures == null || procedures.Count == 0) return null;

            // Búsqueda directa por nombre (SimBrief general.sid / general.star)
            if (!string.IsNullOrEmpty(nameHint))
            {
                var direct = procedures.FirstOrDefault(p =>
                    string.Equals(p.Name, nameHint, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(p.Runway) || string.IsNullOrEmpty(runwayHint)
                        || ProcedureAppliesToRunway(p.Runway, runwayHint)));
                if (direct != null) return direct;
                // fallback: coincidencia por nombre ignorando pista
                var byName = procedures.FirstOrDefault(p =>
                    string.Equals(p.Name, nameHint, StringComparison.OrdinalIgnoreCase));
                if (byName != null) return byName;
            }

            if (planIdents == null || planIdents.Count == 0) return null;
            var fixSet = new HashSet<string>(
                planIdents.Where(f => !string.IsNullOrEmpty(f)),
                StringComparer.OrdinalIgnoreCase);
            if (fixSet.Count == 0) return null;

            NavProcedure Scan(bool filterRwy)
            {
                NavProcedure best = null; int top = 0;
                foreach (var p in procedures)
                {
                    if (filterRwy && !string.IsNullOrEmpty(runwayHint) && !string.IsNullOrEmpty(p.Runway)
                        && !ProcedureAppliesToRunway(p.Runway, runwayHint))
                        continue;
                    int score = p.Legs?.Count(
                        l => !string.IsNullOrEmpty(l.Fix) && fixSet.Contains(l.Fix)) ?? 0;
                    if (score > top) { top = score; best = p; }
                }
                return top > 0 ? best : null;
            }

            return Scan(filterRwy: true) ?? Scan(filterRwy: false);
        }

        private static string MapAmbientType(string apiType)
        {
            if (string.IsNullOrEmpty(apiType)) return "wpt";
            if (apiType.StartsWith("VOR", StringComparison.OrdinalIgnoreCase) ||
                apiType.StartsWith("TACAN", StringComparison.OrdinalIgnoreCase))  return "vor";
            if (apiType.StartsWith("NDB", StringComparison.OrdinalIgnoreCase) ||
                apiType.Equals("compass-locator", StringComparison.OrdinalIgnoreCase)) return "ndb";
            return "wpt";
        }

        private static bool ProcedureAppliesToRunway(string procRunway, string runway)
        {
            if (string.IsNullOrEmpty(procRunway)) return true;
            if (string.Equals(procRunway, runway, StringComparison.OrdinalIgnoreCase)) return true;
            // ARINC 424 "B" suffix: "14B" aplica a "14L", "14C" y "14R" simultáneamente
            string prefix = runway.TrimEnd('L', 'R', 'C');
            return string.Equals(procRunway, prefix + "B", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddProcedureLabel(
            List<SimbriefWaypoint> wps, string stage, string name, List<GMapMarker> markers)
        {
            if (string.IsNullOrEmpty(name)) return;
            var fixes = wps
                .Where(w => (w.Stage ?? "CRZ") == stage && w.Type != "apt")
                .ToList();
            if (fixes.Count < 1) return;
            string procName = name;

            if (fixes.Count == 1)
            {
                markers.Add(new RouteLabelMarker(
                    new PointLatLng(fixes[0].Lat, fixes[0].Lon), procName, 0f));
                return;
            }

            for (int i = 0; i < fixes.Count - 1; i++)
            {
                double dN = (fixes[i + 1].Lat - fixes[i].Lat) * 111320;
                double dE = (fixes[i + 1].Lon - fixes[i].Lon) * 111320
                            * Math.Cos(fixes[i].Lat * Math.PI / 180);
                if (Math.Sqrt(dN * dN + dE * dE) < 500.0) continue;  // solo omite puntos duplicados

                double midLat = (fixes[i].Lat + fixes[i + 1].Lat) / 2.0;
                double midLon = (fixes[i].Lon + fixes[i + 1].Lon) / 2.0;

                float screenAngle = (float)(GeodesicBearing(
                    fixes[i].Lat, fixes[i].Lon,
                    fixes[i + 1].Lat, fixes[i + 1].Lon) - 90.0);
                screenAngle = ((screenAngle % 360f) + 360f) % 360f;
                if (screenAngle > 180f) screenAngle -= 360f;
                if (Math.Abs(screenAngle) > 90f)
                    screenAngle = screenAngle > 0 ? screenAngle - 180f : screenAngle + 180f;

                markers.Add(new RouteLabelMarker(
                    new PointLatLng(midLat, midLon), procName, screenAngle));
            }
        }

        /// <summary>
        /// Builds a ident → FixRestriction dict from SID and STAR procedure legs.
        /// Only legs that have at least one restriction (altitude or speed) are included.
        /// </summary>
        private static Dictionary<string, FixRestriction> BuildRestrictionDict(
            NavProcedure sid, NavProcedure star)
        {
            var dict = new Dictionary<string, FixRestriction>(StringComparer.OrdinalIgnoreCase);
            foreach (var proc in new[] { sid, star })
            {
                if (proc?.Legs == null) continue;
                foreach (var leg in proc.Legs)
                {
                    if (string.IsNullOrEmpty(leg.Fix)) continue;
                    if (!leg.AltitudeFt.HasValue && !leg.SpeedKts.HasValue) continue;

                    var r = new FixRestriction
                    {
                        AltFt    = leg.AltitudeFt,
                        Alt2Ft   = leg.Altitude2Ft,
                        AltDescr = leg.AltDescriptor,
                        SpeedKts = leg.SpeedKts,
                        SpdType  = leg.SpeedLimitType,
                    };
                    dict[leg.Fix] = r;   // last proc wins when same ident appears in both
                }
            }
            return dict;
        }

        // ── Pre-procesamiento de arcos DME/RF ────────────────────────────────────────
        //
        // Para cada leg de tipo AF o RF con center_lat/center_lon válidos en sidProc y
        // starProc, busca en allPts el par (prevFix, endFix) correspondiente y reemplaza
        // el segmento recto por los puntos del arco circular. Los puntos interpolados
        // heredan el Stage y IsFlyover = false del fix de fin de arco.
        //
        // Matching de fixes: por proximidad (<500 m) en lugar de por ident, porque el
        // navlog de SimBrief puede usar nombres ligeramente distintos al NavData.

        private static void InterpolateArcLegs(
            List<(double Lat, double Lon, string Stage, bool IsFlyover)> allPts,
            NavProcedure sidProc, NavProcedure starProc)
        {
            foreach (var proc in new[] { sidProc, starProc })
            {
                if (proc?.Legs == null || proc.Legs.Count < 2) continue;

                for (int li = 1; li < proc.Legs.Count; li++)
                {
                    var leg = proc.Legs[li];
                    if (leg.Type != "AF" && leg.Type != "RF") continue;
                    if (!leg.Lat.HasValue || !leg.Lon.HasValue) continue;

                    var prevLeg = proc.Legs[li - 1];
                    if (!prevLeg.Lat.HasValue || !prevLeg.Lon.HasValue) continue;

                    // Coordenadas del centro del arco — primarias desde el leg, fallback al navaid
                    double cLat, cLon;
                    if (leg.CenterLat.HasValue && leg.CenterLon.HasValue)
                    {
                        cLat = leg.CenterLat.Value;
                        cLon = leg.CenterLon.Value;
                    }
                    else if (!string.IsNullOrEmpty(leg.CenterFix))
                    {
                        try
                        {
                            double midLat = (leg.Lat.Value + prevLeg.Lat.Value) / 2.0;
                            double midLon = (leg.Lon.Value + prevLeg.Lon.Value) / 2.0;
                            var navaid = NavDataClient.GetNavaidAsync(
                                leg.CenterFix, midLat, midLon, "vor")
                                .GetAwaiter().GetResult();
                            if (navaid == null) continue;
                            cLat = navaid.Lat;
                            cLon = navaid.Lon;
                        }
                        catch { continue; }
                    }
                    else continue;

                    bool   turnRight = string.Equals(leg.TurnDirection, "R",
                        StringComparison.OrdinalIgnoreCase);
                    double arcEndLat = leg.Lat.Value,      arcEndLon = leg.Lon.Value;
                    double arcStLat  = prevLeg.Lat.Value,  arcStLon  = prevLeg.Lon.Value;

                    // Localizar arcEnd en allPts (<1 km). Sin él no podemos dibujar nada.
                    int endIdx = -1;
                    for (int i = 0; i < allPts.Count; i++)
                        if (DistanceKm(allPts[i].Lat, allPts[i].Lon, arcEndLat, arcEndLon) < 1.0)
                        { endIdx = i; break; }
                    if (endIdx <= 0) continue;

                    // Localizar arcStart en allPts (solo antes de endIdx, <1 km).
                    // Si no está en el navlog, lo insertamos justo antes de arcEnd.
                    int prevIdx = -1;
                    for (int i = 0; i < endIdx; i++)
                        if (DistanceKm(allPts[i].Lat, allPts[i].Lon, arcStLat, arcStLon) < 1.0)
                        { prevIdx = i; break; }

                    if (prevIdx < 0)
                    {
                        string stg = allPts[endIdx].Stage;
                        allPts.Insert(endIdx, (arcStLat, arcStLon, stg, false));
                        prevIdx = endIdx;
                        endIdx  = endIdx + 1;
                    }

                    if (endIdx <= prevIdx) continue;

                    var arcPts = ComputeDmeArc(
                        allPts[prevIdx].Lat, allPts[prevIdx].Lon,
                        cLat, cLon,
                        allPts[endIdx].Lat, allPts[endIdx].Lon,
                        turnRight);
                    if (arcPts == null || arcPts.Count < 3) continue;

                    string stage    = allPts[endIdx].Stage;
                    int insertAt    = prevIdx + 1;
                    int removeCount = endIdx - prevIdx - 1;
                    if (removeCount > 0)
                        allPts.RemoveRange(insertAt, removeCount);

                    for (int k = arcPts.Count - 2; k >= 1; k--)
                        allPts.Insert(insertAt, (arcPts[k].Lat, arcPts[k].Lng, stage, false));
                }
            }
        }

        /// <summary>
        /// Construye rutas suavizadas: en cada waypoint interior sustituye la esquina aguda por
        /// un arco Bézier cuadrático (estilo "fly-by" del ND del avión).
        /// Radio del brazo: CLB ≈ 1.5 NM (2.8 km) · DSC ≈ 2.0 NM (3.7 km) · CRZ ≈ 2.5 NM (4.6 km) · cap 40 % del tramo más corto.
        /// </summary>
        private static void BuildSmoothedRoutes(
            List<(double Lat, double Lon, string Stage, bool IsFlyover)> pts,
            List<GMapRoute> shadows, List<GMapRoute> colors)
        {
            if (pts.Count < 2) return;

            // Pre-calcular puntos de recorte para cada waypoint interior
            var trimBefore = new PointLatLng?[pts.Count];
            var trimAfter  = new PointLatLng?[pts.Count];

            for (int i = 1; i < pts.Count - 1; i++)
            {
                double refLat = pts[i].Lat;
                double cosRef = Math.Cos(refLat * Math.PI / 180);

                double pN   = (pts[i-1].Lat - refLat)    * 111.32;
                double pE   = (pts[i-1].Lon - pts[i].Lon) * 111.32 * cosRef;
                double pLen = Math.Sqrt(pN * pN + pE * pE);

                double nN   = (pts[i+1].Lat - refLat)    * 111.32;
                double nE   = (pts[i+1].Lon - pts[i].Lon) * 111.32 * cosRef;
                double nLen = Math.Sqrt(nN * nN + nE * nE);

                if (pLen < 0.05 || nLen < 0.05) continue;

                double dot = Math.Max(-1.0, Math.Min(1.0,
                    (pN * nN + pE * nE) / (pLen * nLen)));
                if (Math.Acos(dot) * 180.0 / Math.PI < 5.0) continue; // viraje insignificante

                double armKm = pts[i].Stage == "CLB" ? 2.78   // ≈ 1.5 NM
                             : pts[i].Stage == "DSC" ? 3.70   // ≈ 2.0 NM
                             : 4.63;                           // ≈ 2.5 NM (CRZ)
                double arm   = Math.Min(armKm, Math.Min(pLen, nLen) * 0.40);

                trimAfter[i] = new PointLatLng(
                    refLat     + (nN / nLen) * arm / 111.32,
                    pts[i].Lon + (nE / nLen) * arm / (111.32 * cosRef));

                // Fly-over: solo lado saliente; no recortar el tramo entrante
                if (pts[i].IsFlyover) continue;

                trimBefore[i] = new PointLatLng(
                    refLat     + (pN / pLen) * arm / 111.32,
                    pts[i].Lon + (pE / pLen) * arm / (111.32 * cosRef));
            }

            // Construir segmentos coloreados con arcos en las esquinas
            const int ArcSegs = 12;
            var    segPts = new List<PointLatLng> { new PointLatLng(pts[0].Lat, pts[0].Lon) };
            string stage  = pts[0].Stage;

            for (int i = 1; i < pts.Count; i++)
            {
                string newStage = pts[i].Stage;
                var arcStart = trimBefore[i] ?? new PointLatLng(pts[i].Lat, pts[i].Lon);

                // Cambio de stage: cerrar segmento actual y abrir uno nuevo
                if (newStage != stage)
                {
                    segPts.Add(arcStart);
                    if (segPts.Count >= 2) AppendSegment(stage, segPts, shadows, colors);
                    stage  = newStage;
                    segPts = new List<PointLatLng> { arcStart };
                }
                else
                {
                    segPts.Add(arcStart);
                }

                // Arco Bézier cuadrático: t1 → waypoint → t2
                if (trimBefore[i].HasValue)
                {
                    var t1  = trimBefore[i].Value;
                    var t2  = trimAfter[i].Value;
                    var wpt = new PointLatLng(pts[i].Lat, pts[i].Lon);

                    for (int k = 1; k <= ArcSegs; k++)
                    {
                        double t = (double)k / ArcSegs, u = 1.0 - t;
                        segPts.Add(new PointLatLng(
                            u * u * t1.Lat + 2 * u * t * wpt.Lat + t * t * t2.Lat,
                            u * u * t1.Lng + 2 * u * t * wpt.Lng + t * t * t2.Lng));
                    }
                }
                else if (trimAfter[i].HasValue && i + 1 < pts.Count)
                {
                    // Fly-over: Bézier cúbica con tangentes correctas en ambos extremos
                    double inBrg  = GeodesicBearing(pts[i-1].Lat, pts[i-1].Lon,
                                                     pts[i].Lat,   pts[i].Lon);
                    double outBrg = GeodesicBearing(pts[i].Lat,   pts[i].Lon,
                                                     pts[i+1].Lat, pts[i+1].Lon);
                    var t2    = trimAfter[i].Value;
                    var foArc = ComputeTransitionCurve(
                        pts[i].Lat, pts[i].Lon, inBrg,
                        t2.Lat, t2.Lng, outBrg);
                    if (foArc != null)
                        for (int k = 1; k < foArc.Count; k++)
                            segPts.Add(foArc[k]);
                }
            }

            if (segPts.Count >= 2)
                AppendSegment(stage, segPts, shadows, colors);
        }

        private static void AppendSegment(string stage, List<PointLatLng> pts,
            List<GMapRoute> shadows, List<GMapRoute> colors)
        {
            if (pts.Count < 2) return;
            Color color;
            float width;
            switch (stage)
            {
                case "CLB": color = _clrClb; width = 10f; break;
                case "DSC": color = _clrDsc; width = 10f; break;
                default:    color = _clrCrz; width = 3f;  break;
            }
            string id = $"{stage}_{colors.Count}";
            shadows.Add(new GMapRoute(new List<PointLatLng>(pts), "s_" + id)
                { Stroke = new Pen(_clrShadow, width + 4f) });
            colors.Add(new GMapRoute(new List<PointLatLng>(pts), id)
                { Stroke = new Pen(color, width) });
        }

        // ── Bézier de transición (salida sin SID / llegada sin STAR) ─────────────
        //
        // armFraction >= 0: fuerza arm = chord × armFraction (ignora la fórmula cos).
        //   Usar para arcos de salida/llegada donde se necesita visibilidad garantizada.
        // armFraction < 0: fórmula automática cord × 0.40 × max(0.20, cos(T/2)).

        private static List<PointLatLng> ComputeTransitionCurve(
            double startLat, double startLon, double startBrg,
            double endLat,   double endLon,   double endBrg,
            double armFraction = -1.0)
        {
            const int N = 24;
            double cosRef = Math.Cos(startLat * Math.PI / 180);

            double f1N   = (endLat - startLat) * 111320;
            double f1E   = (endLon - startLon) * 111320 * cosRef;
            double chord = Math.Sqrt(f1N * f1N + f1E * f1E);
            if (chord < 1.0) return null;

            double toEnd     = GeodesicBearing(startLat, startLon, endLat, endLon);
            double turnAngle = HeadingDiff(startBrg, toEnd);

            if (turnAngle < 12.0)
                return new List<PointLatLng> {
                    new PointLatLng(startLat, startLon),
                    new PointLatLng(endLat,   endLon)
                };

            double arm;
            if (armFraction >= 0.0)
                arm = chord * armFraction;
            else
            {
                double cosHalf = Math.Cos(turnAngle * Math.PI / 360.0);
                arm = chord * 0.40 * Math.Max(0.20, cosHalf);
            }
            // Cota superior: evita ojales en giros >150° con brazo muy largo
            arm = Math.Min(arm, chord * 0.45);

            double signedDiff  = ((toEnd - startBrg + 540.0) % 360.0) - 180.0;
            double blendFactor = Math.Min(1.0, turnAngle / 90.0) * 0.5;
            double blendedBrg  = (startBrg + signedDiff * blendFactor + 360.0) % 360.0;

            double p1Rad = blendedBrg * Math.PI / 180;
            double p1N   = arm * Math.Cos(p1Rad);
            double p1E   = arm * Math.Sin(p1Rad);

            double p2Rad = endBrg * Math.PI / 180;
            double p2N   = f1N - arm * Math.Cos(p2Rad);
            double p2E   = f1E - arm * Math.Sin(p2Rad);

            var pts = new List<PointLatLng>(N + 1);
            for (int i = 0; i <= N; i++)
            {
                double u = (double)i / N, v = 1 - u;
                double n = 3*v*v*u * p1N + 3*v*u*u * p2N + u*u*u * f1N;
                double e = 3*v*v*u * p1E + 3*v*u*u * p2E + u*u*u * f1E;
                pts.Add(new PointLatLng(
                    startLat + n / 111320,
                    startLon + e / (111320 * cosRef)));
            }
            return pts;
        }

        // ── Arco DME / RF: circunferencia constante alrededor de un centro ──────────
        //
        // Genera los puntos del arco desde startPt hasta endPt, pasando por la
        // circunferencia que pasa por ambos y cuyo centro es (centerLat, centerLon).
        // El radio se recalcula desde el centro hasta startPt para evitar error
        // de redondeo cuando distance_nm del API no coincide exactamente.
        private static List<PointLatLng> ComputeCircle(
            double centerLat, double centerLon, double radiusNm, int steps = 72)
        {
            var pts   = new List<PointLatLng>(steps + 1);
            double R  = radiusNm * 1852.0;
            double cr = Math.Cos(centerLat * Math.PI / 180.0);
            for (int i = 0; i <= steps; i++)
            {
                double a = i * 2.0 * Math.PI / steps;
                pts.Add(new PointLatLng(
                    centerLat + (R * Math.Cos(a)) / 111320.0,
                    centerLon + (R * Math.Sin(a)) / (111320.0 * cr)));
            }
            return pts;
        }

        // turnRight determina el sentido; si turnRight==false gira a la izquierda.
        // Devuelve null si los datos son incoherentes (centro ausente, radio < 0.1 NM).

        private static List<PointLatLng> ComputeDmeArc(
            double startLat, double startLon,
            double centerLat, double centerLon,
            double endLat,   double endLon,
            bool   turnRight)
        {
            const double DegPerSeg = 5.0;

            double cosRef = Math.Cos(centerLat * Math.PI / 180.0);

            // Vectores planos (en metros) del centro a cada punto
            double sN = (startLat - centerLat) * 111320.0;
            double sE = (startLon - centerLon) * 111320.0 * cosRef;
            double eN = (endLat   - centerLat) * 111320.0;
            double eE = (endLon   - centerLon) * 111320.0 * cosRef;

            double R = Math.Sqrt(sN * sN + sE * sE);
            if (R < 0.1 * 1852.0) return null;   // radio < 0.1 NM — degenerado

            // Ángulos (convención geográfica: 0 = norte, + este)
            double aStart = Math.Atan2(sE, sN) * 180.0 / Math.PI;
            double aEnd   = Math.Atan2(eE, eN) * 180.0 / Math.PI;

            // Ángulo de barrido en el sentido del giro
            double sweep = turnRight
                ? ((aEnd - aStart) + 360.0) % 360.0
                : ((aStart - aEnd) + 360.0) % 360.0;

            if (sweep < 0.5) sweep = 360.0;    // arco completo si son casi coincidentes

            int segs = Math.Max(2, (int)Math.Ceiling(sweep / DegPerSeg));
            var pts = new List<PointLatLng>(segs + 1);
            pts.Add(new PointLatLng(startLat, startLon));

            for (int i = 1; i <= segs; i++)
            {
                double frac = (double)i / segs;
                double angle = turnRight
                    ? aStart + frac * sweep
                    : aStart - frac * sweep;
                double rad = angle * Math.PI / 180.0;
                pts.Add(new PointLatLng(
                    centerLat + R * Math.Cos(rad) / 111320.0,
                    centerLon + R * Math.Sin(rad) / (111320.0 * cosRef)));
            }

            // Sustituir el último punto por las coords exactas del fix de fin de arco
            pts[pts.Count - 1] = new PointLatLng(endLat, endLon);
            return pts;
        }

        // ── Arco circular de salida (radio fijo, tangente al primer fix del plan) ──
        //
        // Desde startPt con tangente startBrg traza un arco de radio radiusNm hasta
        // que la tangente del arco apunta a targetPt (construcción de tangente exterior
        // desde punto externo al círculo), luego añade recta hasta targetPt.
        // Devuelve null si el target está dentro o muy cerca del círculo (D < R×1.05)
        // o si el arco resultaría > 200° (geometría degenerada).

        private static List<PointLatLng> ComputeDepartureArc(
            double startLat, double startLon, double startBrg,
            double targetLat, double targetLon,
            double radiusNm = 2.5)
        {
            double cosRef = Math.Cos(startLat * Math.PI / 180.0);
            double R = radiusNm * 1852.0;           // metros

            // Coordenadas planas locales del target (N = norte, E = este, metros)
            double tN = (targetLat - startLat) * 111320.0;
            double tE = (targetLon - startLon) * 111320.0 * cosRef;

            // Sentido del giro: producto vectorial dirección_salida × dirección_target
            double depRad = startBrg * Math.PI / 180.0;
            double cross  = Math.Cos(depRad) * tE - Math.Sin(depRad) * tN;
            bool turnRight = cross > 0;

            // Centro del círculo (perpendicular al rumbo de salida)
            double perpRad = (startBrg + (turnRight ? 90.0 : -90.0)) * Math.PI / 180.0;
            double cN = R * Math.Cos(perpRad);
            double cE = R * Math.Sin(perpRad);

            // Distancia del centro al target
            double dtN = tN - cN, dtE = tE - cE;
            double d   = Math.Sqrt(dtN * dtN + dtE * dtE);
            if (d < R * 1.05) return null;          // target dentro o junto al círculo

            // Semiángulo de apertura de la tangente exterior (ángulo C→target vs C→T)
            double halfAngleDeg = Math.Acos(Math.Min(1.0, R / d)) * 180.0 / Math.PI;

            // Rumbo de C hacia el target (convención geográfica: 0=N, +E)
            double brgCtoQ = (Math.Atan2(dtE, dtN) * 180.0 / Math.PI + 360.0) % 360.0;

            // Dos puntos de tangencia
            double T1brg = (brgCtoQ + halfAngleDeg + 360.0) % 360.0;
            double T2brg = (brgCtoQ - halfAngleDeg + 360.0) % 360.0;

            // Rumbo de C hacia el punto de inicio (P0 = origen local)
            double brgCtoP0 = (Math.Atan2(-cE, -cN) * 180.0 / Math.PI + 360.0) % 360.0;

            // Ángulo de arco hasta cada punto de tangencia (en el sentido del giro)
            double arc1 = turnRight
                ? (T1brg - brgCtoP0 + 360.0) % 360.0
                : (brgCtoP0 - T1brg + 360.0) % 360.0;
            double arc2 = turnRight
                ? (T2brg - brgCtoP0 + 360.0) % 360.0
                : (brgCtoP0 - T2brg + 360.0) % 360.0;

            // Elegir el punto de tangencia alcanzado primero (menor ángulo de arco)
            double exitBrg = arc1 <= arc2 ? T1brg : T2brg;
            double arcDeg  = Math.Min(arc1, arc2);
            if (arcDeg < 1.0 || arcDeg > 200.0) return null;

            // Generar puntos del arco (~5° por segmento, mínimo 4 segmentos)
            int segs = Math.Max(4, (int)Math.Ceiling(arcDeg / 5.0));
            var pts = new List<PointLatLng>(segs + 2);
            pts.Add(new PointLatLng(startLat, startLon));

            for (int i = 1; i <= segs; i++)
            {
                double frac   = (double)i / segs;
                double brgRad = turnRight
                    ? (brgCtoP0 + frac * arcDeg) * Math.PI / 180.0
                    : (brgCtoP0 - frac * arcDeg) * Math.PI / 180.0;

                double pN = cN + R * Math.Cos(brgRad);
                double pE = cE + R * Math.Sin(brgRad);
                pts.Add(new PointLatLng(
                    startLat + pN / 111320.0,
                    startLon + pE / (111320.0 * cosRef)));
            }

            // Recta tangente al target (la tangente de salida del arco apunta exactamente a él)
            pts.Add(new PointLatLng(targetLat, targetLon));
            return pts;
        }

        // ── Tramo físico de pista (helper compartido salida/llegada) ──────────────

        private static void DrawRunwaySegment(
            NavRunway rwy,
            List<GMapRoute> shadows, List<GMapRoute> colors,
            List<GMapMarker> markers, Color clr)
        {
            var seg = new List<PointLatLng> {
                new PointLatLng(rwy.ThresholdLat, rwy.ThresholdLon),
                new PointLatLng(rwy.EndLat,       rwy.EndLon)
            };
            shadows.Add(new GMapRoute(seg, "s_rwy_" + rwy.Name) { Stroke = new Pen(_clrShadow, 4.5f) });
            colors.Add(new GMapRoute(seg,  "rwy_"   + rwy.Name) { Stroke = new Pen(clr, 2.5f) });
            markers.Add(new FixMarker(
                new PointLatLng(rwy.ThresholdLat, rwy.ThresholdLon),
                rwy.Name, "rwy"));
        }

        // ── Runway selection for departure ────────────────────────────────────────

        private static NavRunway FindDepartureRunway(
            List<NavRunway> runways, string planRwy, SimbriefWaypoint firstFix)
        {
            if (!string.IsNullOrEmpty(planRwy))
            {
                var match = runways.Find(r =>
                    string.Equals(r.Name?.Trim(), planRwy.Trim(),
                        StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            NavRunway best    = null;
            double    bestDif = double.MaxValue;
            foreach (var rwy in runways)
            {
                double depBrg = GeodesicBearing(
                    rwy.ThresholdLat, rwy.ThresholdLon, rwy.EndLat, rwy.EndLon);
                if (HeadingDiff(depBrg, rwy.Heading) > 90)
                    depBrg = (depBrg + 180) % 360;

                double toFix = GeodesicBearing(
                    rwy.ThresholdLat, rwy.ThresholdLon, firstFix.Lat, firstFix.Lon);
                double diff  = HeadingDiff(depBrg, toFix);
                if (diff < bestDif) { bestDif = diff; best = rwy; }
            }
            return bestDif < 90 ? best : null;
        }

        private static NavRunway FindArrivalRunway(
            List<NavRunway> runways, string planRwy,
            double lastFixLat, double lastFixLon)
        {
            if (!string.IsNullOrEmpty(planRwy))
            {
                var match = runways.Find(r =>
                    string.Equals(r.Name?.Trim(), planRwy.Trim(),
                        StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // Selecciona la pista cuyo approachBrg sea más próximo al bearing
            // desde el último fix hasta el umbral de esa pista
            NavRunway best    = null;
            double    bestDif = double.MaxValue;
            foreach (var rwy in runways)
            {
                double depBrg = GeodesicBearing(
                    rwy.ThresholdLat, rwy.ThresholdLon, rwy.EndLat, rwy.EndLon);
                if (HeadingDiff(depBrg, rwy.Heading) > 90)
                    depBrg = (depBrg + 180) % 360;

                double toThreshold = GeodesicBearing(
                    lastFixLat, lastFixLon, rwy.ThresholdLat, rwy.ThresholdLon);
                double diff = HeadingDiff(depBrg, toThreshold);
                if (diff < bestDif) { bestDif = diff; best = rwy; }
            }
            return bestDif < 90 ? best : null;
        }

        private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double cosRef = Math.Cos(lat1 * Math.PI / 180.0);
            double dN = (lat2 - lat1) * 111.32;
            double dE = (lon2 - lon1) * 111.32 * cosRef;
            return Math.Sqrt(dN * dN + dE * dE);
        }

        private static double GeodesicBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double φ1 = lat1 * Math.PI / 180, φ2 = lat2 * Math.PI / 180;
            double Δλ = (lon2 - lon1) * Math.PI / 180;
            double y  = Math.Sin(Δλ) * Math.Cos(φ2);
            double x  = Math.Cos(φ1) * Math.Sin(φ2) - Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);
            return ((Math.Atan2(y, x) * 180 / Math.PI) + 360) % 360;
        }

        private static double HeadingDiff(double a, double b)
        {
            double d = Math.Abs(a - b) % 360;
            return d > 180 ? 360 - d : d;
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
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed) return;

                var pos = new PointLatLng(lat, lon);

                if (_aircraftMarker == null)
                {
                    _aircraftMarker = new AircraftMarker(pos, heading);
                    _aircraftOverlay.Markers.Add(_aircraftMarker);
                    _map.Zoom = 14;
                }
                else
                {
                    _aircraftMarker.Position = pos;
                    _aircraftMarker.Heading  = heading;
                }

                if (_followAircraft)
                    _map.Position = pos;

                _map.Invalidate();

                _lblStatus.Text =
                    $"  {lat:F4}°  {lon:F4}°   HDG {heading:F0}°  Z:{(int)_map.Zoom}";
            }));
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // ── Sidebar de procedimientos ────────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════════════

        private void BuildSidebar()
        {
            const int W        = 230;
            const int TW       = 18;
            const int ItemW    = W - TW - 12;   // 6 px padding cada lado

            _sidebarPanel = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = W,
                BackColor = Color.FromArgb(14, 20, 28),
            };

            _btnToggleSidebar = new Button
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
            _btnToggleSidebar.FlatAppearance.BorderSize = 0;
            _btnToggleSidebar.Click += (s, e) =>
            {
                _sidebarExpanded        = !_sidebarExpanded;
                _sidebarContent.Visible = _sidebarExpanded;
                _sidebarPanel.Width     = _sidebarExpanded ? W : TW;
                _btnToggleSidebar.Text  = _sidebarExpanded ? "◀" : "▶";
            };

            _sidebarContent = new Panel
            {
                Dock       = DockStyle.Fill,
                BackColor  = Color.FromArgb(14, 20, 28),
                AutoScroll = true,
            };

            // Construye controles de top a bottom mediante coordenadas absolutas
            int y = 8;
            Action<Control, int> place = (ctl, h) =>
            {
                ctl.Location = new Point(6, y);
                ctl.Width    = ItemW;
                if (h > 0) ctl.Height = h;
                _sidebarContent.Controls.Add(ctl);
                y += ctl.Height + 3;
            };

            // ── ORIGIN ──────────────────────────────────────────────────────────────
            place(MakeSectionHeader("ORIGIN"), 18);
            _lblOriginAirport = new Label
            {
                Text = "—", ForeColor = Color.White,
                Font = new Font("Consolas", 8, FontStyle.Bold),
                AutoSize = false,
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

            place(MakeSideLabel("Trans."), 14);
            _cmbSidTrans = MakeSideCombo();
            place(_cmbSidTrans, 22);

            y += 10;

            // ── DESTINATION ─────────────────────────────────────────────────────────
            place(MakeSectionHeader("DESTINATION"), 18);
            _lblDestAirport = new Label
            {
                Text = "—", ForeColor = Color.White,
                Font = new Font("Consolas", 8, FontStyle.Bold),
                AutoSize = false,
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

            place(MakeSideLabel("Trans."), 14);
            _cmbStarTrans = MakeSideCombo();
            place(_cmbStarTrans, 22);

            place(MakeSideLabel("Approach"), 14);
            _cmbApproach = MakeSideCombo();
            place(_cmbApproach, 22);

            _lblApproachCount = new Label
            {
                Text = "", ForeColor = Color.FromArgb(130, 160, 195),
                Font = new Font("Consolas", 7), AutoSize = false,
            };
            place(_lblApproachCount, 14);

            var lnkChart = new LinkLabel
            {
                Text      = "📋 APPROACH CHART",
                AutoSize  = true,
                Font      = new Font("Consolas", 7, FontStyle.Regular),
                LinkColor = Color.FromArgb(80, 160, 220),
                Padding   = new Padding(0, 3, 0, 3),
            };
            lnkChart.LinkClicked += (s, e) => OpenApproachChart();
            place(lnkChart, 18);

            // Conectar eventos
            _cmbOriginRwy.SelectedIndexChanged += OnOriginRunwayChanged;
            _cmbSid.SelectedIndexChanged       += OnSidChanged;
            _cmbSidTrans.SelectedIndexChanged  += OnSidTransChanged;
            _cmbDestRwy.SelectedIndexChanged   += OnDestRunwayChanged;
            _cmbStar.SelectedIndexChanged      += OnStarChanged;
            _cmbStarTrans.SelectedIndexChanged += OnStarTransChanged;
            _cmbApproach.SelectedIndexChanged  += OnApproachChanged;

            _sidebarPanel.Controls.Add(_sidebarContent);
            _sidebarPanel.Controls.Add(_btnToggleSidebar);
        }

        private static Label MakeSectionHeader(string text)
        {
            return new Label
            {
                Text      = text,
                ForeColor = Color.FromArgb(0, 180, 255),
                Font      = new Font("Consolas", 8, FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
        }

        private static Label MakeSideLabel(string text)
        {
            return new Label
            {
                Text      = text,
                ForeColor = Color.FromArgb(140, 160, 180),
                Font      = new Font("Consolas", 7),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
        }

        private static ComboBox MakeSideCombo()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Color.FromArgb(25, 35, 48),
                ForeColor     = Color.White,
                Font          = new Font("Consolas", 8),
            };
        }

        // ── PopulateSidebar ──────────────────────────────────────────────────────────

        private void PopulateSidebar(
            List<NavRunway>    originRunways, List<NavRunway>    destRunways,
            List<NavProcedure> sids,          List<NavProcedure> stars,
            List<NavApproach>  approaches,    List<NavIls>       ils,
            NavAirportInfo     originInfo,    NavAirportInfo     destInfo)
        {
            if (_sidebarPanel == null || IsDisposed || !IsHandleCreated) return;

            _sbOriginRunways = originRunways;
            _sbDestRunways   = destRunways;
            _sbSids          = sids;
            _sbStars         = stars;
            _sbApproaches    = approaches;
            _sbIls           = ils;

            _populatingSidebar = true;
            try
            {
                if (originInfo != null && !string.IsNullOrEmpty(_currentOriginIcao))
                {
                    string n = originInfo.Name ?? "";
                    if (n.Length > 18) n = n.Substring(0, 18);
                    _lblOriginAirport.Text = $"{_currentOriginIcao}  {n}";
                }
                else
                    _lblOriginAirport.Text = _currentOriginIcao ?? "—";

                if (destInfo != null && !string.IsNullOrEmpty(_currentDestIcao))
                {
                    string n = destInfo.Name ?? "";
                    if (n.Length > 18) n = n.Substring(0, 18);
                    _lblDestAirport.Text = $"{_currentDestIcao}  {n}";
                }
                else
                    _lblDestAirport.Text = _currentDestIcao ?? "—";

                FillRunwayCombo(_cmbOriginRwy, originRunways, ref _selOriginRunway);
                FillProcBaseCombo(_cmbSid, sids, _selOriginRunway, ref _selSidName);
                FillProcTransCombo(_cmbSidTrans, sids, _selSidName, ref _selSidTransition);

                FillRunwayCombo(_cmbDestRwy, destRunways, ref _selDestRunway);
                FillProcBaseCombo(_cmbStar, stars, _selDestRunway, ref _selStarName);
                FillProcTransCombo(_cmbStarTrans, stars, _selStarName, ref _selStarTransition);
                FillApproachCombo(_cmbApproach, approaches, _selDestRunway, ref _selApproachKey);

                int appCount = approaches?.Count(a => RunwayMatchesApproach(a, _selDestRunway)) ?? 0;
                _lblApproachCount.Text = appCount > 0
                    ? $"{appCount} approach{(appCount > 1 ? "es" : "")} available"
                    : "";

                UpdateWindLabel(_lblOriginWind, originRunways, _selOriginRunway,
                    _metarOriginWindDir, _metarOriginWindSpd);
                UpdateWindLabel(_lblDestWind, destRunways, _selDestRunway,
                    _metarDestWindDir, _metarDestWindSpd);
            }
            finally
            {
                _populatingSidebar = false;
            }
        }

        // ── Combo fill helpers ───────────────────────────────────────────────────────

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
            ComboBox cmb, List<NavProcedure> procs, string runwayFilter,
            ref string selection)
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
                    .OrderBy(a => a.Type).ThenBy(a => a.Suffix ?? ""))
                {
                    string key   = $"{a.Type}{a.Suffix ?? ""}_{a.Runway ?? ""}";
                    string label = string.IsNullOrEmpty(a.Suffix)
                        ? $"{a.Type} {a.Runway}"
                        : $"{a.Type}{a.Suffix} {a.Runway}";
                    cmb.Items.Add(new ApproachItem(key, label));
                }
            }

            if (!string.IsNullOrEmpty(cur))
            {
                for (int i = 1; i < cmb.Items.Count; i++)
                {
                    if ((cmb.Items[i] as ApproachItem)?.Key == cur)
                    { cmb.SelectedIndex = i; return; }
                }
            }
            cmb.SelectedIndex = 0;
            selection = null;
        }

        private static void SelectOrDefault(ComboBox cmb, string value, int defaultIndex)
        {
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < cmb.Items.Count; i++)
                {
                    if (string.Equals(cmb.Items[i]?.ToString(), value,
                            StringComparison.OrdinalIgnoreCase))
                    { cmb.SelectedIndex = i; return; }
                }
            }
            cmb.SelectedIndex = defaultIndex;
        }

        private static string SelectedRunwayName(ComboBox cmb)
            => cmb.SelectedIndex > 0 ? cmb.SelectedItem as string : null;

        // ── Wind chip update ─────────────────────────────────────────────────────────

        private static void UpdateWindLabel(
            Label lbl, List<NavRunway> runways, string runwayName,
            int? windDir, int? windSpd)
        {
            if (windDir == null || windSpd == null || windSpd == 0
                || string.IsNullOrEmpty(runwayName) || runways == null)
            {
                lbl.Text = "";
                return;
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

        // ── SetMetarData (called from MainForm) ──────────────────────────────────────

        internal void SetAirspaces(IList<NavAirspace> airspaces)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => SetAirspaces(airspaces))); return; }

            _airspaceOverlay.Polygons.Clear();
            if (airspaces == null) return;

            foreach (var a in airspaces)
            {
                if (a.Geometry?.Coordinates == null || a.Geometry.Coordinates.Count == 0) continue;
                var ring = a.Geometry.Coordinates[0];
                if (ring == null || ring.Count < 3) continue;

                // GeoJSON: [lon, lat] → GMap.NET: PointLatLng(lat, lon)
                var pts = ring.Select(p => new PointLatLng(p[1], p[0])).ToList();

                Color fill, stroke;
                float strokeW = 1.5f;
                switch (a.Type)
                {
                    case "Prohibited": fill = Color.FromArgb(20, 220, 0,   0);   stroke = Color.FromArgb(95,  200, 0,   0);   break;
                    case "Restricted": fill = Color.FromArgb(17, 255, 100, 0);   stroke = Color.FromArgb(85,  220, 80,  0);   break;
                    case "Danger":     fill = Color.FromArgb(17, 220, 190, 0);   stroke = Color.FromArgb(80,  180, 150, 0);   break;
                    case "CTR":        fill = Color.FromArgb(12, 0,   180, 255); stroke = Color.FromArgb(70,  0,   160, 230); break;
                    case "TMA":        fill = Color.FromArgb( 7, 0,   100, 210); stroke = Color.FromArgb(55,  0,   90,  190); strokeW = 1.0f; break;
                    case "ATZ":        fill = Color.FromArgb(10, 100, 200, 255); stroke = Color.FromArgb(60,  80,  180, 240); strokeW = 1.0f; break;
                    case "RMZ":        fill = Color.FromArgb( 7, 180, 100, 220); stroke = Color.FromArgb(55,  160, 80,  200); strokeW = 1.0f; break;
                    default:           fill = Color.FromArgb( 5, 150, 150, 150); stroke = Color.FromArgb(40,  120, 120, 120); strokeW = 1.0f; break;
                }

                var poly = new GMapPolygon(pts, a.Name ?? a.Type)
                {
                    Fill   = new SolidBrush(fill),
                    Stroke = new Pen(stroke, strokeW),
                };
                _airspaceOverlay.Polygons.Add(poly);
            }

            _map.Refresh();
        }

        internal void SetAircraftCategory(FsuipcService.AircraftCategory cat)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => SetAircraftCategory(cat))); return; }
            if (_aircraftMarker == null) return;
            _aircraftMarker.Category = cat;
            _map.Refresh();
        }

        internal void SetAtcStations(IList<IvaoAtcStation> stations)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => SetAtcStations(stations))); return; }

            _atcOverlay.Markers.Clear();
            _atcOverlay.Polygons.Clear();
            if (stations == null || stations.Count == 0) { _map.Refresh(); return; }

            foreach (var grp in stations.GroupBy(s => s.Icao, StringComparer.OrdinalIgnoreCase))
            {
                var all     = grp.ToList();
                var nonAtis = all.Where(s => s.Position != "ATIS").ToList();
                if (nonAtis.Count == 0) continue;

                var info = NavDataClient.GetAirportInfo(grp.Key);
                if (info == null || (info.Lat == 0 && info.Lon == 0)) continue;

                double lat = info.Lat, lon = info.Lon;
                var coord  = new PointLatLng(lat, lon);
                var local  = nonAtis.Where(s => IsLocalAtcPos(s.Position)).ToList();
                var area   = nonAtis.Where(s => !IsLocalAtcPos(s.Position)).ToList();
                var atis   = all.Where(s => s.Position == "ATIS").ToList();

                if (local.Count > 0)
                {
                    bool hasTwr = local.Any(s => s.Position == "TWR");
                    bool hasGnd = local.Any(s => s.Position == "GND");
                    bool hasDel = local.Any(s => s.Position == "DEL");
                    const double R = 20.0;

                    // Z-order: TWR (bottom) → GND → DEL (top)
                    if (hasTwr) _atcOverlay.Polygons.Add(MakeCirclePolygon(lat, lon, R,
                        Color.FromArgb( 30, 220,  50,  50),
                        new Pen(Color.FromArgb(170, 220,  50,  50), 1.5f)));
                    if (hasGnd) _atcOverlay.Polygons.Add(MakeStarPolygon(lat, lon, R, 0.38,  0.0,
                        Color.FromArgb( 30, 220, 190,   0),
                        new Pen(Color.FromArgb(170, 220, 190,   0), 1.5f)));
                    if (hasDel) _atcOverlay.Polygons.Add(MakeStarPolygon(lat, lon, R, 0.38, 45.0,
                        Color.FromArgb( 30, 255, 130,   0),
                        new Pen(Color.FromArgb(170, 255, 130,   0), 1.5f)));

                    _atcOverlay.Markers.Add(new AtcLabelMarker(coord, grp.Key, local, atis));
                }
                if (area.Count > 0)
                    _atcOverlay.Markers.Add(new AtcStationMarker(coord, grp.Key, area));
            }

            _map.Refresh();
        }

        private static bool IsLocalAtcPos(string pos) =>
            pos == "DEL" || pos == "GND" || pos == "TWR";

        private static GMapPolygon MakeCirclePolygon(double lat, double lon, double radiusNm,
                                                      Color fill, Pen stroke, int n = 72)
        {
            double latR = lat * Math.PI / 180.0;
            double dLat = radiusNm / 60.0;
            double dLon = radiusNm / 60.0 / Math.Cos(latR);
            var pts = new List<PointLatLng>(n);
            for (int i = 0; i < n; i++)
            {
                double a = 2.0 * Math.PI * i / n;
                pts.Add(new PointLatLng(lat + dLat * Math.Sin(a), lon + dLon * Math.Cos(a)));
            }
            return new GMapPolygon(pts, "atc_circle") { Fill = new SolidBrush(fill), Stroke = stroke };
        }

        private static GMapPolygon MakeStarPolygon(double lat, double lon, double outerNm,
                                                    double innerRatio, double startDeg, Color fill, Pen stroke)
        {
            double latR    = lat * Math.PI / 180.0;
            double outerLat = outerNm / 60.0;
            double outerLon = outerNm / 60.0 / Math.Cos(latR);
            double innerLat = outerLat * innerRatio;
            double innerLon = outerLon * innerRatio;
            var pts = new List<PointLatLng>(8);
            for (int i = 0; i < 8; i++)
            {
                double bearing = (startDeg + i * 45.0) * Math.PI / 180.0;
                bool   isOuter = (i % 2 == 0);
                double dLa = (isOuter ? outerLat : innerLat) * Math.Cos(bearing);
                double dLo = (isOuter ? outerLon : innerLon) * Math.Sin(bearing);
                pts.Add(new PointLatLng(lat + dLa, lon + dLo));
            }
            return new GMapPolygon(pts, "atc_star") { Fill = new SolidBrush(fill), Stroke = stroke };
        }

        public void SetMetarData(int? originWindDir, int? originWindSpeedKt,
                                 int? destWindDir,   int? destWindSpeedKt)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed) return;
                _metarOriginWindDir = originWindDir;
                _metarOriginWindSpd = originWindSpeedKt;
                _metarDestWindDir   = destWindDir;
                _metarDestWindSpd   = destWindSpeedKt;
                if (_sidebarPanel == null) return;
                UpdateWindLabel(_lblOriginWind, _sbOriginRunways, _selOriginRunway,
                    _metarOriginWindDir, _metarOriginWindSpd);
                UpdateWindLabel(_lblDestWind, _sbDestRunways, _selDestRunway,
                    _metarDestWindDir, _metarDestWindSpd);
            }));
        }

        // ── RedrawRoute ──────────────────────────────────────────────────────────────

        private void RedrawRoute()
        {
            if (_currentWaypoints == null) return;
            OnProcedureChanged?.Invoke(
                _selOriginRunway, _selSidName, _selDestRunway, _selStarName);
            LoadRoute(_currentWaypoints,
                _currentOriginIcao, _selOriginRunway,
                _currentDestIcao,   _selDestRunway,
                _currentAltIcao,
                _selSidName,        _selStarName);
        }

        // ── Combo event handlers ─────────────────────────────────────────────────────

        private void OnOriginRunwayChanged(object sender, EventArgs e)
        {
            if (_populatingSidebar || _sbSids == null) return;
            string newRwy = SelectedRunwayName(_cmbOriginRwy);
            if (newRwy == _selOriginRunway) return;

            if (!string.IsNullOrEmpty(_selSidName) &&
                !GetProcBaseNames(_sbSids, newRwy)
                    .Contains(_selSidName, StringComparer.OrdinalIgnoreCase))
            {
                string msg = $"Changing to runway {newRwy ?? "(none)"} makes SID " +
                             $"[{_selSidName}] incompatible.\nClear SID and continue?";
                if (EcamDialog.Show(this, msg, "RUNWAY CHANGE", EcamDialogButtons.YesNo)
                    != DialogResult.Yes)
                {
                    _populatingSidebar = true;
                    SelectOrDefault(_cmbOriginRwy, _selOriginRunway, 0);
                    _populatingSidebar = false;
                    return;
                }
                _selSidName       = null;
                _selSidTransition = null;
            }

            _selOriginRunway   = newRwy;
            _populatingSidebar = true;
            FillProcBaseCombo(_cmbSid, _sbSids, _selOriginRunway, ref _selSidName);
            FillProcTransCombo(_cmbSidTrans, _sbSids, _selSidName, ref _selSidTransition);
            UpdateWindLabel(_lblOriginWind, _sbOriginRunways, _selOriginRunway,
                _metarOriginWindDir, _metarOriginWindSpd);
            _populatingSidebar = false;
            RedrawRoute();
        }

        private void OnDestRunwayChanged(object sender, EventArgs e)
        {
            if (_populatingSidebar || _sbStars == null) return;
            string newRwy = SelectedRunwayName(_cmbDestRwy);
            if (newRwy == _selDestRunway) return;

            if (!string.IsNullOrEmpty(_selStarName) &&
                !GetProcBaseNames(_sbStars, newRwy)
                    .Contains(_selStarName, StringComparer.OrdinalIgnoreCase))
            {
                string msg = $"Changing to runway {newRwy ?? "(none)"} makes STAR " +
                             $"[{_selStarName}] incompatible and clears the approach.\nContinue?";
                if (EcamDialog.Show(this, msg, "RUNWAY CHANGE", EcamDialogButtons.YesNo)
                    != DialogResult.Yes)
                {
                    _populatingSidebar = true;
                    SelectOrDefault(_cmbDestRwy, _selDestRunway, 0);
                    _populatingSidebar = false;
                    return;
                }
                _selStarName        = null;
                _selStarTransition  = null;
            }

            _selDestRunway  = newRwy;
            _selApproachKey = null;
            _populatingSidebar = true;
            FillProcBaseCombo(_cmbStar, _sbStars, _selDestRunway, ref _selStarName);
            FillProcTransCombo(_cmbStarTrans, _sbStars, _selStarName, ref _selStarTransition);
            FillApproachCombo(_cmbApproach, _sbApproaches, _selDestRunway, ref _selApproachKey);
            int appCount = _sbApproaches?
                .Count(a => RunwayMatchesApproach(a, _selDestRunway)) ?? 0;
            _lblApproachCount.Text = appCount > 0
                ? $"{appCount} approach{(appCount > 1 ? "es" : "")} available"
                : "";
            UpdateWindLabel(_lblDestWind, _sbDestRunways, _selDestRunway,
                _metarDestWindDir, _metarDestWindSpd);
            _populatingSidebar = false;
            ClearApproachOverlay();
            RedrawRoute();
        }

        private void OnSidChanged(object sender, EventArgs e)
        {
            if (_populatingSidebar) return;
            string newSid = _cmbSid.SelectedIndex > 0 ? _cmbSid.SelectedItem as string : null;
            if (newSid == _selSidName) return;
            _selSidName       = newSid;
            _selSidTransition = null;
            _populatingSidebar = true;
            FillProcTransCombo(_cmbSidTrans, _sbSids, _selSidName, ref _selSidTransition);
            _populatingSidebar = false;
            RedrawRoute();
        }

        private void OnSidTransChanged(object sender, EventArgs e)
        {
            if (_populatingSidebar) return;
            string newTrans = _cmbSidTrans.SelectedIndex > 0
                ? _cmbSidTrans.SelectedItem as string : null;
            if (newTrans == _selSidTransition) return;
            _selSidTransition = newTrans;
            RedrawRoute();
        }

        private void OnStarChanged(object sender, EventArgs e)
        {
            if (_populatingSidebar) return;
            string newStar = _cmbStar.SelectedIndex > 0 ? _cmbStar.SelectedItem as string : null;
            if (newStar == _selStarName) return;
            _selStarName       = newStar;
            _selStarTransition = null;
            _populatingSidebar = true;
            FillProcTransCombo(_cmbStarTrans, _sbStars, _selStarName, ref _selStarTransition);
            _populatingSidebar = false;
            RedrawRoute();
        }

        private void OnStarTransChanged(object sender, EventArgs e)
        {
            if (_populatingSidebar) return;
            string newTrans = _cmbStarTrans.SelectedIndex > 0
                ? _cmbStarTrans.SelectedItem as string : null;
            if (newTrans == _selStarTransition) return;
            _selStarTransition = newTrans;
            RedrawRoute();
        }

        private void OnApproachChanged(object sender, EventArgs e)
        {
            if (_populatingSidebar) return;
            string newKey = (_cmbApproach.SelectedItem as ApproachItem)?.Key;
            if (string.IsNullOrEmpty(newKey)) newKey = null;
            if (newKey == _selApproachKey) return;
            _selApproachKey = newKey;

            if (_selApproachKey == null)
            {
                ClearApproachOverlay();
                return;
            }

            var app = _sbApproaches?.FirstOrDefault(a =>
                $"{a.Type}{a.Suffix ?? ""}_{a.Runway ?? ""}" == _selApproachKey);
            if (app == null) return;

            var destRwy = _sbDestRunways?.FirstOrDefault(r =>
                string.Equals(r.Name, _selDestRunway, StringComparison.OrdinalIgnoreCase));
            var ils = _sbIls?.FirstOrDefault(i =>
                string.Equals(i.Runway, _selDestRunway, StringComparison.OrdinalIgnoreCase));
            DrawApproachOverlay(app, destRwy, ils);
        }

        private void OpenApproachChart()
        {
            if (string.IsNullOrEmpty(_currentDestIcao)) return;
            NavApproach preselected = null;
            if (_selApproachKey != null)
                preselected = _sbApproaches?.FirstOrDefault(a =>
                    $"{a.Type}{a.Suffix ?? ""}_{a.Runway ?? ""}" == _selApproachKey);
            new ApproachChartForm(_currentDestIcao, preselected).Show(this);
        }

        // ── Approach overlay ─────────────────────────────────────────────────────────

        private void ClearApproachOverlay()
        {
            if (_approachOverlay == null || IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke((Action)ClearApproachOverlay); return; }
            _approachOverlay.Routes.Clear();
            _approachOverlay.Markers.Clear();
            _map?.Refresh();
        }

        private void DrawApproachOverlay(NavApproach app, NavRunway rwy, NavIls ils)
        {
            if (_approachOverlay == null || IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => DrawApproachOverlay(app, rwy, ils)));
                return;
            }

            _approachOverlay.Routes.Clear();
            _approachOverlay.Markers.Clear();

            var approachPen    = new Pen(_clrApproach, 2.5f);
            var missedPen      = new Pen(_clrMissed, 1.5f)   { DashStyle = DashStyle.Dash };
            var centerlinePen  = new Pen(
                Color.FromArgb(140, _clrApproach.R, _clrApproach.G, _clrApproach.B), 1.0f)
                { DashStyle = DashStyle.Dot };
            var shadowPen      = new Pen(Color.FromArgb(100, 0, 0, 0), 4.5f);

            // ── Trayectoria de legs ──────────────────────────────────────────────
            var legPts = new List<PointLatLng>();
            if (app.Legs != null)
                foreach (var leg in app.Legs)
                    if (leg.Lat.HasValue && leg.Lon.HasValue)
                        legPts.Add(new PointLatLng(leg.Lat.Value, leg.Lon.Value));

            if (rwy != null && legPts.Count > 0)
                legPts.Add(new PointLatLng(rwy.ThresholdLat, rwy.ThresholdLon));

            if (legPts.Count >= 2)
            {
                _approachOverlay.Routes.Add(
                    new GMapRoute(legPts, "app_shadow") { Stroke = shadowPen });
                _approachOverlay.Routes.Add(
                    new GMapRoute(legPts, "app")        { Stroke = approachPen });
            }

            // ── Extended centerline (5 NM before → 0.5 NM past threshold) ──────
            if (rwy != null)
            {
                double appBrg = GeodesicBearing(
                    rwy.ThresholdLat, rwy.ThresholdLon, rwy.EndLat, rwy.EndLon);
                if (HeadingDiff(appBrg, rwy.Heading) > 90)
                    appBrg = (appBrg + 180) % 360;
                if (ils?.LocTrueHeading.HasValue == true)
                    appBrg = ils.LocTrueHeading.Value;

                double oppBrg  = (appBrg + 180) % 360;
                double cosLat  = Math.Cos(rwy.ThresholdLat * Math.PI / 180);

                double extRad  = oppBrg * Math.PI / 180;
                double ext5Lat = rwy.ThresholdLat + (5.0 * 1852.0 * Math.Cos(extRad)) / 111320;
                double ext5Lon = rwy.ThresholdLon + (5.0 * 1852.0 * Math.Sin(extRad)) / (111320 * cosLat);

                double innRad   = appBrg * Math.PI / 180;
                double inn5Lat  = rwy.ThresholdLat + (0.5 * 1852.0 * Math.Cos(innRad)) / 111320;
                double inn5Lon  = rwy.ThresholdLon + (0.5 * 1852.0 * Math.Sin(innRad)) / (111320 * cosLat);

                var clPts = new List<PointLatLng>
                {
                    new PointLatLng(ext5Lat, ext5Lon),
                    new PointLatLng(rwy.ThresholdLat, rwy.ThresholdLon),
                    new PointLatLng(inn5Lat, inn5Lon),
                };
                _approachOverlay.Routes.Add(
                    new GMapRoute(clPts, "centerline") { Stroke = centerlinePen });
            }

            // ── Missed approach ──────────────────────────────────────────────────
            if (app.MissedLegs != null)
            {
                var missedPts = new List<PointLatLng>();
                foreach (var leg in app.MissedLegs)
                    if (leg.Lat.HasValue && leg.Lon.HasValue)
                        missedPts.Add(new PointLatLng(leg.Lat.Value, leg.Lon.Value));
                if (missedPts.Count >= 2)
                    _approachOverlay.Routes.Add(
                        new GMapRoute(missedPts, "missed") { Stroke = missedPen });
            }

            // ── Umbral de pista ──────────────────────────────────────────────────
            if (rwy != null)
                _approachOverlay.Markers.Add(new FixMarker(
                    new PointLatLng(rwy.ThresholdLat, rwy.ThresholdLon),
                    rwy.Name, "rwy"));

            _map?.Refresh();
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
        private readonly bool _dimmed;

        // Shared resources — allocated once
        // Magenta (#FF14DC): contrasta sobre Carto claro, ESRI satélite y rutas de cualquier color
        private static readonly Font  _font        = new Font("Consolas", 14f);
        private static readonly Font  _fontSmall   = new Font("Consolas", 10f);
        private static readonly Font  _fontRestr   = new Font("Consolas", 9f);
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

        // Dimmed (ambient) resources — steel-blue muted, for background context fixes
        private static readonly Pen   _dimPen     = new Pen(Color.FromArgb(85, 115, 160), 1.2f);
        private static readonly Brush _dimBrush   = new SolidBrush(Color.FromArgb(85, 115, 160));
        private static readonly Brush _dimAptFill = new SolidBrush(Color.FromArgb(35, 85, 115, 160));

        public FixMarker(PointLatLng pos, string ident, string type, string freq = null,
                         FixRestriction restriction = null, bool dimmed = false)
            : base(pos)
        {
            Ident        = ident;
            _type        = type?.ToLower() ?? "wpt";
            _freq        = freq;
            _restriction = restriction;
            _dimmed      = dimmed;
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
                    // Compass ring con 12 ticks radiales (estilo Navigraph)
                    const float VR   = 10f;  // radio del compass ring
                    const float HexR = 5f;   // radio del hexágono interior
                    g.DrawEllipse(pen, cx - VR, cy - VR, VR * 2, VR * 2);
                    for (int k = 0; k < 12; k++)
                    {
                        double ang  = k * 30.0 * Math.PI / 180.0;
                        float  sinA = (float)Math.Sin(ang);
                        float  cosA = (float)Math.Cos(ang);
                        g.DrawLine(pen,
                            cx + VR       * sinA, cy - VR       * cosA,
                            cx + (VR + 4) * sinA, cy - (VR + 4) * cosA);
                    }
                    var vhex = new PointF[6];
                    for (int i = 0; i < 6; i++)
                    {
                        double a = i * 60.0 * Math.PI / 180.0;
                        vhex[i] = new PointF(cx + (float)(HexR * Math.Sin(a)),
                                             cy - (float)(HexR * Math.Cos(a)));
                    }
                    g.DrawPolygon(pen, vhex);
                    g.FillEllipse(brush, cx - 2f, cy - 2f, 4f, 4f);
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
                float lx = (_type == "vor") ? cx + 17f : cx + 8f;
                float ly = cy - _font.Height / 2f;

                if (_type == "pseudo")
                {
                    DrawHaloWithBrush(g, Ident, _font, lx, cy - _font.Height / 2f, _pseudoBrush);
                }
                else if (_type == "apfx")
                {
                    DrawHaloWithBrush(g, Ident, _fontSmall, lx, cy - _fontSmall.Height / 2f, _apfxBrush);
                }
                else
                {
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
