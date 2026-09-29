using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **¿Cuál de las dos políticas propone lo que el piloto rodó de verdad?** Las mediciones anteriores
    /// comparaban el grafo consigo mismo (cobertura y longitud de sus dos variantes), que no dice si la
    /// ruta sugerida **se parece** a la que se rueda. Aquí se mide contra la **traza real**: para cada
    /// vuelo del corpus se toma la secuencia de calles por las que pasó el avión en el rodaje de salida
    /// (650 muestras de 36 vuelos, cada muestra resuelta a la calle más cercana de la red publicada) y se
    /// compara con la secuencia que propone el grafo, con la **subsecuencia común más larga**:
    ///
    ///   `recall`    = LCS(sugerida, rodada) / calles rodadas   → qué parte de lo que rodó te lo dijo
    ///   `precision` = LCS(sugerida, rodada) / calles sugeridas → qué parte de lo que te dijo sirvió
    ///
    /// Es la medida que un piloto juzgaría: no «¿es más corta?» sino «¿es la que hice?».
    /// </summary>
    [TestClass]
    public class TaxiRouteQualityTests
    {
        private sealed class Flight
        {
            public string Pirep, Ident, Airport;
            public double IniLat, IniLon, ObjLat, ObjLon;
            public List<string> Flown = new List<string>();
        }

        [TestMethod]
        public void TheSuggestedRouteIsScoredAgainstTheFlownTrace()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");

            // ── Redes por aeropuerto ──────────────────────────────────────────────
            var networks = new Dictionary<string, List<TaxiGraph.Segment>>();
            foreach (string raw in File.ReadAllLines(Path.Combine(dir, "taxi-networks-2026-09-29.csv")))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] f = line.Split(',');
                if (f.Length < 9) continue;

                long v, nodeA = 0, nodeB = 0;
                bool hasA = long.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                if (hasA) nodeA = v;
                bool hasB = long.TryParse(f[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                if (hasB) nodeB = v;
                double conf = 1.0;
                if (f.Length > 10 && !string.IsNullOrWhiteSpace(f[10]))
                    double.TryParse(f[10], NumberStyles.Float, CultureInfo.InvariantCulture, out conf);

                if (!networks.ContainsKey(f[0])) networks[f[0]] = new List<TaxiGraph.Segment>();
                networks[f[0]].Add(new TaxiGraph.Segment
                {
                    Name = f[2],
                    Lat1 = Num(f[3]),
                    Lon1 = Num(f[4]),
                    Lat2 = Num(f[5]),
                    Lon2 = Num(f[6]),
                    NodeA = hasA ? (long?)nodeA : null,
                    NodeB = hasB ? (long?)nodeB : null,
                    Confidence = conf,
                });
            }
            Assert.IsTrue(networks.Count >= 13, $"redes: {networks.Count}");

            // ── Corpus: origen y destino de cada vuelo ────────────────────────────
            var flights = new Dictionary<string, Flight>();
            string corpus = Path.Combine(dir, "taxi-corpus-2026-09-29.csv");
            string[] head = File.ReadAllLines(corpus).First(l => !l.StartsWith("#"))
                                .Split(',').Select(x => x.Trim().Trim('"')).ToArray();
            int iPirep = Array.IndexOf(head, "pirep"), iIdent = Array.IndexOf(head, "ident");
            int iDpto = Array.IndexOf(head, "dpto");
            int iIniLat = Array.IndexOf(head, "ini_lat"), iIniLon = Array.IndexOf(head, "ini_lon");
            int iObjLat = Array.IndexOf(head, "obj_lat"), iObjLon = Array.IndexOf(head, "obj_lon");

            foreach (string raw in File.ReadAllLines(corpus))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] f = line.Split(',');
                for (int i = 0; i < f.Length; i++) f[i] = f[i].Trim().Trim('"');
                if (f.Length <= iObjLon) continue;

                // La cabecera del corpus NO empieza por `#` (es `piloto,pirep,…`), así que hay que
                // descartarla aquí: un `double.Parse("ini_lat")` reventaba la medición entera. Y de paso
                // se degrada en vez de reventar: una fila ilegible se salta, no se lleva por delante el
                // resto (misma regla que los filtros de plausibilidad del producto).
                double iniLat, iniLon, objLat, objLon;
                if (!double.TryParse(f[iIniLat].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out iniLat)
                 || !double.TryParse(f[iIniLon].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out iniLon)
                 || !double.TryParse(f[iObjLat].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out objLat)
                 || !double.TryParse(f[iObjLon].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out objLon))
                    continue;

                flights[f[iPirep]] = new Flight
                {
                    Pirep = f[iPirep], Ident = f[iIdent], Airport = f[iDpto],
                    IniLat = iniLat,
                    IniLon = iniLon,
                    ObjLat = objLat,
                    ObjLon = objLon,
                };
            }

            // ── Traza real: la secuencia de calles que se rodó ────────────────────
            int samples = 0;
            foreach (string raw in File.ReadAllLines(Path.Combine(dir, "taxi-traces-2026-09-29.csv")))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] f = line.Split(',');
                if (f.Length < 5) continue;
                Flight fl;
                if (!flights.TryGetValue(f[0], out fl)) continue;
                string street = f[4].Trim();
                if (street.Length == 0) continue;
                // Se colapsan las repeticiones consecutivas: la traza muestrea la misma calle muchas veces.
                if (fl.Flown.Count == 0 ||
                    !string.Equals(fl.Flown[fl.Flown.Count - 1], street, StringComparison.OrdinalIgnoreCase))
                    fl.Flown.Add(street);
                samples++;
            }
            Assert.IsTrue(samples > 600, $"muestras de traza: {samples}");

            // ── La comparación ───────────────────────────────────────────────────
            var lines = new List<string>
            {
                "vuelo      apto   prox:recall  prec   ids:recall  prec   gana   ruta propuesta (prox) / (ids)"
            };
            double srProx = 0, spProx = 0, srIds = 0, spIds = 0;
            int measured = 0, proxWins = 0, idsWins = 0, ties = 0, noneProx = 0, noneIds = 0;

            foreach (var fl in flights.Values.OrderBy(x => x.Ident))
            {
                List<TaxiGraph.Segment> segs;
                if (!networks.TryGetValue(fl.Airport, out segs) || fl.Flown.Count < 2) continue;

                var prox = TaxiGraph.Suggest(segs, fl.IniLat, fl.IniLon, fl.ObjLat, fl.ObjLon);
                var ids  = TaxiGraph.Suggest(segs, fl.IniLat, fl.IniLon, fl.ObjLat, fl.ObjLon,
                                             useNodeIds: true);
                if (!prox.Found) noneProx++;
                if (!ids.Found)  noneIds++;

                var p = prox.Text.Split(' ').Where(s => s.Length > 0).ToList();
                var q = ids.Text.Split(' ').Where(s => s.Length > 0).ToList();

                double recP = p.Count > 0 ? (double)Lcs(p, fl.Flown) / fl.Flown.Count : 0.0;
                double preP = p.Count > 0 ? (double)Lcs(p, fl.Flown) / p.Count           : 0.0;
                double recI = q.Count > 0 ? (double)Lcs(q, fl.Flown) / fl.Flown.Count : 0.0;
                double preI = q.Count > 0 ? (double)Lcs(q, fl.Flown) / q.Count           : 0.0;

                srProx += recP; spProx += preP; srIds += recI; spIds += preI;
                measured++;
                if (Math.Abs(recP - recI) < 0.001) ties++;
                else if (recP > recI) proxWins++;
                else idsWins++;

                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0,-9} {1,-6} {2,7:F2} {3,6:F2}  {4,8:F2} {5,6:F2}   {6,-5}  {7} / {8}",
                    fl.Ident, fl.Airport, recP, preP, recI, preI,
                    Math.Abs(recP - recI) < 0.001 ? "igual" : (recP > recI ? "prox" : "ids"),
                    prox.Text, ids.Text));
            }

            lines.Add("");
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "medibles ................................. {0}", measured));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "recall medio  proximidad / ids ........... {0:F3} / {1:F3}", srProx / measured, srIds / measured));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "precision media proximidad / ids ......... {0:F3} / {1:F3}", spProx / measured, spIds / measured));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "gana por recall  proximidad / ids / igual  {0} / {1} / {2}", proxWins, idsWins, ties));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "sin ruta       proximidad / ids .......... {0} / {1}", noneProx, noneIds));

            string dump = Path.Combine(Path.GetTempPath(), "taxi_route_quality.txt");
            File.WriteAllLines(dump, lines);

            // Se fija a propósito: la primera corrida imprime la tabla para leerla.
            // **Cifras medidas el 29/09/2026** contra las 650 muestras de traza real de 36 vuelos: con
            // ellas el grafo propone la mitad de lo que el piloto rodó (recall ~0,49 con las dos
            // políticas: empate) y la identidad por `node_id` **propone menos ruido** (precisión 0,615
            // contra 0,560). En 24 vuelos empatan; decidir el interruptor NO es cosa de este test.
            Assert.AreEqual(33, measured, "vuelos medibles contra la traza real");
            Assert.AreEqual(0.494, Math.Round(srProx / measured, 3), 0.002, "recall de la fusión por proximidad");
            Assert.AreEqual(0.488, Math.Round(srIds  / measured, 3), 0.002, "recall de la identidad por node_id");
            Assert.AreEqual(0.560, Math.Round(spProx / measured, 3), 0.002, "precisión de la fusión por proximidad");
            Assert.AreEqual(0.615, Math.Round(spIds  / measured, 3), 0.002, "precisión de la identidad por node_id");
            Assert.AreEqual(4, proxWins, "vuelos donde gana la proximidad por recall");
            Assert.AreEqual(5, idsWins,  "vuelos donde gana el node_id por recall");
            Assert.AreEqual(24, ties,    "vuelos donde empatan");
            Assert.AreEqual(5, noneProx, "vuelos sin ruta con proximidad");
            Assert.AreEqual(5, noneIds,  "vuelos sin ruta con node_id");
        }

        /// <summary>Subsecuencia común más larga: qué parte de la ruta se hizo en el mismo orden.</summary>
        /// <summary>
        /// Número de un fixture: los ficheros los escribió PowerShell con la cultura local, así que
        /// los decimales pueden venir con **coma** (`47,7`). Se aceptan las dos y se parsea invariante:
        /// un `double.Parse` estricto tiraba `FormatException` y se llevaba por delante la medición.
        /// </summary>
        private static double Num(string s)
            => double.Parse((s ?? "").Trim().Trim('\"').Replace(',', '.'), NumberStyles.Float,
                            CultureInfo.InvariantCulture);
        private static int Lcs(List<string> a, List<string> b)
        {
            var prev = new int[b.Count + 1];
            var cur  = new int[b.Count + 1];
            for (int i = 1; i <= a.Count; i++)
            {
                for (int j = 1; j <= b.Count; j++)
                {
                    cur[j] = string.Equals(a[i - 1], b[j - 1], StringComparison.OrdinalIgnoreCase)
                        ? prev[j - 1] + 1
                        : Math.Max(prev[j], cur[j - 1]);
                }
                var t = prev; prev = cur; cur = t;
                Array.Clear(cur, 0, cur.Length);
            }
            return prev[b.Count];
        }
    }
}
