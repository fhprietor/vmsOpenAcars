using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using vmsOpenAcars.Models;
using vmsOpenAcars.Models.NavData;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.ViewModels
{
    internal sealed class ProcedureRestrictionTracker
    {
        private List<SimbriefWaypoint> _procFixes;
        private int  _procFixIdx;
        private bool _procFixAnnounced;

        internal Action<string, OsdSeverity> OsdMessage;
        internal Action<string, Color>       Log;
        internal Action                      OnSpdViolation;

        internal void Reset()
        {
            _procFixes        = null;
            _procFixIdx       = 0;
            _procFixAnnounced = false;
        }

        // Restarts fix traversal without discarding the loaded restrictions.
        internal void ResetProgress()
        {
            _procFixIdx       = 0;
            _procFixAnnounced = false;
        }

        internal void Load(SimbriefPlan plan)
        {
            Reset();
            try
            {
                if (plan?.Waypoints == null || plan.Waypoints.Count == 0) return;

                NavProcedure sidProc = null, starProc = null;
                if (!string.IsNullOrEmpty(plan.Origin))
                    sidProc = ResolveProcedure(
                        plan.Waypoints.Where(w => (w.Stage ?? "CRZ") == "CLB" && w.Type != "apt")
                                      .Select(w => w.Ident).ToList(),
                        NavDataClient.GetSids(plan.Origin), plan.OriginRunway);

                if (!string.IsNullOrEmpty(plan.Destination))
                    starProc = ResolveProcedure(
                        plan.Waypoints.Where(w => (w.Stage ?? "CRZ") == "DSC" && w.Type != "apt")
                                      .Select(w => w.Ident).ToList(),
                        NavDataClient.GetStars(plan.Destination), plan.DestinationRunway);

                var restrDict = new Dictionary<string, FixRestriction>(StringComparer.OrdinalIgnoreCase);
                foreach (var proc in new[] { sidProc, starProc })
                {
                    if (proc?.Legs == null) continue;
                    foreach (var leg in proc.Legs)
                    {
                        if (string.IsNullOrEmpty(leg.Fix)) continue;
                        if (!leg.AltitudeFt.HasValue && !leg.SpeedKts.HasValue) continue;
                        restrDict[leg.Fix] = new FixRestriction
                        {
                            AltFt    = leg.AltitudeFt,
                            Alt2Ft   = leg.Altitude2Ft,
                            AltDescr = leg.AltDescriptor,
                            SpeedKts = leg.SpeedKts,
                            SpdType  = leg.SpeedLimitType,
                        };
                    }
                }

                if (restrDict.Count == 0) return;

                var procFixes = new List<SimbriefWaypoint>();
                foreach (var wp in plan.Waypoints)
                {
                    if (wp.Type == "apt" || wp.Type == "latlon") continue;
                    string stage = wp.Stage ?? "CRZ";
                    if (stage != "CLB" && stage != "DSC") continue;
                    if (!restrDict.TryGetValue(wp.Ident ?? "", out FixRestriction r)) continue;
                    wp.Restriction = r;
                    procFixes.Add(wp);
                }

                _procFixes = procFixes.Count > 0 ? procFixes : null;
            }
            catch { /* non-critical — proceed without restriction tracking */ }
        }

        internal void Check(double lat, double lon, double iasKt)
        {
            var fixes = _procFixes;
            if (fixes == null || _procFixIdx >= fixes.Count) return;

            var wp = fixes[_procFixIdx];
            double distNm = HaversineNm(lat, lon, wp.Lat, wp.Lon);

            if (!_procFixAnnounced && distNm <= 3.0)
            {
                _procFixAnnounced = true;
                string restrLine = wp.Restriction?.OsdLine();
                string osdMsg = string.IsNullOrEmpty(restrLine)
                    ? wp.Ident
                    : $"{wp.Ident}  {restrLine}";
                OsdMessage?.Invoke($"{_("Osd_ProcNextFix")} {osdMsg}", OsdSeverity.Info);
                Log?.Invoke(
                    string.Format(_("Log_ProcFixApproaching"), wp.Ident, restrLine ?? ""),
                    Theme.MainText);
            }

            if (distNm <= 0.5)
            {
                if (wp.Restriction?.SpeedKts.HasValue == true
                    && (wp.Restriction.SpdType == "max" || wp.Restriction.SpdType == null)
                    && iasKt > wp.Restriction.SpeedKts.Value + 5)
                {
                    OnSpdViolation?.Invoke();
                    Log?.Invoke(
                        $"⚠ SPD RESTRICTION  {wp.Ident}  {(int)iasKt} kt  / {wp.Restriction.SpeedKts} kt limit",
                        Theme.Warning);
                }
                _procFixIdx++;
                _procFixAnnounced = false;
            }
        }

        private static NavProcedure ResolveProcedure(
            IList<string> planIdents, IList<NavProcedure> procedures, string runwayHint)
        {
            if (procedures == null || procedures.Count == 0 || planIdents.Count == 0)
                return null;

            NavProcedure best      = null;
            int          bestScore = 0;

            foreach (var proc in procedures)
            {
                if (!string.IsNullOrEmpty(runwayHint) && !string.IsNullOrEmpty(proc.Runway)
                    && !proc.Runway.Equals(runwayHint, StringComparison.OrdinalIgnoreCase)
                    && !proc.Runway.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (proc.Legs == null) continue;
                var legIdents = new HashSet<string>(
                    proc.Legs.Select(l => l.Fix ?? ""), StringComparer.OrdinalIgnoreCase);
                int score = planIdents.Count(id => legIdents.Contains(id));
                if (score > bestScore) { bestScore = score; best = proc; }
            }
            return best;
        }

        private static double HaversineNm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 3440.065;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
