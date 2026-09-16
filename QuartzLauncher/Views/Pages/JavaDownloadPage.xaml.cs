using System.IO;
using System.Windows;
using System.Windows.Controls;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class JavaDownloadPage : UserControl
{
    private readonly HashSet<int> _installing = new();

    public JavaDownloadPage()
    {
        InitializeComponent();
        InstallDirectoryText.Text = JavaService.InstallDirectory;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        StatusText.Text = "正在从 Adoptium 获取 Java 版本列表...";
        JavaList.Items.Clear();
        try
        {
            var options = await JavaService.GetDownloadOptionsAsync();
            foreach (var option in options)
                JavaList.Items.Add(option);
            StatusText.Text = options.Count == 0
                ? "暂时没有可用的 Java 下载项，请稍后刷新。"
                : $"共 {options.Count} 个 Java 版本，列表从 Java 8 到当前可用版本。";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Java 版本列表加载失败: {ex.Message}";
        }
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: JavaDownloadOption option } button
            || !_installing.Add(option.MajorVersion)) return;

        button.IsEnabled = false;
        try
        {
            var installed = JavaService.FindInstalled(option.MajorVersion);
            if (installed != null)
            {
                var result = AnimatedMessageBox.Show(
                    $"C:\\QuartzLauncher\\Java 中已经安装 Java {option.MajorVersion}。是否下载并重新安装 Temurin {option.Version}？",
                    "重新安装 Java", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
            }

            var item = JavaService.CreateDownloadItem(option);
            JavaInfo? installedJava = null;
            var task = DownloadManager.Instance.Enqueue(
                $"Java {option.MajorVersion} · Temurin {option.Version}",
                [item],
                workers: App.Settings.Data.DownloadWorkers,
                category: "java",
             postDownloadAction: async () =>
             {
                    await Dispatcher.InvokeAsync(() =>
                        StatusText.Text = $"Java {option.MajorVersion} 下载完成，正在解压安装...");
                    installedJava = await JavaService.InstallDownloadedAsync(option);
             });

            StatusText.Text = $"Java {option.MajorVersion} 已加入下载队列，下载完成后会自动安装。";
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateToDownloadCenter(versionOnly: true);
            if (task.Tcs != null) await task.Tcs.Task;
            if (task.Status != DownloadTaskStatus.Completed)
                throw new IOException($"Java {option.MajorVersion} 安装失败: {task.Error}");

            installedJava ??= JavaService.FindInstalled(option.MajorVersion);
            if (installedJava == null)
            {
                StatusText.Text = $"Java {option.MajorVersion} 下载完成，正在补偿安装...";
                installedJava = await JavaService.InstallDownloadedAsync(option);
            }

            if (installedJava != null)
            {
                var detected = App.Settings.Data.DetectedJavas
                    .Where(java => !java.Path.Equals(installedJava.Path, StringComparison.OrdinalIgnoreCase))
                    .Append(installedJava)
                    .ToList();
                App.Settings.Data.DetectedJavas = detected;
                App.Settings.Save();
            }
            StatusText.Text = $"Java {option.MajorVersion} 安装完成，启动游戏时会自动匹配。路径: {installedJava?.Path}";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            AnimatedMessageBox.Show(ex.Message, "Java 安装失败",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _installing.Remove(option.MajorVersion);
            button.IsEnabled = true;
        }
    }
}
