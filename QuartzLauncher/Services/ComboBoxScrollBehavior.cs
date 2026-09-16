using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace QuartzLauncher.Services;

public static class ComboBoxScrollBehavior
{
    private static readonly HashSet<ComboBox> Hooked = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(ComboBoxScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox cb) return;
        if ((bool)e.NewValue)
        {
            if (Hooked.Add(cb))
                cb.PreviewMouseWheel += Cb_PreviewMouseWheel;
        }
        else
        {
            if (Hooked.Remove(cb))
                cb.PreviewMouseWheel -= Cb_PreviewMouseWheel;
        }
    }

    private static void Cb_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        var popup = FindChild<Popup>(cb);
        if (popup is null || !popup.IsOpen) return;

        var sv = FindChild<ScrollViewer>(popup);
        if (sv is null) return;

        var step = e.Delta > 0 ? 36 : -36;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - step);
        e.Handled = true;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindChild<T>(child);
            if (result is not null) return result;
        }
        return null;
    }
}
