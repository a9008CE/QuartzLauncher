#if !FULL_BUILD
namespace QuartzLauncher.Services;

/// <summary>
/// 【开源版占位实现】联网 AI 日志分析不包含在开源内容中，请使用本地日志分析。
/// 完整实现位于私有目录，构建时自动启用（FULL_BUILD）。
/// </summary>
public class McDoctorService
{
    private const string Unavailable = "联网 AI 日志分析不包含在开源内容中，请使用本地「分析日志」功能。";

    public Task<string> AnalyzeLogAsync(string logContent, CancellationToken cancellationToken = default)
        => Task.FromResult(Unavailable);

    public Task<string> AnalyzeLogFileAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.FromResult(Unavailable);
}
#endif
