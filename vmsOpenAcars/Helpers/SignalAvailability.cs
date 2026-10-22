using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Dice si un offset **demostró que el addon lo publica**, mirando si su valor cambió alguna vez.
    ///
    /// Existe por dos offsets que no se pueden distinguir de un cero por su valor:
    ///
    /// - **Autobrake** (`0x2F80`): `0` es un valor **válido** —RTO—, así que «vale 0» no significa ni
    ///   «no hay dato» ni «RTO puesto». Pero un vuelo real **siempre** lo mueve (RTO u OFF antes del
    ///   despegue, 1/2/3 antes de aterrizar, OFF en el rodaje), así que un valor que no cambia en todo
    ///   el vuelo es, en la práctica, un offset que el addon no escribe.
    /// - **Carteles de cabina** (`0x341C`/`0x341D`): si el piloto nunca los toca, 0 puede ser «apagado»
    ///   o «el addon no lo publica». Y no se puede acusar al addon por un vuelo en el que nadie tocó el
    ///   letrero: se exige el cambio, no el valor.
    ///
    /// La regla es la del repo: **degradar sin datos**, no suponer. Mientras no haya cambio se publica
    /// «sin dato»; en cuanto el valor se mueve una vez, el offset queda acreditado para el resto del
    /// vuelo y se usa el valor tal cual (aunque vuelva al de partida).
    /// </summary>
    internal sealed class SignalAvailability<T>
    {
        private readonly IEqualityComparer<T> _comparer = EqualityComparer<T>.Default;

        private T    _first;
        private bool _seeded;

        /// <summary>True en cuanto el valor difiere del primero que se vio.</summary>
        internal bool Available { get; private set; }

        /// <summary>El primer valor visto (el de partida), útil para el registro de diagnóstico.</summary>
        internal T First => _first;

        /// <summary>Un muestreo. Devuelve <see cref="Available"/>.</summary>
        internal bool Update(T value)
        {
            if (!_seeded)
            {
                _first  = value;
                _seeded = true;
                return false;
            }

            if (!_comparer.Equals(value, _first))
                Available = true;

            return Available;
        }

        /// <summary>Vuelo nuevo: se vuelve a exigir la prueba.</summary>
        internal void Reset()
        {
            _first      = default(T);
            _seeded     = false;
            Available   = false;
        }
    }
}
