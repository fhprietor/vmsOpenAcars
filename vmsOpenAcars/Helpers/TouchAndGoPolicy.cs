using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Cuándo un avión que acaba de aterrizar está volviendo a volar**, y cuándo sólo está corriendo
    /// por la pista después de tomar.
    ///
    /// La condición que había era `GS > 60 && SecondsSinceTouchdown >= 5`, y con eso se declaró un
    /// touch-and-go **inexistente** en el PIREP real **`RBmrBApLOjWlYmO2`** (SKSJ→SKSV, BE58 de
    /// Carenado en X-Plane, `vmsOpenACars/0.9.38`): el avión tocó a **03:35:57** con **96 kt** y cinco
    /// segundos después, en plena carrera de aterrizaje, iba a **88 kt** —y el simulador publicó un
    /// contacto con el suelo intermitente—. La máquina lo leyó como un despegue, saltó a `Climb` y
    /// **nunca volvió al suelo**: `OnBlock` sólo se alcanza desde `TaxiIn`, así que el piloto se quedó
    /// sin poder enviar el PIREP y lo único que le ofrecía la aplicación era cancelar, que lo borra.
    ///
    /// **La velocidad no distingue** las dos cosas: un bimotor ligero rueda a 88 kt después de tomar
    /// igual que despega a 88 kt. Lo que las distingue es **la tasa vertical**: en una carrera de
    /// aterrizaje el avión baja o se sostiene —la muestra que disparó el falso touch-and-go marcaba
    /// **−178 fpm**—, y en un despegue de verdad sube. Por eso la regla exige ahora **ascenso
    /// sostenido**, y no un ciclo suelto.
    ///
    /// **Sigue sin sustituir a nada**: la condición vieja sigue dentro (velocidad, tiempo desde el
    /// toque), sólo que ya no decide sola.
    /// </summary>
    internal static class TouchAndGoPolicy
    {
        /// <summary>Velocidad en tierra mínima: por debajo no se está despegando, se está rodando.</summary>
        internal const double MinGroundSpeedKt = 60.0;

        /// <summary>
        /// Segundos mínimos desde el toque. Por debajo de eso cualquier cosa que se vea es todavía la
        /// carrera de aterrizaje.
        /// </summary>
        internal const double MinSecondsSinceTouchdown = 5.0;

        /// <summary>
        /// Ascenso mínimo, en pies por minuto, para aceptar que el avión **está subiendo**. Un avión
        /// que acaba de tomar y rueda no sostiene 200 fpm hacia arriba; uno que despega, sí.
        /// </summary>
        internal const double MinClimbFpm = 200.0;

        /// <summary>
        /// Segundos que tiene que sostenerse el ascenso para decidir. Tres segundos son sesenta ciclos
        /// de sondeo: no es un pico de turbulencia ni un rebote, es una intención.
        /// </summary>
        internal const double ConfirmSeconds = 3.0;

        /// <summary>
        /// ¿Este ciclo, por sí solo, parece un touch-and-go? Todas las condiciones a la vez: en fase de
        /// rodaje de llegada, **en el aire**, por encima de la velocidad mínima, pasado el margen desde
        /// el toque y **subiendo de verdad**.
        /// </summary>
        internal static bool Indicates(FlightPhase phase, bool isOnGround, double groundSpeedKt,
                                       double verticalSpeedFpm, double secondsSinceTouchdown)
            => (phase == FlightPhase.AfterLanding || phase == FlightPhase.TaxiIn)
            && !isOnGround
            && groundSpeedKt      >  MinGroundSpeedKt
            && secondsSinceTouchdown >= MinSecondsSinceTouchdown
            && verticalSpeedFpm   >= MinClimbFpm;

        /// <summary>¿Lleva suficientes segundos sosteniéndose como para declararlo?</summary>
        internal static bool Confirmed(double sustainedSeconds)
            => sustainedSeconds >= ConfirmSeconds;
    }
}
