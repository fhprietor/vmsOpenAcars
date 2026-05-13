using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services
{
    public class LandingLogService
    {
        private readonly string _dbPath;

        public bool IsAvailable => !string.IsNullOrEmpty(_dbPath);

        public LandingLogService(string dbPath)
        {
            _dbPath = dbPath;
            if (IsAvailable) EnsureDatabase();
        }

        // ── Schema ────────────────────────────────────────────────────────────────

        private void EnsureDatabase()
        {
            try
            {
                string dir = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var conn = OpenConn())
                {
                    Exec(conn, @"
                        CREATE TABLE IF NOT EXISTS flights (
                            id                INTEGER PRIMARY KEY AUTOINCREMENT,
                            flight_number     TEXT,
                            origin            TEXT,
                            destination       TEXT,
                            runway_name       TEXT,
                            flight_date       TEXT,
                            landing_rate_fpm  INTEGER,
                            g_force           REAL,
                            touchdown_dist_ft REAL,
                            centerline_dev_ft REAL,
                            score             INTEGER,
                            metar_raw         TEXT
                        )");

                    Exec(conn, @"
                        CREATE TABLE IF NOT EXISTS approach_track (
                            id          INTEGER PRIMARY KEY AUTOINCREMENT,
                            flight_id   INTEGER NOT NULL,
                            seq_no      INTEGER NOT NULL,
                            lat         REAL,
                            lon         REAL,
                            alt_ft      REAL,
                            agl_ft      REAL,
                            ias_kt      REAL,
                            vs_fpm      REAL,
                            heading_deg REAL,
                            dist_nm     REAL,
                            lateral_ft  REAL
                        )");
                }
            }
            catch { }
        }

        // ── Write ─────────────────────────────────────────────────────────────────

        public int SaveFlight(FlightRecord record, IList<ApproachTrackPoint> track)
        {
            if (!IsAvailable) return -1;
            try
            {
                using (var conn = OpenConn())
                using (var tx = conn.BeginTransaction())
                {
                    int flightId = InsertFlight(conn, record);

                    if (track != null)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                                INSERT INTO approach_track
                                    (flight_id, seq_no, lat, lon, alt_ft, agl_ft, ias_kt,
                                     vs_fpm, heading_deg, dist_nm, lateral_ft)
                                VALUES
                                    (@fid, @seq, @lat, @lon, @alt, @agl, @ias,
                                     @vs, @hdg, @dist, @lat2)";

                            foreach (var pt in track)
                            {
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@fid",  flightId);
                                cmd.Parameters.AddWithValue("@seq",  pt.SeqNo);
                                cmd.Parameters.AddWithValue("@lat",  pt.Lat);
                                cmd.Parameters.AddWithValue("@lon",  pt.Lon);
                                cmd.Parameters.AddWithValue("@alt",  pt.AltFt);
                                cmd.Parameters.AddWithValue("@agl",  pt.AglFt);
                                cmd.Parameters.AddWithValue("@ias",  pt.IasKt);
                                cmd.Parameters.AddWithValue("@vs",   pt.VsFpm);
                                cmd.Parameters.AddWithValue("@hdg",  pt.HeadingDeg);
                                cmd.Parameters.AddWithValue("@dist", pt.DistNm);
                                cmd.Parameters.AddWithValue("@lat2", pt.LateralFt);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    tx.Commit();
                    return flightId;
                }
            }
            catch
            {
                return -1;
            }
        }

        private int InsertFlight(SQLiteConnection conn, FlightRecord r)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO flights
                        (flight_number, origin, destination, runway_name, flight_date,
                         landing_rate_fpm, g_force, touchdown_dist_ft, centerline_dev_ft,
                         score, metar_raw)
                    VALUES
                        (@fn, @org, @dest, @rwy, @dt,
                         @rate, @gf, @dist, @cl,
                         @score, @metar)";

                cmd.Parameters.AddWithValue("@fn",    r.FlightNumber ?? "");
                cmd.Parameters.AddWithValue("@org",   r.Origin ?? "");
                cmd.Parameters.AddWithValue("@dest",  r.Destination ?? "");
                cmd.Parameters.AddWithValue("@rwy",   r.RunwayName ?? "");
                cmd.Parameters.AddWithValue("@dt",    r.FlightDate.ToString("o"));
                cmd.Parameters.AddWithValue("@rate",  r.LandingRateFpm);
                cmd.Parameters.AddWithValue("@gf",    r.GForce);
                cmd.Parameters.AddWithValue("@dist",  r.TouchdownDistFt);
                cmd.Parameters.AddWithValue("@cl",    r.CenterlineDevFt);
                cmd.Parameters.AddWithValue("@score", r.Score);
                cmd.Parameters.AddWithValue("@metar", r.MetarRaw ?? "");
                cmd.ExecuteNonQuery();
            }
            using (var cmd2 = conn.CreateCommand())
            {
                cmd2.CommandText = "SELECT last_insert_rowid()";
                return Convert.ToInt32(cmd2.ExecuteScalar());
            }
        }

        // ── Read ──────────────────────────────────────────────────────────────────

        public List<FlightRecord> GetFlights()
        {
            var list = new List<FlightRecord>();
            if (!IsAvailable) return list;
            try
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id, flight_number, origin, destination, runway_name, flight_date,
                               landing_rate_fpm, g_force, touchdown_dist_ft, centerline_dev_ft,
                               score, metar_raw
                        FROM flights
                        ORDER BY flight_date DESC";

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new FlightRecord
                            {
                                Id              = r.GetInt32(0),
                                FlightNumber    = r.IsDBNull(1)  ? "" : r.GetString(1),
                                Origin          = r.IsDBNull(2)  ? "" : r.GetString(2),
                                Destination     = r.IsDBNull(3)  ? "" : r.GetString(3),
                                RunwayName      = r.IsDBNull(4)  ? "" : r.GetString(4),
                                FlightDate      = DateTime.Parse(r.GetString(5)),
                                LandingRateFpm  = r.GetInt32(6),
                                GForce          = r.GetDouble(7),
                                TouchdownDistFt = r.GetDouble(8),
                                CenterlineDevFt = r.GetDouble(9),
                                Score           = r.GetInt32(10),
                                MetarRaw        = r.IsDBNull(11) ? "" : r.GetString(11)
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public List<ApproachTrackPoint> GetTrackPoints(int flightId)
        {
            var list = new List<ApproachTrackPoint>();
            if (!IsAvailable) return list;
            try
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT flight_id, seq_no, lat, lon, alt_ft, agl_ft, ias_kt,
                               vs_fpm, heading_deg, dist_nm, lateral_ft
                        FROM approach_track
                        WHERE flight_id = @fid
                        ORDER BY seq_no";
                    cmd.Parameters.AddWithValue("@fid", flightId);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new ApproachTrackPoint
                            {
                                FlightId   = r.GetInt32(0),
                                SeqNo      = r.GetInt32(1),
                                Lat        = r.GetDouble(2),
                                Lon        = r.GetDouble(3),
                                AltFt      = r.GetDouble(4),
                                AglFt      = r.GetDouble(5),
                                IasKt      = r.GetDouble(6),
                                VsFpm      = r.GetDouble(7),
                                HeadingDeg = r.GetDouble(8),
                                DistNm     = r.GetDouble(9),
                                LateralFt  = r.GetDouble(10)
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public bool HasFlights()
        {
            if (!IsAvailable) return false;
            try
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM flights";
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
            catch { return false; }
        }

        public void DeleteFlight(int id)
        {
            if (!IsAvailable) return;
            using (var conn = OpenConn())
            using (var tx = conn.BeginTransaction())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction  = tx;
                    cmd.CommandText  = "DELETE FROM approach_track WHERE flight_id = @id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction  = tx;
                    cmd.CommandText  = "DELETE FROM flights WHERE id = @id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }

        // ── Mock data ─────────────────────────────────────────────────────────────

        public void SeedMockData()
        {
            if (!IsAvailable) return;

            // SKRG RWY 01 — José María Córdova (Rionegro, Colombia)
            const double ThLat    = 6.149458;
            const double ThLon    = -75.423049;
            const double ThHdg    = 359.66;
            const double AptElev  = 6955.0;  // ft MSL
            const int    Points   = 65;
            const double StepNm   = 0.07778; // 140 kt × 2 s
            const double StartNm  = 5.0;

            var flights = new[]
            {
                new { Fn="VHR055CH", Org="SKBO", Dest="SKRG", Rwy="01", Date="2026-04-10T14:30:00Z",
                      Rate=-152,  Gf=1.10, Dist= 580.0, Cl= 6.0, Score=97, Vref=138.0, VsBias=-10.0, LatBias= 2.0, GpaBias= 50.0,
                      Metar="SKRG 101430Z 01012KT 9999 FEW040 FEW100 21/12 Q1024" },
                new { Fn="VHR012CH", Org="SKCL", Dest="SKRG", Rwy="01", Date="2026-04-17T10:00:00Z",
                      Rate=-275,  Gf=1.32, Dist=1180.0, Cl=22.0, Score=85, Vref=142.0, VsBias=-40.0, LatBias=-8.0, GpaBias=-30.0,
                      Metar="SKRG 171000Z 36015G22KT 9999 FEW035 BKN080 20/13 Q1022" },
                new { Fn="VHR088CH", Org="SKBQ", Dest="SKRG", Rwy="01", Date="2026-04-22T17:15:00Z",
                      Rate=-418,  Gf=1.65, Dist=2150.0, Cl=38.0, Score=67, Vref=148.0, VsBias=-90.0, LatBias=15.0, GpaBias=-80.0,
                      Metar="SKRG 221715Z 35008KT 8000 -RA FEW025 BKN060 17/14 Q1020" },
                new { Fn="VHR031CH", Org="SKSP", Dest="SKRG", Rwy="01", Date="2026-04-28T09:00:00Z",
                      Rate=-183,  Gf=1.17, Dist= 870.0, Cl=11.0, Score=94, Vref=139.0, VsBias= -5.0, LatBias= 4.0, GpaBias= 30.0,
                      Metar="SKRG 280900Z 01010KT 9999 FEW045 FEW120 22/11 Q1025" },
                new { Fn="VHR067CH", Org="SKPE", Dest="SKRG", Rwy="01", Date="2026-05-01T11:45:00Z",
                      Rate=-321,  Gf=1.42, Dist=2780.0, Cl=48.0, Score=74, Vref=143.0, VsBias=-50.0, LatBias=20.0, GpaBias= 10.0,
                      Metar="SKRG 011145Z 01014KT 9999 FEW040 SCT100 23/12 Q1023" },
            };

            double cosLat = Math.Cos(ThLat * Math.PI / 180.0);
            double thrRad = ThHdg * Math.PI / 180.0;
            // Unit vector in runway direction
            double rwyN = Math.Cos(thrRad);
            double rwyE = Math.Sin(thrRad);
            // Per-step delta in runway direction
            double stepM   = StepNm * 1852.0;
            double dLatDeg = rwyN * stepM / 111320.0;
            double dLonDeg = rwyE * stepM / (111320.0 * cosLat);

            // Starting position: StartNm before threshold (opposite of runway direction)
            double startLat = ThLat - rwyN * StartNm * 1852.0 / 111320.0;
            double startLon = ThLon - rwyE * StartNm * 1852.0 / (111320.0 * cosLat);

            var rng = new Random(42);

            foreach (var f in flights)
            {
                var record = new FlightRecord
                {
                    FlightNumber    = f.Fn,
                    Origin          = f.Org,
                    Destination     = f.Dest,
                    RunwayName      = f.Rwy,
                    FlightDate      = DateTime.Parse(f.Date),
                    LandingRateFpm  = f.Rate,
                    GForce          = f.Gf,
                    TouchdownDistFt = f.Dist,
                    CenterlineDevFt = f.Cl,
                    Score           = f.Score,
                    MetarRaw        = f.Metar
                };

                var track = new List<ApproachTrackPoint>(Points);

                for (int i = 0; i < Points; i++)
                {
                    double distNm = StartNm - i * StepNm;
                    if (distNm < 0) distNm = 0;

                    // Glidepath: 3° ref + per-flight bias + small noise
                    double refAgl   = distNm * 319.0;
                    double noise    = (rng.NextDouble() - 0.5) * 20.0;
                    double agl      = Math.Max(0, refAgl + f.GpaBias + noise);
                    double alt      = AptElev + agl;

                    // IAS around Vref
                    double ias = f.Vref + (rng.NextDouble() - 0.5) * 6.0;

                    // VS at 3° / 140kt ≈ -700 fpm, with per-flight bias + noise
                    double vs = -700.0 + f.VsBias + (rng.NextDouble() - 0.5) * 60.0;

                    // Lateral deviation: linearly increasing offset + oscillation
                    double lateralProgress = (double)i / (Points - 1); // 0→1
                    double lateral = f.LatBias * lateralProgress
                                   + (rng.NextDouble() - 0.5) * 8.0;

                    // Position along approach path
                    double lat = startLat + i * dLatDeg;
                    double lon = startLon + i * dLonDeg;

                    // Apply lateral offset (perpendicular to runway)
                    double perpN = -rwyE;  // 90° left of runway
                    double perpE =  rwyN;
                    double latOffM = lateral * 0.3048;  // ft → m
                    lat += perpN * latOffM / 111320.0;
                    lon += perpE * latOffM / (111320.0 * cosLat);

                    track.Add(new ApproachTrackPoint
                    {
                        SeqNo      = i,
                        Lat        = lat,
                        Lon        = lon,
                        AltFt      = alt,
                        AglFt      = agl,
                        IasKt      = ias,
                        VsFpm      = vs,
                        HeadingDeg = ThHdg,
                        DistNm     = distNm,
                        LateralFt  = lateral
                    });
                }

                SaveFlight(record, track);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private SQLiteConnection OpenConn()
        {
            var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
            conn.Open();
            return conn;
        }

        private static void Exec(SQLiteConnection conn, string sql)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
    }
}
