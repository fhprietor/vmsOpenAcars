using System;
using System.Collections.Generic;
using vmsOpenAcars.Db;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Services
{
    public class NavDataService
    {
        private const double MetersPerDegLat  = 111320.0;
        private const double FtPerMeter       = 3.28084;
        private const double RunwayBufferM    = 30.0;
        private const double RunwayWidthScale = 1.0;
        private const double HoldingRadiusM   = 200.0;
        private const double TaxiwayRadiusM   = 300.0;
        private const double ParkingRadiusM   = 200.0;

        // Always true — API credentials are hardcoded; graceful degradation via null returns.
        public bool IsAvailable => true;

        // ── Public API ────────────────────────────────────────────────────────────

        public RunwayTouchdownResult FindTouchdownRunway(
            string airport, double lat, double lon, double heading)
            => ProjectOnRunway(airport, lat, lon, heading);

        public RunwayTouchdownResult FindTakeoffRunway(
            string airport, double lat, double lon, double heading)
            => ProjectOnRunway(airport, lat, lon, heading);

        public RunwayTouchdownResult GetRunwayThreshold(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                var runways = NavDataClient.GetRunways(airport);
                if (runways.Count == 0) return null;

                const double HEADING_TOL_DEG = 15.0;
                const double CROSS_TOL_M     = 3704.0;  // ~2 NM

                NavRunway best      = null;
                double    bestCross = double.MaxValue;
                double    bestDelta = double.MaxValue;

                foreach (var rwy in runways)
                {
                    double d = HeadingDelta(rwy.Heading, heading);
                    if (d > HEADING_TOL_DEG) continue;

                    Project(lat, lon, rwy.ThresholdLat, rwy.ThresholdLon, rwy.Heading,
                            out double along, out double cross);
                    double absCross = Math.Abs(cross);

                    if (absCross > CROSS_TOL_M) continue;
                    if (along > 0) continue;  // already past threshold

                    if (best == null
                        || absCross < bestCross - 50.0
                        || (absCross < bestCross + 50.0 && d < bestDelta))
                    {
                        best = rwy; bestCross = absCross; bestDelta = d;
                    }
                }

                if (best == null) return null;
                return new RunwayTouchdownResult
                {
                    RunwayName       = best.Name,
                    ThresholdLat     = best.ThresholdLat,
                    ThresholdLon     = best.ThresholdLon,
                    ThresholdHeading = best.Heading,
                };
            }
            catch { return null; }
        }

        public static (double DistNm, double LateralFt) ComputeApproachMetrics(
            double thLat, double thLon, double thHdg, double lat, double lon)
        {
            Project(lat, lon, thLat, thLon, thHdg, out double along, out double cross);
            return (-along / 1852.0, cross * FtPerMeter);
        }

        public RunwayEntry FindRunwayEntry(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                var runways = NavDataClient.GetRunways(airport);
                foreach (var rwy in runways)
                {
                    double hdgDelta = HeadingDelta(rwy.Heading, heading);
                    // Skip pure transversal crossings; accept normal entries (≤45°) and backtracks (≥135°)
                    if (hdgDelta > 45.0 && hdgDelta < 135.0) continue;
                    if (!WithinFootprint(lat, lon, rwy)) continue;
                    string twy = NearestTaxiway(NavDataClient.GetTaxiways(airport), lat, lon);
                    return new RunwayEntry { RunwayName = rwy.Name, TaxiwayName = twy, IsBacktrack = hdgDelta >= 135.0 };
                }
                return null;
            }
            catch { return null; }
        }

        public string FindNearestTaxiway(string airport, double lat, double lon,
            double heading = double.NaN)
        {
            try
            {
                return NearestTaxiway(NavDataClient.GetTaxiways(airport), lat, lon, heading);
            }
            catch { return null; }
        }

        // Returns the geographic bearing of the nearest segment of the named taxiway,
        // or double.NaN if not found. Used to apply the angular transition criterion.
        public double FindTaxiwaySegmentBearing(string airport, string taxiwayName,
            double lat, double lon)
        {
            if (string.IsNullOrEmpty(taxiwayName)) return double.NaN;
            try
            {
                var taxiways = NavDataClient.GetTaxiways(airport);
                double bestDist = double.MaxValue;
                double result   = double.NaN;
                foreach (var twy in taxiways)
                {
                    if (!string.Equals(twy.Name, taxiwayName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    double d = DistToSegM(lat, lon, twy.StartLat, twy.StartLon,
                                          twy.EndLat,   twy.EndLon);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        result   = BearingDeg(twy.StartLat, twy.StartLon,
                                              twy.EndLat,   twy.EndLon);
                    }
                }
                return result;
            }
            catch { return double.NaN; }
        }

        public string FindNextIntersection(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                var taxiways = NavDataClient.GetTaxiways(airport);
                string current = NearestTaxiway(taxiways, lat, lon, heading);
                return string.IsNullOrEmpty(current) ? null
                     : NextIntersection(taxiways, lat, lon, heading, current);
            }
            catch { return null; }
        }

        public HoldingPoint FindHoldingPoint(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                var holdShorts = NavDataClient.GetHoldShorts(airport);
                NavHoldShort best = null;
                double bestDist   = double.MaxValue;

                foreach (var hs in holdShorts)
                {
                    if (HeadingDelta(hs.Heading, heading) > 45.0) continue;
                    double d = DistM(lat, lon, hs.Lat, hs.Lon);
                    if (d < HoldingRadiusM && d < bestDist) { bestDist = d; best = hs; }
                }

                if (best == null) return null;
                string twy = NearestTaxiway(NavDataClient.GetTaxiways(airport), lat, lon);
                return new HoldingPoint { RunwayName = best.RunwayName, TaxiwayName = twy };
            }
            catch { return null; }
        }

        public ParkingSpot FindNearestParking(string airport, double lat, double lon)
        {
            try
            {
                var parkings = NavDataClient.GetParkings(airport);
                NavParking best = null;
                double bestDist  = double.MaxValue;

                foreach (var p in parkings)
                {
                    double d = DistM(lat, lon, p.Lat, p.Lon);
                    if (d < ParkingRadiusM && d < bestDist) { bestDist = d; best = p; }
                }

                if (best == null) return null;
                return new ParkingSpot { DisplayName = BuildParkingName(best) };
            }
            catch { return null; }
        }

        // ── ILS / Approach API ────────────────────────────────────────────────────

        public IlsData GetIlsForRunway(string airport, string runwayName)
        {
            if (string.IsNullOrEmpty(airport)) return null;
            try
            {
                // Primary path: /ils/ endpoint provides loc_true_heading (true, not magnetic)
                // and glideslope.altitude_ft (real DA altitude). Fixes the same magnetic-variation
                // error class as TrueRunwayBearing() fixed for touchdown metrics.
                var ilsList = NavDataClient.GetIls(airport);
                if (ilsList != null)
                {
                    foreach (var ils in ilsList)
                    {
                        if (!string.Equals(ils.Runway, runwayName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (ils.FrequencyMhz < 108.0 || !ils.LocTrueHeading.HasValue) continue;
                        if (ils.Glideslope == null || !(ils.Glideslope.PitchDeg > 0.1)) continue;

                        double thLat = 0, thLon = 0, thElev = 0;
                        foreach (var rwy in NavDataClient.GetRunways(airport))
                        {
                            if (!string.Equals(rwy.Name, runwayName, StringComparison.OrdinalIgnoreCase)) continue;
                            thLat  = rwy.ThresholdLat;
                            thLon  = rwy.ThresholdLon;
                            thElev = rwy.ElevationFt;
                            break;
                        }

                        return new IlsData
                        {
                            FrequencyMhz    = ils.FrequencyMhz,
                            Course          = ils.LocTrueHeading.Value,
                            GlideSlopePitch = ils.Glideslope.PitchDeg,
                            RunwayName      = ils.Runway,
                            ThresholdLat    = thLat,
                            ThresholdLon    = thLon,
                            ThresholdElevFt = thElev,
                            GlideslopeAltFt = ils.Glideslope.AltitudeFt,
                        };
                    }
                }

                // Fallback: derive from runway record (magnetic ils_course — less accurate)
                foreach (var rwy in NavDataClient.GetRunways(airport))
                {
                    if (!string.Equals(rwy.Name, runwayName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!rwy.HasIls || !rwy.IlsFreqMhz.HasValue) continue;
                    if (!(rwy.IlsGlideslopeAngle > 0.1)) continue;

                    return new IlsData
                    {
                        FrequencyMhz    = rwy.IlsFreqMhz.Value,
                        Course          = rwy.IlsCourse ?? rwy.Heading,
                        GlideSlopePitch = rwy.IlsGlideslopeAngle ?? 3.0,
                        RunwayName      = rwy.Name,
                        ThresholdLat    = rwy.ThresholdLat,
                        ThresholdLon    = rwy.ThresholdLon,
                        ThresholdElevFt = rwy.ElevationFt,
                    };
                }
            }
            catch { }
            return null;
        }

        public ApproachInfo GetApproachType(string airport, string runwayName)
        {
            if (string.IsNullOrEmpty(airport)) return null;
            try
            {
                NavApproach best = null;
                int bestPriority = int.MaxValue;

                foreach (var ap in NavDataClient.GetApproaches(airport))
                {
                    if (!string.Equals(ap.Runway, runwayName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    int priority = ap.Type == "ILS" ? 1 : ap.Type == "RNAV" ? 2 : 3;
                    if (priority < bestPriority) { bestPriority = priority; best = ap; }
                }

                if (best == null) return null;
                bool hasVert = best.Type == "ILS" || best.Type == "LPV" || best.Type == "GLS"
                            || best.HasVerticalAngle;
                return new ApproachInfo
                {
                    ApproachId          = 0,
                    Type                = best.Type,
                    RunwayName          = best.Runway,
                    HasVerticalGuidance = hasVert,
                };
            }
            catch { }
            return null;
        }

        // Signature changed from (int approachId) to (string airport, string runway).
        public IList<ApproachFix> GetApproachFixes(string airport, string runwayName)
        {
            var list = new List<ApproachFix>();
            if (string.IsNullOrEmpty(airport)) return list;
            try
            {
                NavApproach target = null;
                int bestPriority   = int.MaxValue;

                foreach (var ap in NavDataClient.GetApproaches(airport))
                {
                    if (!string.Equals(ap.Runway, runwayName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    int priority = ap.Type == "ILS" ? 1 : ap.Type == "RNAV" ? 2 : 3;
                    if (priority < bestPriority) { bestPriority = priority; target = ap; }
                }

                if (target == null) return list;

                foreach (var leg in target.Legs)
                {
                    if (!leg.Lat.HasValue || !leg.Lon.HasValue) continue;
                    if (string.IsNullOrEmpty(leg.Fix)) continue;
                    list.Add(new ApproachFix
                    {
                        Name       = leg.Fix,
                        FixType    = leg.Type ?? "",
                        Lat        = leg.Lat.Value,
                        Lon        = leg.Lon.Value,
                        AltitudeFt = leg.AltitudeFt,
                        IsFlyover  = leg.IsFlyover,
                    });
                }
            }
            catch { }
            return list;
        }

        // ── Prefetch ──────────────────────────────────────────────────────────────

        public void PrefetchAirport(string icao) => NavDataClient.PrefetchAirport(icao);

        // ── Core projection ───────────────────────────────────────────────────────

        private static RunwayTouchdownResult ProjectOnRunway(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                var runways = NavDataClient.GetRunways(airport);
                if (runways.Count == 0) return null;

                NavRunway best      = null;
                double    bestDelta = double.MaxValue;

                foreach (var rwy in runways)
                {
                    double d = HeadingDelta(rwy.Heading, heading);
                    if (d < 45.0 && d < bestDelta) { bestDelta = d; best = rwy; }
                }

                if (best == null) return null;

                // Parallel runway disambiguation
                if (!WithinFootprint(lat, lon, best))
                {
                    NavRunway alt      = null;
                    double    altDelta = double.MaxValue;
                    foreach (var rwy in runways)
                    {
                        double d = HeadingDelta(rwy.Heading, heading);
                        if (d < 45.0 && d < altDelta && WithinFootprint(lat, lon, rwy))
                        { altDelta = d; alt = rwy; }
                    }
                    if (alt != null) best = alt;
                }

                // Project on the runway's TRUE geographic bearing (computed from endpoint
                // coordinates) rather than rwy.Heading (magnetic) or the aircraft heading.
                // rwy.Heading is magnetic and produces a projection-axis error of
                //   along × sin(magvar) — up to 600 ft at airports with variation ≥ 13°.
                // The aircraft heading at touchdown approximates the runway axis but includes
                // any crosswind crab angle, which is irrelevant to centreline deviation and
                // introduces errors proportional to sin(crab) × distance from threshold.
                // TrueRunwayBearing() derives the geographic bearing directly from the
                // NavData endpoint coordinates, which are WGS-84 and free of magnetic effects.
                double trueBrg = TrueRunwayBearing(best);
                Project(lat, lon, best.ThresholdLat, best.ThresholdLon, trueBrg,
                        out double along, out double cross);

                return new RunwayTouchdownResult
                {
                    RunwayName            = best.Name,
                    ThresholdDistanceFt   = Math.Max(0.0, along * FtPerMeter),
                    CenterlineDeviationFt = Math.Abs(cross) * FtPerMeter,
                    ThresholdLat          = best.ThresholdLat,
                    ThresholdLon          = best.ThresholdLon,
                    ThresholdHeading      = best.Heading,
                };
            }
            catch { return null; }
        }

        // ── Geometry ─────────────────────────────────────────────────────────────

        private static void Project(
            double lat, double lon, double thLat, double thLon, double thHdg,
            out double along, out double cross)
        {
            double rad    = thHdg * Math.PI / 180.0;
            double cosLat = Math.Cos(thLat * Math.PI / 180.0);
            double dN = (lat - thLat) * MetersPerDegLat;
            double dE = (lon - thLon) * MetersPerDegLat * cosLat;
            along = dE * Math.Sin(rad) + dN * Math.Cos(rad);
            cross = dE * Math.Cos(rad) - dN * Math.Sin(rad);
        }

        private static bool WithinFootprint(double lat, double lon, NavRunway rwy)
        {
            // Use the geographic (true) bearing computed from the runway's endpoint
            // coordinates rather than rwy.Heading (which is magnetic). At airports with
            // significant magnetic variation (e.g. TJSJ ≈ −14°, KEWR ≈ −13°) using the
            // magnetic heading as a geographic axis produces hundreds of feet of spurious
            // cross-track offset, causing taxiways parallel to the runway to appear inside
            // the footprint.
            double trueBearing = TrueRunwayBearing(rwy);
            Project(lat, lon, rwy.ThresholdLat, rwy.ThresholdLon, trueBearing,
                    out double along, out double cross);
            double halfW = rwy.WidthFt / FtPerMeter / 2.0 * RunwayWidthScale;
            double lenM  = rwy.LengthFt / FtPerMeter;
            return along >= -RunwayBufferM
                && along <= lenM + RunwayBufferM
                && Math.Abs(cross) <= halfW;
        }

        private static double TrueRunwayBearing(NavRunway rwy)
        {
            double cosLat = Math.Cos(rwy.ThresholdLat * Math.PI / 180.0);
            double dN = (rwy.EndLat - rwy.ThresholdLat) * MetersPerDegLat;
            double dE = (rwy.EndLon - rwy.ThresholdLon) * MetersPerDegLat * cosLat;
            return (Math.Atan2(dE, dN) * 180.0 / Math.PI + 360.0) % 360.0;
        }

        private static double DistM(double lat1, double lon1, double lat2, double lon2)
        {
            double cosLat = Math.Cos((lat1 + lat2) * 0.5 * Math.PI / 180.0);
            double dN = (lat2 - lat1) * MetersPerDegLat;
            double dE = (lon2 - lon1) * MetersPerDegLat * cosLat;
            return Math.Sqrt(dN * dN + dE * dE);
        }

        private static double DistToSegM(
            double lat, double lon,
            double sLat, double sLon, double eLat, double eLon)
        {
            double cosLat = Math.Cos((sLat + eLat) * 0.5 * Math.PI / 180.0);
            double px = (lon - sLon) * MetersPerDegLat * cosLat;
            double py = (lat - sLat) * MetersPerDegLat;
            double dx = (eLon - sLon) * MetersPerDegLat * cosLat;
            double dy = (eLat - sLat) * MetersPerDegLat;
            double lenSq = dx * dx + dy * dy;
            double t = lenSq < 1e-10
                ? 0.0
                : Math.Max(0.0, Math.Min(1.0, (px * dx + py * dy) / lenSq));
            double ex = px - t * dx;
            double ey = py - t * dy;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        private static double HeadingDelta(double a, double b)
        {
            double d = Math.Abs(a - b) % 360.0;
            return d > 180.0 ? 360.0 - d : d;
        }

        private static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
        {
            double cosLat = Math.Cos(lat1 * Math.PI / 180.0);
            double dE = (lon2 - lon1) * MetersPerDegLat * cosLat;
            double dN = (lat2 - lat1) * MetersPerDegLat;
            return (Math.Atan2(dE, dN) * 180.0 / Math.PI + 360.0) % 360.0;
        }

        private static double ProjectOnSeg(
            double lat, double lon, double sLat, double sLon, double eLat, double eLon)
        {
            double cosLat = Math.Cos((sLat + eLat) * 0.5 * Math.PI / 180.0);
            double px = (lon - sLon) * MetersPerDegLat * cosLat;
            double py = (lat - sLat) * MetersPerDegLat;
            double dx = (eLon - sLon) * MetersPerDegLat * cosLat;
            double dy = (eLat - sLat) * MetersPerDegLat;
            double lenSq = dx * dx + dy * dy;
            return lenSq < 1e-10 ? 0.0 : Math.Max(0.0, Math.Min(1.0, (px * dx + py * dy) / lenSq));
        }

        private static string NearestTaxiway(
            List<NavTaxiway> taxiways, double lat, double lon, double heading = double.NaN)
        {
            string bestName  = null;
            double bestScore = double.MaxValue;
            bool   useHdg    = !double.IsNaN(heading);

            foreach (var twy in taxiways)
            {
                if (string.IsNullOrEmpty(twy.Name)) continue;

                double d = DistToSegM(lat, lon, twy.StartLat, twy.StartLon, twy.EndLat, twy.EndLon);
                if (d >= TaxiwayRadiusM) continue;

                double score = d;
                if (useHdg && d > 1.0)
                {
                    double brg   = BearingDeg(twy.StartLat, twy.StartLon, twy.EndLat, twy.EndLon);
                    double delta = Math.Min(HeadingDelta(heading, brg),
                                            HeadingDelta(heading, (brg + 180.0) % 360.0));
                    if (delta > 50.0) score *= 2.5;
                }

                if (score < bestScore) { bestScore = score; bestName = twy.Name; }
            }
            return bestName;
        }

        private static string NextIntersection(
            List<NavTaxiway> taxiways, double lat, double lon, double heading,
            string currentTaxiway)
        {
            string bestName = null;
            double bestDist = double.MaxValue;

            foreach (var twy in taxiways)
            {
                if (string.IsNullOrEmpty(twy.Name)) continue;
                if (twy.Name == currentTaxiway) continue;

                double t    = ProjectOnSeg(lat, lon, twy.StartLat, twy.StartLon, twy.EndLat, twy.EndLon);
                double cLat = twy.StartLat + t * (twy.EndLat - twy.StartLat);
                double cLon = twy.StartLon + t * (twy.EndLon - twy.StartLon);
                double dist = DistM(lat, lon, cLat, cLon);

                if (dist > 2000.0) continue;
                if (HeadingDelta(BearingDeg(lat, lon, cLat, cLon), heading) > 60.0) continue;

                if (dist < bestDist) { bestDist = dist; bestName = twy.Name; }
            }
            return bestName;
        }

        private static string BuildParkingName(NavParking p)
        {
            string n      = p.Number.HasValue && p.Number > 0 ? p.Number.ToString() : "";
            string result = ((p.Name ?? "") + n + (p.Suffix ?? "")).Trim();
            return string.IsNullOrEmpty(result) ? "RAMP" : result;
        }
    }
}
