using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.Services.Interfaces;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.ViewModels
{
    internal sealed class AcarsReporter
    {
        private readonly FlightManager           _flightManager;
        private readonly IApiService             _apiService;
        private readonly FsuipcService           _fsuipc;
        private readonly ILandingLogService      _landingLogService;
        private readonly SimbriefEnhancedService _simbriefEnhanced;
        private readonly TelemetryCoordinator    _tc;
        private readonly AcarsReporterCallbacks  _cb;

        internal DateTime LastCheckpointSent { get; set; } = DateTime.MinValue;
        internal int      ProcSpdViolations  { get; set; } = 0;

        internal AcarsReporter(
            FlightManager           flightManager,
            IApiService             apiService,
            FsuipcService           fsuipc,
            ILandingLogService      landingLogService,
            SimbriefEnhancedService simbriefEnhanced,
            TelemetryCoordinator    tc,
            AcarsReporterCallbacks  cb)
        {
            _flightManager     = flightManager;
            _apiService        = apiService;
            _fsuipc            = fsuipc;
            _landingLogService = landingLogService;
            _simbriefEnhanced  = simbriefEnhanced;
            _tc                = tc;
            _cb                = cb;
        }

        internal void Reset()
        {
            LastCheckpointSent = DateTime.MinValue;
            ProcSpdViolations  = 0;
        }

        internal bool ShouldSendCheckpoint(int intervalSeconds)
            => (DateTime.UtcNow - LastCheckpointSent).TotalSeconds >= intervalSeconds;

        // ── Plan summary log ──────────────────────────────────────────────────────

        internal void LogPlanSummary(SimbriefPlan p)
        {
            if (p == null) return;
            string origIata = string.IsNullOrEmpty(p.OriginIata)      ? "---" : p.OriginIata;
            string destIata = string.IsNullOrEmpty(p.DestinationIata) ? "---" : p.DestinationIata;
            string date     = p.ScheduledOffTime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(p.ScheduledOffTime).UtcDateTime.ToString("ddMMMyyyy").ToUpper()
                : DateTimeOffset.UtcNow.ToString("ddMMMyyyy").ToUpper();
            string tripStr  = p.TripFuel > 0 ? $"  TRIP {p.TripFuel:F0}" : "";
            _cb.Log?.Invoke(
                $"📋 {p.Airline}{p.FlightNumber}  {p.Origin}/{origIata} → {p.Destination}/{destIata}" +
                $"  {p.AircraftIcao} {p.Registration}  {date}", Theme.Success);
            _cb.Log?.Invoke(
                $"   PAX {p.PaxCount}  FUEL {p.BlockFuel:F0}{tripStr}  CARGO {p.CargoWeight:F0}  FL{p.CruiseAltitude / 100}",
                Theme.MainText);
        }

        // ── Landing / Block position reports ─────────────────────────────────────

        internal void HandleLandingDetected(int verticalSpeed, double gforce, double pitch, double bank)
        {
            var rec = new AcarsPosition
            {
                type         = 0,  status   = "LDG", nav_type = 0, name = "TOUCHDOWN",
                lat          = _flightManager.CurrentLat,
                lon          = _flightManager.CurrentLon,
                altitude     = _flightManager.CurrentAltitude,
                altitude_agl = 0,
                heading      = (int)_fsuipc.CurrentHeading,
                vs           = verticalSpeed,
                gs           = _flightManager.CurrentGroundSpeed,
                ias          = _flightManager.CurrentIndicatedAirspeed,
                gforce       = gforce,  pitch = pitch,  bank = bank,
                sim_time     = DateTime.UtcNow,  source = "vmsOpenAcars"
            };
            Task.Run(async () =>
            {
                var upd = new AcarsPositionUpdate { positions = new[] { rec } };
                await _apiService.SendPositionUpdate(_flightManager.ActivePirepId, upd);
                _cb.Log?.Invoke(_("Log_LandingRecorded", verticalSpeed, $"{gforce:F2}",
                    (int)_fsuipc.CurrentHeading, $"{pitch:F1}", $"{bank:F1}"), Theme.Success);
            });

            int abs = Math.Abs(verticalSpeed);
            OsdSeverity sev   = abs <= 300 ? OsdSeverity.Success : abs <= 600 ? OsdSeverity.Warning : OsdSeverity.Critical;
            string      label = abs <= 300 ? "TOUCHDOWN"         : abs <= 600 ? "FIRM LANDING"      : "HARD LANDING";
            _cb.OsdMessage?.Invoke($"{label}  {verticalSpeed} FPM  {gforce:F2} G", sev);
        }

        internal void HandleBlockDetected()
        {
            _cb.Log?.Invoke(_("Log_OnBlockDetected"), Theme.Success);
            var rec = new AcarsPosition
            {
                type     = 0,  status = "ARR",  name = "ON BLOCK",
                lat      = _flightManager.CurrentLat,
                lon      = _flightManager.CurrentLon,
                altitude = _flightManager.CurrentAltitude,
                heading  = (int)_fsuipc.CurrentHeading,
                sim_time = DateTime.UtcNow,  source = "vmsOpenAcars"
            };
            Task.Run(async () =>
            {
                if (!string.IsNullOrEmpty(_flightManager.ActivePirepId))
                    await _apiService.SendPositionUpdate(
                        _flightManager.ActivePirepId,
                        new AcarsPositionUpdate { positions = new[] { rec } });
            });
        }

        // ── SendPirep ─────────────────────────────────────────────────────────────

        internal async Task SendPirep()
        {
            _flightManager.SetProcedureSpdViolations(ProcSpdViolations);
            var pendingRecord = SnapshotLandingRecord();

            bool filed = false;
            try
            {
                filed = await _flightManager.FilePirep();
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke($"⚠️ Error al enviar PIREP: {ex.Message}", Theme.Danger);
                return;
            }

            if (filed)
            {
                int pirepScore = _flightManager.LastFlightScore;
                OsdSeverity scoreSev = pirepScore >= 80 ? OsdSeverity.Success
                                     : pirepScore >= 60 ? OsdSeverity.Info
                                     : OsdSeverity.Warning;
                _cb.OsdMessage?.Invoke($"PIREP FILED   SCORE {pirepScore} / 100", scoreSev);
                _cb.ButtonStateChanged?.Invoke("START", Color.FromArgb(200, 100, 0), false);
                _cb.ResetTelemetry?.Invoke();
                LastCheckpointSent = DateTime.MinValue;
                _cb.Log?.Invoke("✅ Vuelo reportado, listo para siguiente vuelo", Theme.Success);
                _cb.FlightEnded?.Invoke();
                SaveLandingRecord(pendingRecord);
                Task.Run(RefreshPilotDataAfterPirep);
            }
            else
            {
                _cb.Log?.Invoke("⚠️ No se pudo enviar el PIREP. Verifique la conexión e intente nuevamente.", Theme.Danger);
            }
        }

        // ── Snapshot / Save ───────────────────────────────────────────────────────

        private FlightRecord SnapshotLandingRecord()
        {
            var fm   = _flightManager;
            var plan = fm.ActivePlan;
            return new FlightRecord
            {
                FlightNumber    = plan?.FlightNumber     ?? "",
                Origin          = plan?.Origin           ?? "",
                Destination     = plan?.Destination      ?? "",
                RunwayName      = fm.TouchdownRunwayName ?? "",
                FlightDate      = DateTime.UtcNow,
                LandingRateFpm  = fm.TouchdownFpm        ?? 0,
                GForce          = fm.TouchdownGForce,
                TouchdownDistFt = fm.TouchdownDistanceFt,
                CenterlineDevFt = fm.TouchdownCenterlineFt,
            };
        }

        private void SaveLandingRecord(FlightRecord record)
        {
            int  bufCount = _tc?.ApproachBuffer?.Count ?? 0;
            bool svcOk    = _landingLogService?.IsAvailable ?? false;

            if (!svcOk)
            {
                _cb.Log?.Invoke(_("Log_LandingLogNoService"), Theme.Warning);
                return;
            }
            if (bufCount < 3)
            {
                _cb.Log?.Invoke(_("Log_LandingLogTooFew", bufCount), Theme.Warning);
                return;
            }
            try
            {
                record.Score = _flightManager.LastFlightScore;
                int newId = _landingLogService.SaveFlight(record, _tc.ApproachBuffer);
                if (newId > 0)
                    _cb.Log?.Invoke(_("Log_LandingLogSaved", newId, bufCount, record.RunwayName), Theme.Success);
                else
                    _cb.Log?.Invoke(_("Log_LandingLogBadId", newId), Theme.Danger);
                _tc?.ApproachBuffer?.Clear();
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke(_("Log_LandingLogError", ex.Message), Theme.Danger);
            }
        }

        // ── RefreshPilotData ──────────────────────────────────────────────────────

        private async Task RefreshPilotDataAfterPirep()
        {
            await Task.Delay(5000);
            try
            {
                var result = await _apiService.GetPilotData();
                if (result.Data != null)
                {
                    _flightManager.SetActivePilot(result.Data);
                    _cb.Log?.Invoke(_("Log_BaseUpdated", result.Data.CurrentAirport), Theme.Success);
                    _cb.AirportChanged?.Invoke(result.Data.CurrentAirport);
                    if (_fsuipc.IsConnected)
                    {
                        _flightManager.UpdatePositionValidation(
                            _fsuipc.CurrentLatitude, _fsuipc.CurrentLongitude);
                        _cb.ValidationStatusChanged?.Invoke(_flightManager.PositionValidationStatus);
                    }
                }
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke(_("Log_BaseUpdateError", ex.Message), Theme.Warning);
            }
        }

        // ── CheckAndCleanActivePireps ─────────────────────────────────────────────

        internal async Task<bool> CheckAndCleanActivePireps()
        {
            try
            {
                var activePireps = await _apiService.GetActivePireps();
                if (!activePireps.Any()) return true;

                var pirepInfo = string.Join("\n", activePireps.Select(p =>
                    $"✈️ {p.FlightNumber} | {p.Origin} → {p.Destination} | {p.StateDescription}"));
                var message = $"⚠️ ACTIVE FLIGHT(S) DETECTED ⚠️\n\n" +
                              $"You have {activePireps.Count} active flight(s) in the system:\n" +
                              $"{pirepInfo}\n" +
                              $"• DELETE the active flight(s) and continue\n" +
                              $"• or close this dialog and do nothing";

                if (_cb.ShowConfirmation != null)
                {
                    var result = await _cb.ShowConfirmation(message, "ACTIVE FLIGHTS", EcamDialogButtons.YesNo);
                    if (result == DialogResult.Yes)
                    {
                        bool allDeleted = true;
                        foreach (var pirep in activePireps)
                        {
                            bool deleted = await _apiService.DeletePirepById(pirep.Id);
                            if (!deleted)
                            {
                                _cb.Log?.Invoke(_("Log_ActiveFlightDeleteFail", pirep.FlightNumber), Theme.Danger);
                                allDeleted = false;
                            }
                            else
                            {
                                _cb.Log?.Invoke(_("Log_OrphanedFlightDeleted", pirep.FlightNumber), Theme.Success);
                            }
                        }
                        _cb.Log?.Invoke(
                            allDeleted ? _("Log_OrphansCleared") : _("Log_OrphansPartial"),
                            allDeleted ? Theme.Success : Theme.Warning);
                        return allDeleted;
                    }
                    else
                    {
                        _cb.Log?.Invoke(_("Log_PlannerCancelled"), Theme.MainText);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke(_("Log_ActiveFlightsError", ex.Message), Theme.Danger);
                return true;
            }
        }

        // ── Scoring checkpoint ────────────────────────────────────────────────────

        private string BuildCheckpointLog()
        {
            var fm = _flightManager;
            return $"SC:ov={fm.OverspeedCount}" +
                   $",lt={fm.LightsViolationCount}" +
                   $",sa={fm.StabilizedApproachDeductions}" +
                   $",qnh={fm.QnhViolationCount}" +
                   $",it={(fm.IsOfflineFlight ? 1 : 0)}" +
                   $",od={(fm.DepartedLate ? 1 : 0)}" +
                   $",spd={ProcSpdViolations}" +
                   $",lz={fm.LocalizerViolations}" +
                   $",bm={(fm.BelowMinimums ? 1 : 0)}" +
                   $",ts={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        }

        internal async Task SendScoringCheckpointAsync()
        {
            string pirepId = _flightManager?.ActivePirepId;
            if (string.IsNullOrEmpty(pirepId)) return;

            LastCheckpointSent = DateTime.UtcNow;

            double lat = _flightManager.CurrentLat;
            double lon = _flightManager.CurrentLon;
            int    hdg = (int)_fsuipc.CurrentHeading;

            var chk = new AcarsPositionUpdate
            {
                positions = new[]
                {
                    new AcarsPosition
                    {
                        lat     = lat,
                        lon     = lon,
                        heading = hdg,
                        status  = "CHK",
                        log     = BuildCheckpointLog(),
                        source  = "vmsOpenAcars"
                    }
                }
            };
            await _apiService.SendPositionUpdate(pirepId, chk);
        }

        // ── Resume helpers ────────────────────────────────────────────────────────

        internal async Task ResumeFromAcarsHistoryAsync(string pirepId)
        {
            var acars = await _apiService.GetPirepAcarsAsync(pirepId);
            if (acars == null || acars.Count == 0) return;

            var nonChk = acars
                .Where(a => a.status != "CHK" && !string.IsNullOrEmpty(a.log))
                .ToList();
            foreach (var entry in nonChk.Skip(Math.Max(0, nonChk.Count - 20)))
                _cb.Log?.Invoke($"  [{entry.status ?? "SCH"}]  {entry.log}", Theme.MainText);

            var lastChk = acars.LastOrDefault(a => a.status == "CHK" && !string.IsNullOrEmpty(a.log));
            if (lastChk != null)
                TryRestoreScoringCheckpoint(lastChk.log);
            else
                _cb.OsdMessage?.Invoke("RESUME  NO CHECKPOINT FOUND", OsdSeverity.Warning);
        }

        private void TryRestoreScoringCheckpoint(string log)
        {
            if (string.IsNullOrEmpty(log) || !log.StartsWith("SC:")) return;
            try
            {
                var fields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in log.Substring(3).Split(','))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2 && int.TryParse(kv[1], out int val))
                        fields[kv[0]] = val;
                }
                int  Get(string k)  => fields.TryGetValue(k, out var v) ? v : 0;
                bool GetB(string k) => Get(k) != 0;

                _flightManager.SetResumedPenalties(
                    overspeed:  Get("ov"),
                    lights:     Get("lt"),
                    stabilized: Get("sa"),
                    qnh:        Get("qnh"),
                    offline:    GetB("it"),
                    late:       GetB("od"),
                    procSpd:    0,
                    localizer:  Get("lz"),
                    belowMins:  GetB("bm"));
                ProcSpdViolations = Get("spd");

                long ts = 0;
                if (fields.TryGetValue("ts", out var tsInt)) ts = tsInt;
                var checkpointTime = ts > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime
                    : DateTime.UtcNow;

                _cb.Log?.Invoke(
                    $"  [CHK]  Penalties restored — ov={Get("ov")} lt={Get("lt")} " +
                    $"sa={Get("sa")} qnh={Get("qnh")} spd={Get("spd")} " +
                    $"lz={Get("lz")} (checkpoint {(int)(DateTime.UtcNow - checkpointTime).TotalMinutes} min ago)",
                    Theme.Success);
                _cb.OsdMessage?.Invoke("RESUME  PENALTIES RESTORED", OsdSeverity.Success);
            }
            catch
            {
                _cb.Log?.Invoke("  [CHK]  Could not parse scoring checkpoint", Theme.Warning);
            }
        }

        private async Task ResumeFromSimbriefAsync(Models.Pirep pirep)
        {
            try
            {
                string simbriefUser = System.Configuration.ConfigurationManager.AppSettings["simbrief_user"];
                if (string.IsNullOrEmpty(simbriefUser)) return;

                var plan = await _simbriefEnhanced.FetchAndParseOFP(simbriefUser);
                if (plan == null) return;

                bool originMatch = string.Equals(plan.Origin,      pirep.Origin,      StringComparison.OrdinalIgnoreCase);
                bool destMatch   = string.Equals(plan.Destination, pirep.Destination, StringComparison.OrdinalIgnoreCase);

                if (originMatch && destMatch)
                {
                    _cb.SetActivePlan?.Invoke(plan);
                    _cb.Log?.Invoke(
                        $"  [OFP]  SimBrief plan loaded — {plan.Origin}→{plan.Destination}  FL{plan.CruiseAltitude / 100}",
                        Theme.Success);
                }
                else
                {
                    _cb.Log?.Invoke(
                        $"  [OFP]  SimBrief plan mismatch ({plan.Origin}→{plan.Destination}), skipped",
                        Theme.Warning);
                }
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke($"  [OFP]  SimBrief reload failed: {ex.Message}", Theme.Warning);
            }
        }

        internal async Task CheckAndResumeFlight(Pilot pilot)
        {
            try
            {
                var activePireps = await _apiService.GetActivePireps();
                if (!activePireps.Any()) return;

                var candidate = activePireps
                    .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                    .First();

                var detail = await _apiService.GetPirepDetail(candidate.Id);
                if (detail == null) detail = candidate;

                var lastUpdate = !string.IsNullOrEmpty(detail.UpdatedAt) ? detail.UpdatedAt : detail.CreatedAt;
                var minutesAgo = "";
                if (DateTime.TryParse(lastUpdate, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var lastDt))
                    minutesAgo = $"{(int)(DateTime.UtcNow - lastDt.ToUniversalTime()).TotalMinutes} min ago";

                var message = $"🔄 ACTIVE FLIGHT FOUND\n\n" +
                              $"Flight:       {detail.FlightNumber}\n" +
                              $"Route:        {detail.Origin} → {detail.Destination}\n" +
                              $"Aircraft:     {detail.AircraftType}\n" +
                              $"Flight time:  {detail.FlightTime} min\n" +
                              $"Last update:  {minutesAgo}\n\n" +
                              $"Do you want to resume this flight?";

                if (_cb.ShowConfirmation == null) return;

                var result = await _cb.ShowConfirmation(message, "RESUME FLIGHT?", EcamDialogButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    _flightManager.ResumeFlight(detail, pilot);
                    LastCheckpointSent = DateTime.MinValue;
                    _cb.UpdateFlightInfo?.Invoke();
                    _cb.ButtonStateChanged?.Invoke("ABORT", Color.Red, true);
                    _cb.Log?.Invoke(_("Log_FlightResumed"), Theme.Success);
                    await ResumeFromAcarsHistoryAsync(detail.Id);
                    await ResumeFromSimbriefAsync(detail);
                }
                else
                {
                    _cb.Log?.Invoke(_("Log_ResumeDeclined"), Theme.MainText);
                }
            }
            catch (Exception ex)
            {
                _cb.Log?.Invoke(_("Log_ResumeCheckError", ex.Message), Theme.Warning);
            }
        }
    }
}
