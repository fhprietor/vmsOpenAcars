using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>Qué señal disparó el PA de pushback. El valor se usa para poder probar el orden.</summary>
    internal enum CabinPushbackTrigger
    {
        None = 0,
        DoorsClosed,
        BeaconWithParkingBrakeSet,
        Beacon,
        ParkingBrakeReleased,
        PhaseEntry,
    }

    /// <summary>
    /// **El estado explícito con el que se decide el PA de pushback.** No hay reloj ni red: el
    /// llamante mira el simulador y pasa lo que ve. `ParkingBrakeWasSet` es «en algún momento de
    /// este vuelo vimos el freno puesto», que es lo que convierte una lectura en un **suceso**
    /// (el freno se acaba de soltar) y no en un estado permanente.
    /// </summary>
    internal struct CabinPushbackState
    {
        public bool Announced;
        public bool BeaconOn;
        public bool ParkingBrakeSet;
        public bool ParkingBrakeWasSet;
        public bool DoorsSeenOpen;
    }

    /// <summary>
    /// **Cuándo se anuncia el PA de pushback** — el «cerramos puertas, vamos a empujar». Tiene que
    /// sonar **antes** de que el avión se mueva, y hasta v0.9.65 dependía de dos señales (beacon o
    /// freno liberado) más la entrada en fase como red.
    ///
    /// Medido en el PIREP real `3b0oBlXD5ZolKPM4` (AT76, SKUC→SKBO, 05/11/2026): `Parking Brake:
    /// RELEASED` **01:57:06** → `🔊 PA pushback (voz es)` **01:57:09** (+3,0 s, de los que **2,93 s**
    /// eran el chime `Resources/Audio/chime_warning.wav` que `SpeakFallbackAsync` reproduce antes de
    /// locutar) → `BEACON ON` y `── PUSHBACK ──` **01:57:19**, **13 s después** del freno: el piloto
    /// encendió el beacon ya con el avión moviéndose. El anuncio era correcto como idea pero sonaba
    /// con el empuje en marcha porque el chime se comía el margen.
    ///
    /// Los cinco disparadores, **el que llegue primero** y **una sola vez por vuelo**:
    ///
    /// 1. **Puertas cerradas.** Es la señal literal del PA: la tripulación cierra las salidas y pide
    ///    el remolque. Arma cuando se ve **alguna puerta abierta** y dispara cuando están **todas
    ///    cerradas** (`0x3367`). El diseño **degrada**: `0x3367` no se ha podido verificar en
    ///    simulador, y un addon que no lo publica devuelve siempre el mismo valor —si es 0, nunca se
    ///    vio una puerta abierta y el disparador no se arma; si es distinto de 0, nunca llegan a
    ///    estar todas cerradas—. En los dos casos **no dispara jamás** y deciden los otros cuatro.
    ///    Es a propósito: un cierre de puertas inventado anunciaría un pushback que no existe.
    /// 2. **Beacon encendido con el freno de parqueo puesto.** Requisito explícito del mantenedor:
    ///    si el piloto enciende la luz anti-colisión **antes** de soltar el freno, eso ya anuncia el
    ///    cierre de puertas y el avión aún no se ha movido.
    /// 3. **Beacon encendido.** La señal clásica de «vamos a mover el avión», sin más.
    /// 4. **Freno liberado.** El remolcador engancha y empieza a tirar; exige haber visto el freno
    ///    **puesto** antes, para que un addon que no publica `0x0BC8` (y devuelve siempre 0) no se
    ///    lea como «freno recién liberado» en el primer tick de `Boarding`.
    /// 5. **Entrada en `Pushback`.** La red: el vuelo que ya venía empujado al conectar el ACARS no
    ///    tiene señal temprana y merece un aviso tarde antes que ninguno.
    /// </summary>
    internal static class CabinPushbackPolicy
    {
        /// <summary>
        /// ¿La fase es de las que admiten el anuncio? Solo antes de moverse (`Boarding`) y en el
        /// propio empuje (`Pushback`): en `TaxiOut` el avión ya rueda y el PA llegaría tarde.
        /// </summary>
        internal static bool IsCandidatePhase(FlightPhase phase)
            => phase == FlightPhase.Boarding || phase == FlightPhase.Pushback;

        /// <summary>
        /// El disparador de puertas **se arma al ver una abierta** y no se desarma en el vuelo: lo
        /// que anuncia el PA es el cierre, no que haya puertas. Sin haberlas visto abiertas, una
        /// lectura de «todas cerradas» no prueba nada (puede ser un offset que el addon no publica).
        /// </summary>
        internal static bool ArmOnOpenDoors(bool alreadyArmed, bool anyExitOpen)
            => alreadyArmed || anyExitOpen;

        /// <summary>
        /// Decide con qué señal se anuncia, o <see cref="CabinPushbackTrigger.None"/>. El orden de
        /// los `if` **es** la prioridad: puertas (la señal real), beacon con freno puesto, beacon,
        /// freno liberado y, por último, la entrada en fase.
        /// </summary>
        internal static CabinPushbackTrigger Decide(
            FlightPhase phase, CabinPushbackState state, bool allDoorsClosed)
        {
            if (state.Announced) return CabinPushbackTrigger.None;
            if (!IsCandidatePhase(phase)) return CabinPushbackTrigger.None;

            if (state.DoorsSeenOpen && allDoorsClosed) return CabinPushbackTrigger.DoorsClosed;

            if (state.BeaconOn && state.ParkingBrakeSet)
                return CabinPushbackTrigger.BeaconWithParkingBrakeSet;
            if (state.BeaconOn) return CabinPushbackTrigger.Beacon;

            if (state.ParkingBrakeWasSet && !state.ParkingBrakeSet)
                return CabinPushbackTrigger.ParkingBrakeReleased;

            if (phase == FlightPhase.Pushback) return CabinPushbackTrigger.PhaseEntry;

            return CabinPushbackTrigger.None;
        }
    }
}
