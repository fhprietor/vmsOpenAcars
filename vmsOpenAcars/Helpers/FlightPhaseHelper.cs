using System.Collections.Generic;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    public static class FlightPhaseHelper
    {
        private static readonly Dictionary<FlightPhase, string> PhaseToStatusCode = new Dictionary<FlightPhase, string>
        {
            [FlightPhase.Boarding]     = "BST",
            [FlightPhase.Pushback]     = "PBT",
            [FlightPhase.TaxiOut]      = "TXI",
            [FlightPhase.TaxiIn]       = "TXI",
            [FlightPhase.Takeoff]      = "TOF",
            [FlightPhase.Climb]        = "ICL",
            [FlightPhase.Enroute]      = "ENR",
            [FlightPhase.Descent]      = "APR",
            [FlightPhase.Approach]     = "FIN",
            [FlightPhase.Landing]      = "LDG",
            [FlightPhase.OnBlock]      = "ARR",
            [FlightPhase.Completed]    = "ARR"
        };

        public static string GetStatusCode(FlightPhase phase)
            => PhaseToStatusCode.TryGetValue(phase, out string code) ? code : "INI";

        public static string GetDisplayName(FlightPhase phase)
        {
            switch (phase)
            {
                case FlightPhase.Boarding:  return "BOARDING";
                case FlightPhase.Pushback:  return "PUSHBACK";
                case FlightPhase.TaxiOut:   return "TAXI OUT";
                case FlightPhase.Takeoff:   return "TAKEOFF";
                case FlightPhase.Climb:     return "CLIMB";
                case FlightPhase.Enroute:   return "ENROUTE";
                case FlightPhase.Descent:   return "DESCENT";
                case FlightPhase.Approach:  return "APPROACH";
                case FlightPhase.Landing:   return "LANDING";
                case FlightPhase.TaxiIn:    return "TAXI IN";
                case FlightPhase.Completed: return "COMPLETED";
                default:                    return phase.ToString().ToUpper();
            }
        }

        public static FlightPhase FromPirepStatus(string status)
        {
            switch (status?.ToUpperInvariant())
            {
                case "INI":
                case "BST": return FlightPhase.Boarding;
                case "PBK": return FlightPhase.Pushback;
                case "TXI": return FlightPhase.TaxiOut;
                case "TKF": return FlightPhase.Takeoff;
                case "CLB": return FlightPhase.Climb;
                case "ENR":
                case "CRZ": return FlightPhase.Enroute;
                case "DSC": return FlightPhase.Descent;
                case "APR":
                case "FIN": return FlightPhase.Approach;
                case "LND": return FlightPhase.Landing;
                case "ONB": return FlightPhase.AfterLanding;
                case "ARR": return FlightPhase.TaxiIn;
                default:    return FlightPhase.Enroute;
            }
        }

        public static double GetTerrainElevation(FlightPhase phase, SimbriefPlan plan)
        {
            if (plan == null) return 0.0;
            switch (phase)
            {
                case FlightPhase.Boarding:
                case FlightPhase.Pushback:
                case FlightPhase.TaxiOut:
                case FlightPhase.TakeoffRoll:
                case FlightPhase.Takeoff:
                case FlightPhase.Climb:
                case FlightPhase.Enroute:
                    return plan.OriginElevation;

                case FlightPhase.Descent:
                case FlightPhase.Approach:
                case FlightPhase.Landing:
                case FlightPhase.Landed:
                case FlightPhase.AfterLanding:
                case FlightPhase.TaxiIn:
                case FlightPhase.OnBlock:
                case FlightPhase.Arrived:
                case FlightPhase.Completed:
                    return plan.DestinationElevation;

                default:
                    return 0.0;
            }
        }
    }
}