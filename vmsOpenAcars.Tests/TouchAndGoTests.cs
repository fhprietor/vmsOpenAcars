using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El touch-and-go se decide con un ascenso sostenido, no con la velocidad.** Es la causa raíz del
    /// PIREP real **`RBmrBApLOjWlYmO2`** (SKSJ→SKSV, BE58 de Carenado en X-Plane,
    /// `vmsOpenACars/0.9.38`, UTC del 14/10/2026).
    ///
    /// La secuencia, del propio registro del vuelo: toque a las **03:35:57** con **96 kt** y
    /// **−104 fpm**; cinco segundos después el avión iba a **88 kt** y **−178 fpm** —en plena carrera de
    /// aterrizaje—. Con la condición que había (`GS > 60 && SecondsSinceTouchdown >= 5`) eso bastaba
    /// para declarar un touch-and-go inexistente, la fase saltó a `Climb` y **nunca volvió**: `OnBlock`
    /// sólo se alcanza desde `TaxiIn`, así que el piloto se quedó sin poder enviar el PIREP y lo único
    /// que le ofrecía la aplicación era cancelar, que lo borra.
    ///
    /// La velocidad sola no puede distinguir las dos cosas —un bimotor ligero rueda a 88 kt después de
    /// tomar igual que despega a 88 kt—, pero **la tasa vertical sí**: −178 fpm es un avión que rueda,
    /// y +900 fpm es un avión que se va.
    /// </summary>
    [TestClass]
    public class TouchAndGoTests
    {
        private sealed class TestClock
        {
            public DateTime Now;
            public TestClock(DateTime start) { Now = start; }
            public DateTime Read() => Now;
            public void At(DateTime t) { Now = t; }
        }

        private static PhaseInput Sample(bool onGround, int groundSpeed, int vs, double pitch,
                                         double secondsSinceTouchdown) => new PhaseInput
        {
            GroundSpeed           = groundSpeed,
            IsOnGround            = onGround,
            VerticalSpeed         = vs,
            Pitch                 = pitch,
            Altitude              = 872,
            Lat                   = 2.14946,
            Lon                   = -74.76327,
            Heading               = 313,
            Origin                = "SKSJ",
            Destination           = "SKSV",
            SecondsSinceTouchdown = secondsSinceTouchdown,
        };

        // ══ Las muestras reales del caso ══════════════════════════════════════
        // (GS, VS) tal cual salen del registro del PIREP: el toque y los tres ciclos siguientes.
        [DataTestMethod]
        [DataRow(96,  -104)]
        [DataRow(97,  -140)]
        [DataRow(88,  -178)]
        [DataRow(32,     7)]
        [DataRow(11,    -2)]
        public void LaCarreraDeAterrizajeDelCasoReal_NoEsUnTouchAndGo(int groundSpeed, int vs)
        {
            Assert.IsFalse(
                TouchAndGoPolicy.Indicates(FlightPhase.AfterLanding, isOnGround: false,
                                           groundSpeedKt: groundSpeed, verticalSpeedFpm: vs,
                                           secondsSinceTouchdown: 6.0),
                $"{groundSpeed} kt y {vs} fpm en carrera de aterrizaje no es un despegue");
        }

        [TestMethod]
        public void UnTouchAndGoDeVerdad_SeDeclara()
        {
            // El avión se va: 90 kt de GS subiendo a 900 fpm, tres segundos después del margen.
            Assert.IsTrue(TouchAndGoPolicy.Indicates(FlightPhase.AfterLanding, isOnGround: false,
                                                     groundSpeedKt: 90, verticalSpeedFpm: 900,
                                                     secondsSinceTouchdown: 12.0));
            Assert.IsTrue(TouchAndGoPolicy.Confirmed(3.0), "tres segundos sostenidos ya deciden");
            Assert.IsFalse(TouchAndGoPolicy.Confirmed(2.9), "y un pelo menos, no");
        }

        [DataTestMethod]
        [DataRow(FlightPhase.AfterLanding, false, 45,  900, 12.0, "rodando despacio")]
        [DataRow(FlightPhase.AfterLanding, false, 90,  900,  3.0, "antes del margen desde el toque")]
        [DataRow(FlightPhase.AfterLanding, true,  90,  900, 12.0, "en el suelo")]
        [DataRow(FlightPhase.AfterLanding, false, 90,   50, 12.0, "sin subir de verdad")]
        [DataRow(FlightPhase.AfterLanding, false, 90, -178, 12.0, "bajando")]
        [DataRow(FlightPhase.TaxiOut,      false, 90,  900, 12.0, "en rodaje de salida")]
        [DataRow(FlightPhase.Approach,     false, 90,  900, 12.0, "en aproximación")]
        public void CualquieraDeLasCondicionesQueFalte_LoDescarta(
            FlightPhase phase, bool onGround, int groundSpeed, int vs, double secondsSinceTouchdown,
            string porque)
        {
            Assert.IsFalse(TouchAndGoPolicy.Indicates(phase, onGround, groundSpeed, vs, secondsSinceTouchdown),
                $"no puede declararse: {porque}");
        }

        /// <summary>
        /// **La secuencia real contra la máquina de fases**, que es donde estaba el daño: se toma, el
        /// simulador publica un contacto con el suelo intermitente y el avión sigue rodando. La fase
        /// tiene que quedarse **en el camino del suelo** —`AfterLanding` y de ahí a `TaxiIn`, que es el
        /// único que lleva a `OnBlock`— y **no** saltar a `Climb`, que es de donde no se vuelve.
        /// </summary>
        [TestMethod]
        public void LaSecuenciaRealDelBE58_NoMandaLaFaseAClimb()
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 35, 57, DateTimeKind.Utc));
            var machine = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.Approach);

            int touchAndGoDeclared = 0;
            machine.OnTouchAndGo += () => touchAndGoDeclared++;

            // 1) En el aire, en final.
            machine.Update(Sample(onGround: false, groundSpeed: 90, vs: -600, pitch: 2.0,
                                  secondsSinceTouchdown: double.MaxValue));
            // 2) La toma (-104 fpm), que deja la fase en AfterLanding.
            clock.At(clock.Now.AddSeconds(1));
            machine.Update(Sample(onGround: true, groundSpeed: 96, vs: -104, pitch: 0.6,
                                  secondsSinceTouchdown: 0.0));
            Assert.AreEqual(FlightPhase.AfterLanding, machine.CurrentPhase, "la toma tiene que registrarse");

            // 3) La carrera de aterrizaje con el contacto de suelo intermitente: el simulador publica
            //    «en el aire» y el avión va a 88 kt bajando. Es la muestra que disparó todo.
            foreach (var (gs, vs) in new[] { (97, -140), (88, -178), (32, 7), (11, -2), (7, -1) })
            {
                clock.At(clock.Now.AddSeconds(1));
                machine.Update(Sample(onGround: false, groundSpeed: gs, vs: vs, pitch: 0.6,
                                      secondsSinceTouchdown: 6.0));
            }

            Assert.AreEqual(FlightPhase.TaxiIn, machine.CurrentPhase,
                "el avión está rodando: la fase es la de llegada, el camino que lleva a OnBlock");
            Assert.AreEqual(0, touchAndGoDeclared, "no hubo ningún touch-and-go");
        }

        /// <summary>
        /// Y el caso contrario, para que el arreglo no se lleve por delante lo que sí funciona: un
        /// touch-and-go de verdad, con ascenso sostenido, sigue declarándose y sigue llevando a `Climb`.
        /// </summary>
        [TestMethod]
        public void UnTouchAndGoDeVerdad_SigueLlevandoAClimb()
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 35, 57, DateTimeKind.Utc));
            var machine = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.Approach);

            int touchAndGoDeclared = 0;
            machine.OnTouchAndGo += () => touchAndGoDeclared++;

            machine.Update(Sample(onGround: false, groundSpeed: 90, vs: -600, pitch: 2.0,
                                  secondsSinceTouchdown: double.MaxValue));
            clock.At(clock.Now.AddSeconds(1));
            machine.Update(Sample(onGround: true, groundSpeed: 96, vs: -104, pitch: 0.6,
                                  secondsSinceTouchdown: 0.0));

            // El piloto aplica potencia: el avión se va subiendo, y el ascenso se sostiene.
            for (int i = 0; i < 5; i++)
            {
                clock.At(clock.Now.AddSeconds(1));
                machine.Update(Sample(onGround: false, groundSpeed: 95 + i, vs: 900, pitch: 6.0,
                                      secondsSinceTouchdown: 10.0 + i));
            }

            Assert.AreEqual(FlightPhase.Climb, machine.CurrentPhase);
            Assert.AreEqual(1, touchAndGoDeclared, "se declara una sola vez, no en cada ciclo");
        }
    }
}
