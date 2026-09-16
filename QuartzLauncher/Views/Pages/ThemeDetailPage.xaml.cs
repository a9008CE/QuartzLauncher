using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class ThemeDetailPage : Page
{
    private static readonly Dictionary<string, string> UiStyleLabels = new()
    {
        ["minimal"] = "极简深色",
        ["frosted"] = "毛玻璃",
        ["flat"] = "扁平浅色",
        ["cyberpunk"] = "赛博朋克 2077",
        ["wanderingearth"] = "流浪地球 550W",
    };

    public ThemeDetailPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var mode = App.Settings.Data.ThemeMode;
        DarkBtn.IsChecked = mode == "dark";
        LightBtn.IsChecked = mode == "light";
        SidebarTextLeftSwitch.IsChecked = App.Settings.Data.SidebarTextLeftAligned;
        var resourceStyle = App.Settings.Data.ResourceCenterStyle == "grid";
        ListStyleBtn.IsChecked = !resourceStyle;
        GridStyleBtn.IsChecked = resourceStyle;

        BuildUiStyleButtons();
        BuildThemeButtons();
    }

    private void ResourceStyle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string style)
        {
            App.Settings.Data.ResourceCenterStyle = style;
            App.Settings.Save();
        }
    }

    private void SidebarTextAlignment_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.Data.SidebarTextLeftAligned = SidebarTextLeftSwitch.IsChecked == true;
        App.Settings.Save();
        App.Theme.Apply();
    }

    private void BuildUiStyleButtons()
    {
        UiStylePanel.Children.Clear();
        var current = App.Settings.Data.UiStyle;

        foreach (var (key, label) in UiStyleLabels)
        {
            var btn = new Button
            {
                Width = 130, Height = 44, Margin = new Thickness(4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,
                BorderBrush = key == current
                    ? (Brush)FindResource("PrimaryBrush")
                    : (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(key == current ? 2 : 1),
                Padding = new Thickness(8),
                Tag = key,
                Style = (Style)FindResource("BtnBase"),
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
            };
            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = key == current ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true,
            };
            labelBlock.SetResourceReference(TextBlock.ForegroundProperty,
                key == current ? "PrimaryBrush" : "TextBrush");
            btn.Content = labelBlock;
            btn.Click += UiStyle_Click;
            UiStylePanel.Children.Add(btn);
        }
    }

    private void BuildThemeButtons()
    {
        ThemePanel.Children.Clear();
        var current = App.Settings.Data.ThemeName;
        var uiStyle = App.Settings.Data.UiStyle;
        var mode = App.Settings.Data.ThemeMode;
        var presets = Theme.GetPresets(uiStyle, mode);

        foreach (var (name, theme) in presets)
        {
            var btn = new Button
            {
                Width = 120, Height = 60, Margin = new Thickness(4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,
                BorderBrush = name == current
                    ? (Brush)FindResource("PrimaryBrush")
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Border)),
                BorderThickness = new Thickness(name == current ? 2 : 1),
                Padding = new Thickness(8),
                Tag = name,
                Style = (Style)FindResource("BtnBase"),
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
            };
            btn.Click += Theme_Click;

            var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameBlock = new TextBlock
            {
                Text = name, FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.TextMuted)),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4),
                SnapsToDevicePixels = true,
            };
            panel.Children.Add(nameBlock);

            var colorRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var c in new[] { theme.Primary, theme.Bg, theme.Card })
            {
                colorRow.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 10, Height = 10, Margin = new Thickness(3, 0, 3, 0),
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c))
                });
            }
            panel.Children.Add(colorRow);
            btn.Content = panel;
            ThemePanel.Children.Add(btn);
        }
    }

    private void UiStyle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string style)
            ApplyWithTransition(() => App.Theme.SetUiStyle(style));
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
            ApplyWithTransition(() => App.Theme.SelectPreset(name));
    }

    private void DarkMode_Click(object sender, RoutedEventArgs e)
    {
        ApplyWithTransition(() => App.Theme.SetMode("dark"));
    }

    private void LightMode_Click(object sender, RoutedEventArgs e)
    {
        ApplyWithTransition(() => App.Theme.SetMode("light"));
    }

    // 统一走遮罩过渡，等动画结束后再刷新卡片，避免卡片提前变色
    private void ApplyWithTransition(Action apply)
    {
        void Refresh()
        {
            App.Settings.Save();
            BuildUiStyleButtons();
            BuildThemeButtons();
        }

        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.RunDiagonalThemeTransition(apply, Refresh);
        else
        {
            apply();
            Refresh();
        }
    }
}
