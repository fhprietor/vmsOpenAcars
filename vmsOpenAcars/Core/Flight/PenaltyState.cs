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
        public int  TaxiOutMovingCycles, TaxiOutSingleEngineCycles;
        public int  TaxiInMovingCycles,  TaxiInSingleEngineCycles;
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
            TaxiOutMovingCycles = TaxiOutSingleEngineCycles = 0;
            TaxiInMovingCycles  = TaxiInSingleEngineCycles  = 0;
            EngineWarmupViolation        = false;
            EngineCooldownViolation      = false;
            EngineStabilizationViolation = false;
        }
    }
}
