using System;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Gracia de entrada de fase**: arma el instante en que el avión entra en una fase y decide
    /// después si esa fase **se sostuvo** el tiempo pedido antes de cobrar un check de entrada.
    ///
    /// El porqué es el −5 inmerecido del PIREP real `XPNkY9jDneZ6ByGQ` (SKCG→SKBQ, 13/10/2026 UTC): a las
    /// **01:58:56** se declaró `TaxiOut` con el avión **todavía en el pushback** —el remolque iba a
    /// 2–4 kt y rozó los 5 kt con **una sola muestra**, y la fase entró en el mismo tick que el
    /// check—, así que el avión recibió **«Luces TAXI APAGADAS durante el rodaje» con los motores
    /// aún apagados y el freno liberado**. El rodaje de verdad empezó 38 s después, con el freno
    /// puesto a las **01:59:34**. Cuando el detector se equivoca, el check de entrada no puede ser
    /// irreversible: **la fase tiene que sostenerse** para que el cargo valga.
    ///
    /// Es puro y el reloj se inyecta para poder fijar la secuencia real en los tests sin esperar
    /// 5 s de reloj de pared (mismo motivo por el que el `ScoringService` no depende del tiempo).
    ///
    /// **Cancela, no retrasa**: si la fase deja de ser la que armó la gracia —el falso `TaxiOut`
    /// volvió a `Pushback`, o ya pasó a otra cosa— el armado se descarta y **no se cobra nada**. Eso
    /// es lo que separa esta gracia de un simple `sleep`: un retraso seguiría cobrando el punto.
    /// </summary>
    internal sealed class PhaseEntryGrace
    {
        private readonly Func<DateTime> _clock;
        private DateTime _armedAt = DateTime.MinValue;

        /// <summary>Segundos que hay que sostener la fase antes de que el check de entrada valga.</summary>
        public double GraceSeconds { get; }

        /// <param name="graceSeconds">Gracia pedida, en segundos.</param>
        /// <param name="clock">Reloj inyectable (`null` = reloj del sistema).</param>
        public PhaseEntryGrace(double graceSeconds, Func<DateTime> clock = null)
        {
            GraceSeconds = graceSeconds;
            _clock       = clock ?? (() => DateTime.UtcNow);
        }

        /// <summary>Hay una gracia armada y sin consumir.</summary>
        public bool IsArmed => _armedAt != DateTime.MinValue;

        /// <summary>Segundos transcurridos desde el armado; 0 si no hay gracia armada.</summary>
        public double ElapsedSeconds => IsArmed ? (_clock() - _armedAt).TotalSeconds : 0.0;

        /// <summary>Arma la gracia en el instante actual (entrada en la fase).</summary>
        public void Arm() => _armedAt = _clock();

        /// <summary>Descarta la gracia sin cobrar nada (la fase no se sostuvo, o reseteo del vuelo).</summary>
        public void Cancel() => _armedAt = DateTime.MinValue;

        /// <summary>
        /// Devuelve `true` **una sola vez**, cuando la gracia ya transcurrió; consume el armado para
        /// que el check se cobre una vez por entrada. Mientras no haya transcurrido, `false`.
        /// </summary>
        public bool TryConsume()
        {
            if (!IsArmed || ElapsedSeconds < GraceSeconds) return false;
            Cancel();
            return true;
        }

        /// <summary>
        /// **La fase se sostuvo**: la que hay después de la gracia es la misma que la armó. Es la
        /// condición que comparten el check de luces de `TaxiOut` y el −5 de IVAO —los dos se
        /// disparaban en el tick de entrada, y en el caso real el piloto se conectó **1 segundo**
        /// después del falso `TaxiOut`—. Pura, con test.
        /// </summary>
        public static bool PhaseHeld(FlightPhase phaseAfterGrace, FlightPhase armedPhase)
            => phaseAfterGrace == armedPhase;
    }
}
