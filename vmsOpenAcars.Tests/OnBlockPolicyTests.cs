using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Cuándo se cierra el vuelo en el puesto.** Caso real: PIREP **`ez9OqzRnYxbnGLR8`**
    /// (B738 PMDG, MSFS 2024, SKBO→SKBG).
    ///
    /// ```
    /// 02:37:45  Parking Brake: SET
    /// 02:38:09  Engines shutdown
    /// 02:39:12  ON BLOCK          ← 87 s después del freno, 63 s después de apagar los motores
    /// ```
    ///
    /// La regla exigía **90 s parado** con los motores apagados y no miraba el freno, así que el
    /// cierre llegó por reloj cuando el vuelo estaba cerrado desde las 02:38:09: freno de parqueo
    /// puesto y los dos motores parados no se mueven más. Con `OnBlockPolicy` el avión aparcado
    /// cierra en **5 s**, y el camino de siempre (motores apagados sin freno, o hotel mode) sigue
    /// esperando sus 90 s.
    ///
    /// El primer test va contra la **máquina de fases de verdad**, con las tres horas del log y
    /// reloj inyectado; los demás fijan la regla pura y los caminos que no se tocan.
    /// </summary>
    [TestClass]
    public class OnBlockPolicyTests
    {
        private sealed class TestClock
        {
            internal DateTime Now;
            internal TestClock(DateTime start) { Now = start; }
            internal DateTime Read() => Now;
            internal void At(DateTime t) { Now = t; }
            internal void Advance(double seconds) => Now = Now.AddSeconds(seconds);
        }

        private static readonly DateTime BrakeSet   = new DateTime(2026, 11, 20, 2, 37, 45, DateTimeKind.Utc);
        private static readonly DateTime EnginesOff = new DateTime(2026, 11, 20, 2, 38,  9, DateTimeKind.Utc);

        private static PhaseInput Parked(bool enginesOn, bool brake) => new PhaseInput
        {
            GroundSpeed    = 0,
            IsOnGround     = true,
            VerticalSpeed  = 0,
            Pitch          = 0,
            Altitude       = 8367,          // SKBG, elevación del campo
            Lat            = 7.1265,
            Lon            = -73.1848,
            Heading        = 170,
            Origin         = "SKBO",
            Destination    = "SKBG",
            AreEnginesOn   = enginesOn,
            ParkingBrakeSet = brake,
        };

        // ── Las tres horas reales, contra la máquina de fases ────────────────────

        /// <summary>
        /// **02:37:45 freno · 02:38:09 motores · `OnBlock` en ≤ 5 s.** Con el freno puesto desde el
        /// principio, el avión parado y los motores apagados, el cierre no puede esperar 90 s: en el
        /// vuelo real tardó **87 s**.
        /// </summary>
        [TestMethod]
        public void ConFrenoPuestoYLosMotoresParados_OnBlockEnCincoSegundos()
        {
            var clock   = new TestClock(BrakeSet);
            var machine = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.TaxiIn);

            // 02:37:45 → 02:38:08: freno puesto, motores todavía en marcha. Aquí NO se cierra.
            while (clock.Now < EnginesOff.AddSeconds(-1))
            {
                clock.Advance(1);
                machine.Update(Parked(enginesOn: true, brake: true));
            }
            Assert.AreEqual(FlightPhase.TaxiIn, machine.CurrentPhase,
                "con los motores en marcha el vuelo no está cerrado por mucho freno que haya");

            // 02:38:08 → 02:38:14: motores fuera. El cierre tiene que caer dentro de 5 s.
            double secondsAfterShutdown = -1;
            clock.At(EnginesOff);
            for (int i = 0; i <= 5; i++)
            {
                machine.Update(Parked(enginesOn: false, brake: true));
                if (machine.CurrentPhase == FlightPhase.OnBlock)
                {
                    secondsAfterShutdown = (clock.Now - EnginesOff).TotalSeconds;
                    break;
                }
                clock.Advance(1);
            }

            Assert.AreEqual(FlightPhase.OnBlock, machine.CurrentPhase,
                "freno puesto + motores parados es el vuelo cerrado: no puede quedarse en TaxiIn");
            Assert.IsTrue(secondsAfterShutdown >= 0 && secondsAfterShutdown <= 5,
                $"OnBlock tiene que llegar en ≤ 5 s de apagar los motores, llegó en {secondsAfterShutdown} s");
        }

        /// <summary>
        /// El cierre no se adelanta al apagado: si el avión está parado con el freno **y los motores
        /// en marcha**, no se declara `OnBlock`. Es lo que impide cerrar el vuelo en un alto de
        /// rodaje con el freno puesto.
        /// </summary>
        [TestMethod]
        public void ConLosMotoresEnMarcha_NoSeCierraAunquePaseElTiempo()
        {
            var clock   = new TestClock(BrakeSet);
            var machine = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.TaxiIn);

            for (int i = 0; i < 120; i++)
            {
                clock.Advance(1);
                machine.Update(Parked(enginesOn: true, brake: true));
            }

            Assert.AreEqual(FlightPhase.TaxiIn, machine.CurrentPhase);
        }

        // ── La regla pura, con los dos plazos ────────────────────────────────────

        [DataTestMethod]
        [DataRow(false, true,  4.9, false, "aparcado: 4,9 s todavía no")]
        [DataRow(false, true,  5.0, true,  "aparcado: 5 s es el plazo")]
        [DataRow(false, false, 5.0, false, "sin freno, los 90 s de siempre: 5 s no basta")]
        [DataRow(false, false, 90.0, true, "sin freno, la detección de siempre cierra a los 90 s")]
        [DataRow(true,  true,  500.0, false, "motor en marcha: nunca se cierra")]
        public void LosDosPlazos_Son5SY90S(bool enginesRunning, bool brake, double stoppedSteps,
                                           bool expected, string because)
        {
            Assert.AreEqual(expected,
                OnBlockPolicy.ShouldClose(stoppedSteps, enginesRunning, hotelModeActive: false, parkingBrakeSet: brake),
                because);
        }

        /// <summary>El hotel mode (motores «parados» pero el avión en servicio) mantiene el camino de 90 s.</summary>
        [TestMethod]
        public void HotelMode_SigueSiendoElCaminoDe90S()
        {
            Assert.IsTrue(OnBlockPolicy.IsParked(enginesRunning: true, hotelModeActive: true),
                "en hotel mode los motores no cuentan como en marcha");
            Assert.AreEqual(OnBlockPolicy.NormalSeconds, OnBlockPolicy.RequiredSeconds(false));
            Assert.IsFalse(OnBlockPolicy.ShouldClose(50, true, hotelModeActive: true, parkingBrakeSet: false));
            Assert.IsTrue (OnBlockPolicy.ShouldClose(90, true, hotelModeActive: true, parkingBrakeSet: false));
            Assert.AreEqual(OnBlockPolicy.ParkedSeconds, OnBlockPolicy.RequiredSeconds(true));
        }
    }
}
