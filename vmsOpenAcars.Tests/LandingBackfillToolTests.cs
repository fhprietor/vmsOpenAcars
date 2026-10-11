using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Herramienta manual, NO una prueba de la suite.** Convierte el análisis de un aterrizaje que
    /// ya está en el `landing_log.sqlite` local en el cuerpo del `POST /api/pireps/{id}/landing`, para
    /// rellenar en phpVMS un vuelo **anterior** a la ingesta (v0.9.37).
    ///
    /// **Por qué no manda nada por la red, y por qué existe en vez de un script:** el cuerpo lo arma
    /// `LandingUploadPayload`, que es **el mismo código que usa el cliente en vuelo** —un script en
    /// otro lenguaje sería una segunda definición de la regla de `null`/`0`, del `flaps_index` y del
    /// corte de potencia, y ya sabemos cómo acaban esas—. Lo que **no** hace es el POST: un test que
    /// escribe en producción se dispararía solo en cualquier corrida de la suite. Aquí se escribe un
    /// JSON por vuelo y el POST lo hace una persona, con el `pirep_id` ya comprobado contra la API.
    ///
    /// **Cómo se usa** (sin variables no hace nada y la suite sigue verde):
    /// <code>
    /// $env:VMSACARS_BACKFILL      = 'D:\ruta\de\salida'          # obligatorio: dónde dejar los JSON
    /// $env:VMSACARS_LANDING_DB    = 'E:\vmsOpenAcars\db\landing_log.sqlite'
    /// $env:VMSACARS_BACKFILL_IDS  = '43,44'                      # opcional: ids de `flights`
    /// $env:VMSACARS_BACKFILL_ALL  = '1'                          # opcional: todo el log, no solo el flare
    /// vstest.console.exe … /Tests:ExportaElCuerpoDelAnalisisDeUnVuelo
    /// </code>
    /// Sin `VMSACARS_BACKFILL_IDS` exporta **todos los vuelos que tengan traza de flare**, que es la
    /// definición útil: un vuelo sin traza no aporta nada que no esté ya en el PIREP. Con
    /// `VMSACARS_BACKFILL_ALL=1` entra **todo** el log, que es la serie histórica de solo métricas.
    ///
    /// **Lo que sale lleva `source_name` de relleno** (`vmsOpenACars/0.9.37-relleno`, ver
    /// `LandingUploadPayload.BackfillTag`): es la marca que pidió phpVMS para no confundir un
    /// aterrizaje volado con uno rellenado.
    /// </summary>
    [TestClass]
    public class LandingBackfillToolTests
    {
        [TestMethod]
        public void ExportaElCuerpoDelAnalisisDeUnVuelo()
        {
            string outDir = Environment.GetEnvironmentVariable("VMSACARS_BACKFILL");
            string dbPath = Environment.GetEnvironmentVariable("VMSACARS_LANDING_DB");

            if (string.IsNullOrWhiteSpace(outDir) || string.IsNullOrWhiteSpace(dbPath))
                Assert.Inconclusive("Herramienta manual: defina VMSACARS_BACKFILL y VMSACARS_LANDING_DB.");
            if (!File.Exists(dbPath))
                Assert.Inconclusive("No existe el log local: " + dbPath);

            var service = new LandingLogService(dbPath);
            Assert.IsTrue(service.IsAvailable, "el log local no quedó disponible");

            var wanted = ParseIds(Environment.GetEnvironmentVariable("VMSACARS_BACKFILL_IDS"));
            // Sin ids: por defecto solo los vuelos con traza de flare, que son los que aportan un
            // dato que phpVMS no tiene. Con `VMSACARS_BACKFILL_ALL=1` se exporta **todo** el log:
            // esa es la serie histórica de **solo métricas** (los vuelos anteriores a la traza), que
            // ellos pidieron el 11/10/2026 porque sus líneas base pasan de dos aterrizajes a decenas.
            bool all = Environment.GetEnvironmentVariable("VMSACARS_BACKFILL_ALL") == "1";
            Directory.CreateDirectory(outDir);

            int exported = 0;
            var summary = new StringBuilder();
            foreach (var flight in service.GetFlights().OrderBy(f => f.Id))
            {
                if (wanted.Count > 0 && !wanted.Contains(flight.Id)) continue;

                var trace = service.GetFlareTrack(flight.Id);
                bool hasTrace = trace != null && trace.Count > 0;
                if (wanted.Count == 0 && !all && !hasTrace) continue;

                string payload = LandingUploadPayload.ToJson(
                    LandingUploadPayload.Build(flight, trace, Core.Helpers.AppInfo.Version,
                                               isBackfill: true));

                string file = Path.Combine(outDir, $"vuelo-{flight.Id}.landing.json");
                File.WriteAllText(file, payload, new UTF8Encoding(false));

                summary.AppendLine(Summarize(flight, trace, file));
                exported++;
            }

            Console.WriteLine(summary.ToString());
            // Además del stdout —que `vstest` solo enseña en modo detallado—, el resumen se deja en
            // el directorio de salida: es lo que se lee para decidir si el cuerpo se manda.
            File.WriteAllText(Path.Combine(outDir, "resumen.txt"), summary.ToString(),
                              new UTF8Encoding(false));
            Assert.IsTrue(exported > 0, "no se exportó ningún vuelo (¿ids que no existen?)");
        }

        private static HashSet<int> ParseIds(string raw)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(raw)) return set;
            foreach (var part in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int id;
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                    set.Add(id);
            }
            return set;
        }

        /// <summary>
        /// Una línea por vuelo con lo que hay que mirar **antes** de mandarlo: el recuento de muestras,
        /// las métricas que phpVMS ya tiene en el PIREP (para que cuadren) y una comprobación que nació
        /// de un fallo real: **cuántas muestras en tierra llevan `dist_ft = 0`**, que es el síntoma del
        /// tramo de frenada colapsado que se arregló en la 0.9.37. En el vuelo 43 —anterior al fix— son
        /// 16, y eso viaja tal cual: es lo que se capturó, y no se inventa.
        /// </summary>
        private static string Summarize(FlightRecord f, IList<FlareTrackPoint> trace, string file)
        {
            int collapsed = trace?.Count(s => s.OnGround && s.DistFt == 0.0) ?? 0;
            var m = LandingUploadPayload.Build(f, trace, Core.Helpers.AppInfo.Version).metrics;
            return string.Format(CultureInfo.InvariantCulture,
                "vuelo {0}: {1} ({2}->{3} pista {4}) · {5} muestras · tasa {6} fpm · score {7} · " +
                "toque {8} ft · CL {9} ft · flaps {10} · muestras en tierra con dist=0: {11}\n   -> {12}",
                f.Id, f.FlightNumber, f.Origin, f.Destination, f.RunwayName,
                trace?.Count ?? 0, m.landing_rate_fpm, m.score,
                m.touchdown_dist_ft, m.centerline_dev_ft, m.flaps_label, collapsed, file);
        }
    }
}
