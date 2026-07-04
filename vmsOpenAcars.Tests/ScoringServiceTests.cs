using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    [TestClass]
    public class ScoringServiceTests
    {
        private ScoringService _svc;

        [TestInitialize]
        public void Setup() => _svc = new ScoringService();

        // Perfect flight: all values within acceptable range, no violations.
        private static FlightScoreData Perfect() => new FlightScoreData
        {
            LandingRate   = -100,   // ≤150 → 0 deduction
            LandingGForce = 1.2,    // ≤1.5 → 0 deduction
            LandingBank   = 0.0,    // ≤2°  → 0 deduction
            LandingPitch  = 3.0,    // 1°–7° → 0 deduction
            IlsTunedCorrectly = true, // default — skips localizer check
        };

        // ── Perfect flight ──────────────────────────────────────────────────────

        [TestMethod]
        public void PerfectFlight_Returns100()
        {
            var r = _svc.Calculate(Perfect());
            Assert.AreEqual(100, r.TotalScore);
            Assert.AreEqual(0, r.Deductions.Count);
        }

        // ── Landing Rate ────────────────────────────────────────────────────────

        [TestMethod]
        public void LandingRate_Butter_NoDeduction()
        {
            var d = Perfect(); d.LandingRate = -150;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void LandingRate_Smooth_Minus5()
        {
            var d = Perfect(); d.LandingRate = -250;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void LandingRate_Normal_Minus15()
        {
            var d = Perfect(); d.LandingRate = -350;
            Assert.AreEqual(85, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void LandingRate_Hard_Minus25()
        {
            var d = Perfect(); d.LandingRate = -450;
            Assert.AreEqual(75, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void LandingRate_VeryHard_Minus35()
        {
            var d = Perfect(); d.LandingRate = -650;
            Assert.AreEqual(65, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void LandingRate_Slam_Minus40()
        {
            var d = Perfect(); d.LandingRate = -700;
            Assert.AreEqual(60, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void LandingRate_PositiveValue_SameAsNegative()
        {
            // Uses Math.Abs internally — direction irrelevant.
            var pos = Perfect(); pos.LandingRate = 250;
            var neg = Perfect(); neg.LandingRate = -250;
            Assert.AreEqual(_svc.Calculate(pos).TotalScore, _svc.Calculate(neg).TotalScore);
        }

        // ── Landing rating label ─────────────────────────────────────────────────

        [TestMethod]
        public void LandingRating_Butter()
        {
            var d = Perfect(); d.LandingRate = -100;
            Assert.AreEqual("Butter", _svc.Calculate(d).LandingRating);
        }

        [TestMethod]
        public void LandingRating_Smooth()
        {
            var d = Perfect(); d.LandingRate = -200;
            Assert.AreEqual("Smooth", _svc.Calculate(d).LandingRating);
        }

        [TestMethod]
        public void LandingRating_Normal()
        {
            var d = Perfect(); d.LandingRate = -300;
            Assert.AreEqual("Normal", _svc.Calculate(d).LandingRating);
        }

        [TestMethod]
        public void LandingRating_Hard()
        {
            var d = Perfect(); d.LandingRate = -400;
            Assert.AreEqual("Hard", _svc.Calculate(d).LandingRating);
        }

        [TestMethod]
        public void LandingRating_VeryHard()
        {
            var d = Perfect(); d.LandingRate = -600;
            Assert.AreEqual("Very Hard", _svc.Calculate(d).LandingRating);
        }

        [TestMethod]
        public void LandingRating_Slam()
        {
            var d = Perfect(); d.LandingRate = -700;
            Assert.AreEqual("Slam", _svc.Calculate(d).LandingRating);
        }

        // ── G-Force ──────────────────────────────────────────────────────────────

        [TestMethod]
        public void GForce_Zero_CriterionSkipped()
        {
            var d = Perfect(); d.LandingGForce = 0.0;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void GForce_Under1_5_NoDeduction()
        {
            var d = Perfect(); d.LandingGForce = 1.5;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void GForce_1_6_Minus7()
        {
            var d = Perfect(); d.LandingGForce = 1.6;
            Assert.AreEqual(93, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void GForce_Over1_7_Minus15()
        {
            var d = Perfect(); d.LandingGForce = 1.71;
            Assert.AreEqual(85, _svc.Calculate(d).TotalScore);
        }

        // ── Bank Angle ───────────────────────────────────────────────────────────

        [TestMethod]
        public void Bank_Under2Deg_NoDeduction()
        {
            var d = Perfect(); d.LandingBank = 2.0;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Bank_3Deg_Minus5()
        {
            var d = Perfect(); d.LandingBank = 3.0;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Bank_Over5Deg_Minus10()
        {
            var d = Perfect(); d.LandingBank = 5.1;
            Assert.AreEqual(90, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Bank_NegativeBank_SameAsPositive()
        {
            var pos = Perfect(); pos.LandingBank = 3.0;
            var neg = Perfect(); neg.LandingBank = -3.0;
            Assert.AreEqual(_svc.Calculate(pos).TotalScore, _svc.Calculate(neg).TotalScore);
        }

        // ── Pitch Angle ──────────────────────────────────────────────────────────

        [TestMethod]
        public void Pitch_Ideal_1to7_NoDeduction()
        {
            var d = Perfect(); d.LandingPitch = 1.0;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
            d.LandingPitch = 7.0;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Pitch_Flat_Minus5()
        {
            var d = Perfect(); d.LandingPitch = 0.5;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Pitch_NoseDown_Minus10()
        {
            var d = Perfect(); d.LandingPitch = -3.0;
            Assert.AreEqual(90, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Pitch_ExcessiveFlare_Minus5()
        {
            var d = Perfect(); d.LandingPitch = 8.1;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Pitch_7to8_NoDeduction()
        {
            // 7 < pitch ≤ 8 falls through all conditions → return 0
            var d = Perfect(); d.LandingPitch = 7.5;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        // ── Overspeed ────────────────────────────────────────────────────────────

        [TestMethod]
        public void Overspeed_ZeroPenaltyCount_NoScoreDeduction()
        {
            // Events detected but all ATC-exempt → no point loss
            var d = Perfect(); d.OverspeedCount = 3; d.OverspeedPenaltyCount = 0;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Overspeed_OnePenalty_Minus7()
        {
            var d = Perfect(); d.OverspeedCount = 1; d.OverspeedPenaltyCount = 1;
            Assert.AreEqual(93, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Overspeed_TwoPenalties_Minus15()
        {
            var d = Perfect(); d.OverspeedCount = 2; d.OverspeedPenaltyCount = 2;
            Assert.AreEqual(85, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Overspeed_ThreeOrMore_CappedAt15()
        {
            var d = Perfect(); d.OverspeedCount = 5; d.OverspeedPenaltyCount = 5;
            Assert.AreEqual(85, _svc.Calculate(d).TotalScore);
        }

        // ── Lights Compliance ────────────────────────────────────────────────────

        [TestMethod]
        public void Lights_OneViolation_Minus5()
        {
            var d = Perfect(); d.LightsViolations = 1;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Lights_TwoViolations_Minus10()
        {
            var d = Perfect(); d.LightsViolations = 2;
            Assert.AreEqual(90, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Lights_ThreeOrMore_CappedAt10()
        {
            var d = Perfect(); d.LightsViolations = 5;
            Assert.AreEqual(90, _svc.Calculate(d).TotalScore);
        }

        // ── Stabilized Approach (1000 ft gate) ──────────────────────────────────

        [TestMethod]
        public void Stabilized_3Points_Minus3()
        {
            var d = Perfect(); d.StabilizedApproachDeductions = 3;
            Assert.AreEqual(97, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Stabilized_Over15_CappedAt15()
        {
            var d = Perfect(); d.StabilizedApproachDeductions = 20;
            Assert.AreEqual(85, _svc.Calculate(d).TotalScore);
        }

        // ── QNH Compliance ───────────────────────────────────────────────────────

        [TestMethod]
        public void Qnh_OneViolation_Minus5()
        {
            var d = Perfect(); d.QnhViolations = 1;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Qnh_TwoViolations_Minus10()
        {
            var d = Perfect(); d.QnhViolations = 2;
            Assert.AreEqual(90, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Qnh_ThreeOrMore_CappedAt10()
        {
            var d = Perfect(); d.QnhViolations = 5;
            Assert.AreEqual(90, _svc.Calculate(d).TotalScore);
        }

        // ── Touchdown Zone ───────────────────────────────────────────────────────

        [TestMethod]
        public void TDZ_ZeroDistance_CriterionSkipped()
        {
            var d = Perfect(); d.TouchdownDistanceFt = 0;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void TDZ_Within1500ft_NoDeduction()
        {
            var d = Perfect(); d.TouchdownDistanceFt = 1500;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void TDZ_Between1500And2500_Minus3()
        {
            var d = Perfect(); d.TouchdownDistanceFt = 2000;
            Assert.AreEqual(97, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void TDZ_Over2500ft_Minus7()
        {
            var d = Perfect(); d.TouchdownDistanceFt = 3000;
            Assert.AreEqual(93, _svc.Calculate(d).TotalScore);
        }

        // ── Centreline Deviation ─────────────────────────────────────────────────

        [TestMethod]
        public void Centreline_ZeroDeviation_CriterionSkipped()
        {
            var d = Perfect(); d.CenterlineDeviationFt = 0;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Centreline_Under10ft_NoDeduction()
        {
            var d = Perfect(); d.CenterlineDeviationFt = 10;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Centreline_Between10And30ft_Minus3()
        {
            var d = Perfect(); d.CenterlineDeviationFt = 20;
            Assert.AreEqual(97, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Centreline_Over30ft_Minus7()
        {
            var d = Perfect(); d.CenterlineDeviationFt = 50;
            Assert.AreEqual(93, _svc.Calculate(d).TotalScore);
        }

        // ── Localizer Alignment ──────────────────────────────────────────────────

        [TestMethod]
        public void Localizer_IlsCorrect_NoViolations_CriterionSkipped()
        {
            // Default IlsTunedCorrectly=true, LocalizerViolations=0 → criterion skipped
            Assert.AreEqual(100, _svc.Calculate(Perfect()).TotalScore);
        }

        [TestMethod]
        public void Localizer_IlsNotTuned_Minus3()
        {
            var d = Perfect(); d.IlsTunedCorrectly = false;
            Assert.AreEqual(97, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Localizer_OneViolation_Minus1()
        {
            var d = Perfect(); d.LocalizerViolations = 1;
            Assert.AreEqual(99, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Localizer_TwoViolations_Minus2()
        {
            var d = Perfect(); d.LocalizerViolations = 2;
            Assert.AreEqual(98, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Localizer_MoreThanTwoViolations_CappedAt2()
        {
            var d = Perfect(); d.LocalizerViolations = 5;
            Assert.AreEqual(98, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void Localizer_IlsNotTuned_PlusViolations_CappedAt5()
        {
            // 3 (not tuned) + min(2, 5) = 5, capped at MaxLocalizerDeduction=5
            var d = Perfect(); d.IlsTunedCorrectly = false; d.LocalizerViolations = 5;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        // ── Minimums Compliance ──────────────────────────────────────────────────

        [TestMethod]
        public void Minimums_BelowMinimums_Minus5()
        {
            var d = Perfect(); d.BelowMinimums = true;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        // ── IVAO Offline ─────────────────────────────────────────────────────────

        [TestMethod]
        public void Ivao_OfflineFlight_Minus5()
        {
            var d = Perfect(); d.WasOfflineFlight = true;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        // ── On-Time Departure ────────────────────────────────────────────────────

        [TestMethod]
        public void OnTime_DepartedLate_Minus5()
        {
            var d = Perfect(); d.DepartedLate = true;
            Assert.AreEqual(95, _svc.Calculate(d).TotalScore);
        }

        // ── Procedure Speed Restrictions ─────────────────────────────────────────

        [TestMethod]
        public void ProcSpd_OneViolation_Minus3()
        {
            var d = Perfect(); d.ProcedureSpdViolations = 1;
            Assert.AreEqual(97, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void ProcSpd_ThreeViolations_Minus9()
        {
            var d = Perfect(); d.ProcedureSpdViolations = 3;
            Assert.AreEqual(91, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void ProcSpd_FourOrMore_CappedAt10()
        {
            var d = Perfect(); d.ProcedureSpdViolations = 10;
            Assert.AreEqual(90, _svc.Calculate(d).TotalScore);
        }

        // ── Single Engine Taxi bonus ─────────────────────────────────────────────

        [TestMethod]
        public void SingleEngineTaxi_BonusIs5()
        {
            var d = Perfect(); d.SingleEngineTaxi = true;
            var r = _svc.Calculate(d);
            Assert.AreEqual(5, r.SingleEngineTaxiBonus);
        }

        [TestMethod]
        public void SingleEngineTaxi_ScoreCappedAt100()
        {
            // Perfect + bonus → would be 105, capped at 100
            var d = Perfect(); d.SingleEngineTaxi = true;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        [TestMethod]
        public void SingleEngineTaxi_RecoversFivePoints()
        {
            // 100 − 5 (lights) + 5 (bonus) = 100
            var d = Perfect(); d.LightsViolations = 1; d.SingleEngineTaxi = true;
            Assert.AreEqual(100, _svc.Calculate(d).TotalScore);
        }

        // ── Score floor ──────────────────────────────────────────────────────────

        [TestMethod]
        public void Score_NeverGoesBelow0()
        {
            var d = Perfect();
            d.LandingRate                  = -900;  // −40
            d.LandingGForce                = 2.0;   // −15
            d.LandingBank                  = 10.0;  // −10
            d.LandingPitch                 = -5.0;  // −10
            d.OverspeedCount               = 3;
            d.OverspeedPenaltyCount        = 3;     // −15
            d.LightsViolations             = 3;     // −10
            d.StabilizedApproachDeductions = 15;    // −15
            d.QnhViolations                = 3;     // −10
            d.WasOfflineFlight             = true;  // −5
            d.DepartedLate                 = true;  // −5
            Assert.AreEqual(0, _svc.Calculate(d).TotalScore);
        }

        // ── Deduction list ───────────────────────────────────────────────────────

        [TestMethod]
        public void Deductions_PerfectFlight_EmptyList()
        {
            Assert.AreEqual(0, _svc.Calculate(Perfect()).Deductions.Count);
        }

        [TestMethod]
        public void Deductions_LandingRate_EntryAdded()
        {
            var d = Perfect(); d.LandingRate = -300;
            var r = _svc.Calculate(d);
            Assert.AreEqual(1, r.Deductions.Count);
            Assert.AreEqual("Landing Rate", r.Deductions[0].Criterion);
            Assert.AreEqual(15, r.Deductions[0].PointsDeducted);
        }

        [TestMethod]
        public void Deductions_MultipleViolations_AllEntriesPresent()
        {
            var d = Perfect();
            d.LandingRate       = -400;  // Hard
            d.LightsViolations  = 1;
            d.WasOfflineFlight  = true;
            var r = _svc.Calculate(d);
            Assert.AreEqual(3, r.Deductions.Count);
            Assert.AreEqual(65, r.TotalScore);  // 100 − 25 (LR hard) − 5 (lights) − 5 (ivao)
        }
    }
}
