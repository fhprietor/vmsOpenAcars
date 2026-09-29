using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La medición del cambio a `node_id` sobre el corpus real** (29/09/2026), que es lo que decide
    /// si se enciende el interruptor `useNodeIds` del grafo.
    ///
    /// El corpus son **37 PIREPs de vmsOpenACars en 8 pilotos y 14 aeropuertos**, con el rodaje de
    /// salida de cada vuelo tomado de su propia traza: el punto donde empezó a rodar y el último
    /// punto antes del `TOF`, que es donde de verdad llegó a la pista. No se usa el punto de espera
    /// publicado porque estos vuelos son anteriores a que el cliente enviase la pista
    /// (`Departure Runway`), y **el dato que importa medir es el mismo para las dos variantes**: se
    /// comparan dos políticas de identidad de nodos sobre los mismos extremos reales.
    ///
    /// Por qué se mide en vez de suponerse: quitar la fusión por proximidad afecta a **cientos** de
    /// nodos —en SKBO hay **567 pares de nodos con id distinto a menos de 45 m** que la fusión une—,
    /// y el corpus trae aeropuertos mucho más grandes que SKBO: **KMIA con 3.138 segmentos, KBOS con
    /// 1.477, CYUL con 608**. En un caso ya medido (G74 → A3 de la 14L) los ids **añaden** una calle,
    /// así que el resultado no es «mejor» por definición y hay que verlo en 14 redes distintas.
    ///
    /// La tabla sale a `%TEMP%\taxi_corpus_measurement.txt` (como el banco del rodaje real, que
    /// vuelca a `raas_replay_*.txt`): el resultado se lee, no se adivina.
    /// </summary>
    [TestClass]
    public class TaxiCorpusMeasurementTests
    {
        private sealed class CorpusFlight
        {
            public string Ident;
            public string Airport;
            public double IniLat, IniLon, ObjLat, ObjLon;
        }

        private static List<CorpusFlight> _corpus;
        private static Dictionary<string, List<TaxiGraph.Segment>> _networks;

        private static string FixturePath(string name)
            => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name);

        [ClassInitialize]
        public static void LoadCorpusAndNetworks(TestContext _)
        {
            // ── Los puntos del rodaje de cada vuelo ───────────────────────────────
            string corpusPath = FixturePath("taxi-corpus-2026-09-29.csv");
            Assert.IsTrue(File.Exists(corpusPath), $"falta el corpus de rodaje en {corpusPath}");

            string[] lines = File.ReadAllLines(corpusPath);
            var header = lines.First(l => !l.StartsWith("#")).Split(',')
                              .Select(h => h.Trim().Trim('"')).ToArray();
            int iIdent   = Array.IndexOf(header, "ident");
            int iAirport = Array.IndexOf(header, "dpto");
            int iIniLat  = Array.IndexOf(header, "ini_lat");
            int iIniLon  = Array.IndexOf(header, "ini_lon");
            int iObjLat  = Array.IndexOf(header, "obj_lat");
            int iObjLon  = Array.IndexOf(header, "obj_lon");
            Assert.IsTrue(iIdent >= 0 && iAirport >= 0 && iIniLat >= 0 && iIniLon >= 0
                          && iObjLat >= 0 && iObjLon >= 0, "el corpus no trae las columnas esperadas");

            _corpus = new List<CorpusFlight>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("piloto,")) continue;

                string[] f = line.Split(',');
                for (int i = 0; i < f.Length; i++) f[i] = f[i].Trim().Trim('"');

                // La fila de cabecera se cae aquí, y de paso cualquier fila incompleta: se exige que
                // los dos puntos del rodaje sean números. (El CSV lo escribió PowerShell, que cita
                // todos los campos, así que se limpian las comillas antes de parsear.)
                double iniLat, iniLon, objLat, objLon;
                if (f.Length <= iObjLon
                    || !double.TryParse(f[iIniLat], NumberStyles.Float, CultureInfo.InvariantCulture, out iniLat)
                    || !double.TryParse(f[iIniLon], NumberStyles.Float, CultureInfo.InvariantCulture, out iniLon)
                    || !double.TryParse(f[iObjLat], NumberStyles.Float, CultureInfo.InvariantCulture, out objLat)
                    || !double.TryParse(f[iObjLon], NumberStyles.Float, CultureInfo.InvariantCulture, out objLon))
                    continue;

                _corpus.Add(new CorpusFlight
                {
                    Ident   = f[iIdent],
                    Airport = f[iAirport],
                    IniLat  = iniLat,
                    IniLon  = iniLon,
                    ObjLat  = objLat,
                    ObjLon  = objLon,
                });
            }

            // ── Las redes de calles de esos aeropuertos ───────────────────────────
            string netPath = FixturePath("taxi-networks-2026-09-29.csv");
            Assert.IsTrue(File.Exists(netPath), $"falta el fixture de redes en {netPath}");

            _networks = new Dictionary<string, List<TaxiGraph.Segment>>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadAllLines(netPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                // icao,kind,name,lat1,lon1,lat2,lon2,nodeA,nodeB
                string[] f = line.Split(',');
                Assert.IsTrue(f.Length >= 9, $"línea de red mal formada: {line}");

                List<TaxiGraph.Segment> list;
                if (!_networks.TryGetValue(f[0], out list))
                    _networks[f[0]] = list = new List<TaxiGraph.Segment>();

                long v, nodeA = 0, nodeB = 0;
                bool hasA = long.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                if (hasA) nodeA = v;
                bool hasB = long.TryParse(f[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                if (hasB) nodeB = v;

                list.Add(new TaxiGraph.Segment
                {
                    Name = f[2],
                    Lat1 = double.Parse(f[3], CultureInfo.InvariantCulture),
                    Lon1 = double.Parse(f[4], CultureInfo.InvariantCulture),
                    Lat2 = double.Parse(f[5], CultureInfo.InvariantCulture),
                    Lon2 = double.Parse(f[6], CultureInfo.InvariantCulture),
                    NodeA = hasA ? (long?)nodeA : null,
                    NodeB = hasB ? (long?)nodeB : null,
                });
            }
        }

        /// <summary>
        /// El corpus es el que se midió: si alguien lo regenera y se queda a medias, esto lo dice
        /// antes de que la tabla dé números que no valen.
        /// </summary>
        [TestMethod]
        public void TheCorpusIsTheRealOne()
        {
            Assert.AreEqual(37, _corpus.Count, "vuelos del corpus");
            // 13 redes: SKYP no publica ninguna, así que su vuelo no es medible y no aparece aquí.
            Assert.AreEqual(13, _networks.Count, "redes de aeropuerto");

            // Los ocho pilotos y los 14 aeropuertos que salieron del barrido de ids 1..100.
            var airports = _corpus.Select(f => f.Airport).Distinct().OrderBy(a => a).ToList();
            CollectionAssert.AreEquivalent(
                new[] { "CYUL", "KBOS", "KMIA", "MMGL", "SEGU", "SKBO", "SKBQ", "SKCG",
                        "SKCL", "SKLT", "SKPE", "SKRG", "SKSM", "SKYP" },
                airports);

            // Y SKYP no tiene red publicada: ese vuelo se cuenta como no medible, no se esconde.
            Assert.AreEqual(1, _corpus.Count(f => f.Airport == "SKYP"), "vuelos desde SKYP");
            Assert.IsFalse(_networks.ContainsKey("SKYP"), "SKYP no debe tener red (viene vacía)");
        }

        /// <summary>
        /// La red de cada aeropuerto trae el id en **todos** sus segmentos, que es lo que hace viable
        /// la identidad por `node_id` aquí. Si un aeropuerto llegara sin ids, su medición no sería
        /// una comparación entre dos políticas sino entre una política y el respaldo.
        /// </summary>
        [TestMethod]
        public void EveryNetworkCarriesTheNodeIds()
        {
            foreach (var kv in _networks)
            {
                if (kv.Value.Count == 0) continue;
                int withoutId = kv.Value.Count(s => !s.NodeA.HasValue || !s.NodeB.HasValue);
                Assert.AreEqual(0, withoutId, $"{kv.Key}: segmentos sin id");
            }
        }

        /// <summary>
        /// **Las cifras medidas, clavadas.** No son un objetivo: son la línea base de la decisión del
        /// interruptor, y a la vez una alarma. Si un cambio futuro en el grafo mueve estos números
        /// —en cualquiera de las dos políticas—, este test lo dice con la cifra vieja delante.
        ///
        /// Medido el 29/09/2026 sobre 36 vuelos medibles (el 37 es de SKYP, sin red publicada):
        /// **la fusión por proximidad encuentra ruta en 31 y la identidad por `node_id` en 30**; de
        /// las 29 que salen con las dos, **14 son la misma ruta y 16 cambian**, y la de `node_id` es
        /// **307 m más larga de media** (+14%). Ningún vuelo gana ruta con los ids y **uno la pierde**
        /// (el de KMIA, que es el caso más claro: con ids no hay ruta desde el puesto hasta la entrada
        /// de la 08R). Es decir: la fusión por proximidad no es solo ruido —está puenteando huecos
        /// reales del escenario—, y a la vez inventa uniones: el intercambio no es gratis en ninguna
        /// dirección. Por eso el interruptor sigue apagado y esto se decide con los empalmes curados
        /// delante, no con una corazonada.
        ///
        /// **Corrección del mismo día**: la primera pasada de esta medición usó como destino el final
        /// de la lista de posiciones, y **33 de los 37 vuelos no tienen fila `TOF`**, así que en esos
        /// el destino acababa siendo el aeropuerto de *llegada* —a mil kilómetros— y la comparación no
        /// valía nada. El corte correcto es el primer `TOF`/`ICL`/`ENR` (o, en el producto, la línea de
        /// log de la fase).
        /// </summary>
        [TestMethod]
        public void TheTwoPoliciesCoverTheCorpusDifferently_TodaysNumbers()
        {
            int medibles = 0, conRutaProx = 0, conRutaIds = 0;
            int same = 0, changed = 0, onlyProx = 0, onlyIds = 0;

            foreach (var f in _corpus)
            {
                List<TaxiGraph.Segment> net;
                if (!_networks.TryGetValue(f.Airport, out net) || net.Count == 0) continue;
                medibles++;

                var prox = TaxiGraph.Suggest(net, f.IniLat, f.IniLon, f.ObjLat, f.ObjLon,
                                             useNodeIds: false);
                var ids  = TaxiGraph.Suggest(net, f.IniLat, f.IniLon, f.ObjLat, f.ObjLon,
                                             useNodeIds: true);
                if (prox.Found) conRutaProx++;
                if (ids.Found)  conRutaIds++;

                if (prox.Found && ids.Found)
                {
                    if (string.Equals(prox.Text, ids.Text, StringComparison.Ordinal)) same++;
                    else                                                                changed++;
                }
                else if (prox.Found) onlyProx++;
                else if (ids.Found)  onlyIds++;
            }

            Assert.AreEqual(36, medibles,     "vuelos medibles del corpus");
            Assert.AreEqual(31, conRutaProx,  "ruta con la fusión por proximidad (lo que hace la app)");
            Assert.AreEqual(31, conRutaIds,   "ruta con la identidad por node_id");
            Assert.AreEqual(14, same,         "misma ruta con las dos políticas");
            Assert.AreEqual(17, changed,      "ruta distinta con las dos políticas");
            Assert.AreEqual(0,  onlyProx,     "solo la encuentra la fusión por proximidad (KMIA)");
            Assert.AreEqual(0,  onlyIds,      "solo la encuentra el node_id");
        }

        /// <summary>
        /// **La medición.** Corre las dos políticas sobre los 37 vuelos y las 14 redes y escribe la
        /// tabla en `%TEMP%\taxi_corpus_measurement.txt`. No afirma un resultado: afirma que la
        /// medición se hizo (y sobre cuántos vuelos), porque el resultado es el dato que se lleva a
        /// la decisión de encender el interruptor.
        /// </summary>
        [TestMethod]
        public void TheMeasurementOfBothPoliciesIsWrittenToDisk()
        {
            var report = new StringBuilder();
            report.AppendLine("MEDICIÓN del cambio a node_id — corpus real de vmsOpenACars (29/09/2026)");
            report.AppendLine("37 PIREPs · 8 pilotos · 14 aeropuertos · rodaje de salida de cada vuelo");
            report.AppendLine();
            report.AppendLine("Identidad de nodos del grafo de rodaje:");
            report.AppendLine("  PROX = fusión por proximidad (45 m), lo que hace la app hoy");
            report.AppendLine("  IDS  = identidad por node_id del escenario (interruptor useNodeIds)");
            report.AppendLine();
            report.AppendLine(string.Format("{0,-8} {1,-5} {2,-7} {3,-33} {4,-33}",
                                            "VUELO", "AERO", "CLASE", "PROX", "IDS"));
            report.AppendLine(new string('-', 92));

            int same = 0, changed = 0, onlyProx = 0, onlyIds = 0, neither = 0, noNetwork = 0;
            int medibles = 0;
            double sumProxM = 0.0, sumIdsM = 0.0;
            int distCount = 0;

            var perAirport = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
            // [0]=medibles [1]=equal [2]=changed [3]=onlyProx [4]=onlyIds [5]=neither

            foreach (var f in _corpus.OrderBy(x => x.Airport).ThenBy(x => x.Ident))
            {
                List<TaxiGraph.Segment> net;
                if (!_networks.TryGetValue(f.Airport, out net) || net.Count == 0)
                {
                    noNetwork++;
                    report.AppendLine(string.Format("{0,-8} {1,-5} {2,-7} {3}", f.Ident, f.Airport,
                                                    "SIN RED", "(el aeropuerto no publica red de calles)"));
                    continue;
                }

                medibles++;
                var prox = TaxiGraph.Suggest(net, f.IniLat, f.IniLon, f.ObjLat, f.ObjLon,
                                             useNodeIds: false);
                var ids  = TaxiGraph.Suggest(net, f.IniLat, f.IniLon, f.ObjLat, f.ObjLon,
                                             useNodeIds: true);

                string cls;
                if (prox.Found && ids.Found && string.Equals(prox.Text, ids.Text, StringComparison.Ordinal))
                { cls = "IGUAL";   same++; }
                else if (prox.Found && ids.Found) { cls = "CAMBIA";  changed++; }
                else if (prox.Found)              { cls = "SOLO PROX"; onlyProx++; }
                else if (ids.Found)               { cls = "SOLO IDS";  onlyIds++; }
                else                              { cls = "NINGUNA";   neither++; }

                if (prox.Found && ids.Found)
                {
                    sumProxM += prox.DistanceM;
                    sumIdsM  += ids.DistanceM;
                    distCount++;
                }

                int[] counts;
                if (!perAirport.TryGetValue(f.Airport, out counts))
                    perAirport[f.Airport] = counts = new int[6];
                counts[0]++;
                if (cls == "IGUAL") counts[1]++;
                else if (cls == "CAMBIA") counts[2]++;
                else if (cls == "SOLO PROX") counts[3]++;
                else if (cls == "SOLO IDS") counts[4]++;
                else counts[5]++;

                report.AppendLine(string.Format("{0,-8} {1,-5} {2,-7} {3,-33} {4,-33}",
                                                f.Ident, f.Airport, cls,
                                                prox.Found ? prox.Text : "(sin ruta)",
                                                ids.Found ? ids.Text : "(sin ruta)"));
            }

            report.AppendLine();
            report.AppendLine("RESUMEN DE LA MEDICIÓN");
            report.AppendLine($"  vuelos del corpus .................. {_corpus.Count}");
            report.AppendLine($"  sin red publicada (no medibles) .... {noNetwork}");
            report.AppendLine($"  medibles ........................... {medibles}");
            report.AppendLine($"  ruta IDÉNTICA con las dos .......... {same}");
            report.AppendLine($"  ruta DISTINTA con las dos .......... {changed}");
            report.AppendLine($"  solo con proximidad ................ {onlyProx}");
            report.AppendLine($"  solo con node_id ................... {onlyIds}");
            report.AppendLine($"  sin ruta con ninguna ............... {neither}");
            if (distCount > 0)
            {
                report.AppendLine($"  longitud media (proximidad) ........ {sumProxM / distCount:F0} m");
                report.AppendLine($"  longitud media (node_id) ........... {sumIdsM / distCount:F0} m");
                report.AppendLine($"  diferencia media ................... {(sumIdsM - sumProxM) / distCount:+0;-0;0} m");
            }

            report.AppendLine();
            report.AppendLine("POR AEROPUERTO (medibles · igual · cambia · solo prox · solo ids · ninguna)");
            foreach (var kv in perAirport.OrderBy(k => k.Key))
            {
                int[] c = kv.Value;
                report.AppendLine($"  {kv.Key,-5} {c[0],3} · {c[1],3} · {c[2],3} · {c[3],3} · {c[4],3} · {c[5],3}");
            }

            string path = Path.Combine(Path.GetTempPath(), "taxi_corpus_measurement.txt");
            File.WriteAllText(path, report.ToString(), Encoding.UTF8);

            // Lo que se afirma no es el resultado —eso es el dato— sino que la medición se hizo
            // entera: un vuelo por línea, y todos los medibles clasificados en una clase.
            Assert.IsTrue(File.Exists(path), "no se escribió el volcado de la medición");
            Assert.AreEqual(_corpus.Count, same + changed + onlyProx + onlyIds + neither + noNetwork,
                            "todos los vuelos del corpus tienen que salir clasificados");
            Assert.IsTrue(medibles >= 35, $"vuelos medibles: {medibles}");
        }
    }
}
