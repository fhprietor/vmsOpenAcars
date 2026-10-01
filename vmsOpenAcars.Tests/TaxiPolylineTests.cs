using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La polilínea de la sugerencia**, sobre el caso real `G74 → A3` de la 14L de SKBO (237
    /// segmentos reales del escenario en `Fixtures/SKBO-taxi-2026-09-29.csv`).
    ///
    /// Hasta v0.9.22 `TaxiGraph.RouteSuggestion` solo exponía `Text` y `DistanceM`, así que la
    /// geometría del camino que el algoritmo **sí había recorrido** se perdía al pintar el popup y
    /// NavData no podía auditar su `planned` contra su dataset. Aquí se fija que la polilínea es
    /// exactamente ese camino: cada tramo dibujado es un segmento real de la red (o el salto de
    /// unión, que es el hueco que la fusión de nodos del grafo no cobra) y la secuencia de calles
    /// que se lee de ella es la misma que la sugerencia ya publicaba como texto. Si los puntos
    /// estuvieran desordenados o faltara uno, esa secuencia no cuadraría.
    /// </summary>
    [TestClass]
    public class TaxiPolylineTests
    {
        private const double StandG74Lat = 4.69887113571167, StandG74Lon = -74.1445617675781;
        private const double HoldShortA3Lat = 4.712803, HoldShortA3Lon = -74.152382;

        /// <summary>Tolerancia para reconocer un par de vértices como los extremos EXACTOS de un
        /// segmento del fixture: la polilínea copia esas coordenadas, así que es igualdad salvo
        /// ruido de coma flotante.</summary>
        private const double ExactDeg = 1e-9;

        private static List<TaxiGraph.Segment> _skbo;

        [ClassInitialize]
        public static void LoadFixture(TestContext _)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                       "Fixtures", "SKBO-taxi-2026-09-29.csv");
            Assert.IsTrue(File.Exists(path), $"falta el fixture de taxiways en {path}");

            _skbo = new List<TaxiGraph.Segment>();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] f = line.Split(',');
                Assert.IsTrue(f.Length == 5 || f.Length == 7, $"línea de fixture mal formada: {line}");
                long? nodeA = null, nodeB = null;
                if (f.Length == 7)
                {
                    long v;
                    if (long.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) nodeA = v;
                    if (long.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) nodeB = v;
                }
                _skbo.Add(new TaxiGraph.Segment
                {
                    Name = f[0],
                    Lat1 = double.Parse(f[1], CultureInfo.InvariantCulture),
                    Lon1 = double.Parse(f[2], CultureInfo.InvariantCulture),
                    Lat2 = double.Parse(f[3], CultureInfo.InvariantCulture),
                    Lon2 = double.Parse(f[4], CultureInfo.InvariantCulture),
                    NodeA = nodeA,
                    NodeB = nodeB,
                });
            }
        }

        private static double Meters(double lat1, double lon1, double lat2, double lon2)
            => GeoMath.DistanceNm(lat1, lon1, lat2, lon2) * GeoMath.MetersPerNm;

        private static double PolylineLengthM(List<(double Lat, double Lon)> poly)
        {
            double total = 0.0;
            for (int i = 1; i < poly.Count; i++)
                total += Meters(poly[i - 1].Lat, poly[i - 1].Lon, poly[i].Lat, poly[i].Lon);
            return total;
        }

        /// <summary>El segmento del fixture cuyos extremos son ese par, en cualquier sentido.</summary>
        private static TaxiGraph.Segment SegmentOf((double Lat, double Lon) p, (double Lat, double Lon) q)
            => _skbo.FirstOrDefault(s =>
                   (Near(s.Lat1, s.Lon1, p) && Near(s.Lat2, s.Lon2, q)) ||
                   (Near(s.Lat2, s.Lon2, p) && Near(s.Lat1, s.Lon1, q)));

        private static bool Near(double lat, double lon, (double Lat, double Lon) p)
            => Math.Abs(lat - p.Lat) < ExactDeg && Math.Abs(lon - p.Lon) < ExactDeg;

        private static TaxiGraph.RouteSuggestion StandToHoldShort14L(bool useNodeIds = false)
            => TaxiGraph.Suggest(_skbo, StandG74Lat, StandG74Lon,
                                 HoldShortA3Lat, HoldShortA3Lon, useNodeIds: useNodeIds);

        [TestMethod]
        public void ThePolylineIsTheWalkedPath_AndReadsBackTheSameStreets()
        {
            var r = StandToHoldShort14L();

            Assert.IsTrue(r.Found, "el caso G74 → A3 tiene que encontrar ruta");
            Assert.IsTrue(r.Polyline.Count >= 2, $"la polilínea trae {r.Polyline.Count} puntos");

            double firstM = Meters(r.Polyline[0].Lat, r.Polyline[0].Lon, StandG74Lat, StandG74Lon);
            Assert.IsTrue(firstM <= 300.0,
                $"arranca en el nodo de la red más cercano al puesto ({firstM:F1} m)");

            var last = r.Polyline[r.Polyline.Count - 1];
            double lastM = Meters(last.Lat, last.Lon, HoldShortA3Lat, HoldShortA3Lon);
            // La tolerancia es el radio de fusión de nodos del grafo (SnapM = 45 m): el vértice es el
            // extremo del último segmento recorrido, no la coordenada del nodo fusionado.
            Assert.IsTrue(lastM <= 50.0,
                $"acaba en el punto de espera A3 de la 14L ({lastM:F1} m)");

            // Cada tramo dibujado es un segmento real de la red o el salto de unión (≤ 2×SnapM, que
            // es lo máximo que pueden separarse dos extremos que la fusión ha metido en el mismo
            // nodo), y de los segmentos se lee la misma secuencia de calles que ya daba `Text`.
            var read = new List<string>();
            for (int i = 1; i < r.Polyline.Count; i++)
            {
                var seg = SegmentOf(r.Polyline[i - 1], r.Polyline[i]);
                if (seg != null)
                {
                    string name = seg.Name.Trim();
                    if (read.Count == 0 || !string.Equals(read[read.Count - 1], name,
                                                          StringComparison.OrdinalIgnoreCase))
                        read.Add(name);
                }
                else
                {
                    double gap = Meters(r.Polyline[i - 1].Lat, r.Polyline[i - 1].Lon,
                                        r.Polyline[i].Lat, r.Polyline[i].Lon);
                    Assert.IsTrue(gap <= 2.0 * TaxiGraph.SnapM,
                        $"el tramo {i - 1}→{i} no es un segmento de la red ni un salto de unión " +
                        $"({gap:F1} m): la polilínea se ha salido del camino");
                }
            }

            CollectionAssert.AreEqual(r.Names, read,
                "la secuencia de calles leída de la polilínea tiene que ser la de la sugerencia");
        }

        [TestMethod]
        public void ThePolylineCoversEveryTraversedSegment_SoItIsAtLeastTheRouteDistance()
        {
            var r = StandToHoldShort14L();

            // La polilínea une los tramos recorridos uno a uno, así que nunca puede medir menos que
            // `DistanceM`. Lo que suma de más son los saltos de unión: la fusión de nodos del grafo
            // los da por gratis, pero la geometría del dataset los tiene. Medido en este caso real:
            // 2 126 m de polilínea contra 1 869 m de `DistanceM`.
            double len = PolylineLengthM(r.Polyline);
            Assert.IsTrue(len >= r.DistanceM - 1.0,
                $"la polilínea ({len:F0} m) no puede ser más corta que la ruta ({r.DistanceM:F0} m)");

            double maxGaps = 2.0 * TaxiGraph.SnapM * r.Polyline.Count;
            Assert.IsTrue(len <= r.DistanceM + maxGaps,
                $"la polilínea ({len:F0} m) se pasa de la ruta ({r.DistanceM:F0} m) más de lo que " +
                $"permiten los saltos de unión ({maxGaps:F0} m)");
        }

        [TestMethod]
        public void TheJunctionPointIsNotRepeated()
        {
            var r = StandToHoldShort14L();

            for (int i = 1; i < r.Polyline.Count; i++)
            {
                double d = Meters(r.Polyline[i - 1].Lat, r.Polyline[i - 1].Lon,
                                  r.Polyline[i].Lat, r.Polyline[i].Lon);
                Assert.IsTrue(d > 0.001, $"los puntos {i - 1} y {i} son el mismo ({d:F4} m)");
            }
        }

        [TestMethod]
        public void TheSameHoldsWithNodeIds_WhichIsTheOtherPolicyOfTheGraph()
        {
            var r = StandToHoldShort14L(useNodeIds: true);

            if (!r.Found) return;   // la política por id puede no encontrar ruta en este recorte
            Assert.IsTrue(r.Polyline.Count >= 2);
            Assert.IsTrue(PolylineLengthM(r.Polyline) >= r.DistanceM - 1.0);
        }

        [TestMethod]
        public void WithoutARoute_ThereIsNoPolyline()
        {
            var r = TaxiGraph.Suggest(new List<TaxiGraph.Segment>(), StandG74Lat, StandG74Lon,
                                      HoldShortA3Lat, HoldShortA3Lon);
            Assert.IsFalse(r.Found);
            Assert.AreEqual(0, r.Polyline.Count, "sin ruta no se inventa geometría");
        }
    }
}
