using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using QuartzLauncher.Models;

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

public sealed class LauncherMcpTools
{
    public const string InstallInstanceTool = "minecraft.install_instance";

    private static readonly Regex VersionRegex = new(@"(?<!\d)1\.\d{1,2}(?:\.\d{1,2})?(?!\d)", RegexOptions.Compiled);
    private static readonly Regex LoaderRegex = new(@"(?<loader>neoforge|forge|fabric|quilt)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ModRegex = new(@"(?:加载(?!器)|安装|添加|下载)\s*(?<mods>[a-zA-Z][a-zA-Z0-9 _+、,，和]*)\s*模组", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EnglishModRegex = new(@"(?:load|install|add|download)\s+(?!minecraft\b)(?<mods>[a-zA-Z][a-zA-Z0-9 _+-]{0,40})\s+mods?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LooseModRegex = new(@"(?:[，,、；;。]|\band\b|和)\s*(?:加载|安装|添加|下载)?\s*(?<mods>[a-zA-Z0-9][a-zA-Z0-9 _+-]{0,40})\s*(?:模组|mod)s?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ChineseModRegex = new(@"(?:加载(?!器)|安装|添加|下载)\s*(?<mods>[\u4e00-\u9fa5]{2,24})\s*模组", RegexOptions.Compiled);

    private readonly MinecraftService _minecraft;
    private readonly LoaderService _loaders;
    private sealed record AiModDownload(string Name, ModVersionItem Version, DownloadItem Item, bool Requested);

    public LauncherMcpTools()
    {
        _minecraft = new MinecraftService(App.Paths, App.Settings);
        _loaders = new LoaderService(App.Paths, App.Settings);
    }

    public static bool TryParseInstallPlan(string input, out MinecraftInstallPlan plan)
    {
        plan = null!;
        if (string.IsNullOrWhiteSpace(input)
            || !Regex.IsMatch(input, "帮我|安装|下载|配置|创建|install|download|configure|create", RegexOptions.IgnoreCase)) return false;

        var version = VersionRegex.Match(input).Value;
        if (string.IsNullOrEmpty(version)) return false;
        var loaderMatch = LoaderRegex.Match(input);
        var loader = loaderMatch.Success ? loaderMatch.Groups["loader"].Value.ToLowerInvariant() : "vanilla";
        var loaderVersion = Regex.IsMatch(input, "最新版|最新|latest", RegexOptions.IgnoreCase) ? "" : ExtractLoaderVersion(input, loader);
        var mods = ExtractMods(input);
        var chinese = Regex.IsMatch(input, "中文|简体中文|chinese", RegexOptions.IgnoreCase);
        if (loader == "neoforge" && version.StartsWith("1.12", StringComparison.OrdinalIgnoreCase)) return false;
        plan = new MinecraftInstallPlan(version, loader, loaderVersion, mods, chinese, input.Trim());
        return true;
    }

    public static bool IsInstallRequest(string input)
        => !string.IsNullOrWhiteSpace(input)
           && Regex.IsMatch(input,
               @"(?:帮我|请|please)\s*(?:下载|安装|创建|配置)|(?:下载|安装|创建|配置).*(?:版本|minecraft|游戏)|(?:install|download|create|configure).*minecraft",
               RegexOptions.IgnoreCase);

    public async Task<List<string>> GetMinecraftVersionsAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await _minecraft.ManifestAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return manifest.GetProperty("versions").EnumerateArray()
            .Where(version => string.Equals(version.GetProperty("type").GetString(), "release",
                StringComparison.OrdinalIgnoreCase))
            .Select(version => version.GetProperty("id").GetString())
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Select(version => version!)
            .ToList();
    }

    public async Task<List<MinecraftLoaderChoice>> GetSupportedLoadersAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        var result = new List<MinecraftLoaderChoice>
        {
            new("vanilla", "原版", "")
        };

        async Task AddLoaderAsync(string id, string displayName, Func<Task<List<string>>> getVersions)
        {
            try
            {
                var versions = await getVersions();
                cancellationToken.ThrowIfCancellationRequested();
                var latest = versions.FirstOrDefault(version => !string.IsNullOrWhiteSpace(version));
                if (!string.IsNullOrWhiteSpace(latest))
                    result.Add(new MinecraftLoaderChoice(id, $"{displayName} · {latest}", latest));
            }
            catch
            {
                // One unavailable loader service must not hide the other choices.
            }
        }

        await AddLoaderAsync("fabric", "Fabric", () => _loaders.FabricVersionsAsync(minecraftVersion));
        await AddLoaderAsync("forge", "Forge", () => _loaders.ForgeVersionsAsync(minecraftVersion));
        await AddLoaderAsync("neoforge", "NeoForge", () => _loaders.NeoForgeVersionsAsync(minecraftVersion));
        await AddLoaderAsync("quilt", "Quilt", () => _loaders.QuiltVersionsAsync(minecraftVersion));
        return result;
    }

