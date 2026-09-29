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
    /// El caso real **G74 → punto de espera A3 de la 14L en SKBO**, aportado por el mantenedor
    /// (que vuela ese aeropuerto) contra la ruta que propone el grafo.
    ///
    /// El grafo propone la **más corta geométricamente** y el mantenedor dice que la ruta normal es
    /// **`F E M A A3`**, con dos reglas que **no están en ningún dataset**: `B5` es un desvío a la
    /// izquierda en el que no se entra para continuar, y `X` no forma parte de la ruta. Ninguna de
    /// las dos se deduce de la geometría —`B5` está bien formada, une dos puntos de `A` y ahorra
    /// metros—, así que este fixture existe para que la base de conocimiento de rutas reales tenga
    /// contra qué validarse: cuando el cliente sepa proponer la ruta acostumbrada, los tests de
    /// abajo se dan la vuelta.
    ///
    /// Datos: taxiways reales de NavData (`/airport/SKBO/taxiways/`, 2026-09-29), recortados a las
    /// calles que intervienen en el caso. **Cuidado con leerlo como si fuera el aeropuerto entero**:
    /// al quitar las calles que no participan desaparecen rutas alternativas, así que reproduce el
    /// caso, no la competencia completa de SKBO (ver la nota en `WithoutX_TheGraphReachesThePilotPrefix`).
    ///
    /// **Y cuidado con leer el CSV como el camino de datos del producto: no lo es.** Es el banco de
    /// pruebas del caso (un test no puede salir a la red). La información de rutas de rodaje
    /// acostumbradas vivirá en **NavData**, que la agrega y la sirve a toda su comunidad; el cliente
    /// solo envía observaciones y lee la respuesta. El pedido está en
    /// `Docs/PEDIDO-NAVDATA-RUTAS-TAXI.md`.
    /// </summary>
    [TestClass]
    public class TaxiRouteCaseTests
    {
        // ── El caso, tal como lo describe el piloto ───────────────────────────────
        private const double StandG74Lat = 4.69887113571167,  StandG74Lon = -74.1445617675781;

        /// <summary>Extremo norte de la calle A3: es, al sexto decimal, el hold-short de 14L que el
        /// mantenedor llama «A3». NavData confirmó (29/09/2026) que ese nodo es un **cruce de cuatro
        /// calles** —`taxiways: ["A1","A2","A3","E"]`— y que su `taxiway` sugerida sale `A2` (la más
        /// perpendicular), no «A3»: por eso el nombre del punto de espera se toma de la lista y
        /// cotejado con la calle por la que llega el avión (`HoldShortSelector`).</summary>
        private const double HoldShortA3Lat = 4.712803, HoldShortA3Lon = -74.152382;

        /// <summary>La ruta que se hace de verdad, según el mantenedor.</summary>
        private const string CustomaryRoute = "F E M A A3";

        /// <summary>Lo que el grafo propone hoy (la más corta).</summary>
        private const string ShortestRoute = "F E X A B5 A A3";

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
                Assert.AreEqual(5, f.Length, $"línea de fixture mal formada: {line}");
                _skbo.Add(new TaxiGraph.Segment
                {
                    Name = f[0],
                    Lat1 = double.Parse(f[1], CultureInfo.InvariantCulture),
                    Lon1 = double.Parse(f[2], CultureInfo.InvariantCulture),
                    Lat2 = double.Parse(f[3], CultureInfo.InvariantCulture),
                    Lon2 = double.Parse(f[4], CultureInfo.InvariantCulture),
                });
            }
        }

        private static string Suggest(IEnumerable<TaxiGraph.Segment> segments)
            => TaxiGraph.Suggest(segments, StandG74Lat, StandG74Lon,
                                 HoldShortA3Lat, HoldShortA3Lon).Text;

        private static List<TaxiGraph.Segment> Without(params string[] streets)
            => _skbo.Where(s => !streets.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).ToList();

        // ── Lo que el grafo hace hoy ──────────────────────────────────────────────

        [TestMethod]
        public void TodayTheGraphSuggestsTheShortest_WhichIsNotTheCustomaryRoute()
        {
            // Punto de partida del caso: por distancia pura gana `F E X A B5 A A3`.
            // El mantenedor dice que la normal es `F E M A A3`: X no es ruta y a B5 no se entra para
            // continuar. **Cuando la base de conocimiento proponga la acostumbrada, este assert
            // cambia**: es el rojo-a-verde del caso.
            Assert.AreEqual(ShortestRoute, Suggest(_skbo),
                "si esto cambia, revisar por qué el grafo dejó de preferir la más corta");
        }

        [TestMethod]
        public void WithoutX_TheGraphReachesThePilotPrefix_ButStillWalksThroughB5()
        {
            // Sin `X`, el grafo llega a `F E M A` —el prefijo de la ruta del piloto— y **sigue
            // entrando en B5 para volver a A**. Eso separa los dos problemas:
            //   · `X` es un conector más corto que `M`: gana por distancia, no por topología.
            //   · `B5` es una calle real y bien formada que el grafo usa como atajo de paso, y que en
            //     la operación real no se usa para continuar. Eso **solo lo sabe quien rueda ahí**.
            //
            // Ojo con leer esto como si el fixture fuera SKBO entero: al recortar el dataset
            // desaparecen las alternativas que en el aeropuerto completo compiten con `M`
            // (medido sobre los 569 segmentos, sin X y sin B5 el grafo se va por `P`/`S`).
            string text = Suggest(Without("X"));
            Assert.AreEqual("F E M A B5 A A3", text,
                "sin X, el grafo debe llegar por M y seguir usando B5 como paso");
            StringAssert.StartsWith(text, "F E M A",
                "el prefijo de la ruta del piloto tiene que ser alcanzable en estos datos");
        }

        [TestMethod]
        public void TheCustomaryRouteIsNotProducedByAnyVariant()
        {
            // El corazón del caso: **ninguna** variante produce `F E M A A3`. No es cuestión de
            // ajustar el optimizador: la ruta del piloto ahorra giros y evita calles que el grafo
            // *no puede saber* que no se usan. Es exactamente lo que la base de conocimiento aporta.
            var variants = new[]
            {
                Suggest(_skbo),
                Suggest(Without("X")),
                Suggest(Without("B5")),
                Suggest(Without("X", "B5")),
            };

            CollectionAssert.DoesNotContain(variants, CustomaryRoute,
                "si el grafo ya propone la ruta del piloto, el caso está resuelto y hay que revisar esto");

            foreach (string v in variants)
                Assert.AreNotEqual(CustomaryRoute, v);
        }

        // ── Coherencia del propio fixture ─────────────────────────────────────────

        [TestMethod]
        public void TheCustomaryRouteOnlyNamesStreetsThatExistInTheData()
        {
            // Si un día desaparece una calle del dataset (obras, cambio de AIRAC), la ruta
            // acostumbrada guardada tiene que poder descartarse en vez de proponerse a ciegas.
            var available = new HashSet<string>(_skbo.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
            foreach (string name in CustomaryRoute.Split(' '))
                Assert.IsTrue(available.Contains(name),
                    $"la ruta del piloto nombra '{name}', que ya no está en los datos");
        }

        [TestMethod]
        public void TheTwoCulpritsAreStillInTheFixture()
        {
            // Los tests de arriba hablan de `X` y `B5`: si el recorte del fixture los perdiera, no
            // fallarían — dirían otra cosa. Se ancla también eso.
            Assert.IsTrue(_skbo.Any(s => s.Name == "X"),  "el fixture tiene que incluir la calle X");
            Assert.IsTrue(_skbo.Any(s => s.Name == "B5"), "el fixture tiene que incluir la calle B5");

            // Y el punto de espera sigue siendo el extremo norte de A3, que es como se identifica.
            var a3 = _skbo.Where(s => s.Name == "A3").ToList();
            Assert.IsTrue(a3.Count > 0, "el fixture tiene que incluir la calle A3");
            double nearest = a3.Min(s => Math.Min(
                GeoMath.DistanceNm(s.Lat1, s.Lon1, HoldShortA3Lat, HoldShortA3Lon),
                GeoMath.DistanceNm(s.Lat2, s.Lon2, HoldShortA3Lat, HoldShortA3Lon)) * 1852.0);
            Assert.IsTrue(nearest < 20.0,
                $"el hold-short «A3» tiene que caer en el extremo de la calle A3 (está a {nearest:F0} m)");
        }
    }
}
