using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace QuartzLauncher.Services;

public sealed class SmoothProgressDialog : Window
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _statusText;
    private readonly TextBlock _countText;
    private readonly Button _cancelButton;
    private double _displayedValue;
    private bool _allowClose;

    public CancellationToken CancellationToken => _cancellation.Token;

    public SmoothProgressDialog(string title)
    {
        Title = title;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.Transparent;
        AllowsTransparency = true;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 20, 24, 20),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 6,
                Opacity = 0.3,
                Color = Colors.Black
            }
        };
        card.SetResourceReference(BackgroundProperty, "DialogCardBrush");
        card.SetResourceReference(BorderBrushProperty, "BorderBrush");

        var root = new StackPanel();
        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        var closeButton = CreateButton("关闭", "BtnBase", 86);
        closeButton.Click += (_, _) => RequestClose();
        Grid.SetColumn(closeButton, 1);
        header.Children.Add(titleText);
        header.Children.Add(closeButton);

        _statusText = new TextBlock
        {
            Text = "准备扫描...",
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _statusText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        _progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 12,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _progressBar.SetResourceReference(ProgressBar.ForegroundProperty, "PrimaryBrush");
        _progressBar.SetResourceReference(ProgressBar.BackgroundProperty, "InputBgBrush");

        _countText = new TextBlock
        {
            Text = "已发现 0 个 Java",
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 18)
        };
        _countText.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _cancelButton = CreateButton("取消扫描", "BtnBase", 100);
        _cancelButton.Click += (_, _) => RequestClose();
        actions.Children.Add(_cancelButton);

        root.Children.Add(header);
        root.Children.Add(_statusText);
        root.Children.Add(_progressBar);
        root.Children.Add(_countText);
        root.Children.Add(actions);
        card.Child = root;
        Content = new Grid { Margin = new Thickness(24), Children = { card } };

        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                Cancel();
                args.Handled = true;
            }
        };
        Closing += OnClosing;
    }

    public void Update(JavaScanProgress progress)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => Update(progress));
            return;
        }
        if (_allowClose) return;

        _statusText.Text = progress.Message;
        _countText.Text = $"已发现 {progress.FoundCount} 个 Java";
        var target = Math.Clamp(progress.Fraction, 0, 1);
        var animation = new DoubleAnimation
        {
            From = _displayedValue,
            To = target,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        _progressBar.BeginAnimation(RangeBase.ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
        _displayedValue = target;
    }

    public void Complete(string message, int foundCount)
    {
        Update(new JavaScanProgress(1, message, foundCount));
        _cancelButton.Content = "关闭";
        _cancelButton.IsEnabled = true;
        _allowClose = true;
    }

    public void Cancel()
    {
        if (_cancellation.IsCancellationRequested) return;
        _cancellation.Cancel();
        _statusText.Text = "正在取消扫描...";
        _cancelButton.IsEnabled = false;
    }

    private void RequestClose()
    {
        if (_allowClose)
            Close();
        else
            Cancel();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        Cancel();
        e.Cancel = true;
    }

    private static Button CreateButton(object content, string styleKey, double width)
    {
        var button = new Button
        {
            Content = content,
            Width = width,
            Height = 38,
            Padding = new Thickness(12, 5, 12, 5),
            Cursor = Cursors.Hand
        };
        button.SetResourceReference(StyleProperty, styleKey);
        return button;
    }
}
