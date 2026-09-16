using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace QuartzLauncher.Models;

public class ItemProgress : INotifyPropertyChanged
{
    public string Target { get; set; } = "";
    private string _fileName = "";
    private string _statusText = "等待中";
    private double _progress;
    private string _bytesText = "";
    private bool _isCompleted;

    public string FileName { get => _fileName; set { _fileName = value; OnPropertyChanged(); } }
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
    public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
    public string BytesText { get => _bytesText; set { _bytesText = value; OnPropertyChanged(); } }
    public bool IsCompleted { get => _isCompleted; set { _isCompleted = value; OnPropertyChanged(); } }

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
