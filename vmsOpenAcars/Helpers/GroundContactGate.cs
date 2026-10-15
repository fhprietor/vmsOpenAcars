namespace vmsOpenAcars.Helpers
{
    /// <summary>Qué toca hacer con este frame de la bandera de suelo del simulador.</summary>
    internal enum GroundContactStep
    {
        /// <summary>Nada: el estado confirmado no ha cambiado (o el frame era un parpadeo).</summary>
        None,
        /// <summary>Toque confirmado (aire → suelo).</summary>
        Touchdown,
        /// <summary>Despegue confirmado (suelo → aire).</summary>
        Liftoff,
    }

    /// <summary>
    /// **Cuándo se da por bueno un cambio de la bandera de suelo del simulador.** Un solo frame no es
    /// una medida, y el propio código ya lo sabía a medias: junto a las constantes de detección está
    /// escrito que *«el sim reporta OnGround=0 brevemente tras el primer contacto»*, y hay un debounce
    /// de **10 s** para el *toque* que evita registrarlo dos veces.
    ///
    /// Lo que faltaba era la otra mitad: ese mismo `OnGround=0` breve **también** daba por hecho el
    /// despegue —`HandleTakeoff` lo descarta por su cuenta, porque ignora cualquier liftoff dentro de
    /// los 30 s de un toque, pero **`_lastOnGround` sí se ponía en falso**— y esa bandera es la que
    /// viaja en `RawTelemetryData.IsOnGround` hasta la máquina de fases. De ahí salió el touch-and-go
    /// inexistente del PIREP real **`RBmrBApLOjWlYmO2`**: a las 03:36:02, cinco segundos después de
    /// tomar y a 88 kt, la máquina creyó que el avión estaba en el aire y se fue a `Climb` para no
    /// volver, dejando al piloto sin poder enviar el PIREP.
    ///
    /// **Se confirman las dos direcciones**, no solo el paso a tierra: tres frames (150 ms a 20 Hz) es
    /// imperceptible para un registro de toque y suficiente para que un parpadeo no mueva nada.
    /// </summary>
    internal sealed class GroundContactGate
    {
        /// <summary>Frames seguidos que tiene que publicar el simulador para dar el cambio por bueno.</summary>
        internal const int ConfirmFrames = 3;

        private int  _groundFrames;
        private int  _airFrames;
        private bool _onGround = true;

        /// <summary>El estado **confirmado**: en tierra, salvo que el simulador insista en lo contrario.</summary>
        internal bool IsOnGround => _onGround;

        /// <summary>
        /// Un frame del simulador. Devuelve el cambio confirmado, o <see cref="GroundContactStep.None"/>
        /// si no hay ninguno (incluido el caso del parpadeo: unos frames en falso que vuelven).
        /// </summary>
        internal GroundContactStep Update(bool rawOnGround)
        {
            if (rawOnGround)
            {
                _airFrames = 0;
                if (_onGround) return GroundContactStep.None;

                _groundFrames++;
                if (_groundFrames < ConfirmFrames) return GroundContactStep.None;

                _groundFrames = 0;
                _onGround     = true;
                return GroundContactStep.Touchdown;
            }

            _groundFrames = 0;
            if (!_onGround) return GroundContactStep.None;

            _airFrames++;
            if (_airFrames < ConfirmFrames) return GroundContactStep.None;

            _airFrames = 0;
            _onGround  = false;
            return GroundContactStep.Liftoff;
        }
    }
}
