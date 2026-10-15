using System;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El avión que no se mueve no está volando.** Detecta la fase de vuelo que ya no se corresponde
    /// con la telemetría: la máquina quedó en `Takeoff`/`Climb`/`Enroute`/`Descent` y el avión lleva un
    /// rato rodando o aparcado.
    ///
    /// El caso real que lo obliga es el PIREP **`RBmrBApLOjWlYmO2`** (SKSJ→SKSV, BE58 de Carenado en
    /// X-Plane, `vmsOpenACars/0.9.38`). Un rebote en la toma hizo que el **touch-and-go se declarara 5 s
    /// después del toque** (GS 88 kt, `SecondsSinceTouchdown >= 5`, en `HandleAirPhases`), la fase
    /// saltó a `Climb` y **nunca volvió**: desde `Climb` no hay camino al suelo, `OnBlock` sólo se
    /// alcanza desde `TaxiIn` y el piloto se quedó **sin poder enviar el PIREP** —lo único que le
    /// quedaba era cancelar, que lo borra—. Su propia traza lo delata: de **03:36:06 a 03:39:19** el
    /// cliente siguió diciendo `ICL` (en vuelo) con el avión a **GS 6–21 kt** y el **MSL constante en
    /// 873–880 ft**, con el freno de parqueo puesto.
    ///
    /// **No se fía de la bandera de «en el suelo»**, y es deliberado: es justamente la que falló —el
    /// paso a «aire» en `FsuipcService.DetectGroundTransition` no exige confirmación ninguna—, así que
    /// la regla se apoya en lo que el avión **hace**: por debajo de 40 kt y sin tasa vertical, ninguna
    /// de esas cuatro fases es posible.
    ///
    /// **No sustituye a ninguna transición**: sólo dice «esto no cuadra». El rescate de la fase es el
    /// paso siguiente y reutiliza <see cref="ShouldWarn"/> con el tiempo sostenido.
    /// </summary>
    internal static class StuckAirPhasePolicy
    {
        /// <summary>Por debajo de esta velocidad en tierra no se está volando, se está rodando.</summary>
        internal const double StoppedGroundSpeedKt = 40.0;

        /// <summary>
        /// Tasa vertical máxima (en valor absoluto) que se acepta como «no está volando». Un ascenso o
        /// un descenso de verdad no se sostiene a ±100 fpm; una aeronave de ala rotatoria sí, pero este
        /// cliente clasifica helicópteros y no los lleva por estas cuatro fases.
        /// </summary>
        internal const double MaxVerticalSpeedFpm = 100.0;

        /// <summary>
        /// Cuánto tiene que durar para no reaccionar a un dato suelto. Quince segundos son trescientos
        /// ciclos de sondeo: no es un parpadeo, es un avión parado.
        /// </summary>
        internal const double MinSeconds = 15.0;

        /// <summary>¿Es una fase en la que el avión tiene que estar en el aire?</summary>
        internal static bool IsAirPhase(FlightPhase phase)
            => phase == FlightPhase.Takeoff
            || phase == FlightPhase.Climb
            || phase == FlightPhase.Enroute
            || phase == FlightPhase.Descent;

        /// <summary>
        /// **¿El avión está en el suelo, según lo que hace?** Por debajo de 40 kt y sin tasa vertical
        /// no se vuela: ni un avión rodando ni uno aparcado. Es la comprobación que **no** depende de la
        /// bandera del simulador, que es justo la que falló en `RBmrBApLOjWlYmO2` —publicó «en el aire»
        /// con el avión aparcado durante casi tres minutos—.
        ///
        /// El límite de tasa vertical es lo que separa «en el suelo» de «volando despacio»: una pérdida
        /// real baja a más de −100 fpm, y un avión en el suelo no sostiene ±100 fpm.
        /// </summary>
        internal static bool LooksOnGround(double groundSpeedKt, double verticalSpeedFpm)
            => groundSpeedKt < StoppedGroundSpeedKt
            && Math.Abs(verticalSpeedFpm) <= MaxVerticalSpeedFpm;

        /// <summary>
        /// ¿La fase no se corresponde con lo que hace el avión? Fase de vuelo y el avión en el suelo:
        /// las dos cosas a la vez.
        /// </summary>
        internal static bool IsStuck(FlightPhase phase, double groundSpeedKt, double verticalSpeedFpm)
            => IsAirPhase(phase) && LooksOnGround(groundSpeedKt, verticalSpeedFpm);

        /// <summary>¿Ya lleva suficiente tiempo así como para decirlo (o para rescatarlo)?</summary>
        internal static bool ShouldWarn(double sustainedSeconds)
            => sustainedSeconds >= MinSeconds;
    }
}
