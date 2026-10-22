using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Cuenta **una vez cada excursión** fuera de tolerancia, no cada muestra que la incumple.
    ///
    /// Existe por el PIREP real **`5ow69vzDNW5ZMayB`** (SKRG, 21/10/2026). La desviación del localizador
    /// bajo 500 ft se evaluaba en cada ciclo de 20 Hz y sumaba un punto por ciclo (tope 2), así que
    /// **un solo apartamiento de −5,1° que duró un segundo y medio agotó el tope entero**:
    ///
    /// ```
    /// 20:25:12  ⚠️ LOCALIZADOR: rumbo 355° desvía -5,1° del CRS ILS 0°
    /// 20:25:13  ⚠️ LOCALIZADOR: rumbo 355° desvía -5,1° del CRS ILS 0°
    /// 20:25:14  (ya no: el tope de 2 estaba gastado)
    /// …
    /// −2 pts: Alineación de Localizador (2 alignment deviation(s) below 500 ft)
    /// ```
    ///
    /// Dos líneas de la **misma** desviación, y −2 en un aterrizaje de −126 fpm. La regla correcta es
    /// por **situación**: una desviación se cuenta al entrar, y el contador solo se re-arma cuando el
    /// avión **vuelve** dentro de la tolerancia —así dos apartamientos separados siguen siendo dos—.
    ///
    /// Además se exige **0,5 s** fuera de tolerancia antes de contar, para que un frame suelto del
    /// simulador no gaste una desviación. Es reloj inyectado: se prueba sin esperar.
    /// </summary>
    internal sealed class ExcursionCounter
    {
        /// <summary>Tiempo mínimo fuera de tolerancia para dar la excursión por real.</summary>
        internal const double MinDurationSec = 0.5;

        private DateTime? _startedAt;
        private bool      _counted;

        /// <summary>
        /// Un muestreo. Devuelve <c>true</c> **una sola vez por excursión**, cuando lleva
        /// <see cref="MinDurationSec"/> fuera de tolerancia. Volver dentro re-arma el contador.
        /// </summary>
        internal bool Update(DateTime now, bool outOfTolerance)
        {
            if (!outOfTolerance)
            {
                _startedAt = null;      // dentro de tolerancia: la próxima desviación es nueva
                _counted   = false;
                return false;
            }

            if (!_startedAt.HasValue)
            {
                _startedAt = now;
                return false;
            }

            if (_counted) return false;
            if ((now - _startedAt.Value).TotalSeconds < MinDurationSec) return false;

            _counted = true;
            return true;
        }

        /// <summary>Vuelo o aproximación nueva: ni excursión ni re-armado del anterior.</summary>
        internal void Reset()
        {
            _startedAt = null;
            _counted   = false;
        }
    }
}
