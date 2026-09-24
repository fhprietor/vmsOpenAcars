using System;
using System.Collections.Generic;
using System.Drawing;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Core.Flight
{
    internal static class PirepBuilder
    {
        private static readonly Dictionary<string, string> _critKeyMap = new Dictionary<string, string>
        {
            { "Landing Rate",        "Score_CritLandingRate"  },
            { "G-Force",             "Score_CritGForce"       },
            { "Bank Angle",          "Score_CritBankAngle"    },
            { "Pitch Angle",         "Score_CritPitchAngle"   },
            { "Overspeed",           "Score_CritOverspeed"    },
            { "Lights Compliance",   "Score_CritLights"       },
            { "Stabilized Approach", "Score_CritStabilized"   },
            { "QNH Compliance",      "Score_CritQnh"          },
            { "Standard Pressure",   "Score_CritStdPressure"  },
            { "IVAO Presence",       "Score_CritIvao"         },
            { "On-Time Departure",   "Score_CritDeparture"    },
            { "Touchdown Zone",      "Score_CritTdz"          },
            { "Centreline",          "Score_CritCentreline"   },
            { "Localizer Alignment", "Score_CritLocalizer"    },
            { "Minimums Compliance", "Score_CritMinimums"     },
            { "Procedure Speed",     "Score_CritProcSpeed"    },
            { "Engine Stabilization","Score_CritEngineStab"   }
        };

        internal static ScoringResult ComputeScore(FlightScoreData data)
            => new ScoringService().Calculate(data);

        internal static void LogScore(ScoringResult result, Action<string, Color> log)
        {
            string ratingKey = "Score_" + result.LandingRating.Replace(" ", "");
            log?.Invoke(string.Format(_("Score_Result"), result.TotalScore, _(ratingKey)), Theme.Success);            foreach (var ded in result.Deductions)
            {
                string critLabel = _critKeyMap.TryGetValue(ded.Criterion, out string ck) ? _(ck) : ded.Criterion;
                log?.Invoke(string.Format(_("Score_Deduction"), ded.PointsDeducted, critLabel, ded.Reason), Theme.Warning);
            }
            if (result.SingleEngineTaxiBonus > 0)
                log?.Invoke(_("Log_BonusSingleEngine", result.SingleEngineTaxiBonus), Theme.Success);
            else if (result.SingleEngineTaxiDeniedReason != null)
                log?.Invoke($"⚠️ Single engine taxi sin bonificación — {result.SingleEngineTaxiDeniedReason}", Theme.Warning);
        }

        internal static object BuildPayload(PirepPayloadArgs a)
        {
            // Dictionary (not an anonymous object) so diversion-airport can be left
            // truly ABSENT from the JSON when there's no diversion — phpVMS only
            // processes a diversion when the pirep includes that key, so an explicit
            // null would be the wrong signal.
            var payload = new Dictionary<string, object>
            {
                ["state"] = 2,
                ["submitted_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                ["block_on_time"] = (a.BlockOnTime != default(DateTime) ? a.BlockOnTime : DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm:ss"),
                ["arr_airport_id"] = a.ArrivalAirport,
                ["distance"] = Math.Round(a.TotalDistanceNm, 2),
                ["planned_distance"] = Math.Round(a.PlannedDistanceNm, 2),
                ["flight_time"] = a.ActualFlightTimeMinutes,
                ["planned_flight_time"] = a.PlannedFlightTimeMinutes,
                ["block_fuel"] = Math.Round(a.BlockFuel, 0),
                ["fuel_used"] = Math.Round(a.FuelUsed, 0),
                // phpVMS needs a number; the internal NoLandingData sentinel (-1) must
                // never reach the server, so an uncaptured touchdown is reported as 0.
                ["landing_rate"] = a.LandingRateFpm.HasValue &&
                                   a.LandingRateFpm.Value != ScoringService.NoLandingData
                                       ? a.LandingRateFpm.Value
                                       : 0,
                ["score"] = a.Score,
                ["notes"] = $"vmsOpenAcars Report - Total: {a.TotalFlightTimeMinutes} min, Flight: {a.ActualFlightTimeMinutes} min, Dist: {a.TotalDistanceNm:F1} NM"
            };
            if (!string.IsNullOrEmpty(a.DiversionAirport))
                payload["diversion-airport"] = a.DiversionAirport;
            return payload;
        }
    }

    internal sealed class PirepPayloadArgs
    {
        internal int      TotalFlightTimeMinutes;
        internal int      ActualFlightTimeMinutes;
        internal int      PlannedFlightTimeMinutes;
        internal double   TotalDistanceNm;
        internal double   PlannedDistanceNm;
        internal double   BlockFuel;
        internal double   FuelUsed;
        internal int?     LandingRateFpm;
        internal int      Score;
        internal DateTime BlockOnTime;
        internal string   ArrivalAirport;
        internal string   DiversionAirport;
    }
}
