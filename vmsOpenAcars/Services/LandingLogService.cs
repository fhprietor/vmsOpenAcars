using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services.Interfaces;

namespace vmsOpenAcars.Services
{
    public class LandingLogService : ILandingLogService
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
