using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class LoaderDetailPage : Page
{
    private readonly string _loaderType;
    private readonly string _mcVersion;
    private readonly LoaderService _loader;
    private string? _selectedVersion;

    private static readonly Dictionary<string, (string Title, string Desc, string Info)> LoaderInfo = new()
    {
        ["fabric"] = ("Fabric", "高性能、轻量级的 Mod 加载器，社区活跃，更新速度快。", "Fabric Loader 是一个轻量级、体积小巧的 Mod 加载器。它通过 mixin 注入机制实现 Mod 加载，启动速度极快，兼容性优秀。Fabric API 提供了额外的 Mod 开发接口，包括事件系统、网络通信、物品注册等，许多 Fabric Mod 都依赖此 API，建议一并安装。"),
        ["forge"] = ("Forge", "最经典、最成熟的 Mod 加载器，兼容性最强。", "Minecraft Forge 是历史最悠久的 Mod 加载器，拥有最庞大的 Mod 生态。几乎所有经典 Mod 都基于 Forge 开发。安装过程需要运行 Installer。"),
        ["neoforge"] = ("NeoForge", "Forge 的社区分支，持续维护更新。", "NeoForge 是从 Minecraft Forge 分叉出来的社区维护版本，保持对新版本 Minecraft 的快速适配。API 与 Forge 高度兼容。"),
        ["quilt"] = ("Quilt", "面向现代 Minecraft 的开放式 Mod 加载器。", "Quilt Loader 兼容大量 Fabric 生态内容，并提供独立的加载器元数据和启动配置。"),
        ["fabricapi"] = ("Fabric API", "Fabric Mod 的核心依赖库，推荐 Fabric 用户安装。", "Fabric API 提供了大量基础接口供 Mod 使用，包括事件系统、网络通信、物品注册等。许多 Fabric Mod 都依赖此 API。安装后自动放入 mods 文件夹。"),
        ["optifine"] = ("OptiFine", "性能优化与光影支持，提升游戏画面和帧率。", "OptiFine 是经典的 Minecraft 优化 Mod，支持高清纹理、光影 Shader、动态模糊等。可独立使用，也可与部分加载器共存。"),
        ["liteloader"] = ("LiteLoader", "轻量级辅助 Mod 加载器，适合小工具类 Mod。", "LiteLoader 是一个轻量级的 Mod 加载器，主要用于加载小工具类 Mod（如小地图、HUD 增强等）。体积小，启动快。")
    };

    public LoaderDetailPage(string loaderType, string mcVersion, LoaderService loader)
    {
        _loaderType = loaderType;
        _mcVersion = mcVersion;
        _loader = loader;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (LoaderInfo.TryGetValue(_loaderType, out var info))
        {
            TitleText.Text = info.Title;
            DescText.Text = info.Desc;
            InfoText.Text = info.Info;
        }

        if (_loaderType == "fabric")
        {
            FabricApiPanel.Visibility = Visibility.Visible;
            LoadFabricApiVersion();
        }
        else
        {
            FabricApiPanel.Visibility = Visibility.Collapsed;
        }

        if (_loaderType is "fabric" or "forge" or "neoforge" or "quilt")
            LoadLoaderVersions();
        else if (_loaderType == "optifine")
            LoadOptifineVersions();
        else if (_loaderType == "liteloader")
            LoadLiteLoaderVersions();
        else if (_loaderType == "fabricapi")
            LoadFabricApiVersions();
        else
            VersionList.Visibility = Visibility.Collapsed;
    }

    private async void LoadFabricApiVersion()
    {
        try
        {
            var apiVersion = await _loader.GetFabricApiVersionAsync(_mcVersion);
            if (!string.IsNullOrEmpty(apiVersion))
                FabricApiVersionText.Text = $"最新: {apiVersion}";
            else
                FabricApiVersionText.Text = $"暂无 {_mcVersion} 版本";
        }
        catch
        {
            FabricApiVersionText.Text = "获取失败";
        }
    }

    private async void LoadLoaderVersions()
    {
        StatusText.Text = "正在获取版本列表...";
        try
        {
            var versions = _loaderType switch
            {
                "fabric" => await _loader.FabricVersionsAsync(_mcVersion),
                "neoforge" => await _loader.NeoForgeVersionsAsync(_mcVersion),
                "quilt" => await _loader.QuiltVersionsAsync(_mcVersion),
                _ => await _loader.ForgeVersionsAsync(_mcVersion)
            };

            VersionList.Items.Clear();
            foreach (var v in versions)
            {
                var item = new ListBoxItem
                {
                    Content = v, Tag = v,
                    Padding = new Thickness(6, 4, 6, 4),
                    Foreground = (Brush)FindResource("TextBrush"),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                VersionList.Items.Add(item);
            }
            ConfirmBtn.IsEnabled = versions.Count > 0;
            StatusText.Text = versions.Count > 0
                ? $"共 {versions.Count} 个版本可用"
                : $"{LoaderInfo[_loaderType].Title} 不支持 Minecraft {_mcVersion}";
            if (versions.Count == 0 && _loaderType == "neoforge")
            {
                AnimatedMessageBox.ShowTimed(
                    $"NeoForge 不支持 Minecraft {_mcVersion}",
                    "版本不受支持",
                    TimeSpan.FromSeconds(3),
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"获取失败: {ex.Message}";
        }
    }

    private async void LoadLiteLoaderVersions()
    {
        StatusText.Text = "正在获取 LiteLoader 版本列表...";
        try
        {
            var versions = await _loader.LiteLoaderVersionsAsync(_mcVersion);
            VersionList.Items.Clear();
            foreach (var (displayName, version) in versions)
            {
                var item = new ListBoxItem
                {
                    Content = displayName, Tag = version,
                    Padding = new Thickness(6, 4, 6, 4),
                    Foreground = (Brush)FindResource("TextBrush"),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                VersionList.Items.Add(item);
            }
            ConfirmBtn.IsEnabled = versions.Count > 0;
            StatusText.Text = versions.Count > 0
                ? $"共 {versions.Count} 个版本可用"
                : $"LiteLoader 不支持 Minecraft {_mcVersion}";
            if (versions.Count == 0)
            {
                AnimatedMessageBox.ShowTimed(
                    $"LiteLoader 不支持 Minecraft {_mcVersion}",
                    "版本不受支持",
                    TimeSpan.FromSeconds(3),
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"获取失败: {ex.Message}";
        }
    }

    private async void LoadFabricApiVersions()
    {
        StatusText.Text = "正在获取 Fabric API 版本...";
        try
        {
            var apiVersion = await _loader.GetFabricApiVersionAsync(_mcVersion);
            VersionList.Items.Clear();
            if (!string.IsNullOrEmpty(apiVersion))
            {
                var item = new ListBoxItem
                {
                    Content = apiVersion, Tag = apiVersion,
                    Padding = new Thickness(6, 4, 6, 4),
                    Foreground = (Brush)FindResource("TextBrush"),
                    IsSelected = true
                };
                VersionList.Items.Add(item);
                _selectedVersion = apiVersion;
                StatusText.Text = $"最新版本: {apiVersion}";
            }
            else
            {
                StatusText.Text = $"Fabric API 不支持 {_mcVersion}";
                ConfirmBtn.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"获取失败: {ex.Message}";
        }
    }

    private async void LoadOptifineVersions()
    {
        StatusText.Text = "正在获取 OptiFine 版本列表...";
        try
        {
            var versions = await _loader.OptifineVersionsAsync(_mcVersion);
            VersionList.Items.Clear();
            foreach (var (displayName, fileName) in versions)
            {
                var item = new ListBoxItem
                {
                    Content = displayName, Tag = fileName,
                    Padding = new Thickness(6, 4, 6, 4),
                    Foreground = (Brush)FindResource("TextBrush"),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                VersionList.Items.Add(item);
            }
            ConfirmBtn.IsEnabled = versions.Count > 0;
            StatusText.Text = versions.Count > 0
                ? $"共 {versions.Count} 个版本可用"
                : $"OptiFine 不支持 Minecraft {_mcVersion}";
            if (versions.Count == 0)
            {
                AnimatedMessageBox.ShowTimed(
                    $"OptiFine 不支持 Minecraft {_mcVersion}",
                    "版本不受支持",
                    TimeSpan.FromSeconds(3),
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"获取失败: {ex.Message}";
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (VersionList.Visibility == Visibility.Visible && VersionList.SelectedItem is ListBoxItem item)
            _selectedVersion = item.Tag?.ToString();

        if (_loaderType is "fabric" or "forge" or "neoforge" or "quilt" or "optifine" or "liteloader")
        {
            foreach (var type in new[] { "fabric", "forge", "neoforge", "quilt", "optifine", "liteloader" })
                LoaderPickerPage.SelectedLoaders.Remove(type);
            if (_loaderType != "fabric")
                LoaderPickerPage.SelectedLoaders.Remove("fabricapi");
        }

        LoaderPickerPage.SelectedLoaders[_loaderType] = new LoaderPickerPage.LoaderSelection
        {
            Type = _loaderType,
            MinecraftVersion = _mcVersion,
            Version = _selectedVersion ?? "",
            AutoInstall = App.Settings.Data.AutoInstallLoader
        };

        if (_loaderType == "fabric" && FabricApiPanel.Visibility == Visibility.Visible && FabricApiCheck.IsChecked == true)
        {
            LoaderPickerPage.SelectedLoaders["fabricapi"] = new LoaderPickerPage.LoaderSelection
            {
                Type = "fabricapi",
                MinecraftVersion = _mcVersion,
                Version = "",
                AutoInstall = true
            };
        }
        else if (_loaderType == "fabric")
        {
            LoaderPickerPage.SelectedLoaders.Remove("fabricapi");
        }

        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var versionsPage = mainWindow.GetVersionsPage();
            versionsPage.RestoreVersionSelection(_mcVersion);
            mainWindow.NavigateTo(versionsPage);
        }
    }
}
