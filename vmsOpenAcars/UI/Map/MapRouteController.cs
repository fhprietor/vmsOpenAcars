using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using vmsOpenAcars.Models;
using vmsOpenAcars.Models.NavData;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.UI.Forms
{
    internal sealed partial class MapRouteController
    {
        private readonly GMapControl   _map;
        private readonly SpinnerOverlay _spinner;
        private readonly Label          _lblStatus;
        private AircraftMarker          _aircraftMarker;

        private static readonly Color _clrClb    = Color.FromArgb(  0, 210, 175);
        private static readonly Color _clrCrz    = Color.FromArgb(185,  45, 255);
        private static readonly Color _clrDsc    = Color.FromArgb(255, 185,  50);
        private static readonly Color _clrAlt    = Color.FromArgb(170, 150, 215);
        private static readonly Color _clrShadow = Color.FromArgb(140,   0,   0,  0);
        private static readonly Color _clrMissed = Color.FromArgb(  0, 200, 255);

        internal GMapOverlay RouteShadowOverlay { get; } = new GMapOverlay("route_shadow");
        internal GMapOverlay RouteOverlay       { get; } = new GMapOverlay("route");
        internal GMapOverlay AmbientOverlay     { get; } = new GMapOverlay("ambient");
        internal GMapOverlay WaypointOverlay    { get; } = new GMapOverlay("waypoints");
        internal GMapOverlay ApproachOverlay    { get; } = new GMapOverlay("approach");
        internal GMapOverlay AircraftOverlay    { get; } = new GMapOverlay("aircraft");

        internal bool FollowAircraft { get; set; } = true;

        internal bool RouteLayerVisible
        {
            set
            {
                RouteOverlay.IsVisibile       = value;
                RouteShadowOverlay.IsVisibile = value;
                WaypointOverlay.IsVisibile    = value;
            }
        }

        internal MapRouteController(GMapControl map, SpinnerOverlay spinner, Label lblStatus)
        {
            _map      = map;
            _spinner  = spinner;
            _lblStatus = lblStatus;
        }

        internal void UpdateAmbientVisibility(int zoom)
        {
            AmbientOverlay.IsVisibile = zoom >= 10;
        }

        // ── Position update ───────────────────────────────────────────────────────

        internal void UpdatePosition(double lat, double lon, double heading)
        {
            if (_map.IsDisposed || !_map.IsHandleCreated) return;
            _map.BeginInvoke((Action)(() =>
            {
                if (_map.IsDisposed) return;

                var pos = new PointLatLng(lat, lon);

                if (_aircraftMarker == null)
                {
                    _aircraftMarker = new AircraftMarker(pos, heading);
                    AircraftOverlay.Markers.Add(_aircraftMarker);
                    _map.Zoom = 14;
                }
                else
                {
                    _aircraftMarker.Position = pos;
                    _aircraftMarker.Heading  = heading;
                }

                if (FollowAircraft)
                    _map.Position = pos;

                _map.Invalidate();

                _lblStatus.Text =
                    $"  {lat:F4}°  {lon:F4}°   HDG {heading:F0}°  Z:{(int)_map.Zoom}";
            }));
        }

        internal void SetAircraftCategory(FsuipcService.AircraftCategory cat)
        {
            if (_map.IsDisposed || !_map.IsHandleCreated) return;
            if (_map.InvokeRequired) { _map.BeginInvoke(new Action(() => SetAircraftCategory(cat))); return; }
            if (_aircraftMarker == null) return;
            _aircraftMarker.Category = cat;
            _map.Refresh();
        }

        // ── Route loading ─────────────────────────────────────────────────────────

        internal void LoadRoute(
            IList<SimbriefWaypoint> waypoints,
            string originIcao, string originRunway,
            string destIcao,   string destRunway,
            string altIcao,
            string sidName,    string starName,
            Action<RouteNavDataResult> onCompleted)
        {
            var wps = waypoints.ToList();

            _spinner.StartSpin();
            System.Threading.Tasks.Task.Run(() =>
            {
                var shadowRoutes   = new List<GMapRoute>();
                var colorRoutes    = new List<GMapRoute>();
                var markers        = new List<GMapMarker>();
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

                        bool hasSid = sidFixes.Any(w => w.IsSidStar);

                        if (runways?.Count > 0 && firstPlanFix != null)
                        {
                            NavRunway rwy;

                            if (hasSid)
                            {
                                rwy = FindDepartureRunway(runways, originRunway, sidFixes[0]);
                                if (rwy != null)
                                {
                                    DrawRunwaySegment(rwy, shadowRoutes, colorRoutes, markers, _clrClb,
                                        labelText: $"{originIcao}/{rwy.Name}", role: "origin");
                                    hasDepRwy = true;
                                    depEnd    = (rwy.EndLat, rwy.EndLon);
                                }
                            }
                            else
                            {
                                rwy = FindDepartureRunway(runways, originRunway, firstPlanFix);
                                if (rwy != null)
                                {
                                    DrawRunwaySegment(rwy, shadowRoutes, colorRoutes, markers, _clrClb,
                                        labelText: $"{originIcao}/{rwy.Name}", role: "origin");

                                    double depBrg = GeodesicBearing(
                                        rwy.ThresholdLat, rwy.ThresholdLon, rwy.EndLat, rwy.EndLon);
                                    if (HeadingDiff(depBrg, rwy.Heading) > 90)
                                        depBrg = (depBrg + 180) % 360;

                                    double rad    = depBrg * Math.PI / 180;
                                    double cosEnd = Math.Cos(rwy.EndLat * Math.PI / 180);
                                    double ext    = 3.0 * 1852.0;
                                    double extLat = rwy.EndLat + (ext * Math.Cos(rad)) / 111320;
                                    double extLon = rwy.EndLon + (ext * Math.Sin(rad)) / (111320 * cosEnd);

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

                                    var depExt = new List<PointLatLng> {
                                        new PointLatLng(rwy.EndLat, rwy.EndLon),
                                        new PointLatLng(extLat, extLon)
                                    };
                                    shadowRoutes.Add(new GMapRoute(depExt, "s_depext")
                                        { Stroke = new Pen(_clrShadow, 4.5f) });
                                    colorRoutes.Add(new GMapRoute(depExt, "depext")
                                        { Stroke = new Pen(_clrClb, 2.5f) });

                                    var curve = ComputeDepartureArc(
                                        extLat, extLon, depBrg,
                                        firstPlanFix.Lat, firstPlanFix.Lon);

                                    if (curve == null)
                                    {
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

                                        if (curve.Count >= 3)
                                        {
                                            noSidStub    = true;
                                            noSidStubLat = curve[curve.Count - 2].Lat;
                                            noSidStubLon = curve[curve.Count - 2].Lng;
                                        }
                                    }

                                    hasDepRwy = true;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // ── Resolución SID / STAR desde NavData ──
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

                bool useSidLegs  = sidProc?.Legs?.Any(l => l.Lat.HasValue && l.Lon.HasValue) == true;
                bool useStarLegs = starProc?.Legs?.Any(l => l.Lat.HasValue && l.Lon.HasValue) == true;

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

                        bool hasStar = starFixes.Any(w => w.IsSidStar) || useStarLegs;
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

                                    noStarThr5    = true;
                                    noStarThr5Lat = thr5Lat;
                                    noStarThr5Lon = thr5Lon;

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
                                        arrRwy.Name, "rwy", null, null, false,
                                        role: "dest", labelText: $"{destIcao}/{arrRwy.Name}"));
                                    hasArrRwy = true;
                                }
                            }
                        }
                        else
                        {
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

                                    if (HeadingDiff(apBrg2, brgLastToThr) > 25.0)
                                    {
                                        var nearbyA = NavDataClient.GetAirportWaypoints(destIcao);
                                        double lastDistKm = DistanceKm(
                                            lastStarFix.Lat, lastStarFix.Lon,
                                            arrRwy2.ThresholdLat, arrRwy2.ThresholdLon);

                                        NavAirportWaypoint bestAligned = null;
                                        double bestScore = double.MaxValue;
                                        const double TargetKm    = 10.0 * 1.852;
                                        const double MinKm       =  3.0 * 1.852;
                                        const double MaxAlignErr = 20.0;

                                        foreach (var wp in nearbyA
                                            ?? Enumerable.Empty<NavAirportWaypoint>())
                                        {
                                            double dKm = DistanceKm(
                                                wp.Lat, wp.Lon,
                                                arrRwy2.ThresholdLat, arrRwy2.ThresholdLon);
                                            if (dKm < MinKm || dKm > lastDistKm) continue;

                                            double bToThr = GeodesicBearing(
                                                wp.Lat, wp.Lon,
                                                arrRwy2.ThresholdLat, arrRwy2.ThresholdLon);
                                            if (HeadingDiff(apBrg2, bToThr) >= MaxAlignErr) continue;

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
                                                arrRwy2.Name, "rwy", null, null, false,
                                                role: "dest", labelText: $"{destIcao}/{arrRwy2.Name}"));

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

                foreach (var wp in wps)
                {
                    string wpStage = wp.Stage ?? "CRZ";
                    if (hasDepRwy && wp.Type == "apt" && wpStage == "CLB") continue;
                    if (hasArrRwy && wp.Type == "apt" && wpStage == "DSC") continue;
                    if (useSidLegs  && wpStage == "CLB") continue;
                    if (useStarLegs && wpStage == "DSC") continue;
                    bool fo = flyoverIdents.Contains(wp.Ident ?? "");
                    allPts.Add((wp.Lat, wp.Lon, wpStage, fo));
                }

                if (useSidLegs)
                {
                    var sidPts = sidProc.Legs
                        .Where(l => l.Lat.HasValue && l.Lon.HasValue)
                        .Select(l => (l.Lat.Value, l.Lon.Value, "CLB", l.IsFlyover))
                        .ToList();
                    allPts.InsertRange(0, sidPts);
                }

                if (useStarLegs)
                {
                    var starPts = starProc.Legs
                        .Where(l => l.Lat.HasValue && l.Lon.HasValue)
                        .Select(l => (l.Lat.Value, l.Lon.Value, "DSC", l.IsFlyover))
                        .ToList();
                    allPts.AddRange(starPts);
                }

                if (noStarThr5 && allPts.Count > 0)
                {
                    int li = allPts.Count - 1;
                    var lp = allPts[li];
                    allPts[li] = (lp.Lat, lp.Lon, lp.Stage, true);
                    if (noStarNoAligned)
                        allPts.Add((noStarNoAlignedLat, noStarNoAlignedLon, "DSC", false));
                    allPts.Add((noStarThr5Lat, noStarThr5Lon, "DSC", false));
                }

                if (noStarAligned && allPts.Count > 0)
                {
                    int li = allPts.Count - 1;
                    var lp = allPts[li];
                    allPts[li] = (lp.Lat, lp.Lon, lp.Stage, true);
                    allPts.Add((noStarAlignedLat,    noStarAlignedLon,    "DSC", false));
                    allPts.Add((noStarAlignedThrLat, noStarAlignedThrLon, "DSC", false));
                }

                if (noSidStub && !useSidLegs && allPts.Count > 0)
                {
                    var f = allPts[0];
                    allPts[0] = (f.Lat, f.Lon, f.Stage, true);
                    allPts.Insert(0, (noSidStubLat, noSidStubLon, "CLB", false));
                }

                if (depEnd.HasValue && allPts.Count > 0)
                {
                    var f = allPts[0];
                    allPts[0] = (f.Lat, f.Lon, f.Stage, true);
                }

                if (depEnd.HasValue)
                    allPts.Insert(0, (depEnd.Value.Lat, depEnd.Value.Lon, "CLB", false));

                InterpolateArcLegs(allPts, sidProc, starProc);
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

                    string role = null, labelText = null;
                    if (!isTodToc)
                    {
                        string stage = wp.Stage ?? "CRZ";
                        if (wp.Type == "apt")
                        {
                            role      = stage == "CLB" ? "origin" : "dest";
                            string rw = stage == "CLB" ? originRunway : destRunway;
                            labelText = string.IsNullOrEmpty(rw) ? wp.Ident : $"{wp.Ident}/{rw}";
                        }
                        else if (wp.Type == "vor" || wp.Type == "dme")
                        {
                            role      = "vor_route";
                            labelText = string.IsNullOrEmpty(wp.Freq) ? wp.Ident : $"{wp.Ident}/{wp.Freq}";
                        }
                        else
                        {
                            role      = stage == "CLB" ? "sid" : stage == "DSC" ? "star" : "enroute";
                            labelText = wp.Ident;
                        }
                    }

                    markers.Add(new FixMarker(
                        new PointLatLng(wp.Lat, wp.Lon), wp.Ident,
                        isTodToc ? "pseudo" : wp.Type, wp.Freq, restr,
                        role: role, labelText: labelText));
                }

                // ── Labels SID / STAR ──
                if (useSidLegs)
                    AddProcedureLabelFromProc(sidProc,  resolvedSid,  markers);
                else
                    AddProcedureLabel(wps, "CLB", resolvedSid,  markers);

                if (useStarLegs)
                    AddProcedureLabelFromProc(starProc, resolvedStar, markers);
                else
                    AddProcedureLabel(wps, "DSC", resolvedStar, markers);

                // ── Etiquetas de distancia y rumbo por leg ───────────────
                {
                    var infoWps = wps.Where(w => w.Type != "latlon").ToList();
                    for (int i = 0; i + 1 < infoWps.Count; i++)
                    {
                        var a = infoWps[i];
                        var b = infoWps[i + 1];
                        double distNm = DistanceKm(a.Lat, a.Lon, b.Lat, b.Lon) / 1.852;
                        if (distNm < 5.0) continue;
                        double trueBrg = GeodesicBearing(a.Lat, a.Lon, b.Lat, b.Lon);
                        double midLat  = (a.Lat + b.Lat) / 2.0;
                        double midLon  = (a.Lon + b.Lon) / 2.0;

                        double displayBrg = a.MagTrack ?? trueBrg;

                        float ang = (float)(trueBrg - 90.0);
                        ang = ((ang % 360f) + 360f) % 360f;
                        if (ang > 180f) ang -= 360f;
                        if (Math.Abs(ang) > 90f)
                            ang = ang > 0 ? ang - 180f : ang + 180f;

                        string label = $"{(int)Math.Round(distNm)}NM {(int)Math.Round(displayBrg)}°";
                        markers.Add(new LegInfoMarker(
                            new PointLatLng(midLat, midLon), label, ang, distNm));
                    }
                }

                // ── Waypoints ambient cerca del destino y del origen ─────
                var routeIdents = new HashSet<string>(
                    wps.Select(w => w.Ident).Where(id => !string.IsNullOrEmpty(id)),
                    StringComparer.OrdinalIgnoreCase);
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
                                if (wp.DistanceNm > 30.0) continue;
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
                                if (wp.DistanceNm > 20.0) continue;
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

                // ── Recopilar datos para el sidebar ──────────────────────
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
                if (_map.IsDisposed || !_map.IsHandleCreated) return;
                _map.BeginInvoke((Action)(() =>
                {
                    if (_map.IsDisposed) return;
                    RouteShadowOverlay.Routes.Clear();
                    RouteOverlay.Routes.Clear();
                    AmbientOverlay.Markers.Clear();
                    WaypointOverlay.Markers.Clear();

                    foreach (var r in shadowRoutes)   RouteShadowOverlay.Routes.Add(r);
                    foreach (var r in colorRoutes)    RouteOverlay.Routes.Add(r);
                    foreach (var m in ambientMarkers) AmbientOverlay.Markers.Add(m);
                    foreach (var m in markers)        WaypointOverlay.Markers.Add(m);

                    AmbientOverlay.IsVisibile = (int)_map.Zoom >= 10;
                    _map.Refresh();

                    if (_aircraftMarker == null)
                    {
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

                    onCompleted?.Invoke(new RouteNavDataResult
                    {
                        OriginRunways = sbOrgRwys,
                        DestRunways   = sbDstRwys,
                        Sids          = sbSids,
                        Stars         = sbStars,
                        Approaches    = sbApps,
                        Ils           = sbIls,
                        OriginInfo    = sbOrgInfo,
                        DestInfo      = sbDstInfo,
                    });
                    _spinner.StopSpin();
                }));
            });
        }
    }
}
