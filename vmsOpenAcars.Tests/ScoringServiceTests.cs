using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Umbrales de <see cref="ScoringService"/>. Un test por criterio y por frontera.
    ///
    /// Los umbrales son inclusivos por el lado bajo: "≤150 fpm = 0" implica que 150 no
    /// penaliza pero 151 sí. Por eso cada frontera se comprueba en sus dos lados, que es
    /// donde un cambio de &lt; por &lt;= pasaría desapercibido.
    ///
    /// Todos los tests parten de un vuelo perfecto (score 100, cero deducciones) y añaden
    /// exactamente la violación bajo prueba, de modo que la deducción observada es
    /// atribuible sin ambigüedad a ese criterio.
    /// </summary>
    [TestClass]
    public class ScoringServiceTests
    {
        private ScoringService _sut;

        [TestInitialize]
        public void SetUp() => _sut = new ScoringService();

        // ══ Helpers ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Vuelo impecable: touchdown suave capturado, sin violaciones y con el bonus de
        /// single-engine concedido (Turboprop, que no exige ciclo de vida de motores).
        /// </summary>
        private static FlightScoreData PerfectFlight() => new FlightScoreData
        {
            LandingDataCaptured          = true,
            LandingRate                  = -140,   // Butter
            LandingGForce                = 1.20,    // sin deducción
            LandingBank                  = 0.5,
            LandingPitch                 = 4.0,     // rango ideal 1–7
            OverspeedPenaltyCount        = 0,
            LightsViolations             = 0,
            StabilizedApproachDeductions = 0,
            QnhViolations                = 0,
            StdPressureViolation         = false,
            WasOfflineFlight             = false,
            DepartedLate                 = false,
            TouchdownDistanceFt          = 0,       // criterios TDZ/centreline omitidos
            CenterlineDeviationFt        = 0,
            IlsTunedCorrectly            = true,
            LocalizerViolations          = 0,
            BelowMinimums                = false,
            ProcedureSpdViolations       = 0,
            EngineType                   = ScoredEngineType.Turboprop,
            SingleEngineTaxi             = true,
        };

        private ScoringResult Score(FlightScoreData data) => _sut.Calculate(data);

        private static int DeductionFor(ScoringResult r, string criterion)
            => r.Deductions.Where(d => d.Criterion == criterion).Sum(d => d.PointsDeducted);

        // ══ Punto de partida ═════════════════════════════════════════════════════

        [TestMethod]
        public void PerfectFlight_Scores100_WithNoDeductions()
        {
            var r = Score(PerfectFlight());
            Assert.AreEqual(100, r.TotalScore);
            Assert.AreEqual(0, r.Deductions.Count, "un vuelo perfecto no debe producir deducciones");
        }

        [TestMethod]
        public void PerfectFlight_ReportsButterRating()
        {
            Assert.AreEqual("Butter", Score(PerfectFlight()).LandingRating);
        }

        // ══ Landing Rate (max 40) ════════════════════════════════════════════════

        [DataTestMethod]
        [DataRow(-150, 0,  "150 fpm es el limite superior de Butter")]
        [DataRow(-151, 5,  "justo por encima de 150")]
        [DataRow(-250, 5,  "limite superior de Smooth")]
        [DataRow(-251, 15, "justo por encima de 250")]
        [DataRow(-350, 15, "limite superior de Normal")]
        [DataRow(-351, 25, "justo por encima de 350")]
        [DataRow(-450, 25, "limite superior de Hard")]
        [DataRow(-451, 35, "justo por encima de 450")]
        [DataRow(-650, 35, "limite superior de Very Hard")]
        [DataRow(-651, 40, "Slam")]
        [DataRow(-1200, 40, "muy por encima del maximo, tope 40")]
        public void LandingRate_Thresholds(int rate, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.LandingRate = rate;
            var r = Score(d);
            Assert.AreEqual(expectedDeduction, DeductionFor(r, "Landing Rate"), because);
        }

        [TestMethod]
        public void LandingRate_UsesAbsoluteValue_SoPositiveRateIsNotFree()
        {
            var d = PerfectFlight();
            d.LandingRate = 700;   // positivo: no debe colarse como "sin deduccion"
            Assert.AreEqual(40, DeductionFor(Score(d), "Landing Rate"));
        }

        // ══ Landing Rate — sin datos de aterrizaje ═══════════════════════════════

        [TestMethod]
        public void NoLandingData_OmitsLandingRateCriterion_InsteadOfScoring0Fpm()
        {
            var d = PerfectFlight();
            d.LandingDataCaptured = false;
            d.LandingRate = ScoringService.NoLandingData;

            var r = Score(d);

            Assert.AreEqual(0, DeductionFor(r, "Landing Rate"),
                "sin touchdown no hay nada que penalizar");
            Assert.IsFalse(r.Deductions.Any(x => x.Criterion == "Landing Rate"));
        }

        [TestMethod]
        public void NoLandingData_ReportsUnknownRating_NotButter()
        {
            var d = PerfectFlight();
            d.LandingDataCaptured = false;
            d.LandingRate = ScoringService.NoLandingData;

            Assert.AreEqual("Unknown", Score(d).LandingRating,
                "un aterrizaje no capturado no puede reportarse como Butter");
        }

        [TestMethod]
        public void NoLandingData_SentinelIsAlsoHonoured_EvenIfCapturedFlagIsSet()
        {
            // Defensa en profundidad: si alguien construye FlightScoreData con el centinela
            // pero olvida el flag, el resultado no debe ser un "Butter" gratuito.
            var d = PerfectFlight();
            d.LandingDataCaptured = true;
            d.LandingRate = ScoringService.NoLandingData;

            var r = Score(d);
            Assert.AreEqual("Unknown", r.LandingRating);
            Assert.AreEqual(0, DeductionFor(r, "Landing Rate"));
        }

        [TestMethod]
        public void RealZeroFpmLanding_IsStillScoredAsButter()
        {
            // El centinela no debe robar el caso legitimo de 0 fpm.
            var d = PerfectFlight();
            d.LandingDataCaptured = true;
            d.LandingRate = 0;

            var r = Score(d);
            Assert.AreEqual("Butter", r.LandingRating);
            Assert.AreEqual(0, DeductionFor(r, "Landing Rate"));
        }

        // ══ G-Force (max 15) ═════════════════════════════════════════════════════

        [DataTestMethod]
        [DataRow(1.50, 0,  "limite superior sin penalizar")]
        [DataRow(1.51, 7,  "justo por encima de 1.5g")]
        [DataRow(1.70, 7,  "limite superior de la banda media")]
        [DataRow(1.71, 15, "por encima de 1.7g")]
        [DataRow(3.00, 15, "tope")]
        public void GForce_Thresholds(double g, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.LandingGForce = g;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "G-Force"), because);
        }

        [TestMethod]
        public void GForce_IsOmittedWhenDataIsUnavailable()
        {
            var d = PerfectFlight();
            d.LandingGForce = 0.0;   // 0 = sin dato
            var r = Score(d);
            Assert.IsFalse(r.Deductions.Any(x => x.Criterion == "G-Force"));
        }

        // ══ Bank Angle (max 10) ══════════════════════════════════════════════════

        [DataTestMethod]
        [DataRow(2.0,  0,  "limite sin penalizar")]
        [DataRow(2.1,  5,  "por encima de 2 grados")]
        [DataRow(5.0,  5,  "limite de la banda media")]
        [DataRow(5.1,  10, "por encima de 5 grados")]
        [DataRow(-7.0, 10, "el signo no importa, se usa el valor absoluto")]
        public void BankAngle_Thresholds(double bank, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.LandingBank = bank;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "Bank Angle"), because);
        }

        // ══ Pitch Angle (max 10) ═════════════════════════════════════════════════

        [DataTestMethod]
        [DataRow(1.0,   0,  "borde inferior del rango ideal")]
        [DataRow(4.0,   0,  "centro del rango ideal")]
        [DataRow(7.0,   0,  "borde superior del rango ideal")]
        [DataRow(7.5,   0,  "hueco 7-8 grados: sin penalizacion")]
        [DataRow(8.0,   0,  "8 grados exactos: aun sin penalizacion")]
        [DataRow(8.1,   5,  "flapeo excesivo")]
        [DataRow(0.5,   5,  "plano o ligeramente picado")]
        [DataRow(-1.9,  5,  "dentro de la banda -2 a 1")]
        [DataRow(-2.0,  5,  "borde de la banda")]
        [DataRow(-2.1,  10, "aterrizaje picado: lo mas penalizado")]
        public void PitchAngle_Thresholds(double pitch, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.LandingPitch = pitch;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "Pitch Angle"), because);
        }

        // ══ Overspeed (max 15) ═══════════════════════════════════════════════════

        [DataTestMethod]
        [DataRow(0, 0,  "sin eventos")]
        [DataRow(1, 7,  "un evento")]
        [DataRow(2, 15, "dos eventos alcanzan el tope")]
        [DataRow(9, 15, "el tope no se supera")]
        public void Overspeed_Thresholds(int penaltyCount, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.OverspeedCount = penaltyCount;
            d.OverspeedPenaltyCount = penaltyCount;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "Overspeed"), because);
        }

        [TestMethod]
        public void Overspeed_ExemptedByAtc_DoesNotDeduct()
        {
            // Todos los eventos exentos: se registran pero no penalizan.
            var d = PerfectFlight();
            d.OverspeedCount = 3;
            d.OverspeedPenaltyCount = 0;

            var r = Score(d);
            Assert.AreEqual(0, DeductionFor(r, "Overspeed"));
            Assert.AreEqual(100, r.TotalScore);
        }

        [TestMethod]
        public void Overspeed_ExemptedEvents_AreOnlyReportedWhenTheyActuallyHappened()
        {
            var d = PerfectFlight();
            d.OverspeedCount = 0;
            d.OverspeedPenaltyCount = 0;
            Assert.IsFalse(Score(d).Deductions.Any(x => x.Criterion == "Overspeed"),
                "sin eventos no debe aparecer la fila de Overspeed, aunque sean 0 pts");
        }

        [TestMethod]
        public void Overspeed_ReasonDistinguishesTotalFromPenalized()
        {
            var d = PerfectFlight();
            d.OverspeedCount = 3;
            d.OverspeedPenaltyCount = 1;

            var reason = Score(d).Deductions.First(x => x.Criterion == "Overspeed").Reason;
            StringAssert.Contains(reason, "3 event(s)");
            StringAssert.Contains(reason, "ATC exempt: 2");
        }

        // ══ Lights Compliance (max 10) ═══════════════════════════════════════════

        [DataTestMethod]
        [DataRow(0, 0,  "sin violaciones")]
        [DataRow(1, 5,  "una violacion")]
        [DataRow(2, 10, "dos violaciones alcanzan el tope")]
        [DataRow(5, 10, "el tope no se supera")]
        public void Lights_Thresholds(int violations, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.LightsViolations = violations;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "Lights Compliance"), because);
        }

        // ══ Stabilized Approach (max 15) ═════════════════════════════════════════

        [DataTestMethod]
        [DataRow(0,  0,  "estabilizado")]
        [DataRow(5,  5,  "una infraccion")]
        [DataRow(15, 15, "suma cruda que coincide con el tope")]
        [DataRow(30, 15, "la suma cruda del gate (hasta 30) se colapsa al tope de 15")]
        public void StabilizedApproach_Thresholds(int rawDeductions, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.StabilizedApproachDeductions = rawDeductions;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "Stabilized Approach"), because);
        }

        // ══ QNH Compliance (max 10, dos componentes independientes) ══════════════

        [DataTestMethod]
        [DataRow(0, 0,  "salida y llegada correctas")]
        [DataRow(1, 5,  "solo salida o solo llegada")]
        [DataRow(2, 10, "salida y llegada incorrectas")]
        [DataRow(3, 10, "el tope es 10 aunque haya mas de dos")]
        public void Qnh_Thresholds(int violations, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.QnhViolations = violations;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "QNH Compliance"), because);
        }

        // ══ Standard Pressure (max 5) — criterio propio desde v0.9.3 ════════════

        [TestMethod]
        public void StdPressure_DeductsFiveWhenNotSet()
        {
            var d = PerfectFlight();
            d.StdPressureViolation = true;
            Assert.AreEqual(5, DeductionFor(Score(d), "Standard Pressure"));
        }

        [TestMethod]
        public void StdPressure_DoesNotDeductWhenSet()
        {
            var d = PerfectFlight();
            d.StdPressureViolation = false;
            Assert.IsFalse(Score(d).Deductions.Any(x => x.Criterion == "Standard Pressure"));
        }

        [TestMethod]
        public void StdPressure_IsIndependentOfQnhViolations()
        {
            // Este es el bug que motivo separar el contador: un STD incorrecto no debe
            // consumir el tope del QNH de salida/llegada.
            var d = PerfectFlight();
            d.StdPressureViolation = true;
            d.QnhViolations = 2;   // salida + llegada, ambas mal

            var r = Score(d);
            Assert.AreEqual(5,  DeductionFor(r, "Standard Pressure"), "el STD es su propio criterio");
            Assert.AreEqual(10, DeductionFor(r, "QNH Compliance"),    "el QNH conserva su tope completo");
            Assert.AreEqual(15, r.Deductions.Sum(x => x.PointsDeducted),
                "ambos criterios se suman en vez de competir por el mismo tope");
        }

        [TestMethod]
        public void StdPressure_AndArrivalQnh_AreIndependentlyVisible()
        {
            var d = PerfectFlight();
            d.StdPressureViolation = true;
            d.QnhViolations = 1;

            var r = Score(d);
            Assert.AreEqual(2, r.Deductions.Count(x =>
                x.Criterion == "Standard Pressure" || x.Criterion == "QNH Compliance"),
                "cada problema debe aparecer en su propia linea del desglose");
        }

        // ══ Touchdown Zone (max 7) ═══════════════════════════════════════════════

        [DataTestMethod]
        [DataRow(0,     0, "sin dato: criterio omitido")]
        [DataRow(1500,  0, "limite de la zona de toma")]
        [DataRow(1501,  3, "justo fuera de la zona")]
        [DataRow(2500,  3, "limite de la banda media")]
        [DataRow(2501,  7, "muy largo")]
        [DataRow(6000,  7, "tope")]
        public void TouchdownZone_Thresholds(double distFt, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.TouchdownDistanceFt = distFt;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "Touchdown Zone"), because);
        }

        [TestMethod]
        public void TouchdownZone_IsOmittedWhenNoRunwayData()
        {
            var d = PerfectFlight();
            d.TouchdownDistanceFt = 0;
            Assert.IsFalse(Score(d).Deductions.Any(x => x.Criterion == "Touchdown Zone"));
        }

        // ══ Centreline Deviation (max 7) ═════════════════════════════════════════

        [DataTestMethod]
        [DataRow(0,    0, "sin dato: criterio omitido")]
        [DataRow(10,   0, "limite sin penalizar")]
        [DataRow(11,   3, "justo por encima de 10 ft")]
        [DataRow(30,   3, "limite de la banda media")]
        [DataRow(31,   7, "por encima de 30 ft")]
        [DataRow(200,  7, "tope")]
        public void Centreline_Thresholds(double devFt, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.CenterlineDeviationFt = devFt;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "Centreline"), because);
        }

        // ══ Localizer Alignment (max 5) ══════════════════════════════════════════

        [TestMethod]
        public void Localizer_IsOmittedWhenIlsTunedAndNoViolations()
        {
            var d = PerfectFlight();
            d.IlsTunedCorrectly = true;
            d.LocalizerViolations = 0;
            Assert.IsFalse(Score(d).Deductions.Any(x => x.Criterion == "Localizer Alignment"));
        }

        [TestMethod]
        public void Localizer_NotTuned_DeductsThree()
        {
            var d = PerfectFlight();
            d.IlsTunedCorrectly = false;
            d.LocalizerViolations = 0;
            Assert.AreEqual(3, DeductionFor(Score(d), "Localizer Alignment"));
        }

        [DataTestMethod]
        [DataRow(1, 1, "una desviacion")]
        [DataRow(2, 2, "dos desviaciones")]
        [DataRow(5, 2, "las desviaciones se limitan a 2 puntos")]
        public void Localizer_Deviations_AreCappedAtTwoPoints(int violations, int expected, string because)
        {
            var d = PerfectFlight();
            d.IlsTunedCorrectly = true;
            d.LocalizerViolations = violations;
            Assert.AreEqual(expected, DeductionFor(Score(d), "Localizer Alignment"), because);
        }

        [TestMethod]
        public void Localizer_NotTunedPlusDeviations_IsCappedAtFive()
        {
            var d = PerfectFlight();
            d.IlsTunedCorrectly = false;
            d.LocalizerViolations = 4;   // 3 + min(2,4) = 5, justo en el tope
            Assert.AreEqual(5, DeductionFor(Score(d), "Localizer Alignment"));
        }

        // ══ Minimums Compliance (max 5) ══════════════════════════════════════════

        [TestMethod]
        public void Minimums_Busted_DeductsFive()
        {
            var d = PerfectFlight();
            d.BelowMinimums = true;
            Assert.AreEqual(5, DeductionFor(Score(d), "Minimums Compliance"));
        }

        [TestMethod]
        public void Minimums_NotBusted_DeductsNothing()
        {
            var d = PerfectFlight();
            d.BelowMinimums = false;
            Assert.IsFalse(Score(d).Deductions.Any(x => x.Criterion == "Minimums Compliance"));
        }

        // ══ IVAO Offline (max 5) ═════════════════════════════════════════════════

        [TestMethod]
        public void IvaoOffline_DeductsFive()
        {
            var d = PerfectFlight();
            d.WasOfflineFlight = true;
            Assert.AreEqual(5, DeductionFor(Score(d), "IVAO Presence"));
        }

        [TestMethod]
        public void IvaoOnline_DeductsNothing()
        {
            var d = PerfectFlight();
            d.WasOfflineFlight = false;
            Assert.IsFalse(Score(d).Deductions.Any(x => x.Criterion == "IVAO Presence"));
        }

        // ══ On-Time Departure (max 5) ════════════════════════════════════════════

        [TestMethod]
        public void DepartedLate_DeductsFive()
        {
            var d = PerfectFlight();
            d.DepartedLate = true;
            Assert.AreEqual(5, DeductionFor(Score(d), "On-Time Departure"));
        }

        [TestMethod]
        public void DepartedOnTime_DeductsNothing()
        {
            var d = PerfectFlight();
            d.DepartedLate = false;
            Assert.IsFalse(Score(d).Deductions.Any(x => x.Criterion == "On-Time Departure"));
        }

        // ══ Procedure Speed (max 10, 3 pts por evento) ═══════════════════════════

        [DataTestMethod]
        [DataRow(0, 0,  "sin violaciones")]
        [DataRow(1, 3,  "una violacion")]
        [DataRow(2, 6,  "dos violaciones")]
        [DataRow(3, 9,  "tres violaciones")]
        [DataRow(4, 10, "cuatro alcanzan el tope de 10")]
        [DataRow(9, 10, "el tope no se supera")]
        public void ProcedureSpeed_Thresholds(int violations, int expectedDeduction, string because)
        {
            var d = PerfectFlight();
            d.ProcedureSpdViolations = violations;
            Assert.AreEqual(expectedDeduction, DeductionFor(Score(d), "Procedure Speed"), because);
        }

        // ══ Engine Stabilization (max 5) ═════════════════════════════════════════

        [TestMethod]
        public void EngineStabilization_DeductsFive()
        {
            var d = PerfectFlight();
            d.EngineStabilizationViolation = true;
            Assert.AreEqual(5, DeductionFor(Score(d), "Engine Stabilization"));
        }

        [TestMethod]
        public void EngineStabilization_DoesNotDeductWhenStable()
        {
            var d = PerfectFlight();
            d.EngineStabilizationViolation = false;
            Assert.IsFalse(Score(d).Deductions.Any(x => x.Criterion == "Engine Stabilization"));
        }

        // ══ Bonus Single Engine Taxi (+5) ════════════════════════════════════════

        [TestMethod]
        public void SingleEngineTaxi_Turboprop_AlwaysGetsBonus()
        {
            var d = PerfectFlight();
            d.EngineType = ScoredEngineType.Turboprop;
            d.SingleEngineTaxi = true;

            var r = Score(d);
            Assert.AreEqual(5, r.SingleEngineTaxiBonus);
            Assert.AreEqual(100, r.TotalScore);
        }

        [TestMethod]
        public void SingleEngineTaxi_Piston_NeverGetsBonus()
        {
            var d = PerfectFlight();
            d.EngineType = ScoredEngineType.Piston;
            d.SingleEngineTaxi = true;

            var r = Score(d);
            Assert.AreEqual(0, r.SingleEngineTaxiBonus);
        }

        [TestMethod]
        public void SingleEngineTaxi_Jet_GetsBonusWhenLifecycleCompliant()
        {
            var d = PerfectFlight();
            d.EngineType = ScoredEngineType.Jet;
            d.SingleEngineTaxi = true;
            d.EngineWarmupViolation = false;
            d.EngineCooldownViolation = false;
            d.EngineStabilizationViolation = false;

            Assert.AreEqual(5, Score(d).SingleEngineTaxiBonus);
        }

        [TestMethod]
        public void SingleEngineTaxi_Jet_DeniedOnWarmupViolation_WithReason()
        {
            var d = PerfectFlight();
            d.EngineType = ScoredEngineType.Jet;
            d.SingleEngineTaxi = true;
            d.EngineWarmupViolation = true;

            var r = Score(d);
            Assert.AreEqual(0, r.SingleEngineTaxiBonus);
            StringAssert.Contains(r.SingleEngineTaxiDeniedReason, "warm-up");
        }

        [TestMethod]
        public void SingleEngineTaxi_Jet_DeniedOnCooldownViolation_WithReason()
        {
            var d = PerfectFlight();
            d.EngineType = ScoredEngineType.Jet;
            d.SingleEngineTaxi = true;
            d.EngineCooldownViolation = true;

            var r = Score(d);
            Assert.AreEqual(0, r.SingleEngineTaxiBonus);
            StringAssert.Contains(r.SingleEngineTaxiDeniedReason, "cool-down");
        }

        [TestMethod]
        public void SingleEngineTaxi_Jet_DeniedOnStabilizationViolation_WithReason()
        {
            var d = PerfectFlight();
            d.EngineType = ScoredEngineType.Jet;
            d.SingleEngineTaxi = true;
            d.EngineStabilizationViolation = true;

            var r = Score(d);
            Assert.AreEqual(0, r.SingleEngineTaxiBonus);
            StringAssert.Contains(r.SingleEngineTaxiDeniedReason, "estabilizados");
        }

        [TestMethod]
        public void SingleEngineTaxi_Jet_ListsEveryUnmetRequirement()
        {
            var d = PerfectFlight();
            d.EngineType = ScoredEngineType.Jet;
            d.SingleEngineTaxi = true;
            d.EngineWarmupViolation = true;
            d.EngineCooldownViolation = true;

            var reason = Score(d).SingleEngineTaxiDeniedReason;
            StringAssert.Contains(reason, "warm-up");
            StringAssert.Contains(reason, "cool-down");
        }

        [TestMethod]
        public void SingleEngineTaxi_NotDetected_NoBonusAndNoDenialReason()
        {
            var d = PerfectFlight();
            d.SingleEngineTaxi = false;

            var r = Score(d);
            Assert.AreEqual(0, r.SingleEngineTaxiBonus);
            Assert.IsNull(r.SingleEngineTaxiDeniedReason,
                "sin single-engine taxi no hay nada que denegar");
        }

        [TestMethod]
        public void Bonus_CannotPushScoreAbove100()
        {
            var r = Score(PerfectFlight());
            Assert.AreEqual(100, r.TotalScore, "el bonus no puede superar el maximo de 100");
        }

        [TestMethod]
        public void Bonus_IsAppliedAfterDeductions()
        {
            var d = PerfectFlight();
            d.EngineType = ScoredEngineType.Turboprop;
            d.SingleEngineTaxi = true;
            d.DepartedLate = true;   // −5

            Assert.AreEqual(100, Score(d).TotalScore,
                "95 + 5 de bonus vuelve a 100");
        }

        // ══ Suelo del score y acumulacion ════════════════════════════════════════

        [TestMethod]
        public void Score_IsFlooredAtZero()
        {
            var d = PerfectFlight();
            d.LandingDataCaptured = true;
            d.LandingRate = -2000;        // −40
            d.LandingGForce = 3.0;        // −15
            d.LandingBank = 20;           // −10
            d.LandingPitch = -5;          // −10
            d.OverspeedCount = 5;
            d.OverspeedPenaltyCount = 5;  // −15
            d.LightsViolations = 5;       // −10
            d.StabilizedApproachDeductions = 30; // −15
            d.QnhViolations = 3;          // −10
            d.StdPressureViolation = true;// −5
            d.WasOfflineFlight = true;    // −5
            d.DepartedLate = true;        // −5
            d.TouchdownDistanceFt = 5000; // −7
            d.CenterlineDeviationFt = 200;// −7
            d.IlsTunedCorrectly = false;
            d.LocalizerViolations = 5;    // −5
            d.BelowMinimums = true;       // −5
            d.ProcedureSpdViolations = 9; // −10
            d.EngineStabilizationViolation = true; // −5
            d.SingleEngineTaxi = false;

            Assert.AreEqual(0, Score(d).TotalScore);
        }

        [TestMethod]
        public void AllCriteria_AreIndividuallyAttributable()
        {
            // Un vuelo con 17 problemas debe producir 17 lineas de desglose distintas:
            // si dos criterios compartieran contador (el bug de STD/QNH), faltaria una.
            var d = PerfectFlight();
            d.LandingDataCaptured = true;
            d.LandingRate = -2000;
            d.LandingGForce = 3.0;
            d.LandingBank = 20;
            d.LandingPitch = -5;
            d.OverspeedCount = 5; d.OverspeedPenaltyCount = 5;
            d.LightsViolations = 5;
            d.StabilizedApproachDeductions = 30;
            d.QnhViolations = 3;
            d.StdPressureViolation = true;
            d.WasOfflineFlight = true;
            d.DepartedLate = true;
            d.TouchdownDistanceFt = 5000;
            d.CenterlineDeviationFt = 200;
            d.IlsTunedCorrectly = false; d.LocalizerViolations = 5;
            d.BelowMinimums = true;
            d.ProcedureSpdViolations = 9;
            d.EngineStabilizationViolation = true;
            d.SingleEngineTaxi = false;

            var criteria = Score(d).Deductions.Select(x => x.Criterion).ToList();
            Assert.AreEqual(17, criteria.Count);
            CollectionAssert.AllItemsAreUnique(criteria);
        }

        // ══ Ratings ══════════════════════════════════════════════════════════════

        [DataTestMethod]
        [DataRow(-100, "Butter")]
        [DataRow(-150, "Butter")]
        [DataRow(-151, "Smooth")]
        [DataRow(-250, "Smooth")]
        [DataRow(-251, "Normal")]
        [DataRow(-350, "Normal")]
        [DataRow(-351, "Hard")]
        [DataRow(-450, "Hard")]
        [DataRow(-451, "Very Hard")]
        [DataRow(-650, "Very Hard")]
        [DataRow(-651, "Slam")]
        [DataRow(-1000, "Slam")]
        public void LandingRating_Thresholds(int rate, string expected)
        {
            var d = PerfectFlight();
            d.LandingRate = rate;
            Assert.AreEqual(expected, Score(d).LandingRating);
        }
    }
}
