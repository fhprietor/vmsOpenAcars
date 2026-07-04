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
            { "IVAO Presence",       "Score_CritIvao"         },
            { "On-Time Departure",   "Score_CritDeparture"    },
            { "Touchdown Zone",      "Score_CritTdz"          },
            { "Centreline",          "Score_CritCentreline"   },
            { "Localizer Alignment", "Score_CritLocalizer"    },
            { "Minimums Compliance", "Score_CritMinimums"     },
            { "Procedure Speed",     "Score_CritProcSpeed"    }
        };

        internal static ScoringResult ComputeScore(FlightScoreData data)
            => new ScoringService().Calculate(data);

        internal static void LogScore(ScoringResult result, Action<string, Color> log)
        {
            string ratingKey = "Score_" + result.LandingRating.Replace(" ", "");
            log?.Invoke(string.Format(_("Score_Result"), result.TotalScore, _(ratingKey)), Theme.Success);
            foreach (var ded in result.Deductions)
            {
                string critLabel = _critKeyMap.TryGetValue(ded.Criterion, out string ck) ? _(ck) : ded.Criterion;
                log?.Invoke(string.Format(_("Score_Deduction"), ded.PointsDeducted, critLabel, ded.Reason), Theme.Warning);
            }
            if (result.SingleEngineTaxiBonus > 0)
                log?.Invoke(_("Log_BonusSingleEngine", result.SingleEngineTaxiBonus), Theme.Success);
        }

        internal static object BuildPayload(PirepPayloadArgs a) => new
        {
            state = 2,
            submitted_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            block_on_time = (a.BlockOnTime != default(DateTime) ? a.BlockOnTime : DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm:ss"),
            distance = Math.Round(a.TotalDistanceNm, 2),
            planned_distance = Math.Round(a.PlannedDistanceNm, 2),
            flight_time = a.ActualFlightTimeMinutes,
            planned_flight_time = a.PlannedFlightTimeMinutes,
            block_fuel = Math.Round(a.BlockFuel, 0),
            fuel_used = Math.Round(a.FuelUsed, 0),
            landing_rate = a.LandingRateFpm ?? 0,
            score = a.Score,
            notes = $"vmsOpenAcars Report - Total: {a.TotalFlightTimeMinutes} min, Flight: {a.ActualFlightTimeMinutes} min, Dist: {a.TotalDistanceNm:F1} NM"
        };
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
    }
}
