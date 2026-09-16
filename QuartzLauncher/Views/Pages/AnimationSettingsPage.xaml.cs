using System.Windows;
using System.Windows.Controls;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class AnimationSettingsPage : Page
{
    private bool _isLoading;

    public AnimationSettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadSelection();
    }

    private void LoadSelection()
    {
        _isLoading = true;
        try
        {
            TearSpeedSlider.Value = Math.Clamp(App.Settings.Data.TearAnimationDurationMs, 350, 1540);
            TearSpeedText.Text = $"{(int)TearSpeedSlider.Value} ms";
            TearApexSlider.Value = Math.Clamp(App.Settings.Data.TearApexSharpness, 10, 90);
            TearApexText.Text = $"{(int)TearApexSlider.Value}%";
            TearRandomDirectionCheck.IsChecked = App.Settings.Data.TearRandomDirection;
            if (string.Equals(App.Settings.Data.TearDirection, "horizontal", StringComparison.OrdinalIgnoreCase))
                TearHorizontalRadio.IsChecked = true;
            else
                TearVerticalRadio.IsChecked = true;
            UpdateDirectionControls();

            switch (App.Settings.Data.ThemeTransitionStyle)
            {
                case "topRight": TopRightRadio.IsChecked = true; break;
                case "ripple": RippleRadio.IsChecked = true; break;
                case "fade": FadeRadio.IsChecked = true; break;
                case "none": NoneRadio.IsChecked = true; break;
                default: TopLeftRadio.IsChecked = true; break;
            }

            switch (App.Settings.Data.PageAnimationStyle)
            {
                case "quick": QuickSlideRadio.IsChecked = true; break;
                case "zoom": ZoomRadio.IsChecked = true; break;
                case "dissolve": DissolveRadio.IsChecked = true; break;
                case "tear": TearRadio.IsChecked = true; break;
                case "none": NoPageAnimationRadio.IsChecked = true; break;
                default: SlideRadio.IsChecked = true; break;
            }
            if (App.Settings.Data.AdvancedAnimationEnabled)
                SlideRadio.IsChecked = true;
            TearSpeedPanel.Visibility = App.Settings.Data.PageAnimationStyle == "tear"
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateCinematicStatus();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Transition_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender is not RadioButton { Tag: string style }) return;
        App.Settings.Data.ThemeTransitionStyle = style;
        App.Settings.Save();
    }

    private void PageAnimation_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender is not RadioButton { Tag: string style }) return;
        App.Settings.Data.PageAnimationStyle = style;
        App.Settings.Data.AdvancedAnimationEnabled = false;
        App.Settings.Save();
        TearSpeedPanel.Visibility = style == "tear" ? Visibility.Visible : Visibility.Collapsed;
        UpdateCinematicStatus();
    }

    private void Cinematic_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (App.Settings.Data.AdvancedAnimationEnabled)
        {
            App.Settings.Data.AdvancedAnimationEnabled = false;
            App.Settings.Save();
            UpdateCinematicStatus();
            return;
        }

        if (App.Settings.Data.PageAnimationStyle != "slide")
        {
            var result = AnimatedMessageBox.Show(
                "警告：星轨流光仅能运用于“默认平移”，否则动画会出现异常。\n\n点击“确定”后将自动切换为“默认平移”并启用星轨流光；点击“取消”则保持当前动画设置。",
                "高级动效警告", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK) return;
            App.Settings.Data.PageAnimationStyle = "slide";
            SlideRadio.IsChecked = true;
        }

        App.Settings.Data.AdvancedAnimationEnabled = true;
        App.Settings.Save();
        UpdateCinematicStatus();
    }

    private void UpdateCinematicStatus()
    {
        if (CinematicButton == null || CinematicStatusText == null) return;
        var enabled = App.Settings.Data.AdvancedAnimationEnabled;
        CinematicButton.Content = enabled ? "停用星轨流光" : "启用星轨流光";
        CinematicStatusText.Text = enabled
            ? "当前转场：星轨流光"
            : "当前未启用，点击按钮即可切换。";
    }

    private void TearSpeed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TearSpeedText == null) return;
        var duration = (int)e.NewValue;
        TearSpeedText.Text = $"{duration} ms";
        if (_isLoading) return;
        App.Settings.Data.TearAnimationDurationMs = duration;
        App.Settings.Save();
    }

    private void TearApex_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TearApexText == null) return;
        var sharpness = (int)e.NewValue;
        TearApexText.Text = $"{sharpness}%";
        if (_isLoading) return;
        App.Settings.Data.TearApexSharpness = sharpness;
        App.Settings.Save();
    }

    private void TearRandomDirection_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Data.TearRandomDirection = TearRandomDirectionCheck.IsChecked == true;
        UpdateDirectionControls();
        App.Settings.Save();
    }

    private void TearDirection_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading || sender is not RadioButton { Tag: string direction }) return;
        App.Settings.Data.TearDirection = direction;
        App.Settings.Save();
    }

    private void UpdateDirectionControls()
    {
        TearFixedDirectionPanel.IsEnabled = TearRandomDirectionCheck.IsChecked != true;
    }
}
