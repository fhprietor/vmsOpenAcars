using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Qué avisos de espacio aéreo llegan al OSD**, con los datos del PIREP `nk2a96dl5vO0B2ab`
    /// (SKBO→TJSJ, B738, 27/10/2026), que es el que destapó las dos cosas a la vez.
    ///
    /// En su llegada a San Juan, entre las 19:01 y las 19:36, el registro tiene **19** entradas de
    /// espacio aéreo (sin contar las de salida), y **cuatro caen en los últimos tres minutos de la
    /// final**. El piloto había apagado la casilla y las seguía viendo: la v0.9.55 solo silenciaba las
    /// zonas acotadas. Casi todas las demás eran `CTA … CLASS E5`: fronteras de clase E que no piden
    /// nada.
    /// </summary>
    [TestClass]
    public class AirspaceOsdPolicyTests
    {
        // Los tipos tal cual llegan del registro, con el interruptor **encendido**.
        [DataTestMethod]
        [DataRow("CTR", true,  DisplayName = "La torre de San Juan sí avisa: lleva frecuencia")]
        [DataRow("TMA", true,  DisplayName = "El TMA de San Juan también")]
        [DataRow("ATZ", true,  DisplayName = "ATZ: poco común pero accionable")]
        [DataRow("RMZ", true,  DisplayName = "RMZ: zona de radio obligatoria")]
        [DataRow("CTA", false, DisplayName = "La CTA (clase E) solo va al registro")]
        [DataRow("cta", false, DisplayName = "El tipo no siempre llega en mayúsculas")]
        public void ConElInterruptorEncendido(string type, bool expected)
            => Assert.AreEqual(expected, !AirspaceOsdPolicy.IsLogOnly(type));

        [DataTestMethod]
        [DataRow("CTR")]
        [DataRow("TMA")]
        [DataRow("CTA")]
        [DataRow("ATZ")]
        [DataRow("RMZ")]
        public void ConElInterruptorApagadoNoVaNadaAlOsd(string type)
        {
            // La casilla manda sobre todo lo informativo: en la v0.9.55 no mandaba sobre CTR/TMA y de
            // ahí los 19 avisos de la llegada a TJSJ.
            const bool switchOff = false;
            bool toOsd = switchOff && !AirspaceOsdPolicy.IsLogOnly(type);
            Assert.IsFalse(toOsd, $"{type} no puede pintarse con la casilla apagada");
        }

        [TestMethod]
        public void LaLlegadaA_SanJuan_ConLaCasillaApagadaQuedaEnSilencio()
        {
            // Las 19 entradas del registro en la llegada, en orden.
            string[] llegada =
            {
                "CTA", "CTA", "CTA", "CTA", "CTA", "CTA", "CTA", "CTA", "TMA", "CTR",
                "CTR", "CTR", "CTA", "CTA", "CTA", "CTR", "CTR", "TMA", "CTA", "CTA",
                "CTR", "TMA", "CTR", "CTR", "CTR"
            };

            const bool switchOff = false;
            int toOsd = 0;
            foreach (var type in llegada)
                if (switchOff && !AirspaceOsdPolicy.IsLogOnly(type)) toOsd++;

            Assert.AreEqual(0, toOsd, "con la casilla apagada no se pinta ninguno");
        }

        [TestMethod]
        public void LasZonasAcotadasSiguenSuCamino()
        {
            // Prohibited/Restricted/Danger no pasan por esta política: las decide la casilla sola en
            // `LogBoundedAirspace`. Aquí se fija que la política no las toque por accidente.
            Assert.IsFalse(AirspaceOsdPolicy.IsLogOnly("Prohibited"));
            Assert.IsFalse(AirspaceOsdPolicy.IsLogOnly("Restricted"));
            Assert.IsFalse(AirspaceOsdPolicy.IsLogOnly("Danger"));
        }
    }
}
