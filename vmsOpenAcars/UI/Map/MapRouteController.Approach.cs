using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using GMap.NET;
using GMap.NET.WindowsForms;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.UI.Forms
{
    internal sealed partial class MapRouteController
    {
        // ── Approach overlay ─────────────────────────────────────────────────────────

        internal void ClearApproachOverlay()
        {
            if (ApproachOverlay == null || _map.IsDisposed || !_map.IsHandleCreated) return;
            if (_map.InvokeRequired) { _map.BeginInvoke((Action)ClearApproachOverlay); return; }
            ApproachOverlay.Routes.Clear();
            ApproachOverlay.Markers.Clear();
            _map?.Refresh();
        }

        internal void DrawApproachOverlay(
            NavApproach app, NavApproachTransition trans, NavRunway rwy, NavIls ils)
        {
            if (ApproachOverlay == null || _map.IsDisposed || !_map.IsHandleCreated) return;
            if (_map.InvokeRequired)
            {
                _map.BeginInvoke((Action)(() => DrawApproachOverlay(app, trans, rwy, ils)));
                return;
            }

            ApproachOverlay.Routes.Clear();
            ApproachOverlay.Markers.Clear();

            var missedPen     = new Pen(_clrMissed, 3f) { DashStyle = DashStyle.Dash };
            var centerlinePen = new Pen(Color.FromArgb(140, 60, 215, 100), 1.0f)
                { DashStyle = DashStyle.Dot };

            var transPen    = new Pen(_clrDsc, 5f);
            var transShadow = new Pen(Color.FromArgb(100, 0, 0, 0), 9f);

            var finalPen    = new Pen(Color.FromArgb(60, 215, 100), 4f);
            var finalShadow = new Pen(Color.FromArgb(100, 0, 0, 0), 8f);

            var transPts = new List<PointLatLng>();
            var appPts   = new List<PointLatLng>();

            if (trans?.Legs != null)
                foreach (var leg in trans.Legs)
                    if (leg.Lat.HasValue && leg.Lon.HasValue)
                        transPts.Add(new PointLatLng(leg.Lat.Value, leg.Lon.Value));

            if (app.Legs != null)
                foreach (var leg in app.Legs)
                    if (leg.Lat.HasValue && leg.Lon.HasValue)
                        appPts.Add(new PointLatLng(leg.Lat.Value, leg.Lon.Value));

            if (transPts.Count > 0 && appPts.Count > 0)
                transPts.Add(appPts[0]);

            if (rwy != null && appPts.Count > 0)
                appPts.Add(new PointLatLng(rwy.ThresholdLat, rwy.ThresholdLon));

            if (transPts.Count >= 2)
            {
                ApproachOverlay.Routes.Add(new GMapRoute(transPts, "app_trans_sh") { Stroke = transShadow });
                ApproachOverlay.Routes.Add(new GMapRoute(transPts, "app_trans")    { Stroke = transPen    });
            }
            if (appPts.Count >= 2)
            {
                ApproachOverlay.Routes.Add(new GMapRoute(appPts, "app_final_sh") { Stroke = finalShadow });
                ApproachOverlay.Routes.Add(new GMapRoute(appPts, "app_final")    { Stroke = finalPen    });
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
                ApproachOverlay.Routes.Add(
                    new GMapRoute(clPts, "centerline") { Stroke = centerlinePen });
            }

            // ── Missed approach ──────────────────────────────────────────────────
            if (app.MissedLegs != null && app.MissedLegs.Count > 0)
            {
                var missedPts = new List<PointLatLng>();
                double prevLat = rwy?.ThresholdLat ?? 0;
                double prevLon = rwy?.ThresholdLon ?? 0;
                if (rwy != null)
                    missedPts.Add(new PointLatLng(prevLat, prevLon));

                NavApproachLeg holdLeg = null;
                foreach (var leg in app.MissedLegs)
                {
                    if (leg.Type == "HM" || leg.Type == "HF" || leg.Type == "HA")
                    {
                        holdLeg = leg;
                        if (leg.Lat.HasValue && leg.Lon.HasValue)
                        {
                            missedPts.Add(new PointLatLng(leg.Lat.Value, leg.Lon.Value));
                            prevLat = leg.Lat.Value;
                            prevLon = leg.Lon.Value;
                        }
                        break;
                    }
                    if (leg.Lat.HasValue && leg.Lon.HasValue)
                    {
                        missedPts.Add(new PointLatLng(leg.Lat.Value, leg.Lon.Value));
                        prevLat = leg.Lat.Value;
                        prevLon = leg.Lon.Value;
                    }
                    else if (leg.Type == "CA" || leg.Type == "VA" || leg.Type == "FA" ||
                             leg.Type == "FM" || leg.Type == "VI" || leg.Type == "VM")
                    {
                        double distNm = leg.DistanceNm > 0.5 ? Math.Min(leg.DistanceNm, 6.0) : 3.0;
                        double eLat, eLon;
                        DispGeoNm(prevLat, prevLon, leg.Course, distNm, out eLat, out eLon);
                        missedPts.Add(new PointLatLng(eLat, eLon));
                        prevLat = eLat;
                        prevLon = eLon;
                    }
                }

                if (missedPts.Count >= 2)
                    ApproachOverlay.Routes.Add(
                        new GMapRoute(missedPts, "missed") { Stroke = missedPen });

                if (holdLeg != null && holdLeg.Lat.HasValue && holdLeg.Lon.HasValue)
                {
                    bool   right  = !string.Equals(holdLeg.TurnDirection, "L",
                                        StringComparison.OrdinalIgnoreCase);
                    double course = holdLeg.Course;
                    double legNm  = holdLeg.DistanceNm > 0.5 ? holdLeg.DistanceNm : 1.5;

                    var racePts = ComputeHoldRacetrack(
                        holdLeg.Lat.Value, holdLeg.Lon.Value, course, right, legNm);

                    if (racePts != null && racePts.Count >= 3)
                        ApproachOverlay.Routes.Add(
                            new GMapRoute(racePts, "hold") { Stroke = missedPen });

                    ApproachOverlay.Markers.Add(new FixMarker(
                        new PointLatLng(holdLeg.Lat.Value, holdLeg.Lon.Value),
                        holdLeg.Fix ?? "", "wpt"));
                }
            }

            if (rwy != null)
                ApproachOverlay.Markers.Add(new FixMarker(
                    new PointLatLng(rwy.ThresholdLat, rwy.ThresholdLon),
                    rwy.Name, "rwy"));

            _map?.Refresh();
        }
    }
}
