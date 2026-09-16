using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class ExportVersionPage : Page
{
    private readonly string _instanceId;
    private readonly string _instanceName;

    private static readonly Dictionary<string, (string Title, string Desc)> HelpData = new()
    {
        ["content"] = ("导出内容",
            "选择要写入 QuartzPack 的实例内容。Minecraft 客户端、运行库、账户凭据和全局启动器配置不会被打包；版本与加载器信息写入清单，导入时按需下载。"),
        ["export"] = ("导出",
            "点击「导出 QuartzPack」后选择保存位置。每个文件会写入大小与 SHA-1 校验信息，导入时会复核内容完整性。"),
        ["io"] = ("读取 / 写入",
            "「读取」从磁盘重新加载 instance.json，显示该实例的名称、版本和加载器信息；「写入」把当前实例数据强制写回 instance.json，适合配置文件被改乱时一键恢复数据来源。"),
    };

    private const string DefaultHelp =
        "将鼠标移到左侧的设置卡片上，可查看对应的使用说明。";

    public ExportVersionPage(string instanceId, string instanceName)
    {
        _instanceId = instanceId;
        _instanceName = instanceName;
        InitializeComponent();
    }

    private void SetIoStatus(string text)
    {
        IoStatus.Text = text;
        IoStatus.Visibility = Visibility.Visible;
    }

    private void Card_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border card && card.Tag is string key && HelpData.TryGetValue(key, out var help))
            HelpDesc.Text = help.Desc;
    }

    private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HelpDesc.Text = DefaultHelp;
    }

    private void LoadData_Click(object sender, RoutedEventArgs e)
    {
        var file = Path.Combine(App.Paths.InstancesDir, _instanceId, "instance.json");
        if (!File.Exists(file))
        {
            SetIoStatus("未找到 instance.json，实例数据不存在。");
            return;
        }

        try
        {
            var instance = JsonConvert.DeserializeObject<Instance>(File.ReadAllText(file));
            if (instance == null)
            {
                SetIoStatus("读取失败：数据为空。");
                return;
            }
            SetIoStatus($"读取完成：{instance.Name} · Minecraft {instance.McVersion} · 加载器 {instance.Loader}");
        }
        catch (Exception ex)
        {
            SetIoStatus($"读取失败：{ex.Message}");
        }
    }

    private void WriteData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var store = new InstanceStore(App.Paths.InstancesDir);
            var instance = store.List().FirstOrDefault(i => i.Id == _instanceId);
            if (instance == null)
            {
                SetIoStatus("未找到实例数据，无法写入。");
                return;
            }
            store.Create(instance);
            SetIoStatus($"已写入：{instance.Name}（instance.json 已更新）");
        }
        catch (Exception ex)
        {
            SetIoStatus($"写入失败：{ex.Message}");
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_instanceId))
        {
            AnimatedMessageBox.Show("请先从首页选择一个实例。", "提示");
            return;
        }

        var root = InstancePathService.GetGameDirectory(App.Paths, App.Settings.Data, _instanceId);
        if (!Directory.Exists(root))
        {
            AnimatedMessageBox.Show("该版本的实例目录不存在。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出 QuartzPack",
            FileName = $"{_instanceName}.qpack",
            Filter = "QuartzPack (*.qpack)|*.qpack"
        };
        if (dialog.ShowDialog() != true) return;

        ExportStatus.Visibility = Visibility.Visible;
        ExportStatus.Text = "正在导出，请稍候…";
        ExportButton.IsEnabled = false;
        try
        {
            await Task.Run(() =>
            {
                var instance = new InstanceStore(App.Paths.InstancesDir).List()
                    .FirstOrDefault(item => item.Id == _instanceId)
                    ?? throw new InvalidDataException("未找到实例清单。");
                new ModpackService().Export(dialog.FileName, instance, root, new ModpackExportOptions(
                    IncludeMods.IsChecked == true,
                    IncludeResourcePacks.IsChecked == true,
                    IncludeShaderPacks.IsChecked == true,
                    IncludeSaves.IsChecked == true,
                    IncludeCrashes.IsChecked == true,
                    IncludeGameSettings.IsChecked == true));
            });

            ExportStatus.Text = $"导出完成：{dialog.FileName}";
            AnimatedMessageBox.Show(
                $"版本「{_instanceName}」已导出到：\n\n{dialog.FileName}",
                "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ExportStatus.Text = $"导出失败：{ex.Message}";
            AnimatedMessageBox.Show($"导出失败：{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ExportButton.IsEnabled = true;
        }
    }
}
