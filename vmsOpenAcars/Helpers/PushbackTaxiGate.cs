using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La tercera puerta para cerrar el `Pushback`: que el avión esté rodando de verdad.**
    ///
    /// Hasta v0.9.54 la salida de `Pushback` tenía **dos** puertas: el **freno de parqueo puesto**
    /// —la señal semántica del fin del empuje— y el **rescate de 300 s** para el piloto que nunca lo
    /// pone. La del freno exige un **cambio de estado**, y eso deja fuera un caso real: el PIREP
    /// `rMBVEKj5PZzx0pX9` (SKRG→SKSM, A321 de Fenix, 22/10/2026) arrancó con el freno **ya liberado**
    /// —el piloto lo soltó antes de abrir el ACARS— y no lo volvió a poner en todo el vuelo. Sin
    /// cambio de estado no hay evento, así que `TaxiOut` no se declaró hasta el rescate: **18:30:31**,
    /// con el avión rodando desde las **18:28:41** (**110 s** de rodaje fuera de la fase).
    ///
    /// La puerta nueva no adivina: pide **velocidad de rodaje sostenida** y **un motor en marcha**.
    ///
    /// - **8 kt y no 5**: el remolque de los casos reales va a **4-6 kt** (`XPNkY9jDneZ6ByGQ` rozó
    ///   5 kt con **una sola muestra**; el del `rMBVEKj5PZzx0pX9` iba a **4 kt**), mientras que el
    ///   rodaje real de los tres vuelos medidos va a **9-24 kt**.
    /// - **15 s**: 8 kt durante 15 s son **62 m** recorridos. Un remolque no sostiene eso; el 13/10,
    ///   que es el caso que obligó a endurecer la puerta, no lo habría alcanzado.
    /// - **Con motor en marcha**: el remolque se hace con los motores apagados, y el rodaje no. Si el
    ///   avión **no publica** el estado de los motores, esta puerta **no se abre nunca** y deciden las
    ///   otras dos: es la misma regla de siempre, un dato que falta no puede bloquear.
    ///
    /// La ventana se re-arma sola al caer por debajo de 8 kt (o al pararse el motor), así que lo que
    /// se mide es el tramo continuo y no la suma de dos arrancadas.
    /// </summary>
    internal sealed class PushbackTaxiGate
    {
        /// <summary>Velocidad de tierra que separa el rodaje del remolque (kt).</summary>
        internal const double TaxiSpeedKt = 8.0;

        /// <summary>Segundos que hay que sostenerla para darla por buena (62 m a 8 kt).</summary>
        internal const int SustainSeconds = 15;

        private DateTime _aboveSince = DateTime.MinValue;

        /// <summary>Vuelo nuevo, o se volvió a entrar en `Pushback`: la ventana empieza de cero.</summary>
        internal void Reset() => _aboveSince = DateTime.MinValue;

        /// <summary>
        /// ¿Hay evidencia de rodaje? Devuelve `true` **una vez** alcanzados los 15 s sostenidos; el
        /// llamante cierra la fase y re-arma.
        /// </summary>
        internal bool Confirm(DateTime now, double groundSpeedKt, bool anyEngineRunning)
        {
            if (!anyEngineRunning || groundSpeedKt < TaxiSpeedKt)
            {
                _aboveSince = DateTime.MinValue;
                return false;
            }

            if (_aboveSince == DateTime.MinValue)
            {
                _aboveSince = now;
                return false;
            }

            return (now - _aboveSince).TotalSeconds >= SustainSeconds;
        }
    }
}
