using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Un cambio de flaps solo se anuncia cuando el detente se asienta, no cuando el simulador
    /// tiembla.** En el PIREP real `l5QVxBEmJ4yOaY2O` (B738 SSW, MSFS 2024) la aproximación escribió
    /// **siete líneas en seis segundos** oscilando entre 62 %, 75 % y 87 %:
    ///
    /// ```
    /// 01:49:27  62% → 87%
    /// 01:49:28  87% → 62%
    /// 01:49:29  62% → 87%
    /// 01:49:29  87% → 75%
    /// 01:49:31  75% → 87%
    /// 01:49:32  87% → 75%
    /// 01:49:33  75% → 87%
    /// ```
    ///
    /// Y el despegue, **doce en quince segundos**, con saltos imposibles (`0 % → 38 %` y
    /// `38 % → 13 %` en el mismo segundo). El piloto movió la palanca **una vez**. La causa no era el
    /// detector de FSUIPC —que ya agrupaba por bandas— sino que la línea del log salía **en cuanto**
    /// la banda cambiaba (`TelemetryCoordinator.OnFlapsChanged`), sin esperar a que el valor se
    /// quedara quieto: un addon que publica el mando con temblor de un punto escribe una transición
    /// por cada temblor.
    ///
    /// **El portero exige dos cosas, y las dos son necesarias.** (1) El valor tiene que permanecer
    /// en el **mismo detente** durante <see cref="SettleSeconds"/>; (2) ese detente tiene que ser
    /// **distinto del último anunciado**. Se compara por detente (`FlapsScale.BandKey`), no por
    /// porcentaje, porque el mando se mueve en pasos de 1/16 383 y comparar el número directo
    /// escribiría una línea por punto.
    ///
    /// **Por qué 1,5 s.** En las dos secuencias reales los falsos sostenidos duran entre 0,5 y 2 s;
    /// con 1,5 s, el único tramo que llega a asentarse sin ser el detente ya anunciado es el 75 %
    /// del 01:49:29–01:49:31 (banda 6, la misma del 62 % que ya estaba publicada), así que la
    /// secuencia de siete transiciones queda en **una** línea: el 87 % final. En el despegue, el
    /// 13 % que se sostiene entre 00:59:49 y 00:59:53 sí supera el umbral y deja una segunda línea;
    /// el resultado son **dos**, no doce. Un umbral más corto (1 s) volvería a escribir el temblor
    /// de la aproximación; uno más largo (2 s) retrasaría un cambio real de detente —que el piloto
    /// mantiene hasta el toque— sin ganar nada.
    ///
    /// **El «desde» es el último anunciado, no el último leído (v0.9.80).** En el PIREP
    /// `YxzZxO92Qnbxlqla` (B738 SSW, MSFS 2024, MPTO→SKCG) la línea de las 03:36:39 decía
    /// `75% → 87%` cuando lo último anunciado había sido el **62 %**, y la de las 03:40:47 decía
    /// `62% → 50%` cuando lo último anunciado había sido el **75 %**. La causa: al asentarse un
    /// valor en el **mismo** detente ya anunciado se guardaba su porcentaje exacto como «desde», y
    /// 62 % y 75 % caen en la misma banda 6. Ese valor exacto solo puede afinar el **destino**.
    /// </summary>
    internal sealed class FlapTransitionGate
    {
        /// <summary>
        /// Tiempo que el valor tiene que quedarse en el mismo detente para contar como asentado.
        /// El porqué de 1,5 s está en el comentario de la clase, con las dos secuencias medidas.
        /// </summary>
        internal const double SettleSeconds = 1.5;

        private bool     _seeded;
        private int      _lastAnnouncedBand;
        private double   _lastAnnouncedPercent;
        private int      _candidateBand;
        private double   _candidatePercent;
        private DateTime _candidateSinceUtc;

        /// <summary>
        /// Alimenta el portero con la lectura cruda del mando y el instante de la lectura. Devuelve
        /// la transición **asentada** que hay que escribir, o `null` si todavía no hay nada que
        /// anunciar. Hay que llamarlo en cada ciclo de telemetría (20 Hz), no solo cuando el evento
        /// de cambio dispara: si no, el último detente —el que se queda quieto— no se anunciaría
        /// nunca.
        /// </summary>
        internal FlapTransition? Update(double percent, DateTime nowUtc)
        {
            int band = FlapsScale.BandKey(percent);

            // Primera lectura: siembra el estado. No es un cambio que contarle al piloto —el avión
            // puede estar aparcado con los flaps ya en una posición— y además da el «desde» del
            // primer cambio real.
            if (!_seeded)
            {
                _seeded                = true;
                _lastAnnouncedBand     = band;
                _lastAnnouncedPercent  = percent;
                _candidateBand         = band;
                _candidatePercent      = percent;
                _candidateSinceUtc     = nowUtc;
                return null;
            }

            // Cada temblor reinicia el reloj del candidato: lo que se anuncia es el detente que se
            // queda, no el pico que pasa.
            if (band != _candidateBand)
            {
                _candidateBand     = band;
                _candidatePercent  = percent;
                _candidateSinceUtc = nowUtc;
                return null;
            }

            // Mismo detente: se guarda el último valor exacto para que la línea publique el número
            // asentado y no el primero que llegó a esa banda. **Ese valor afina solo el destino**:
            // el «desde» sigue siendo `_lastAnnouncedPercent` (ver más abajo).
            _candidatePercent = percent;

            if ((nowUtc - _candidateSinceUtc).TotalSeconds < SettleSeconds) return null;

            // Asentado en el detente que ya se anunció: no hay línea y **no se toca el «desde»**.
            // Guardar aquí el valor exacto contaminaba el origen con un número que nunca se publicó:
            // 62 % y 75 % son la misma banda 6, y en `YxzZxO92Qnbxlqla` eso hizo que la línea de las
            // 03:40:47 dijera `62% → 50%` cuando lo anunciado era el 75 %.
            if (band == _lastAnnouncedBand)
                return null;

            var transition = new FlapTransition(_lastAnnouncedPercent, _candidatePercent);
            _lastAnnouncedBand    = band;
            _lastAnnouncedPercent = _candidatePercent;
            return transition;
        }

        /// <summary>Vuelve al estado de siembra (arranque de vuelo o reconexión).</summary>
        internal void Reset()
        {
            _seeded                = false;
            _lastAnnouncedBand     = 0;
            _lastAnnouncedPercent  = 0.0;
            _candidateBand         = 0;
            _candidatePercent      = 0.0;
            _candidateSinceUtc     = default(DateTime);
        }
    }

    /// <summary>La transición asentada: el último valor publicado y el nuevo.</summary>
    internal struct FlapTransition
    {
        internal FlapTransition(double previousPercent, double percent)
        {
            PreviousPercent = previousPercent;
            Percent         = percent;
        }

        internal double PreviousPercent { get; }
        internal double Percent         { get; }
    }
}
