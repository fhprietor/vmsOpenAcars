using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Cuándo se anuncia el PA de pushback.** El de «cerramos puertas, vamos a empujar» tiene que
    /// sonar **antes** de que el avión se mueva, y hasta v0.9.56 sonaba al entrar en la fase
    /// `Pushback`… que por construcción se declara **después** de que el empuje haya empezado:
    /// la máquina de fases entra en `Pushback` cuando la GS lleva 8 s por debajo de 6 kt
    /// (`PushbackMinSec`) más el debounce de 3 s, o sea **~11 s de empuje ya hechos**.
    ///
    /// Medido en el PIREP real `r4JNk2b2bBMJd8lD` (B738, SKCC→SKUC): freno liberado **01:54:34**,
    /// beacon **01:54:42**, `── PUSHBACK ──` **01:54:47**, `🔊 PA pushback` **01:55:03** y **freno
    /// puesto 01:55:24** — el anuncio terminaba con el remolque ya acabado. (El instante del registro
    /// es el **fin** de la locución; por eso se mueve también esa marca al principio.)
    ///
    /// La señal buena es la que el piloto da **antes** de moverse, y hay dos según cómo vuele:
    /// el **beacon encendido** —luz anti-colisión, el aviso de la tripulación de que se empuja— y el
    /// **freno de parqueo liberado** con el avión aún parado. La primera que llegue dispara; si el
    /// avión arranca el vuelo con las dos ya dadas (empuje en curso al conectar el ACARS), no hay
    /// evento y decide la entrada en fase, que sigue ahí como red.
    /// </summary>
    internal static class CabinPushbackPolicy
    {
        /// <summary>
        /// ¿Toca anunciar el pushback con esta señal, con el avión aún en el puesto? Solo en
        /// `Boarding`: si ya estamos en `Pushback` el avión se está moviendo y el anuncio llega
        /// tarde; ahí decide <see cref="AnnounceOnPhaseEntry"/>.
        /// </summary>
        internal static bool AnnounceBeforePush(FlightPhase phase, bool alreadySent)
            => !alreadySent && phase == FlightPhase.Boarding;

        /// <summary>
        /// La red: entrar en `Pushback` sin haberlo anunciado (el piloto empujó antes de que
        /// viéramos el beacon ni el freno) sí anuncia, aunque sea tarde. Mejor un aviso con retraso
        /// que ningún aviso — es el caso de `rMBVEKj5PZzx0pX9`, que empezó con el freno ya liberado.
        /// </summary>
        internal static bool AnnounceOnPhaseEntry(bool alreadySent) => !alreadySent;
    }
}
