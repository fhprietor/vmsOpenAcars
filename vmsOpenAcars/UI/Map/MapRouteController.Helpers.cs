using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GMap.NET;
using GMap.NET.WindowsForms;
using vmsOpenAcars.Models;
using vmsOpenAcars.Models.NavData;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI.Forms
{
    internal sealed partial class MapRouteController
    {
        // ── SID / STAR procedure matching ─────────────────────────────────────────────

        private static NavProcedure MatchProcedure(
            IList<string> planIdents, IList<NavProcedure> procedures, string runwayHint,
            string nameHint = null)
        {
            if (procedures == null || procedures.Count == 0) return null;

            if (!string.IsNullOrEmpty(nameHint))
            {
                var direct = procedures.FirstOrDefault(p =>
                    string.Equals(p.Name, nameHint, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(p.Runway) || string.IsNullOrEmpty(runwayHint)
                        || ProcedureAppliesToRunway(p.Runway, runwayHint)));
                if (direct != null) return direct;
                var byName = procedures.FirstOrDefault(p =>
                    string.Equals(p.Name, nameHint, StringComparison.OrdinalIgnoreCase));
                if (byName != null) return byName;
                var byBase = procedures.FirstOrDefault(p =>
                {
                    int d  = p.Name.IndexOf('.');
                    string bn = d > 0 ? p.Name.Substring(0, d) : p.Name;
                    return string.Equals(bn, nameHint, StringComparison.OrdinalIgnoreCase)
                        && (string.IsNullOrEmpty(p.Runway) || string.IsNullOrEmpty(runwayHint)
                            || ProcedureAppliesToRunway(p.Runway, runwayHint));
                });
                if (byBase != null) return byBase;
                var byBaseAny = procedures.FirstOrDefault(p =>
                {
                    int d  = p.Name.IndexOf('.');
                    string bn = d > 0 ? p.Name.Substring(0, d) : p.Name;
                    return string.Equals(bn, nameHint, StringComparison.OrdinalIgnoreCase);
                });
                if (byBaseAny != null) return byBaseAny;
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
            string prefix = runway.TrimEnd('L', 'R', 'C');
            return string.Equals(procRunway, prefix + "B", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddProcedureLabel(
            List<SimbriefWaypoint> wps, string stage, string name, List<GMapMarker> markers)
        {
            if (string.IsNullOrEmpty(name)) return;
            var pts = wps
                .Where(w => (w.Stage ?? "CRZ") == stage && w.Type != "apt")
                .Select(w => (w.Lat, w.Lon))
                .ToList();
            AddProcedureLabelFromCoords(pts, name, markers);
        }

        private static void AddProcedureLabelFromProc(
            NavProcedure proc, string name, List<GMapMarker> markers)
        {
            if (string.IsNullOrEmpty(name) || proc?.Legs == null) return;
            var pts = proc.Legs
                .Where(l => l.Lat.HasValue && l.Lon.HasValue)
                .Select(l => (l.Lat.Value, l.Lon.Value))
                .ToList();
            AddProcedureLabelFromCoords(pts, name, markers);
        }

        private static void AddProcedureLabelFromCoords(
            List<(double Lat, double Lon)> pts, string name, List<GMapMarker> markers)
        {
            if (string.IsNullOrEmpty(name) || pts.Count < 1) return;

            if (pts.Count == 1)
            {
                markers.Add(new RouteLabelMarker(
                    new PointLatLng(pts[0].Lat, pts[0].Lon), name, 0f));
                return;
            }

            for (int i = 0; i < pts.Count - 1; i++)
            {
                double dN = (pts[i + 1].Lat - pts[i].Lat) * 111320;
                double dE = (pts[i + 1].Lon - pts[i].Lon) * 111320
                            * Math.Cos(pts[i].Lat * Math.PI / 180);
                if (Math.Sqrt(dN * dN + dE * dE) < 500.0) continue;

                double midLat = (pts[i].Lat + pts[i + 1].Lat) / 2.0;
                double midLon = (pts[i].Lon + pts[i + 1].Lon) / 2.0;

                float screenAngle = (float)(GeodesicBearing(
                    pts[i].Lat, pts[i].Lon,
                    pts[i + 1].Lat, pts[i + 1].Lon) - 90.0);
                screenAngle = ((screenAngle % 360f) + 360f) % 360f;
                if (screenAngle > 180f) screenAngle -= 360f;
                if (Math.Abs(screenAngle) > 90f)
                    screenAngle = screenAngle > 0 ? screenAngle - 180f : screenAngle + 180f;

                markers.Add(new RouteLabelMarker(
                    new PointLatLng(midLat, midLon), name, screenAngle));
            }
        }

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
                    dict[leg.Fix] = r;
                }
            }
            return dict;
        }

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

                    int endIdx = -1;
                    for (int i = 0; i < allPts.Count; i++)
                        if (DistanceKm(allPts[i].Lat, allPts[i].Lon, arcEndLat, arcEndLon) < 1.0)
                        { endIdx = i; break; }
                    if (endIdx <= 0) continue;

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

        private static void BuildSmoothedRoutes(
            List<(double Lat, double Lon, string Stage, bool IsFlyover)> pts,
            List<GMapRoute> shadows, List<GMapRoute> colors)
        {
            if (pts.Count < 2) return;

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
                if (Math.Acos(dot) * 180.0 / Math.PI < 5.0) continue;

                double armKm = pts[i].Stage == "CLB" ? 2.78
                             : pts[i].Stage == "DSC" ? 3.70
                             : 4.63;
                double arm   = Math.Min(armKm, Math.Min(pLen, nLen) * 0.40);

                trimAfter[i] = new PointLatLng(
                    refLat     + (nN / nLen) * arm / 111.32,
                    pts[i].Lon + (nE / nLen) * arm / (111.32 * cosRef));

                if (pts[i].IsFlyover) continue;

                trimBefore[i] = new PointLatLng(
                    refLat     + (pN / pLen) * arm / 111.32,
                    pts[i].Lon + (pE / pLen) * arm / (111.32 * cosRef));
            }

            const int ArcSegs = 12;
            var    segPts = new List<PointLatLng> { new PointLatLng(pts[0].Lat, pts[0].Lon) };
            string stage  = pts[0].Stage;

            for (int i = 1; i < pts.Count; i++)
            {
                string newStage = pts[i].Stage;
                var arcStart = trimBefore[i] ?? new PointLatLng(pts[i].Lat, pts[i].Lon);

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
                default:    color = _clrCrz; width = 5f;  break;
            }
            string id = $"{stage}_{colors.Count}";
            shadows.Add(new GMapRoute(new List<PointLatLng>(pts), "s_" + id)
                { Stroke = new Pen(_clrShadow, width + 4f) });
            colors.Add(new GMapRoute(new List<PointLatLng>(pts), id)
                { Stroke = new Pen(color, width) });
        }

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

        private static List<PointLatLng> ComputeDmeArc(
            double startLat, double startLon,
            double centerLat, double centerLon,
            double endLat,   double endLon,
            bool   turnRight)
        {
            const double DegPerSeg = 5.0;

            double cosRef = Math.Cos(centerLat * Math.PI / 180.0);

            double sN = (startLat - centerLat) * 111320.0;
            double sE = (startLon - centerLon) * 111320.0 * cosRef;
            double eN = (endLat   - centerLat) * 111320.0;
            double eE = (endLon   - centerLon) * 111320.0 * cosRef;

            double R = Math.Sqrt(sN * sN + sE * sE);
            if (R < 0.1 * 1852.0) return null;

            double aStart = Math.Atan2(sE, sN) * 180.0 / Math.PI;
            double aEnd   = Math.Atan2(eE, eN) * 180.0 / Math.PI;

            double sweep = turnRight
                ? ((aEnd - aStart) + 360.0) % 360.0
                : ((aStart - aEnd) + 360.0) % 360.0;

            if (sweep < 0.5) sweep = 360.0;

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

            pts[pts.Count - 1] = new PointLatLng(endLat, endLon);
            return pts;
        }

        private static List<PointLatLng> ComputeDepartureArc(
            double startLat, double startLon, double startBrg,
            double targetLat, double targetLon,
            double radiusNm = 2.5)
        {
            double cosRef = Math.Cos(startLat * Math.PI / 180.0);
            double R = radiusNm * 1852.0;

            double tN = (targetLat - startLat) * 111320.0;
            double tE = (targetLon - startLon) * 111320.0 * cosRef;

            double depRad = startBrg * Math.PI / 180.0;
            double cross  = Math.Cos(depRad) * tE - Math.Sin(depRad) * tN;
            bool turnRight = cross > 0;

            double perpRad = (startBrg + (turnRight ? 90.0 : -90.0)) * Math.PI / 180.0;
            double cN = R * Math.Cos(perpRad);
            double cE = R * Math.Sin(perpRad);

            double dtN = tN - cN, dtE = tE - cE;
            double d   = Math.Sqrt(dtN * dtN + dtE * dtE);
            if (d < R * 1.05) return null;

            double halfAngleDeg = Math.Acos(Math.Min(1.0, R / d)) * 180.0 / Math.PI;

            double brgCtoQ = (Math.Atan2(dtE, dtN) * 180.0 / Math.PI + 360.0) % 360.0;

            double T1brg = (brgCtoQ + halfAngleDeg + 360.0) % 360.0;
            double T2brg = (brgCtoQ - halfAngleDeg + 360.0) % 360.0;

            double brgCtoP0 = (Math.Atan2(-cE, -cN) * 180.0 / Math.PI + 360.0) % 360.0;

            double arc1 = turnRight
                ? (T1brg - brgCtoP0 + 360.0) % 360.0
                : (brgCtoP0 - T1brg + 360.0) % 360.0;
            double arc2 = turnRight
                ? (T2brg - brgCtoP0 + 360.0) % 360.0
                : (brgCtoP0 - T2brg + 360.0) % 360.0;

            double exitBrg = arc1 <= arc2 ? T1brg : T2brg;
            double arcDeg  = Math.Min(arc1, arc2);
            if (arcDeg < 1.0 || arcDeg > 200.0) return null;

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

            pts.Add(new PointLatLng(targetLat, targetLon));
            return pts;
        }

        private static void DispGeoNm(
            double lat, double lon, double bearingDeg, double distNm,
            out double outLat, out double outLon)
        {
            double rad    = bearingDeg * Math.PI / 180.0;
            double meters = distNm * 1852.0;
            double cosRef = Math.Cos(lat * Math.PI / 180.0);
            outLat = lat + meters * Math.Cos(rad) / 111320.0;
            outLon = lon + meters * Math.Sin(rad) / (111320.0 * cosRef);
        }

        private static List<PointLatLng> ComputeHoldRacetrack(
            double holdLat, double holdLon,
            double inbndCrs, bool turnRight, double legNm)
        {
            const double R    = 0.85;
            const int    Segs = 10;

            double outBrg  = (inbndCrs + 180.0) % 360.0;
            double perpBrg = turnRight
                ? (inbndCrs + 90.0)          % 360.0
                : (inbndCrs - 90.0 + 360.0) % 360.0;

            double bLat, bLon;
            DispGeoNm(holdLat, holdLon, outBrg, legNm, out bLat, out bLon);

            double cALat, cALon, cBLat, cBLon;
            DispGeoNm(holdLat, holdLon, perpBrg, R, out cALat, out cALon);
            DispGeoNm(bLat,    bLon,    perpBrg, R, out cBLat, out cBLon);

            double sweep  = turnRight ? 1.0 : -1.0;
            double startA = (perpBrg + 180.0) % 360.0;
            double startB = perpBrg;

            var pts = new List<PointLatLng>();

            for (int k = 0; k <= Segs; k++)
            {
                double ang = ((startA + sweep * 180.0 * k / Segs) % 360.0 + 360.0) % 360.0;
                double pLat, pLon;
                DispGeoNm(cALat, cALon, ang, R, out pLat, out pLon);
                pts.Add(new PointLatLng(pLat, pLon));
            }

            double bsLat, bsLon;
            DispGeoNm(cBLat, cBLon, startB, R, out bsLat, out bsLon);
            pts.Add(new PointLatLng(bsLat, bsLon));

            for (int k = 0; k <= Segs; k++)
            {
                double ang = ((startB + sweep * 180.0 * k / Segs) % 360.0 + 360.0) % 360.0;
                double pLat, pLon;
                DispGeoNm(cBLat, cBLon, ang, R, out pLat, out pLon);
                pts.Add(new PointLatLng(pLat, pLon));
            }

            pts.Add(new PointLatLng(holdLat, holdLon));
            return pts;
        }

        private static void DrawRunwaySegment(
            NavRunway rwy,
            List<GMapRoute> shadows, List<GMapRoute> colors,
            List<GMapMarker> markers, Color clr,
            string labelText = null, string role = null)
        {
            var seg = new List<PointLatLng> {
                new PointLatLng(rwy.ThresholdLat, rwy.ThresholdLon),
                new PointLatLng(rwy.EndLat,       rwy.EndLon)
            };
            shadows.Add(new GMapRoute(seg, "s_rwy_" + rwy.Name) { Stroke = new Pen(_clrShadow, 4.5f) });
            colors.Add(new GMapRoute(seg,  "rwy_"   + rwy.Name) { Stroke = new Pen(clr, 2.5f) });
            markers.Add(new FixMarker(
                new PointLatLng(rwy.ThresholdLat, rwy.ThresholdLon),
                rwy.Name, "rwy", null, null, false,
                role: role, labelText: labelText));
        }

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

        // ── Math helpers ──────────────────────────────────────────────────────────────

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
    }
}
