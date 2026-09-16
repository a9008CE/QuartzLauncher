using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace QuartzLauncher.Services;

/// <summary>
/// 移植自 PCL2 的「启动前内存优化」：整理物理内存占用，为游戏腾出更多可用内存。
/// 1) 裁剪所有可访问进程的工作集（EmptyWorkingSet）
/// 2) 清空系统备用内存列表（需管理员权限，非管理员时自动跳过）
/// 注意：会显著延长启动耗时，仅在内存不足时建议开启。
/// </summary>
public static class SystemMemoryOptimizer
{
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int systemInformationClass, IntPtr systemInformation, int systemInformationLength);

    private const int SystemMemoryListInformation = 0x50;
    private const int MemoryPurgeStandbyList = 4;

    public static bool IsAdministrator
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>执行内存优化，返回释放的物理内存（MB）。</summary>
    public static Task<long> OptimizeAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            var before = MemoryAdvisor.GetAvailablePhysicalMemoryMb();

            progress?.Report("正在整理进程工作集...");
            TrimWorkingSets(cancellationToken);

            progress?.Report("正在清空系统备用内存...");
            PurgeStandbyList();

            var after = MemoryAdvisor.GetAvailablePhysicalMemoryMb();
            return Math.Max(0, after - before);
        }, cancellationToken);

    private static void TrimWorkingSets(CancellationToken cancellationToken)
    {
        foreach (var process in Process.GetProcesses())
        {
            if (cancellationToken.IsCancellationRequested) return;
            try
            {
                var handle = process.Handle;
                if (handle == IntPtr.Zero) continue;
                if (!EmptyWorkingSet(handle))
                    SetProcessWorkingSetSize(handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch
            {
                // 权限不足的进程直接跳过
            }
            finally
            {
                process.Dispose();
            }
        }

        // 最后整理自己，把刚分配的临时内存也还给系统
        try
        {
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        catch
        {
        }
    }

    private static void PurgeStandbyList()
    {
        if (!IsAdministrator) return;
        try
        {
            var size = sizeof(int);
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.WriteInt32(buffer, MemoryPurgeStandbyList);
                NtSetSystemInformation(SystemMemoryListInformation, buffer, size);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
        }
    }
}
