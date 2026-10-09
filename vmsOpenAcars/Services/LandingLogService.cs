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

                    // ── La traza FINA del flare ───────────────────────────────────────
                    // Tabla **nueva**, no una columna ni una reutilización de `approach_track`: esa
                    // es de 2 s (2,25 s medidos en la base local, ≈ 550 ft por muestra a 150 kt) y la
                    // usan los cuatro gráficos del análisis. El flare se muestrea a 10 Hz **solo** en
                    // los últimos ~1 500 ft, y guardarlo en la misma tabla obligaría a cambiar el
                    // muestreo de todo el descenso —o a mentir sobre la resolución— para afinar
                    // cinco segundos. Separadas, cada una dice la verdad de su ritmo.
                    //
                    // `flight_id` enlaza con `flights.id` y se borra con el vuelo (`DeleteFlight`),
                    // que es como lo hace `approach_track` (no hay `FOREIGN KEY ... CASCADE`: la
                    // tabla vieja tampoco la tiene y añadirla ahora obligaría a recrear `flights`).
                    //
                    // Todo nullable menos lo imprescindible: `dist_ft` es la X del gráfico y siempre
                    // existe (la calcula la telemetría), `on_ground` cierra la traza. Lo demás es
                    // «lo que el simulador publicó»: sin dato va NULL, **nunca 0**, porque un 0 en
                    // `radar_alt_ft` sería un avión a cero pies y en `pitch_deg` un avión nivelado.
                    Exec(conn, @"
                        CREATE TABLE IF NOT EXISTS flare_track (
                            id            INTEGER PRIMARY KEY AUTOINCREMENT,
                            flight_id     INTEGER NOT NULL,
                            seq_no        INTEGER NOT NULL,
                            ts_utc        TEXT,
                            dist_ft       REAL NOT NULL,
                            agl_ft        REAL,
                            radar_alt_ft  REAL,
                            ias_kt        REAL,
                            vs_fpm        REAL,
                            pitch_deg     REAL,
                            bank_deg      REAL,
                            gs_kt         REAL,
                            eng1_pct      REAL,
                            eng2_pct      REAL,
                            eng1_n2_pct   REAL,
                            eng2_n2_pct   REAL,
                            flaps_pct     REAL,
                            spoilers      INTEGER,
                            on_ground     INTEGER,
                            flaps_index   INTEGER
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

                    // ── ¿Se armó la captura del flare? ────────────────────────────────
                    // Va en `flights` y **no** se deduce de `flare_track`: existe justo para el caso
                    // contrario, el de la captura armada **sin** filas guardadas —que hasta v0.9.34
                    // era el caso de todos los vuelos, porque el reset del vuelo vaciaba el buffer
                    // antes de que se persistiera—. Sin este dato, la ventana del flare no puede
                    // distinguir «vuelo anterior a la traza» de «la captura se armó y se perdió», y
                    // contaba lo segundo como lo primero. `INTEGER` 0/1; NULL en las filas viejas =
                    // no armada.
                    EnsureColumn(conn, "flights", "flare_capture_armed", "INTEGER");

                    // ── La familia de la aeronave ─────────────────────────────────────
                    // **Sin esto las compuertas de flaps no se pueden etiquetar fuera del vuelo.**
                    // `flaps_pct` es un porcentaje del recorrido del mando y el mismo número significa
                    // cosas distintas en un Airbus que en un Boeing (`Helpers/FlapSetting`), así que
                    // al abrir un vuelo del historial hay que saber en qué avión se voló. El dato sale
                    // del **modelo ATC** del simulador (`FsuipcService.AircraftIcao`, offset `0x0618`),
                    // que es el que publica la familia (`B737`, `B777`, `A320`…).
                    //
                    // Nullable: los vuelos anteriores a la columna se quedan en NULL y el helper
                    // devuelve el porcentaje sin etiqueta, que es la respuesta honesta cuando no se
                    // sabe el avión. **No se rellena a posteriori con el tipo del OFP**: el plan y el
                    // avión que voló pueden no coincidir, y esa discrepancia es justo lo que valida
                    // `AircraftTypeMatch` al empezar el vuelo.
                    EnsureColumn(conn, "flights", "aircraft_icao", "TEXT");

                    // ── La identidad de la aeronave, entera ───────────────────────────
                    // `aircraft_icao` guarda solo el **modelo ATC** (la familia), que es lo que
                    // bastaba para etiquetar las compuertas de flaps. La cabecera del gráfico del
                    // flare necesita lo demás, y ese dato **no se puede reconstruir después**: el
                    // título (`0x3D00`) y el modelo (`0x0B26`) solo existen mientras el avión está
                    // cargado en el simulador, y al filear el vuelo se pierden. De ahí que se
                    // persistan, en vez de volver a leerlos del simulador al abrir el historial.
                    //
                    // Nullable y vacío = NULL, como `aircraft_icao`: los vuelos anteriores a la
                    // columna se quedan sin título y el bloque de datos simplemente enseña la
                    // familia, que es lo honesto.
                    EnsureColumn(conn, "flights", "aircraft_title", "TEXT");
                    EnsureColumn(conn, "flights", "aircraft_model", "TEXT");

                    // ── El peso en la toma (LTOW), en LIBRAS ──────────────────────────
                    // Es una **lectura** del peso bruto del simulador hecha en el contacto, no una
                    // cuenta con el ZFW del plan. NULL = no se pudo leer (sin simulador, o un addon
                    // que no publica sus estaciones de carga), y entonces el bloque **no pinta la
                    // línea**: un 0 ahí sería un avión sin peso.
                    EnsureColumn(conn, "flights", "landing_weight_lbs", "REAL");

                    // La traza del flare también se migra por si la tabla viene de una versión
                    // anterior con menos columnas: `CREATE TABLE IF NOT EXISTS` no toca una tabla que
                    // ya está, exactamente el mismo caso que el de `flights`.
                    foreach (var column in FlareColumns)
                        EnsureColumn(conn, "flare_track", column.Name, column.Type);
                }
            }
            catch { }
        }

        /// <summary>
        /// Las columnas de `flare_track`, en el orden en que las lee <see cref="GetFlareTrack"/>.
        /// Están en una lista y no repetidas a mano para que el `CREATE TABLE` y la migración
        /// defensiva no puedan desincronizarse: si mañana falta una en una base vieja, se añade sola.
        /// </summary>
        private static readonly (string Name, string Type)[] FlareColumns =
        {
            ("flight_id",    "INTEGER"),
            ("seq_no",       "INTEGER"),
            ("ts_utc",       "TEXT"),
            ("dist_ft",      "REAL"),
            ("agl_ft",       "REAL"),
            ("radar_alt_ft", "REAL"),
            ("ias_kt",       "REAL"),
            ("vs_fpm",       "REAL"),
            ("pitch_deg",    "REAL"),
            ("bank_deg",     "REAL"),
            ("gs_kt",        "REAL"),
            ("eng1_pct",     "REAL"),
            ("eng2_pct",     "REAL"),
            ("eng1_n2_pct",  "REAL"),
            ("eng2_n2_pct",  "REAL"),
            ("flaps_pct",    "REAL"),
            ("spoilers",     "INTEGER"),
            ("on_ground",    "INTEGER"),
            // El detente real (`0x0BFC`), que hasta ahora solo servía para el rótulo del panel de
            // vuelo. Va **aparte** de `flaps_pct` porque no son el mismo dato: el porcentaje es la
            // posición del mando y el detente es la compuerta que el avión tiene puesta. Es la
            // lectura que permite etiquetar la compuerta **sin** el «≈» de la traducción por bandas
            // (`Helpers/FlapSetting`), así que se guarda nullable: sin él se cae a las bandas, y un
            // 0 por defecto sería «flaps arriba», que es un dato, no un hueco.
            ("flaps_index",  "INTEGER"),
        };

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

        /// <summary>
        /// **La traza fina del flare**, atada a un vuelo ya guardado.
        ///
        /// Va en su propia transacción, después de <see cref="SaveFlight"/>: el `flight_id` solo se
        /// conoce tras el INSERT, y el flare es un extra —si falla, el vuelo y su traza de 2 s ya
        /// están en la base—. Devuelve cuántas muestras se persistieron, `0` si no se pudo.
        ///
        /// **Sin muestras no se escribe nada**: un vuelo sin captura de flare (los viejos, o uno en el
        /// que la geometría del umbral nunca se resolvió) se queda sin filas, y el gráfico lo dice.
        /// No se rellena con `approach_track`.
        /// </summary>
        public int SaveFlareTrack(int flightId, IList<FlareTrackPoint> samples)
        {
            if (!IsAvailable || flightId <= 0 || samples == null || samples.Count == 0) return 0;
            try
            {
                using (var conn = OpenConn())
                using (var tx = conn.BeginTransaction())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO flare_track
                            (flight_id, seq_no, ts_utc, dist_ft, agl_ft, radar_alt_ft, ias_kt,
                             vs_fpm, pitch_deg, bank_deg, gs_kt, eng1_pct, eng2_pct,
                             eng1_n2_pct, eng2_n2_pct, flaps_pct, spoilers, on_ground, flaps_index)
                        VALUES
                            (@fid, @seq, @ts, @dist, @agl, @ra, @ias,
                             @vs, @pitch, @bank, @gs, @eng1n1, @eng2n1,
                             @eng1n2, @eng2n2, @flaps, @spoilers, @ground, @flapsIndex)";

                    int written = 0;
                    foreach (var s in samples)
                    {
                        if (s == null) continue;
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@fid",  flightId);
                        cmd.Parameters.AddWithValue("@seq",  s.SeqNo);
                        cmd.Parameters.AddWithValue("@ts",   (object)s.TimestampUtc.ToString("o") ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@dist", s.DistFt);
                        // Nullable de verdad: lo que el simulador no publicó va NULL, no 0.
                        cmd.Parameters.AddWithValue("@agl",      Param(s.AglFt));
                        cmd.Parameters.AddWithValue("@ra",       Param(s.RadarAltFt));
                        cmd.Parameters.AddWithValue("@ias",      Param(s.IasKt));
                        cmd.Parameters.AddWithValue("@vs",       Param(s.VsFpm));
                        cmd.Parameters.AddWithValue("@pitch",    Param(s.PitchDeg));
                        cmd.Parameters.AddWithValue("@bank",     Param(s.BankDeg));
                        cmd.Parameters.AddWithValue("@gs",       Param(s.GsKt));
                        // Los N1 llegan ya a NULL cuando el offset viene a 0: un 0 ahí es «no lo
                        // publica» o «motor parado», y no hay manera de distinguirlos.
                        cmd.Parameters.AddWithValue("@eng1n1",   Param(s.Eng1Pct));
                        cmd.Parameters.AddWithValue("@eng2n1",   Param(s.Eng2Pct));
                        // El N2 va por el mismo camino y por el mismo motivo: `0x2220`/`0x2420` se
                        // quedan a cero en los addons que no los escriben (documentado en el propio
                        // `FsuipcService`), así que un 0 es «no lo sé» y se guarda NULL.
                        cmd.Parameters.AddWithValue("@eng1n2",   Param(s.Eng1N2Pct));
                        cmd.Parameters.AddWithValue("@eng2n2",   Param(s.Eng2N2Pct));
                        cmd.Parameters.AddWithValue("@flaps",    Param(s.FlapsPct));
                        cmd.Parameters.AddWithValue("@spoilers", s.SpoilersDeployed.HasValue
                                                                    ? (object)(s.SpoilersDeployed.Value ? 1 : 0)
                                                                    : DBNull.Value);
                        cmd.Parameters.AddWithValue("@ground", s.OnGround ? 1 : 0);
                        // El detente real: nullable de verdad. Sin dato va NULL, nunca 0, porque un 0
                        // es «flaps arriba» y afirmarlo cuando el avión no publica el notch inventaría
                        // una compuerta (ver `FlapSetting.DetentOrNull`).
                        cmd.Parameters.AddWithValue("@flapsIndex", s.FlapsIndex.HasValue
                                                                       ? (object)s.FlapsIndex.Value
                                                                       : DBNull.Value);
                        cmd.ExecuteNonQuery();
                        written++;
                    }

                    tx.Commit();
                    return written;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>`DBNull` para un nullable sin valor; el valor tal cual cuando lo hay.</summary>
        private static object Param(double? value) => (object)value ?? DBNull.Value;

        private int InsertFlight(SQLiteConnection conn, FlightRecord r)        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO flights
                        (flight_number, origin, destination, runway_name, flight_date,
                         landing_rate_fpm, g_force, touchdown_dist_ft, centerline_dev_ft,
                         score, metar_raw,
                         landing_metar_obs_utc, landing_wind_dir_deg, landing_wind_speed_kt,
                         landing_wind_gust_kt, landing_headwind_kt, landing_crosswind_kt,
                         landing_runway_true_deg, runway_length_ft,
                         flare_capture_armed, aircraft_icao,
                         aircraft_title, aircraft_model, landing_weight_lbs)
                    VALUES
                        (@fn, @org, @dest, @rwy, @dt,
                         @rate, @gf, @dist, @cl,
                         @score, @metar,
                         @obs, @wdir, @wspd,
                         @wgst, @hw, @xw,
                         @rwytrue, @rlen, @flareArmed, @acft,
                         @acftTitle, @acftModel, @lw)";

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
                // La captura del flare, 0/1. **No** es «hay filas en `flare_track`»: ver
                // `FlightRecord.FlareCaptureArmed`.
                cmd.Parameters.AddWithValue("@flareArmed", r.FlareCaptureArmed ? 1 : 0);
                // La familia del avión que voló, para poder etiquetar los flaps al releer el vuelo.
                // Vacío es «no la sé» y va NULL, igual que el resto de datos que pueden faltar.
                cmd.Parameters.AddWithValue("@acft",
                    string.IsNullOrWhiteSpace(r.AircraftIcao) ? (object)DBNull.Value : r.AircraftIcao.Trim());
                // El título y el modelo, con el mismo criterio: vacío es «no lo sé» y va NULL.
                cmd.Parameters.AddWithValue("@acftTitle",
                    string.IsNullOrWhiteSpace(r.AircraftTitle) ? (object)DBNull.Value : r.AircraftTitle.Trim());
                cmd.Parameters.AddWithValue("@acftModel",
                    string.IsNullOrWhiteSpace(r.AircraftModel) ? (object)DBNull.Value : r.AircraftModel.Trim());
                // El LTOW, en libras. Sin lectura va NULL: el bloque de datos omite la línea en vez
                // de enseñar un cero, que sería un avión sin peso.
                cmd.Parameters.AddWithValue("@lw", (object)r.LandingWeightLbs ?? DBNull.Value);
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
                               landing_runway_true_deg, runway_length_ft,
                               flare_capture_armed, aircraft_icao,
                               aircraft_title, aircraft_model, landing_weight_lbs
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
                                RunwayLengthFt      = r.IsDBNull(19) ? (double?)null   : r.GetDouble(19),
                                // NULL en una fila anterior a la columna = «no se sabe», y se trata
                                // como no armada: en la duda no se acusa a la captura de un fallo.
                                FlareCaptureArmed   = !r.IsDBNull(20) && r.GetInt32(20) != 0,
                                // NULL = vuelo anterior a la columna, o el simulador no publicaba el
                                // modelo ATC: el helper de flaps devuelve el porcentaje sin etiqueta.
                                AircraftIcao        = r.IsDBNull(21) ? null : r.GetString(21),
                                // La identidad completa y el peso de la toma. NULL en las filas
                                // anteriores a las columnas: el bloque del gráfico enseña lo que hay.
                                AircraftTitle       = r.IsDBNull(22) ? null : r.GetString(22),
                                AircraftModel       = r.IsDBNull(23) ? null : r.GetString(23),
                                LandingWeightLbs    = r.IsDBNull(24) ? (double?)null : r.GetDouble(24)
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

        /// <summary>
        /// La traza fina del flare de un vuelo, ordenada por `seq_no`. **Lista vacía es la respuesta
        /// correcta para un vuelo viejo** (sin `flare_track`): el gráfico no se rellena con
        /// `approach_track`, porque eso sería presentar un muestreo de 2 s como uno de 0,1 s.
        /// </summary>
        public List<FlareTrackPoint> GetFlareTrack(int flightId)
        {
            var list = new List<FlareTrackPoint>();
            if (!IsAvailable) return list;
            try
            {
                using (var conn = OpenConn())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT flight_id, seq_no, ts_utc, dist_ft, agl_ft, radar_alt_ft, ias_kt,
                               vs_fpm, pitch_deg, bank_deg, gs_kt, eng1_pct, eng2_pct,
                               eng1_n2_pct, eng2_n2_pct, flaps_pct, spoilers, on_ground, flaps_index
                        FROM flare_track
                        WHERE flight_id = @fid
                        ORDER BY seq_no";
                    cmd.Parameters.AddWithValue("@fid", flightId);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new FlareTrackPoint
                            {
                                FlightId     = r.GetInt32(0),
                                SeqNo        = r.GetInt32(1),
                                TimestampUtc = r.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(r.GetString(2)),
                                DistFt       = r.GetDouble(3),
                                AglFt        = Nullable(r, 4),
                                RadarAltFt   = Nullable(r, 5),
                                IasKt        = Nullable(r, 6),
                                VsFpm        = Nullable(r, 7),
                                PitchDeg     = Nullable(r, 8),
                                BankDeg      = Nullable(r, 9),
                                GsKt         = Nullable(r, 10),
                                Eng1Pct      = Nullable(r, 11),
                                Eng2Pct      = Nullable(r, 12),
                                Eng1N2Pct    = Nullable(r, 13),
                                Eng2N2Pct    = Nullable(r, 14),
                                FlapsPct     = Nullable(r, 15),
                                SpoilersDeployed = r.IsDBNull(16) ? (bool?)null : r.GetInt32(16) != 0,
                                OnGround     = !r.IsDBNull(17) && r.GetInt32(17) != 0,
                                // NULL = vuelo anterior a la columna, o el avión no publica `0x0BFC`:
                                // el helper de flaps cae entonces a la traducción por bandas.
                                FlapsIndex   = r.IsDBNull(18) ? (int?)null : r.GetInt32(18),
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        private static double? Nullable(SQLiteDataReader r, int index) =>
            r.IsDBNull(index) ? (double?)null : r.GetDouble(index);

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
                // Las dos trazas caen con el vuelo: si `approach_track` se limpia y `flare_track` no,
                // quedarían filas huérfanas apuntando a un `flight_id` que ya no existe.
                foreach (string table in new[] { "approach_track", "flare_track" })
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction  = tx;
                        cmd.CommandText  = $"DELETE FROM {table} WHERE flight_id = @id";
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
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
