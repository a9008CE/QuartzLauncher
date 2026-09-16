using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace QuartzLauncher.Models;

public enum DownloadTaskStatus
{
    Queued,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public class DownloadTask : INotifyPropertyChanged
{
    private string _name = "";
    private string _statusText = "排队中";
    private DownloadTaskStatus _status = DownloadTaskStatus.Queued;
    private double _progress;
    private long _bytesReceived;
    private long _totalBytes;
    private string _speed = "";
    private string _error = "";
    private bool _isExpanded;

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Category { get; set; } = "general";
    public int MaxAttempts { get; set; } = 3;
    public List<DownloadItem> Items { get; set; } = new();
    public int TotalFiles => Items.Count;
    public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnPropertyChanged(); } }
    public ObservableCollection<ItemProgress> ItemProgresses { get; } = new();

    public DownloadTaskStatus Status
    {
        get => _status;
        set { _status = value; StatusText = GetStatusText(value); OnPropertyChanged(); }
    }

    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
    public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
    public long BytesReceived { get => _bytesReceived; set { _bytesReceived = value; OnPropertyChanged(); OnPropertyChanged(nameof(BytesText)); } }
    public long TotalBytes { get => _totalBytes; set { _totalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(BytesText)); } }
    public string Speed { get => _speed; set { _speed = value; OnPropertyChanged(); } }
    public string Error { get => _error; set { _error = value; OnPropertyChanged(); } }

    private int _completedFiles;
    public int CompletedFiles
    {
        get => _completedFiles;
        set { _completedFiles = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
    }

    public CancellationTokenSource? Cts { get; set; }
    internal int RunGeneration { get; set; }
    public TaskCompletionSource<DownloadTask>? Tcs { get; set; }
    public Func<Task>? PostDownloadAction { get; set; }

    public string ProgressText => TotalFiles > 0
        ? $"{CompletedFiles}/{TotalFiles}"
        : "";

    public string BytesText => TotalBytes > 0
        ? $"{FormatBytes(BytesReceived)} / {FormatBytes(TotalBytes)}"
        : "";

    private static string GetStatusText(DownloadTaskStatus s) => s switch
    {
        DownloadTaskStatus.Queued => "排队中",
        DownloadTaskStatus.Downloading => "下载中",
        DownloadTaskStatus.Paused => "已暂停",
        DownloadTaskStatus.Completed => "完成",
        DownloadTaskStatus.Failed => "失败",
        DownloadTaskStatus.Cancelled => "已取消",
        _ => ""
    };

    public static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        var handler = PropertyChanged;
        if (handler == null) return;

        var args = new PropertyChangedEventArgs(name);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.BeginInvoke(() => handler(this, args));
        else
            handler(this, args);
    }
}
