#if !FULL_BUILD
namespace QuartzLauncher.Services;

/// <summary>
/// 【开源版占位实现】帮助页 AI 助手不包含在开源内容中。
/// 完整实现位于私有目录，构建时自动启用（FULL_BUILD）。
/// </summary>
public class HelpAiService
{
    private const string Unavailable = "AI 助手不包含在开源内容中，请查阅帮助文档或加入交流群提问。";

    public string Ask(string input) => Unavailable;

    public string AnalyzeLog(string content) => Unavailable;
}
#endif
