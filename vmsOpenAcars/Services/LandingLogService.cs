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

                    // ── Meteo del aterrizaje ──────────────────────────────────────────
                    // La tabla `flights` **ya existe en la base de cada piloto**, así que las
                    // columnas nuevas hay que añadirlas sobre la marcha: `CREATE TABLE IF NOT
                    // EXISTS` no toca una tabla que ya está. Se hace con `PRAGMA table_info` y
                    // `ALTER TABLE ... ADD COLUMN` **solo si falta**, que es idempotente y no
                    // pierde datos (una columna añadida es NULL en las filas viejas).
                    //
                    // El METAR en crudo **no estrena columna**: `metar_raw` ya existe y desde este
                    // cambio guarda el del momento del **aterrizaje**, no el de la hora de filear.
                    // Duplicarlo en `landing_metar_raw` dejaría dos copias del mismo dato.
                    EnsureColumn(conn, "flights", "landing_metar_obs_utc",   "TEXT");
                    EnsureColumn(conn, "flights", "landing_wind_dir_deg",    "REAL");
                    EnsureColumn(conn, "flights", "landing_wind_speed_kt",   "REAL");
                    EnsureColumn(conn, "flights", "landing_wind_gust_kt",    "REAL");
                    EnsureColumn(conn, "flights", "landing_headwind_kt",     "REAL");
                    EnsureColumn(conn, "flights", "landing_crosswind_kt",    "REAL");
                    EnsureColumn(conn, "flights", "landing_runway_true_deg", "REAL");

                    // ── Longitud de la pista de la toma ───────────────────────────────
                    // La necesita el **closeup del aterrizaje** del perfil vertical
                    // (`UI/Forms/LandingAnalysisForm` + `Helpers/TouchdownCloseupGeometry`): sin ella
                    // el gráfico no puede dibujar la línea de pista, y **no se inventa un largo**.
                    // Va sin prefijo `landing_` a propósito: no es meteo del aterrizaje, es geometría
                    // de la pista, como `touchdown_dist_ft` y `centerline_dev_ft`, que tampoco lo
                    // llevan. Nullable: las filas viejas se quedan en NULL y el closeup degrada.
                    EnsureColumn(conn, "flights", "runway_length_ft", "REAL");
                }
            }
            catch { }
        }

        /// <summary>
        /// Añade la columna si falta. `PRAGMA table_info` es lo que dice la verdad del esquema real
        /// —no el `CREATE TABLE`, que puede ser viejo—, y el `ALTER TABLE` va **solo** cuando la
        /// columna no está: repetirlo daría «duplicate column name» y, con el `catch` de arriba, se
        /// llevaría por delante el resto de la migración en silencio.
        /// </summary>
        private static void EnsureColumn(SQLiteConnection conn, string table, string column, string type)
        {
            if (HasColumn(conn, table, column)) return;
            Exec(conn, $"ALTER TABLE {table} ADD COLUMN {column} {type}");
        }

        private static bool HasColumn(SQLiteConnection conn, string table, string column)
        {
            using (var cmd = conn.CreateCommand())
            {
                // El nombre de tabla no es un dato de entrada: son constantes de este fichero, así
                // que no hay inyección posible en el PRAGMA (que además no admite parámetros).
                cmd.CommandText = $"PRAGMA table_info({table})";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        // Columna 1 de table_info = nombre.
                        if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            return false;
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
                         score, metar_raw,
                         landing_metar_obs_utc, landing_wind_dir_deg, landing_wind_speed_kt,
                         landing_wind_gust_kt, landing_headwind_kt, landing_crosswind_kt,
                         landing_runway_true_deg, runway_length_ft)
                    VALUES
                        (@fn, @org, @dest, @rwy, @dt,
                         @rate, @gf, @dist, @cl,
                         @score, @metar,
                         @obs, @wdir, @wspd,
                         @wgst, @hw, @xw,
                         @rwytrue, @rlen)";

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
                // Nullable de verdad: sin dato va NULL (no 0, que sería un viento del norte).
                cmd.Parameters.AddWithValue("@obs",     (object)r.LandingMetarObsUtc?.ToString("o") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@wdir",    (object)r.LandingWindDirDeg     ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@wspd",    (object)r.LandingWindSpeedKt    ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@wgst",    (object)r.LandingWindGustKt     ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@hw",      (object)r.LandingHeadwindKt     ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@xw",      (object)r.LandingCrosswindKt    ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rwytrue", (object)r.LandingRunwayTrueDeg  ?? DBNull.Value);
                // Longitud de pista: 0 significa «no la sé» (así la publica `TouchdownState`), y eso
                // se guarda NULL, no 0 — un 0 sería una pista de longitud cero.
                double? runwayLength = r.RunwayLengthFt.HasValue && r.RunwayLengthFt.Value > 0.0
                    ? r.RunwayLengthFt : null;
                cmd.Parameters.AddWithValue("@rlen", (object)runwayLength ?? DBNull.Value);
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
                               score, metar_raw,
                               landing_metar_obs_utc, landing_wind_dir_deg, landing_wind_speed_kt,
                               landing_wind_gust_kt, landing_headwind_kt, landing_crosswind_kt,
                               landing_runway_true_deg, runway_length_ft
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
                                MetarRaw        = r.IsDBNull(11) ? "" : r.GetString(11),
                                // Las columnas nuevas van al final del SELECT para no mover los
                                // índices de las de siempre (una base vieja las tiene NULL).
                                LandingMetarObsUtc  = r.IsDBNull(12) ? (DateTime?)null : DateTime.Parse(r.GetString(12)),
                                LandingWindDirDeg   = r.IsDBNull(13) ? (double?)null   : r.GetDouble(13),
                                LandingWindSpeedKt  = r.IsDBNull(14) ? (double?)null   : r.GetDouble(14),
                                LandingWindGustKt   = r.IsDBNull(15) ? (double?)null   : r.GetDouble(15),
                                LandingHeadwindKt   = r.IsDBNull(16) ? (double?)null   : r.GetDouble(16),
                                LandingCrosswindKt  = r.IsDBNull(17) ? (double?)null   : r.GetDouble(17),
                                LandingRunwayTrueDeg= r.IsDBNull(18) ? (double?)null   : r.GetDouble(18),
                                RunwayLengthFt      = r.IsDBNull(19) ? (double?)null   : r.GetDouble(19)
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
