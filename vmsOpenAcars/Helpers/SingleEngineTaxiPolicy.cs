using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>Por qué el criterio de motor único dio lo que dio.</summary>
    internal enum SingleEngineTaxiOutcome
    {
        /// <summary>Cumple el mínimo: hay bonificación.</summary>
        Awarded,
        /// <summary>Rodó con un solo motor, pero por debajo del mínimo.</summary>
        BelowThreshold,
        /// <summary>Hubo rodaje, pero nunca con un solo motor.</summary>
        NoSingleEngine,
        /// <summary>Nunca se vieron los dos motores en marcha: no hay nada que acreditar.</summary>
        EnginesNotSeen,
    }

    /// <summary>Las cifras del criterio y su veredicto.</summary>
    internal sealed class SingleEngineTaxiVerdict
    {
        /// <summary>Segundos en movimiento contados en la fase (con uno o con dos motores).</summary>
        internal double MovingSeconds { get; set; }
        /// <summary>De esos, los que se hicieron con **un solo** motor.</summary>
        internal double SingleEngineSeconds { get; set; }
        /// <summary>El porcentaje redondeado, solo para enseñarlo: la decisión no usa el redondeo.</summary>
        internal int Percent { get; set; }
        internal SingleEngineTaxiOutcome Outcome { get; set; }

        internal bool Awarded => Outcome == SingleEngineTaxiOutcome.Awarded;
    }

    /// <summary>
    /// **El criterio de la bonificación de motor único**, en un solo sitio: qué cuenta como tiempo
    /// válido y a partir de qué porcentaje se otorga.
    ///
    /// Hasta v0.9.42 la regla estaba escrita **dos veces** —una en la entrada en pista para el rodaje
    /// de salida y otra en `OnBlock` para el de llegada— comparando **ciclos de sondeo** en vez de
    /// tiempo, y el veredicto negativo no dejaba **ninguna** línea en el log: el piloto del PIREP real
    /// `lA4ZVAzgxYzlG4vD` rodó **290 s de 630** con un solo motor (**46 %**) y no tuvo forma de saber
    /// por qué no cobró. La regla no cambia —sigue siendo el **50 %** del tiempo en movimiento de la
    /// fase—, pero ahora hay una sola copia, se mide en segundos (una unidad que se puede enseñar) y
    /// el veredicto se publica siempre, se otorgue o no.
    ///
    /// **El reloj del equipo no es de fiar**: si el portátil se suspende a mitad de rodaje, el hueco
    /// entre dos ciclos puede ser de horas. Ese tramo no se cuenta (lo decide
    /// <see cref="SampleClock.CountableSeconds"/>, compartido con el contador de fase atascada),
    /// porque un minuto inventado de motor único vale una bonificación que no se voló.
    /// </summary>
    internal static class SingleEngineTaxiPolicy
    {
        /// <summary>Porcentaje mínimo del tiempo en movimiento que hay que hacer con un solo motor.</summary>
        internal const int MinPercent = 50;

        /// <summary>
        /// ¿Llega al mínimo? Se compara sin redondear: 49,6 % no es el 50 %. Y sin movimiento no hay
        /// nada que acreditar, diga lo que diga el cociente.
        /// </summary>
        internal static bool Meets(double movingSeconds, double singleEngineSeconds)
            => movingSeconds > 0.0 && singleEngineSeconds * 100.0 >= movingSeconds * MinPercent;

        /// <summary>
        /// Metros que hay que recorrer en la ventana de <see cref="GroundMotion.WindowSeconds"/> para
        /// considerar que el avión se mueve: 8 m en 5 s son ~3 kt, el mismo umbral de siempre, pero
        /// medido con desplazamiento.
        /// </summary>
        internal const double MinMovingMetres = 8.0;

        /// <summary>
        /// **¿Este ciclo cuenta como rodaje en movimiento?**
        ///
        /// Dos condiciones, y las dos con su caso real detrás:
        ///
        /// 1. **Que se haya movido de verdad** (`windowMetres` viene de <see cref="GroundMotion"/>). El
        ///    vuelo `29Z3A8r748rGPnzq` (SKRG, 20/10/2026) tenía el avión aparcado haciendo **60 m en
        ///    ocho minutos** mientras el simulador publicaba **GS 4-7 kt** e **IAS 0**: esas filas
        ///    entraron como rodaje y Falsearon el veredicto en los dos sentidos (243 s de más en el
        ///    denominador, ~90 s de regalo en el numerador).
        /// 2. **Que haya un motor en marcha.** Un avión no rueda por su cuenta con todo apagado: eso es
        ///    el remolque del pushback o ruido, y contarlo restaba como si se hubiera rodado con dos
        ///    motores (cero motores no es «uno»).
        /// </summary>
        internal static bool CountsAsMoving(double windowMetres, bool eng1Running, bool eng2Running)
            => windowMetres >= MinMovingMetres && (eng1Running || eng2Running);

        /// <summary>
        /// El veredicto con sus cifras. `bothEnginesRunning` entra en la decisión porque la
        /// bonificación premia rodar con uno **teniendo dos**: sin haber visto nunca los dos en
        /// marcha no hay nada que premiar, y decirlo es la diferencia entre «no lo hiciste» y «no te
        /// vi los motores».
        /// </summary>
        internal static SingleEngineTaxiVerdict Evaluate(
            double movingSeconds, double singleEngineSeconds, bool bothEnginesRunning)
        {
            var verdict = new SingleEngineTaxiVerdict
            {
                MovingSeconds       = movingSeconds,
                SingleEngineSeconds = singleEngineSeconds,
                // **Se trunca, no se redondea**, y no es un detalle estético: con redondeo un 49,6 %
                // se publicaría como «50 % · mínimo 50 % · sin bonificación», que se lee como una
                // contradicción. Truncando, el porcentaje que se enseña es coherente con el veredicto
                // en los dos sentidos: se otorga si y solo si el número enseñado llega a 50.
                Percent             = movingSeconds > 0.0
                                          ? (int)(singleEngineSeconds * 100.0 / movingSeconds)
                                          : 0,
            };

            if (!bothEnginesRunning)                            verdict.Outcome = SingleEngineTaxiOutcome.EnginesNotSeen;
            else if (singleEngineSeconds <= 0.0)                verdict.Outcome = SingleEngineTaxiOutcome.NoSingleEngine;
            else if (Meets(movingSeconds, singleEngineSeconds))  verdict.Outcome = SingleEngineTaxiOutcome.Awarded;
            else                                                verdict.Outcome = SingleEngineTaxiOutcome.BelowThreshold;

            return verdict;
        }

        /// <summary>
        /// **El criterio sobre el rodaje completo**: las dos fases sumadas en un solo porcentaje
        /// (`Ola 4`). Un rodaje de salida corto con un motor y uno de llegada largo con un motor pasan
        /// juntos aunque ninguno de los dos llegue al 50 % por su cuenta.
        ///
        /// **Es un camino que se AÑADE, no uno que sustituye**: el aprobado por fase sigue en pie
        /// (<see cref="Evaluate"/>), porque si la suma fuera la única regla quitaría la bonificación a
        /// quien hoy la cobra —un rodaje de salida con un motor al 60 % y una llegada con dos motores
        /// baja al 35 % sumado—. Quitar lo que ya se paga no es «relajar», es lo contrario, y se decide
        /// con el corpus delante.
        /// </summary>
        internal static SingleEngineTaxiVerdict EvaluateWholeTaxi(
            double taxiOutMovingSeconds, double taxiOutSingleEngineSeconds,
            double taxiInMovingSeconds,  double taxiInSingleEngineSeconds,
            bool bothEnginesRunning)
            => Evaluate(taxiOutMovingSeconds + taxiInMovingSeconds,
                        taxiOutSingleEngineSeconds + taxiInSingleEngineSeconds,
                        bothEnginesRunning);

        /// <summary>Clave de idioma del sufijo que explica el veredicto en el log.</summary>
        internal static string OutcomeKey(SingleEngineTaxiOutcome outcome)
        {
            switch (outcome)
            {
                case SingleEngineTaxiOutcome.Awarded:        return "SETaxi_Awarded";
                case SingleEngineTaxiOutcome.BelowThreshold: return "SETaxi_Below";
                case SingleEngineTaxiOutcome.NoSingleEngine: return "SETaxi_NoMove";
                default:                                     return "SETaxi_NoEngines";
            }
        }
    }
}
