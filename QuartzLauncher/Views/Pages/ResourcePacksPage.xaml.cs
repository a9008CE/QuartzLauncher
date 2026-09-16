using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class ResourcePacksPage : Page
{
    private readonly ContentService _content = new();
    private string _instanceRoot = "";

    public ResourcePacksPage() : this(null)
    {
    }

    public ResourcePacksPage(string? instanceId)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(instanceId))
            _instanceRoot = InstancePathService.GetGameDirectory(App.Paths, App.Settings.Data, instanceId);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RefreshPacks();

    private void RefreshPacks()
    {
        PackList.Items.Clear();
        if (string.IsNullOrEmpty(_instanceRoot))
        {
            HintText.Text = "未选择游戏版本，请从首页选择后进入版本设置";
            return;
        }
        var packs = _content.ResourcePacks(_instanceRoot);
        HintText.Text = $"{packs.Count} 个材质包";
        foreach (var path in packs)
        {
            var enabled = !path.EndsWith(".disabled");
            var name = Path.GetFileNameWithoutExtension(path).Replace(".disabled", "");
            var size = new FileInfo(path).Length;
            var sizeText = size < 1024 * 1024 ? $"{size / 1024:F0} KB" : $"{size / (1024.0 * 1024):F1} MB";

            var panel = new DockPanel { Tag = path };
            var sizeLabel = new TextBlock
            {
                Text = sizeText,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            DockPanel.SetDock(sizeLabel, Dock.Right);

            var statusIcon = new TextBlock
            {
                Text = enabled ? "\u2713" : "\u2717",
                FontSize = 12,
                Foreground = enabled ? Brushes.LimeGreen : Brushes.OrangeRed,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var nameLabel = new TextBlock
            {
                Text = name,
                FontSize = 13,
                Foreground = (Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            panel.Children.Add(sizeLabel);
            panel.Children.Add(statusIcon);
            panel.Children.Add(nameLabel);

            var listItem = new ListBoxItem
            {
                Content = panel,
                Tag = path,
                Padding = new Thickness(4, 3, 4, 3),
                Foreground = (Brush)FindResource("TextBrush"),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            listItem.MouseDoubleClick += Pack_DoubleClick;
            PackList.Items.Add(listItem);
        }
    }

    private void Pack_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.Tag is not string path) return;
        var name = Path.GetFileNameWithoutExtension(path).Replace(".disabled", "");
        var enabled = !path.EndsWith(".disabled");
        var action = enabled ? "禁用" : "启用";
        var result = QuartzLauncher.Services.AnimatedMessageBox.Show($"是否{action}材质包 \"{name}\"?", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _content.ToggleResourcePack(path);
                RefreshPacks();
            }
            catch (Exception ex)
            {
                QuartzLauncher.Services.AnimatedMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_instanceRoot)) { QuartzLauncher.Services.AnimatedMessageBox.Show("请先从首页选择游戏版本"); return; }
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "ZIP archives (*.zip)|*.zip|All files (*.*)|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                foreach (var file in dialog.FileNames)
                    _content.ImportResourcePack(_instanceRoot, file);
                RefreshPacks();
            }
            catch (Exception ex)
            {
                QuartzLauncher.Services.AnimatedMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Page_Drop(object sender, DragEventArgs e)
    {
        if (string.IsNullOrEmpty(_instanceRoot)) { QuartzLauncher.Services.AnimatedMessageBox.Show("请先从首页选择游戏版本"); return; }
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            try
            {
                foreach (var file in files)
                    if (Path.GetExtension(file).ToLower() == ".zip")
                        _content.ImportResourcePack(_instanceRoot, file);
                RefreshPacks();
            }
            catch (Exception ex)
            {
                QuartzLauncher.Services.AnimatedMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshPacks();
}
