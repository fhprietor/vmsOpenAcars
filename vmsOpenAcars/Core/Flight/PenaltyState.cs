namespace vmsOpenAcars.Core.Flight
{
    internal sealed class PenaltyState
    {
        public int  QnhViolations;
        public bool IsOfflineFlight;
        public bool DepartedLate;
        public int  ProcedureSpdViolations;
        public bool SingleEngineTaxiDetected;
        public bool BothEnginesRunning;
        // Tiempo de rodaje **en segundos**, no en ciclos de sondeo: el ciclo es una unidad interna
        // (20 Hz hoy) que no se le puede enseñar al piloto ni significa nada fuera del bucle. El
        // criterio (`Helpers/SingleEngineTaxiPolicy.cs`) compara estos segundos con el umbral del
        // 50 %, y el log publica estas mismas cifras.
        public double TaxiOutMovingSeconds, TaxiOutSingleEngineSeconds;
        public double TaxiInMovingSeconds,  TaxiInSingleEngineSeconds;
        // Tiempo que el simulador daba por «en movimiento» **sin ningún motor en marcha**. No es
        // rodaje —es el remolque del pushback o ruido de la GS del simulador— y ya **no** entra en el
        // denominador del criterio (`SingleEngineTaxiPolicy.CountsAsMoving`); se acumula aparte solo
        // para poder decir en el log cuánto tiempo se descartó y que la cifra publicada se explique.
        public double TaxiOutNoEngineSeconds, TaxiInNoEngineSeconds;
        // Metros **realmente recorridos** en la fase (suma de desplazamientos, no velocidad × tiempo):
        // es el dato que deja al piloto comprobar si el rodaje daba para el criterio, y el que explica
        // por qué un porcentaje es bajo cuando el rodaje fue corto.
        public double TaxiOutMetres, TaxiInMetres;
        // Engine lifecycle compliance — gate the single-engine taxi bonus for jets.
        public bool EngineWarmupViolation;        // insufficient idle time at TakeoffRoll
        public bool EngineCooldownViolation;      // engines shut down before reverser cool-down elapsed
        public bool EngineStabilizationViolation; // engines not stabilized (oil temp/press/N2) at lineup

        public void Reset()
        {
            QnhViolations             = 0;
            IsOfflineFlight           = false;
            DepartedLate              = false;
            ProcedureSpdViolations    = 0;
            SingleEngineTaxiDetected  = false;
            BothEnginesRunning        = false;
            TaxiOutMovingSeconds = TaxiOutSingleEngineSeconds = 0.0;
            TaxiInMovingSeconds  = TaxiInSingleEngineSeconds  = 0.0;
            TaxiOutNoEngineSeconds = TaxiInNoEngineSeconds = 0.0;
            TaxiOutMetres = TaxiInMetres = 0.0;
            EngineWarmupViolation        = false;
            EngineCooldownViolation      = false;
            EngineStabilizationViolation = false;
        }
    }
}
