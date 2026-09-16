using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuartzLauncher.Services;

public static class EasterEggService
{
    public static bool IsActive { get; private set; }

    private sealed class ButtonState
    {
        public ButtonBase Control = null!;
        public Brush? BorderBrush;
        public double BorderThickness;
        public Border? TemplateBorder;
        public Brush? TemplateBorderBrush;
        public double TemplateBorderThickness;
    }

    private static readonly List<ButtonState> _saved = new();
    private static DispatcherTimer? _rotateTimer;
    private static double _fastAngle;
    private static double _slowAngle;
    private static LinearGradientBrush? _fastBrush;
    private static LinearGradientBrush? _slowBrush;

    private static DispatcherTimer? _heartbeatTimer;
    private static double _heartbeatPhase;
    private static ScaleTransform? _heartbeatScale;
    private static Transform? _savedRootTransform;
    private static Point _savedRootOrigin;
    private static FrameworkElement? _heartbeatTarget;

    public static LinearGradientBrush FastBrush
    {
        get
        {
            _fastBrush ??= CreateRainbowBrush();
            EnsureRotationTimer();
            return _fastBrush;
        }
    }

    private static LinearGradientBrush SlowBrush
    {
        get
        {
            _slowBrush ??= CreateRainbowBrush();
            EnsureRotationTimer();
            return _slowBrush;
        }
    }

    public static LinearGradientBrush CreateRainbowBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            SpreadMethod = GradientSpreadMethod.Pad,
        };
        var colors = new[]
        {
            Color.FromRgb(0xFF, 0x00, 0x00),
            Color.FromRgb(0xFF, 0x7F, 0x00),
            Color.FromRgb(0xFF, 0xFF, 0x00),
            Color.FromRgb(0x00, 0xFF, 0x00),
            Color.FromRgb(0x00, 0xFF, 0xFF),
            Color.FromRgb(0x00, 0x00, 0xFF),
            Color.FromRgb(0x8B, 0x00, 0xFF),
        };
        for (var i = 0; i < colors.Length; i++)
            brush.GradientStops.Add(new GradientStop(colors[i], (double)i / (colors.Length - 1)));
        return brush;
    }

    private static void EnsureRotationTimer()
    {
        if (_rotateTimer != null) return;
        _rotateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _rotateTimer.Tick += (_, _) =>
        {
            _fastAngle = (_fastAngle + 1.2) % 360;
            _slowAngle = (_slowAngle + 1.2 * 0.35) % 360;
            if (_fastBrush != null)
                _fastBrush.RelativeTransform = new RotateTransform(_fastAngle, 0.5, 0.5);
            if (_slowBrush != null)
                _slowBrush.RelativeTransform = new RotateTransform(_slowAngle, 0.5, 0.5);
        };
        _rotateTimer.Start();
    }

    private static void StopRotationIfIdle()
    {
        if (_fastBrush == null && _slowBrush == null && _rotateTimer != null)
        {
            _rotateTimer.Stop();
            _rotateTimer = null;
        }
    }

    public static void ReleaseFast()
    {
        _fastBrush = null;
        StopRotationIfIdle();
    }

    public static void Activate(FrameworkElement heartbeatTarget, FrameworkElement? exclude)
    {
        if (IsActive) return;
        IsActive = true;

        var window = Application.Current?.MainWindow;
        if (window != null)
            ApplyRainbowToTree(window, SlowBrush, exclude);

        EnsureRotationTimer();

        if (heartbeatTarget != null)
        {
            _savedRootTransform = heartbeatTarget.RenderTransform;
            _savedRootOrigin = heartbeatTarget.RenderTransformOrigin;
            _heartbeatScale = new ScaleTransform(1, 1);
            heartbeatTarget.RenderTransformOrigin = new Point(0.5, 0.5);
            heartbeatTarget.RenderTransform = _heartbeatScale;
            _heartbeatTarget = heartbeatTarget;
            _heartbeatPhase = 0;
            _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _heartbeatTimer.Tick += (_, _) =>
            {
                _heartbeatPhase += 0.12;
                var s = 1 + 0.02 * Math.Sin(_heartbeatPhase);
                if (_heartbeatScale == null) return;
                _heartbeatScale.ScaleX = s;
                _heartbeatScale.ScaleY = s;
            };
            _heartbeatTimer.Start();
        }
    }

    public static void Deactivate(FrameworkElement heartbeatTarget)
    {
        IsActive = false;

        _heartbeatTimer?.Stop();
        _heartbeatTimer = null;
        if (heartbeatTarget != null && _savedRootTransform != null)
        {
            heartbeatTarget.RenderTransform = _savedRootTransform;
            heartbeatTarget.RenderTransformOrigin = _savedRootOrigin;
        }
        _heartbeatTarget = null;
        _heartbeatScale = null;

        foreach (var s in _saved)
        {
            try
            {
                s.Control.BorderBrush = s.BorderBrush;
                s.Control.BorderThickness = new Thickness(s.BorderThickness);
                if (s.TemplateBorder != null)
                {
                    s.TemplateBorder.BorderBrush = s.TemplateBorderBrush;
                    s.TemplateBorder.BorderThickness = new Thickness(s.TemplateBorderThickness);
                }
            }
            catch
            {
            }
        }
        _saved.Clear();

        _slowBrush = null;
        StopRotationIfIdle();
    }

    private static void ApplyRainbowToTree(DependencyObject root, Brush brush, FrameworkElement? exclude)
    {
        var buttons = new List<ButtonBase>();
        CollectButtons(root, buttons);
        foreach (var control in buttons)
        {
            if (exclude != null && ReferenceEquals(control, exclude)) continue;
            try
            {
                var state = new ButtonState
                {
                    Control = control,
                    BorderBrush = control.BorderBrush,
                    BorderThickness = control.BorderThickness.Left,
                };
                var templateBorder = FindTemplateBorder(control);
                if (templateBorder != null)
                {
                    state.TemplateBorder = templateBorder;
                    state.TemplateBorderBrush = templateBorder.BorderBrush;
                    state.TemplateBorderThickness = templateBorder.BorderThickness.Left;
                    templateBorder.BorderBrush = brush;
                    templateBorder.BorderThickness = new Thickness(Math.Max(1, templateBorder.BorderThickness.Left));
                }
                control.BorderBrush = brush;
                control.BorderThickness = new Thickness(Math.Max(1, control.BorderThickness.Left));
                _saved.Add(state);
            }
            catch
            {
            }
        }
    }

    private static Border? FindTemplateBorder(ButtonBase control)
    {
        if (control.Template == null) return null;
        foreach (var name in new[] { "bg", "border", "hoverBg" })
        {
            try
            {
                if (control.Template.FindName(name, control) is Border border) return border;
            }
            catch
            {
            }
        }
        return null;
    }

    private static void CollectButtons(DependencyObject current, List<ButtonBase> list)
    {
        var count = VisualTreeHelper.GetChildrenCount(current);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(current, i);
            if (child is ButtonBase button) list.Add(button);
            CollectButtons(child, list);
        }
    }
}
