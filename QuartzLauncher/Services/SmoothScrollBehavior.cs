using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QuartzLauncher.Services;

public static class SmoothScrollBehavior
{
    private static readonly HashSet<ScrollViewer> ActiveViewers = new();
    private const double DefaultStepPixels = 32d;
    private const double DefaultLerpFactor = 0.12;
    private const double SnapThreshold = 0.3;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty StepPixelsProperty =
        DependencyProperty.RegisterAttached(
            "StepPixels", typeof(double), typeof(SmoothScrollBehavior),
            new PropertyMetadata(DefaultStepPixels));

    public static readonly DependencyProperty LerpFactorProperty =
        DependencyProperty.RegisterAttached(
            "LerpFactor", typeof(double), typeof(SmoothScrollBehavior),
            new PropertyMetadata(DefaultLerpFactor));

    private static readonly DependencyProperty TargetOffsetProperty =
        DependencyProperty.RegisterAttached(
            "TargetOffset", typeof(double), typeof(SmoothScrollBehavior),
            new PropertyMetadata(0d));

    private static readonly DependencyProperty RenderingProperty =
        DependencyProperty.RegisterAttached(
            "Rendering", typeof(bool), typeof(SmoothScrollBehavior),
            new PropertyMetadata(false));

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    public static void SetStepPixels(DependencyObject element, double value)
        => element.SetValue(StepPixelsProperty, value);

    public static double GetStepPixels(DependencyObject element)
        => (double)element.GetValue(StepPixelsProperty);

    public static void SetLerpFactor(DependencyObject element, double value)
        => element.SetValue(LerpFactorProperty, value);

    public static double GetLerpFactor(DependencyObject element)
        => (double)element.GetValue(LerpFactorProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ScrollViewer viewer) return;
        if ((bool)e.NewValue)
            viewer.PreviewMouseWheel += Viewer_PreviewMouseWheel;
        else
        {
            viewer.PreviewMouseWheel -= Viewer_PreviewMouseWheel;
            StopRendering(viewer);
        }
    }

    private static void Viewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer || viewer.ScrollableHeight <= 0) return;

        var target = viewer.GetValue(TargetOffsetProperty) is double current
            ? current
            : viewer.VerticalOffset;
        var step = Math.Max(1d, GetStepPixels(viewer));
        target = Math.Clamp(target - e.Delta / 120d * step, 0, viewer.ScrollableHeight);
        viewer.SetValue(TargetOffsetProperty, target);
        StartRendering(viewer);
        e.Handled = true;
    }

    private static void StartRendering(ScrollViewer viewer)
    {
        if ((bool)viewer.GetValue(RenderingProperty)) return;
        viewer.SetValue(RenderingProperty, true);
        ActiveViewers.Add(viewer);
        CompositionTarget.Rendering += Viewer_Rendering;
    }

    private static void StopRendering(ScrollViewer viewer)
    {
        if (!(bool)viewer.GetValue(RenderingProperty)) return;
        viewer.SetValue(RenderingProperty, false);
        ActiveViewers.Remove(viewer);
        if (ActiveViewers.Count == 0)
            CompositionTarget.Rendering -= Viewer_Rendering;
    }

    private static void Viewer_Rendering(object? sender, EventArgs e)
    {
        foreach (var viewer in ActiveViewers.ToArray())
        {
            if (!viewer.IsVisible || viewer.ScrollableHeight <= 0)
            {
                StopRendering(viewer);
                continue;
            }

            var target = (double)viewer.GetValue(TargetOffsetProperty);
            var distance = target - viewer.VerticalOffset;
            if (Math.Abs(distance) < SnapThreshold)
            {
                viewer.ScrollToVerticalOffset(target);
                StopRendering(viewer);
                continue;
            }

            var lerp = Math.Clamp(GetLerpFactor(viewer), 0.02, 0.5);
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset + distance * lerp);
        }
    }
}
