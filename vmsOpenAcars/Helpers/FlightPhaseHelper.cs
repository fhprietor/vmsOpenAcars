using System;
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

        // Alias aceptados AL REANUDAR un PIREP que no son emitidos por este cliente.
        // El vocabulario propio (PhaseToStatusCode) se resuelve por inversión, de modo
        // que ambos sentidos no pueden desincronizarse al añadir una fase.
        // NO incluir aquí ninguna clave presente en PhaseToStatusCode.
        private static readonly Dictionary<string, FlightPhase> StatusAliases =
            new Dictionary<string, FlightPhase>(StringComparer.OrdinalIgnoreCase)
            {
                ["INI"] = FlightPhase.Boarding,
                ["PBK"] = FlightPhase.Pushback,
                ["TKF"] = FlightPhase.Takeoff,
                ["CLB"] = FlightPhase.Climb,
                ["CRZ"] = FlightPhase.Enroute,
                ["DSC"] = FlightPhase.Descent,
                ["LND"] = FlightPhase.Landing,
                ["ONB"] = FlightPhase.AfterLanding,
            };

        /// <summary>
        /// Traduce el código de estado de un PIREP (columna status de phpVMS) a la fase
        /// interna. Prioriza el vocabulario propio y cae a los alias de compatibilidad.
        /// </summary>
        public static FlightPhase FromPirepStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return FlightPhase.Boarding;
            string token = status.Trim().ToUpperInvariant();

            // 1. Vocabulario que emite este cliente, por inversión de PhaseToStatusCode.
            //    Así "APR" resuelve a Descent y "FIN" a Approach — exactamente lo que
            //    GetStatusCode envía — sin poder divergir del sentido de emisión.
            foreach (var kv in PhaseToStatusCode)
                if (string.Equals(kv.Value, token, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;

            // 2. Alias procedentes de otras fuentes ACARS.
            if (StatusAliases.TryGetValue(token, out var alias)) return alias;

            // 3. Desconocido: un PIREP activo suele estar en vuelo, no en tierra.
            return FlightPhase.Enroute;
        }

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