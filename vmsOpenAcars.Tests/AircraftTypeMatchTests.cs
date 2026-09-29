using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Discrepancia de aeronave entre lo que vuela el simulador y lo que trae el OFP.
    ///
    /// El caso que originó esto es real y lo reportó el mantenedor: PMDG 777-200LR en MSFS 2024,
    /// el simulador publica el **modelo ATC** `B777` (familia) y el OFP de SimBrief el **tipo
    /// ICAO** `B77L` (variante). Comparados con igualdad exacta, el aviso saltaba con el avión
    /// correcto y bloqueaba el START hasta confirmarlo (v0.9.16).
    /// </summary>
    [TestClass]
    public class AircraftTypeMatchTests
    {
        [TestMethod]
        public void SameFamily_AcceptsTheSimulatorAtcModelAgainstTheIcaoTypeOfTheOfp()
        {
            // El modelo ATC de familia que reporta el simulador contra el designador ICAO de
            // variante que publica SimBrief: los cinco casos que produce cada familia Boeing.
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("B777", "B77L"), "777-200LR, el reportado");
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("B777", "B77W"), "777-300ER");
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("B737", "B738"), "737-800");
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("B747", "B748"), "747-8");
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("B787", "B789"), "787-9");

            // Y entre variantes de la misma familia, sin pasar por el modelo de familia.
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("B77L", "B77W"));
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("A320", "A321"));
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("b77l", " B77L "), "caja y espacios no cuentan");
        }

        [TestMethod]
        public void SameFamily_RejectsADifferentAircraft()
        {
            // Esta es la puerta que importa y que el aviso tiene que seguir cazando: el OFP es de
            // otro avión, no de otra variante.
            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("A320", "B738"));
            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("B77L", "A333"));
            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("B738", "AT76"));
            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("B772", "B38M"), "777 contra 737 MAX");
        }

        [TestMethod]
        public void SameFamily_WithoutData_DoesNotBlockAnything()
        {
            // Degradar sin datos: si falta el dato no hay discrepancia que anunciar ni ventana
            // que abrir. `????` es el centinela del cliente.
            Assert.IsFalse(AircraftTypeMatch.IsKnown("????"));
            Assert.IsFalse(AircraftTypeMatch.IsKnown(""));
            Assert.IsFalse(AircraftTypeMatch.IsKnown("   "));
            Assert.IsTrue(AircraftTypeMatch.IsKnown("b77l"));

            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("????", "B77L"));
            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("B77L", "????"));
        }

        [TestMethod]
        public void SameFamily_ShortCodes_RequireExactEquality()
        {
            // Con menos de tres caracteres no hay familia que comparar: `B58` (Baron) es un
            // designador completo, no el prefijo de nada.
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("B58", "b58"));
            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("B58", "C172"));
        }

        // ── La variante, sacada del modelo/título del simulador ─────────────────────────────

        [TestMethod]
        public void VariantFromText_ResolvesTheRealModelStrings()
        {
            // Lo que el mantenedor ve en su log: «✈️ Aeronave: 777-200LR» y «📋 ICAO: B777».
            Assert.AreEqual("B77L", AircraftTypeMatch.VariantFromText("777-200LR"));
            Assert.AreEqual("B77L", AircraftTypeMatch.VariantFromText("PMDG 777-200LR British Airways"));
            Assert.AreEqual("B77W", AircraftTypeMatch.VariantFromText("777-300ER"));
            Assert.AreEqual("B772", AircraftTypeMatch.VariantFromText("777-200ER"));
            Assert.AreEqual("B772", AircraftTypeMatch.VariantFromText("777-200"));
            Assert.AreEqual("B773", AircraftTypeMatch.VariantFromText("777-300"));
            Assert.AreEqual("B77F", AircraftTypeMatch.VariantFromText("777F"));
            Assert.AreEqual("B738", AircraftTypeMatch.VariantFromText("737-800"));
            Assert.AreEqual("B738", AircraftTypeMatch.VariantFromText("Boeing 737-800 NGX"));
            Assert.AreEqual("B38M", AircraftTypeMatch.VariantFromText("737 MAX 8"));
            Assert.AreEqual("B789", AircraftTypeMatch.VariantFromText("787-9"));
            Assert.AreEqual("B78X", AircraftTypeMatch.VariantFromText("787-10"));
            Assert.AreEqual("B748", AircraftTypeMatch.VariantFromText("747-8"));
            Assert.AreEqual("A320", AircraftTypeMatch.VariantFromText("Fenix A320 CFM"));
            Assert.AreEqual("A20N", AircraftTypeMatch.VariantFromText("A320neo"));
            Assert.AreEqual("A21N", AircraftTypeMatch.VariantFromText("Airbus A321neo"));

            // Lo que no reconoce devuelve vacío: el que llama cae a la familia, no inventa.
            Assert.AreEqual("", AircraftTypeMatch.VariantFromText(""));
            Assert.AreEqual("", AircraftTypeMatch.VariantFromText("????"));
            Assert.AreEqual("", AircraftTypeMatch.VariantFromText("Avión genérico"));
        }

        [TestMethod]
        public void SameAircraft_WithTheVariantKnown_DoesNotAcceptAnotherVersion()
        {
            // El caso reportado: 777-200LR en el simulador (modelo ATC B777) y OFP de B77L.
            // Ahora la variante SÍ se resuelve, así que se compara exacto y no avisa.
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("B777", "777-200LR", "PMDG 777-200LR", "B77L"));

            // Y lo que pidió el mantenedor: dos versiones distintas del 777 NO se dan por buenas.
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("B777", "777-200LR", "", "B77W"),
                           "un plan de 777-300ER en un 777-200LR");
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("B777", "777-300ER", "", "B77L"));
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("B777", "777-200ER", "", "B77L"),
                           "777-200ER contra 777-200LR");
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("B738", "737-800", "", "B39M"),
                           "737-800 contra 737 MAX 9");
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("A320", "A320", "", "A20N"),
                           "A320 contra A320neo");

            // Si el OFP no trae un designador de variante sino uno de familia, no se puede
            // comparar exacto: se cae a la familia y no se avisa en falso.
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("B777", "777-200LR", "", "B777"));
        }

        [TestMethod]
        public void SameAircraft_WithoutTheVariant_FallsBackToTheFamily()
        {
            // Un addon que solo publica el modelo ATC de familia: sin variante a la vista, la
            // comparación es por familia y el avión correcto no se marca (el falso positivo que
            // originó v0.9.16).
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("B777", "", "", "B77L"));
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("B777", "????", "????", "B77W"));
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("B737", "", "", "B738"));

            // Pero un avión de otra familia sigue avisando aunque falte la variante.
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("A320", "", "", "B738"));
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("B777", "", "", "A333"));

            // Y sin dato en el simulador no se bloquea nada.
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("????", "777-200LR", "", "B77L"));
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("", "777-200LR", "", "B77L"));
        }

        [TestMethod]
        public void ResolveVariant_AcceptsTheAtcModelWhenItIsAlreadyAVariant()
        {
            Assert.AreEqual("B77L", AircraftTypeMatch.ResolveVariant("", "", "B77L"));
            Assert.AreEqual("B77L", AircraftTypeMatch.ResolveVariant("", "PMDG 777-200LR", "B777"));
            Assert.AreEqual("B77L", AircraftTypeMatch.ResolveVariant("777-200LR", "otra cosa", "B777"));
            Assert.AreEqual("", AircraftTypeMatch.ResolveVariant("", "", "B777"),
                            "B777 es familia, no variante");
            Assert.IsTrue(AircraftTypeMatch.IsVariantDesignator("b77l"));
            Assert.IsFalse(AircraftTypeMatch.IsVariantDesignator("B777"));
        }
    }
}
