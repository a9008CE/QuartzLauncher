using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace QuartzLauncher.Services;

public static class HoverAnimationBehavior
{
    public static readonly DependencyProperty HoverScaleProperty =
        DependencyProperty.RegisterAttached("HoverScale", typeof(double), typeof(HoverAnimationBehavior),
            new PropertyMetadata(0.0, OnHoverScaleChanged));

    public static readonly DependencyProperty HoverShiftProperty =
        DependencyProperty.RegisterAttached("HoverShift", typeof(double), typeof(HoverAnimationBehavior),
            new PropertyMetadata(0.0, OnHoverShiftChanged));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.RegisterAttached("HoverColor", typeof(string), typeof(HoverAnimationBehavior),
            new PropertyMetadata(null, OnHoverColorChanged));

    public static void SetHoverScale(DependencyObject obj, double value) => obj.SetValue(HoverScaleProperty, value);
    public static double GetHoverScale(DependencyObject obj) => (double)obj.GetValue(HoverScaleProperty);

    public static void SetHoverShift(DependencyObject obj, double value) => obj.SetValue(HoverShiftProperty, value);
    public static double GetHoverShift(DependencyObject obj) => (double)obj.GetValue(HoverShiftProperty);

    public static void SetHoverColor(DependencyObject obj, string? value) => obj.SetValue(HoverColorProperty, value);
    public static string? GetHoverColor(DependencyObject obj) => (string?)obj.GetValue(HoverColorProperty);

    private static void OnHoverScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe || e.NewValue is not double scale || scale <= 0) return;

        var transform = new ScaleTransform(1, 1);
        fe.RenderTransform = transform;
        fe.RenderTransformOrigin = new Point(0.5, 0.5);

        var enterAnim = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(150))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var exitAnim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

        fe.MouseEnter += (_, _) => transform.BeginAnimation(ScaleTransform.ScaleXProperty, enterAnim);
        fe.MouseLeave += (_, _) => transform.BeginAnimation(ScaleTransform.ScaleXProperty, exitAnim);
        fe.MouseEnter += (_, _) => transform.BeginAnimation(ScaleTransform.ScaleYProperty, enterAnim);
        fe.MouseLeave += (_, _) => transform.BeginAnimation(ScaleTransform.ScaleYProperty, exitAnim);
    }

    private static void OnHoverShiftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe || e.NewValue is not double shift || shift <= 0) return;

        var transform = new TranslateTransform(0, 0);
        fe.RenderTransform = transform;

        var enterAnim = new DoubleAnimation(shift, TimeSpan.FromMilliseconds(120))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var exitAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

        fe.MouseEnter += (_, _) => transform.BeginAnimation(TranslateTransform.XProperty, enterAnim);
        fe.MouseLeave += (_, _) => transform.BeginAnimation(TranslateTransform.XProperty, exitAnim);
    }

    private static void OnHoverColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border border || e.NewValue is not string hex || string.IsNullOrEmpty(hex)) return;

        var brush = border.Background as SolidColorBrush;
        if (brush == null) return;

        var localBrush = brush.CloneCurrentValue();
        border.Background = localBrush;
        var originalColor = localBrush.Color;
        var hoverColor = (Color)ColorConverter.ConvertFromString(hex);

        var enterAnim = new ColorAnimation(hoverColor, TimeSpan.FromMilliseconds(120));
        var exitAnim = new ColorAnimation(originalColor, TimeSpan.FromMilliseconds(180));

        border.MouseEnter += (_, _) =>
        {
            localBrush.BeginAnimation(SolidColorBrush.ColorProperty, enterAnim);
        };
        border.MouseLeave += (_, _) =>
        {
            localBrush.BeginAnimation(SolidColorBrush.ColorProperty, exitAnim);
        };
    }
}
