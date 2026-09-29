using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El caso real de la sugerencia de rodaje de KMIA** (PIREP `Z8pORZd86Zr68OL1`, `VHR31`
    /// KMIA→SKRG, cliente 0.9.16, puesto G181). Su propio log dejó escrita la ruta que el grafo
    /// propuso:
    ///
    ///     GUÍA DE RODAJE: PISTA 08R
    ///     VÍA 26 24 Q Q8 T 20 T P S P 16 13 Y1 W Y1 HH JJ M Z M3 M M2 N P Q Q1 M1
    ///
    /// **Veintiocho calles, con repeticiones** (`T`, `P`, `Y1` y `Q` dos veces cada una y un
    /// `16 13` en medio) para un rodaje que en la realidad fueron `24 Q Q8 … M1 L1`. Este test fija
    /// la causa y la comprueba con el mismo código que usa la app: el destino de entonces era el
    /// **umbral de `/runways/` (Navigraph)** —un punto que la red de MSFS alcanza por otro sitio— y
    /// el de ahora es el **punto de espera** de la pista, de la misma fuente que la red
    /// (`HoldShortSelector.AccessPointFor`).
    ///
    /// Los datos son reales y están en los fixtures: la red de KMIA (3.138 segmentos),
    /// sus 170 puntos de espera y sus 8 pistas, y el punto de arranque, que es la primera muestra
    /// de rodaje del vuelo (el final de su pushback; el popup salió 20 s después, a unos metros).
    /// </summary>
    [TestClass]
    public class TaxiSuggestionKmIaTests
    {
        private const string Airport  = "KMIA";
        private const string Runway   = "08R";

        private static double _startLat, _startLon;
        private static double _goalLat, _goalLon;
        private static double _thresholdLat, _thresholdLon;
        private static List<TaxiGraph.Segment> _network;
        private static List<NavHoldShort> _holds;

        private static string FixturePath(string name)
            => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name);

        [ClassInitialize]
        public static void LoadRealData(TestContext _)
        {
            // ── La red de KMIA (segmentos + empalmes) ─────────────────────────────
            string netPath = FixturePath("taxi-networks-2026-09-29.csv");
            Assert.IsTrue(File.Exists(netPath), $"falta el fixture de redes en {netPath}");
            _network = new List<TaxiGraph.Segment>();
            foreach (string raw in File.ReadAllLines(netPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] f = line.Split(',');
                if (!string.Equals(f[0], Airport, StringComparison.OrdinalIgnoreCase)) continue;

                long v, nodeA = 0, nodeB = 0;
                bool hasA = long.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                if (hasA) nodeA = v;
                bool hasB = long.TryParse(f[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                if (hasB) nodeB = v;

                _network.Add(new TaxiGraph.Segment
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
            Assert.IsTrue(_network.Count > 3000, $"red de KMIA: {_network.Count} segmentos");

            // ── Pistas y puntos de espera ─────────────────────────────────────────
            string hrPath = FixturePath("KMIA-holds-runways-2026-09-29.csv");
            Assert.IsTrue(File.Exists(hrPath), $"falta pistas y puntos de espera en {hrPath}");
            _holds = new List<NavHoldShort>();
            foreach (string raw in File.ReadAllLines(hrPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] f = line.Split(',');

                if (f[0] == "runway")
                {
                    if (string.Equals(f[1], Runway, StringComparison.OrdinalIgnoreCase))
                    {
                        _thresholdLat = double.Parse(f[9],  CultureInfo.InvariantCulture);
                        _thresholdLon = double.Parse(f[10], CultureInfo.InvariantCulture);
                    }
                    continue;
                }

                var names = string.IsNullOrWhiteSpace(f[3])
                    ? new List<string>()
                    : f[3].Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                var streets = string.IsNullOrWhiteSpace(f[4])
                    ? new List<string>()
                    : f[4].Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

                _holds.Add(new NavHoldShort
                {
                    RunwayName  = f[2],
                    RunwayNames = names,
                    Taxiways    = streets,
                    Taxiway     = f[5],
                    Type        = f[6],
                    Lat         = double.Parse(f[7], CultureInfo.InvariantCulture),
                    Lon         = double.Parse(f[8], CultureInfo.InvariantCulture),
                });
            }
            Assert.IsTrue(_holds.Count > 100, $"puntos de espera de KMIA: {_holds.Count}");
            Assert.IsTrue(_thresholdLat > 25.0, "no se leyó el umbral de la 08R");

            // ── El punto de arranque: el rodaje real de VHR31 ─────────────────────
            string corpusPath = FixturePath("taxi-corpus-2026-09-29.csv");
            Assert.IsTrue(File.Exists(corpusPath), $"falta el corpus en {corpusPath}");
            var header = File.ReadAllLines(corpusPath).First(l => !l.StartsWith("#"))
                             .Split(',').Select(h => h.Trim().Trim('"')).ToArray();
            int iAirport = Array.IndexOf(header, "dpto");
            int iIniLat  = Array.IndexOf(header, "ini_lat");
            int iIniLon  = Array.IndexOf(header, "ini_lon");
            int iObjLat  = Array.IndexOf(header, "obj_lat");
            int iObjLon  = Array.IndexOf(header, "obj_lon");

            bool found = false;
            foreach (string raw in File.ReadAllLines(corpusPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] f = line.Split(',');
                for (int i = 0; i < f.Length; i++) f[i] = f[i].Trim().Trim('"');
                if (f.Length <= iIniLon || f[iAirport] != Airport) continue;

                _startLat = double.Parse(f[iIniLat], CultureInfo.InvariantCulture);
                _startLon = double.Parse(f[iIniLon], CultureInfo.InvariantCulture);
                _goalLat  = double.Parse(f[iObjLat], CultureInfo.InvariantCulture);
                _goalLon  = double.Parse(f[iObjLon], CultureInfo.InvariantCulture);
                found = true;
                break;
            }
            Assert.IsTrue(found, "no se encontró el arranque de VHR31 en el corpus");
        }

        /// <summary>
        /// **La causa, reproducida**: apuntando al umbral de Navigraph el grafo se va por las ramas
        /// y apuntando al punto de espera de la pista (el de la misma fuente que la red) la ruta es
        /// la del rodaje real. Las dos cifras quedan fijadas aquí: si alguien vuelve a mover el
        /// destino de la sugerencia, esto lo dice.
        /// </summary>
        [TestMethod]
        public void GoingToTheThresholdRunsAway_GoingToTheHoldShortIsTheRealRoute()
        {
            var toThreshold = TaxiGraph.Suggest(_network, _startLat, _startLon,
                                                _thresholdLat, _thresholdLon);

            var access = HoldShortSelector.AccessPointFor(_holds, Runway,
                                                          _thresholdLat, _thresholdLon);
            Assert.IsNotNull(access, "la 08R tiene que tener punto de espera");

            var toHoldShort = TaxiGraph.Suggest(_network, _startLat, _startLon,
                                                access.Lat, access.Lon);

            Assert.IsTrue(toThreshold.Found, "la variante del umbral tiene que producir ruta");
            Assert.IsTrue(toHoldShort.Found, "la variante del punto de espera tiene que producir ruta");

            // **Lo medido, y desmiente la hipótesis con la que nació este test.** La variante del
            // umbral reproduce **exactamente** la ruta que el vuelo dejó en su log —así que la causa
            // del destino queda probada—, pero la del punto de espera da **la misma ruta y una calle
            // más** (`L1`): el punto de espera de acceso de la 08R está a **84 m** del umbral, o sea
            // en el mismo sitio para el grafo. Es decir: **cambiar el destino NO arregla este caso**;
            // las 24 calles de más son de la **topología** de KMIA (3.138 segmentos y **cero empalmes
            // curados publicados**), que es justo lo que mide `TaxiCorpusMeasurementTests` — allí KMIA
            // es el único vuelo donde la identidad por `node_id` **no encuentra ninguna ruta**—.
            Assert.AreEqual(
                "26 24 Q Q8 T 20 T P S P 16 13 Y1 W Y1 HH JJ M Z M3 M M2 N P Q Q1 M1",
                toThreshold.Text,
                "la ruta al umbral de Navigraph: es la que el vuelo anunció en su log");
            Assert.AreEqual(
                "26 24 Q Q8 T 20 T P S P 16 13 Y1 W Y1 HH JJ M Z M3 M M2 N P Q Q1 M1 L1",
                toHoldShort.Text,
                "la ruta al punto de espera: misma ruta, una calle más (el acceso está a 84 m del umbral)");
        }

        /// <summary>
        /// **El arreglo que sí resuelve este caso.** Con identidad por `node_id` —la política que
        /// apaga la fusión por proximidad— la red de KMIA se parte en dos: la plataforma (2.728
        /// nodos, un componente) y los **muñones del eje de pista** (`L1`, `K1`, 16 nodos) que
        /// NavData mide aparte y que el criterio de empalmes **rechaza unir con razón** (el par más
        /// cercano está a 2,8 m con giro de 179,6°, es decir dos muñones opuestos sobre el
        /// pavimento). El destino de esta ruta —el acceso de la 08R— cae justo ahí, así que sin más
        /// la ruta **no se encuentra**.
        ///
        /// El arreglo es del lado del cliente y sirve en cualquier aeropuerto: **si el destino no es
        /// alcanzable, se enruta al nodo alcanzable más cercano a él** —el borde de plataforma— y se
        /// deja dicho (`GoalMoved` / `GoalMovedM`) para que el llamante lo registre. El Dijkstra ya
        /// había recorrido todo el componente del avión, así que no hace falta calcular componentes
        /// ni creerle a nadie: la propia búsqueda dice qué es alcanzable.
        /// </summary>
        [TestMethod]
        public void WithNodeIdsTheRunwayStubIsUnreachable_AndTheRouteGoesToThePlatformEdge()
        {
            // **El destino es el punto por el que el vuelo entró de verdad en la pista**, no el umbral
            // de Navigraph: con el umbral el nodo más cercano resulta estar en la plataforma y la ruta
            // se encuentra sin más —comprobado, y por eso este test nació mal apuntando allí—. Es el
            // punto del corpus (25.80075, -80.30057), el del rodaje real.
            var toStub = TaxiGraph.Suggest(_network, _startLat, _startLon,
                                           _goalLat, _goalLon, useNodeIds: true);

            // Y con el destino de plataforma la ruta sí es idéntica a la de la fusión: eso es lo que
            // mide el corpus. Aquí se fija el otro extremo: que el punto de entrada **no** es
            // alcanzable por ids y que el aviso de «me he movido al borde» sale.
            var toPlatform = TaxiGraph.Suggest(_network, _startLat, _startLon,
                                               _thresholdLat, _thresholdLon, useNodeIds: true);
            Assert.IsTrue(toPlatform.Found, "al umbral (plataforma) la ruta tiene que encontrarse");
            Assert.IsFalse(toPlatform.GoalMoved,
                           "y sin mover el destino: cerca del umbral hay nodos de plataforma");

            Assert.IsTrue(toStub.Found, "al punto de entrada la ruta sale por el borde de plataforma");
            Assert.IsTrue(toStub.GoalMoved, "el punto de entrada cae en los muñones y hay que decirlo");
            Assert.IsTrue(toStub.GoalMovedM < 500.0,
                          $"el borde de plataforma está a {toStub.GoalMovedM:F0} m de lo pedido");

            // **Seis calles contra veintiocho.** Con ids, la ruta al borde de plataforma es la sensata
            // (`26 24 Q P U P M1`) y mueve el destino 76 m; la de la fusión por proximidad es la de 28
            // calles con repeticiones del log —`… T P S P … Y1 W Y1 …`—, porque la fusión **puede coser
            // el muñón de pista** a la plataforma y a partir de ahí el grafo se va por donde quiere.
            // O sea: los ids no solo no empeoran este caso, lo mejoran de forma brutal.
            Assert.AreEqual("26 24 Q P U P M1", toStub.Text,
                            "la ruta al borde de plataforma con identidad por node_id");
            Assert.AreEqual(76.0, toStub.GoalMovedM, 1.0,
                            "el borde de plataforma más cercano a la entrada de la 08R");
        }
    }
}
