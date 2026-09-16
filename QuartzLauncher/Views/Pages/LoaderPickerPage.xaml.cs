using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class LoaderPickerPage : Page, IStandaloneSidebarPage
{
    public static Dictionary<string, LoaderSelection> SelectedLoaders { get; } = new();

    public class LoaderSelection
    {
        public string Type { get; set; } = "";
        public string MinecraftVersion { get; set; } = "";
        public string Version { get; set; } = "";
        public bool AutoInstall { get; set; }
    }

    private readonly string _mcVersion;
    private readonly LoaderService _loader;

    public LoaderPickerPage(string mcVersion, LoaderService loader)
    {
        _mcVersion = mcVersion;
        _loader = loader;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoaderBar.Opacity = 0;
        LoaderBar.RenderTransform = new TranslateTransform(-260, 0);
        LoaderFrame.Opacity = 0;
        LoaderFrame.RenderTransform = new TranslateTransform(300, 0);

        LoaderFrame.Navigate(new LoaderDetailPage("fabric", _mcVersion, _loader));

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            AnimateIn();
        };
        timer.Start();
    }

    internal void AnimateIn()
    {
        var style = App.Settings.Data.PageAnimationStyle;
        if (style is "none" or "tear")
        {
            LoaderBar.Opacity = 1;
            LoaderFrame.Opacity = 1;
            LoaderBar.RenderTransform = null;
            LoaderFrame.RenderTransform = null;
            return;
        }
        var quick = style == "quick";
        var duration = quick ? 180 : 450;
        var barFrom = quick ? -100 : -260;
        var contentFrom = quick ? 140 : 350;
        var barSlide = new DoubleAnimation(barFrom, 0, TimeSpan.FromMilliseconds(duration));
        barSlide.EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut };
        var barTransform = new TranslateTransform(-260, 0);
        LoaderBar.RenderTransform = barTransform;
        barTransform.BeginAnimation(TranslateTransform.XProperty, barSlide);
        LoaderBar.Opacity = 1;

        var contentSlide = new DoubleAnimation(contentFrom, 0, TimeSpan.FromMilliseconds(duration));
        contentSlide.EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut };
        var contentTransform = new TranslateTransform(350, 0);
        LoaderFrame.RenderTransform = contentTransform;
        contentTransform.BeginAnimation(TranslateTransform.XProperty, contentSlide);
        LoaderFrame.Opacity = 1;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var versionsPage = mainWindow.GetVersionsPage();
            versionsPage.RestoreVersionSelection(_mcVersion);
            mainWindow.NavigateTo(versionsPage);
        }
    }

    public async Task AnimateStandaloneExitAsync(bool quick)
    {
        var ease = new SineEase { EasingMode = EasingMode.EaseIn };
        var duration = quick ? 180 : 450;
        var sidebarDistance = quick ? -100 : -260;
        var contentDistance = quick ? 140 : 350;
        var barTransform = new TranslateTransform();
        var contentTransform = new TranslateTransform();
        LoaderBar.RenderTransform = barTransform;
        LoaderFrame.RenderTransform = contentTransform;
        barTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, sidebarDistance, TimeSpan.FromMilliseconds(duration)) { EasingFunction = ease });
        contentTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, contentDistance, TimeSpan.FromMilliseconds(duration)) { EasingFunction = ease });
        await Task.Delay(duration);
    }

    private void NavFabric_Click(object sender, RoutedEventArgs e)
        => LoaderFrame.Navigate(new LoaderDetailPage("fabric", _mcVersion, _loader));

    private void NavForge_Click(object sender, RoutedEventArgs e)
        => LoaderFrame.Navigate(new LoaderDetailPage("forge", _mcVersion, _loader));

    private void NavNeoForge_Click(object sender, RoutedEventArgs e)
        => LoaderFrame.Navigate(new LoaderDetailPage("neoforge", _mcVersion, _loader));

    private void NavQuilt_Click(object sender, RoutedEventArgs e)
        => LoaderFrame.Navigate(new LoaderDetailPage("quilt", _mcVersion, _loader));

    private void NavFabricApi_Click(object sender, RoutedEventArgs e)
        => LoaderFrame.Navigate(new LoaderDetailPage("fabricapi", _mcVersion, _loader));

    private void NavOptiFine_Click(object sender, RoutedEventArgs e)
        => LoaderFrame.Navigate(new LoaderDetailPage("optifine", _mcVersion, _loader));

    private void NavLiteLoader_Click(object sender, RoutedEventArgs e)
        => LoaderFrame.Navigate(new LoaderDetailPage("liteloader", _mcVersion, _loader));
}
