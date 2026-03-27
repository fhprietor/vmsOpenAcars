// Helpers/FlightPhaseHelper.cs
using System.Collections.Generic;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    public static class FlightPhaseHelper
    {
        private static readonly Dictionary<FlightPhase, string> PhaseToStatusCode = new Dictionary<FlightPhase, string>
        {
            [FlightPhase.Boarding] = "BST",
            [FlightPhase.Pushback] = "PBT",
            [FlightPhase.TaxiOut] = "TXI",
            [FlightPhase.Takeoff] = "TOF",
            [FlightPhase.Climb] = "ICL",
            [FlightPhase.Enroute] = "ENR",
            [FlightPhase.Descent] = "APR",
            [FlightPhase.Approach] = "FIN",
            [FlightPhase.Landing] = "LDG",
            [FlightPhase.TaxiIn] = "TXI",
            [FlightPhase.OnBlock] = "ARR",
            [FlightPhase.Completed] = "ARR"
        };

        public static string GetStatusCode(FlightPhase phase)
        {
            return PhaseToStatusCode.TryGetValue(phase, out string code) ? code : "INI";
        }
    }
}