// Services/NavDataCache.cs
using System;
using System.Data.SQLite;
using System.IO;
using System.Reflection;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Caché SQLite persistente para datos estáticos de NavData (renovados con el AIRAC).
    /// El METAR y los anuncios de cabina NO se cachean aquí.
    /// El archivo NavData_cache.sqlite se crea junto al ejecutable.
    /// </summary>
    internal static class NavDataCache
    {
        private static string _dbPath;
        private static readonly object _lock = new object();
        private static string _currentAirac = "";

        // ── Ciclo de vida ─────────────────────────────────────────────────────────

        /// <summary>
        /// Crea las tablas si no existen, lee el AIRAC almacenado y, si ya venció,
        /// purga todas las entradas para forzar un re-fetch limpio.
        /// Llamar desde el constructor estático de NavDataClient.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                _dbPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "NavData_cache.sqlite");
                CreateSchema();
                _currentAirac = ReadMeta("airac_cycle") ?? "";

                // Si la validez del AIRAC almacenado ya venció, purgar todo
                string validUntil = ReadMeta("airac_valid_until") ?? "";
                if (!string.IsNullOrEmpty(validUntil)
                    && DateTime.TryParse(validUntil,
                           System.Globalization.CultureInfo.InvariantCulture,
                           System.Globalization.DateTimeStyles.None, out var exp)
                    && exp.Date < DateTime.UtcNow.Date)
                {
                    PurgeAll();
                    _currentAirac = "";
                }
            }
            catch { _dbPath = null; }
        }

        /// <summary>
        /// Actualiza el AIRAC en la caché. Si el ciclo cambió, elimina todas las entradas
        /// del ciclo anterior. También guarda la fecha de validez para auto-purga en
        /// el próximo arranque si el AIRAC vence mientras la app está cerrada.
        /// Llamar desde NavDataClient.TestApiAsync cuando se obtiene un nuevo ciclo.
        /// </summary>
        public static void SyncAirac(string airac, string validUntil = null)
        {
            if (string.IsNullOrEmpty(airac) || _dbPath == null) return;
            lock (_lock)
            {
                try
                {
                    bool changed = airac != _currentAirac;

                    if (changed)
                    {
                        using (var conn = Open())
                        using (var tx   = conn.BeginTransaction())
                        {
                            Run(conn, tx, "DELETE FROM airport_entries WHERE airac_cycle != @a", "@a", airac);
                            Run(conn, tx, "DELETE FROM navaid_entries  WHERE airac_cycle != @a", "@a", airac);
                            Run(conn, tx,
                                "INSERT OR REPLACE INTO meta (key,value) VALUES ('airac_cycle',@v)",
                                "@v", airac);
                            tx.Commit();
                        }
                        _currentAirac = airac;
                    }

                    if (!string.IsNullOrEmpty(validUntil))
                    {
                        using (var conn = Open())
                            Run(conn, null,
                                "INSERT OR REPLACE INTO meta (key,value) VALUES ('airac_valid_until',@v)",
                                "@v", validUntil);
                    }
                }
                catch { }
            }
        }

        // ── Datos de aeropuerto ───────────────────────────────────────────────────

        /// <summary>
        /// Recupera el JSON almacenado para (icao, dataType).
        /// dataType ∈ { "block", "sids", "stars", "ils", "waypoints" }.
        /// Devuelve null si no hay entrada.
        /// </summary>
        public static string TryGet(string dataType, string icao)
        {
            if (_dbPath == null) return null;
            lock (_lock)
            {
                try
                {
                    using (var conn = Open())
                    using (var cmd  = conn.CreateCommand())
                    {
                        cmd.CommandText =
                            "SELECT json_data FROM airport_entries WHERE icao=@i AND data_type=@t";
                        cmd.Parameters.AddWithValue("@i", icao.ToUpperInvariant());
                        cmd.Parameters.AddWithValue("@t", dataType);
                        return cmd.ExecuteScalar() as string;
                    }
                }
                catch { return null; }
            }
        }

        /// <summary>Guarda (o actualiza) el JSON serializado para (icao, dataType).</summary>
        public static void Store(string dataType, string icao, string json)
        {
            if (_dbPath == null || string.IsNullOrEmpty(json)) return;
            lock (_lock)
            {
                try
                {
                    using (var conn = Open())
                        Run(conn, null,
                            @"INSERT OR REPLACE INTO airport_entries
                              (icao,data_type,airac_cycle,json_data) VALUES (@i,@t,@a,@j)",
                            "@i", icao.ToUpperInvariant(),
                            "@t", dataType,
                            "@a", _currentAirac,
                            "@j", json);
                }
                catch { }
            }
        }

        // ── Navaids ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Recupera el JSON de un navaid. cacheKey = "vor:PIE", "ndb:FLL", etc.
        /// </summary>
        public static string TryGetNavaid(string cacheKey)
        {
            if (_dbPath == null) return null;
            lock (_lock)
            {
                try
                {
                    using (var conn = Open())
                    using (var cmd  = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT json_data FROM navaid_entries WHERE cache_key=@k";
                        cmd.Parameters.AddWithValue("@k", cacheKey);
                        return cmd.ExecuteScalar() as string;
                    }
                }
                catch { return null; }
            }
        }

        /// <summary>Guarda (o actualiza) un navaid.</summary>
        public static void StoreNavaid(string cacheKey, string json)
        {
            if (_dbPath == null || string.IsNullOrEmpty(json)) return;
            lock (_lock)
            {
                try
                {
                    using (var conn = Open())
                        Run(conn, null,
                            @"INSERT OR REPLACE INTO navaid_entries
                              (cache_key,airac_cycle,json_data) VALUES (@k,@a,@j)",
                            "@k", cacheKey,
                            "@a", _currentAirac,
                            "@j", json);
                }
                catch { }
            }
        }

        // ── Internos ──────────────────────────────────────────────────────────────

        private static void CreateSchema()
        {
            lock (_lock)
            {
                using (var conn = Open())
                {
                    var tables = new[]
                    {
                        "CREATE TABLE IF NOT EXISTS meta (" +
                            "key TEXT PRIMARY KEY, value TEXT NOT NULL)",

                        "CREATE TABLE IF NOT EXISTS airport_entries (" +
                            "icao TEXT NOT NULL, data_type TEXT NOT NULL, " +
                            "airac_cycle TEXT NOT NULL, json_data TEXT NOT NULL, " +
                            "PRIMARY KEY (icao, data_type))",

                        "CREATE TABLE IF NOT EXISTS navaid_entries (" +
                            "cache_key TEXT PRIMARY KEY, " +
                            "airac_cycle TEXT NOT NULL, json_data TEXT NOT NULL)",
                    };
                    foreach (var ddl in tables)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = ddl;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private static void PurgeAll()
        {
            lock (_lock)
            {
                try
                {
                    using (var conn = Open())
                    {
                        Run(conn, null, "DELETE FROM airport_entries");
                        Run(conn, null, "DELETE FROM navaid_entries");
                    }
                }
                catch { }
            }
        }

        private static string ReadMeta(string key)
        {
            lock (_lock)
            {
                try
                {
                    using (var conn = Open())
                    using (var cmd  = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT value FROM meta WHERE key=@k";
                        cmd.Parameters.AddWithValue("@k", key);
                        return cmd.ExecuteScalar() as string;
                    }
                }
                catch { return null; }
            }
        }

        private static void Run(SQLiteConnection conn, SQLiteTransaction tx,
            string sql, params object[] p)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Transaction = tx;
                for (int i = 0; i + 1 < p.Length; i += 2)
                    cmd.Parameters.AddWithValue((string)p[i], p[i + 1]);
                cmd.ExecuteNonQuery();
            }
        }

        private static SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(
                $"Data Source={_dbPath};Version=3;Pooling=True;");
            conn.Open();
            return conn;
        }
    }
}
