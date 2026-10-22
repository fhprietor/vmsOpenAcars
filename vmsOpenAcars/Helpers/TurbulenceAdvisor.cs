using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>Qué hay que decir en este muestreo, si hay algo que decir.</summary>
    internal enum TurbulenceCallout
    {
        /// <summary>Nada: ni empieza ni termina.</summary>
        None = 0,
        /// <summary>Empieza la turbulencia: «abróchense los cinturones».</summary>
        Onset = 1,
        /// <summary>Ha quedado lisa: «pueden moverse por la cabina».</summary>
        Cleared = 2,
    }

    /// <summary>
    /// Decide **cuándo** un anuncio de turbulencia tiene sentido, con estado y reloj inyectado.
    ///
    /// No usa la señal de cinturones de la cabina: ese offset no se lee hoy en `FsuipcService` y,
    /// cuando está, el piloto suele dejarlo encendido todo el vuelo, así que como disparador sería
    /// peor que la turbulencia misma. Se decide con el **factor de carga** (`0x11BA`, expuesto como
    /// `RawTelemetryData.GForce`), que es lo que el pasajero siente y lo que el avión acaba de medir.
    ///
    /// **Lo que la turbulencia tiene de turbulencia es la oscilación, no el desvío.** La primera
    /// versión contaba muestras con `|g − 1| ≥ 0,15` y reversos de la tasa vertical, y con eso el
    /// PIREP real **`5ow69vzDNW5ZMayB`** (KSEA→SKRG, 21/10/2026) anunció turbulencia en pleno
    /// ascenso y la dio por superada 77 s después: el avión marcaba **`g 0,83`** y **`VS 5221 fpm`**
    /// mientras subía de 871 a 4 700 ft acelerando. Ninguna de las dos cosas es turbulencia —un
    /// ascenso acelerando da g < 1 de forma **sostenida** y el VS salta entre 700 y 5 500 fpm—, pero
    /// las dos encajaban en los umbrales: g fuera de banda son *todos* los muestreos durante minutos
    /// (120 en la ventana de 6 s), así que el detector declaraba turbulencia permanente mientras
    /// duró el ascenso. Con un desvío estático no se puede distinguir un bache de una maniobra.
    ///
    /// Ahora se cuentan **oscilaciones**: el estado pasa a «por encima» con `g ≥ 1,10`, a «por
    /// debajo» con `g ≤ 0,90`, y **una oscilación es cruzar de una banda a la otra**. Hacen falta
    /// **3 cruces en 6 s** (media oscilación por segundo), que es lo que hace el aire turbulento y
    /// lo que no hace ninguna maniobra: un ascenso deja a lo sumo dos cruces en un minuto, un
    /// «level-off» uno, y un golpe aislado ninguno. La tasa vertical **ya no decide**: es una
    /// magnitud derivada, ruidosa y dominada por el perfil de subida.
    ///
    /// El resto de reglas, y por qué:
    /// - **60 s sin oscilar** para decir que se ha terminado: una tregua de diez segundos no es el
    ///   final de nada, y al revés que la entrada aquí conviene esperar.
    /// - **10 min de enfriamiento** entre anuncios de entrada: una zona de media hora no se anuncia
    ///   seis veces.
    /// - **AGL mínimo 1 000 ft y avión en el aire**: en la final no se anuncia turbulencia, se vuela.
    /// - **Sin dato de g no se anuncia nada**: si el addon no escribe `0x11BA` el valor llega 0 y
    ///   nunca hay oscilación; se calla en vez de suponer.
    /// </summary>
    internal sealed class TurbulenceAdvisor
    {
        // ── Umbrales (públicos para los tests, que los fijan como contrato) ──────
        internal const double SignalSec     = 6.0;    // ventana donde se cuentan las oscilaciones
        internal const double GAmplitude    = 0.10;   // banda: g ≥ 1,10 o g ≤ 0,90
        internal const int    MinSwings     = 3;      // cruces de banda dentro de la ventana
        internal const double ClearSec      = 60.0;   // sin oscilar para anunciar el final
        internal const double CooldownSec   = 600.0;  // entre anuncios de entrada
        internal const double MinAglFt      = 1000.0;

        private readonly List<DateTime> _swings = new List<DateTime>();

        private int       _band;        // -1 por debajo, 0 dentro, +1 por encima
        private DateTime? _smoothSince;
        private DateTime? _lastOnset;
        private bool      _announced;

        /// <summary>
        /// Un muestreo. Devuelve <see cref="TurbulenceCallout.Onset"/> la primera vez que hay
        /// turbulencia clara, <see cref="TurbulenceCallout.Cleared"/> cuando lleva un minuto sin
        /// oscilar, y <see cref="TurbulenceCallout.None"/> el resto del tiempo.
        /// </summary>
        internal TurbulenceCallout Feed(DateTime now, double gLoad, bool airborne, double aglFt)
        {
            // Fuera de vuelo (o demasiado bajo) la ventana no vale nada: se tira y se re-arma.
            if (!airborne || aglFt < MinAglFt)
            {
                ResetWindow();
                return TurbulenceCallout.None;
            }

            TrackBand(now, gLoad);
            Prune(now);

            bool turbulent = _swings.Count >= MinSwings;

            if (turbulent)
            {
                _smoothSince = null;

                if (_announced) return TurbulenceCallout.None;

                if (_lastOnset.HasValue &&
                    (now - _lastOnset.Value).TotalSeconds < CooldownSec)
                    return TurbulenceCallout.None;

                _announced = true;
                _lastOnset = now;
                return TurbulenceCallout.Onset;
            }

            if (!_announced) return TurbulenceCallout.None;

            if (!_smoothSince.HasValue) _smoothSince = now;
            if ((now - _smoothSince.Value).TotalSeconds < ClearSec)
                return TurbulenceCallout.None;

            _announced   = false;
            _smoothSince = null;
            return TurbulenceCallout.Cleared;
        }

        /// <summary>Vuelo nuevo: ni ventana ni enfriamiento del anterior.</summary>
        internal void Reset()
        {
            ResetWindow();
            _lastOnset = null;
        }

        private void ResetWindow()
        {
            _swings.Clear();
            _band        = 0;
            _smoothSince = null;
            _announced   = false;
        }

        /// <summary>
        /// Lleva la banda en la que está g y apunta cada cruce de una banda a la otra. Entrar en la
        /// banda muerta no cambia el estado: así un valor que se queda en 0,95 no oscila.
        /// </summary>
        private void TrackBand(DateTime now, double gLoad)
        {
            int band = gLoad >= 1.0 + GAmplitude ? 1
                     : gLoad <= 1.0 - GAmplitude ? -1
                     : 0;

            if (band == 0) return;                 // dentro de la banda: no hay noticia

            if (band != _band && _band != 0)       // cruce de una banda a la contraria
                _swings.Add(now);

            _band = band;
        }

        private void Prune(DateTime now)
        {
            int remove = 0;
            while (remove < _swings.Count &&
                   (now - _swings[remove]).TotalSeconds > SignalSec)
                remove++;

            if (remove > 0) _swings.RemoveRange(0, remove);
        }
    }
}
