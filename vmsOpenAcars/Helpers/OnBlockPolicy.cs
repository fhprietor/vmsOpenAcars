using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Cuándo el vuelo está cerrado en el puesto**: cuántos segundos parado hacen falta para
    /// declarar `OnBlock`.
    ///
    /// La regla vieja exigía **90 s parado** con los motores apagados (o en hotel mode). En el PIREP
    /// real **`ez9OqzRnYxbnGLR8`** eso dio un cierre tardío de manual:
    ///
    /// ```
    /// 02:37:45  Parking Brake: SET
    /// 02:38:09  Engines shutdown
    /// 02:39:12  ON BLOCK        ← 87 s después del freno, 63 s después de parar los motores
    /// ```
    ///
    /// Con el **freno de parqueo puesto y todos los motores parados** el vuelo está cerrado: el
    /// avión no se va a mover más, no queda nada que medir y el piloto espera para poder enviar el
    /// PIREP. Ahí bastan **5 s**. Los 90 s siguen siendo el plazo del caso menos concluyente (motores
    /// apagados sin freno puesto, o hotel mode): es la detección de siempre y **no se toca**.
    ///
    /// **Los otros dos caminos de cierre no pasan por aquí** y siguen intactos: el `ForceOnBlock`
    /// manual del piloto y el rescate de fase (`StuckAirPhasePolicy`).
    /// </summary>
    internal static class OnBlockPolicy
    {
        /// <summary>
        /// Segundos parado cuando el avión está **aparcado del todo** (freno puesto **y** motores
        /// parados). En el vuelo real el freno se puso 24 s antes de apagar los motores: con este
        /// plazo el cierre llega en el mismo ciclo del apagado, no 63 s después.
        /// </summary>
        internal const double ParkedSeconds = 5.0;

        /// <summary>Segundos parado de la detección de siempre, cuando el estado no es concluyente.</summary>
        internal const double NormalSeconds = 90.0;

        /// <summary>
        /// ¿Está el avión en el puesto con todo cerrado? **Los motores tienen que estar parados** (o
        /// en hotel mode): con un motor en marcha no se cierra el vuelo por mucho freno que haya.
        /// </summary>
        internal static bool IsParked(bool enginesRunning, bool hotelModeActive)
            => !enginesRunning || hotelModeActive;

        /// <summary>El plazo que le toca a este estado: 5 s si además está el freno puesto, 90 s si no.</summary>
        internal static double RequiredSeconds(bool parkingBrakeSet)
            => parkingBrakeSet ? ParkedSeconds : NormalSeconds;

        /// <summary>
        /// **La decisión**: ¿toca declarar `OnBlock` con este estado y estos segundos parado?
        /// </summary>
        internal static bool ShouldClose(double stoppedSeconds,
                                         bool enginesRunning,
                                         bool hotelModeActive,
                                         bool parkingBrakeSet)
        {
            if (!IsParked(enginesRunning, hotelModeActive)) return false;
            return stoppedSeconds >= RequiredSeconds(parkingBrakeSet);
        }
    }
}
