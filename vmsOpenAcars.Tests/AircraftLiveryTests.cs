using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El «🎨 Pintura: …» del log. El caso que lo originó es el del mantenedor: un PMDG 777-200LR
    /// cuyo título es solo `777-200LR` imprimía «Pintura: 777» —el modelo disfrazado de pintura—,
    /// porque el último recurso del detector devolvía cualquier token de 3–4 caracteres en
    /// mayúsculas y su lista de exclusión tenía `B777`, no `777` (v0.9.16).
    /// </summary>
    [TestClass]
    public class AircraftLiveryTests
    {
        [TestMethod]
        public void FromTitle_AModelOnlyTitle_IsNotALivery()
        {
            // El caso reportado, tal cual: título del PMDG, sin aerolínea.
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("777-200LR"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("737-800"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("Boeing 737-800"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("787-9"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("A320neo"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle(""));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("   "));
        }

        [TestMethod]
        public void FromTitle_WithTheAirlineInTheTitle_ReturnsIt()
        {
            Assert.AreEqual("British", AircraftLivery.FromTitle("PMDG 777-200LR British Airways"));
            Assert.AreEqual("Avianca", AircraftLivery.FromTitle("Fenix A320 CFM Avianca"));
            Assert.AreEqual("VHR", AircraftLivery.FromTitle("Airbus A320neo VHR"));
            Assert.AreEqual("LATAM", AircraftLivery.FromTitle("iniBuilds A320 LATAM"));
        }

        [TestMethod]
        public void FromTitle_WithALiveryCode_ReturnsTheCode()
        {
            // Un código de pintura es alfabético: AAL, DAL, SAS… Un modelo no, porque lleva dígitos.
            Assert.AreEqual("AAL", AircraftLivery.FromTitle("B738 AAL"));
            Assert.AreEqual("SAS", AircraftLivery.FromTitle("ToLiss A321 SAS"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("B738 B77L"), "dos modelos no son pintura");
        }

        // Nota: la pintura por matrícula (leer el `livery.cfg` del simulador) se implementó y se
        // **retiró** en v0.9.16 por decisión del mantenedor —«no quiero que escanee mi carpeta de
        // Community; si no se puede por FSUIPC, prefiero no tener esa exactitud»—. Por eso no hay
        // tests de eso: el código ya no está. Ver el CHANGELOG.
    }
}
