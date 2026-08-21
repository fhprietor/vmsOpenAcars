using System;
using System.Globalization;
using System.Threading.Tasks;
using vmsOpenAcars.Core.Helpers;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Core.Flight
{
    public partial class FlightManager
    {
        private void ResetFlightState()
        {
            ActivePirepId = "";
            _activePlan = null;
            _phaseMachine.Reset();
            _totalDistanceKm  = 0;
            _lastAirborneTime = null;
            _lastPosition     = null;
            _lastPositionTime = null;
            _timer.Reset();
            _initialFuel = 0;
            _totalFuelUsed = 0;
            _fuelAtTakeoffRoll = 0;
            _fuelAtTaxiInStart = 0;
            CurrentFuel = 0;
            _effectiveDestination = null;
            _td.Reset();
            _pen.Reset();
            _approachValidator.Reset();
            _engStartMonitor.Reset();
            _reverserMonitor.Reset();
            _engStabilizedOsdFired = false;
            OnPhaseChanged?.Invoke(CurrentPhase.ToString());
            PhaseChanged?.Invoke(CurrentPhase);
        }

        private async Task UpdatePirepStatus(string statusCode)
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return;
            try
            {
                bool success = await _apiService.UpdatePirep(ActivePirepId, new { status = statusCode });
                if (success) OnLog?.Invoke(_("Log_PirepStatus", statusCode), Theme.MainText);
            }
            catch (Exception ex) { OnLog?.Invoke(_("Log_ErrorPirepStatus", ex.Message), Theme.Danger); }
        }

        private async Task UpdateBlockOffTime()
        {
            if (string.IsNullOrEmpty(ActivePirepId) || _timer.BlockOffRecorded) return;
            try
            {
                bool ok = await _apiService.UpdatePirep(ActivePirepId,
                    new { block_off_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") });
                if (ok)
                {
                    _timer.RecordBlockOff(DateTime.UtcNow);
                    OnLog?.Invoke(_("Log_BlockOff", _timer.ServerBlockOffTime.ToString("HH:mm:ss")), Theme.MainText);

                    if (_activePlan?.ScheduledOutTime > 0)
                    {
                        var std = DateTimeOffset.FromUnixTimeSeconds(_activePlan.ScheduledOutTime).UtcDateTime;
                        double deltaMins = (_timer.ServerBlockOffTime - std).TotalMinutes;
                        if (Math.Abs(deltaMins) > 10)
                        {
                            _pen.DepartedLate = true;
                            string deptKey = deltaMins > 0 ? "Log_DepartedTooLate" : "Log_DepartedTooEarly";
                            OnLog?.Invoke(_( deptKey, (int)Math.Abs(deltaMins), std.ToString("HH:mm")), Theme.Warning);
                        }
                        else
                        {
                            OnLog?.Invoke(_("Log_DepartedOnTime", $"{deltaMins:+0;-0}", std.ToString("HH:mm")), Theme.Success);
                        }
                    }
                }
                else OnLog?.Invoke(_("Log_BlockOffError"), Theme.Warning);
            }
            catch (Exception ex) { OnLog?.Invoke(_("Log_BlockOffSaveError", ex.Message), Theme.Danger); }
        }

        public async Task<bool> CancelFlight()
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return false;
            bool success = await _apiService.DeletePirep(ActivePirepId);
            if (success) { OnLog?.Invoke(_("Log_FlightCancelled"), Theme.Warning); ResetFlightState(); }
            return success;
        }

        public async Task<bool> StartFlight(SimbriefPlan plan, Pilot pilot, double actualFuel)
        {
            if (_apiService == null) { OnLog?.Invoke(string.Format("{0}: ApiService not configured.", _("Error")), Theme.Warning); return false; }
            if (actualFuel <= 0) { OnLog?.Invoke(string.Format("{0}: No fuel data from simulator.", _("Error")), Theme.Warning); return false; }

            // Reset all scoring state before the API call so stale data from a
            // previous flight never survives into a new one, even if prefileing fails.
            _apEngagedCounter = 0;
            _td.Reset();
            _pen.Reset();
            _approachValidator.Reset();

            _activePlan = plan;
            _activePilot = pilot;
            OnLog?.Invoke($"{_("SendingPrefile")}...", Theme.MainText);

            double plannedFuel = plan.BlockFuel;
            plan.BlockFuel = actualFuel;

            var result = await _apiService.PrefileFlight(plan, pilot);
            ActivePirepId = result.pirepId;
            if (!string.IsNullOrEmpty(ActivePirepId))
            {
                _initialFuel   = actualFuel;
                _totalFuelUsed = 0;
                _timer.Start(result.serverCreatedAt);

                if (!string.IsNullOrEmpty(plan.BidId))
                    await _apiService.DeleteBid(plan.BidId);

                await Task.Run(() => UpdatePirepStatus("BST"));
                var perf = AircraftPerformanceTable.Get(_activePlan?.AircraftIcao);
                _approachValidator.VmoKts       = perf.VmoKts;
                _approachValidator.AircraftIcao = _activePlan?.AircraftIcao;
                _approachValidator.DestIcao     = _activePlan?.Destination;
                _phaseMachine.Reset();
                _phaseMachine.SetPhase(FlightPhase.Boarding);
                FlightStartTime = DateTime.Now;
                PhaseChanged?.Invoke(FlightPhase.Boarding);
                return true;
            }
            OnLog?.Invoke(string.Format("{0}: Server did not return a PIREP ID.", _("Error")), Theme.Danger);
            return false;
        }

        public void ResumeFlight(Models.Pirep pirep, Pilot pilot)
        {
            _activePilot = pilot;

            // Reconstruir el plan mínimo necesario para FilePirep y CheckViolations
            _activePlan = new SimbriefPlan
            {
                FlightNumber = pirep.FlightNumber,
                Origin = pirep.Origin,
                Destination = pirep.Destination,
                AircraftIcao = pirep.AircraftType,
                Aircraft = pirep.AircraftType,
                BlockFuel = pirep.BlockFuel,
                PlannedBlockFuel = pirep.BlockFuel,
                Distance = pirep.Distance,
            };

            // Restaurar estado de vuelo
            ActivePirepId = pirep.Id;
            _initialFuel   = pirep.BlockFuel;
            _totalFuelUsed = pirep.FuelUsed;

            DateTime serverCreatedAt = DateTime.TryParse(pirep.CreatedAt, null,
                DateTimeStyles.RoundtripKind, out var created)
                ? created
                : DateTime.UtcNow.AddMinutes(-pirep.FlightTime);
            _timer.StartResumed(serverCreatedAt);

            _phaseMachine.SetPhase(FlightPhaseHelper.FromPirepStatus(pirep.Status));
            FlightStartTime = DateTime.Now;

            var perf = AircraftPerformanceTable.Get(pirep.AircraftType);
            _approachValidator.VmoKts       = perf.VmoKts;
            _approachValidator.AircraftIcao = pirep.AircraftType;
            _approachValidator.DestIcao     = _activePlan?.Destination;

            // El scoring de esta sesión arranca limpio (no podemos recuperar
            // los datos de la sesión anterior)
            _effectiveDestination = null;
            _td.Reset();
            _pen.Reset();
            _approachValidator.Reset();
        }

        public void SetResumedPenalties(int overspeed, int lights, int stabilized,
            int qnh, bool offline, bool late, int procSpd, int localizer, bool belowMins)
        {
            _approachValidator.SetResumedPenalties(overspeed, lights, stabilized, qnh, localizer, belowMins);
            _pen.IsOfflineFlight        = offline;
            _pen.DepartedLate           = late;
            _pen.ProcedureSpdViolations = procSpd;
        }

        public async Task<bool> AbortFlight()
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return false;
            bool success = await _apiService.DeletePirep(ActivePirepId);
            if (success) { OnLog?.Invoke(_("Log_FlightAborted"), Theme.Warning); ResetFlightState(); }
            return success;
        }

        public async Task<bool> FilePirep()
        {
            if (string.IsNullOrEmpty(ActivePirepId)) return false;

            int totalFlightTimeMinutes  = (int)(DateTime.UtcNow - _timer.ServerCreatedAt).TotalMinutes;
            int actualFlightTimeMinutes = totalFlightTimeMinutes;
            if (_timer.ServerBlockOffTime != default(DateTime) && _timer.ServerBlockOnTime != default(DateTime))
                actualFlightTimeMinutes = (int)(_timer.ServerBlockOnTime - _timer.ServerBlockOffTime).TotalMinutes;
            else if (_timer.ServerBlockOffTime != default(DateTime))
                actualFlightTimeMinutes = (int)(DateTime.UtcNow - _timer.ServerBlockOffTime).TotalMinutes;

            double fuelUsed = _initialFuel - CurrentFuel;
            if (fuelUsed < 0) fuelUsed = 0;

            double totalDistanceNm          = _totalDistanceKm * 0.539957;
            double plannedDistance          = _activePlan?.Distance ?? 0;
            int    plannedFlightTimeMinutes = (_activePlan?.EstTimeEnroute ?? 0) / 60;
            double blockFuel                = _activePlan?.BlockFuel ?? 0;

            OnLog?.Invoke(_("Log_PlannedDistance", $"{plannedDistance:F1}"), Theme.MainText);
            OnLog?.Invoke(_("Log_PlannedTime", plannedFlightTimeMinutes), Theme.MainText);
            OnLog?.Invoke(_("Log_ActualDistance", $"{totalDistanceNm:F1}"), Theme.MainText);
            OnLog?.Invoke(_("Log_ActualTime", actualFlightTimeMinutes), Theme.MainText);
            if (_fuelAtTakeoffRoll > 0 && _fuelAtTaxiInStart > 0)
            {
                double taxiOutKg   = _initialFuel      - _fuelAtTakeoffRoll;
                double tripKg      = _fuelAtTakeoffRoll - _fuelAtTaxiInStart;
                double taxiInKg    = _fuelAtTaxiInStart - CurrentFuel;
                double taxiTotalKg = Math.Max(0, taxiOutKg) + Math.Max(0, taxiInKg);
                OnLog?.Invoke(_("Log_FuelSummary",
                    (int)Math.Round(Math.Max(0, taxiTotalKg)),
                    (int)Math.Round(Math.Max(0, tripKg)),
                    (int)Math.Round(fuelUsed)), Theme.MainText);
            }
            else
            {
                OnLog?.Invoke(_("Log_FuelUsed", $"{fuelUsed:F0}"), Theme.MainText);
            }

            var scoreResult = PirepBuilder.ComputeScore(BuildScoreData());
            LastFlightScore = scoreResult.TotalScore;
            PirepBuilder.LogScore(scoreResult, OnLog);

            var finalData = PirepBuilder.BuildPayload(new PirepPayloadArgs
            {
                TotalFlightTimeMinutes   = totalFlightTimeMinutes,
                ActualFlightTimeMinutes  = actualFlightTimeMinutes,
                PlannedFlightTimeMinutes = plannedFlightTimeMinutes,
                TotalDistanceNm          = totalDistanceNm,
                PlannedDistanceNm        = plannedDistance,
                BlockFuel                = blockFuel,
                FuelUsed                 = fuelUsed,
                LandingRateFpm           = _td.Fpm,
                Score                    = scoreResult.TotalScore,
                BlockOnTime              = _timer.ServerBlockOnTime,
            });

            bool success = await _apiService.FilePirep(ActivePirepId, finalData);
            if (!success)
            {
                // phpVMS puede procesar el PIREP y devolver un código no-2xx.
                // Verificamos el estado real antes de asumir fallo.
                try
                {
                    var pirepDetail = await _apiService.GetPirepDetail(ActivePirepId);
                    if (pirepDetail?.Status != null &&
                        pirepDetail.Status != "1" && pirepDetail.Status != "6")
                        success = true;
                }
                catch { }
            }

            if (success)
            {
                ActivePirepId = "";          // impide que CancelFlight borre el PIREP si falla algo después
                OnLog?.Invoke(_("Log_PirepFiled"), Theme.Success);
                ResetFlightState();
                return true;
            }
            return false;
        }

        private FlightScoreData BuildScoreData() => new FlightScoreData
        {
            LandingRate                  = (int)(_td.Fpm ?? 0),
            LandingPitch                 = _td.Pitch,
            LandingBank                  = _td.Bank,
            LandingGForce                = _td.GForce,
            OverspeedCount               = _approachValidator.OverspeedCount,
            OverspeedPenaltyCount        = _approachValidator.OverspeedPenaltyCount,
            LightsViolations             = _approachValidator.LightsViolationCount,
            StabilizedApproachDeductions = _approachValidator.StabilizedDeductions,
            QnhViolations                = _approachValidator.QnhViolations,
            WasOfflineFlight             = _pen.IsOfflineFlight,
            DepartedLate                 = _pen.DepartedLate,
            TouchdownDistanceFt          = _td.DistanceFt,
            CenterlineDeviationFt        = _td.CenterlineDeviationFt,
            RunwayName                   = _td.RunwayName,
            IlsTunedCorrectly            = _approachValidator.IlsTunedCorrectly,
            LocalizerViolations          = _approachValidator.LocalizerViolations,
            BelowMinimums                = _approachValidator.BelowMinimums,
            SingleEngineTaxi             = _pen.SingleEngineTaxiDetected && _pen.BothEnginesRunning,
            EngineType                   = LastRawData?.EngineCategory == FsuipcService.AircraftCategory.Piston    ? Models.ScoredEngineType.Piston
                                         : LastRawData?.EngineCategory == FsuipcService.AircraftCategory.Turboprop ? Models.ScoredEngineType.Turboprop
                                         : Models.ScoredEngineType.Jet,
            EngineWarmupViolation        = _pen.EngineWarmupViolation,
            EngineCooldownViolation      = _pen.EngineCooldownViolation,
            EngineStabilizationViolation = _pen.EngineStabilizationViolation,
            ProcedureSpdViolations       = _pen.ProcedureSpdViolations,
        };
    }
}
