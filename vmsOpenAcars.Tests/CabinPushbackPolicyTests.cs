using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Los cinco disparadores del PA de pushback, con los tiempos del vuelo real**
    /// `3b0oBlXD5ZolKPM4` (AT76, SKUC→SKBO, 05/11/2026): freno liberado **01:57:06**, voz **01:57:09**
    /// (2,93 s de chime), beacon y `── PUSHBACK ──` **01:57:19** — 13 s después del freno.
    ///
    /// Deciden el orden `CabinPushbackPolicy.Decide` y el armado del disparador de puertas
    /// `ArmOnOpenDoors`; no hay reloj ni red, el estado entra por parámetro.
    /// </summary>
    [TestClass]
    public class CabinPushbackPolicyTests
    {
        /// <summary>El avión en el puesto antes de la primera señal: freno puesto, beacon apagado.</summary>
        private static CabinPushbackState EnElPuesto()
            => new CabinPushbackState
            {
                Announced          = false,
                BeaconOn           = false,
                ParkingBrakeSet    = true,
                ParkingBrakeWasSet = true,
                DoorsSeenOpen      = false,
            };

        [TestMethod]
        public void ElFrenoLiberadoDelVueloRealDisparaElAviso()
        {
            CabinPushbackState state = EnElPuesto();

            // Freno puesto del puesto: todavía no hay señal que anuncie nada.
            Assert.AreEqual(CabinPushbackTrigger.None,
                CabinPushbackPolicy.Decide(FlightPhase.Boarding, state, allDoorsClosed: true));

            // 01:57:06 — Parking Brake: RELEASED.
            state.ParkingBrakeSet = false;
            Assert.AreEqual(CabinPushbackTrigger.ParkingBrakeReleased,
                CabinPushbackPolicy.Decide(FlightPhase.Boarding, state, allDoorsClosed: true),
                "el freno se acaba de soltar con el avión aún en el puesto: es el momento");

            // 01:57:19 — beacon ON, 13 s después. El aviso ya se dijo y no se repite.
            state.BeaconOn = true;
            state.Announced = true;
            Assert.AreEqual(CabinPushbackTrigger.None,
                CabinPushbackPolicy.Decide(FlightPhase.Boarding, state, allDoorsClosed: true));
        }

        [TestMethod]
        public void ElBeaconConElFrenoPuestoDisparaElAvisoAntes()
        {
            // Requisito del mantenedor: si enciende el beacon antes de soltar el freno, eso ya
            // anuncia el cierre de puertas. No hace falta esperar al freno.
            CabinPushbackState state = EnElPuesto();
            state.BeaconOn = true;

            Assert.AreEqual(CabinPushbackTrigger.BeaconWithParkingBrakeSet,
                CabinPushbackPolicy.Decide(FlightPhase.Boarding, state, allDoorsClosed: true));
        }

        [TestMethod]
        public void LasPuertasAbiertasYCerradasDisparanElAviso()
        {
            CabinPushbackState state = EnElPuesto();

            // Se ve una puerta abierta en algún momento del vuelo: el disparador queda armado.
            Assert.IsTrue(CabinPushbackPolicy.ArmOnOpenDoors(alreadyArmed: false, anyExitOpen: true));
            state.DoorsSeenOpen = true;

            // Con la puerta aún abierta no se anuncia nada.
            Assert.AreEqual(CabinPushbackTrigger.None,
                CabinPushbackPolicy.Decide(FlightPhase.Boarding, state, allDoorsClosed: false));

            // Se cierran todas: es la señal literal del PA.
            Assert.AreEqual(CabinPushbackTrigger.DoorsClosed,
                CabinPushbackPolicy.Decide(FlightPhase.Boarding, state, allDoorsClosed: true));
        }

        [TestMethod]
        public void SiNuncaSeVioUnaPuertaAbiertaElDisparadorDePuertasNoDispara()
        {
            // `0x3367` no se ha podido verificar en simulador. Si el addon no lo publica y devuelve
            // siempre 0, nunca se arma; si devuelve siempre lo mismo distinto de 0, nunca llega a
            // «todas cerradas». En los dos casos deciden los otros cuatro disparadores.
            CabinPushbackState state = EnElPuesto();
            Assert.IsFalse(CabinPushbackPolicy.ArmOnOpenDoors(alreadyArmed: false, anyExitOpen: false),
                "sin haber visto una puerta abierta, «todas cerradas» no prueba nada");

            Assert.AreEqual(CabinPushbackTrigger.None,
                CabinPushbackPolicy.Decide(FlightPhase.Boarding, state, allDoorsClosed: true),
                "el disparador de puertas no puede disparar si nunca se armó");
        }

        [TestMethod]
        public void ElAvisoDePushbackSoloSeDiceUnaVezPorVuelo()
        {
            CabinPushbackState state = EnElPuesto();
            state.Announced = true;

            // Aunque lleguen todas las señales a la vez, ya se dijo.
            state.BeaconOn           = true;
            state.ParkingBrakeSet    = false;
            state.DoorsSeenOpen      = true;

            Assert.AreEqual(CabinPushbackTrigger.None,
                CabinPushbackPolicy.Decide(FlightPhase.Boarding, state, allDoorsClosed: true));
            Assert.AreEqual(CabinPushbackTrigger.None,
                CabinPushbackPolicy.Decide(FlightPhase.Pushback, state, allDoorsClosed: true));
        }

        [TestMethod]
        public void EnElRodajeYaNoSeAnunciaElPushback()
        {
            CabinPushbackState state = EnElPuesto();
            state.BeaconOn = true;
            Assert.AreEqual(CabinPushbackTrigger.None,
                CabinPushbackPolicy.Decide(FlightPhase.TaxiOut, state, allDoorsClosed: true),
                "en TaxiOut el avión ya rueda: el PA llegaría tarde");
        }
    }
}
