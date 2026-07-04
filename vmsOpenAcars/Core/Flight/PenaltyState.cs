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

        public void Reset()
        {
            QnhViolations            = 0;
            IsOfflineFlight          = false;
            DepartedLate             = false;
            ProcedureSpdViolations   = 0;
            SingleEngineTaxiDetected = false;
            BothEnginesRunning       = false;
            TaxiOutMovingCycles = TaxiOutSingleEngineCycles = 0;
            TaxiInMovingCycles  = TaxiInSingleEngineCycles  = 0;
        }
    }
}
