using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class ServerBrowserPage : Page
{
    private bool _loading;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private bool _domestic = true;
    private string _keyword = "";
    private readonly Dictionary<int, ServerDirectoryResult> _pageCache = new();
    private bool _hasLoaded;

    public ServerBrowserPage()
    {
        InitializeComponent();
        IsVisibleChanged += async (_, e) =>
        {
            if (e.NewValue is true && !_hasLoaded)
            {
                _hasLoaded = true;
                await LoadServersAsync();
            }
        };
    }

    private DispatcherTimer? _spinnerTimer;

    private void ShowLoading()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        _spinnerTimer?.Stop();
        _spinnerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        var angle = 0.0;
        _spinnerTimer.Tick += (_, _) =>
        {
            angle = (angle + 8) % 360;
            ((RotateTransform)SpinnerPath.RenderTransform).Angle = angle;
        };
        _spinnerTimer.Start();
    }

    private void HideLoading()
    {
        _spinnerTimer?.Stop();
        _spinnerTimer = null;
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private async Task LoadServersAsync(bool forceRefresh = false)
    {
        if (_loading) return;
        _loading = true;
        ShowLoading();
        try
        {
            if (forceRefresh)
            {
                _pageCache.Clear();
                _currentPage = 1;
            }

            if (!_pageCache.TryGetValue(_currentPage, out var result))
            {
                StatusText.Text = $"正在获取第 {_currentPage} 页...";
                for (var retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        result = await MinecraftServerDirectoryService.GetOnlineServersAsync(_currentPage);
                        break;
                    }
                    catch when (retry < 2)
                    {
                        await Task.Delay(800 * (retry + 1));
                    }
                }
                if (result == null)
                    throw new IOException("服务器列表获取失败，请稍后重试");
                _pageCache[_currentPage] = result;
            }

            _currentPage = result.Page;
            _totalPages = Math.Max(1, result.TotalPages);
            RenderServers(result.Items);
            PageText.Text = $"{_currentPage} / {_totalPages}";
            PreviousButton.IsEnabled = _currentPage > 1;
            NextButton.IsEnabled = _currentPage < _totalPages;
        }
        catch (Exception ex)
        {
            StatusText.Text = "获取失败";
            AnimatedMessageBox.Show($"服务器列表获取失败: {ex.Message}", "多人游戏服务器", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _loading = false;
            HideLoading();
        }
    }

    private void RenderServers(IEnumerable<ServerDirectoryItem> source)
    {
        var servers = source
            .Where(server => _domestic
                ? string.Equals(server.CountryCode, "CN", StringComparison.OrdinalIgnoreCase)
                : !string.Equals(server.CountryCode, "CN", StringComparison.OrdinalIgnoreCase))
            .Where(server => string.IsNullOrWhiteSpace(_keyword)
                || $"{server.Name} {server.Description}".Contains(_keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ServerPanel.Children.Clear();
        foreach (var server in servers)
            ServerPanel.Children.Add(CreateServerCard(server));
        StatusText.Text = servers.Count == 0 ? "暂无匹配服务器" : $"本页显示 {servers.Count} 个服务器";
    }

    private Border CreateServerCard(ServerDirectoryItem server)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Image { Width = 48, Height = 48, Margin = new Thickness(0, 0, 14, 0), Stretch = Stretch.Uniform };
        if (Uri.TryCreate(server.IconUrl, UriKind.Absolute, out var iconUri))
            icon.Source = new BitmapImage(iconUri);
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock
        {
            Text = server.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush")
        });
        details.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(server.Description) ? "Minecraft Server" : server.Description,
            FontSize = 11,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 4, 12, 0)
        });
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        var status = new TextBlock
        {
            Text = $"{server.Players} 人  ·  {server.Ping} ms",
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 16, 0)
        };
        Grid.SetColumn(status, 2);
        grid.Children.Add(status);

        var button = new Button { Content = "详情", Tag = server.PageUrl, Padding = new Thickness(12, 5, 12, 5), VerticalAlignment = VerticalAlignment.Center };
        button.SetResourceReference(StyleProperty, "BtnBase");
        button.Click += Details_Click;
        Grid.SetColumn(button, 3);
        grid.Children.Add(button);

        var card = new Border { Child = grid, Margin = new Thickness(0, 0, 0, 10) };
        card.SetResourceReference(StyleProperty, "CardStyle");
        return card;
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url }
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateToBrowser(uri.AbsoluteUri);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadServersAsync(true);

    private async void Domestic_Click(object sender, RoutedEventArgs e)
    {
        _domestic = true;
        _currentPage = 1;
        await LoadServersAsync();
    }

    private async void Foreign_Click(object sender, RoutedEventArgs e)
    {
        _domestic = false;
        _currentPage = 1;
        await LoadServersAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _keyword = SearchBox.Text.Trim();
        if (_pageCache.TryGetValue(_currentPage, out var result))
            RenderServers(result.Items);
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage <= 1 || _loading) return;
        _currentPage--;
        await LoadServersAsync();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage >= _totalPages || _loading) return;
        _currentPage++;
        await LoadServersAsync();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateTo(mainWindow.HomePage);
    }
}
