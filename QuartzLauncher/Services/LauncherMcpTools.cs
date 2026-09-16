#if !FULL_BUILD
namespace QuartzLauncher.Services;

public sealed record MinecraftInstallPlan(
    string MinecraftVersion,
    string Loader,
    string LoaderVersion,
    IReadOnlyList<string> Mods,
    bool Chinese,
    string SourceText);

public sealed record MinecraftInstallResult(
    string InstanceName,
    string InstanceId,
    string MinecraftVersion,
    string Loader,
    IReadOnlyList<string> InstalledMods,
    IReadOnlyList<string> SkippedMods);

public sealed record MinecraftLoaderChoice(string Id, string DisplayName, string Version);

/// <summary>
/// 【开源版占位实现】AI 自动安装（MCP 工具调用）不包含在开源内容中，
/// 请使用「快速安装」或「资源中心」手动安装。
/// 完整实现位于私有目录，构建时自动启用（FULL_BUILD）。
/// </summary>
public sealed class LauncherMcpTools
{
    public const string InstallInstanceTool = "minecraft.install_instance";

    private const string Unavailable = "AI 自动安装不包含在开源内容中，请使用「快速安装」或「资源中心」。";

    public LauncherMcpTools()
    {
    }

    public static bool TryParseInstallPlan(string input, out MinecraftInstallPlan plan)
    {
        plan = null!;
        return false;
    }

    public static bool IsInstallRequest(string input) => false;

    public static string DescribePlan(MinecraftInstallPlan plan) => Unavailable;

    public Task<List<string>> GetMinecraftVersionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<string>());

    public Task<List<MinecraftLoaderChoice>> GetSupportedLoadersAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new List<MinecraftLoaderChoice>());

    public Task<MinecraftInstallResult> ExecuteInstallAsync(
        MinecraftInstallPlan plan,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException(Unavailable);
}
#endif
