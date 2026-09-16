using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class SavesPage : Page
{
    private readonly ContentService _content = new();
    private string _instanceRoot = "";

    public SavesPage() : this(null)
    {
    }

    public SavesPage(string? instanceId)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(instanceId))
            _instanceRoot = InstancePathService.GetGameDirectory(App.Paths, App.Settings.Data, instanceId);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RefreshSaves();

    private void RefreshSaves()
    {
        SaveList.Items.Clear();
        if (string.IsNullOrEmpty(_instanceRoot))
        {
            HintText.Text = "未选择游戏版本，请从首页选择后进入版本设置";
            return;
        }
        var saves = _content.Saves(_instanceRoot);
        HintText.Text = $"{saves.Count} 个存档";
        foreach (var save in saves)
        {
            var name = Path.GetFileName(save);
            var time = Directory.GetCreationTime(save).ToString("yyyy-MM-dd HH:mm");
            var item = new ListBoxItem
            {
                Content = $"{name}  ({time})",
                Tag = save,
                Padding = new Thickness(4, 3, 4, 3),
                Foreground = (Brush)FindResource("TextBrush"),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            item.MouseDoubleClick += Save_DoubleClick;
            SaveList.Items.Add(item);
        }
    }

    private void Save_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.Tag is not string savePath) return;
        var result = QuartzLauncher.Services.AnimatedMessageBox.Show($"删除存档 \"{Path.GetFileName(savePath)}\"?", "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _content.DeleteSave(savePath);
                RefreshSaves();
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
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                _content.ImportSave(_instanceRoot, dialog.FileName);
                RefreshSaves();
            }
            catch (Exception ex)
            {
                QuartzLauncher.Services.AnimatedMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshSaves();
}
