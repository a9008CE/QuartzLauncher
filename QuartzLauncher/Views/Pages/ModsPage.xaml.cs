using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class ModsPage : Page
{
    private readonly ContentService _content = new();
    private string _instanceRoot = "";
    private List<ModInfo> _allMods = new();
    private string _searchText = "";
    private string _filter = "all";

    public ModsPage() : this(null) { }

    public ModsPage(string? instanceId)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(instanceId))
            _instanceRoot = InstancePathService.GetGameDirectory(App.Paths, App.Settings.Data, instanceId);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RefreshMods();

    private void RefreshMods()
    {
        ModList.Items.Clear();
        if (string.IsNullOrEmpty(_instanceRoot))
        {
            EmptyHint.Text = "未选择游戏版本，请从首页选择后进入版本设置";
            EmptyHint.Visibility = Visibility.Visible;
            return;
        }

        _allMods = _content.Mods(_instanceRoot);

        var enabledCount = _allMods.Count(m => m.Enabled);
        var disabledCount = _allMods.Count(m => !m.Enabled);
        FilterEnabled.Content = $"已启用 ({enabledCount})";
        FilterDisabled.Content = $"已禁用 ({disabledCount})";
        SubtitleText.Text = $"共 {enabledCount + disabledCount} 个 Mod";

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        ModList.Items.Clear();
        var filtered = _filter switch
        {
            "enabled" => _allMods.Where(m => m.Enabled),
            "disabled" => _allMods.Where(m => !m.Enabled),
            _ => _allMods.AsEnumerable()
        };

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var keywords = _searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            filtered = filtered.Where(m =>
                keywords.All(k =>
                    m.Name.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    m.FileName.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    m.Version.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    m.Loader.Contains(k, StringComparison.OrdinalIgnoreCase)));
        }

        var items = filtered.ToList();
        EmptyHint.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ModList.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        foreach (var mod in items)
            ModList.Items.Add(CreateModItem(mod));

        UpdateBulkBar();
    }

    private ListBoxItem CreateModItem(ModInfo mod)
    {
        var loaderTag = string.IsNullOrEmpty(mod.Loader) ? "" : $" [{mod.Loader}]";
        var versionTag = string.IsNullOrEmpty(mod.Version) ? "" : $" {mod.Version}";
        var sizeTag = FormatSize(mod.FileSize);
        var statusIcon = mod.Enabled ? "\u2713" : "\u2717";
        var statusColor = mod.Enabled ? "#4CAF50" : "#F44336";

        var grid = new Grid { Margin = new Thickness(8, 6, 8, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 启用状态指示条
        var indicator = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusColor)),
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 2, 8, 2)
        };
        Grid.SetColumn(indicator, 0);
        grid.Children.Add(indicator);

        // 名称 + 描述
        var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var nameBlock = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        nameBlock.Inlines.Add(new System.Windows.Documents.Run(mod.Name));
        if (!string.IsNullOrEmpty(loaderTag))
            nameBlock.Inlines.Add(new System.Windows.Documents.Run(loaderTag)
            {
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888")),
                FontSize = 11
            });
        if (!string.IsNullOrEmpty(versionTag))
            nameBlock.Inlines.Add(new System.Windows.Documents.Run(versionTag)
            {
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888")),
                FontSize = 11
            });
        if (!mod.Enabled)
            nameBlock.TextDecorations = TextDecorations.Strikethrough;
        infoPanel.Children.Add(nameBlock);

        var descBlock = new TextBlock
        {
            FontSize = 11,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0),
            Text = string.IsNullOrEmpty(mod.Description)
                ? $"{mod.FileName}  ({sizeTag})"
                : $"{mod.Description}  |  {mod.FileName}  ({sizeTag})"
        };
        infoPanel.Children.Add(descBlock);
        Grid.SetColumn(infoPanel, 1);
        grid.Children.Add(infoPanel);

        // 操作按钮
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        var toggleBtn = new Button
        {
            Content = mod.Enabled ? "\u2717" : "\u2713",
            FontSize = 12,
            Width = 26,
            Height = 26,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusColor)),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusColor)),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = mod.Enabled ? "禁用" : "启用",
            Tag = mod
        };
        toggleBtn.PreviewMouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            ToggleMod_Click(toggleBtn, e);
        };
        btnPanel.Children.Add(toggleBtn);

        var deleteBtn = new Button
        {
            Content = "\u2715",
            FontSize = 11,
            Width = 26,
            Height = 26,
            Margin = new Thickness(4, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "删除",
            Tag = mod
        };
        deleteBtn.PreviewMouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            DeleteMod_Click(deleteBtn, e);
        };
        btnPanel.Children.Add(deleteBtn);

        var openBtn = new Button
        {
            Content = "\u2197",
            FontSize = 11,
            Width = 26,
            Height = 26,
            Margin = new Thickness(4, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "打开文件位置",
            Tag = mod
        };
        openBtn.PreviewMouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            OpenFileLocation_Click(openBtn, e);
        };
        btnPanel.Children.Add(openBtn);

        Grid.SetColumn(btnPanel, 2);
        grid.Children.Add(btnPanel);

        return new ListBoxItem
        {
            Content = grid,
            Tag = mod,
            Padding = new Thickness(4, 2, 4, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private void ToggleMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ModInfo mod) return;
        try
        {
            _content.ToggleMod(mod);
            RefreshMods();
        }
        catch (Exception ex)
        {
            AnimatedMessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ModInfo mod) return;
        var result = AnimatedMessageBox.Show(
            $"确定要删除「{mod.Name}」吗？\n\n文件: {mod.FileName}",
            "删除 Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            _content.DeleteMod(mod);
            RefreshMods();
        }
        catch (Exception ex)
        {
            AnimatedMessageBox.Show($"删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ModInfo mod) return;
        try
        {
            var arg = $"/select,\"{mod.Path}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", arg) { UseShellExecute = true });
        }
        catch { }
    }

    // 筛选
    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (FilterAll.IsChecked == true) _filter = "all";
        else if (FilterEnabled.IsChecked == true) _filter = "enabled";
        else if (FilterDisabled.IsChecked == true) _filter = "disabled";
        ApplyFilter();
    }

    // 搜索
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        ApplyFilter();
    }

    // 全选
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        ModList.SelectAll();
        UpdateBulkBar();
    }

    // 批量操作
    private void BulkEnable_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMods().Where(m => !m.Enabled).ToList();
        if (selected.Count == 0) return;
        try
        {
            _content.ToggleMods(selected);
            RefreshMods();
        }
        catch (Exception ex)
        {
            AnimatedMessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BulkDisable_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMods().Where(m => m.Enabled).ToList();
        if (selected.Count == 0) return;
        try
        {
            _content.ToggleMods(selected);
            RefreshMods();
        }
        catch (Exception ex)
        {
            AnimatedMessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BulkDelete_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMods().ToList();
        if (selected.Count == 0) return;
        var result = AnimatedMessageBox.Show(
            $"确定要删除选中的 {selected.Count} 个 Mod 吗？",
            "批量删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            _content.DeleteMods(selected);
            RefreshMods();
        }
        catch (Exception ex)
        {
            AnimatedMessageBox.Show($"删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ModList.UnselectAll();
        UpdateBulkBar();
    }

    private List<ModInfo> GetSelectedMods()
    {
        return ModList.SelectedItems
            .Cast<ListBoxItem>()
            .Where(i => i.Tag is ModInfo)
            .Select(i => (ModInfo)i.Tag!)
            .ToList();
    }

    private void UpdateBulkBar()
    {
        var count = ModList.SelectedItems.Count;
        if (count > 0)
        {
            BulkBar.Visibility = Visibility.Visible;
            BulkInfo.Text = $"已选择 {count} 个文件";
        }
        else
        {
            BulkBar.Visibility = Visibility.Collapsed;
        }
    }

    // 导入
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_instanceRoot))
        {
            AnimatedMessageBox.Show("请先从首页选择游戏版本");
            return;
        }
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Mod 文件 (*.jar;*.zip;*.litemod)|*.jar;*.zip;*.litemod|所有文件 (*.*)|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true)
        {
            _content.AddMods(_instanceRoot, dialog.FileNames.ToList());
            RefreshMods();
        }
    }

    // 打开文件夹
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_instanceRoot)) return;
        var modsDir = Path.Combine(_instanceRoot, "mods");
        Directory.CreateDirectory(modsDir);
        Process.Start(new ProcessStartInfo("explorer.exe", modsDir) { UseShellExecute = true });
    }

    // 拖放
    private void Page_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Page_Drop(object sender, DragEventArgs e)
    {
        if (string.IsNullOrEmpty(_instanceRoot)) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        var modFiles = files.Where(f =>
        {
            var ext = Path.GetExtension(f).ToLower();
            return ext is ".jar" or ".zip" or ".litemod" or ".disabled" or ".old";
        }).ToList();
        if (modFiles.Count > 0)
        {
            _content.AddMods(_instanceRoot, modFiles);
            RefreshMods();
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
    };
}
