using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class BrowserPage : Page, IPageTransitionAware
{
    private const string LayoutStabilizerScript = """
        (() => {
            const style = document.createElement('style');
            style.id = 'quartz-layout-stabilizer';
            style.textContent = 'html { visibility: hidden !important; }';
            document.documentElement.appendChild(style);

            const reveal = () => {
                const current = document.getElementById('quartz-layout-stabilizer');
                if (current) current.remove();
            };

            const loadLazyImages = () => {
                document.querySelectorAll('img[data-src]').forEach(image => {
                    const source = image.getAttribute('data-src');
                    if (source) image.setAttribute('src', source);
                    image.removeAttribute('data-src');
                    image.classList.remove('lazy');
                    image.loading = 'eager';
                });
            };

            const waitForImages = () => {
                const lazyImages = Array.from(document.querySelectorAll('img[data-src]'));
                loadLazyImages();
                const pending = lazyImages
                    .filter(image => !image.complete)
                    .map(image => new Promise(resolve => {
                        image.addEventListener('load', resolve, { once: true });
                        image.addEventListener('error', resolve, { once: true });
                    }));
                Promise.all(pending).then(() => setTimeout(reveal, 700));
                setTimeout(reveal, 8000);
            };

            if (document.readyState === 'loading')
                document.addEventListener('DOMContentLoaded', waitForImages, { once: true });
            else
                waitForImages();
        })();
        """;

    private readonly string _initialUrl;
    private readonly Page? _backTarget;
    private bool _initialized;

    public BrowserPage(string url, Page? backTarget = null)
    {
        InitializeComponent();
        _initialUrl = url;
        _backTarget = backTarget;
        Loaded += BrowserPage_Loaded;
        Unloaded += BrowserPage_Unloaded;
    }

    private async void BrowserPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;

        try
        {
            var userData = App.Paths.BrowserDataDir;
            Directory.CreateDirectory(userData);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await Browser.EnsureCoreWebView2Async(environment);
            _initialized = true;
            await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(LayoutStabilizerScript);
            Browser.NavigationStarting += Browser_NavigationStarting;
            Browser.NavigationCompleted += Browser_NavigationCompleted;
            Browser.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            Navigate(_initialUrl);
        }
        catch (Exception ex)
        {
            StatusText.Text = "WebView2 Runtime 未安装";
            AnimatedMessageBox.Show(
                $"内置浏览器初始化失败：{ex.Message}\n\n请安装 Microsoft Edge WebView2 Runtime 后重试。",
                "内置浏览器", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BrowserPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_initialized || Browser.CoreWebView2 == null) return;
        Browser.NavigationStarting -= Browser_NavigationStarting;
        Browser.NavigationCompleted -= Browser_NavigationCompleted;
        Browser.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
    }

    public async Task PrepareForNavigationExitAsync()
    {
        if (!_initialized || Browser.CoreWebView2 == null || Browser.ActualWidth <= 0 || Browser.ActualHeight <= 0)
            return;

        try
        {
            using var stream = new MemoryStream();
            await Browser.CoreWebView2.CapturePreviewAsync(
                CoreWebView2CapturePreviewImageFormat.Png, stream);
            stream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            BrowserSnapshot.Source = bitmap;
            Browser.Visibility = Visibility.Collapsed;
            BrowserSnapshot.Visibility = Visibility.Visible;
        }
        catch
        {
            // Use the normal transition if WebView2 cannot capture a preview.
        }
    }

    private void Navigate(string? value)
    {
        var url = value?.Trim() ?? "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            StatusText.Text = "请输入 HTTP/HTTPS 地址";
            return;
        }

        AddressBox.Text = uri.AbsoluteUri;
        Browser.CoreWebView2?.Navigate(uri.AbsoluteUri);
    }

    private void Browser_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        AddressBox.Text = e.Uri;
        StatusText.Text = "正在加载...";
    }

    private void Browser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        StatusText.Text = e.IsSuccess ? "已加载" : $"加载失败 ({e.WebErrorStatus})";
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        Navigate(e.Uri);
    }

    private void BackNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoBack == true)
            Browser.CoreWebView2.GoBack();
    }

    private void ForwardNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoForward == true)
            Browser.CoreWebView2.GoForward();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Browser.CoreWebView2?.Reload();

    private void Stop_Click(object sender, RoutedEventArgs e) => Browser.CoreWebView2?.Stop();

    private void Navigate_Click(object sender, RoutedEventArgs e) => Navigate(AddressBox.Text);

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Navigate(AddressBox.Text);
        e.Handled = true;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow mainWindow) return;
        if (_backTarget != null)
            mainWindow.NavigateTo(_backTarget);
        else
            mainWindow.NavigateToServerBrowser();
    }
}
