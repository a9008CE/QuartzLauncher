using System.Runtime.InteropServices;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public static class MemoryAdvisor
{
    private const int MinMemoryMb = 1024;
    private const double MaxUsageRatio = 0.95;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    public static long GetTotalPhysicalMemoryMb()
    {
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref memStatus))
            return (long)(memStatus.ullTotalPhys / (1024 * 1024));
        return 8192;
    }

    public static long GetAvailablePhysicalMemoryMb()
    {
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref memStatus))
            return (long)(memStatus.ullAvailPhys / (1024 * 1024));
        return 0;
    }

    public static int GetRecommendedMemory(string? versionId = null, int modCount = 0)
    {
        var totalMb = GetTotalPhysicalMemoryMb();
        var maxAllowed = (int)(totalMb * MaxUsageRatio);
        var baseMemory = versionId switch
        {
            null => 2048,
            var v when v.StartsWith("1.2") => 3072,
            var v when v.StartsWith("1.1") => 2048,
            var v when v.StartsWith("1.0") => 1536,
            _ => 2048
        };

        if (modCount > 50) baseMemory += 2048;
        else if (modCount > 20) baseMemory += 1024;
        else if (modCount > 10) baseMemory += 512;

        var recommended = Math.Clamp(baseMemory, MinMemoryMb, maxAllowed);
        recommended = (recommended / 512) * 512;
        return recommended;
    }

    public static int ApplyAutoMemory(Settings settings, string? versionId = null, int modCount = 0)
    {
        if (!settings.AutoMemory) return settings.MemoryMb;

        var recommended = GetRecommendedMemory(versionId, modCount);
        settings.MemoryMb = recommended;
        return recommended;
    }
}
