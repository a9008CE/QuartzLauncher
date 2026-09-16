using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class VersionSettingsPage : Page, IStandaloneSidebarPage
{
    private int _contentNavigationGeneration;
    private string? _instanceId;
    private string? _instanceName;
    private Instance? _instance;

    public VersionSettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public VersionSettingsPage(string instanceId, string instanceName) : this()
    {
        _instanceId = instanceId;
        _instanceName = instanceName;
    }

    public VersionSettingsPage(Instance instance) : this()
    {
        _instance = instance;
        _instanceId = instance.Id;
        _instanceName = instance.Name;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PresetBar.BeginAnimation(OpacityProperty, null);
        PresetFrame.BeginAnimation(OpacityProperty, null);
        PresetBar.Opacity = 0;
        PresetBar.RenderTransform = new TranslateTransform(-260, 0);
        PresetFrame.Opacity = 0;
        PresetFrame.RenderTransform = new TranslateTransform(300, 0);

        if (PresetFrame.Content != null)
        {
            SelectPresetNavButton(PresetFrame.Content as Page ?? CreateDetailPage(_instanceId!, _instanceName!));
        }
        else if (_instance != null)
        {
            PresetFrame.Navigate(CreateDetailPage(_instance));
        }
        else if (!string.IsNullOrEmpty(_instanceId) && !string.IsNullOrEmpty(_instanceName))
        {
            PresetFrame.Navigate(CreateDetailPage(_instanceId, _instanceName));
        }
        else
        {
            PresetFrame.Navigate(new ModsPage());
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Render, AnimateIn);
    }

    internal void AnimateIn()
    {
        var style = App.Settings.Data.PageAnimationStyle;
        if (style is "none" or "tear")
        {
            PresetBar.Opacity = 1;
            PresetFrame.Opacity = 1;
            PresetBar.RenderTransform = null;
            PresetFrame.RenderTransform = null;
            return;
        }
        var quick = style == "quick";
        var duration = quick ? 180 : 450;
        var barFrom = quick ? -100 : -260;
        var contentFrom = quick ? 140 : 350;
        var barSlide = new DoubleAnimation(barFrom, 0, TimeSpan.FromMilliseconds(duration));
        barSlide.EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut };
        var barTransform = new TranslateTransform(-260, 0);
        PresetBar.RenderTransform = barTransform;
        barTransform.BeginAnimation(TranslateTransform.XProperty, barSlide);
        PresetBar.Opacity = 1;

        var contentSlide = new DoubleAnimation(contentFrom, 0, TimeSpan.FromMilliseconds(duration));
        contentSlide.EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut };
        var contentTransform = new TranslateTransform(350, 0);
        PresetFrame.RenderTransform = contentTransform;
        contentTransform.BeginAnimation(TranslateTransform.XProperty, contentSlide);
        PresetFrame.Opacity = 1;
    }

    public async Task AnimateStandaloneExitAsync(bool quick)
    {
        var ease = new SineEase { EasingMode = EasingMode.EaseIn };
        var duration = quick ? 180 : 450;
        var sidebarDistance = quick ? -100 : -260;
        var contentDistance = quick ? 140 : 350;
        var barTransform = new TranslateTransform();
        var contentTransform = new TranslateTransform();
        PresetBar.RenderTransform = barTransform;
        PresetFrame.RenderTransform = contentTransform;
        barTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, sidebarDistance, TimeSpan.FromMilliseconds(duration)) { EasingFunction = ease });
        contentTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, contentDistance, TimeSpan.FromMilliseconds(duration)) { EasingFunction = ease });
        await Task.Delay(duration);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateTo(mainWindow.HomePage);
    }

    private void NavigateContent(Page page)
        => _ = NavigateContentAsync(page, ++_contentNavigationGeneration);

    private async Task NavigateContentAsync(Page page, int generation)
    {
        PresetFrame.BeginAnimation(OpacityProperty, null);
        PresetFrame.Opacity = 1;
        PresetFrame.IsHitTestVisible = false;
        var transform = new TranslateTransform();
        PresetFrame.RenderTransform = transform;
        var slideOut = new DoubleAnimation(0, 24, TimeSpan.FromMilliseconds(110));
        transform.BeginAnimation(TranslateTransform.XProperty, slideOut);
        await Task.Delay(110);
        if (generation != _contentNavigationGeneration) return;

        PresetFrame.Navigate(page);
        SelectPresetNavButton(page);
        PresetFrame.Opacity = 1;
        var nextTransform = new TranslateTransform();
        PresetFrame.RenderTransform = nextTransform;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        nextTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
        await Task.Delay(240);
        if (generation != _contentNavigationGeneration) return;
        PresetFrame.BeginAnimation(OpacityProperty, null);
        PresetFrame.Opacity = 1;
        PresetFrame.RenderTransform = null;
        PresetFrame.IsHitTestVisible = true;
    }

    private void SelectPresetNavButton(Page page)
    {
        NavDetail.IsChecked = page is InstanceDetailPage;
        NavMods.IsChecked = page is ModsPage;
        NavSaves.IsChecked = page is SavesPage;
        NavResourcePacks.IsChecked = page is ResourcePacksPage;
        NavShaderPacks.IsChecked = page is ShaderPacksPage;
        NavPreset5.IsChecked = page is ExportVersionPage;
    }

    private void Preset1_Click(object sender, RoutedEventArgs e) => NavigateContent(new ModsPage(_instanceId));
    private void Preset2_Click(object sender, RoutedEventArgs e) => NavigateContent(new SavesPage(_instanceId));
    private void Preset3_Click(object sender, RoutedEventArgs e) => NavigateContent(new ResourcePacksPage(_instanceId));
    private void Preset4_Click(object sender, RoutedEventArgs e) => NavigateContent(new ShaderPacksPage(_instanceId));
    private void Preset5_Click(object sender, RoutedEventArgs e)
        => NavigateContent(new ExportVersionPage(_instanceId ?? "", _instanceName ?? ""));
    private void Detail_Click(object sender, RoutedEventArgs e)
    {
        if (_instance != null)
            NavigateContent(CreateDetailPage(_instance));
        else if (!string.IsNullOrEmpty(_instanceId) && !string.IsNullOrEmpty(_instanceName))
            NavigateContent(CreateDetailPage(_instanceId, _instanceName));
        else
            AnimatedMessageBox.Show("请先从首页选择一个实例。", "提示");
    }

    private InstanceDetailPage CreateDetailPage(Instance instance)
    {
        var page = new InstanceDetailPage(instance);
        page.InstanceDeleted += Detail_InstanceDeleted;
        return page;
    }

    private InstanceDetailPage CreateDetailPage(string instanceId, string instanceName)
    {
        var page = new InstanceDetailPage(instanceId, instanceName);
        page.InstanceDeleted += Detail_InstanceDeleted;
        return page;
    }

    private void Detail_InstanceDeleted(object? sender, EventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateTo(mainWindow.HomePage);
    }
}
