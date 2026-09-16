using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ookii.Dialogs.Wpf;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class LocalVersionsPage : Page, IStandaloneSidebarPage
{
    private readonly ObservableCollection<LocalVersionEntry> _versions = new();
    public LocalVersionsPage()
    {
        InitializeComponent();
        VersionList.ItemsSource = _versions;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshVersions();
        VersionListBar.BeginAnimation(OpacityProperty, null);
        DetailFrame.BeginAnimation(OpacityProperty, null);
        VersionListBar.Opacity = 1;
        DetailFrame.Opacity = 1;
        if (App.Settings.Data.PageAnimationStyle is not ("none" or "tear"))
            AnimateIn();
    }

    private void RefreshVersions(string? selectedId = null)
    {
        _versions.Clear();
        var instances = new InstanceStore(App.Paths.InstancesDir).List();
        foreach (var instance in instances)
            _versions.Add(new LocalVersionEntry(instance));

        var represented = instances
            .SelectMany(instance => new[] { instance.VersionId, instance.McVersion })
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(App.Paths.VersionsDir))
        {
            foreach (var directory in Directory.EnumerateDirectories(App.Paths.VersionsDir))
            {
                var versionId = Path.GetFileName(directory);
                if (string.IsNullOrWhiteSpace(versionId) || represented.Contains(versionId)) continue;
                if (!File.Exists(Path.Combine(directory, versionId + ".json"))) continue;
                _versions.Add(new LocalVersionEntry(new Instance
                {
                    Id = "local-" + versionId,
                    Name = "原版",
                    VersionId = versionId,
                    McVersion = versionId,
                    Loader = "vanilla",
                    VersionIsolation = true,
                    UsesVersionDirectory = true
                }));
            }
        }

        var ordered = _versions.OrderByDescending(entry => entry.CreatedAt).ToList();
        _versions.Clear();
        foreach (var entry in ordered) _versions.Add(entry);
        EmptyHint.Visibility = _versions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        VersionList.Visibility = _versions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_versions.Count > 0)
            VersionList.SelectedItem = _versions.FirstOrDefault(entry => entry.Instance.Id == selectedId)
                                       ?? _versions[0];
    }

    private void VersionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VersionList.SelectedItem is not LocalVersionEntry entry) return;
        var detail = new InstanceDetailPage(entry.Instance);
        detail.InstanceVisualChanged += (_, _) => RefreshVersions(entry.Instance.Id);
        detail.InstanceDeleted += (_, _) =>
        {
            RefreshVersions();
            if (_versions.Count == 0) DetailFrame.Content = null;
        };
        DetailFrame.Navigate(detail);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateTo(mainWindow.HomePage);
    }

    private void InstallVersion_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateTo(mainWindow.GetVersionsPage());
    }

    private void AddExistingFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "选择已有的 Minecraft 游戏文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        AddExistingFolder(dialog.SelectedPath);
    }

    private void AddExistingFolder(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
        {
            AnimatedMessageBox.Show("所选路径不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var gameDir = DetectGameDirectory(selectedPath);
        if (gameDir == null)
        {
            AnimatedMessageBox.Show(
                "未在所选文件夹中检测到 Minecraft 游戏文件。\n\n请确保选择的是 .minecraft 文件夹或包含 versions 子目录的文件夹。",
                "无法识别", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var versionsDir = Path.Combine(gameDir, "versions");
        if (!Directory.Exists(versionsDir) || !Directory.EnumerateDirectories(versionsDir).Any())
        {
            AnimatedMessageBox.Show(
                "所选文件夹中没有已安装的 Minecraft 版本。",
                "无可用版本", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var versionEntries = Directory.EnumerateDirectories(versionsDir)
            .Where(dir =>
            {
                var name = Path.GetFileName(dir);
                return File.Exists(Path.Combine(dir, name + ".json"));
            })
            .Select(dir => Path.GetFileName(dir)!)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (versionEntries.Count == 0)
        {
            AnimatedMessageBox.Show(
                "versions 目录存在但未找到完整的游戏版本（需要存在 .json 文件）。",
                "无可用版本", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var detectedLoader = DetectLoaderFromDirectory(gameDir, versionEntries);
        var detectedMcVersion = DetectMcVersion(versionEntries);

        var folderName = Path.GetFileName(selectedPath);
        var instanceName = string.IsNullOrWhiteSpace(folderName) ? "导入的游戏" : folderName;

        var store = new InstanceStore(App.Paths.InstancesDir);

        // 防止重复添加同一文件夹
        var allExisting = store.List();
        var duplicate = allExisting.FirstOrDefault(i =>
            !string.IsNullOrWhiteSpace(i.CustomGameDir) &&
            string.Equals(i.CustomGameDir.TrimEnd('\\', '/'), gameDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
        {
            AnimatedMessageBox.Show(
                $"该文件夹已添加过（「{duplicate.Name}」）。\n\n路径: {gameDir}",
                "重复添加", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existingNames = allExisting.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (existingNames.Contains(instanceName))
        {
            for (var suffix = 2; ; suffix++)
            {
                var candidate = instanceName + suffix;
                if (!existingNames.Contains(candidate))
                {
                    instanceName = candidate;
                    break;
                }
            }
        }

        var instanceId = new string(instanceName.Trim().Select(c =>
                char.IsLetterOrDigit(c) || c is '-' or '_' ? char.ToLowerInvariant(c) : '-')
            .ToArray());
        while (instanceId.Contains("--")) instanceId = instanceId.Replace("--", "-");
        instanceId = instanceId.Trim('-', '_');
        if (string.IsNullOrWhiteSpace(instanceId)) instanceId = "imported";
        if (instanceId.Length > 64) instanceId = instanceId[..64].TrimEnd('-', '_');
        instanceId += "-" + Guid.NewGuid().ToString("N")[..6];

        var instance = new Instance
        {
            Id = instanceId,
            Name = instanceName,
            VersionId = detectedMcVersion,
            McVersion = detectedMcVersion,
            Loader = detectedLoader.loader,
            LoaderVersion = detectedLoader.version,
            VersionIsolation = null,
            UsesVersionDirectory = null,
            CustomGameDir = gameDir
        };

        store.Create(instance);
        RefreshVersions(instanceId);

        AnimatedMessageBox.Show(
            $"已添加「{instanceName}」\n\n版本: {detectedMcVersion}\n加载器: {(string.IsNullOrEmpty(detectedLoader.loader) || detectedLoader.loader == "vanilla" ? "原版" : detectedLoader.loader)}\n路径: {gameDir}",
            "添加成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void FolderDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        e.Effects = paths.Length == 1 && Directory.Exists(paths[0])
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void FolderDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (paths.Length == 1 && Directory.Exists(paths[0]))
            AddExistingFolder(paths[0]);
        e.Handled = true;
    }

    private static string DetectMcVersion(List<string> versionEntries)
    {
        // 优先选纯版本号（如 1.20.1、1.21.4），取最新
        var pureVersions = versionEntries
            .Where(v => System.Text.RegularExpressions.Regex.IsMatch(v, @"^1\.\d+(\.\d+)?$"))
            .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (pureVersions.Count > 0)
            return pureVersions[0];

        // 次选含加载器后缀的版本号（如 1.20.1-fabric-0.15.3）
        var loaderVersions = versionEntries
            .Where(v => v.Contains('-'))
            .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (loaderVersions.Count > 0)
        {
            // 从 "1.20.1-fabric-0.15.3" 提取 "1.20.1"
            var match = System.Text.RegularExpressions.Regex.Match(loaderVersions[0], @"^(1\.\d+(\.\d+)?)");
            if (match.Success)
                return match.Groups[1].Value;
        }

        // 兜底：第一个版本
        return versionEntries[0];
    }

    private static string? DetectGameDirectory(string selectedPath)
    {
        if (Directory.Exists(Path.Combine(selectedPath, "versions")))
            return selectedPath;

        var parent = Directory.GetParent(selectedPath);
        if (parent != null && Directory.Exists(Path.Combine(parent.FullName, "versions")))
            return parent.FullName;

        return null;
    }

    private static (string loader, string version) DetectLoaderFromDirectory(string gameDir, List<string> versionEntries)
    {
        var versionsDir = Path.Combine(gameDir, "versions");

        // 扫描所有版本文件夹，找加载器线索
        foreach (var versionId in versionEntries.OrderByDescending(v => v))
        {
            var jsonPath = Path.Combine(versionsDir, versionId, versionId + ".json");
            if (!File.Exists(jsonPath)) continue;
            try
            {
                var json = File.ReadAllText(jsonPath);
                var result = DetectLoaderFromJson(json);
                if (result.loader != "vanilla")
                    return result;
            }
            catch { }
        }
        return ("vanilla", "");
    }

    private static (string loader, string version) DetectLoaderFromJson(string json)
    {
        // === 在完整 JSON 字符串中搜索库名 ===

        // Fabric / Quilt: 搜索 "net.fabricmc:fabric-loader" 或 "org.quiltmc:quilt-loader"
        if (json.Contains("net.fabricmc:fabric-loader") || json.Contains("org.quiltmc:quilt-loader"))
        {
            var version = RegexVersion(json, @"net\.fabricmc:fabric-loader:([0-9\.]+(\+build\.[0-9]+)?)")
                      ?? RegexVersion(json, @"org\.quiltmc:quilt-loader:([0-9\.]+)")
                      ?? "";
            version = version.Replace("+build", "");
            return ("fabric", version);
        }

        // Forge: 搜索 "minecraftforge"（排除 NeoForge）
        if (json.Contains("minecraftforge") && !json.Contains("net.neoforge"))
        {
            var version = RegexVersion(json, @"forge:[0-9\.]+(_pre[0-9]*)?-([0-9\.]+)")
                      ?? RegexVersion(json, @"net\.minecraftforge:minecraftforge:([0-9\.]+)")
                      ?? RegexVersion(json, @"net\.minecraftforge:fmlloader:[0-9\.]+-([0-9\.]+)")
                      ?? "";
            return ("forge", version);
        }

        // NeoForge: 搜索 "net.neoforge"
        if (json.Contains("net.neoforge"))
        {
            // 从 arguments 中提取：--fml.neoForgeVersion", "20.6.119-beta
            var version = RegexVersion(json, @"forgeVersion"",""([^""]+)""") ?? "";
            return ("neoforge", version);
        }

        // OptiFine: 搜索 "optifine"
        if (json.Contains("optifine", StringComparison.OrdinalIgnoreCase))
        {
            var version = RegexVersion(json, @"HD_U_([^""/:]+)") ?? "";
            return ("optifine", version);
        }

        // LiteLoader: 搜索 "liteloader"
        if (json.Contains("liteloader", StringComparison.OrdinalIgnoreCase))
            return ("liteloader", "");

        return ("vanilla", "");
    }

    private static string? RegexVersion(string input, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private void AnimateIn()
    {
        var style = App.Settings.Data.PageAnimationStyle;
        var quick = style == "quick";
        var duration = quick ? 180 : 450;
        var barTransform = new TranslateTransform(-260, 0);
        var detailTransform = new TranslateTransform(350, 0);
        VersionListBar.RenderTransform = barTransform;
        DetailFrame.RenderTransform = detailTransform;
        barTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(quick ? -100 : -260, 0, TimeSpan.FromMilliseconds(duration))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
        detailTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(quick ? 140 : 350, 0, TimeSpan.FromMilliseconds(duration))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
    }

    public async Task AnimateStandaloneExitAsync(bool quick)
    {
        var duration = quick ? 180 : 450;
        var barTransform = new TranslateTransform();
        var detailTransform = new TranslateTransform();
        VersionListBar.RenderTransform = barTransform;
        DetailFrame.RenderTransform = detailTransform;
        barTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, quick ? -100 : -260, TimeSpan.FromMilliseconds(duration))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn } });
        detailTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, quick ? 140 : 350, TimeSpan.FromMilliseconds(duration))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn } });
        await Task.Delay(duration);
    }

    private sealed class LocalVersionEntry
    {
        public LocalVersionEntry(Instance instance)
        {
            Instance = instance;
            Name = string.IsNullOrWhiteSpace(instance.Name) ? instance.McVersion : instance.Name;
            var root = InstancePathService.GetGameDirectory(App.Paths, App.Settings.Data, instance);
            CreatedAt = Directory.Exists(root)
                ? Directory.GetCreationTime(root)
                : File.GetCreationTime(Path.Combine(App.Paths.VersionsDir, instance.VersionId, instance.VersionId + ".json"));
            Icon = InstanceIconService.Load(instance);
            var minecraft = string.IsNullOrWhiteSpace(instance.McVersion) ? instance.VersionId : instance.McVersion;
            var loader = string.IsNullOrWhiteSpace(instance.Loader) ? "vanilla" : instance.Loader;
            VersionLabel = loader.Equals("vanilla", StringComparison.OrdinalIgnoreCase)
                ? minecraft
                : $"{minecraft} · {char.ToUpperInvariant(loader[0]) + loader[1..]}";
            CreatedLabel = !string.IsNullOrWhiteSpace(instance.CustomGameDir)
                ? $"外部导入 · {instance.CustomGameDir}"
                : $"创建于 {CreatedAt:yyyy-MM-dd HH:mm}";
        }

        public Instance Instance { get; }
        public string Name { get; }
        public string VersionLabel { get; }
        public string CreatedLabel { get; }
        public DateTime CreatedAt { get; }
        public ImageSource Icon { get; }
    }
}
