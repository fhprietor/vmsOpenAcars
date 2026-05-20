using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.Projections;
using GMap.NET.WindowsForms;

namespace vmsOpenAcars.UI.Forms
{
    public class MapForm : Form
    {
        private GMapControl    _map;
        private GMapOverlay    _aircraftOverlay;
        private AircraftMarker _aircraftMarker;
        private Label          _lblStatus;
        private CheckBox       _chkFollow;
        private ComboBox       _cmbProvider;
        private bool           _followAircraft = true;

        public MapForm()
        {
            Text          = "vmsOpenAcars — MAP";
            Size          = new Size(920, 660);
            MinimumSize   = new Size(600, 420);
            BackColor     = Color.FromArgb(15, 20, 25);
            ForeColor     = Color.White;
            Font          = new Font("Consolas", 9);
            StartPosition = FormStartPosition.Manual;

            BuildLayout();
            InitMap();
        }

        // ── Layout ────────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
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
            _cmbProvider.Items.AddRange(new object[] { "Street (Carto)", "Satellite (ESRI)" });
            _cmbProvider.SelectedIndex = 0;
            _cmbProvider.SelectedIndexChanged += OnProviderChanged;

            var btnZoomIn  = MakeZoomBtn("+");
            var btnZoomOut = MakeZoomBtn("−");
            btnZoomIn.Dock  = DockStyle.Right;
            btnZoomOut.Dock = DockStyle.Right;
            btnZoomIn.Click  += (s, e) => { if (_map.Zoom < _map.MaxZoom) _map.Zoom++; };
            btnZoomOut.Click += (s, e) => { if (_map.Zoom > _map.MinZoom) _map.Zoom--; };

            bar.Controls.Add(_lblStatus);
            bar.Controls.Add(_chkFollow);
            bar.Controls.Add(_cmbProvider);
            bar.Controls.Add(btnZoomOut);
            bar.Controls.Add(btnZoomIn);

            _map = new GMapControl { Dock = DockStyle.Fill };

            Controls.Add(_map);
            Controls.Add(bar);
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

        // ── GMap initialization ───────────────────────────────────────────────────

        private void InitMap()
        {
            GMaps.Instance.Mode = AccessMode.ServerAndCache;

            // Required by OSM and most CDN-backed tile servers
            GMapProvider.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0";

            _map.MapProvider = CartoLightProvider.Instance;
            _map.MinZoom     = 2;
            _map.MaxZoom     = 19;
            _map.Zoom        = 14;
            _map.ShowCenter  = false;
            _map.DragButton  = MouseButtons.Left;
            _map.BackColor   = Color.FromArgb(30, 40, 50);

            _aircraftOverlay = new GMapOverlay("aircraft");
            _map.Overlays.Add(_aircraftOverlay);

            _map.OnMapZoomChanged += () => UpdateZoomInStatus();
        }

        private void UpdateZoomInStatus()
        {
            if (_lblStatus.IsDisposed) return;
            string t = _lblStatus.Text;
            int zIdx = t.IndexOf("  Z:");
            if (zIdx >= 0) t = t.Substring(0, zIdx);
            _lblStatus.Text = t + $"  Z:{(int)_map.Zoom}";
        }

        private void OnProviderChanged(object sender, EventArgs e)
        {
            switch (_cmbProvider.SelectedIndex)
            {
                case 0: _map.MapProvider = CartoLightProvider.Instance;    break;
                case 1: _map.MapProvider = EsriSatelliteProvider.Instance; break;
            }
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
            => GetTileImageUsingHttp(
                $"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{zoom}/{pos.Y}/{pos.X}");
    }

    // ── Aircraft marker ───────────────────────────────────────────────────────────

    internal sealed class AircraftMarker : GMapMarker
    {
        public double Heading { get; set; }

        private static readonly Brush _body   = new SolidBrush(Color.FromArgb(255, 215, 40));
        private static readonly Pen   _edge   = new Pen(Color.FromArgb(100, 70, 0), 1.5f);
        private static readonly Brush _shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0));

        public AircraftMarker(PointLatLng pos, double heading) : base(pos)
        {
            Heading   = heading;
            Offset    = new Point(0, 0);
            this.Size = new Size(28, 28);
        }

        public override void OnRender(Graphics g)
        {
            var state = g.Save();

            g.TranslateTransform(LocalPosition.X, LocalPosition.Y);
            g.RotateTransform((float)Heading);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int S = 11;
            var body = new PointF[]
            {
                new PointF(  0,          -S),
                new PointF(  S * 0.55f,  S * 0.65f),
                new PointF(  0,          S * 0.30f),
                new PointF( -S * 0.55f,  S * 0.65f),
            };

            g.TranslateTransform(1.5f, 1.5f);
            g.FillPolygon(_shadow, body);
            g.TranslateTransform(-1.5f, -1.5f);

            g.FillPolygon(_body, body);
            g.DrawPolygon(_edge, body);

            g.Restore(state);
        }
    }
}
