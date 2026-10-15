using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La bandera de suelo del simulador se confirma en las dos direcciones.**
    ///
    /// El propio código ya sabía la mitad: junto a las constantes de detección está escrito que *«el sim
    /// reporta OnGround=0 brevemente tras el primer contacto»*, y por eso el **toque** tiene un debounce
    /// de 10 s que evita registrarlo dos veces. Lo que no había era nada que impidiera dar por hecho el
    /// **despegue** con ese mismo parpadeo: `HandleTakeoff` lo descarta por su cuenta —ignora cualquier
    /// liftoff dentro de los 30 s de un toque— pero **`_lastOnGround` sí se ponía en falso**, y esa
    /// bandera es la que viaja hasta la máquina de fases.
    ///
    /// De ahí salió el touch-and-go inexistente del PIREP real **`RBmrBApLOjWlYmO2`**: a las **03:36:02**,
    /// cinco segundos después de tomar y a **88 kt**, la máquina creyó que el avión estaba en el aire y
    /// se fue a `Climb` para no volver, dejando al piloto sin poder enviar el PIREP.
    ///
    /// Los tres frames son **150 ms** al sondeo de 20 Hz (`polling_interval_ms = 50`): imperceptible
    /// para un registro de toque, suficiente para que un parpadeo no mueva nada.
    /// </summary>
    [TestClass]
    public class GroundContactGateTests
    {
        private static GroundContactGate InTheAir()
        {
            var gate = new GroundContactGate();
            for (int i = 0; i < GroundContactGate.ConfirmFrames; i++) gate.Update(false);
            Assert.IsFalse(gate.IsOnGround);
            return gate;
        }

        /// <summary>El caso real: en tierra, un frame en falso y de vuelta. No pasa nada.</summary>
        [TestMethod]
        public void ElParpadeoDeUnFrameDelCasoReal_NoDeclaraElDespegue()
        {
            var gate = new GroundContactGate();   // arranca en tierra

            Assert.AreEqual(GroundContactStep.None, gate.Update(false), "un frame no es una medida");
            Assert.IsTrue(gate.IsOnGround, "el avión sigue en el suelo");

            Assert.AreEqual(GroundContactStep.None, gate.Update(true));
            Assert.IsTrue(gate.IsOnGround);

            // Y aunque el parpadeo se repita, cada uno se apaga solo al volver la bandera.
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(GroundContactStep.None, gate.Update(false));
                Assert.AreEqual(GroundContactStep.None, gate.Update(true));
            }
            Assert.IsTrue(gate.IsOnGround);
        }

        [TestMethod]
        public void DosFramesTampocoBastan_PeroTresSi()
        {
            var gate = new GroundContactGate();

            Assert.AreEqual(GroundContactStep.None, gate.Update(false));
            Assert.AreEqual(GroundContactStep.None, gate.Update(false));
            Assert.IsTrue(gate.IsOnGround, "dos frames de 50 ms siguen siendo un parpadeo");

            Assert.AreEqual(GroundContactStep.Liftoff, gate.Update(false),
                "el tercero confirma el despegue");
            Assert.IsFalse(gate.IsOnGround);
        }

        /// <summary>Un despegue de verdad se declara **una sola vez**, no en cada frame que sigue.</summary>
        [TestMethod]
        public void ElDespegueSeDeclaraUnaSolaVez()
        {
            var gate = new GroundContactGate();

            int liftoffs = 0;
            for (int i = 0; i < 200; i++)
                if (gate.Update(false) == GroundContactStep.Liftoff) liftoffs++;

            Assert.AreEqual(1, liftoffs);
            Assert.IsFalse(gate.IsOnGround);
        }

        /// <summary>El toque también se confirma: un frame en tierra no registra un aterrizaje.</summary>
        [TestMethod]
        public void ElToqueSeConfirmaIgual()
        {
            var gate = InTheAir();

            Assert.AreEqual(GroundContactStep.None, gate.Update(true), "un frame no registra un toque");
            Assert.AreEqual(GroundContactStep.None, gate.Update(false), "y al volver la bandera, se apaga");
            Assert.AreEqual(GroundContactStep.None, gate.Update(true));
            Assert.AreEqual(GroundContactStep.None, gate.Update(true));
            Assert.AreEqual(GroundContactStep.Touchdown, gate.Update(true));
            Assert.IsTrue(gate.IsOnGround);

            // Y una vez en tierra, seguir en tierra no vuelve a declarar nada.
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(GroundContactStep.None, gate.Update(true));
        }

        /// <summary>
        /// **La secuencia completa del vuelo real**, que es lo que hay que dejar fijado: toque, parpadeos
        /// de la bandera en la carrera de aterrizaje y avión que sigue rodando. Ningún parpadeo corto
        /// mueve el estado, así que la máquina de fases no llega a enterarse de nada raro.
        /// </summary>
        [TestMethod]
        public void LaSecuenciaRealDelBE58_NoProduceNiDespegueNiSegundoToque()
        {
            var gate = InTheAir();

            // La toma, confirmada.
            GroundContactStep step = GroundContactStep.None;
            for (int i = 0; i < GroundContactGate.ConfirmFrames; i++) step = gate.Update(true);
            Assert.AreEqual(GroundContactStep.Touchdown, step);
            Assert.IsTrue(gate.IsOnGround);

            // La carrera de aterrizaje: el simulador publica «en el aire» a ratos (rachas de uno y dos
            // frames), con el avión rodando a 88 kt.
            var bandera = new[] { false, true, false, false, true, true, false, true, true, false, false,
                                  true, false, true };
            foreach (bool frame in bandera)
                Assert.AreEqual(GroundContactStep.None, gate.Update(frame),
                    "ningún parpadeo corto de la carrera puede mover el estado");

            Assert.IsTrue(gate.IsOnGround, "el avión nunca dejó de estar en el suelo");
        }

        /// <summary>
        /// **Hasta dónde llega esta compuerta, dicho en claro**: protege contra el parpadeo **corto**
        /// —que es lo que el propio código describe, «OnGround=0 brevemente tras el primer contacto»—,
        /// no contra una bandera que se quede en falso. Si el simulador sostiene tres frames o más, el
        /// despegue se declara.
        ///
        /// Para eso está la otra mitad: el reparto entre el camino del suelo y el del aire **no se fía
        /// sólo de esta bandera** (`StuckAirPhasePolicy.LooksOnGround`), así que una bandera atascada ya
        /// no puede dejar la fase sin salida. Las dos piezas son necesarias y ninguna basta sola.
        /// </summary>
        [TestMethod]
        public void UnaBanderaAtascada_SiDeclaraElDespegue_YPorEsoExisteLaEvidenciaDeSuelo()
        {
            var gate = new GroundContactGate();

            GroundContactStep step = GroundContactStep.None;
            for (int i = 0; i < GroundContactGate.ConfirmFrames; i++) step = gate.Update(false);

            Assert.AreEqual(GroundContactStep.Liftoff, step,
                "tres frames seguidos sí son una medida: la compuerta no adivina");
            Assert.IsFalse(gate.IsOnGround);
        }
    }
}
