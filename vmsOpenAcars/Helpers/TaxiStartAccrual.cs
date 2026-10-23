namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El rodaje de salida empieza a contar cuando el avión se mueve, no cuando la fase lo dice.**
    ///
    /// El caso que lo obliga es el PIREP real `rMBVEKj5PZzx0pX9` (SKRG→SKSM, A321 de Fenix,
    /// 22/10/2026): el avión empezó a rodar a las **18:28:41** (GS 6 kt subiendo a 17, 22 y 24) y
    /// `TAXI OUT` no se declaró hasta las **18:30:31** — **110 s** de rodaje real fuera de la fase—,
    /// porque en ese vuelo **no hubo ningún evento de freno de parqueo** (el offset `0x0BC8` documenta
    /// `0 = off, 32767 = on`) y la transición la hizo el **rescate de 300 s**: `PUSHBACK` entró a las
    /// 18:25:32 y `TAXI OUT` salió **299 s** después. Esos 110 s no contaban ni para el tiempo en
    /// movimiento ni, por tanto, para la bonificación de motor único.
    ///
    /// **La regla**: mientras el avión está en `Pushback`, lo que ruede se acumula en un **pendiente**
    /// y se vuelca a `TaxiOut` en cuanto la fase se declara. El pendiente **se pone a cero en cada
    /// parada**, así que cuenta desde la última vez que el avión estuvo quieto — que es justo lo que
    /// pidió el piloto— y no desde el principio del empuje: el remolque anterior a la parada no entra,
    /// y además no podría entrar porque <see cref="SingleEngineTaxiPolicy.CountsAsMoving"/> exige un
    /// motor en marcha, y durante el remolque suelen estar los dos apagados.
    ///
    /// **Por qué no se retrasa la fase en sí**: `Block Off` (que alimenta el criterio de salida a
    /// tiempo) y las líneas del registro se escriben con la hora de la transición. Mover la fase hacia
    /// atrás haría que el PIREP dijera que el avión salió del puesto 110 s antes de que se moviera.
    /// Solo se recupera la **cuenta de la métrica**, que es donde el piloto tenía razón.
    /// </summary>
    internal sealed class TaxiStartAccrual
    {
        /// <summary>Segundos en movimiento desde la última parada (con uno o con dos motores).</summary>
        internal double MovingSeconds { get; private set; }

        /// <summary>De esos, los hechos con **un solo** motor.</summary>
        internal double SingleEngineSeconds { get; private set; }

        /// <summary>Metros recorridos en ese mismo tramo, para el total que se publica en el registro.</summary>
        internal double Metres { get; private set; }

        /// <summary>Ya se volcó (o se cerró) en este vuelo: no se vuelve a pagar dos veces.</summary>
        internal bool Consumed { get; private set; }

        /// <summary>
        /// Un ciclo **antes** de que la fase sea `TaxiOut`. Si el avión no se está moviendo, la racha
        /// se reinicia: lo que importa es el tramo continuo que acaba en la transición.
        /// </summary>
        internal void Feed(double dtSeconds, double metres, bool moving, bool oneEngineOnly)
        {
            if (Consumed) return;

            if (!moving)
            {
                MovingSeconds       = 0.0;
                SingleEngineSeconds = 0.0;
                Metres              = 0.0;
                return;
            }

            if (dtSeconds <= 0.0) return;

            MovingSeconds += dtSeconds;
            Metres        += metres;
            if (oneEngineOnly) SingleEngineSeconds += dtSeconds;
        }

        /// <summary>
        /// Lo acumulado, **una sola vez por vuelo**. Devuelve `false` si no hay nada que volcar (nunca
        /// se movió antes de la transición), que es el caso normal de un rodaje que empieza ya en
        /// `TaxiOut`.
        /// </summary>
        internal bool Take(out double movingSeconds, out double singleEngineSeconds, out double metres)
        {
            if (Consumed || MovingSeconds <= 0.0)
            {
                Consumed            = true;
                movingSeconds       = 0.0;
                singleEngineSeconds = 0.0;
                metres              = 0.0;
                return false;
            }

            movingSeconds       = MovingSeconds;
            singleEngineSeconds = SingleEngineSeconds;
            metres              = Metres;

            MovingSeconds       = 0.0;
            SingleEngineSeconds = 0.0;
            Metres              = 0.0;
            Consumed            = true;
            return true;
        }

        /// <summary>Vuelo nuevo: se vuelve a acumular.</summary>
        internal void Reset()
        {
            MovingSeconds       = 0.0;
            SingleEngineSeconds = 0.0;
            Metres              = 0.0;
            Consumed            = false;
        }
    }
}
