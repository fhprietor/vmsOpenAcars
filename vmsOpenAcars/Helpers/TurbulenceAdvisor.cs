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
    /// peor que la turbulencia misma. Se detecta con los dos datos que **ya** se leen cada ciclo de
    /// 20 Hz: el factor de carga (`0x11BA`, convertido con `CurrentGForce`) y la tasa vertical
    /// (`0x02C8`). Eso funciona en cualquier avión, publique los letreros o no.
    ///
    /// Los umbrales no son inventados: el propio proyecto ya tiene medido que **~100 fpm de ruido de
    /// VS es turbulencia leve** —es la cifra con la que la máquina de fases evita falsas
    /// transiciones—, así que un «golpe» cuenta a partir de **150 fpm**, por encima de ese ruido.
    ///
    /// **La densidad es el «sostenido».** La primera versión miraba una ventana de 20 s y pedía 3 s
    /// seguidos de señal: un bache de 3 s dejaba sus 4 golpes en la ventana y seguía contando como
    /// turbulencia **20 s después de haber terminado**, con lo que anunciaba un bache corto y además
    /// retrasaba el «ya pasó». Ahora la señal se mide sobre **6 s**: hacen falta **6 reversiones del
    /// VS** o **4 muestras con g fuera de 1** *dentro de esos 6 s*, algo que un golpe aislado no
    /// consigue —un bache de 3 s da 5 reversiones y 3 muestras— y una zona de verdad sí.
    ///
    /// El resto de reglas, y por qué:
    /// - **60 s lisos** para decir que se ha terminado: una tregua de diez segundos no es el final
    ///   de nada, y al revés que la entrada aquí conviene esperar.
    /// - **10 min de enfriamiento** entre anuncios de entrada: una zona de media hora no se anuncia
    ///   seis veces.
    /// - **AGL mínimo 1 000 ft y avión en el aire**: en la final no se anuncia turbulencia, se vuela.
    /// </summary>
    internal sealed class TurbulenceAdvisor
    {
        // ── Umbrales (públicos para los tests, que los fijan como contrato) ──────
        internal const double SignalSec       = 6.0;    // ventana donde se mide la densidad
        internal const double GDeviation      = 0.15;   // |g − 1|
        internal const double VsJerkFpm       = 150.0;  // golpe por encima del ruido (~100 fpm)
        internal const int    MinGDeviations  = 4;      // muestras con g fuera de 1 en 6 s
        internal const int    MinReversals    = 6;      // cambios de sentido del VS en 6 s
        internal const double ClearSec        = 60.0;   // liso sostenido para anunciar el final
        internal const double CooldownSec     = 600.0;  // entre anuncios de entrada
        internal const double MinAglFt        = 1000.0;

        private struct Sample
        {
            public DateTime At;
            public double   G;
            public double   Vs;
        }

        private readonly List<Sample> _window = new List<Sample>();

        private DateTime? _smoothSince;
        private DateTime? _lastOnset;
        private bool      _announced;

        /// <summary>
        /// Un muestreo. Devuelve <see cref="TurbulenceCallout.Onset"/> la primera vez que hay
        /// turbulencia clara, <see cref="TurbulenceCallout.Cleared"/> cuando lleva un minuto lisa, y
        /// <see cref="TurbulenceCallout.None"/> el resto del tiempo.
        /// </summary>
        internal TurbulenceCallout Feed(DateTime now, double gLoad, double verticalSpeedFpm,
                                        bool airborne, double aglFt)
        {
            // Fuera de vuelo (o demasiado bajo) la ventana no vale nada: se tira y se re-arma.
            if (!airborne || aglFt < MinAglFt)
            {
                ResetWindow();
                return TurbulenceCallout.None;
            }

            _window.Add(new Sample { At = now, G = gLoad, Vs = verticalSpeedFpm });
            Prune(now);

            bool turbulent = IsTurbulent();

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
            _window.Clear();
            _smoothSince = null;
            _announced   = false;
        }

        private void Prune(DateTime now)
        {
            int remove = 0;
            while (remove < _window.Count &&
                   (now - _window[remove].At).TotalSeconds > SignalSec)
                remove++;

            if (remove > 0) _window.RemoveRange(0, remove);
        }

        /// <summary>
        /// Las dos firmas de la turbulencia dentro de la ventana corta, cualquiera de las dos basta:
        /// muchas muestras con g fuera de 1, o muchos cambios de sentido del VS con golpe.
        /// </summary>
        private bool IsTurbulent()
        {
            int gDeviations = 0;
            for (int i = 0; i < _window.Count; i++)
                if (Math.Abs(_window[i].G - 1.0) >= GDeviation) gDeviations++;

            if (gDeviations >= MinGDeviations) return true;

            int reversals = 0;
            double? previousDelta = null;
            for (int i = 1; i < _window.Count; i++)
            {
                double delta = _window[i].Vs - _window[i - 1].Vs;
                if (Math.Abs(delta) < VsJerkFpm) continue;

                if (previousDelta.HasValue &&
                    previousDelta.Value != 0 &&
                    Math.Sign(delta) != Math.Sign(previousDelta.Value))
                    reversals++;

                previousDelta = delta;
            }

            return reversals >= MinReversals;
        }
    }
}