    public static string DescribePlan(MinecraftInstallPlan plan)
    {
        var loader = plan.Loader == "vanilla"
            ? "原版"
            : $"{plan.Loader} {(string.IsNullOrEmpty(plan.LoaderVersion) ? "最新版" : plan.LoaderVersion)}";
        var mods = plan.Mods.Count == 0 ? "无" : string.Join("、", plan.Mods);
        var language = plan.Chinese ? "简体中文" : "保持默认语言";
        return $"我准备调用 `{InstallInstanceTool}`：\n\nMinecraft：{plan.MinecraftVersion}\n加载器：{loader}\nMod：{mods}\n语言：{language}\n\n确认后才会开始下载和创建实例。";
    }

    public async Task<MinecraftInstallResult> ExecuteInstallAsync(
        MinecraftInstallPlan plan,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"MCP · 正在获取 Minecraft {plan.MinecraftVersion} 下载清单...");
        List<DownloadItem> baseItems;
        try
        {
            baseItems = await _minecraft.PrepareInstallAsync(plan.MinecraftVersion);
        }
        catch (Exception ex)
        {
            throw new IOException($"获取 Minecraft {plan.MinecraftVersion} 下载清单失败: {ex.Message}", ex);
        }
        progress?.Report($"MCP · 已获取下载清单，共 {baseItems.Count} 个文件，正在加入下载队列...");
        var baseTask = DownloadManager.Instance.Enqueue(
            $"AI 安装 Minecraft {plan.MinecraftVersion}", baseItems,
            workers: App.Settings.Data.DownloadWorkers, category: "version");
        await WaitForDownloadTaskAsync(baseTask, "基础游戏文件", progress, cancellationToken);

        var installedVersionId = plan.MinecraftVersion;
        var loaderVersion = plan.LoaderVersion;
        JavaInfo? installerJava = null;
        if (plan.Loader != "vanilla")
        {
            progress?.Report($"MCP · 正在解析并安装 {plan.Loader}...");
            loaderVersion = await ResolveLoaderVersionAsync(plan.Loader, plan.MinecraftVersion, loaderVersion, cancellationToken);
            if (plan.Loader is "forge" or "neoforge")
                installerJava = await EnsureJavaAsync(plan.MinecraftVersion, progress, cancellationToken);
            installedVersionId = await InstallLoaderAsync(plan.Loader, plan.MinecraftVersion, loaderVersion, installerJava?.Path);
        }

