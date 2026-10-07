using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La migración de `landing_log.sqlite`** (`LandingLogService.EnsureDatabase`).
    ///
    /// La tabla `flights` **ya existe en la base de cada piloto**, así que las columnas de la meteo
    /// del aterrizaje hay que añadirlas sobre la marcha: `CREATE TABLE IF NOT EXISTS` no toca una
    /// tabla que ya está. Aquí se reproduce el caso real —una base con el esquema **viejo** y filas
    /// dentro— y se comprueba que al abrirla:
    ///
    /// 1. las columnas nuevas aparecen (`PRAGMA table_info`),
    /// 2. **no se pierde nada**: la fila vieja sigue ahí y las columnas nuevas salen NULL, no 0,
    /// 3. el viaje de ida y vuelta de un aterrizaje con meteo conserva los valores,
    /// 4. abrirla dos veces no rompe nada (`ALTER TABLE` repetido daría «duplicate column name» y el
    ///    `catch` de la migración se llevaría por delante el resto **en silencio**).
    /// </summary>
    [TestClass]
    public class LandingLogServiceTests
    {
        private string _dbPath;

        [TestInitialize]
        public void CreateOldSchemaDatabase()
        {
            _dbPath = Path.Combine(Path.GetTempPath(),
                                   "vmsopenacars_migration_" + Guid.NewGuid().ToString("N") + ".sqlite");

            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // El esquema EXACTO de antes de este cambio: sin ninguna columna `landing_*`.
                    cmd.CommandText = @"
                        CREATE TABLE flights (
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
                        )";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        INSERT INTO flights
                            (flight_number, origin, destination, runway_name, flight_date,
                             landing_rate_fpm, g_force, touchdown_dist_ft, centerline_dev_ft,
                             score, metar_raw)
                        VALUES
                            ('8525', 'SKBQ', 'SKBO', '14R', '2026-09-24T02:14:31.7567190Z',
                             -180, 1.28, 1200.0, 15.0, 88, '')";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        [TestCleanup]
        public void DeleteDatabase()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }

        [TestMethod]
        public void UnaBaseVieja_GanaLasColumnasNuevasSinPerderFilas()
        {
            var svc = new LandingLogService(_dbPath);

            foreach (string column in NewColumns)
                Assert.IsTrue(HasColumn(column), $"falta la columna {column} tras la migración");

            var flights = svc.GetFlights();
            Assert.AreEqual(1, flights.Count, "la fila que ya estaba no puede desaparecer");

            var old = flights[0];
            Assert.AreEqual("8525", old.FlightNumber);
            Assert.AreEqual(88,     old.Score);
            // Columnas nuevas en una fila vieja: NULL, y por eso nullable. Un 0 sería «viento del
            // norte» / «cruzada nula», que es un dato, no un hueco.
            Assert.IsNull(old.LandingMetarObsUtc);
            Assert.IsNull(old.LandingWindDirDeg);
            Assert.IsNull(old.LandingWindSpeedKt);
            Assert.IsNull(old.LandingWindGustKt);
            Assert.IsNull(old.LandingHeadwindKt);
            Assert.IsNull(old.LandingCrosswindKt);
            Assert.IsNull(old.LandingRunwayTrueDeg);
            Assert.IsNull(old.RunwayLengthFt,
                "una fila vieja no tiene longitud de pista: el closeup tendrá que arreglárselas sin ella");
            Assert.IsFalse(old.WindAtLanding.Available);
        }

        [TestMethod]
        public void LaMeteaDelAterrizaje_ViajaDeIdaYVuelta()
        {
            var svc = new LandingLogService(_dbPath);

            var record = new FlightRecord
            {
                FlightNumber = "9247",
                Origin = "SKRG",
                Destination = "SKBG",
                RunwayName = "17",
                FlightDate = new DateTime(2026, 9, 25, 1, 46, 6, DateTimeKind.Utc),
                LandingRateFpm = -152,
                GForce = 1.18,
                MetarRaw = "METAR SKBG 050000Z 32003KT 280V010 9999 SCT020 23/20 Q1015",
                LandingMetarObsUtc = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc),
                LandingWindDirDeg = 320,
                LandingWindSpeedKt = 3,
                LandingWindGustKt = null,
                LandingHeadwindKt = -2.83,
                LandingCrosswindKt = 1.00,
                LandingRunwayTrueDeg = 159.53,
                // La pista de la toma, la que usará el closeup del perfil vertical.
                RunwayLengthFt = 12467.0,
            };

            int id = svc.SaveFlight(record, null);
            Assert.IsTrue(id > 0, "el INSERT con las columnas nuevas tiene que funcionar");

            var saved = svc.GetFlights().Find(f => f.Id == id);
            Assert.IsNotNull(saved);
            Assert.AreEqual(record.MetarRaw, saved.MetarRaw);
            // La columna se guarda en ISO con `Z` y `DateTime.Parse` la devuelve en hora local (la
            // misma convención que `flight_date`, que el LOGBOOK pinta con `ToLocalTime()`): se
            // compara el instante, no los ticks.
            Assert.AreEqual(record.LandingMetarObsUtc.Value,
                            saved.LandingMetarObsUtc.Value.ToUniversalTime());
            Assert.AreEqual(320,   saved.LandingWindDirDeg.Value,    1e-9);
            Assert.AreEqual(3,     saved.LandingWindSpeedKt.Value,   1e-9);
            Assert.IsNull(saved.LandingWindGustKt, "sin racha se guarda NULL, no 0");
            Assert.AreEqual(-2.83, saved.LandingHeadwindKt.Value,    1e-9);
            Assert.AreEqual(1.00,  saved.LandingCrosswindKt.Value,   1e-9);
            Assert.AreEqual(159.53, saved.LandingRunwayTrueDeg.Value, 1e-9);
            Assert.AreEqual(12467.0, saved.RunwayLengthFt.Value,      1e-9);

            // Y las componentes leídas se reconstruyen para poder pintarlas con el mismo helper.
            var wind = saved.WindAtLanding;
            Assert.IsTrue(wind.Available);
            Assert.AreEqual(-2.83, wind.HeadwindKt, 1e-9);
            Assert.AreEqual(1.00,  wind.CrosswindKt, 1e-9);
        }

        [TestMethod]
        public void AbrirLaBaseDosVeces_NoRompeLaMigracion()
        {
            // El `ALTER TABLE` solo se ejecuta si `PRAGMA table_info` dice que falta: repetirlo daría
            // «duplicate column name» y, con el catch del EnsureDatabase, se llevaría por delante las
            // columnas que quedaran por añadir sin decir nada.
            new LandingLogService(_dbPath);
            var second = new LandingLogService(_dbPath);

            foreach (string column in NewColumns)
                Assert.IsTrue(HasColumn(column), $"falta la columna {column} tras la segunda apertura");

            Assert.IsFalse(second.HasFlights() == false && second.GetFlights().Count != 1);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static readonly string[] NewColumns =
        {
            "landing_metar_obs_utc",
            "landing_wind_dir_deg",
            "landing_wind_speed_kt",
            "landing_wind_gust_kt",
            "landing_headwind_kt",
            "landing_crosswind_kt",
            "landing_runway_true_deg",
            // La que necesita el closeup del perfil vertical (`TouchdownCloseupGeometry`): sin ella
            // no se dibuja la pista, y **no se inventa un largo**.
            "runway_length_ft",
        };

        private bool HasColumn(string column)
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(flights)";
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                                return true;
                }
            }
            return false;
        }
    }
}
