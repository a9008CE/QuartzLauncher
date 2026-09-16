using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class DownloadCenterPage : UserControl
{
    private bool _loadingSettings;
    private readonly bool _versionOnly;
    private readonly ICollectionView _taskView;

    public DownloadCenterPage() : this(false)
    {
    }

    public DownloadCenterPage(bool versionOnly)
    {
        _versionOnly = versionOnly;
        InitializeComponent();
        var source = new CollectionViewSource { Source = DownloadManager.Instance.Tasks };
        _taskView = source.View;
        if (_versionOnly)
        {
            _taskView.Filter = item => item is DownloadTask task
                && (task.Category is "version" or "java");
            TitleText.Text = "版本下载管理";
        }
        TaskList.ItemsSource = _taskView;
        Loaded += (_, _) => LoadSettings();
        DownloadManager.Instance.QueueChanged += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            _taskView.Refresh();
            RefreshSummary();
        });
        DownloadManager.Instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DownloadManager.MaxConcurrent))
                Dispatcher.BeginInvoke(RefreshThreadCount);
        };
    }

    private void LoadSettings()
    {
        _loadingSettings = true;
        AutoThreadsCheck.IsChecked = App.Settings.Data.AutoDownloadThreads;
        ThreadSlider.Value = Math.Clamp(App.Settings.Data.ManualDownloadThreads, 1, 16);
        ThreadSlider.IsEnabled = AutoThreadsCheck.IsChecked != true;
        _loadingSettings = false;
        RefreshSummary();
        RefreshThreadCount();
    }

    private void RefreshSummary()
    {
        var tasks = _taskView.Cast<DownloadTask>().ToList();
        var active = tasks.Count(task => task.Status is DownloadTaskStatus.Queued or DownloadTaskStatus.Downloading);
        SummaryText.Text = tasks.Count == 0 ? "暂无下载任务" : $"共 {tasks.Count} 个任务，{active} 个正在进行";
    }

    private void RefreshThreadCount()
    {
        ThreadCountText.Text = DownloadManager.Instance.MaxConcurrent.ToString();
    }

    private void AutoThreads_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        var automatic = AutoThreadsCheck.IsChecked == true;
        ThreadSlider.IsEnabled = !automatic;
        DownloadManager.Instance.AutoAdjustConcurrency = automatic;
        if (!automatic) DownloadManager.Instance.MaxConcurrent = (int)ThreadSlider.Value;
        App.Settings.Data.AutoDownloadThreads = automatic;
        App.Settings.Save();
    }

    private void ThreadSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingSettings || AutoThreadsCheck.IsChecked == true) return;
        var threads = Math.Clamp((int)Math.Round(e.NewValue), 1, 16);
        DownloadManager.Instance.MaxConcurrent = threads;
        App.Settings.Data.ManualDownloadThreads = threads;
        App.Settings.Save();
    }

    private void TogglePause_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DownloadTask task }) return;
        if (task.Status is DownloadTaskStatus.Paused or DownloadTaskStatus.Failed)
            DownloadManager.Instance.Resume(task);
        else
            DownloadManager.Instance.Pause(task);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadTask task }) DownloadManager.Instance.Cancel(task);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadTask task })
            DownloadManager.Instance.Remove(task);
    }

    private void CancelAll_Click(object sender, RoutedEventArgs e)
    {
        if (!_versionOnly)
        {
            DownloadManager.Instance.CancelAll();
            return;
        }
        foreach (var task in _taskView.Cast<DownloadTask>().ToList())
            DownloadManager.Instance.Cancel(task);
    }

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadTask task })
            task.IsExpanded = !task.IsExpanded;
    }
}
