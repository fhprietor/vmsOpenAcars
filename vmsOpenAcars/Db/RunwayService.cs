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
        /// </summary>
        public RunwayTouchdownResult GetRunwayThreshold(string airport, double heading)
        {
            try
            {
                if (!IsAvailable) return null;
                using (var conn = OpenConn())
                {
                    long apId = GetAirportId(conn, airport);
                    if (apId < 0) return null;

                    RunwayEndInfo best = null;
                    double bestDelta = double.MaxValue;

                    foreach (var end in GetRunwayEnds(conn, apId))
                    {
                        double d = HeadingDelta(end.Heading, heading);
                        if (d < 45.0 && d < bestDelta) { bestDelta = d; best = end; }
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

        public string FindNearestTaxiway(string airport, double lat, double lon)
        {
            try
            {
                if (!IsAvailable) return null;
                using (var conn = OpenConn())
                {
                    long apId = GetAirportId(conn, airport);
                    return apId < 0 ? null : NearestTaxiway(conn, apId, lat, lon);
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

                    string current = NearestTaxiway(conn, apId, lat, lon);
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
        private static string NearestTaxiway(
            SQLiteConnection conn, long airportId, double lat, double lon)
        {
            const string sql = @"
                SELECT name, start_lonx, start_laty, end_lonx, end_laty
                FROM   taxi_path
                WHERE  airport_id = @aid
                  AND  type = 'T'
                  AND  name IS NOT NULL AND name != ''";

            string bestName = null;
            double bestDist = double.MaxValue;

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@aid", airportId);
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        double d = DistToSegM(
                            lat, lon,
                            rdr.GetDouble(2), rdr.GetDouble(1),
                            rdr.GetDouble(4), rdr.GetDouble(3));

                        if (d < TaxiwayRadiusM && d < bestDist)
                        {
                            bestDist = d;
                            bestName = rdr.GetString(0);
                        }
                    }
                }
            }
            return bestName;
        }
    }
}
