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
    /// **El umbral de confianza, en dos niveles** (`TaxiGraph.ConfidenceFloor`). La regla existe porque
    /// descartar los empalmes flojos sin más **partiría aeropuertos**: en **CYUL** el **único** empalme
    /// publicado tiene **confianza 0,14** y un giro de **68°** —un puente feo, pero es el único que une
    /// las dos componentes—, mientras que el `component_bridge` de SKBO viene con **0,88** y 0,6°.
    ///
    /// Con el umbral tal como está: primero se intenta sin los flojos (que no ensucien una ruta que ya
    /// se podía hacer bien) y, **solo si con eso no hay ruta**, se repite con todos. Los dos hechos se
    /// fijan aquí con el CYUL real, y los dos son decisivos:
    /// **(1)** sin el puente flojo **no hay ruta** —así que la reaparición del segundo nivel no es un
    /// adorno: es lo único que hace que esto funcione—, y **(2)** con él la ruta sale y viene marcada
    /// (`UsedLowConfidence`), no colada en silencio.
    /// </summary>
    [TestClass]
    public class TaxiConfidenceTests
    {
        private const string Airport = "CYUL";

        private static List<TaxiGraph.Segment> _network;
        private static double _aLat, _aLon, _bLat, _bLon;

        [ClassInitialize]
        public static void LoadRealData(TestContext _)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                       "Fixtures", "taxi-networks-2026-09-29.csv");
            Assert.IsTrue(File.Exists(path), $"falta el fixture de redes en {path}");

            _network = new List<TaxiGraph.Segment>();
            double bestConf = double.MaxValue;

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] f = line.Split(',');
                if (f.Length < 9 || !string.Equals(f[0], Airport, StringComparison.OrdinalIgnoreCase))
                    continue;

                long v, nodeA = 0, nodeB = 0;
                bool hasA = long.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                if (hasA) nodeA = v;
                bool hasB = long.TryParse(f[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                if (hasB) nodeB = v;

                double conf = 1.0;   // los segmentos de calle no traen confianza: son aristas firmes
                if (f.Length > 10 && !string.IsNullOrWhiteSpace(f[10]))
                    double.TryParse(f[10], NumberStyles.Float, CultureInfo.InvariantCulture, out conf);

                _network.Add(new TaxiGraph.Segment
                {
                    Name = f[2],
                    Lat1 = double.Parse(f[3], CultureInfo.InvariantCulture),
                    Lon1 = double.Parse(f[4], CultureInfo.InvariantCulture),
                    Lat2 = double.Parse(f[5], CultureInfo.InvariantCulture),
                    Lon2 = double.Parse(f[6], CultureInfo.InvariantCulture),
                    NodeA = hasA ? (long?)nodeA : null,
                    NodeB = hasB ? (long?)nodeB : null,
                    Confidence = conf,
                });

                // El empalme flojo de CYUL: el de menos confianza de los que están por debajo del umbral.
                if (f[1] == "join" && conf < TaxiGraph.ConfidenceFloor && conf < bestConf)
                {
                    bestConf = conf;
                    _aLat = double.Parse(f[3], CultureInfo.InvariantCulture);
                    _aLon = double.Parse(f[4], CultureInfo.InvariantCulture);
                    _bLat = double.Parse(f[5], CultureInfo.InvariantCulture);
                    _bLon = double.Parse(f[6], CultureInfo.InvariantCulture);
                }
            }

            Assert.IsTrue(_network.Count > 100, $"red de CYUL: {_network.Count} segmentos");
            Assert.IsTrue(bestConf < TaxiGraph.ConfidenceFloor,
                          $"CYUL tiene que traer un empalme por debajo del umbral (el más flojo: {bestConf})");
        }

        /// <summary>
        /// **(1)** Sin el puente flojo los dos componentes están separados y **la ruta no existe**: es
        /// lo que demuestra que el segundo nivel no es un adorno. **(2)** Con todo el dataset la ruta
        /// sale y **dice que necesitó el empalme flojo**. Con identidad por `node_id`, que es donde las
        /// componentes son las de verdad (la fusión por proximidad las cose).
        /// </summary>
        [TestMethod]
        public void TheWeakBridgeIsTheOnlyLink_AndTheSecondTierFindsTheRoute()
        {
            var strongOnly = _network.Where(s => s.Confidence >= TaxiGraph.ConfidenceFloor).ToList();

            var withoutBridge = TaxiGraph.Suggest(strongOnly, _aLat, _aLon, _bLat, _bLon,
                                                  useNodeIds: true);
            Assert.IsFalse(withoutBridge.Found,
                           "sin el puente flojo las dos componentes de CYUL están separadas: " +
                           "por eso descartarlo sin más partiría el aeropuerto");

            var withBridge = TaxiGraph.Suggest(_network, _aLat, _aLon, _bLat, _bLon,
                                               useNodeIds: true);
            Assert.IsTrue(withBridge.Found, "con el puente flojo la ruta tiene que salir");
            Assert.IsTrue(withBridge.UsedLowConfidence,
                          "y tiene que decir que la encontró en el segundo nivel");
        }
    }
}
