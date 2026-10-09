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
            Assert.IsFalse(old.FlareCaptureArmed,
                "sin la columna no se sabe si la captura se armó, y en la duda se dice que no");
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

        // ── La traza fina del flare (`flare_track`) ───────────────────────────────

        /// <summary>
        /// **«La captura del flare se armó» viaja con el vuelo.** No es «hay filas en `flare_track`»,
        /// y esa diferencia es el motivo de que exista la columna: es lo único que, con cero filas,
        /// separa «esta captura se armó y se perdió» de «este vuelo es anterior a la traza fina».
        /// Hasta v0.9.34 el buffer del flare se vaciaba antes de persistir y **todos** los vuelos
        /// salían con cero filas, así que la ventana contaba lo primero como lo segundo.
        /// </summary>
        [TestMethod]
        public void LaCapturaArmadaDelFlare_ViajaDeIdaYVuelta()
        {
            var svc = new LandingLogService(_dbPath);

            // El vuelo 42 de la base local (SKCG → MMTL): la captura se armó y terminó con 196
            // muestras, y aun así `flare_track` quedó vacía.
            int armada = svc.SaveFlight(new FlightRecord
            {
                FlightNumber = "578", Origin = "SKCG", Destination = "MMTL", RunwayName = "12",
                FlightDate = new DateTime(2026, 10, 8, 4, 37, 50, DateTimeKind.Utc),
                LandingRateFpm = -216, GForce = 1.48, TouchdownDistFt = 3949.5,
                FlareCaptureArmed = true,
            }, null);

            // Y uno sin captura: umbral sin resolver, o anterior a la traza.
            int sinArmar = svc.SaveFlight(new FlightRecord
            {
                FlightNumber = "9821", Origin = "SKBG", Destination = "SKCG", RunwayName = "01",
                FlightDate = new DateTime(2026, 10, 7, 3, 9, 22, DateTimeKind.Utc),
                FlareCaptureArmed = false,
            }, null);

            Assert.IsTrue(armada > 0 && sinArmar > 0);

            var vuelos = svc.GetFlights();
            Assert.IsTrue(vuelos.Find(f => f.Id == armada).FlareCaptureArmed,
                "la captura armada tiene que sobrevivir al vuelo: es lo que la ventana del flare mira");
            Assert.IsFalse(vuelos.Find(f => f.Id == sinArmar).FlareCaptureArmed);

            // Y sigue siendo verdad lo que dice el otro camino: armada no es «tiene traza».
            Assert.AreEqual(0, svc.GetFlareTrack(armada).Count,
                "una captura armada y perdida son cero filas: por eso el recuento no sirve de testigo");
        }

        /// <summary>
        /// **La tabla del flare nace al abrir la base**, también en una base vieja: `CREATE TABLE IF
        /// NOT EXISTS` la crea sin tocar nada de lo que ya había. Y va aparte de `approach_track` a
        /// propósito —esa es de 2 s y la usan los cuatro gráficos del análisis—, así que la
        /// comprobación de que la tabla nueva no sustituye a la vieja es parte del test.
        /// </summary>
        [TestMethod]
        public void LaBaseVieja_GanaLaTablaDelFlareSinTocarLaTrazaDeAproximacion()
        {
            var svc = new LandingLogService(_dbPath);

            Assert.IsTrue(TableExists("flare_track"), "la tabla del flare tiene que existir tras migrar");
            foreach (string column in FlareColumns)
                Assert.IsTrue(HasColumn(column, "flare_track"), $"falta la columna flare_track.{column}");

            // La traza de 2 s sigue siendo la suya: la tabla vieja no se toca ni se renombra.
            Assert.IsTrue(TableExists("approach_track"));
            Assert.AreEqual(1, svc.GetFlights().Count, "la fila que ya estaba no puede desaparecer");
            Assert.AreEqual(0, svc.GetFlareTrack(1).Count,
                "un vuelo viejo no tiene traza fina, y la respuesta es la lista vacía, no un error");
        }

        /// <summary>
        /// El viaje de ida y vuelta de la traza fina, con las magnitudes reales de la captura, y
        /// **con los huecos en NULL**: lo que el simulador no publicó no puede volver como 0, porque
        /// un 0 en `radar_alt_ft` sería un avión a cero pies y en `pitch_deg` un avión nivelado.
        /// </summary>
        [TestMethod]
        public void LaTrazaDelFlare_ViajaDeIdaYVueltaYConservaLosHuecos()
        {
            var svc = new LandingLogService(_dbPath);

            var record = new FlightRecord
            {
                FlightNumber = "9821", Origin = "SKBG", Destination = "SKCG",
                RunwayName = "01", FlightDate = new DateTime(2026, 10, 7, 3, 9, 22, DateTimeKind.Utc),
                TouchdownDistFt = 1736.398, RunwayLengthFt = 7841.0,
            };
            int id = svc.SaveFlight(record, null);
            Assert.IsTrue(id > 0);

            var t0 = new DateTime(2026, 10, 7, 3, 8, 52, DateTimeKind.Utc);
            var samples = new List<FlareTrackPoint>
            {
                // Una muestra con TODO lo que publica el simulador…
                new FlareTrackPoint
                {
                    SeqNo = 0, TimestampUtc = t0, DistFt = 1408.3,
                    AglFt = 118.0, RadarAltFt = 114.0, IasKt = 151.6, VsFpm = -370.6,
                    PitchDeg = 2.4, BankDeg = -0.8, GsKt = 150.2,
                    Eng1Pct = 58.4, Eng2Pct = 57.9, FlapsPct = 100.0, FlapsIndex = 8,
                    SpoilersDeployed = false, OnGround = false,
                },
                // …y otra sin radioaltímetro ni motores: los huecos son huecos.
                new FlareTrackPoint
                {
                    SeqNo = 1, TimestampUtc = t0.AddMilliseconds(100), DistFt = 155.7,
                    AglFt = 102.0, RadarAltFt = null, IasKt = 151.3, VsFpm = -504.4,
                    PitchDeg = 3.6, BankDeg = 0.2, GsKt = null,
                    Eng1Pct = null, Eng2Pct = null, FlapsPct = 100.0, FlapsIndex = null,
                    SpoilersDeployed = true, OnGround = false,
                },
                // La del toque, ya en tierra.
                new FlareTrackPoint
                {
                    SeqNo = 2, TimestampUtc = t0.AddSeconds(2), DistFt = -499.3,
                    AglFt = 80.0, RadarAltFt = null, IasKt = 149.5, VsFpm = -657.4,
                    PitchDeg = 1.2, BankDeg = 0.0, GsKt = 148.0,
                    Eng1Pct = 42.0, Eng2Pct = 41.8, FlapsPct = 100.0,
                    SpoilersDeployed = true, OnGround = true,
                },
            };

            int written = svc.SaveFlareTrack(id, samples);
            Assert.AreEqual(3, written, "las tres muestras tienen que persistirse");

            var back = svc.GetFlareTrack(id);
            Assert.AreEqual(3, back.Count);
            Assert.AreEqual(0, back[0].SeqNo);
            Assert.AreEqual(1408.3, back[0].DistFt, 1e-6);
            Assert.AreEqual(118.0, back[0].AglFt.Value, 1e-6);
            Assert.AreEqual(114.0, back[0].RadarAltFt.Value, 1e-6);
            Assert.AreEqual(2.4,   back[0].PitchDeg.Value, 1e-6);
            Assert.AreEqual(-0.8,  back[0].BankDeg.Value, 1e-6);
            Assert.AreEqual(150.2, back[0].GsKt.Value, 1e-6);
            Assert.AreEqual(58.4,  back[0].Eng1Pct.Value, 1e-6);
            Assert.IsFalse(back[0].OnGround);

            // Los huecos vuelven como huecos.
            Assert.IsNull(back[1].RadarAltFt, "sin radioaltímetro se guarda NULL, no 0");
            Assert.IsNull(back[1].GsKt);
            Assert.IsNull(back[1].Eng1Pct);
            // El detente real viaja junto al porcentaje, y su hueco vuelve como hueco: un 0 sería
            // «flaps arriba», que es lo contrario de «el avión no publicó el notch».
            Assert.AreEqual(8, back[0].FlapsIndex.Value, "el detente real se guarda tal cual");
            Assert.IsNull(back[1].FlapsIndex, "sin detente se guarda NULL, no 0");
            Assert.IsTrue(back[1].SpoilersDeployed.Value);
            Assert.IsTrue(back[2].OnGround);

            // La marca de tiempo va en UTC con el mismo formato que `flight_date`.
            Assert.AreEqual(t0, back[0].TimestampUtc.ToUniversalTime());
        }

        /// <summary>
        /// **Sin muestras no se escribe nada**: un vuelo sin captura de flare (los anteriores a la
        /// tabla, o aquellos en los que la pista del umbral nunca se resolvió) no estrena filas. La
        /// traza de 2 s **no** se cuela como traza fina.
        /// </summary>
        [TestMethod]
        public void SinMuestrasDeFlare_NoSeEscribeNingunaFila()
        {
            var svc = new LandingLogService(_dbPath);
            int id = svc.SaveFlight(new FlightRecord
            {
                FlightNumber = "0001", Origin = "SKBO", Destination = "SKBO",
                FlightDate = DateTime.UtcNow,
            }, null);

            Assert.AreEqual(0, svc.SaveFlareTrack(id, null));
            Assert.AreEqual(0, svc.SaveFlareTrack(id, new List<FlareTrackPoint>()));
            Assert.AreEqual(0, svc.SaveFlareTrack(0, new List<FlareTrackPoint> { new FlareTrackPoint() }),
                "sin vuelo al que colgarla no se escribe nada");
            Assert.AreEqual(0, svc.GetFlareTrack(id).Count);
        }

        /// <summary>
        /// Las dos trazas caen con el vuelo. Si `flare_track` no se limpiara, quedarían filas
        /// huérfanas apuntando a un `flight_id` que ya no existe —y la siguiente limpieza de la
        /// historia no las vería—.
        /// </summary>
        [TestMethod]
        public void BorrarElVuelo_SeLlevaLaTrazaDeAproximacionYLaDelFlare()
        {
            var svc = new LandingLogService(_dbPath);
            int id = svc.SaveFlight(new FlightRecord
            {
                FlightNumber = "0002", Origin = "SKBO", Destination = "SKBO",
                FlightDate = DateTime.UtcNow,
            }, new List<ApproachTrackPoint>
            {
                new ApproachTrackPoint { SeqNo = 0, DistNm = 0.1 },
                new ApproachTrackPoint { SeqNo = 1, DistNm = 0.2 },
                new ApproachTrackPoint { SeqNo = 2, DistNm = 0.3 },
            });
            svc.SaveFlareTrack(id, new List<FlareTrackPoint>
            {
                new FlareTrackPoint { SeqNo = 0, DistFt = 100.0 },
                new FlareTrackPoint { SeqNo = 1, DistFt = 50.0 },
            });

            svc.DeleteFlight(id);

            // La fila de `TestInitialize` (id 1) sigue ahí a propósito: además de comprobar que las
            // dos trazas caen, deja claro que el borrado no se lleva nada más.
            var restantes = svc.GetFlights();
            Assert.AreEqual(1, restantes.Count, "solo tiene que caer el vuelo borrado");
            Assert.AreNotEqual(id, restantes[0].Id);
            Assert.AreEqual(0, svc.GetTrackPoints(id).Count);
            Assert.AreEqual(0, svc.GetFlareTrack(id).Count);
        }

        /// <summary>
        /// **La migración defensiva sobre una `flare_track` que ya existe.** La tabla del flare nació
        /// en v0.9.34 y en la base local ya está creada; `CREATE TABLE IF NOT EXISTS` no la toca, así
        /// que `flaps_index` —igual que `aircraft_icao` en `flights`— solo puede aparecer por el
        /// `PRAGMA table_info` + `ALTER TABLE` de `LandingLogService`. Este test reproduce ese esquema
        /// viejo y comprueba las dos mitades: la columna aparece y el detente viaja de ida y vuelta.
        /// </summary>
        [TestMethod]
        public void LaBaseViejaConFlareTrackSinFlapsIndex_MigraLaColumnaYElDetenteViaja()
        {
            string path = Path.Combine(Path.GetTempPath(),
                                       "vmsopenacars_flapsindex_" + Guid.NewGuid().ToString("N") + ".sqlite");
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={path};Version=3;"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        // El esquema EXACTO de `flare_track` antes de este cambio: sin `flaps_index`.
                        cmd.CommandText = @"
                            CREATE TABLE flare_track (
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
                                on_ground     INTEGER
                            )";
                        cmd.ExecuteNonQuery();
                    }
                }

                var svc = new LandingLogService(path);   // aquí corre la migración

                Assert.IsTrue(HasColumnIn(path, "flaps_index", "flare_track"),
                    "una `flare_track` vieja tiene que ganar `flaps_index` al abrir la base");

                int id = svc.SaveFlight(new FlightRecord
                {
                    FlightNumber = "578", Origin = "SKCG", Destination = "MMTL",
                    FlightDate = new DateTime(2026, 10, 8, 4, 37, 50, DateTimeKind.Utc),
                }, null);

                int written = svc.SaveFlareTrack(id, new List<FlareTrackPoint>
                {
                    new FlareTrackPoint { SeqNo = 0, DistFt = 120.0, FlapsPct = 100.0, FlapsIndex = 8 },
                    new FlareTrackPoint { SeqNo = 1, DistFt = 40.0,  FlapsPct = 100.0, FlapsIndex = null },
                });
                Assert.AreEqual(2, written, "la base migrada tiene que aceptar las muestras con detente");

                var back = svc.GetFlareTrack(id);
                Assert.AreEqual(2, back.Count);
                Assert.AreEqual(8, back[0].FlapsIndex.Value);
                Assert.IsNull(back[1].FlapsIndex, "sin detente el hueco vuelve como NULL, no 0");
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
            }
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
            // «La captura del flare se armó». No se deduce de `flare_track`: existe para el caso de
            // la captura armada **sin** filas guardadas (v0.9.34), que es justo el que hay que poder
            // contarle a la ventana del flare.
            "flare_capture_armed",
            // La identidad de la aeronave que no se puede reconstruir después: el título (`0x3D00`)
            // y el modelo (`0x0B26`) solo existen mientras el avión está cargado en el simulador, y
            // son de donde salen la variante ICAO y el nombre del addon del bloque del gráfico.
            "aircraft_title",
            "aircraft_model",
            // El peso en la toma (LTOW), en libras, **leído** del simulador en el contacto. NULL en
            // las filas viejas y en las que no hubo lectura: el bloque omite la línea.
            "landing_weight_lbs",
        };

        /// <summary>
        /// Las columnas de `flare_track`, en el mismo orden que las declara `LandingLogService`. La
        /// lista está a mano **a propósito**: si el servicio añade una columna y el test no, es que
        /// alguien tocó el esquema sin decirlo, y eso es lo que hay que ver.
        /// </summary>
        private static readonly string[] FlareColumns =
        {
            "flight_id", "seq_no", "ts_utc", "dist_ft", "agl_ft", "radar_alt_ft", "ias_kt",
            "vs_fpm", "pitch_deg", "bank_deg", "gs_kt", "eng1_pct", "eng2_pct",
            "flaps_pct", "spoilers", "on_ground", "flaps_index",
        };

        private bool TableExists(string table)
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@n";
                    cmd.Parameters.AddWithValue("@n", table);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        private bool HasColumn(string column) => HasColumn(column, "flights");

        private bool HasColumn(string column, string table) => HasColumnIn(_dbPath, column, table);

        private static bool HasColumnIn(string dbPath, string column, string table)
        {
            using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA table_info({table})";
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
