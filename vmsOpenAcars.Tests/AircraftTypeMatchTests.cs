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

        // ── De dónde salió el designador: X-Plane no es MSFS ────────────────────────────────
        //
        // El caso real (medido el 20/10/2026 con X-Plane 12.4.3 + XPUIPC 2.0.3.8 y el ToLiss A320neo
        // cargado): el modelo ATC (0x0618) llega **vacío**, y el 0x3D00 —el «título»— es el nombre de
        // la CARPETA, `ToLissA320_V1p2p1`. De ahí se deducía `A320`, y el A20N de verdad está en el
        // `_ICAO` del `.acf` del avión. Con el designador deducido, el avión correcto avisaba de
        // discrepancia contra su propio OFP (un A20N) y además se perdía la comparación exacta.

        [TestMethod]
        public void ConElTipoLeidoDelAcf_MandaElAcfYNoElNombreDeLaCarpeta()
        {
            // El `.acf` dice A20N y la carpeta dice A320: el avión es el del OFP, sin aviso.
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("A20N", "", "ToLissA320_V1p2p1", "A20N",
                                                           AircraftTypeSource.AcfFile));

            // Un título que sí dijera «neo» tampoco cambia nada: el `.acf` es la ficha del avión.
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("A20N", "", "Airbus A320neo", "A20N",
                                                           AircraftTypeSource.AcfFile));

            // Y la puerta que importa sigue cerrada: el OFP de un CEO no es el de un NEO.
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("A20N", "", "ToLissA320_V1p2p1", "A320",
                                                            AircraftTypeSource.AcfFile),
                           "un plan de A320 en un A320neo");
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("A320", "", "ToLissA320_V1p2p1", "A20N",
                                                            AircraftTypeSource.AcfFile),
                           "un plan de A320neo en un CEO");

            // Y otra familia sigue avisando.
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("A20N", "", "ToLissA320_V1p2p1", "B738",
                                                            AircraftTypeSource.AcfFile));
        }

        [TestMethod]
        public void SinDatoDelSimulador_ElTipoDeducidoSoloComparaPorFamilia()
        {
            // Si el `.acf` no se pudo leer, el «A320» es una suposición sobre el nombre de la carpeta:
            // no distingue CEO de NEO, así que no puede acusar a ninguno de los dos. Y ojo con la
            // trampa que destapó esto: `A320` empieza por `A32` y `A20N` por `A20`, así que la
            // familia de tres caracteres a secas los daba por aviones distintos (ver `FamilyKey`).
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("A320", "", "ToLissA320_V1p2p1", "A20N",
                                                           AircraftTypeSource.TitleGuess));
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("A320", "", "ToLissA320_V1p2p1", "A321",
                                                           AircraftTypeSource.TitleGuess));
            Assert.IsTrue(AircraftTypeMatch.IsSameAircraft("A320", "", "ToLissA320_V1p2p1", "A21N",
                                                           AircraftTypeSource.TitleGuess));

            // Pero un avión de otra familia sí avisa, que es lo que el piloto agradece saber.
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("A320", "", "ToLissA320_V1p2p1", "B738",
                                                            AircraftTypeSource.TitleGuess));
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("A320", "", "ToLissA320_V1p2p1", "A333",
                                                            AircraftTypeSource.TitleGuess));
        }

        [TestMethod]
        public void LaFamiliaA320IncluyeAlCeoYAlNeo()
        {
            // El CEO y el NEO de Airbus no comparten los tres primeros caracteres: A320 → A32 y
            // A20N → A20. La familia tiene que agruparlos, o la degradación honesta del cliente no
            // funciona justo con el avión que la motivó.
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("A320", "A20N"));
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("A20N", "A320"));
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("A319", "A320"), "A319 → A31, A320 → A32");
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("A321", "A21N"));
            Assert.IsTrue(AircraftTypeMatch.IsSameFamily("A19N", "A320"));

            // Y no se lleva por delante las familias de verdad distintas.
            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("A320", "A330"));
            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("A20N", "A350"));
            Assert.IsFalse(AircraftTypeMatch.IsSameFamily("A320", "B738"));

            // El aviso exacto sigue en pie: dos variantes conocidas se comparan exactas.
            Assert.IsFalse(AircraftTypeMatch.IsSameAircraft("A20N", "", "", "A320",
                                                            AircraftTypeSource.AcfFile));
        }

        [TestMethod]
        public void ElDesignadorQueSePublica_DependeDelOrigen()
        {
            // Publicado por el ATC: la variante que se resuelve del modelo/título (como siempre).
            Assert.AreEqual("B77L", AircraftTypeMatch.DesignatorFor(AircraftTypeSource.AtcModel,
                                        "777-200LR", "PMDG 777-200LR", "B777"));
            // Del `.acf`: él, tal cual, sin que el título lo afine.
            Assert.AreEqual("A20N", AircraftTypeMatch.DesignatorFor(AircraftTypeSource.AcfFile,
                                        "", "ToLissA320_V1p2p1", "A20N"));
            // Deducido: se publica el deducido (el log lo marca), y no se afina con el título.
            Assert.AreEqual("A320", AircraftTypeMatch.DesignatorFor(AircraftTypeSource.TitleGuess,
                                        "", "ToLissA320_V1p2p1", "A320"));
            // Sin dato: vacío, para que el que llama decida qué calla.
            Assert.AreEqual("", AircraftTypeMatch.DesignatorFor(AircraftTypeSource.AcfFile, "", "", "????"));
        }
    }
}
