using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Db;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// La regla de mínimos, en su punto exacto: el marcador <see cref="ApproachValidator.BelowMinimums"/>
    /// lo enciende la bajada por debajo de la DA **en el aire**, y lo limpia **el aterrizaje**
    /// (<see cref="ApproachValidator.ClearBelowMinimumsIfLanded"/>, llamado desde
    /// <c>FlightManager.RegisterTouchdown</c>). El −5 del criterio *Minimums Compliance* solo cae
    /// si el marcador sobrevive, o sea si no hubo toque.
    ///
    /// **Por qué existe este archivo.** La DA que usa el cliente es «elevación del umbral + 200 ft»
    /// —o la altitud del *glideslope*—, y **cualquier aterrizaje normal cruza esa altitud en el
    /// *flare***. Una regla que castigue «bajar de mínimos y aterrizar» resta −5 en casi todos los
    /// vuelos: es el fallo que se corrigió en la v0.8.2 con <c>ClearBelowMinimumsIfLanded()</c> y que
    /// se intentó reintroducir (invirtiendo la regla) el 12/10/2026, revirtiéndose por completo. Hasta
    /// ahora no había ninguna prueba que impidiera repetirlo: <c>ScoringServiceTests</c> fija el
    /// umbral de puntos (−5 con <c>BelowMinimums=true</c>), pero no **de dónde sale ese booleano**, que
    /// es justo lo que se rompió las dos veces.
    ///
    /// Los tests alimentan el validador por donde lo alimenta el vuelo
    /// (<c>FlightManager.UpdateFlightTracking</c> → <c>CheckApproachBelowGate</c>) y disparan el toque
    /// por el camino real (<c>RegisterTouchdown</c>, que en vuelo llega desde
    /// <c>_phaseMachine.OnTouchdownDetected</c>). La geometría y las alturas son las del fixture real
    /// de la 04R de KBOS que ya usa <c>ApproachThresholdTests</c>.
    /// </summary>
    [TestClass]
    public class ApproachValidatorMinimumsTests
    {
        // ══ Fixture real: KBOS 04R ══════════════════════════════════════════════
        //
        // Mismos números que `ApproachThresholdTests.KbosRwy04R()` —la pista que el endpoint de
        // NavData eligió en el vuelo real SKCG→KBOS del 22-23/09/2026—: umbral en
        // 42.35105895996094 / -71.01179504394531, eje 33,4° (magnético), 10.006 ft de largo y
        // elevación 20 ft. Aquí se arma como `IlsData` porque es lo que consume el validador;
        // `SetApproachData` deriva la DA como `ThresholdElevFt + 200` = **220 ft MSL**, que es la
        // altitud contra la que se compara el *flare*.

        private const double Kbos04RDaLatitude  = 42.35105895996094;
        private const double Kbos04RDaLongitude = -71.01179504394531;
        private const double Kbos04RCourse      = 33.4;   // el del fixture; el eje físico coincide
        private const double Kbos04RFieldElevFt = 20.0;
        private const int    Kbos04RDaftFt      = 220;    // 20 ft de campo + 200 ft

        /// <summary>
        /// Punto real sobre el eje, 0,6 NM por delante del umbral (el mismo desplazamiento que usa
        /// <c>ApproachThresholdTests</c>). No entra en la regla de mínimos —que solo mira la altitud—
        /// pero mantiene el contexto de aproximación coherente y evita disparar el localizer.
        /// </summary>
        private const double OnAxisLatitude  = Kbos04RDaLatitude  - 0.010;
        private const double OnAxisLongitude = Kbos04RDaLongitude - 0.0049;

        private static IlsData Kbos04RIls() => new IlsData
        {
            RunwayName      = "04R",
            Course          = Kbos04RCourse,
            ThresholdLat    = Kbos04RDaLatitude,
            ThresholdLon    = Kbos04RDaLongitude,
            ThresholdElevFt = Kbos04RFieldElevFt,
            GlideslopeAltFt = null,   // el fixture no lo trae: manda el «umbral + 200 ft»
        };

        /// <summary>
        /// Ciclo de telemetría del *flare*: ya por debajo de la DA (220 ft MSL) y **en el aire**, que
        /// es lo que pasa siempre en un aterrizaje normal. El AGL (160 ft) es coherente con la
        /// altitud sobre la elevación del campo (180 − 20).
        /// </summary>
        private static ApproachContext FlareContext(int altitudeMsl = 180) => new ApproachContext
        {
            Phase       = FlightPhase.Approach,
            AltitudeMsl = altitudeMsl,
            AGL         = altitudeMsl - Kbos04RFieldElevFt,
            IsOnGround  = false,
            Heading     = Kbos04RCourse,
            Lat         = OnAxisLatitude,
            Lon         = OnAxisLongitude,
            LandingLightOn = true,
            BeaconLightOn  = true,
            GearDown       = true,
            FlapsPosition  = 100,
        };

        /// <summary>
        /// Datos de puntuación mínimos: lo único que se mueve entre casos es `BelowMinimums`, que es
        /// exactamente el valor que `FlightManager.BuildScoreData()` copia de
        /// `_approachValidator.BelowMinimums` (`Core/Flight/FlightManager.Lifecycle.cs`).
        /// </summary>
        private static ScoringResult Score(bool belowMinimums) => new ScoringService().Calculate(
            new FlightScoreData
            {
                LandingDataCaptured = true,
                LandingRate         = -140,          // butter: no mete ruido en el total
                IlsTunedCorrectly   = true,
                BelowMinimums       = belowMinimums,
                EngineType          = ScoredEngineType.Turboprop,
            });

        private static ScoringDeduction MinimumsDeduction(ScoringResult r)
            => r.Deductions.FirstOrDefault(d => d.Criterion == "Minimums Compliance");

        // ══ Acceso al camino de producción del toque ═══════════════════════════
        //
        // `RegisterTouchdown` es privado y solo lo dispara `_phaseMachine.OnTouchdownDetected`
        // dentro de `FlightManager`; el vuelo se construye en `MainForm`, no en pruebas. Se arma el
        // `FlightManager` de verdad (su constructor solo cablea eventos: con servicios nulos no sale
        // a la red) y se entra por reflexión a los dos miembros que la regla necesita: el validador
        // —para darle telemetría como hace `UpdateFlightTracking`— y el propio `RegisterTouchdown`
        // —para hacer tocar el avión—. Así la prueba falla si alguien quita la llamada a
        // `ClearBelowMinimumsIfLanded()` de ese método.

        private static readonly FieldInfo ValidatorField = typeof(FlightManager)
            .GetField("_approachValidator", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo RegisterTouchdownMethod = typeof(FlightManager)
            .GetMethod("RegisterTouchdown", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FlightManager NewFlightManager() => new FlightManager(null, null);

        private static ApproachValidator ValidatorOf(FlightManager fm)
            => (ApproachValidator)ValidatorField.GetValue(fm);

        private static void SimulateTouchdown(FlightManager fm, int verticalSpeedFpm)
            => RegisterTouchdownMethod.Invoke(fm, new object[] { verticalSpeedFpm });

        // ══ El caso importante ══════════════════════════════════════════════════

        /// <summary>
        /// **Es la red del fallo de la v0.8.2 y del intento de inversión del 12/10/2026.** El avión
        /// baja de la DA en el aire —el *flare* de cualquier aterrizaje normal, aquí a 180 ft MSL
        /// contra los 220 ft de la 04R— y **toca**: el marcador queda limpio y no hay deducción.
        /// Si alguien vuelve a castigar «bajar de mínimos y aterrizar», esta prueba falla.
        /// </summary>
        [TestMethod]
        public void AterrizajeNormal_NoSePenaliza()
        {
            // 220 ft MSL es la DA real de la 04R (elevación 20 + 200): el contexto del flare tiene
            // que quedar por debajo para que el caso sea el de verdad.
            Assert.IsTrue(FlareContext().AltitudeMsl < Kbos04RDaftFt,
                "el flare del fixture (180 ft) tiene que estar por debajo de la DA de 220 ft");

            var fm        = NewFlightManager();
            var validator = ValidatorOf(fm);
            validator.SetApproachData(Kbos04RIls(), null, null);

            // El flare: por debajo de la DA y todavía en el aire → el marcador se enciende.
            validator.CheckApproachBelowGate(FlareContext());
            Assert.IsTrue(validator.BelowMinimums,
                "bajar de la DA EN EL AIRE enciende el marcador");
            Assert.IsTrue(fm.BelowMinimums,
                "el valor que puntúa el PIREP es el del validador");

            // El avión toca: el mismo camino que el vuelo real (RegisterTouchdown).
            SimulateTouchdown(fm, -180);

            Assert.IsFalse(validator.BelowMinimums,
                "el aterrizaje normal limpia el marcador: es la regla de la v0.8.2");
            Assert.IsFalse(fm.BelowMinimums, "y el PIREP ve el marcador limpio");

            Assert.IsNull(MinimumsDeduction(Score(fm.BelowMinimums)),
                "un aterrizaje normal no puede perder 5 puntos por haber cruzado la DA en el flare");
        }

        /// <summary>
        /// El contraste: bajada por debajo de la DA **sin toque** (motor y al aire, o go-around), el
        /// marcador sobrevive y el −5 cae con su motivo. Es la única forma de que el criterio
        /// dispare, y por eso el motivo dice «sin aterrizar».
        /// </summary>
        [TestMethod]
        public void BajadaDeMinimosSinAterrizar_SePenaliza()
        {
            var fm        = NewFlightManager();
            var validator = ValidatorOf(fm);
            validator.SetApproachData(Kbos04RIls(), null, null);

            validator.CheckApproachBelowGate(FlareContext());

            // Sin RegisterTouchdown no hay limpieza: el avión no volvió a tocar.
            Assert.IsTrue(fm.BelowMinimums, "sin aterrizaje el marcador sobrevive");

            ScoringResult r = Score(fm.BelowMinimums);
            ScoringDeduction d = MinimumsDeduction(r);

            Assert.IsNotNull(d, "bajar de la DA sin aterrizar sí se penaliza");
            Assert.AreEqual(5, d.PointsDeducted, "Minimums Compliance vale 5 puntos");
            Assert.AreEqual("descended below DA/DH without landing", d.Reason,
                "el motivo tiene que decir que no hubo aterrizaje");
        }

        /// <summary>
        /// Sin datos de ILS la DA es 0 y el validador no detecta nada: el criterio queda **omitido**,
        /// no aprobado con 0 ft. Se comprueba a 180 ft MSL (altura de flare) y a 25 ft MSL (por debajo
        /// de cualquier DA real), que es donde una detección sin dato se notaría.
        /// </summary>
        [TestMethod]
        public void SinIls_NoHayDeteccionNiPenalizacion()
        {
            var fm        = NewFlightManager();
            var validator = ValidatorOf(fm);
            validator.SetApproachData(null, null, null);   // sin ILS: DA = 0

            validator.CheckApproachBelowGate(FlareContext());
            Assert.IsFalse(fm.BelowMinimums,
                "sin ILS no hay DA contra la que comparar: no hay detección a 180 ft");

            validator.CheckApproachBelowGate(FlareContext(altitudeMsl: 25));
            Assert.IsFalse(fm.BelowMinimums,
                "ni siquiera a 25 ft MSL: la DA es 0 y el check está desactivado");

            Assert.IsNull(MinimumsDeduction(Score(fm.BelowMinimums)),
                "el criterio queda omitido, no puntuado");
        }

        /// <summary>
        /// `Reset()` —el que se llama en el arranque de vuelo y en `ResetFlightState`— devuelve el
        /// validador a su estado limpio: sin marcador y sin los datos de aproximación, así que la DA
        /// vuelve a 0 y un ciclo posterior a 180 ft no enciende nada.
        /// </summary>
        [TestMethod]
        public void Reset_DejaElEstadoLimpio()
        {
            var validator = new ApproachValidator(new HashSet<string>());
            validator.SetApproachData(Kbos04RIls(), null, null);
            validator.CheckApproachBelowGate(FlareContext());
            Assert.IsTrue(validator.BelowMinimums, "el caso de partida tiene el marcador encendido");

            validator.Reset();

            Assert.IsFalse(validator.BelowMinimums, "Reset() apaga el marcador");
            Assert.IsTrue(validator.IlsTunedCorrectly, "y vuelve a dar el ILS por bueno");

            // Los datos de aproximación también se han ido: la DA vuelve a 0.
            validator.CheckApproachBelowGate(FlareContext(altitudeMsl: 25));
            Assert.IsFalse(validator.BelowMinimums,
                "tras el Reset la DA es 0: no puede volver a encenderse solo");
        }
    }
}
