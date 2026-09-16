using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace QuartzLauncher.Services;

public static class NavAlignmentAnimationBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(NavAlignmentAnimationBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly ConditionalWeakTable<FrameworkElement, AnimationState> States = new();

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element) return;

        if (e.NewValue is true)
        {
            element.Loaded += Element_Loaded;
            element.Unloaded += Element_Unloaded;
            if (element.IsLoaded) Attach(element);
        }
        else
        {
            element.Loaded -= Element_Loaded;
            element.Unloaded -= Element_Unloaded;
            Detach(element);
        }
    }

    private static void Element_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element) Attach(element);
    }

    private static void Element_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element) Detach(element);
    }

    private static void Attach(FrameworkElement element)
    {
        if (States.TryGetValue(element, out _)) return;

        var state = new AnimationState(element);
        States.Add(element, state);
        state.Attach();
    }

    private static void Detach(FrameworkElement element)
    {
        if (!States.TryGetValue(element, out var state)) return;
        state.Detach();
        States.Remove(element);
    }

    private sealed class AnimationState
    {
        private readonly FrameworkElement _element;
        private readonly DependencyPropertyDescriptor? _alignmentDescriptor;
        private TranslateTransform? _transform;
        private HorizontalAlignment _lastAlignment;
        private bool _ownsTransform;
        private int _animationVersion;

        public AnimationState(FrameworkElement element)
        {
            _element = element;
            _lastAlignment = element.HorizontalAlignment;
            _alignmentDescriptor = DependencyPropertyDescriptor.FromProperty(
                FrameworkElement.HorizontalAlignmentProperty,
                typeof(FrameworkElement));
        }

        public void Attach()
            => _alignmentDescriptor?.AddValueChanged(_element, AlignmentChanged);

        public void Detach()
        {
            _alignmentDescriptor?.RemoveValueChanged(_element, AlignmentChanged);
            _transform?.BeginAnimation(TranslateTransform.XProperty, null);
        }

        private void AlignmentChanged(object? sender, EventArgs e)
        {
            var nextAlignment = _element.HorizontalAlignment;
            var previousAlignment = _lastAlignment;
            _lastAlignment = nextAlignment;

            if ((previousAlignment, nextAlignment) is not
                (HorizontalAlignment.Left, HorizontalAlignment.Center) and not
                (HorizontalAlignment.Center, HorizontalAlignment.Left))
                return;

            var centerOffset = GetCenterOffset();
            if (centerOffset <= 0) return;

            var layoutDelta = previousAlignment == HorizontalAlignment.Left
                ? centerOffset
                : -centerOffset;

            if (_element.RenderTransform is null)
            {
                _transform = new TranslateTransform();
                _element.RenderTransform = _transform;
                _ownsTransform = true;
            }
            else if (_element.RenderTransform is TranslateTransform existing)
            {
                _transform = existing;
            }
            else
            {
                return;
            }

            _transform.BeginAnimation(TranslateTransform.XProperty, null);
            _transform.X = -layoutDelta;
            var animationVersion = ++_animationVersion;

            var animation = new DoubleAnimation(-layoutDelta, 0, TimeSpan.FromMilliseconds(340))
            {
                EasingFunction = new BackEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Amplitude = 0.28
                }
            };
            animation.Completed += (_, _) =>
            {
                if (_transform == null || animationVersion != _animationVersion) return;
                _transform.BeginAnimation(TranslateTransform.XProperty, null);
                _transform.X = 0;
                if (_ownsTransform)
                {
                    _element.RenderTransform = null;
                    _transform = null;
                    _ownsTransform = false;
                }
            };
            _transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private double GetCenterOffset()
        {
            if (VisualTreeHelper.GetParent(_element) is not FrameworkElement parent)
                return 0;

            var availableWidth = parent.ActualWidth;
            if (parent is Border border)
            {
                availableWidth -= border.Padding.Left + border.Padding.Right
                    + border.BorderThickness.Left + border.BorderThickness.Right;
            }

            availableWidth -= _element.Margin.Left + _element.Margin.Right;
            return Math.Max(0, (availableWidth - _element.ActualWidth) / 2);
        }
    }
}
