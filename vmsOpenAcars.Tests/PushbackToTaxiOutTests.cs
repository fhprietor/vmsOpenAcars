using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El fin del pushback manda** (v0.9.40): `Pushback → TaxiOut` ya no se declara con una sola
    /// muestra de velocidad. Exige **tres** cosas: que el empuje haya terminado —freno de parqueo
    /// puesto, o el tope de seguridad `PushbackMaxSec`—, que la GS supere 5 kt **sostenidos**
    /// `PushbackToTaxiSustainSec`, y nada más (el estado de motores **no** es puerta: ver
    /// `MotoresApagadosONoSinDato_NoBloqueanElRodaje`).
    ///
    /// **El tope de `PushbackMaxSec` es solo el rescate, no la puerta habitual** (v0.9.41): con los
    /// **90 s** de la v0.9.40 se disparaba a mitad de **todos** los pushbacks reales medidos —**142 s**
    /// en `XPNkY9jDneZ6ByGQ` y **148 s** en `wk0896ajpxKalPrp`— y volvió a cobrarse un −5
    /// inmerecido; ahora son **300 s** (ver `RemolqueDe112SegundosSinFrenoNiMotores_NoDeclaraTaxiOut`,
    /// el caso real que lo obligó).
    ///
    /// El caso es el PIREP real **`XPNkY9jDneZ6ByGQ`** (SKCG→SKBQ, `vmsOpenACars/0.9.38`), con sus
    /// horas y sus cifras (UTC del 13/10/2026 —el vuelo estaba **en curso** cuando se informó del
    /// fallo—): empuje de **01:57:12** a **01:59:34** —101 s—, remolque a **2–4 kt** con el morro
    /// girando (rumbos 323° → 2° → 66°), **motores apagados** hasta las **01:59:06** y freno
    /// **liberado** todo el empuje. A las **01:58:56** la GS rozó los 5 kt en **una sola muestra** y
    /// la máquina declaró `TaxiOut` en el mismo tick que el check de luces: **−5 por «Luces TAXI
    /// APAGADAS durante el rodaje»** con el avión todavía remolcado, y el punto no se revirtió. Con
    /// estas reglas el rodaje se habría declarado a las **01:59:34**.
    ///
    /// El reloj se inyecta (`FlightPhaseStateMachine(Func&lt;DateTime&gt;)`) para poder recorrer la
    /// secuencia real sin esperar 300 s de reloj de pared.
    /// </summary>
    [TestClass]
    public class PushbackToTaxiOutTests
    {
        // ══ La secuencia real, en UTC ══════════════════════════════════════════
        private static readonly DateTime PushbackStart = new DateTime(2026, 10, 13, 1, 57, 12, DateTimeKind.Utc);
        private static readonly DateTime FalseTaxiOut  = new DateTime(2026, 10, 13, 1, 58, 56, DateTimeKind.Utc);
        private static readonly DateTime EnginesStart  = new DateTime(2026, 10, 13, 1, 59, 6,  DateTimeKind.Utc);
        private static readonly DateTime BrakeSet      = new DateTime(2026, 10, 13, 1, 59, 34, DateTimeKind.Utc);

        // ══ El caso que obligó a subir el tope (v0.9.41), también en UTC ═══════
        // PIREP `wk0896ajpxKalPrp` (SKBQ→SKCL, `vmsOpenACars/0.9.40`, en curso).
        private static readonly DateTime WkPushbackStart = new DateTime(2026, 10, 13, 3, 19, 42, DateTimeKind.Utc);
        private static readonly DateTime WkFalseTaxiOut  = new DateTime(2026, 10, 13, 3, 21, 34, DateTimeKind.Utc);
        private static readonly DateTime WkEnginesStart  = new DateTime(2026, 10, 13, 3, 22, 4,  DateTimeKind.Utc);
        private static readonly DateTime WkBrakeSet      = new DateTime(2026, 10, 13, 3, 22, 10, DateTimeKind.Utc);

        private sealed class TestClock
        {
            public DateTime Now;
            public TestClock(DateTime start) { Now = start; }
            public DateTime Read() => Now;
            public void At(DateTime t) { Now = t; }
        }

        private static PhaseInput Sample(int groundSpeedKt, bool parkingBrakeSet, bool enginesOn) => new PhaseInput
        {
            GroundSpeed   = groundSpeedKt,
            IsOnGround    = true,
            ParkingBrakeSet = parkingBrakeSet,
            AreEnginesOn  = enginesOn,
            Altitude      = 30,
            Lat           = 10.4422,
            Lon           = -75.5130,
            Heading       = 323,
            Origin        = "SKCG",
            Destination   = "SKBQ",
        };

        /// <summary>
        /// Arma la máquina en `Pushback` a la hora real y la engancha al validador como lo hace
        /// `FlightManager`: cada transición dispara `CheckPhaseEntryLights`, y cada ciclo de
        /// telemetría pasa el tick de la gracia. Así el test mide **las dos cosas**: la fase y el −5.
        /// </summary>
        private static (FlightPhaseStateMachine machine, ApproachValidator validator) MachineInPushback(TestClock clock)
        {
            var validator = new ApproachValidator(new HashSet<string>(), clock.Read);
            var machine   = new FlightPhaseStateMachine(clock.Read);
            machine.SetPhase(FlightPhase.Pushback);
            machine.OnTransitioned += (from, to) =>
                validator.CheckPhaseEntryLights(to, navOn: true, taxiOn: false, strobeOn: false, landingOn: false);
            return (machine, validator);
        }

        /// <summary>Un ciclo de telemetría de la máquina (con su tick de la gracia de luces).</summary>
        private static void Cycle(FlightPhaseStateMachine machine, ApproachValidator validator, PhaseInput input)
        {
            machine.Update(input);
            validator.TickDeferredTaxiOutLights(machine.CurrentPhase, navOn: true, taxiOn: false);
        }

        [TestMethod]
        public void Pushback_FrenadoSinMotores_YVelocidadDeRemolque_NoDeclaraTaxiOut()
        {
            // ── EL CASO REAL ────────────────────────────────────────────────────
            // Motores apagados (arrancan a las 01:59:06), freno liberado desde las 01:56:55 y el
            // remolque a 2–4 kt. La muestra de 01:58:56 es **la del −5**: una sola por encima de
            // 5 kt. Antes bastaba; ahora no.
            var clock = new TestClock(PushbackStart);
            var (machine, validator) = MachineInPushback(clock);

            Cycle(machine, validator, Sample(2, false, false));            // 01:57:42, rumbo 323°
            clock.At(new DateTime(2026, 10, 13, 1, 58, 22, DateTimeKind.Utc));
            Cycle(machine, validator, Sample(2, false, false));
            clock.At(new DateTime(2026, 10, 13, 1, 58, 43, DateTimeKind.Utc));
            Cycle(machine, validator, Sample(4, false, false));            // rumbo 66°

            clock.At(FalseTaxiOut);
            Cycle(machine, validator, Sample(6, false, false));            // el roce: UNA muestra

            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase,
                "una sola muestra por encima de 5 kt no puede declarar el rodaje (era el fallo de 01:58:56)");
            Assert.AreEqual(0, validator.LightsViolationCount,
                "sin rodaje no hay «Luces TAXI APAGADAS»: el remolque se hace con la luz de taxi apagada");

            // El remolque vuelve a 2–4 kt (la traza de posiciones del caso) y a las 01:59:13 marca
            // 5 kt: **no es > 5 kt**, así que la ventana de 3 s no llega a abrirse.
            clock.At(new DateTime(2026, 10, 13, 1, 58, 58, DateTimeKind.Utc));
            Cycle(machine, validator, Sample(3, false, false));
            clock.At(new DateTime(2026, 10, 13, 1, 59, 13, DateTimeKind.Utc));
            Cycle(machine, validator, Sample(5, false, false));            // rumbo 93°: sigue el empuje

            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase);
            Assert.AreEqual(0, validator.LightsViolationCount);

            // Y a las 01:59:34, con el freno puesto (fin real del empuje), sí entra: el rodaje de
            // verdad empieza ahí, no 38 s antes.
            clock.At(BrakeSet);
            Cycle(machine, validator, Sample(5, true, false));
            clock.At(BrakeSet.AddSeconds(1));
            Cycle(machine, validator, Sample(6, true, false));             // arranca la ventana
            clock.At(BrakeSet.AddSeconds(4));
            Cycle(machine, validator, Sample(6, true, false));             // 3 s sostenidos

            Assert.AreEqual(FlightPhase.TaxiOut, machine.CurrentPhase,
                "con el empuje terminado el rodaje se declara en cuanto la GS se sostiene");
        }

        [TestMethod]
        public void ConMotorEnMarcha_VelocidadSostenidaTresSegundos_YFreno_FinalmenteDeclaraTaxiOut()
        {
            var clock = new TestClock(PushbackStart);
            var (machine, validator) = MachineInPushback(clock);

            clock.At(EnginesStart);                                        // 🔄 motores iniciados
            Cycle(machine, validator, Sample(0, false, true));

            clock.At(BrakeSet);                                            // fin del empuje
            Cycle(machine, validator, Sample(0, true, true));

            clock.At(BrakeSet.AddSeconds(5));
            Cycle(machine, validator, Sample(6, true, true));              // arranca la ventana
            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase,
                "mientras la GS no lleve 3 s por encima de 5 kt no hay rodaje");

            clock.At(BrakeSet.AddSeconds(7));
            Cycle(machine, validator, Sample(6, true, true));              // 2 s: todavía no
            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase);

            clock.At(BrakeSet.AddSeconds(8));
            Cycle(machine, validator, Sample(6, true, true));              // 3 s sostenidos
            Assert.AreEqual(FlightPhase.TaxiOut, machine.CurrentPhase,
                "motor en marcha + empuje terminado + GS>5 kt durante 3 s = rodaje");
        }

        [TestMethod]
        public void MotoresApagadosONoSinDato_NoBloqueanElRodaje()
        {
            // El estado de motores **no** es puerta de esta transición, y es una decisión, no un
            // olvido: `RawTelemetryData.Eng1Running/Eng2Running` se derivan de N1/RPM/torque
            // (`FsuipcService`, umbrales por categoría), así que un addon que no publique esos
            // offsets devuelve **exactamente lo mismo** que un motor parado —«cero motores»— y no hay
            // forma de distinguirlo. El propio código ya asume esa ambigüedad en otro sitio
            // (`ThrustReverserMonitor.CheckAddonSupport`: «el addon puede no soportar este offset»).
            // Una puerta así dejaría a ese piloto **atrapado en `Pushback` para siempre** —un fallo
            // peor que el que se arregla—, y no habría ayudado en este caso: a las 01:58:56, la hora
            // del −5, **no había arrancado ningún motor todavía** (arrancaron a las 01:59:06), así que
            // cualquier inferencia por historial habría degradado y dejado pasar la muestra igual.
            var clock = new TestClock(PushbackStart);
            var (machine, validator) = MachineInPushback(clock);

            clock.At(BrakeSet);
            Cycle(machine, validator, Sample(0, true, false));             // freno puesto, cero motores
            clock.At(BrakeSet.AddSeconds(2));
            Cycle(machine, validator, Sample(6, true, false));
            clock.At(BrakeSet.AddSeconds(5));
            Cycle(machine, validator, Sample(6, true, false));

            Assert.AreEqual(FlightPhase.TaxiOut, machine.CurrentPhase,
                "sin dato fiable de motores la transición decide con el freno y la velocidad");
        }

        [TestMethod]
        public void SinFreno_PeroPasadosLosTrescientosSegundos_DeclaraTaxiOut()
        {
            // El tope de seguridad: el piloto que nunca pone el freno no puede quedarse en `Pushback`
            // para siempre. Pero el tope **no sustituye** a la velocidad: a los 303 s, con 5 kt (que no
            // es > 5 kt), sigue en `Pushback`; con 6 kt sostenidos 3 s, entra.
            var clock = new TestClock(PushbackStart);
            var (machine, validator) = MachineInPushback(clock);

            clock.At(PushbackStart.AddSeconds(303));
            Cycle(machine, validator, Sample(5, false, true));
            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase,
                "pasado el tope sigue haciendo falta superar 5 kt");

            clock.At(PushbackStart.AddSeconds(308));
            Cycle(machine, validator, Sample(6, false, true));             // arranca la ventana
            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase,
                "el tope no exime de sostener la velocidad");

            clock.At(PushbackStart.AddSeconds(311));
            Cycle(machine, validator, Sample(6, false, true));             // 3 s sostenidos
            Assert.AreEqual(FlightPhase.TaxiOut, machine.CurrentPhase,
                "a los 311 s sin freno (el tope son 300 s) el rodaje se declara");
        }

        [TestMethod]
        public void RemolqueDe112SegundosSinFrenoNiMotores_NoDeclaraTaxiOut()
        {
            // ── EL CASO REAL QUE OBLIGÓ A SUBIR EL TOPE A 300 s (v0.9.41) ───────
            // PIREP `wk0896ajpxKalPrp` (SKBQ→SKCL, `vmsOpenACars/0.9.40`, en curso). El empuje va de
            // las 03:19:42 a las 03:22:10 —**148 s**—, con los motores arrancando a las 03:22:04. A
            // los **112 s** (03:21:34) el avión seguía remolcado, por encima de 5 kt y sin freno: con
            // el tope de **90 s** de la v0.9.40 la puerta ya estaba abierta desde las 03:21:12 y el
            // rodaje se declaró en pleno empuje (03:21:34), con el −5 de «Luces TAXI APAGADAS» 5 s
            // después (03:21:39). Con **300 s** el tope no llega —a los 112 s quedan 188 s— y la
            // única puerta es el freno de parqueo, que es la señal correcta.
            var clock = new TestClock(WkPushbackStart);
            var (machine, validator) = MachineInPushback(clock);

            clock.At(WkPushbackStart.AddSeconds(18));                      // 03:20:00, ya remolcado
            Cycle(machine, validator, Sample(6, false, false));            // abre la ventana de 3 s

            clock.At(WkPushbackStart.AddSeconds(90));                      // 03:21:12: aquí disparaba el tope de 90 s
            Cycle(machine, validator, Sample(6, false, false));
            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase,
                "a los 90 s el empuje sigue en curso: el tope de la 0.9.40 declaraba aquí el TaxiOut falso");

            clock.At(WkFalseTaxiOut);                                      // 03:21:34 = 112 s
            Cycle(machine, validator, Sample(6, false, false));
            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase,
                "112 s de remolque sin freno ni motores no son un rodaje");
            Assert.AreEqual(0, validator.LightsViolationCount,
                "sin rodaje no hay «Luces TAXI APAGADAS»: el avión seguía remolcado");

            clock.At(WkEnginesStart);                                      // 03:22:04, motores en marcha
            Cycle(machine, validator, Sample(6, false, true));

            clock.At(WkBrakeSet);                                          // 03:22:10: fin real del empuje
            Cycle(machine, validator, Sample(2, true, true));              // el remolque se detiene
            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase,
                "con el freno puesto terminó el empuje, pero el rodaje aún exige sostener la velocidad");

            clock.At(WkBrakeSet.AddSeconds(20));                           // 03:22:30
            Cycle(machine, validator, Sample(6, true, true));              // abre la ventana
            clock.At(WkBrakeSet.AddSeconds(24));                           // 03:22:34
            Cycle(machine, validator, Sample(6, true, true));              // 4 s sostenidos
            Assert.AreEqual(FlightPhase.TaxiOut, machine.CurrentPhase,
                "el rodaje de verdad empieza cuando el empuje terminó, no 36 s antes");
        }

        [TestMethod]
        public void LaVelocidadDeRemolqueQueSeCae_ReiniciaLaVentanaDeTresSegundos()
        {
            // La ventana se resetea en cuanto la GS baja a 5 kt o menos: no basta con «haber ido
            // rápido alguna vez» durante el empuje. Es el patrón de los demás antirrebotes del
            // fichero (`_takeoffRollStart`, `_climbStableStart`).
            var clock = new TestClock(PushbackStart);
            var (machine, validator) = MachineInPushback(clock);

            clock.At(BrakeSet);
            Cycle(machine, validator, Sample(0, true, true));
            clock.At(BrakeSet.AddSeconds(1));
            Cycle(machine, validator, Sample(6, true, true));              // ventana abierta
            clock.At(BrakeSet.AddSeconds(2));
            Cycle(machine, validator, Sample(4, true, true));              // se cae: reinicio
            clock.At(BrakeSet.AddSeconds(4));
            Cycle(machine, validator, Sample(6, true, true));              // ventana nueva
            clock.At(BrakeSet.AddSeconds(6));
            Cycle(machine, validator, Sample(6, true, true));              // 2 s: no
            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase);
            clock.At(BrakeSet.AddSeconds(7));
            Cycle(machine, validator, Sample(6, true, true));              // 3 s: sí
            Assert.AreEqual(FlightPhase.TaxiOut, machine.CurrentPhase);
        }

        [TestMethod]
        public void Reset_LimpiaElFrenoYLaVentanaDeVelocidad()
        {
            var clock = new TestClock(PushbackStart);
            var (machine, validator) = MachineInPushback(clock);

            // Freno puesto en `Pushback`: a partir de aquí la fase tiene su salida habilitada…
            clock.At(BrakeSet);
            Cycle(machine, validator, Sample(0, true, true));

            // …pero el vuelo se resetea (nuevo vuelo, o retomar). Un `_pushbackEndedByBrake` que
            // sobreviviera al reset declararía el rodaje del **vuelo siguiente** en cuanto la GS
            // pasara de 5 kt, sin que nadie hubiera puesto el freno en él.
            machine.Reset();
            machine.SetPhase(FlightPhase.Pushback);

            clock.At(BrakeSet.AddSeconds(1));
            Cycle(machine, validator, Sample(6, false, true));             // freno liberado
            clock.At(BrakeSet.AddSeconds(4));
            Cycle(machine, validator, Sample(6, false, true));             // 3 s sostenidos

            Assert.AreEqual(FlightPhase.Pushback, machine.CurrentPhase,
                "el reset borra el fin de pushback: ni el freno ni la velocidad anteriores cuentan");
        }
    }
}