        var instanceId = CreateId(plan.MinecraftVersion, plan.Loader, plan.Mods);
        var instance = new Instance
        {
            Id = instanceId,
            Name = CreateName(plan),
            VersionId = installedVersionId,
            McVersion = plan.MinecraftVersion,
            Loader = plan.Loader,
            LoaderVersion = loaderVersion,
            AutoSetChinese = plan.Chinese,
            VersionIsolation = true,
            UsesVersionDirectory = false
        };
        if (installerJava != null) instance.JavaPath = installerJava.Path;
        var store = new InstanceStore(App.Paths.InstancesDir);
        var root = InstancePathService.EnsureGameDirectory(App.Paths, App.Settings.Data, instance);
        try
        {
            var modDownloads = new List<AiModDownload>();
            var directMods = new List<(string Name, ModVersionItem Version)>();
            var resolvedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resolvedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var installedMods = new List<string>();
            var skippedMods = new List<string>();
            foreach (var modName in plan.Mods.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                progress?.Report($"MCP · 正在匹配 Mod：{modName}...");
                var version = await ResolveModVersionAsync(
                    modName, plan.MinecraftVersion, plan.Loader, cancellationToken);
                if (version == null)
                {
                    skippedMods.Add(modName);
                    progress?.Report($"MCP · 找不到 Mod {modName} 匹配 {plan.MinecraftVersion} 的文件，已跳过。");
                    continue;
                }
                directMods.Add((modName, version));
                AddModDownload(modDownloads, resolvedProjects, resolvedTargets,
                    modName, version, Path.Combine(root, "mods"), requested: true);
            }

            foreach (var directMod in directMods)
            {
                await AddRequiredDependenciesAsync(
                    directMod.Version,
                    plan.MinecraftVersion,
                    plan.Loader,
                    Path.Combine(root, "mods"),
                    modDownloads,
                    resolvedProjects,
                    resolvedTargets,
                    progress,
                    cancellationToken);
            }

            if (skippedMods.Count > 0)
                progress?.Report($"MCP · 已跳过找不到的 Mod：{string.Join("、", skippedMods)}，继续安装其余内容...");

            if (modDownloads.Count > 0)
            {
                var modTask = DownloadManager.Instance.Enqueue(
                    $"AI 安装 Mod · {instance.Name}", modDownloads.Select(download => download.Item).ToList(),
                    workers: App.Settings.Data.DownloadWorkers,
                    category: "resource");
                await WaitForDownloadTaskAsync(modTask, "Mod 文件", progress, cancellationToken);

                var missingMods = modDownloads
                    .Where(download => !DownloadService.ValidFile(download.Item))
                    .Select(download => download.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (missingMods.Count > 0)
                    throw new IOException($"Mod 文件未写入实例目录: {string.Join("、", missingMods)}");

                installedMods.AddRange(modDownloads
                    .Where(download => download.Requested)
                    .Select(download => download.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
                var dependencyCount = modDownloads.Count(download => !download.Requested);
                if (dependencyCount > 0)
                    progress?.Report($"MCP · 已安装 {installedMods.Count} 个指定 Mod 和 {dependencyCount} 个必需前置。");
            }
            else if (plan.Mods.Count > 0)
            {
                progress?.Report("MCP · 没有找到兼容的 Mod 文件，实例将只安装游戏和加载器。");
            }

            progress?.Report("MCP · 正在写入实例配置...");
            store.Create(instance);
            if (!store.List().Any(saved => saved.Id.Equals(instance.Id, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("实例配置写入失败，未找到新建实例");
            return new MinecraftInstallResult(instance.Name, instance.Id, instance.McVersion,
                instance.Loader, installedMods, skippedMods);
        }
        catch
        {
            store.Delete(instance.Id);
            if (Directory.Exists(root)) Directory.Delete(root, true);
            throw;
        }
    }

    private static async Task<ModVersionItem?> ResolveModVersionAsync(
        string modName,
        string minecraftVersion,
        string loader,
        CancellationToken cancellationToken)
    {
        var targetLoader = loader.Equals("vanilla", StringComparison.OrdinalIgnoreCase) ? null : loader;
        var projects = await ModrinthService.SearchModsAsync(modName, minecraftVersion, targetLoader, 10);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var project in RankProjects(projects, modName))
        {
            var version = (await ModrinthService.GetVersionsAsync(project.Id, minecraftVersion, targetLoader))
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.DownloadUrl));
            cancellationToken.ThrowIfCancellationRequested();
            if (version != null) return version;
        }

        var mcmodProjects = await McmodService.SearchModsAsync(modName, 5);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var mcmodProject in mcmodProjects)
        {
            var links = await McmodService.GetOfficialProjectSlugsAsync(mcmodProject.McmodPageUrl);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(links.ModrinthSlug))
            {
                var version = (await ModrinthService.GetVersionsAsync(
                        links.ModrinthSlug, minecraftVersion, targetLoader))
                    .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.DownloadUrl));
                cancellationToken.ThrowIfCancellationRequested();
                if (version != null) return version;
            }

