using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Deja pasar un cambio de estado **solo cuando el valor nuevo se ha sostenido** un tiempo, y
    /// devuelve el valor anterior para que quien lo use pueda publicar «de X a Y».
    ///
    /// Existe por el ruido de los offsets del simulador. En el PIREP real **`5ow69vzDNW5ZMayB`**
    /// (B77W en MSFS, 21/10/2026) el registro del vuelo enseñó esto durante el ascenso:
    ///
    /// ```
    /// 13:17:46  Spoilers: EXTENDIDOS
    /// 13:17:49  Spoilers: RETRAÍDOS
    /// 13:17:50  Spoilers: RETRAÍDOS     ← dos «retraídos» seguidos: hubo un EXT/RET en medio
    /// 13:17:50  Spoilers: EXTENDIDOS
    /// 13:20:30  Spoilers: RETRAÍDOS
    /// 13:20:31  Spoilers: EXTENDIDOS
    /// 13:20:32  Spoilers: EXTENDIDOS
    /// ```
    ///
    /// El avión iba limpio: lo que rebotaba era el offset (0x0BD8), no los spoilers. El **estado**
    /// que consume la telemetría ya se filtraba en `FlightManager.DebounceState`; el **evento** que
    /// pinta la línea del registro no tenía filtro ninguno, así que cada rebote de 1 s era una línea.
    ///
    /// La regla es la misma que la de las luces (`LightHoldSeconds = 2,5 s`): no se anuncia el cambio
    /// hasta que el valor nuevo lleva ese tiempo sostenido; si vuelve atrás antes, no pasó nada. La
    /// **primera** muestra solo siembra el estado —no publica nada—, de modo que un vuelo que empieza
    /// con el tren abajo no anuncia «tren abajo» al pulsar START.
    ///
    /// Es genérico porque sirve para el tren (máscara `int`) y para los spoilers (`bool`) sin duplicar
    /// la lógica, y es puro: el reloj entra por parámetro y se prueba sin esperar.
    /// </summary>
    internal sealed class StateChangeGate<T>
    {
        private readonly double _holdSeconds;
        private readonly IEqualityComparer<T> _comparer = EqualityComparer<T>.Default;

        private T        _stable;
        private T        _pending;
        private DateTime _pendingSince = DateTime.MinValue;
        private bool     _seeded;

        internal StateChangeGate(double holdSeconds)
        {
            _holdSeconds = holdSeconds;
        }

        /// <summary>Segundos que el valor nuevo tiene que sostenerse para darse por bueno.</summary>
        internal double HoldSeconds => _holdSeconds;

        /// <summary>
        /// Un muestreo. Devuelve <c>true</c> la primera vez que el valor lleva <see cref="HoldSeconds"/>
        /// sostenido, con <paramref name="previous"/> en el valor que había antes. La primera llamada
        /// solo siembra y devuelve <c>false</c>.
        /// </summary>
        internal bool Update(DateTime now, T value, out T previous)
        {
            previous = _stable;

            if (!_seeded)
            {
                _stable = value;
                _seeded = true;
                return false;
            }

            if (_comparer.Equals(value, _stable))
            {
                _pendingSince = DateTime.MinValue;   // volvió a lo de siempre: no había cambio
                return false;
            }

            if (_pendingSince == DateTime.MinValue || !_comparer.Equals(value, _pending))
            {
                _pending      = value;
                _pendingSince = now;
                return false;
            }

            if ((now - _pendingSince).TotalSeconds < _holdSeconds) return false;

            previous      = _stable;                 // el de antes del cambio
            _stable       = value;
            _pendingSince = DateTime.MinValue;
            return true;
        }
    }
}
