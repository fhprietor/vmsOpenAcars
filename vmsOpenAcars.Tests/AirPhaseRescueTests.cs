using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El rescate de la fase y la evidencia de suelo**, que son la garantía de que `OnBlock` siempre
    /// es alcanzable.
    ///
    /// En el PIREP real **`RBmrBApLOjWlYmO2`** el BE58 se quedó clasificado en `Climb` desde el falso
    /// touch-and-go de las 03:36:02 y ya no volvió: el simulador publicaba «en el aire» con el avión
    /// **rodando y aparcado** (`msl` clavado en 873-880 ft, GS 6-21 kt, freno puesto) y el reparto de
    /// tareas de la máquina colgaba entero de esa bandera. Como `OnBlock` sólo se alcanza desde
    /// `TaxiIn`, y `TaxiIn` sólo desde `AfterLanding`, el piloto se quedó **sin poder enviar el
    /// PIREP** —lo único que le ofrecía la aplicación era cancelar, que lo borra— hasta que un
    /// administrador lo cerró desde phpVMS.
    ///
    /// Se arreglan las dos mitades: la máquina **no se fía sólo de la bandera** para decidir si el
    /// avión está en el suelo, y si aun así se queda en una fase de vuelo con el avión rodando, vuelve
    /// al camino del suelo en 15 s.
    /// </summary>
    [TestClass]
    public class AirPhaseRescueTests
    {
        private sealed class TestClock
        {
            public DateTime Now;
            public TestClock(DateTime start) { Now = start; }
            public DateTime Read() => Now;
            public void At(DateTime t) { Now = t; }
        }

        private static PhaseInput Sample(bool onGround, int groundSpeed, int vs, double pitch = 0.6) => new PhaseInput
        {
            GroundSpeed           = groundSpeed,
            IsOnGround            = onGround,
            VerticalSpeed         = vs,
            Pitch                 = pitch,
            Altitude              = 875,
            Lat                   = 2.14901,
            Lon                   = -74.76444,
            Heading               = 135,
            Origin                = "SKSJ",
            Destination           = "SKSV",
            SecondsSinceTouchdown = 60.0,
        };

        /// <summary>
        /// **El camino del arreglo, con las cifras reales.** La fase es `AfterLanding` —el touch-and-go
        /// falso ya no se declara— pero el simulador sigue diciendo «en el aire» con el avión a
        /// **20 kt**. La evidencia tiene que mandar: la máquina entra en el camino del suelo y sale
        /// hacia `TaxiIn`, que es el único que lleva a `OnBlock`.
        /// </summary>
        [TestMethod]
        public void ConLaBanderaEnAirePeroElAvionRodando_LaFaseSigueElCaminoDelSuelo()
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 36, 18, DateTimeKind.Utc));
            var machine = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.AfterLanding);

            machine.Update(Sample(onGround: false, groundSpeed: 20, vs: 0));

            Assert.AreEqual(FlightPhase.TaxiIn, machine.CurrentPhase,
                "a 20 kt el avión está rodando: la fase tiene que ir a TaxiIn, no quedarse colgada");
        }

        /// <summary>
        /// **El rescate del estado exacto en que quedó el vuelo real**: fase `Climb` (a donde la llevó
        /// el falso touch-and-go) con el avión aparcado, `IsOnGround` en falso. A los 15 s sostenidos
        /// vuelve al camino del suelo —`AfterLanding`, porque el vuelo **ya había aterrizado**— y de
        /// ahí sale sola hacia `TaxiIn` y `OnBlock`.
        /// </summary>
        [TestMethod]
        public void ElEstadoAtascadoDelCasoReal_VuelveAlCaminoDelSueloEnQuinceSegundos()
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 36, 6, DateTimeKind.Utc));
            var machine = new FlightPhaseStateMachine(clock.Read);

            // El vuelo llegó a aterrizar (Approach → toque → AfterLanding), y luego el falso
            // touch-and-go lo mandó a `Climb`: se reproduce ese destino con `SetPhase`, que es
            // exactamente el estado que producía la condición vieja.
            machine.SetPhase(FlightPhase.Approach);
            machine.Update(Sample(onGround: false, groundSpeed: 90, vs: -600, pitch: 2.0));
            clock.At(clock.Now.AddSeconds(1));
            machine.Update(Sample(onGround: true, groundSpeed: 96, vs: -104));
            machine.SetPhase(FlightPhase.Climb);

            // La racha real, que duró casi tres minutos: GS 6-21 kt, VS ±8 fpm, y la bandera en falso.
            var racha = new[] { (32, 7), (11, -2), (7, -1), (16, 0), (21, -5), (19, -8), (17, -3),
                                (15, -6), (6, 0), (7, 0), (6, 5), (0, 0) };

            // Se para **en el ciclo del rescate** para poder comprobar a qué fase vuelve: el vuelo ya
            // había aterrizado, así que le toca `AfterLanding` y de ahí sale sola hacia `TaxiIn`.
            FlightPhase rescued = FlightPhase.Climb;
            int i = 0;
            var limit = new DateTime(2026, 10, 14, 3, 36, 6, DateTimeKind.Utc).AddSeconds(20);
            while (clock.Now < limit)
            {
                var (gs, vs) = racha[i % racha.Length];
                i++;
                clock.At(clock.Now.AddSeconds(1));
                machine.Update(Sample(onGround: false, groundSpeed: gs, vs: vs));

                if (machine.CurrentPhase != FlightPhase.Climb)
                {
                    rescued = machine.CurrentPhase;
                    break;
                }
            }

            Assert.AreEqual(FlightPhase.AfterLanding, rescued,
                "el avión aterrizó: la fase de vuelo con el avión en el suelo tiene que rendirse y " +
                "volver al camino del suelo, que es el único que lleva a OnBlock");
        }

        /// <summary>
        /// Y lo que **no** puede pasar: rescatar a un avión que está volando. A 250 kt y subiendo, la
        /// fase de vuelo es la correcta y no se toca por muchos ciclos que pasen.
        /// </summary>
        [TestMethod]
        public void UnAvionVolandoDeVerdad_NoSeRescata()
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 40, 0, DateTimeKind.Utc));
            var machine = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.Climb);

            for (int i = 0; i < 60; i++)
            {
                clock.At(clock.Now.AddSeconds(1));
                machine.Update(Sample(onGround: false, groundSpeed: 250, vs: 1500, pitch: 8.0));
            }

            Assert.AreEqual(FlightPhase.Climb, machine.CurrentPhase);
        }

        /// <summary>
        /// El rescate no se dispara mientras el avión va rápido: rodando a 100 kt por la pista la fase
        /// de vuelo no cuadra, pero **`IsStuck` exige ir por debajo de 40 kt**, así que espera a que el
        /// avión frene. Es lo que evita confundir una carrera de despegue con un avión aparcado.
        /// </summary>
        [TestMethod]
        public void RodandoRapidoPorLaPista_ElRescateEsperaAQueFrene()
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 36, 6, DateTimeKind.Utc));
            var machine = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.Climb);

            for (int i = 0; i < 30; i++)
            {
                clock.At(clock.Now.AddSeconds(1));
                machine.Update(Sample(onGround: true, groundSpeed: 100, vs: -20));
            }

            Assert.AreEqual(FlightPhase.Climb, machine.CurrentPhase,
                "a 100 kt la fase no cuadra, pero no es un avión aparcado: hay que esperar");
        }
    }
}
