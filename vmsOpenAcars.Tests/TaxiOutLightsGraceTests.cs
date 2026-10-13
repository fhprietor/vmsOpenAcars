using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La gracia de entrada de fase** (v0.9.40): el check que se cobra **en el tick** en que el
    /// avión entra en una fase es irreversible, así que se arma y se decide
    /// <see cref="ApproachValidator.TaxiOutLightsGraceSec"/> segundos después; si la fase no se
    /// sostuvo, se cancela **sin cobrar nada**.
    ///
    /// El porqué, con las cifras del PIREP real **`XPNkY9jDneZ6ByGQ`** (SKCG→SKBQ, 13/10/2026 UTC):
    /// a las **01:58:56** el falso `TaxiOut` cobró **−5 por «Luces TAXI APAGADAS durante el rodaje»**
    /// con el avión **todavía remolcado** —el empuje no terminó hasta las **01:59:34**, 38 s
    /// después— y con el taxi light apagado, que durante un pushback es lo correcto. El punto no se
    /// revirtió aunque la fase sí.
    ///
    /// **Y la gracia no perdona la regla**: rodar con el taxi light apagado **más de 5 s** sigue
    /// costando su −5. Los dos lados están fijados aquí.
    /// </summary>
    [TestClass]
    public class TaxiOutLightsGraceTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 11, 1, 58, 56, DateTimeKind.Utc);

        private sealed class TestClock
        {
            public DateTime Now;
            public TestClock(DateTime start) { Now = start; }
            public DateTime Read() => Now;
            public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
        }

        private static ApproachValidator ValidatorAt(TestClock clock)
            => new ApproachValidator(new HashSet<string>(), clock.Read);

        /// <summary>Entrada en `TaxiOut` como la dispara el vuelo: `CheckPhaseEntryLights`.</summary>
        private static void EnterTaxiOut(ApproachValidator v, bool navOn, bool taxiOn)
            => v.CheckPhaseEntryLights(FlightPhase.TaxiOut, navOn, taxiOn, strobeOn: false, landingOn: false);

        [TestMethod]
        public void LaGraciaDeLuces_CancelaSiLaFaseNoSeSostiene()
        {
            // El caso real: el falso `TaxiOut` de las 01:58:56 revirtió a `Pushback` (con el arreglo
            // del fin del empuje ni llega a declararse, y esta gracia es la defensa de profundidad si
            // el detector vuelve a equivocarse). La fase no se sostiene → **no se cobra nada**.
            var clock = new TestClock(T0);
            var v = ValidatorAt(clock);

            EnterTaxiOut(v, navOn: true, taxiOn: false);
            Assert.AreEqual(0, v.LightsViolationCount, "al entrar no se evalúa: solo se arma");

            clock.Advance(2);
            Assert.IsFalse(v.TickDeferredTaxiOutLights(FlightPhase.TaxiOut, true, false),
                           "a los 2 s la gracia aún no ha transcurrido");
            Assert.AreEqual(0, v.LightsViolationCount);

            // La fase dejó de ser `TaxiOut` (volvió a `Pushback`): el check se cancela.
            clock.Advance(2);   // 4 s, todavía dentro de la gracia
            v.TickDeferredTaxiOutLights(FlightPhase.Pushback, true, false);
            Assert.AreEqual(0, v.LightsViolationCount, "la fase no se sostuvo: el check se cancela");

            // Aunque la fase vuelva a ser `TaxiOut` más tarde, aquel armado ya no existe: una entrada
            // nueva armaría un check nuevo.
            clock.Advance(60);
            Assert.IsFalse(v.TickDeferredTaxiOutLights(FlightPhase.TaxiOut, true, false));
            Assert.AreEqual(0, v.LightsViolationCount);
        }

        [TestMethod]
        public void RodarMasDeCincoSegundosConElTaxiLightApagado_SiguePenalizando()
        {
            // El caso normal **no se pierde**: el piloto que rueda con el taxi light apagado más de
            // 5 s sigue pagando su −5 (el arreglo del falso positivo no perdona la regla).
            var clock = new TestClock(T0);
            var v = ValidatorAt(clock);
            string osd = null;
            v.OnOsdMessage += (msg, sev) => osd = msg;

            EnterTaxiOut(v, navOn: true, taxiOn: false);

            clock.Advance(4);
            v.TickDeferredTaxiOutLights(FlightPhase.TaxiOut, true, false);
            Assert.AreEqual(0, v.LightsViolationCount, "a los 4 s todavía está dentro de la gracia");

            clock.Advance(1);   // 5 s justos
            Assert.IsTrue(v.TickDeferredTaxiOutLights(FlightPhase.TaxiOut, true, false));
            Assert.AreEqual(1, v.LightsViolationCount, "rodar 5 s con el taxi light apagado cuesta su −5");
            StringAssert.Contains(osd, "TAXI LIGHTS");

            // Y se cobra una sola vez: el armado se consume.
            clock.Advance(30);
            Assert.IsFalse(v.TickDeferredTaxiOutLights(FlightPhase.TaxiOut, true, false));
            Assert.AreEqual(1, v.LightsViolationCount);
        }

        [TestMethod]
        public void ElTaxiLightEncendidoDentroDeLaGracia_NoCuestaPuntos()
        {
            // «El piloto enciende el taxi light justo al soltarse el remolque»: si el encendido
            // ocurre dentro de la gracia, el check —que evalúa el estado **vivo**— no cobra nada.
            var clock = new TestClock(T0);
            var v = ValidatorAt(clock);

            EnterTaxiOut(v, navOn: false, taxiOn: false);   // ambas apagadas al entrar

            clock.Advance(5);
            // A los 5 s las dos ya están encendidas.
            v.TickDeferredTaxiOutLights(FlightPhase.TaxiOut, navOn: true, taxiOn: true);

            Assert.AreEqual(0, v.LightsViolationCount, "encenderlas dentro de la gracia no cuesta puntos");
        }

        [TestMethod]
        public void LaGraciaDeIvao_SoloSeCobraSiElRodajeSeSostiene()
        {
            // El otro −5 que colgaba del mismo tick: «desconectado al iniciar TaxiOut». En el caso
            // real el piloto se conectó a IVAO **1 segundo** después del falso `TaxiOut` (01:58:56
            // → 01:58:57), así que el cargo estuvo a un pelo. El chequeo se difiere la misma gracia y
            // solo se lanza si la fase sigue siendo `TaxiOut`: la consulta, **después** de la gracia,
            // ve al piloto ya conectado.
            Assert.IsFalse(PhaseEntryGrace.PhaseHeld(FlightPhase.Pushback, FlightPhase.TaxiOut),
                "un TaxiOut falso que volvió a Pushback no puede cobrar el −5 de IVAO");
            Assert.IsTrue(PhaseEntryGrace.PhaseHeld(FlightPhase.TaxiOut, FlightPhase.TaxiOut),
                "si el rodaje se sostiene, la consulta de IVAO se hace (y a los 5 s ya está conectado)");
        }

        [TestMethod]
        public void LaGracia_SinArmar_NoEvaluaNada()
        {
            // Sin entrada en la fase no hay nada que evaluar ni que consumir: el ciclo de telemetría
            // corre desde el primer segundo del vuelo y no puede cobrar un check que nadie armó.
            var clock = new TestClock(T0);
            var v = ValidatorAt(clock);

            Assert.IsFalse(v.TickDeferredTaxiOutLights(FlightPhase.TaxiOut, false, false));
            Assert.AreEqual(0, v.LightsViolationCount);
        }
    }
}
