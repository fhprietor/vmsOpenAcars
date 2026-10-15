using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El cierre manual del vuelo** (`FlightPhaseStateMachine.ForceOnBlock`), que es la salida del
    /// PIREP real **`RBmrBApLOjWlYmO2`**.
    ///
    /// Su BE58 se quedó clasificado en `Climb` desde el falso touch-and-go de las 03:36:02 y **nunca
    /// volvió al suelo**: desde esa fase no hay camino a `OnBlock` (sólo se llega por `TaxiIn`), así que
    /// `MainViewModel` —que habilita `SEND PIREP` únicamente en `OnBlock` o `Completed`— no le ofreció
    /// nunca el botón de enviar y **al piloto sólo le quedaba cancelar, que borra el PIREP**. El vuelo
    /// lo cerró a mano un administrador desde phpVMS, y el PIREP quedó con `score` y `notes` vacíos
    /// porque nuestro cliente no llegó a filearlo.
    ///
    /// Lo que se fija aquí es lo que hace útil el override: **publica la transición** —sin eso, la fase
    /// cambiaría por dentro y el botón de enviar seguiría sin aparecer— y **no se puede aplicar dos
    /// veces**, para no cerrar el mismo vuelo por duplicado.
    /// </summary>
    [TestClass]
    public class ManualOnBlockTests
    {
        private sealed class TestClock
        {
            public DateTime Now;
            public TestClock(DateTime start) { Now = start; }
            public DateTime Read() => Now;
            public void At(DateTime t) { Now = t; }
        }

        // El estado real del BE58 a las 03:39:19 —último registro del PIREP—: la fase del cliente era
        // `ICL` (Climb) y el avión estaba **aparcado**, con el `msl` clavado en 875 ft y el freno de
        // parqueo puesto. Rodó así casi tres minutos sin que la máquina se enterara.
        private static PhaseInput ParkedInClimb() => new PhaseInput
        {
            GroundSpeed     = 0,
            IsOnGround      = true,
            ParkingBrakeSet = true,
            AreEnginesOn    = false,
            Altitude        = 875,
            Lat             = 2.14901,
            Lon             = -74.76444,
            Heading         = 135,
            Origin          = "SKSJ",
            Destination     = "SKSV",
        };

        private static FlightPhaseStateMachine MachineInClimb(TestClock clock)
        {
            var machine = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.Climb);
            return machine;
        }

        /// <summary>
        /// El caso real: la máquina en `Climb` con el avión aparcado. El cierre manual tiene que dejar
        /// la fase en `OnBlock` **y publicar la transición con el `from` correcto**, que es lo que hace
        /// que `MainViewModel` habilite `SEND PIREP`.
        /// </summary>
        [TestMethod]
        public void DesdeLaFaseAtascadaDelCasoReal_CierraElVueloYPublicaLaTransicion()
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 39, 20, DateTimeKind.Utc));
            var machine = MachineInClimb(clock);

            var transitions = new List<(FlightPhase From, FlightPhase To)>();
            machine.OnTransitioned += (from, to) => transitions.Add((from, to));

            Assert.IsTrue(machine.ForceOnBlock(), "había vuelo abierto en una fase sin salida");

            Assert.AreEqual(FlightPhase.OnBlock, machine.CurrentPhase);
            CollectionAssert.AreEqual(
                new[] { (FlightPhase.Climb, FlightPhase.OnBlock) },
                transitions.ToArray(),
                "sin transición publicada el botón de enviar no aparece: es la mitad del arreglo");
        }

        /// <summary>
        /// Cerrar dos veces no puede duplicar el cierre (ni el `block_on_time`, ni la línea del log).
        /// Segundo intento: no hace nada y **no publica nada**.
        /// </summary>
        [TestMethod]
        public void ConElVueloYaCerrado_NoHaceNadaNiDuplicaLaTransicion()
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 39, 20, DateTimeKind.Utc));
            var machine = MachineInClimb(clock);

            int published = 0;
            machine.OnTransitioned += (from, to) => published++;

            Assert.IsTrue(machine.ForceOnBlock());
            Assert.AreEqual(1, published);

            Assert.IsFalse(machine.ForceOnBlock(), "ya estaba en OnBlock");
            Assert.AreEqual(FlightPhase.OnBlock, machine.CurrentPhase);
            Assert.AreEqual(1, published, "no puede publicarse una segunda transición");
        }

        [TestMethod]
        public void UnVueloCompletado_TampocoSeCierraOtraVez()
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 39, 20, DateTimeKind.Utc));
            var machine = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.Completed);

            int published = 0;
            machine.OnTransitioned += (from, to) => published++;

            Assert.IsFalse(machine.ForceOnBlock());
            Assert.AreEqual(FlightPhase.Completed, machine.CurrentPhase);
            Assert.AreEqual(0, published);
        }

        /// <summary>
        /// **El cierre manual no se puede deshacer solo.** Después de cerrar, la telemetría sigue
        /// llegando —y en el vuelo real llegaba con la bandera de «en el suelo» en falso, que es lo que
        /// había roto todo—, así que la fase tiene que aguantar en `OnBlock` con el avión parado tanto
        /// si el simulador dice que está en tierra como si no.
        /// </summary>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void DespuesDelCierreManual_LaTelemetriaNoLoDeshace(bool simulatorSaysOnGround)
        {
            var clock   = new TestClock(new DateTime(2026, 10, 14, 3, 39, 20, DateTimeKind.Utc));
            var machine = MachineInClimb(clock);
            Assert.IsTrue(machine.ForceOnBlock());

            for (int i = 0; i < 10; i++)
            {
                clock.At(clock.Now.AddSeconds(1));
                var sample = ParkedInClimb();
                sample.IsOnGround = simulatorSaysOnGround;
                machine.Update(sample);
            }

            Assert.AreEqual(FlightPhase.OnBlock, machine.CurrentPhase,
                "el cierre del piloto no puede deshacerlo el siguiente ciclo de telemetría");
        }
    }
}