            if (string.IsNullOrWhiteSpace(links.CurseForgeSlug)) continue;
            var curseForgeProject = await CurseForgeService.GetModBySlugAsync(links.CurseForgeSlug);
            if (curseForgeProject == null || !long.TryParse(curseForgeProject.Id, out var projectId)) continue;
            var curseForgeVersion = (await CurseForgeService.GetVersionsAsync(projectId, minecraftVersion))
                .FirstOrDefault(item => LoaderMatches(item, targetLoader));
            cancellationToken.ThrowIfCancellationRequested();
            if (curseForgeVersion == null) continue;
            if (string.IsNullOrWhiteSpace(curseForgeVersion.DownloadUrl)
                && long.TryParse(curseForgeVersion.Id, out var fileId))
            {
                curseForgeVersion.DownloadUrl = await CurseForgeService.GetDownloadUrlAsync(projectId, fileId);
            }
            if (!string.IsNullOrWhiteSpace(curseForgeVersion.DownloadUrl)) return curseForgeVersion;
        }
        return null;
    }

    private static IEnumerable<ModItem> RankProjects(IEnumerable<ModItem> projects, string modName) =>
        projects
            .OrderByDescending(item => item.Slug.Equals(modName, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Name.Equals(modName, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Downloads);

    private static bool LoaderMatches(ModVersionItem version, string? loader) =>
        string.IsNullOrWhiteSpace(loader)
        || version.Loaders.Count == 0
        || version.Loaders.Contains(loader, StringComparer.OrdinalIgnoreCase);

    private static bool GameVersionMatches(ModVersionItem version, string? gameVersion) =>
        string.IsNullOrWhiteSpace(gameVersion)
        || version.GameVersions.Count == 0
        || version.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase);

    private static bool IsCompatible(ModVersionItem version, string? gameVersion, string? loader) =>
        GameVersionMatches(version, gameVersion) && LoaderMatches(version, loader);

    private static void AddModDownload(
        List<AiModDownload> downloads,
        HashSet<string> resolvedProjects,
        HashSet<string> resolvedTargets,
        string name,
        ModVersionItem version,
        string modsDirectory,
        bool requested)
    {
        var projectKey = GetProjectKey(version);
        if (!string.IsNullOrEmpty(projectKey)) resolvedProjects.Add(projectKey);
        var target = Path.Combine(modsDirectory, version.FileName);
        if (!resolvedTargets.Add(target)) return;
        downloads.Add(new AiModDownload(
            name,
            version,
            new DownloadItem(version.DownloadUrl, target, version.Sha1, version.FileSize),
            requested));
    }

    private static async Task AddRequiredDependenciesAsync(
        ModVersionItem parent,
        string minecraftVersion,
        string loader,
        string modsDirectory,
        List<AiModDownload> downloads,
        HashSet<string> resolvedProjects,
        HashSet<string> resolvedTargets,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        foreach (var dependency in parent.Dependencies
                     .Where(item => item.Type.Equals("required", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dependencyKey = GetDependencyKey(dependency);
            if (!string.IsNullOrEmpty(dependencyKey) && resolvedProjects.Contains(dependencyKey)) continue;

            var version = await ResolveRequiredDependencyAsync(
                dependency, minecraftVersion, loader, cancellationToken);
            if (version == null || string.IsNullOrWhiteSpace(version.DownloadUrl)
                                || string.IsNullOrWhiteSpace(version.FileName))
            {
                var identity = !string.IsNullOrWhiteSpace(dependency.ProjectName)
                    ? dependency.ProjectName
                    : !string.IsNullOrWhiteSpace(dependency.ProjectId)
                        ? dependency.ProjectId
                        : dependency.VersionId;
                throw new IOException($"无法解析必需前置 Mod: {identity}");
            }

            var versionKey = GetProjectKey(version);
            if (!string.IsNullOrEmpty(versionKey) && resolvedProjects.Contains(versionKey)) continue;
            var dependencyName = string.IsNullOrWhiteSpace(dependency.ProjectName)
                ? $"前置 {version.ProjectId}"
                : dependency.ProjectName;
            progress?.Report($"MCP · 已匹配必需前置：{dependencyName}");
            AddModDownload(downloads, resolvedProjects, resolvedTargets,
                dependencyName, version, modsDirectory, requested: false);
            await AddRequiredDependenciesAsync(
                version, minecraftVersion, loader, modsDirectory,
                downloads, resolvedProjects, resolvedTargets, progress, cancellationToken);
        }
    }

    private static async Task<ModVersionItem?> ResolveRequiredDependencyAsync(
        ModDependency dependency,
        string minecraftVersion,
        string loader,
        CancellationToken cancellationToken)
    {
        var targetLoader = loader.Equals("vanilla", StringComparison.OrdinalIgnoreCase) ? null : loader;

        if (dependency.Source == ModSource.Modrinth)
        {
            // 指定的精确版本仅在适配当前游戏版本/加载器时使用
            if (!string.IsNullOrWhiteSpace(dependency.VersionId))
            {
                var exact = await ModrinthService.GetVersionAsync(dependency.VersionId);
                if (exact != null && !string.IsNullOrWhiteSpace(exact.DownloadUrl)
                    && IsCompatible(exact, minecraftVersion, targetLoader))
                    return exact;
            }

            if (!string.IsNullOrWhiteSpace(dependency.ProjectId))
            {
                var versions = await ModrinthService.GetVersionsAsync(
                    dependency.ProjectId, minecraftVersion, targetLoader);
                cancellationToken.ThrowIfCancellationRequested();
                // 取适配当前游戏版本/加载器的最新版，而不是全局最新版
                return versions
                    .Where(item => !string.IsNullOrWhiteSpace(item.DownloadUrl))
                    .OrderByDescending(item => item.DateUploaded)
                    .FirstOrDefault();
            }
            return null;
        }

        if (!long.TryParse(dependency.ProjectId, out var projectId)) return null;
        var curseForgeVersion = (await CurseForgeService.GetVersionsAsync(projectId, minecraftVersion))
            .Where(item => LoaderMatches(item, targetLoader))
            .OrderByDescending(item => item.DateUploaded)
            .FirstOrDefault();
        cancellationToken.ThrowIfCancellationRequested();
        if (curseForgeVersion == null) return null;
        if (string.IsNullOrWhiteSpace(curseForgeVersion.DownloadUrl)
            && long.TryParse(curseForgeVersion.Id, out var fileId))
        {
            curseForgeVersion.DownloadUrl = await CurseForgeService.GetDownloadUrlAsync(projectId, fileId);
        }
        return string.IsNullOrWhiteSpace(curseForgeVersion.DownloadUrl) ? null : curseForgeVersion;
    }

    private static string GetProjectKey(ModVersionItem version) =>
        string.IsNullOrWhiteSpace(version.ProjectId)
            ? $"{version.Source}:version:{version.Id}"
            : $"{version.Source}:project:{version.ProjectId}";

    private static string GetDependencyKey(ModDependency dependency) =>
        !string.IsNullOrWhiteSpace(dependency.ProjectId)
            ? $"{dependency.Source}:project:{dependency.ProjectId}"
            : !string.IsNullOrWhiteSpace(dependency.VersionId)
                ? $"{dependency.Source}:version:{dependency.VersionId}"
                : "";

    private async Task<string> ResolveLoaderVersionAsync(string loader, string minecraftVersion, string requested, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (loader == "forge" && !requested.StartsWith(minecraftVersion + "-", StringComparison.OrdinalIgnoreCase))
                return $"{minecraftVersion}-{requested}";
            if (loader == "neoforge" && minecraftVersion == "1.20.1"
                && !requested.StartsWith(minecraftVersion + "-", StringComparison.OrdinalIgnoreCase))
                return $"{minecraftVersion}-{requested}";
            return requested;
        }
        var versions = loader switch
        {
            "fabric" => await _loaders.FabricVersionsAsync(minecraftVersion),
            "forge" => await _loaders.ForgeVersionsAsync(minecraftVersion),
            "neoforge" => await _loaders.NeoForgeVersionsAsync(minecraftVersion),
            "quilt" => await _loaders.QuiltVersionsAsync(minecraftVersion),
            _ => new List<string>()
        };
        ct.ThrowIfCancellationRequested();
        return versions.FirstOrDefault() ?? throw new IOException($"没有找到 Minecraft {minecraftVersion} 的 {loader} 版本");
    }

    private async Task<string> InstallLoaderAsync(string loader, string minecraftVersion, string loaderVersion, string? javaPath)
        => loader switch
        {
            "fabric" => await _loaders.InstallFabricAsync(minecraftVersion, loaderVersion),
            "forge" => await _loaders.InstallForgeAsync(loaderVersion, javaPath),
            "neoforge" => await _loaders.InstallNeoForgeAsync(loaderVersion, javaPath),
            "quilt" => await _loaders.InstallQuiltAsync(minecraftVersion, loaderVersion),
            _ => minecraftVersion
        };

    private static async Task<JavaInfo> EnsureJavaAsync(
        string minecraftVersion,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var java = await Task.Run(() => JavaService.FindForVersion(minecraftVersion), cancellationToken);
        if (java != null) return java;
        var required = JavaService.RequiredMajorVersion(minecraftVersion);
        progress?.Report($"MCP · 正在安装 Forge 所需的 Java {required}...");
        var option = await JavaService.GetDownloadOptionAsync(required, cancellationToken);
        if (option == null)
            throw new IOException($"无法获取 Java {required} 的下载信息");
        var item = JavaService.CreateDownloadItem(option);
        JavaInfo? installed = null;
        var task = DownloadManager.Instance.Enqueue(
            $"AI 安装 Java {required}",
            [item],
            category: "java",
            postDownloadAction: async () => installed = await JavaService.InstallDownloadedAsync(option));
        await WaitForDownloadTaskAsync(task, $"Java {required}", progress, cancellationToken);
        return installed ?? throw new IOException($"Java {required} 安装未完成");
    }

    private static async Task WaitForDownloadTaskAsync(
        DownloadTask task,
        string stage,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        void Report()
        {
            var progressText = task.TotalFiles > 0
                ? $" · {task.CompletedFiles}/{task.TotalFiles} 个文件 · {task.Progress:F0}%"
                : "";
            var speed = string.IsNullOrWhiteSpace(task.Speed) ? "" : $" · {task.Speed}";
            progress?.Report($"MCP · {stage}：{task.StatusText}{progressText}{speed}");
        }

        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName is nameof(DownloadTask.Status)
                or nameof(DownloadTask.StatusText)
                or nameof(DownloadTask.Progress)
                or nameof(DownloadTask.CompletedFiles)
                or nameof(DownloadTask.Speed))
                Report();
        };

        task.PropertyChanged += handler;
        try
        {
            Report();
            if (task.Tcs != null)
                await task.Tcs.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            task.PropertyChanged -= handler;
        }

        if (task.Status == DownloadTaskStatus.Completed)
        {
            progress?.Report($"MCP · {stage}下载完成。");
            return;
        }

        var reason = string.IsNullOrWhiteSpace(task.Error) ? task.StatusText : task.Error;
        throw new IOException($"{stage}下载失败: {reason}");
    }

    private static List<string> ExtractMods(string input)
    {
        var matches = ModRegex.Matches(input).Cast<Match>()
            .Concat(EnglishModRegex.Matches(input).Cast<Match>())
            .Concat(LooseModRegex.Matches(input).Cast<Match>())
            .Concat(ChineseModRegex.Matches(input).Cast<Match>())
            .ToList();
        if (matches.Count == 0) return new();
        return matches.Select(match => match.Groups["mods"].Value)
            .SelectMany(raw => raw.Split(['、', ',', '，'], StringSplitOptions.RemoveEmptyEntries))
            .SelectMany(value => Regex.Split(value.Trim(), "\\s*(?:和|and)\\s*", RegexOptions.IgnoreCase))
            .Select(value => value.Trim().Trim('+'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ExtractLoaderVersion(string input, string loader)
    {
        var match = Regex.Match(input,
            $"{loader}\\s*(?:版本)?\\s*[:：]?\\s*([0-9][0-9A-Za-z.+-]*)",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string CreateName(MinecraftInstallPlan plan)
    {
        var loader = plan.Loader == "vanilla" ? "原版" : char.ToUpperInvariant(plan.Loader[0]) + plan.Loader[1..];
        var suffix = plan.Mods.Count == 0 ? "" : " · " + string.Join("+", plan.Mods.Take(3));
        return $"AI · {plan.MinecraftVersion} · {loader}{suffix}";
    }

    private static string CreateId(string version, string loader, IReadOnlyList<string> mods)
    {
        var slug = Regex.Replace($"ai-{version}-{loader}-{string.Join("-", mods)}".ToLowerInvariant(), "[^a-z0-9-]+", "-");
        slug = Regex.Replace(slug, "-+", "-").Trim('-');
        return (slug.Length > 64 ? slug[..64].TrimEnd('-') : slug) + "-" + Guid.NewGuid().ToString("N")[..6];
    }
}
