using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El punto de espera que el avión tiene delante y cómo se llama, con los **26 puntos reales que
    /// NavData publica para SKBO** (29/09/2026), copiados de `/holdshort/`.
    ///
    /// Esta lista **cambió de fuente** ese día y por eso el inventario va fijado en un test: antes se
    /// deducía por geometría —14 puntos para la 14L, de los que **12 eran nodos de las paralelas**
    /// `A`/`A1`/`A2`/`L`, a 139–254 m del eje, y el avión rueda *sobre* ellos, así que el radio de
    /// 200 m y el filtro de rumbo no los descartaban y se avisaba «espera antes de pista 14L» yendo a
    /// la 14R—. Ahora sale de los **tipos de nodo del escenario** (`HSND`/`IHSND`) y la 14L tiene 6,
    /// la 14R 1 y hay un único `ils_hold_short` en todo el aeropuerto. Si alguien vuelve a cambiar la
    /// fuente, el inventario lo delata.
    ///
    /// Dos reglas que estos tests protegen: el filtro por pista se pregunta a **`runway_names`** (la
    /// pareja física, p. ej. `["14R","32L"]`), no a `runway_name` —un punto a mitad de pista no es de
    /// un extremo, es de la pista—; y el nombre del aviso es **la calle por la que llega el avión** si
    /// está entre las del nodo.
    /// </summary>
    [TestClass]
    public class HoldShortSelectorTests
    {
        private static NavHoldShort Hs(string runwayName, double lat, double lon, string taxiway,
                                       string type, string runwayNames, params string[] taxiways)
            => new NavHoldShort
            {
                RunwayName  = runwayName,
                Lat         = lat,
                Lon         = lon,
                Taxiway     = taxiway,
                Type        = type,
                RunwayNames = new List<string>(runwayNames.Split(',')),
                Taxiways    = new List<string>(taxiways),
            };

        private static List<NavHoldShort> Skbo() => new List<NavHoldShort>
        {
            Hs("14L", 4.701035, -74.154930, "K4", "hold_short", "14L,32R", "K4"),
            Hs("14L", 4.704394, -74.143143, "D",  "hold_short", "14L,32R", "D"),
            Hs("14L", 4.704467, -74.159454, "K3", "hold_short", "14L,32R", "K3"),
            Hs("14L", 4.704656, -74.143173, "D",  "hold_short", "14L,32R", "D"),
            Hs("14L", 4.705553, -74.143280, "D",  "hold_short", "14L,32R", "D"),
            Hs("14L", 4.712803, -74.152382, "A1", "hold_short", "14L,32R", "A1", "A2", "A3", "E"),
            Hs("14R", 4.710662, -74.168892, "K1", "hold_short", "14R,32L", "K1"),
            Hs("32L", 4.690323, -74.141785, "N",  "ils_hold_short", "14R,32L", "K7", "K8", "N"),
            Hs("32L", 4.694209, -74.145836, "P",  "hold_short", "14R,32L", "P"),
            Hs("32L", 4.697199, -74.150101, "D",  "hold_short", "14R,32L", "D", "N"),
            Hs("32L", 4.697251, -74.150536, "D",  "hold_short", "14R,32L", "D"),
            Hs("32L", 4.697334, -74.150963, "D",  "hold_short", "14R,32L", "D"),
            Hs("32L", 4.697445, -74.151398, "D",  "hold_short", "14R,32L", "D"),
            Hs("32L", 4.697612, -74.151894, "D",  "hold_short", "14R,32L", "D"),
            Hs("32L", 4.697736, -74.152168, "D",  "hold_short", "14R,32L", "D"),
            Hs("32L", 4.699103, -74.134926, "A7", "hold_short", "14R,32L", "A7"),
            Hs("32L", 4.700043, -74.136208, "A6", "hold_short", "14R,32L", "A6"),
            Hs("32L", 4.702490, -74.138924, "C",  "hold_short", "14R,32L", "C"),
            Hs("32R", 4.692601, -74.125076, "A",  "hold_short", "14L,32R", "A"),
            Hs("32R", 4.692825, -74.125038, "A",  "hold_short", "14L,32R", "A"),
            Hs("32R", 4.692936, -74.125069, "A",  "hold_short", "14L,32R", "A"),
            Hs("32R", 4.697793, -74.133110, "A8", "hold_short", "14L,32R", "A8"),
            Hs("32R", 4.698572, -74.133224, "A8", "hold_short", "14L,32R", "A8"),
            Hs("32R", 4.699077, -74.133430, "A8", "hold_short", "14L,32R", "A8"),
            Hs("32R", 4.699246, -74.133797, "A7", "hold_short", "14L,32R", "A7"),
            Hs("32R", 4.699413, -74.133667, "A8", "hold_short", "14L,32R", "A8"),
        };

        [TestMethod]
        public void TheInventoryIsTheScenarioOne_NotTheOldHeuristic()
        {
            // 26 en total (antes 35), 6 para la 14L (antes 14), 1 para la 14R (antes 5), 25
            // `hold_short` y **un** `ils_hold_short`. Es el «banco de pruebas» que NavData pidió
            // correr tras cambiar la fuente de los puntos.
            var all = Skbo();
            Assert.AreEqual(26, all.Count);
            Assert.AreEqual(6,  all.Count(h => h.RunwayName == "14L"));
            Assert.AreEqual(1,  all.Count(h => h.RunwayName == "14R"));
            Assert.AreEqual(11, all.Count(h => h.RunwayName == "32L"));
            Assert.AreEqual(8,  all.Count(h => h.RunwayName == "32R"));
            Assert.AreEqual(1,  all.Count(h => h.Type == "ils_hold_short"));
            Assert.AreEqual(25, all.Count(h => h.Type == "hold_short"));

            // El punto de 40 m con calle `E` que publicaba la geometría **ya no existe**: el
            // escenario no lo marca como nodo de espera.
            Assert.IsFalse(all.Any(h => h.Lat > 4.713 && h.Lon < -74.152),
                "el nodo de 40 m del eje no era un HSND del escenario");

            // Y `V` ya no se publica (Addenda 2, 29/09/2026): es un muñón de 54 m del eje de la
            // pista al punto de espera, un nombre que las cartas no rotulan. Si vuelve a aparecer
            // en un `taxiways`, este test lo dice.
            Assert.IsFalse(all.Any(h => h.Taxiways.Contains("V")),
                "`V` es un muñón de entrada, no una calle del punto de espera");
        }

        [TestMethod]
        public void TheRunwayFilterAsksForThePhysicalPair_NotTheNearestEndLabel()
        {
            // El caso que obliga a `runway_names`: un punto de espera de la **misma pista física**.
            // Este va etiquetado `32L` pero su pareja es `["14R","32L"]`: yendo a la **14R sí
            // sirve** —está en la franja que se va a usar— y filtrando por `runway_name` se
            // descartaría por error.
            var enD = Skbo().First(h => h.Lat == 4.697199);

            Assert.AreEqual("32L", enD.RunwayName, "la etiqueta del extremo más cercano");
            Assert.IsTrue(HoldShortSelector.ServesRunway(enD, "14R"),
                "es de la pista 14R/32L: sirve para la 14R aunque su etiqueta diga 32L");
            Assert.IsTrue(HoldShortSelector.ServesRunway(enD, "32L"));
            Assert.IsFalse(HoldShortSelector.ServesRunway(enD, "14L"),
                "un punto de la franja 14R/32L no es de la 14L");
        }

        [TestMethod]
        public void GoingTo14L_TheJunctionNearTheThresholdWins()
        {
            // La traza real de A3 (sus segmentos van de 4.71088,-74.15212 a 4.71280,-74.15238) hacia
            // el cruce A1/A2/A3/E, que es el **único** punto de espera de la 14L junto al umbral.
            double lat = 4.712530, lon = -74.152347;   // 30 m antes del cruce, sobre A3
            double rumboA3 = 352.0;

            var elegido = HoldShortSelector.Select(Skbo(), lat, lon, rumboA3, runway: "14L");

            Assert.IsNotNull(elegido);
            Assert.AreEqual("14L", elegido.RunwayName);
            Assert.AreEqual(4.712803, elegido.Lat, 1e-6, "es el nodo del cruce");
            Assert.AreEqual("A3", HoldShortSelector.ForCallout("A3", elegido),
                "y se llama como la calle por la que llega el avión, no como la sugerencia de NavData");
        }

        [TestMethod]
        public void GoingTo14R_TheHoldShortOfThe14LStripIsNotSelected()
        {
            // La red de seguridad que ya estaba: rodando por `D` hacia un punto de la franja
            // **14L/32R** y con destino 14R, ese punto no cuenta (y no hay ninguno de la 14R/32L en
            // 200 m), así que no se avisa.
            double lat = 4.704250, lon = -74.143160;   // sobre D, 30 m al sur del punto
            double rumboD = 010.0;

            Assert.IsNull(HoldShortSelector.Select(Skbo(), lat, lon, rumboD, runway: "14R"));

            // Y con destino 14L el mismo punto sí se avisa: el filtro discrimina pista, no distancia.
            var mismo = HoldShortSelector.Select(Skbo(), lat, lon, rumboD, runway: "14L");
            Assert.IsNotNull(mismo);
            Assert.AreEqual(4.704394, mismo.Lat, 1e-6);
        }

        [TestMethod]
        public void PastTheAccesses_DoesNotWarn()
        {
            // Con el punto de espera **detrás** (avión ya al norte de él), no se avisa; rodando hacia
            // él, sí. El filtro discrimina dirección, no distancia.
            double lat = 4.713700, lon = -74.152200;   // al norte del cruce
            double rumboNorte = 340.0;

            Assert.IsNull(HoldShortSelector.Select(Skbo(), lat, lon, rumboNorte, runway: "14L"),
                "con el punto detrás no se avisa");
            Assert.IsNotNull(HoldShortSelector.Select(Skbo(), lat, lon, 160.0, runway: "14L"),
                "rodando hacia él, el aviso sale");
        }

        [TestMethod]
        public void TheCalloutNamesTheTaxiwayTheAircraftIsOn_WhenItTouchesTheNode()
        {
            // En el cruce los cuatro nombres son ciertos: NavData sugiere `A1` y el piloto dice
            // «A3». Llegando por A3, el nombre bueno es A3; si la calle del avión no toca el nodo,
            // se usa la lista del nodo (`A1`) y nunca un vecino.
            var cruce = Skbo().First(h => h.Lat == 4.712803);

            Assert.AreEqual("A3", HoldShortSelector.ForCallout("A3", cruce));
            Assert.AreEqual("A1", HoldShortSelector.ForCallout("B7", cruce));
        }

        [TestMethod]
        public void WithoutTaxiwayNames_TheGeometricGuessIsKept()
        {
            // Degradar sin datos: si la respuesta no trae calles (o el cliente no las ha mapeado),
            // se sigue usando el muestreo geométrico de siempre.
            var mudo = new NavHoldShort { RunwayName = "14L", Lat = 4.712803, Lon = -74.152382 };

            Assert.AreEqual("A3", HoldShortSelector.ForCallout("A3", mudo));
            Assert.AreEqual("A3", HoldShortSelector.ForCallout("A3", null));

            var soloSugerida = new NavHoldShort { RunwayName = "14L", Taxiway = "A1" };
            Assert.AreEqual("A1", HoldShortSelector.ForCallout("A3", soloSugerida),
                "sin lista, la sugerencia de NavData manda sobre el muestreo");
        }
    }
}
