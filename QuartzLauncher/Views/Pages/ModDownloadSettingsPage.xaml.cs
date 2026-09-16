using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class ModDownloadSettingsPage : Page
{
    private bool _isLoading;

    private static readonly (string Label, string Value)[] Sources = new[]
    {
        ("Modrinth", "Modrinth"),
        ("CurseForge", "CurseForge"),
        ("MC百科", "MCmod"),
        ("混合 (自定义多选源)", "mixed"),
        ("全部 (同时搜索所有源)", "all"),
    };

    private const string AllSources = "Modrinth,CurseForge,MCmod";

    private static readonly Dictionary<string, (string Title, string Desc)> HelpData = new()
    {
        ["Source"] = ("下载源",
            "选择 Mod 搜索和下载时使用的平台。支持 Modrinth、CurseForge、MC百科三个源，也可以混合多个源同时搜索。"),
        ["ApiKey"] = ("CurseForge API Key",
            "可选填写个人 API Key。留空时使用公开兼容源；填写后优先访问官方 API，失败时仍会自动回退。Key 仅保存在本地，不会上传。"),
        ["Path"] = ("下载目录",
            "设置 Mod 的下载存放位置。默认使用当前实例的 mods 目录；材质包和光影包仍会安装到当前实例的对应目录。"),
        ["Concurrent"] = ("下载并发",
            "同时下载任务数控制同时下载多少个文件。过高可能导致服务器限速或连接失败，建议 4~8。线程数控制每个任务的并行连接，自动模式根据网速智能调整。"),
        ["Loader"] = ("加载器",
            "启用后，在安装新游戏版本时自动检测并安装匹配的 Fabric、Forge 或 Quilt 加载器，省去手动选择的步骤。"),
    };

    public ModDownloadSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void Card_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border card && card.Tag is string key && HelpData.TryGetValue(key, out var help))
        {
            HelpTitle.Text = help.Title;
            HelpDesc.Text = help.Desc;
        }
    }

    private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HelpTitle.Text = "下载源";
        HelpDesc.Text = "选择 Mod 搜索和下载时使用的平台。支持 Modrinth、CurseForge、MC百科三个源，也可以混合多个源同时搜索。";
    }

    private void LoadSettings()
    {
        _isLoading = true;
        try
        {
            var s = App.Settings.Data;

            SourceCombo.Items.Clear();
            var current = s.ModDownloadSource;
            var selectedIndex = 0;

            for (var i = 0; i < Sources.Length; i++)
            {
                SourceCombo.Items.Add(new ComboBoxItem
                {
                    Content = Sources[i].Label,
                    Tag = Sources[i].Value
                });
                if (Sources[i].Value == current)
                    selectedIndex = i;
            }

            SourceCombo.SelectedIndex = selectedIndex;
            UpdateCustomPanelVisibility(current);

            if (current == "mixed")
            {
                var saved = s.ModDownloadSources ?? "";
                var parts = saved.Split(',', StringSplitOptions.RemoveEmptyEntries);
                ChkAll.IsChecked = parts.Length == 3;
                ChkModrinth.IsChecked = parts.Contains("Modrinth");
                ChkCurseForge.IsChecked = parts.Contains("CurseForge");
                ChkMcmod.IsChecked = parts.Contains("MCmod");
            }
            else if (current == "all")
            {
                ChkAll.IsChecked = true;
            }

            UpdateSummary();

            ApiKeyBox.Text = s.CurseForgeApiKey;
            DownloadPathBox.Text = string.IsNullOrEmpty(s.ModDownloadPath)
                ? "(默认: 游戏目录/.minecraft/mods)"
                : s.ModDownloadPath;

            ConcurrentSlider.Value = Math.Clamp(s.ConcurrentDownloads, 1, 16);
            DownloadManager.Instance.ResourceMaxConcurrent = (int)ConcurrentSlider.Value;
            AutoThreadChk.IsChecked = s.AutoDownloadThreads;
            ManualThreadPanel.IsEnabled = !s.AutoDownloadThreads;
            ThreadSlider.Value = Math.Clamp(s.ManualDownloadThreads, 1, 16);
            RetrySlider.Value = Math.Clamp(s.DownloadRetryCount, 0, 10);
            AutoInstallChk.IsChecked = s.AutoInstallLoader;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (SourceCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string value) return;

        if (value == "mixed" || value == "all")
        {
            App.Settings.Data.ModDownloadSource = value;
            if (value == "all")
            {
                App.Settings.Data.ModDownloadSources = AllSources;
                _isLoading = true;
                ChkAll.IsChecked = true;
                ChkModrinth.IsChecked = true;
                ChkCurseForge.IsChecked = true;
                ChkMcmod.IsChecked = true;
                _isLoading = false;
            }
        }
        else
        {
            App.Settings.Data.ModDownloadSource = value;
        }

        UpdateCustomPanelVisibility(value);
        UpdateSummary();
        App.Settings.Save();
    }

    private void ChkAny_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        if (ChkAll.IsChecked == true)
        {
            _isLoading = true;
            ChkModrinth.IsChecked = true;
            ChkCurseForge.IsChecked = true;
            ChkMcmod.IsChecked = true;
            _isLoading = false;
        }
        else if (sender == ChkAll)
        {
            _isLoading = true;
            ChkModrinth.IsChecked = false;
            ChkCurseForge.IsChecked = false;
            ChkMcmod.IsChecked = false;
            _isLoading = false;
        }

        UpdateSummary();
        SaveCustomSources();
    }

    private void SaveCustomSources()
    {
        var sources = new List<string>();
        if (ChkModrinth.IsChecked == true) sources.Add("Modrinth");
        if (ChkCurseForge.IsChecked == true) sources.Add("CurseForge");
        if (ChkMcmod.IsChecked == true) sources.Add("MCmod");

        App.Settings.Data.ModDownloadSource = sources.Count == 3 ? "all" : "mixed";
        App.Settings.Data.ModDownloadSources = string.Join(",", sources);

        if (sources.Count == 3)
        {
            var idx = 0;
            for (var i = 0; i < Sources.Length; i++)
            {
                if (Sources[i].Value == "all") { idx = i; break; }
            }
            _isLoading = true;
            SourceCombo.SelectedIndex = idx;
            _isLoading = false;
        }

        UpdateCustomPanelVisibility(App.Settings.Data.ModDownloadSource);
        App.Settings.Save();
    }

    private void UpdateCustomPanelVisibility(string value)
    {
        CustomPanel.Visibility = value == "mixed"
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateSummary()
    {
        var selected = new List<string>();
        if (ChkModrinth.IsChecked == true) selected.Add("Modrinth");
        if (ChkCurseForge.IsChecked == true) selected.Add("CurseForge");
        if (ChkMcmod.IsChecked == true) selected.Add("MC百科");

        CustomSummary.Text = selected.Count == 0
            ? "未选择任何源"
            : $"已选择: {string.Join(" + ", selected)}";
    }

    private void ApiKeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Data.CurseForgeApiKey = ApiKeyBox.Text.Trim();
        App.Settings.Save();
    }

    private void BrowseDownloadPath_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "选择 Mod 下载目录"
        };
        if (dlg.ShowDialog() == true)
        {
            App.Settings.Data.ModDownloadPath = dlg.FolderName;
            DownloadPathBox.Text = dlg.FolderName;
            App.Settings.Save();
        }
    }

    private void ResetDownloadPath_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.Data.ModDownloadPath = "";
        DownloadPathBox.Text = "(默认: 游戏目录/.minecraft/mods)";
        App.Settings.Save();
    }

    private void ConcurrentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || ConcurrentLabel == null) return;
        ConcurrentLabel.Text = ((int)ConcurrentSlider.Value).ToString();
        App.Settings.Data.ConcurrentDownloads = (int)ConcurrentSlider.Value;
        DownloadManager.Instance.ResourceMaxConcurrent = App.Settings.Data.ConcurrentDownloads;
        App.Settings.Save();
    }

    private void AutoThread_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || ManualThreadPanel == null) return;
        var auto = AutoThreadChk.IsChecked == true;
        ManualThreadPanel.IsEnabled = !auto;
        App.Settings.Data.AutoDownloadThreads = auto;
        App.Settings.Save();
    }

    private void ThreadSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || ThreadLabel == null) return;
        ThreadLabel.Text = ((int)ThreadSlider.Value).ToString();
        App.Settings.Data.ManualDownloadThreads = (int)ThreadSlider.Value;
        App.Settings.Save();
    }

    private void RetrySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || RetryLabel == null) return;
        RetryLabel.Text = ((int)RetrySlider.Value).ToString();
        App.Settings.Data.DownloadRetryCount = (int)RetrySlider.Value;
        App.Settings.Save();
    }

    private void AutoInstall_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Data.AutoInstallLoader = AutoInstallChk.IsChecked == true;
        App.Settings.Save();
    }
}
