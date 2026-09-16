#if !FULL_BUILD
namespace QuartzLauncher.Services;

/// <summary>
/// 【开源版占位实现】联网翻译与自动汉化不包含在开源内容中。
/// 完整实现位于私有目录，构建时自动启用（FULL_BUILD）。
/// </summary>
public static class TranslationService
{
    public static Task<string> ToChineseAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(text);
}
#endif
