using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La fase de vuelo que ya no se corresponde con el avión**, con la traza real del caso que lo
    /// obliga: el BE58 del PIREP **`RBmrBApLOjWlYmO2`** (SKSJ→SKSV, `vmsOpenACars/0.9.38`, UTC del
    /// 14/10/2026).
    ///
    /// Después de la toma (03:35:57, −104 fpm) el touch-and-go se declaró **5 s más tarde** —GS 88 kt—
    /// y la fase saltó a `Climb`. Nunca volvió: de **03:36:18 a 03:39:19** el cliente siguió en `ICL`
    /// con el avión **rodando y aparcado**, y el `msl` del propio registro lo demuestra —
    /// **880, 880, 880, 879, 877, 876, 875, 874, 873, 873, 874, 875**—, es decir, siempre a la altura
    /// del campo, variando ocho pies por el perfil de la calle. Las velocidades de esa racha están
    /// abajo, sample a sample.
    ///
    /// Como `OnBlock` sólo se alcanza desde `TaxiIn`, el piloto se quedó **sin poder enviar el PIREP**
    /// (sólo cancelar, que lo borra) hasta que un administrador lo cerró a mano.
    /// </summary>
    [TestClass]
    public class StuckAirPhasePolicyTests
    {
        // ══ La racha real, del registro del PIREP (gs kt, vs fpm) ═════════════
        [DataTestMethod]
        [DataRow(32, 7)]
        [DataRow(11, -2)]
        [DataRow(7,  -1)]
        [DataRow(16, 0)]
        [DataRow(21, -5)]
        [DataRow(19, -8)]
        [DataRow(18, -1)]
        [DataRow(17, -3)]
        [DataRow(15, -6)]
        [DataRow(6,  0)]
        [DataRow(0,  0)]
        public void CadaMuestraDeLaRachaReal_SeDetecta(int groundSpeedKt, int verticalSpeedFpm)
        {
            Assert.IsTrue(
                StuckAirPhasePolicy.IsStuck(FlightPhase.Climb, groundSpeedKt, verticalSpeedFpm),
                $"en Climb, {groundSpeedKt} kt y {verticalSpeedFpm} fpm es el avión rodando");
        }

        /// <summary>
        /// La primera muestra de la racha en la que el avión **todavía corría** (03:36:18 es la de
        /// 88 kt… y la anterior, a 88 kt, es de la propia carrera de aterrizaje): a esa velocidad en
        /// tierra no se puede distinguir de un despegue de verdad, y la regla no lo intenta.
        /// </summary>
        [TestMethod]
        public void MientrasCorrePorLaPista_NoSeDeclaraNada()
        {
            Assert.IsFalse(StuckAirPhasePolicy.IsStuck(FlightPhase.Climb, 88, -178),
                "a 88 kt se puede estar despegando: no es un avión parado");
            Assert.IsFalse(StuckAirPhasePolicy.IsStuck(FlightPhase.Takeoff, 60, -178));
        }

        /// <summary>
        /// Rodar despacio sólo es sospechoso **en una fase de vuelo**: en las de suelo es lo normal, y
        /// confundirlas sería avisar en todos los rodajes del mundo.
        /// </summary>
        [DataTestMethod]
        [DataRow(FlightPhase.TaxiOut)]
        [DataRow(FlightPhase.TaxiIn)]
        [DataRow(FlightPhase.AfterLanding)]
        [DataRow(FlightPhase.TakeoffRoll)]
        [DataRow(FlightPhase.OnBlock)]
        [DataRow(FlightPhase.Boarding)]
        [DataRow(FlightPhase.Pushback)]
        public void RodarDespacioEnUnaFaseDeSuelo_EsLoNormal(FlightPhase phase)
        {
            Assert.IsFalse(StuckAirPhasePolicy.IsStuck(phase, 8, 0),
                $"{phase} es una fase de suelo: rodar despacio es exactamente lo que toca");
        }

        [DataTestMethod]
        [DataRow(FlightPhase.Takeoff)]
        [DataRow(FlightPhase.Climb)]
        [DataRow(FlightPhase.Enroute)]
        [DataRow(FlightPhase.Descent)]
        public void LasCuatroFasesDeVuelo_SonLasQueSeVigilan(FlightPhase phase)
        {
            Assert.IsTrue(StuckAirPhasePolicy.IsAirPhase(phase));
            Assert.IsTrue(StuckAirPhasePolicy.IsStuck(phase, 8, 0));
        }

        /// <summary>
        /// Un avión lento pero **volando** no cuenta: si está subiendo o bajando de verdad, la fase es
        /// correcta aunque la velocidad en tierra sea baja.
        /// </summary>
        [TestMethod]
        public void UnAvionLentoPeroSubiendoDeVerdad_NoCuenta()
        {
            Assert.IsFalse(StuckAirPhasePolicy.IsStuck(FlightPhase.Climb, 35, 700),
                "subiendo a 700 fpm no está parado, por despacio que vaya");
            Assert.IsFalse(StuckAirPhasePolicy.IsStuck(FlightPhase.Descent, 38, -900),
                "y bajando a −900 fpm, tampoco");
        }

        /// <summary>
        /// **La evidencia de suelo, que es la que no depende del simulador.** Es la mitad que faltaba:
        /// la bandera de «en el suelo» fue la que falló en el vuelo real —publicó «en el aire» con el
        /// avión aparcado— y por eso la máquina necesita poder decidirlo por lo que el avión hace.
        /// </summary>
        [DataTestMethod]
        [DataRow(0,    0,  true,  "aparcado")]
        [DataRow(6,    5,  true,  "rodando despacio")]
        [DataRow(21,  -5,  true,  "rodando con el perfil de la calle")]
        [DataRow(39,   0,  true,  "justo por debajo del límite")]
        [DataRow(40,   0,  false, "en el límite ya no: 40 kt es velocidad de vuelo")]
        [DataRow(100, -20, false, "rodando rápido por la pista")]
        [DataRow(250, 1500, false, "volando")]
        [DataRow(120, -900, false, "bajando de verdad: una pérdida no es estar en el suelo")]
        public void LaEvidenciaDeSuelo_NoDependeDeLaBanderaDelSimulador(
            int groundSpeed, int vs, bool expected, string porque)
        {
            Assert.AreEqual(expected, StuckAirPhasePolicy.LooksOnGround(groundSpeed, vs), porque);
        }

        /// <summary>
        /// Y las dos piezas encajan: la fase atascada es **fase de vuelo + evidencia de suelo**, sin
        /// repetir la condición en dos sitios que se puedan separar.
        /// </summary>
        [TestMethod]
        public void LaFaseAtascada_EsFaseDeVueloConEvidenciaDeSuelo()
        {
            foreach (var phase in new[] { FlightPhase.Takeoff, FlightPhase.Climb,
                                          FlightPhase.Enroute, FlightPhase.Descent })
                Assert.IsTrue(StuckAirPhasePolicy.IsStuck(phase, 8, 0));

            foreach (var phase in new[] { FlightPhase.TaxiOut, FlightPhase.TaxiIn,
                                          FlightPhase.AfterLanding, FlightPhase.OnBlock })
                Assert.IsFalse(StuckAirPhasePolicy.IsStuck(phase, 8, 0),
                    "rodar despacio en una fase de suelo es lo normal");

            Assert.IsFalse(StuckAirPhasePolicy.IsStuck(FlightPhase.Climb, 250, 1500),
                "volando, la fase de vuelo es la correcta");
        }

        /// <summary>
        /// La racha real tardó **15 s** en ser concluyente: de 03:36:18 (32 kt) a 03:36:33 (11 kt). El
        /// umbral existe para no reaccionar a un rebote ni a un dato suelto, pero no tanto como para
        /// dejar pasar un avión aparcado.
        /// </summary>
        [TestMethod]
        public void ElAvisoNecesitaQuinceSegundosSostenidos()
        {
            Assert.IsFalse(StuckAirPhasePolicy.ShouldWarn(0.0));
            Assert.IsFalse(StuckAirPhasePolicy.ShouldWarn(14.9), "un rebote no llega aquí");
            Assert.IsTrue(StuckAirPhasePolicy.ShouldWarn(15.0),
                "a los 15 s de la racha real (03:36:18 → 03:36:33) ya se puede decir");
            Assert.IsTrue(StuckAirPhasePolicy.ShouldWarn(163.0),
                "y la racha real duró casi tres minutos sin que nadie dijera nada");
        }
    }
}
