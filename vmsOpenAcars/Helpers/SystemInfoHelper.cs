using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using vmsOpenAcars.Core.Helpers;

namespace vmsOpenAcars.Helpers
{
    internal static class SystemInfoHelper
    {
        public static string OsSummary  { get; private set; } = string.Empty;
        public static string GpuSummary { get; private set; } = string.Empty;
        public static string SimSummary { get; private set; } = string.Empty;

        public static void Initialize()
        {
            try { OsSummary = $"{GetOsName()} / RAM {GetRamString()}"; } catch { }
            try
            {
                var (gpuName, vramStr) = GetBestGpu();
                GpuSummary = $"{gpuName} / VRAM {vramStr}";
            }
            catch { }
        }

        public static void SetSimVersion(string simName)
        {
            try { SimSummary = BuildSimSummary(simName); } catch { SimSummary = simName; }
        }

        public static string GetPrefileNotes()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"vmsOpenAcars v{AppInfo.Version}");
            if (!string.IsNullOrEmpty(OsSummary))  sb.AppendLine(OsSummary);
            if (!string.IsNullOrEmpty(GpuSummary)) sb.AppendLine(GpuSummary);
            if (!string.IsNullOrEmpty(SimSummary)) sb.AppendLine(SimSummary);
            return sb.ToString().TrimEnd();
        }

        private static string GetOsName()
        {
            try
            {
                const string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
                string name = Registry.GetValue(key, "ProductName", null) as string;
                if (string.IsNullOrEmpty(name)) return "Windows";

                // ProductName still says "Windows 10" on Win11 builds >= 22000
                string buildStr = Registry.GetValue(key, "CurrentBuildNumber", null) as string;
                if (int.TryParse(buildStr, out int build) && build >= 22000)
                    name = name.Replace("Windows 10", "Windows 11");

                return name;
            }
            catch { return "Windows"; }
        }

        private static string GetRamString()
        {
            try
            {
                var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                if (GlobalMemoryStatusEx(ref ms))
                {
                    double gb = ms.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    return $"{(int)Math.Round(gb)} GB";
                }
            }
            catch { }
            return "?";
        }

        // Returns (name, vramString) of the best GPU:
        // — excludes virtual/software adapters
        // — picks the one with most VRAM (discrete GPUs always have more than integrated)
        // — ties broken by preferring NVIDIA/AMD discrete over Intel/integrated
        private static (string name, string vram) GetBestGpu()
        {
            string bestName = "?";
            ulong  bestVram = 0;
            int    bestRank = -1;

            try
            {
                using (var baseKey = OpenVideoAdaptersKey())
                {
                    if (baseKey == null) return (bestName, "?");
                    foreach (string sub in baseKey.GetSubKeyNames())
                    {
                        if (!int.TryParse(sub, out _)) continue;
                        using (var k = baseKey.OpenSubKey(sub))
                        {
                            string name = k?.GetValue("DriverDesc") as string ?? "";
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            if (IsVirtualAdapter(name)) continue;

                            ulong vram = ReadMemorySize(k);
                            int   rank = DiscreteRank(name);

                            // Rank has absolute priority: discrete always beats integrated.
                            // VRAM is secondary tiebreaker within the same rank tier.
                            if (rank > bestRank || (rank == bestRank && vram > bestVram))
                            {
                                bestName = name;
                                bestVram = vram;
                                bestRank = rank;
                            }
                        }
                    }
                }
            }
            catch { }

            string vramStr = bestVram > 0
                ? $"{(int)Math.Round(bestVram / (1024.0 * 1024.0 * 1024.0))} GB"
                : "?";
            return (bestName, vramStr);
        }

        // Higher = more likely to be the discrete/gaming GPU
        private static int DiscreteRank(string name)
        {
            if (name.Contains("NVIDIA") || name.Contains("GeForce") || name.Contains("Quadro") || name.Contains("RTX") || name.Contains("GTX"))
                return 3;
            if (name.Contains("Radeon RX") || name.Contains("Radeon Pro") || name.Contains("AMD Radeon"))
                return 2;
            if (name.Contains("Intel Arc"))
                return 2;
            if (name.Contains("Intel"))
                return 0;  // integrated
            return 1;
        }

        private static bool IsVirtualAdapter(string name)
        {
            return name.Contains("Microsoft Basic") ||
                   name.Contains("Hyper-V")         ||
                   name.Contains("Remote Desktop")  ||
                   name.Contains("VMware")           ||
                   name.Contains("VirtualBox")       ||
                   name.Contains("Parsec")           ||
                   name.Contains("VDDM");
        }


        private static RegistryKey OpenVideoAdaptersKey()
        {
            return Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
        }

        private static ulong ReadMemorySize(RegistryKey k)
        {
            if (k == null) return 0;
            var raw = k.GetValue("HardwareInformation.MemorySize");
            if (raw is byte[] b)
            {
                if (b.Length >= 8) return BitConverter.ToUInt64(b, 0);
                if (b.Length >= 4) return BitConverter.ToUInt32(b, 0);
            }
            else if (raw is int  i && i > 0) return (ulong)i;
            else if (raw is long l && l > 0) return (ulong)l;
            return 0;
        }

        // ── Simulator ─────────────────────────────────────────────────────────

        private static string BuildSimSummary(string simName)
        {
            string procName = null;
            switch (simName)
            {
                case "MSFS 2024": procName = "FlightSimulator2024"; break;
                case "MSFS 2020": procName = "FlightSimulator";     break;
                case "X-Plane":   procName = "X-Plane";             break;
                case "Prepar3D":  procName = "Prepar3D";            break;
            }

            if (procName != null)
            {
                var procs = Process.GetProcessesByName(procName);
                if (procs.Length > 0)
                {
                    try
                    {
                        var fvi  = FileVersionInfo.GetVersionInfo(procs[0].MainModule.FileName);
                        string v = fvi.ProductVersion ?? fvi.FileVersion ?? "?";
                        // Trim to 3 version parts (e.g. 1.39.15 instead of 1.39.15.0)
                        var parts = v.Split('.');
                        if (parts.Length >= 3) v = $"{parts[0]}.{parts[1]}.{parts[2]}";
                        return $"{simName} / {v}";
                    }
                    catch { }
                }
            }
            return simName;
        }

        // ── P/Invoke ──────────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint  dwLength;
            public uint  dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
