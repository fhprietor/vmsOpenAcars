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

        [TestMethod]
        public void FromTitle_TheManufacturerIsNeverTheLivery()
        {
            // El caso real del PIREP `nROPdk4GxYY5AOy1`: título del ATR en MSFS 2024 tal cual lo
            // publica el simulador, y el log imprimía «🎨 Pintura: ATR». La v0.9.16 solo miraba
            // «sin dígitos», y ATR no lleva ninguno.
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("ATR 72-600 Passenger"));

            // La misma clase de fallo para el resto de fabricantes de 3–4 letras en mayúsculas.
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("BOEING 737-800"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("AIRBUS A320"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("EMBRAER E195"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("CESSNA 172 Skyhawk"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("PIPER PA-28"));
        }

        [TestMethod]
        public void FromTitle_TheModelOrTheIcaoDesignatorIsNotALivery()
        {
            // El modelo y el designador ICAO llegan desde `FsuipcService`; un título que los escriba
            // sueltos no puede convertirlos en pintura. Con `AT76` de ICAO, `ATR 72-600` no deja
            // ningún token creíble.
            Assert.AreEqual("Unknown",
                AircraftLivery.FromTitle("ATR 72-600 Passenger", "ATR 72-600", "AT76"));
            Assert.AreEqual("Unknown",
                AircraftLivery.FromTitle("B738 B77L", "737-800", "B77L"),
                "un token idéntico al ICAO del avión no es una pintura");
            Assert.AreEqual("AAL",
                AircraftLivery.FromTitle("B738 AAL", "737-800", "B738"),
                "la pintura real del título sigue saliendo");
        }

        [TestMethod]
        public void FromTitle_WithAManufacturerAndAnAirline_ReturnsTheAirline()
        {
            // La aerolínea manda sobre el fabricante: en el título del ATR con librea real, lo que
            // se pinta es la aerolínea, no el aviso de que el fabricante no es pintura.
            Assert.AreEqual("Avianca", AircraftLivery.FromTitle("ATR 72-600 Avianca"));
            Assert.AreEqual("LATAM",   AircraftLivery.FromTitle("Airbus A320 LATAM"));
        }

        // Nota: la pintura por matrícula (leer el `livery.cfg` del simulador) se implementó y se
        // **retiró** en v0.9.16 por decisión del mantenedor —«no quiero que escanee mi carpeta de
        // Community; si no se puede por FSUIPC, prefiero no tener esa exactitud»—. Por eso no hay
        // tests de eso: el código ya no está. Ver el CHANGELOG.
    }
}
