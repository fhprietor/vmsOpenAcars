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
        public static string CpuSummary { get; private set; } = string.Empty;
        public static string GpuSummary { get; private set; } = string.Empty;
        public static string SimSummary { get; private set; } = string.Empty;

        public static void Initialize()
        {
            try { OsSummary = $"{GetOsName()} / RAM {GetRamString()}"; } catch { }
            try { CpuSummary = GetCpuString(); } catch { }
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

        private static string GetCpuString()
        {
            string name = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString", null) as string;
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            name = name.Trim();
            int threads = Environment.ProcessorCount;
            return $"{name} / {threads} threads";
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

            // Registry stores MemorySize as a 4-byte DWORD on many drivers.
            // All common VRAM sizes (4/8/12/16/24 GB) are exact multiples of 4 GB = 2^32,
            // which overflows uint32 to exactly 0. DXGI reports the correct SIZE_T value.
            if (bestVram == 0)
                bestVram = TryGetVramViaDxgi(bestName != "?" ? bestName : null);

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

        // ── DXGI VRAM fallback ────────────────────────────────────────────────

        [DllImport("dxgi.dll", PreserveSig = true)]
        private static extern int CreateDXGIFactory(
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppFactory);

        private static ulong TryGetVramViaDxgi(string gpuName)
        {
            var iid = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
            try
            {
                if (CreateDXGIFactory(ref iid, out object factoryObj) < 0 || factoryObj == null)
                    return 0;
                var factory = factoryObj as IDXGIFactory_SIH;
                if (factory == null) { Marshal.ReleaseComObject(factoryObj); return 0; }
                try
                {
                    ulong bestMatch = 0, bestAny = 0;
                    const int DXGI_ERROR_NOT_FOUND = unchecked((int)0x887A0002);
                    for (uint i = 0; ; i++)
                    {
                        int hr = factory.EnumAdapters(i, out IDXGIAdapter_SIH adapter);
                        if (hr == DXGI_ERROR_NOT_FOUND) break;
                        if (hr < 0 || adapter == null) break;
                        try
                        {
                            if (adapter.GetDesc(out DxgiAdapterDesc_SIH d) < 0) continue;
                            ulong vram = (ulong)d.DedicatedVideoMemory;
                            if (vram == 0) continue;
                            if (vram > bestAny) bestAny = vram;
                            if (!string.IsNullOrEmpty(gpuName) && d.Description != null)
                            {
                                bool match =
                                    d.Description.IndexOf(gpuName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    gpuName.IndexOf(d.Description, StringComparison.OrdinalIgnoreCase) >= 0;
                                if (match && vram > bestMatch) bestMatch = vram;
                            }
                        }
                        finally { Marshal.ReleaseComObject(adapter); }
                    }
                    return bestMatch > 0 ? bestMatch : bestAny;
                }
                finally { Marshal.ReleaseComObject(factory); }
            }
            catch { return 0; }
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

    // ── DXGI COM declarations ─────────────────────────────────────────────────
    // Vtable order must match the DXGI SDK headers exactly (IUnknown slots are
    // implicit with InterfaceIsIUnknown; IDXGIObject methods precede each interface).

    [ComImport, Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDXGIAdapter_SIH
    {
        // IDXGIObject
        [PreserveSig] int SetPrivateData(ref Guid name, uint dataSize, IntPtr pData);
        [PreserveSig] int SetPrivateDataInterface(ref Guid name, IntPtr pUnknown);
        [PreserveSig] int GetPrivateData(ref Guid name, ref uint pDataSize, IntPtr pData);
        [PreserveSig] int GetParent(ref Guid riid, out IntPtr ppParent);
        // IDXGIAdapter
        [PreserveSig] int EnumOutputs(uint output, out IntPtr ppOutput);
        [PreserveSig] int GetDesc(out DxgiAdapterDesc_SIH pDesc);
        [PreserveSig] int CheckInterfaceSupport(ref Guid interfaceName, out long pUMDVersion);
    }

    [ComImport, Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDXGIFactory_SIH
    {
        // IDXGIObject
        [PreserveSig] int SetPrivateData(ref Guid name, uint dataSize, IntPtr pData);
        [PreserveSig] int SetPrivateDataInterface(ref Guid name, IntPtr pUnknown);
        [PreserveSig] int GetPrivateData(ref Guid name, ref uint pDataSize, IntPtr pData);
        [PreserveSig] int GetParent(ref Guid riid, out IntPtr ppParent);
        // IDXGIFactory
        [PreserveSig] int EnumAdapters(uint adapter, out IDXGIAdapter_SIH ppAdapter);
        [PreserveSig] int MakeWindowAssociation(IntPtr windowHandle, uint flags);
        [PreserveSig] int GetWindowAssociation(out IntPtr pWindowHandle);
        [PreserveSig] int CreateSwapChain(IntPtr pDevice, IntPtr pDesc, out IntPtr ppSwapChain);
        [PreserveSig] int CreateSoftwareAdapter(IntPtr module, out IDXGIAdapter_SIH ppAdapter);
    }

    // Mirrors native DXGI_ADAPTER_DESC layout (64-bit): Description[128] = 256 B,
    // 4×UINT = 16 B, 3×SIZE_T (UIntPtr) = 24 B, LUID (DWORD+LONG) = 8 B → 304 B total.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DxgiAdapterDesc_SIH
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string  Description;
        public uint    VendorId;
        public uint    DeviceId;
        public uint    SubSysId;
        public uint    Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public uint    LuidLow;
        public int     LuidHigh;
    }
}
