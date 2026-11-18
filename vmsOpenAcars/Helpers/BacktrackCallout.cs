using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Un aviso de backtrack por maniobra**: la misma entrada en pista no se anuncia dos veces.
    ///
    /// En el PIREP real **`ez9OqzRnYxbnGLR8`** la maniobra de retroceso en la 17 salió **dos veces**,
    /// `↩️ BACKTRACK RWY 17` a las **02:26:15** y otra vez a las **02:26:39**. No eran dos
    /// maniobras: era **una**. El aviso vive en dos ramas del seguimiento de rodaje —la de salida
    /// (entrada en pista) y la de llegada (retroceso para despejar)— y cada una llevaba su propia
    /// bandera (`_wasOnRunwayForEntry` y `_wasOnRunwayForExit`); cuando la fase cambiaba a `TaxiIn`
    /// estando el avión todavía en la pista, la segunda rama veía «entrada nueva» y volvía a avisar.
    ///
    /// El estado es uno solo y se re-arma **saliendo de la pista**, que es lo que separa una
    /// maniobra de la siguiente: dos entradas distintas, dos avisos; una entrada, uno.
    /// </summary>
    internal sealed class BacktrackCallout
    {
        private bool _announced;

        /// <summary>
        /// ¿Toca anunciar el backtrack **ahora**? Devuelve `true` una sola vez por entrada en pista,
        /// y solo si la entrada es un retroceso. Estar en la pista sin retroceder no consume el
        /// aviso: si el avión gira y retrocede después, se anuncia.
        /// </summary>
        internal bool ShouldAnnounce(bool confirmedOnRunway, bool isBacktrack)
        {
            if (!confirmedOnRunway)
            {
                _announced = false;      // fuera de la pista: la próxima entrada es una maniobra nueva
                return false;
            }
            if (!isBacktrack || _announced) return false;

            _announced = true;
            return true;
        }

        /// <summary>Vuelo nuevo: no se arrastra la maniobra anterior.</summary>
        internal void Reset() => _announced = false;
    }
}
