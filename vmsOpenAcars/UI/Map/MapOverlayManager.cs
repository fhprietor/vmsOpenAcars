using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GMap.NET;
using GMap.NET.WindowsForms;
using vmsOpenAcars.Models.NavData;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI.Forms
{
    internal sealed class MapOverlayManager
    {
        private readonly GMapControl _map;

        internal GMapOverlay AirspaceOverlay { get; } = new GMapOverlay("airspaces");
        internal GMapOverlay AtcOverlay      { get; } = new GMapOverlay("atc");
        internal IList<IvaoAtcStation> LastAtcStations { get; private set; } = new List<IvaoAtcStation>();

        internal MapOverlayManager(GMapControl map)
        {
            _map = map;
        }

        internal void SetAirspaces(IList<NavAirspace> airspaces)
        {
            if (_map.IsDisposed || !_map.IsHandleCreated) return;
            if (_map.InvokeRequired) { _map.BeginInvoke(new Action(() => SetAirspaces(airspaces))); return; }

            AirspaceOverlay.Polygons.Clear();
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
                AirspaceOverlay.Polygons.Add(poly);
            }

            _map.Refresh();
        }

        internal void SetAtcStations(IList<IvaoAtcStation> stations)
        {
            if (_map.IsDisposed || !_map.IsHandleCreated) return;
            if (_map.InvokeRequired) { _map.BeginInvoke(new Action(() => SetAtcStations(stations))); return; }

            LastAtcStations = stations ?? new List<IvaoAtcStation>();

            AtcOverlay.Markers.Clear();
            AtcOverlay.Polygons.Clear();
            if (stations == null || stations.Count == 0) { _map.Refresh(); return; }

            foreach (var grp in stations.GroupBy(s => s.Icao, StringComparer.OrdinalIgnoreCase))
            {
                var all     = grp.ToList();
                var nonAtis = all.Where(s => s.Position != "ATIS").ToList();
                if (nonAtis.Count == 0) continue;

                var info = NavDataClient.GetAirportInfo(grp.Key);
                double lat, lon;
                if (info != null && (info.Lat != 0 || info.Lon != 0))
                {
                    lat = info.Lat;
                    lon = info.Lon;
                }
                else
                {
                    var first = grp.FirstOrDefault(s => s.Lat != 0 || s.Lon != 0);
                    if (first == null) continue;
                    lat = first.Lat;
                    lon = first.Lon;
                }
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
                    if (hasTwr) AtcOverlay.Polygons.Add(MakeCirclePolygon(lat, lon, R,
                        Color.FromArgb( 30, 220,  50,  50),
                        new Pen(Color.FromArgb(170, 220,  50,  50), 1.5f)));
                    if (hasGnd) AtcOverlay.Polygons.Add(MakeStarPolygon(lat, lon, R, 0.38,  0.0,
                        Color.FromArgb( 30, 220, 190,   0),
                        new Pen(Color.FromArgb(170, 220, 190,   0), 1.5f)));
                    if (hasDel) AtcOverlay.Polygons.Add(MakeStarPolygon(lat, lon, R, 0.38, 45.0,
                        Color.FromArgb( 30, 255, 130,   0),
                        new Pen(Color.FromArgb(170, 255, 130,   0), 1.5f)));

                    AtcOverlay.Markers.Add(new AtcLabelMarker(coord, grp.Key, local, atis));
                }
                if (area.Count > 0)
                    AtcOverlay.Markers.Add(new AtcStationMarker(coord, grp.Key, area));
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
    }
}
