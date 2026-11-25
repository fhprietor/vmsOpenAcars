using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El «🎨 Pintura: …» del log. Tres addons distintos rompieron la misma heurística de «primer
    /// token plausible»: `777-200LR` → «Pintura: 777» (v0.9.16), `ATR 72-600 Passenger` →
    /// «Pintura: ATR» (v0.9.67) y, en el PIREP real `l5QVxBEmJ4yOaY2O`, `737-800 PAX SSW HD` →
    /// «Pintura: PAX» (v0.9.79). La regla que queda: **solo se pinta una aerolínea reconocible
    /// escrita en el título** (o el código de aerolínea del plan, si el título también lo escribe).
    /// Si no hay evidencia, no hay línea. Estos tests fijan que los tres títulos reales ya no pintan
    /// nada y que una aerolínea de verdad sí se devuelve.
    /// </summary>
    [TestClass]
    public class AircraftLiveryTests
    {
        [TestMethod]
        public void LosTresTitulosQueFallaron_NoPintanNada()
        {
            // Literales de los tres casos, con el modelo/ICAO que publica el simulador.
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("737-800 PAX SSW HD"),
                "PAX es una etiqueta de cabina, no una pintura (v0.9.79)");
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("777-200LR"),
                "el modelo no es la pintura (v0.9.16)");
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("ATR 72-600 Passenger"),
                "el fabricante no es la pintura (v0.9.67)");
        }

        [TestMethod]
        public void FromTitle_AModelOnlyTitle_IsNotALivery()
        {
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
            // Aerolíneas reales de la lista de la app.
            Assert.AreEqual("British", AircraftLivery.FromTitle("PMDG 777-200LR British Airways"));
            Assert.AreEqual("Avianca", AircraftLivery.FromTitle("Fenix A320 CFM Avianca"));
            Assert.AreEqual("VHR", AircraftLivery.FromTitle("Airbus A320neo VHR"));
            Assert.AreEqual("LATAM", AircraftLivery.FromTitle("iniBuilds A320 LATAM"));
        }

        [TestMethod]
        public void FromTitle_SinAerolineaEnElTitulo_NoHayPinturaAunqueHayaToken()
        {
            // Ya no se devuelve «el primer token plausible»: AAL o SAS sueltos no son evidencia de
            // una librea. Es el fallo que se repitió tres veces con listas de exclusión.
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("B738 AAL"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("ToLiss A321 SAS"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("B738 B77L"), "dos modelos no son pintura");
        }

        [TestMethod]
        public void ElCodigoDeAerolineaDelPlan_SoloCuentaSiElTituloLoTambienLoEscribe()
        {
            // `SimbriefPlan.Airline` es un código ICAO (AVA, AAL…). La pintura es lo que el avión
            // dice llevar: sin el token en el título no se inventa una librea.
            Assert.AreEqual("AAL", AircraftLivery.FromTitle("B738 AAL", "AAL"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("737-800 PAX SSW HD", "AVA"),
                "el plan no convierte una etiqueta de cabina en pintura");

            // Comparación por token: AVA no puede casar dentro de Avanti.
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("B738 Avanti", "AVA"));
        }

        [TestMethod]
        public void FromTitle_TheManufacturerIsNeverTheLivery()
        {
            // La misma clase de fallo para el resto de fabricantes que la lista antigua no cubría.
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("BOEING 737-800"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("AIRBUS A320"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("EMBRAER E195"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("CESSNA 172 Skyhawk"));
            Assert.AreEqual("Unknown", AircraftLivery.FromTitle("PIPER PA-28"));
        }

        [TestMethod]
        public void FromTitle_WithAManufacturerAndAnAirline_ReturnsTheAirline()
        {
            // La aerolínea manda sobre el fabricante: en el título del ATR con librea real, lo que
            // se pinta es la aerolínea.
            Assert.AreEqual("Avianca", AircraftLivery.FromTitle("ATR 72-600 Avianca"));
            Assert.AreEqual("LATAM",   AircraftLivery.FromTitle("Airbus A320 LATAM"));
        }

        // Nota: la pintura por matrícula (leer el `livery.cfg` del simulador) se implementó y se
        // **retiró** en v0.9.16 por decisión del mantenedor —«no quiero que escanee mi carpeta de
        // Community; si no se puede por FSUIPC, prefiero no tener esa exactitud»—. Por eso no hay
        // tests de eso: el código ya no está. Ver el CHANGELOG.
    }
}
