using System;
using System.Collections.Generic;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// A single score deduction applied during calculation.
    /// </summary>
    public class ScoringDeduction
    {
        /// <summary>Short name of the evaluated criterion (e.g. "Landing Rate").</summary>
        public string Criterion { get; set; }

        /// <summary>Human-readable explanation of why points were deducted.</summary>
        public string Reason { get; set; }

        /// <summary>Number of points deducted (positive integer).</summary>
        public int PointsDeducted { get; set; }
    }

    /// <summary>
    /// Result returned by <see cref="ScoringService.Calculate"/>.
    /// </summary>
    public class ScoringResult
    {
        /// <summary>Final score from 0 to 100.</summary>
        public int TotalScore { get; set; }

        /// <summary>
        /// Qualitative rating of the landing based solely on vertical speed.
        /// Values: "Butter", "Smooth", "Normal", "Hard", "Very Hard", "Slam".
        /// </summary>
        public string LandingRating { get; set; }

        /// <summary>List of all deductions applied. Empty if the flight was perfect.</summary>
        public List<ScoringDeduction> Deductions { get; set; } = new List<ScoringDeduction>();
    }

    /// <summary>
    /// Calculates a 0–100 flight performance score based on landing quality,
    /// in-flight violations, and procedure compliance.
    ///
    /// <para>
    /// The score starts at 100 and deductions are applied per criterion.
    /// Maximum deductions per criterion are tuned so the total possible
    /// deduction equals exactly 100 points:
    /// </para>
    /// <list type="table">
    ///   <listheader><term>Criterion</term><description>Max deduction</description></listheader>
    ///   <item><term>Landing Rate</term><description>40 pts</description></item>
    ///   <item><term>G-Force at touchdown</term><description>15 pts</description></item>
    ///   <item><term>Bank angle at touchdown</term><description>10 pts</description></item>
    ///   <item><term>Pitch angle at touchdown</term><description>10 pts</description></item>
    ///   <item><term>Overspeed events</term><description>15 pts</description></item>
    ///   <item><term>Lights compliance</term><description>10 pts</description></item>
    /// </list>
    ///
    /// <para>
    /// The computed <see cref="ScoringResult.TotalScore"/> is sent to phpVMS as
    /// the <c>score</c> field when filing the PIREP via the API.
    /// </para>
    /// </summary>
    public class ScoringService
    {
        // ─── Max deduction per criterion (must sum to 100) ────────────────────────
        private const int MaxLandingRateDeduction = 40;
        private const int MaxGForceDeduction = 15;
        private const int MaxBankDeduction = 10;
        private const int MaxPitchDeduction = 10;
        private const int MaxOverspeedDeduction = 15;
        private const int MaxLightsDeduction = 10;

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates the score for a completed flight.
        /// </summary>
        /// <param name="data">
        /// Performance data collected during the flight.
        /// Must have <see cref="FlightScoreData.LandingRate"/> populated at minimum.
        /// </param>
        /// <returns>
        /// A <see cref="ScoringResult"/> containing the final score (0–100),
        /// the landing rating label, and a detailed list of deductions.
        /// </returns>
        public ScoringResult Calculate(FlightScoreData data)
        {
            var result = new ScoringResult
            {
                LandingRating = GetLandingRating(data.LandingRate)
            };

            int totalDeduction = 0;

            // ── Landing Rate ─────────────────────────────────────────────────────
            int lrDeduction = CalcLandingRateDeduction(data.LandingRate);
            if (lrDeduction > 0)
            {
                result.Deductions.Add(new ScoringDeduction
                {
                    Criterion = "Landing Rate",
                    Reason = $"{data.LandingRate} ft/min",
                    PointsDeducted = lrDeduction
                });
                totalDeduction += lrDeduction;
            }

            // ── G-Force ──────────────────────────────────────────────────────────
            // Skip criterion when no g-force data is available (value == 0.0).
            if (data.LandingGForce > 0.0)
            {
                int gfDeduction = CalcGForceDeduction(data.LandingGForce);
                if (gfDeduction > 0)
                {
                    result.Deductions.Add(new ScoringDeduction
                    {
                        Criterion = "G-Force",
                        Reason = $"{data.LandingGForce:F2}g at touchdown",
                        PointsDeducted = gfDeduction
                    });
                    totalDeduction += gfDeduction;
                }
            }

            // ── Bank Angle ───────────────────────────────────────────────────────
            int bankDeduction = CalcBankDeduction(data.LandingBank);
            if (bankDeduction > 0)
            {
                result.Deductions.Add(new ScoringDeduction
                {
                    Criterion = "Bank Angle",
                    Reason = $"{Math.Abs(data.LandingBank):F1}° at touchdown",
                    PointsDeducted = bankDeduction
                });
                totalDeduction += bankDeduction;
            }

            // ── Pitch Angle ──────────────────────────────────────────────────────
            int pitchDeduction = CalcPitchDeduction(data.LandingPitch);
            if (pitchDeduction > 0)
            {
                result.Deductions.Add(new ScoringDeduction
                {
                    Criterion = "Pitch Angle",
                    Reason = $"{data.LandingPitch:F1}° at touchdown",
                    PointsDeducted = pitchDeduction
                });
                totalDeduction += pitchDeduction;
            }

            // ── Overspeed ────────────────────────────────────────────────────────
            int osDeduction = CalcOverspeedDeduction(data.OverspeedCount);
            if (osDeduction > 0)
            {
                result.Deductions.Add(new ScoringDeduction
                {
                    Criterion = "Overspeed",
                    Reason = $"{data.OverspeedCount} event(s) detected",
                    PointsDeducted = osDeduction
                });
                totalDeduction += osDeduction;
            }

            // ── Lights Compliance ────────────────────────────────────────────────
            int lightsDeduction = CalcLightsDeduction(data.LightsViolations);
            if (lightsDeduction > 0)
            {
                result.Deductions.Add(new ScoringDeduction
                {
                    Criterion = "Lights Compliance",
                    Reason = $"{data.LightsViolations} violation(s)",
                    PointsDeducted = lightsDeduction
                });
                totalDeduction += lightsDeduction;
            }

            result.TotalScore = Math.Max(0, 100 - totalDeduction);
            return result;
        }

        // ─── Criterion evaluators ─────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the landing vertical speed.
        /// Works with the absolute value — direction is irrelevant.
        /// </summary>
        private static int CalcLandingRateDeduction(int landingRate)
        {
            int rate = Math.Abs(landingRate);

            if (rate <= 100) return 0;   // Butter  — no deduction
            if (rate <= 200) return 5;   // Smooth  — minor
            if (rate <= 300) return 15;  // Normal  — moderate
            if (rate <= 400) return 25;  // Hard    — significant
            if (rate <= 600) return 35;  // Very Hard
            return MaxLandingRateDeduction; // Slam (≥ 600 ft/min)
        }

        /// <summary>
        /// Evaluates G-force at touchdown.
        /// Skip by leaving <see cref="FlightScoreData.LandingGForce"/> at 0.
        /// </summary>
        private static int CalcGForceDeduction(double gForce)
        {
            if (gForce <= 1.3) return 0;
            if (gForce <= 1.5) return 7;
            return MaxGForceDeduction;
        }

        /// <summary>
        /// Evaluates absolute bank angle at touchdown.
        /// Ideal: ≤2°. Over 5° is a significant cross-wind/technique issue.
        /// </summary>
        private static int CalcBankDeduction(double bank)
        {
            double abs = Math.Abs(bank);
            if (abs <= 2.0) return 0;
            if (abs <= 5.0) return 5;
            return MaxBankDeduction;
        }

        /// <summary>
        /// Evaluates nose pitch at touchdown.
        /// Ideal: slightly nose-up (+1° to +5°).
        /// Nose-down landings are the most penalized.
        /// </summary>
        private static int CalcPitchDeduction(double pitch)
        {
            if (pitch >= 1.0 && pitch <= 5.0) return 0;    // Ideal nose-up attitude
            if (pitch < -2.0) return MaxPitchDeduction; // Nose-down — hard on nose gear
            if (pitch >= -2.0 && pitch < 1.0) return 5;    // Flat or slightly nose-down
            if (pitch > 8.0) return 5;    // Excessive flare
            return 0;
        }

        /// <summary>
        /// Evaluates overspeed events recorded during the flight.
        /// </summary>
        private static int CalcOverspeedDeduction(int count)
        {
            if (count == 0) return 0;
            if (count == 1) return 7;
            return MaxOverspeedDeduction;
        }

        /// <summary>
        /// Evaluates lights compliance violations. Each violation costs 5 pts,
        /// capped at <see cref="MaxLightsDeduction"/>.
        /// </summary>
        private static int CalcLightsDeduction(int violations)
        {
            if (violations == 0) return 0;
            return Math.Min(violations * 5, MaxLightsDeduction);
        }

        // ─── Rating label ─────────────────────────────────────────────────────────

        /// <summary>Returns a human-readable landing quality label based on vertical speed.</summary>
        private static string GetLandingRating(int landingRate)
        {
            int rate = Math.Abs(landingRate);
            if (rate <= 100) return "Butter";
            if (rate <= 200) return "Smooth";
            if (rate <= 300) return "Normal";
            if (rate <= 400) return "Hard";
            if (rate <= 600) return "Very Hard";
            return "Slam";
        }
    }
}