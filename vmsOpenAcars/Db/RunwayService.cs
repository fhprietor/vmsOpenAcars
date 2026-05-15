using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace vmsOpenAcars.Db
{
    // ─── Result types ────────────────────────────────────────────────────────────

    public class RunwayTouchdownResult
    {
        public string RunwayName            { get; set; }
        public double ThresholdDistanceFt   { get; set; }
        public double CenterlineDeviationFt { get; set; }
        // Populated for approach-capture use (GetRunwayThreshold / FindTouchdownRunway)
        public double ThresholdLat          { get; set; }
        public double ThresholdLon          { get; set; }
        public double ThresholdHeading      { get; set; }
    }

    public class RunwayEntry
    {
        public string RunwayName  { get; set; }
        public string TaxiwayName { get; set; }
    }

    public class HoldingPoint
    {
        public string RunwayName  { get; set; }
        public string TaxiwayName { get; set; }
    }

    public class ParkingSpot
    {
        public string DisplayName { get; set; }
    }

    // ─── ILS / Approach result types ─────────────────────────────────────────────

    public class IlsData
    {
        public double FrequencyMhz     { get; set; }
        public double Course           { get; set; }  // localizer course, magnetic degrees
        public double GlideSlopePitch  { get; set; }  // degrees (> 0 = full ILS, 0 = LOC-only)
        public string RunwayName       { get; set; }
        public double ThresholdLat     { get; set; }
        public double ThresholdLon     { get; set; }
        public double ThresholdElevFt  { get; set; }  // airport elevation used for DA calculation
    }

    public class ApproachInfo
    {
        public int    ApproachId          { get; set; }
        public string Type                { get; set; }  // "ILS", "RNAV", "NDB", "VOR", "GPS", "LOC"…
        public string RunwayName          { get; set; }
        public bool   HasVerticalGuidance { get; set; }  // true for ILS / LPV / GLS
    }

    public class ApproachFix
    {
        public string Name       { get; set; }
        public string FixType    { get; set; }  // "IF", "FAF", "MAP", "" for plain fixes
        public double Lat        { get; set; }
        public double Lon        { get; set; }
        public double AltitudeFt { get; set; }
    }

    // ─── Internal DTO ─────────────────────────────────────────────────────────────

    internal class RunwayEndInfo
    {
        public string Name     { get; set; }
        public double Heading  { get; set; }
        public double Lat      { get; set; }
        public double Lon      { get; set; }
        public double WidthFt  { get; set; }
        public double LengthFt { get; set; }
    }

    // ─── RunwayService ────────────────────────────────────────────────────────────

    public class RunwayService
    {
        private readonly string _dbPath;

        private const double MetersPerDegLat  = 111320.0;
        private const double FtPerMeter       = 3.28084;
        private const double RunwayBufferM    = 30.0;   // tolerance before threshold / past far end
        private const double RunwayWidthScale = 1.0;    // half-width multiplier for entry detection
        private const double HoldingRadiusM   = 200.0;
        private const double TaxiwayRadiusM   = 300.0;
        private const double ParkingRadiusM   = 200.0;

        public bool IsAvailable =>
            !string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath);

        public RunwayService(string dbPath) { _dbPath = dbPath; }

        // ── Public API ────────────────────────────────────────────────────────────

        public RunwayTouchdownResult FindTouchdownRunway(
            string airport, double lat, double lon, double heading)
            => SafeProjectOnRunway(airport, lat, lon, heading);

        public RunwayTouchdownResult FindTakeoffRunway(
            string airport, double lat, double lon, double heading)
            => SafeProjectOnRunway(airport, lat, lon, heading);

        /// <summary>
        /// Returns threshold position/heading for a runway matching <paramref name="heading"/> ±45°.
        /// Used to set up approach-track capture before landing.
        /// When parallel runways share a similar heading (e.g. 14L/14R), the one whose
        /// centreline the aircraft is laterally closest to is preferred.
        /// </summary>
        public RunwayTouchdownResult GetRunwayThreshold(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                if (!IsAvailable) return null;
                using (var conn = OpenConn())
                {
                    long apId = GetAirportId(conn, airport);
                    if (apId < 0) return null;

                    // Acceptance criteria for an "intent to land on this runway":
                    //   1. heading aligns within HEADING_TOL_DEG with runway heading.
                    //   2. lateral distance to extended centreline within CROSS_TOL_M.
                    //   3. aircraft is BEFORE the threshold in landing direction (along < 0
                    //      with our convention, i.e. on the approach side).
                    // If multiple runways qualify, pick the one with smallest |cross|;
                    // if still tied, smallest heading delta.
                    const double HEADING_TOL_DEG = 15.0;
                    const double CROSS_TOL_M     = 3704.0;   // ~2 NM

                    RunwayEndInfo best      = null;
                    double        bestCross = double.MaxValue;
                    double        bestDelta = double.MaxValue;

                    foreach (var end in GetRunwayEnds(conn, apId))
                    {
                        double d = HeadingDelta(end.Heading, heading);
                        if (d > HEADING_TOL_DEG) continue;

                        Project(lat, lon, end.Lat, end.Lon, end.Heading,
                                out double along, out double cross);
                        double absCross = Math.Abs(cross);

                        if (absCross > CROSS_TOL_M) continue;
                        if (along > 0) continue;     // already past the threshold

                        if (best == null
                            || absCross < bestCross - 50.0
                            || (absCross < bestCross + 50.0 && d < bestDelta))
                        {
                            best      = end;
                            bestCross = absCross;
                            bestDelta = d;
                        }
                    }

                    if (best == null) return null;

                    return new RunwayTouchdownResult
                    {
                        RunwayName       = best.Name,
                        ThresholdLat     = best.Lat,
                        ThresholdLon     = best.Lon,
                        ThresholdHeading = best.Heading
                    };
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Computes distance-to-threshold and signed lateral deviation for an approaching aircraft.
        /// DistNm  > 0 means aircraft is before the threshold (normal approach).
        /// LateralFt positive = right of centreline.
        /// </summary>
        public static (double DistNm, double LateralFt) ComputeApproachMetrics(
            double thLat, double thLon, double thHdg, double lat, double lon)
        {
            Project(lat, lon, thLat, thLon, thHdg, out double along, out double cross);
            double distNm   = -along / 1852.0;          // negative along → positive dist before threshold
            double lateralFt = cross * FtPerMeter;       // signed: + right, – left
            return (distNm, lateralFt);
        }

        public RunwayEntry FindRunwayEntry(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                if (!IsAvailable) return null;
                using (var conn = OpenConn())
                {
                    long apId = GetAirportId(conn, airport);
                    if (apId < 0) return null;

                    foreach (var end in GetRunwayEnds(conn, apId))
                    {
                        if (HeadingDelta(end.Heading, heading) > 45.0) continue;
                        if (!WithinFootprint(lat, lon, end)) continue;
                        string twy = NearestTaxiway(conn, apId, lat, lon);
                        return new RunwayEntry { RunwayName = end.Name, TaxiwayName = twy };
                    }
                    return null;
                }
            }
            catch { return null; }
        }

        public string FindNearestTaxiway(string airport, double lat, double lon,
            double heading = double.NaN)
        {
            try
            {
                if (!IsAvailable) return null;
                using (var conn = OpenConn())
                {
                    long apId = GetAirportId(conn, airport);
                    return apId < 0 ? null : NearestTaxiway(conn, apId, lat, lon, heading);
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Next intersection ahead of the aircraft's heading (different taxiway name).
        /// Returns the taxiway name that will be crossed next, or null if none ahead.
        /// </summary>
        public string FindNextIntersection(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                if (!IsAvailable) return null;
                using (var conn = OpenConn())
                {
                    long apId = GetAirportId(conn, airport);
                    if (apId < 0) return null;

                    string current = NearestTaxiway(conn, apId, lat, lon, heading);
                    return string.IsNullOrEmpty(current) ? null
                         : NextIntersection(conn, apId, lat, lon, heading, current);
                }
            }
            catch { return null; }
        }

        public HoldingPoint FindHoldingPoint(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                if (!IsAvailable) return null;
                using (var conn = OpenConn())
                {
                    long apId = GetAirportId(conn, airport);
                    if (apId < 0) return null;

                    RunwayEndInfo best = null;
                    double bestDist    = double.MaxValue;

                    foreach (var end in GetRunwayEnds(conn, apId))
                    {
                        if (HeadingDelta(end.Heading, heading) > 45.0) continue;
                        double d = DistM(lat, lon, end.Lat, end.Lon);
                        if (d < HoldingRadiusM && d < bestDist) { bestDist = d; best = end; }
                    }

                    if (best == null) return null;
                    string twy = NearestTaxiway(conn, apId, lat, lon);
                    return new HoldingPoint { RunwayName = best.Name, TaxiwayName = twy };
                }
            }
            catch { return null; }
        }

        public ParkingSpot FindNearestParking(string airport, double lat, double lon)
        {
            try
            {
                if (!IsAvailable) return null;
                using (var conn = OpenConn())
                {
                    long apId = GetAirportId(conn, airport);
                    if (apId < 0) return null;

                    const string sql =
                        "SELECT name, number, suffix, lonx, laty " +
                        "FROM parking WHERE airport_id = @aid";

                    ParkingSpot best = null;
                    double bestDist  = double.MaxValue;

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@aid", apId);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                double d = DistM(lat, lon, r.GetDouble(4), r.GetDouble(3));
                                if (d < ParkingRadiusM && d < bestDist)
                                {
                                    bestDist = d;
                                    best = new ParkingSpot
                                    {
                                        DisplayName = BuildParkingName(
                                            r.IsDBNull(0) ? "" : r.GetString(0),
                                            r.GetInt32(1),
                                            r.IsDBNull(2) ? "" : r.GetString(2))
                                    };
                                }
                            }
                        }
                    }
                    return best;
                }
            }
            catch { return null; }
        }

        // ── Core projection ───────────────────────────────────────────────────────

        private RunwayTouchdownResult SafeProjectOnRunway(
            string airport, double lat, double lon, double heading)
        {
            try
            {
                if (!IsAvailable) return null;
                using (var conn = OpenConn())
                {
                    long apId = GetAirportId(conn, airport);
                    if (apId < 0) return null;

                    var ends = GetRunwayEnds(conn, apId);
                    RunwayEndInfo best = null;
                    double bestDelta   = double.MaxValue;

                    foreach (var end in ends)
                    {
                        double d = HeadingDelta(end.Heading, heading);
                        if (d < 45.0 && d < bestDelta) { bestDelta = d; best = end; }
                    }

                    if (best == null) return null;

                    // Parallel runway disambiguation: if position is not within the
                    // best-heading runway footprint, prefer a different runway with a
                    // heading close enough that actually contains the point.
                    if (!WithinFootprint(lat, lon, best))
                    {
                        RunwayEndInfo alt = null;
                        double altDelta = double.MaxValue;
                        foreach (var end in ends)
                        {
                            double d = HeadingDelta(end.Heading, heading);
                            if (d < 45.0 && d < altDelta && WithinFootprint(lat, lon, end))
                            { altDelta = d; alt = end; }
                        }
                        if (alt != null) best = alt;
                    }

                    Project(lat, lon, best.Lat, best.Lon, best.Heading,
                            out double along, out double cross);

                    return new RunwayTouchdownResult
                    {
                        RunwayName            = best.Name,
                        ThresholdDistanceFt   = Math.Max(0.0, along * FtPerMeter),
                        CenterlineDeviationFt = Math.Abs(cross) * FtPerMeter,
                        ThresholdLat          = best.Lat,
                        ThresholdLon          = best.Lon,
                        ThresholdHeading      = best.Heading
                    };
                }
            }
            catch { return null; }
        }

        // ── Geometry ──────────────────────────────────────────────────────────────

        // Flat-earth projection of (lat, lon) onto runway axis.
        // Threshold at (thLat, thLon), heading thHdg° from North.
        // along > 0 → toward far end of runway.
        // cross > 0 → right of centreline.
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

        private static bool WithinFootprint(double lat, double lon, RunwayEndInfo end)
        {
            Project(lat, lon, end.Lat, end.Lon, end.Heading, out double along, out double cross);
            double halfW = end.WidthFt / FtPerMeter / 2.0 * RunwayWidthScale;
            double lenM  = end.LengthFt / FtPerMeter;
            return along >= -RunwayBufferM
                && along <= lenM + RunwayBufferM
                && Math.Abs(cross) <= halfW;
        }

        // Flat-earth distance in metres between two lat/lon points.
        private static double DistM(double lat1, double lon1, double lat2, double lon2)
        {
            double cosLat = Math.Cos((lat1 + lat2) * 0.5 * Math.PI / 180.0);
            double dN = (lat2 - lat1) * MetersPerDegLat;
            double dE = (lon2 - lon1) * MetersPerDegLat * cosLat;
            return Math.Sqrt(dN * dN + dE * dE);
        }

        // Shortest distance from point (lat, lon) to segment (sLat,sLon)→(eLat,eLon).
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

        // Smallest angular difference between two headings (0–180°).
        private static double HeadingDelta(double a, double b)
        {
            double d = Math.Abs(a - b) % 360.0;
            return d > 180.0 ? 360.0 - d : d;
        }

        private static string BuildParkingName(string name, int number, string suffix)
        {
            string n      = number > 0 ? number.ToString() : "";
            string result = (name + n + suffix).Trim();
            return string.IsNullOrEmpty(result) ? "RAMP" : result;
        }

        // Bearing from (lat1,lon1) to (lat2,lon2) in degrees (0–360).
        private static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
        {
            double cosLat = Math.Cos(lat1 * Math.PI / 180.0);
            double dE = (lon2 - lon1) * MetersPerDegLat * cosLat;
            double dN = (lat2 - lat1) * MetersPerDegLat;
            return (Math.Atan2(dE, dN) * 180.0 / Math.PI + 360.0) % 360.0;
        }

        // Projection parameter t (0..1) of closest point on segment from (lat,lon).
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

        // Nearest taxiway with a DIFFERENT name ahead of (lat,lon) heading <hdg>.
        private string NextIntersection(
            SQLiteConnection conn, long airportId, double lat, double lon, double heading,
            string currentTaxiway)
        {
            const string sql = @"
                SELECT DISTINCT name, start_lonx, start_laty, end_lonx, end_laty
                FROM   taxi_path
                WHERE  airport_id = @aid
                  AND  type = 'T'
                  AND  name IS NOT NULL AND name != ''
                  AND  name != @cur";

            string bestName = null;
            double bestDist = double.MaxValue;

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@aid", airportId);
                cmd.Parameters.AddWithValue("@cur", currentTaxiway);
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string name = rdr.GetString(0);
                        double sLat = rdr.GetDouble(2), sLon = rdr.GetDouble(1);
                        double eLat = rdr.GetDouble(4), eLon = rdr.GetDouble(3);

                        double t     = ProjectOnSeg(lat, lon, sLat, sLon, eLat, eLon);
                        double cLat  = sLat + t * (eLat - sLat);
                        double cLon  = sLon + t * (eLon - sLon);
                        double dist  = DistM(lat, lon, cLat, cLon);

                        if (dist > 2000.0) continue;
                        if (HeadingDelta(BearingDeg(lat, lon, cLat, cLon), heading) > 60.0) continue;

                        if (dist < bestDist) { bestDist = dist; bestName = name; }
                    }
                }
            }
            return bestName;
        }

        // ── Database ──────────────────────────────────────────────────────────────

        private SQLiteConnection OpenConn()
        {
            var c = new SQLiteConnection($"Data Source={_dbPath};Read Only=True;");
            c.Open();
            return c;
        }

        private static long GetAirportId(SQLiteConnection conn, string icao)
        {
            const string sql = "SELECT airport_id FROM airport WHERE ident = @icao LIMIT 1";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@icao", icao);
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? -1L : Convert.ToInt64(v);
            }
        }

        // ── ILS / Approach API ────────────────────────────────────────────────────

        /// <summary>
        /// Returns ILS data for the named runway at <paramref name="airport"/>.
        /// Filters ILS from LOC using gs_pitch > 0.1.
        /// Returns null if not available or DB not configured.
        /// </summary>
        public IlsData GetIlsForRunway(string airport, string runwayName)
        {
            if (!IsAvailable || string.IsNullOrEmpty(airport)) return null;
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    // Airport elevation for DA calculation
                    double apElevFt = 0;
                    const string elevSql = "SELECT altitude FROM airport WHERE ident = @ap LIMIT 1";
                    using (var cmd = new SQLiteCommand(elevSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ap", airport);
                        object v = cmd.ExecuteScalar();
                        if (v != null && v != DBNull.Value) apElevFt = Convert.ToDouble(v);
                    }

                    const string sql = @"
                        SELECT i.frequency, i.locheading, i.gs_pitch,
                               re.name, re.laty, re.lonx
                        FROM   ils i
                        JOIN   runway_end re ON re.runway_end_id = i.runway_end_id
                        JOIN   runway rw     ON rw.primary_end_id   = re.runway_end_id
                                            OR rw.secondary_end_id = re.runway_end_id
                        JOIN   airport ap    ON ap.airport_id = rw.airport_id
                        WHERE  ap.ident   = @ap
                          AND  i.gs_pitch > 0.1
                          AND  re.name    = @rwy
                        LIMIT  1";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ap",  airport);
                        cmd.Parameters.AddWithValue("@rwy", runwayName ?? "");
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                return new IlsData
                                {
                                    FrequencyMhz    = rdr.GetInt32(0) / 1000.0,
                                    Course          = rdr.GetDouble(1),
                                    GlideSlopePitch = rdr.GetDouble(2),
                                    RunwayName      = rdr.GetString(3),
                                    ThresholdLat    = rdr.GetDouble(4),
                                    ThresholdLon    = rdr.GetDouble(5),
                                    ThresholdElevFt = apElevFt,
                                };
                            }
                        }
                    }
                }
            }
            catch { /* DB unavailable or schema mismatch — return null */ }
            return null;
        }

        /// <summary>
        /// Returns the best approach procedure for <paramref name="runwayName"/> at <paramref name="airport"/>.
        /// Prefers ILS over RNAV over other types.
        /// Falls back to runway_end join when approach.runway_name is empty.
        /// Returns null if not found.
        /// </summary>
        public ApproachInfo GetApproachType(string airport, string runwayName)
        {
            if (!IsAvailable || string.IsNullOrEmpty(airport)) return null;
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    // Primary lookup: match runway_name directly
                    const string sql = @"
                        SELECT a.approach_id, a.type, a.runway_name
                        FROM   approach a
                        JOIN   airport ap ON ap.airport_id = a.airport_id
                        WHERE  ap.ident = @ap
                          AND  a.runway_name = @rwy
                        ORDER  BY CASE a.type
                                    WHEN 'ILS'  THEN 1
                                    WHEN 'RNAV' THEN 2
                                    ELSE             3
                                  END
                        LIMIT  1";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ap",  airport);
                        cmd.Parameters.AddWithValue("@rwy", runwayName ?? "");
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                                return BuildApproachInfo(rdr);
                        }
                    }

                    // Fallback: runway_name is empty — join through runway_end
                    const string fallbackSql = @"
                        SELECT a.approach_id, a.type, a.runway_name
                        FROM   approach a
                        JOIN   airport   ap ON ap.airport_id = a.airport_id
                        JOIN   runway_end re ON re.runway_end_id = a.runway_end_id
                        WHERE  ap.ident = @ap
                          AND  re.name  = @rwy
                        ORDER  BY CASE a.type
                                    WHEN 'ILS'  THEN 1
                                    WHEN 'RNAV' THEN 2
                                    ELSE             3
                                  END
                        LIMIT  1";
                    using (var cmd = new SQLiteCommand(fallbackSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ap",  airport);
                        cmd.Parameters.AddWithValue("@rwy", runwayName ?? "");
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                                return BuildApproachInfo(rdr);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static ApproachInfo BuildApproachInfo(SQLiteDataReader rdr)
        {
            string type = rdr.IsDBNull(1) ? "Unknown" : rdr.GetString(1);
            bool hasVert = type == "ILS" || type == "LPV" || type == "GLS";
            return new ApproachInfo
            {
                ApproachId          = rdr.GetInt32(0),
                Type                = type,
                RunwayName          = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                HasVerticalGuidance = hasVert,
            };
        }

        /// <summary>
        /// Returns named approach fixes (IF, FAF, MAP) for the given approach procedure,
        /// ordered by sequence. Skips fixes without coordinates.
        /// </summary>
        public IList<ApproachFix> GetApproachFixes(int approachId)
        {
            var list = new List<ApproachFix>();
            if (!IsAvailable || approachId <= 0) return list;
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    const string sql = @"
                        SELECT al.fix_name, al.approach_fix_type, al.laty, al.lonx, al.altitude1
                        FROM   approach_leg al
                        WHERE  al.approach_id = @id
                          AND  al.approach_fix_type IN ('B','F','M')
                          AND  al.lonx IS NOT NULL
                          AND  al.laty IS NOT NULL
                        ORDER  BY al.approach_leg_id";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", approachId);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string fixTypeCode = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                                string fixTypeLabel = fixTypeCode == "B" ? "IF"
                                                    : fixTypeCode == "F" ? "FAF"
                                                    : fixTypeCode == "M" ? "MAP" : "";
                                list.Add(new ApproachFix
                                {
                                    Name       = rdr.IsDBNull(0) ? "" : rdr.GetString(0),
                                    FixType    = fixTypeLabel,
                                    Lat        = rdr.GetDouble(2),
                                    Lon        = rdr.GetDouble(3),
                                    AltitudeFt = rdr.IsDBNull(4) ? 0 : rdr.GetDouble(4),
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        // Returns both ends of every runway at the airport.
        // Uses UNION ALL so each end gets the correct threshold coordinates
        // (primary_end has its own lonx/laty, as does secondary_end).
        private static List<RunwayEndInfo> GetRunwayEnds(SQLiteConnection conn, long airportId)
        {
            const string sql = @"
                SELECT re.name, re.heading, re.laty, re.lonx, r.width, r.length
                FROM   runway r
                JOIN   runway_end re ON re.runway_end_id = r.primary_end_id
                WHERE  r.airport_id = @aid
                UNION ALL
                SELECT re.name, re.heading, re.laty, re.lonx, r.width, r.length
                FROM   runway r
                JOIN   runway_end re ON re.runway_end_id = r.secondary_end_id
                WHERE  r.airport_id = @aid";

            var list = new List<RunwayEndInfo>();
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@aid", airportId);
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(new RunwayEndInfo
                        {
                            Name     = rdr.GetString(0),
                            Heading  = rdr.GetDouble(1),
                            Lat      = rdr.GetDouble(2),
                            Lon      = rdr.GetDouble(3),
                            WidthFt  = rdr.GetDouble(4),
                            LengthFt = rdr.GetDouble(5)
                        });
                    }
                }
            }
            return list;
        }

        // Nearest named taxiway segment within TaxiwayRadiusM metres.
        // When heading is not NaN, segments misaligned by >50° receive a ×2.5 distance
        // penalty so that cross-taxiways at intersections are not spuriously preferred.
        private static string NearestTaxiway(
            SQLiteConnection conn, long airportId, double lat, double lon,
            double heading = double.NaN)
        {
            const string sql = @"
                SELECT name, start_lonx, start_laty, end_lonx, end_laty
                FROM   taxi_path
                WHERE  airport_id = @aid
                  AND  type = 'T'
                  AND  name IS NOT NULL AND name != ''";

            string bestName  = null;
            double bestScore = double.MaxValue;
            bool   useHdg    = !double.IsNaN(heading);

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@aid", airportId);
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        double sLat = rdr.GetDouble(2), sLon = rdr.GetDouble(1);
                        double eLat = rdr.GetDouble(4), eLon = rdr.GetDouble(3);

                        double d = DistToSegM(lat, lon, sLat, sLon, eLat, eLon);
                        if (d >= TaxiwayRadiusM) continue;

                        double score = d;
                        if (useHdg && d > 1.0)
                        {
                            // taxiways are bidirectional — use smallest of fwd/rev delta
                            double brg   = BearingDeg(sLat, sLon, eLat, eLon);
                            double delta = Math.Min(HeadingDelta(heading, brg),
                                                    HeadingDelta(heading, (brg + 180.0) % 360.0));
                            if (delta > 50.0) score *= 2.5;
                        }

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestName  = rdr.GetString(0);
                        }
                    }
                }
            }
            return bestName;
        }
    }
}
