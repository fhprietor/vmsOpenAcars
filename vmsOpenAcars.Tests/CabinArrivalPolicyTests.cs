using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **Cuándo se anuncia el desarme de toboganes.** Con los tiempos del vuelo real
    /// `3b0oBlXD5ZolKPM4` (AT76, SKUC→SKBO, 05/11/2026): el freno se puso a las **03:28:57** con la
    /// fase `TaxiIn`; el de después del pushback fue a las **01:57:51**, en fase `Pushback`, y no
    /// puede anunciar la llegada.
    /// </summary>
    [TestClass]
    public class CabinArrivalPolicyTests
    {
        [TestMethod]
        public void ElFrenoPuestoEnTaxiInTrasAterrizarAnunciaDesarmarToboganes()
        {
            // 03:28:57 — Parking Brake: SET, en el puesto y ya rodando la llegada.
            Assert.IsTrue(CabinArrivalPolicy.ShouldAnnounceSlidesDisarm(
                FlightPhase.TaxiIn, hasLanded: true, parkingBrakeSet: true, alreadySent: false));
        }

        [TestMethod]
        public void ElFrenoDelPushbackNoAnunciaDesarmarToboganes()
        {
            // 01:57:51 — fin del empuje, freno puesto en fase `Pushback`. No es una llegada.
            Assert.IsFalse(CabinArrivalPolicy.ShouldAnnounceSlidesDisarm(
                FlightPhase.Pushback, hasLanded: false, parkingBrakeSet: true, alreadySent: false));

            // Aunque la bandera de aterrizaje se quedara pegada, la fase manda.
            Assert.IsFalse(CabinArrivalPolicy.ShouldAnnounceSlidesDisarm(
                FlightPhase.Pushback, hasLanded: true, parkingBrakeSet: true, alreadySent: false));
        }

        [TestMethod]
        public void ElDesarmeDeToboganesNoSeRepite()
        {
            Assert.IsFalse(CabinArrivalPolicy.ShouldAnnounceSlidesDisarm(
                FlightPhase.TaxiIn, hasLanded: true, parkingBrakeSet: true, alreadySent: true));
        }

        [TestMethod]
        public void SinAterrizajeNoSeAnuncia()
        {
            // `TaxiIn` antes de tocar tierra no existe, pero la bandera lo deja explícito: sin
            // aterrizaje no hay toboganes que desarmar.
            Assert.IsFalse(CabinArrivalPolicy.ShouldAnnounceSlidesDisarm(
                FlightPhase.TaxiIn, hasLanded: false, parkingBrakeSet: true, alreadySent: false));
        }
    }
}
