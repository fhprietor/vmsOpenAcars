using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Orden de las posiciones ATC en el panel lateral.
    ///
    /// El orden no es cosmético: en rodaje y despegue lo que el piloto necesita
    /// inmediatamente es DEL/GND/TWR, no CTR. Si el criterio se rompiera, el panel
    /// seguiría siendo "correcto" a simple vista pero con lo urgente al final de una
    /// lista que puede tener decenas de posiciones.
    ///
    /// La regla vive en Helpers (no en el control WinForms) precisamente para poder
    /// probarla sin arrastrar System.Windows.Forms al proyecto de tests.
    /// </summary>
    [TestClass]
    public class AtcStationOrderTests
    {
        [TestMethod]
        public void LocalPositions_ComesBeforeAreaPositions()
        {
            int del = AtcStationOrder.PositionRank("DEL");
            int gnd = AtcStationOrder.PositionRank("GND");
            int twr = AtcStationOrder.PositionRank("TWR");
            int app = AtcStationOrder.PositionRank("APP");
            int ctr = AtcStationOrder.PositionRank("CTR");

            Assert.IsTrue(del < gnd, "DEL antes que GND");
            Assert.IsTrue(gnd < twr, "GND antes que TWR");
            Assert.IsTrue(twr < app, "las locales antes que las de área");
            Assert.IsTrue(app < ctr, "APP antes que CTR");
        }

        [TestMethod]
        public void Atis_ComesAfterTowerButBeforeApproach()
        {
            // El ATIS acompaña a las dependencias del aeródromo, así que va con las
            // locales; después de TWR porque el contacto real es la torre.
            Assert.IsTrue(AtcStationOrder.PositionRank("TWR") < AtcStationOrder.PositionRank("ATIS"));
            Assert.IsTrue(AtcStationOrder.PositionRank("ATIS") < AtcStationOrder.PositionRank("APP"));
        }

        [TestMethod]
        public void UnknownAndMissingPosition_FallToTheEnd()
        {
            int unknown = AtcStationOrder.PositionRank("XYZ");
            foreach (string known in new[] { "DEL", "GND", "TWR", "ATIS", "APP", "DEP", "CTR" })
                Assert.IsTrue(unknown > AtcStationOrder.PositionRank(known),
                    $"una posición desconocida no debe adelantar a {known}");

            Assert.AreEqual(unknown, AtcStationOrder.PositionRank(null));
            Assert.AreEqual(unknown, AtcStationOrder.PositionRank(""));
        }

        [TestMethod]
        public void IsCaseInsensitive()
        {
            Assert.AreEqual(AtcStationOrder.PositionRank("TWR"), AtcStationOrder.PositionRank("twr"));
        }

        [TestMethod]
        public void Sort_OrdersByAirportThenByRank()
        {
            var stations = new List<IvaoAtcStation>
            {
                new IvaoAtcStation { Icao = "SKBO", Position = "CTR" },
                new IvaoAtcStation { Icao = "SKBO", Position = "TWR" },
                new IvaoAtcStation { Icao = "SKBO", Position = "DEL" },
                new IvaoAtcStation { Icao = "SKRG", Position = "GND" },
                new IvaoAtcStation { Icao = "SKBO", Position = "APP" },
            };

            var ordered = AtcStationOrder.Sort(stations)
                .Select(s => $"{s.Icao}_{s.Position}")
                .ToList();

            CollectionAssert.AreEqual(
                new[] { "SKBO_DEL", "SKBO_TWR", "SKBO_APP", "SKBO_CTR", "SKRG_GND" },
                ordered);
        }

        [TestMethod]
        public void Sort_NullInput_ReturnsEmptyInsteadOfThrowing()
        {
            Assert.AreEqual(0, AtcStationOrder.Sort(null).Count);
        }
    }
}

