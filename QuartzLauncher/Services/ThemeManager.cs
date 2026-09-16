using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public class ThemeManager
{
    private readonly SettingsService _settings;
    private static readonly string LogFile = Path.Combine(
        AppContext.BaseDirectory, "Launcher", "crash.log");
    public Theme Current { get; private set; }

    public ThemeManager(SettingsService settings)
    {
        _settings = settings;
        Current = ResolveTheme();
    }

    private Theme ResolveTheme()
    {
        var s = _settings.Data;
        var presets = Theme.GetPresets(s.UiStyle, s.ThemeMode);
        var theme = presets.TryGetValue(s.ThemeName, out var t)
            ? t
            : presets.TryGetValue("默认", out var defaultTheme)
                ? defaultTheme
                : presets.Values.FirstOrDefault() ?? new Theme();

        if (!string.IsNullOrEmpty(s.ThemePrimary)) theme.Primary = s.ThemePrimary;
        if (!string.IsNullOrEmpty(s.ThemeBg)) theme.Bg = s.ThemeBg;
        if (!string.IsNullOrEmpty(s.ThemeCard)) theme.Card = s.ThemeCard;
        if (!string.IsNullOrEmpty(s.ThemeText)) theme.Text = s.ThemeText;
        if (!string.IsNullOrEmpty(s.ThemeTextMuted)) theme.TextMuted = s.ThemeTextMuted;
        if (!string.IsNullOrEmpty(s.ThemeSidebar)) theme.Sidebar = s.ThemeSidebar;
        if (!string.IsNullOrEmpty(s.ThemeBorder)) theme.Border = s.ThemeBorder;
        if (!string.IsNullOrEmpty(s.ThemeDanger)) theme.Danger = s.ThemeDanger;

        return theme;
    }

    private Theme? _baseTheme;
    private double _hueShift;

    private ResourceDictionary? _colorDict;
    private bool _stylesLoaded;
    private bool _hasAppliedOnce;

    /// <summary>
    /// 为 true 时跳过 Apply() 内置的窗口淡出淡入动画，
    /// 供 MainWindow 的遮罩过渡（斜切/涟漪/淡入）独占控制，避免两套动画叠加冲突。
    /// </summary>
    public bool SuppressTransition { get; set; }

    public void Apply()
    {
        Current = ResolveTheme();
        _baseTheme ??= Current;
        var t = Current;
        if (_hueShift != 0 && _baseTheme != null)
            t = ShiftHue(_baseTheme, _hueShift);
        var app = Application.Current;
        if (app == null) return;

        var rd = new ResourceDictionary();
        var isFrosted = _settings.Data.UiStyle == "frosted";
        var isCyberpunk = _settings.Data.UiStyle == "cyberpunk";
        var isWanderingEarth = _settings.Data.UiStyle == "wanderingearth";
        var isLight = _settings.Data.ThemeMode == "light";

        rd.Add("BgColor", ToColor(t.Bg));
        rd.Add("CardColor", ToColor(t.Card));
        rd.Add("SidebarColor", ToColor(t.Sidebar));
        rd.Add("BorderColor", ToColor(t.Border));
        rd.Add("TextColor", ToColor(t.Text));
        rd.Add("TextMutedColor", ToColor(t.TextMuted));
        rd.Add("PrimaryColor", ToColor(t.Primary));
        rd.Add("DangerColor", ToColor(t.Danger));
        rd.Add("SuccessColor", ToColor(t.Success));
        var inputBg = isFrosted
            ? WithAlpha(Mix(t.Bg, isLight ? "#FFFFFF" : "#000000", isLight ? 0.18 : 0.12), (byte)(isLight ? 76 : 72))
            : isCyberpunk
                ? Mix(t.Card, t.Primary, isLight ? 0.06 : 0.1)
                : isWanderingEarth
                    ? Mix(t.Card, t.Primary, isLight ? 0.08 : 0.12)
                : Darken(t.Bg, 5);
        rd.Add("InputBgColor", ToColor(inputBg));

        rd.Add("BgBrush", isFrosted
            ? CreateBackdropBrush(t.Bg, t.Primary, isLight)
            : isCyberpunk ? CreateCyberpunkBackdropBrush(t.Bg, t.Primary, isLight)
            : isWanderingEarth ? CreateWanderingEarthBackdropBrush(t.Bg, t.Primary, isLight)
            : ToBrush(t.Bg));
        rd.Add("WindowBackgroundBrush", isFrosted
            ? CreateBackdropBrush(t.Bg, t.Primary, isLight)
            : isCyberpunk ? CreateCyberpunkBackdropBrush(t.Bg, t.Primary, isLight)
            : isWanderingEarth ? CreateWanderingEarthBackdropBrush(t.Bg, t.Primary, isLight)
            : ToBrush(t.Bg));
        rd.Add("ContentBackgroundBrush", isFrosted
            ? ToBrush(WithAlpha(isLight ? "#FFFFFF" : "#071326", (byte)(isLight ? 10 : 16)))
            : isCyberpunk ? ToBrush(Mix(t.Bg, t.Card, isLight ? 0.18 : 0.08))
            : isWanderingEarth ? ToBrush(Mix(t.Bg, t.Card, isLight ? 0.12 : 0.06))
            : ToBrush(t.Bg));
        rd.Add("CardBrush", isFrosted
            ? ToBrush(WithAlpha(t.Card, (byte)(isLight ? 64 : 72)))
            : ToBrush(t.Card));
        rd.Add("DialogCardBrush", isFrosted
            ? ToBrush(WithAlpha(t.Card, (byte)(isLight ? 210 : 220)))
            : ToBrush(t.Card));
        rd.Add("CardSurfaceBrush", isFrosted
            ? CreateGlassBrush(t.Card, isLight)
            : isCyberpunk ? CreateCyberpunkCardBrush(t.Card, t.Primary, isLight)
            : isWanderingEarth ? CreateWanderingEarthCardBrush(t.Card, t.Primary, isLight)
            : ToBrush(t.Card));
        rd.Add("GlassControlBrush", isFrosted
            ? CreateControlGlassBrush(t.Card, t.Primary, isLight)
            : isCyberpunk ? CreateCyberpunkControlBrush(t.Card, t.Primary, isLight)
            : isWanderingEarth ? CreateWanderingEarthControlBrush(t.Card, t.Primary, isLight)
            : Brushes.Transparent);
        rd.Add("GlassControlHoverBrush", isFrosted
            ? CreateControlGlassBrush(t.Card, t.Primary, isLight, true)
            : isCyberpunk ? CreateCyberpunkControlBrush(t.Card, t.Primary, isLight, true)
            : isWanderingEarth ? CreateWanderingEarthControlBrush(t.Card, t.Primary, isLight, true)
            : ToBrush(Lighten(t.Bg, 5)));
        rd.Add("GlassControlBorderBrush", isFrosted
            ? ToBrush(WithAlpha(t.Border, (byte)(isLight ? 190 : 178)))
            : isCyberpunk ? CreateCyberpunkBorderBrush(t.Border, t.Primary, isLight)
            : isWanderingEarth ? CreateWanderingEarthBorderBrush(t.Border, t.Primary, isLight)
            : ToBrush(t.Border));
        rd.Add("GlassPrimaryBrush", isFrosted
            ? CreateAccentGlassBrush(t.Primary, isLight)
            : isCyberpunk ? CreateCyberpunkAccentBrush(t.Primary, isLight)
            : isWanderingEarth ? CreateWanderingEarthAccentBrush(t.Primary, isLight)
            : ToBrush(t.Primary));
        rd.Add("GlassPrimaryHoverBrush", isFrosted
            ? CreateAccentGlassBrush(t.Primary, isLight, true)
            : isCyberpunk ? CreateCyberpunkAccentBrush(t.Primary, isLight, true)
            : isWanderingEarth ? CreateWanderingEarthAccentBrush(t.Primary, isLight, true)
            : ToBrush(Lighten(t.Primary, 8)));
        rd.Add("GlassPrimaryBorderBrush", isFrosted
            ? ToBrush(WithAlpha(t.Primary, (byte)(isLight ? 190 : 178)))
            : ToBrush(t.Primary));
        rd.Add("GlassDangerBrush", isFrosted
            ? CreateAccentGlassBrush(t.Danger, isLight)
            : isCyberpunk ? CreateCyberpunkAccentBrush(t.Danger, isLight)
            : isWanderingEarth ? CreateWanderingEarthAccentBrush(t.Danger, isLight)
            : Brushes.Transparent);
        rd.Add("GlassDangerHoverBrush", isFrosted
            ? CreateAccentGlassBrush(t.Danger, isLight, true)
            : isCyberpunk ? CreateCyberpunkAccentBrush(t.Danger, isLight, true)
            : isWanderingEarth ? CreateWanderingEarthAccentBrush(t.Danger, isLight, true)
            : ToBrush(Lighten(t.Danger, 8)));
        rd.Add("GlassDangerBorderBrush", isFrosted
            ? ToBrush(WithAlpha(t.Danger, (byte)(isLight ? 190 : 178)))
            : ToBrush(t.Danger));
        rd.Add("GlassBorderBrush", isFrosted
            ? CreateGlassBorderBrush(t.Border, isLight)
            : isCyberpunk ? CreateCyberpunkBorderBrush(t.Border, t.Primary, isLight)
            : isWanderingEarth ? CreateWanderingEarthBorderBrush(t.Border, t.Primary, isLight)
            : ToBrush(t.Border));
        rd.Add("SidebarBrush", isFrosted
            ? ToBrush(WithAlpha(t.Sidebar, (byte)(isLight ? 72 : 86)))
            : ToBrush(t.Sidebar));
        rd.Add("BorderBrush", isFrosted
            ? ToBrush(WithAlpha(t.Border, (byte)(isLight ? 180 : 168)))
            : ToBrush(t.Border));
        rd.Add("TextBrush", ToBrush(t.Text));
        rd.Add("TextMutedBrush", ToBrush(t.TextMuted));
        rd.Add("PrimaryBrush", ToBrush(t.Primary));
        rd.Add("DangerBrush", ToBrush(t.Danger));
        rd.Add("SuccessBrush", ToBrush(t.Success));
        rd.Add("PrimaryButtonForeground", (isCyberpunk || isWanderingEarth) && isLight ? ToBrush(t.Text) : Brushes.White);
        rd.Add("DangerButtonForeground", (isCyberpunk || isWanderingEarth) && isLight
            ? ToBrush(t.Text)
            : isCyberpunk ? Brushes.White : ToBrush(t.Danger));
        rd.Add("InputBgBrush", isFrosted
            ? ToBrush(WithAlpha(isLight ? "#FFFFFF" : "#142131", (byte)(isLight ? 218 : 224)))
            : isCyberpunk ? CreateCyberpunkControlBrush(t.Card, t.Primary, isLight)
            : isWanderingEarth ? CreateWanderingEarthControlBrush(t.Card, t.Primary, isLight)
            : ToBrush(inputBg));
        rd.Add("GlassHighlightBrush", ToBrush(WithAlpha("#FFFFFF", (byte)(isLight ? 190 : 94))));
        rd.Add("UiFontFamily", isCyberpunk || isWanderingEarth ? new FontFamily("Bahnschrift") : new FontFamily("Segoe UI"));
        rd.Add("UiDisplayFontFamily", isCyberpunk || isWanderingEarth ? new FontFamily("Bahnschrift SemiBold") : new FontFamily("Segoe UI"));
        rd.Add("BrandText", "星落LaunCher");
        rd.Add("CardPadding", isCyberpunk || isWanderingEarth ? new Thickness(16) : new Thickness(20));
        rd.Add("PageTitleFontSize", isCyberpunk || isWanderingEarth ? 26d : 28d);
        rd.Add("SidebarWidth", new GridLength(isCyberpunk ? 224 : isWanderingEarth ? 232 : t.SidebarWidth));
        rd.Add("TopBarHeight", isCyberpunk ? 46d : isWanderingEarth ? 44d : 38d);
        rd.Add("BottomBarHeight", isCyberpunk || isWanderingEarth ? 52d : 60d);
        rd.Add("WanderingEarthHudHeight", isWanderingEarth ? new GridLength(72) : new GridLength(0));
        rd.Add("CyberpunkVisibility", isCyberpunk ? Visibility.Visible : Visibility.Collapsed);
        rd.Add("WanderingEarthVisibility", isWanderingEarth ? Visibility.Visible : Visibility.Collapsed);
        rd.Add("WindowAccentBorderBrush", isCyberpunk || isWanderingEarth ? ToBrush(t.Primary) : ToBrush(t.Border));
        rd.Add("CyberpunkHudBrush", isCyberpunk ? ToBrush(WithAlpha(t.Primary, (byte)(isLight ? 150 : 190))) : Brushes.Transparent);
        rd.Add("WanderingEarthHudBrush", isWanderingEarth ? ToBrush(WithAlpha(t.Primary, (byte)(isLight ? 150 : 190))) : Brushes.Transparent);

        rd.Add("ReadablePanelBrush", isFrosted
            ? ToBrush(WithAlpha(isLight ? "#F8FBFF" : "#182536", (byte)(isLight ? 232 : 226)))
            : ToBrush(t.Card));
        rd.Add("ReadableControlBrush", isFrosted
            ? ToBrush(WithAlpha(isLight ? "#FFFFFF" : "#142131", (byte)(isLight ? 218 : 224)))
            : ToBrush(inputBg));
        rd.Add("ReadablePopupBrush", isFrosted
            ? ToBrush(WithAlpha(isLight ? "#F9FCFF" : "#172536", (byte)(isLight ? 246 : 242)))
            : ToBrush(t.Card));
        rd.Add("ReadableItemBrush", isFrosted
            ? ToBrush(WithAlpha(isLight ? "#E5EFFA" : "#26394C", (byte)(isLight ? 232 : 236)))
            : ToBrush(inputBg));

        var navActiveBg = isCyberpunk ? Mix(t.Sidebar, t.Primary, 0.24) : Mix(t.Sidebar, t.Primary, 0.16);
        rd.Add("NavActiveBgBrush", ToBrush(navActiveBg));
        rd.Add("NavActiveBgColor", ToColor(navActiveBg));
        rd.Add("NavActiveBgBrushColor", ToColor(navActiveBg));
        rd.Add("NavIconBrush", ToBrush(t.TextMuted));
        rd.Add("NavTextAlignment", _settings.Data.SidebarTextLeftAligned
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Center);

        rd.Add("InputBgBrushColor", ToColor(inputBg));

        rd.Add("CardCornerRadius", new CornerRadius(isCyberpunk ? Math.Max(0, t.CardCornerRadius) : Math.Max(8, t.CardCornerRadius)));
        rd.Add("ButtonCornerRadius", new CornerRadius(isCyberpunk ? Math.Max(0, t.ButtonCornerRadius) : Math.Max(6, t.ButtonCornerRadius)));
        rd.Add("InputCornerRadius", new CornerRadius(isCyberpunk ? Math.Max(0, t.InputCornerRadius) : Math.Max(6, t.InputCornerRadius)));
        rd.Add("NavButtonHeight", t.NavButtonHeight);
        rd.Add("CardShadowDepth", t.CardShadowDepth);
        rd.Add("CardShadowBlur", t.CardShadowBlur);
        rd.Add("CardShadowOpacity", t.CardShadowOpacity);
        rd.Add("CardShadowColor", ToColor(t.CardShadowColor));
        rd.Add("CardBorderThickness", new Thickness(t.CardBorderThickness));
        if (_colorDict != null)
            app.Resources.MergedDictionaries.Remove(_colorDict);

        _colorDict = rd;
        app.Resources.MergedDictionaries.Insert(0, rd);

        if (!_stylesLoaded)
        {
            _stylesLoaded = true;
            try
            {
                var dict = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Themes/Styles.xaml")
                };
                app.Resources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(LogFile, $"[{DateTime.Now}] Styles load: {ex.Message}\n\n"); } catch { }
            }
        }

        if (_hasAppliedOnce && !SuppressTransition && app.MainWindow is { Content: FrameworkElement root })
        {
            var hasOpenDialogs = app.Windows.Count > 1;
            if (!hasOpenDialogs)
            {
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250));
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(250));
                fadeIn.BeginTime = TimeSpan.FromMilliseconds(250);
                fadeOut.Completed += (_, _) => root.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                root.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }
        _hasAppliedOnce = true;

    }

    public void SelectPreset(string name)
    {
        _settings.Data.ThemeName = name;
        _settings.Data.ThemePrimary = "";
        _settings.Data.ThemeBg = "";
        _settings.Data.ThemeCard = "";
        _settings.Data.ThemeText = "";
        _settings.Data.ThemeTextMuted = "";
        _settings.Data.ThemeSidebar = "";
        _settings.Data.ThemeBorder = "";
        _settings.Data.ThemeDanger = "";
        _settings.Data.ThemeBackgroundImage = "";
        Apply();
    }

    public void SetMode(string mode)
    {
        _settings.Data.ThemeMode = mode;
        Apply();
    }

    public void SetUiStyle(string style)
    {
        _settings.Data.UiStyle = style;
        var presets = Theme.GetPresets(style, _settings.Data.ThemeMode);
        _settings.Data.ThemeName = presets.Keys.FirstOrDefault() ?? "默认";
        _settings.Data.ThemePrimary = "";
        _settings.Data.ThemeBg = "";
        _settings.Data.ThemeCard = "";
        Apply();
    }

    private static Theme ShiftHue(Theme source, double degrees)
    {
        return new Theme
        {
            Name = source.Name,
            Primary = RotateHue(source.Primary, degrees),
            Bg = RotateHue(source.Bg, degrees),
            Card = RotateHue(source.Card, degrees),
            Text = RotateHue(source.Text, degrees),
            TextMuted = RotateHue(source.TextMuted, degrees),
            Sidebar = RotateHue(source.Sidebar, degrees),
            Border = RotateHue(source.Border, degrees),
            Danger = RotateHue(source.Danger, degrees),
            Success = RotateHue(source.Success, degrees),
            BackgroundImage = source.BackgroundImage,
            CardCornerRadius = source.CardCornerRadius,
            ButtonCornerRadius = source.ButtonCornerRadius,
            InputCornerRadius = source.InputCornerRadius,
            SidebarWidth = source.SidebarWidth,
            CardShadowDepth = source.CardShadowDepth,
            CardShadowBlur = source.CardShadowBlur,
            CardShadowOpacity = source.CardShadowOpacity,
            CardShadowColor = source.CardShadowColor,
            CardBorderThickness = source.CardBorderThickness,
            NavButtonHeight = source.NavButtonHeight,
        };
    }

    public static string RotateHue(string hex, double degrees)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var (h, s, l) = RgbToHsl(c);
            h = ((h + degrees) % 360 + 360) % 360;
            var (r, g, b) = HslToRgb(h, s, l);
            return $"#{(byte)Math.Round(r * 255):X2}{(byte)Math.Round(g * 255):X2}{(byte)Math.Round(b * 255):X2}";
        }
        catch
        {
            return hex;
        }
    }

    public void SetHueShift(double degrees)
    {
        _baseTheme = ResolveTheme();
        _hueShift = degrees;
        Apply();
    }

    public void ClearHueShift()
    {
        _hueShift = 0;
        _baseTheme = null;
        Apply();
    }

    private static (double h, double s, double l) RgbToHsl(Color c)
    {
        var r = c.R / 255.0;
        var g = c.G / 255.0;
        var b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        double h = 0, s = 0;
        var d = max - min;
        if (d != 0)
        {
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }
        return (h, s, l);
    }

    private static (double r, double g, double b) HslToRgb(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        var m = l - c / 2;
        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }
        return (r + m, g + m, b + m);
    }

    private static Color ToColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.Gray; }
    }

    private static Brush ToBrush(string hex)
    {
        try { return new SolidColorBrush(ToColor(hex)); }
        catch { return Brushes.Gray; }
    }

    private static Brush CreateGlassBrush(string baseHex, bool isLight)
    {
        var group = new DrawingGroup();
        var canvas = new RectangleGeometry(new Rect(0, 0, 100, 100));
        var baseBrush = new SolidColorBrush(ToColor(WithAlpha(baseHex, (byte)(isLight ? 28 : 40))));
        group.Children.Add(new GeometryDrawing(baseBrush, null, canvas));

        var tint = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        tint.GradientStops.Add(new GradientStop(ToColor(WithAlpha(isLight ? "#FFFFFF" : "#93B9FF", (byte)(isLight ? 30 : 24))), 0));
        tint.GradientStops.Add(new GradientStop(ToColor(WithAlpha(baseHex, (byte)(isLight ? 14 : 24))), 0.48));
        tint.GradientStops.Add(new GradientStop(ToColor(WithAlpha(Mix(baseHex, isLight ? "#69D9D2" : "#7A6EFF", 0.35), (byte)(isLight ? 28 : 36))), 1));
        group.Children.Add(new GeometryDrawing(tint, null, canvas));

        var reflection = new RectangleGeometry(new Rect(-18, -30, 34, 170))
        {
            Transform = new RotateTransform(-18, 50, 50)
        };
        var reflectionBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        reflectionBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
        reflectionBrush.GradientStops.Add(new GradientStop(ToColor(WithAlpha("#FFFFFF", (byte)(isLight ? 72 : 44))), 0.5));
        reflectionBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        group.Children.Add(new GeometryDrawing(reflectionBrush, null, reflection));

        var gloss = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        gloss.GradientStops.Add(new GradientStop(ToColor(WithAlpha("#FFFFFF", (byte)(isLight ? 72 : 44))), 0));
        gloss.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.18));
        gloss.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        group.Children.Add(new GeometryDrawing(gloss, null, canvas));

        return CreateDrawingBrush(group);
    }

    private static Brush CreateControlGlassBrush(string baseHex, string primaryHex, bool isLight, bool hover = false)
    {
        var alpha = hover ? (isLight ? 74 : 78) : (isLight ? 58 : 64);
        var color = Mix(baseHex, isLight ? "#FFFFFF" : "#FFFFFF", isLight ? 0.12 : 0.08);
        var brush = new SolidColorBrush(ToColor(WithAlpha(color, (byte)alpha)));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateAccentGlassBrush(string accentHex, bool isLight, bool hover = false)
    {
        var alpha = hover ? (isLight ? 136 : 148) : (isLight ? 112 : 124);
        var brush = new SolidColorBrush(ToColor(WithAlpha(accentHex, (byte)alpha)));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateCyberpunkControlBrush(string baseHex, string primaryHex, bool isLight, bool hover = false)
    {
        var ratio = hover ? 0.18 : 0.1;
        var color = Mix(baseHex, primaryHex, ratio);
        var brush = new SolidColorBrush(ToColor(color));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateCyberpunkAccentBrush(string accentHex, bool isLight, bool hover = false)
    {
        var color = hover ? Lighten(accentHex, 12) : accentHex;
        var brush = new SolidColorBrush(ToColor(color));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateCyberpunkBorderBrush(string baseHex, string primaryHex, bool isLight)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        brush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, primaryHex, isLight ? 0.38 : 0.24)), 0));
        brush.GradientStops.Add(new GradientStop(ToColor(baseHex), 0.5));
        brush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, primaryHex, isLight ? 0.22 : 0.16)), 1));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateCyberpunkCardBrush(string baseHex, string primaryHex, bool isLight)
    {
        var group = new DrawingGroup();
        var canvas = new RectangleGeometry(new Rect(0, 0, 100, 100));
        var baseBrush = new SolidColorBrush(ToColor(baseHex));
        group.Children.Add(new GeometryDrawing(baseBrush, null, canvas));

        var topLine = new RectangleGeometry(new Rect(0, 0, 100, 1));
        var topLineBrush = new SolidColorBrush(ToColor(WithAlpha(primaryHex, (byte)(isLight ? 150 : 190))));
        group.Children.Add(new GeometryDrawing(topLineBrush, null, topLine));

        var sideLine = new RectangleGeometry(new Rect(0, 0, 2, 100));
        var sideLineBrush = new SolidColorBrush(ToColor(WithAlpha(primaryHex, (byte)(isLight ? 100 : 135))));
        group.Children.Add(new GeometryDrawing(sideLineBrush, null, sideLine));

        var diagonal = new RectangleGeometry(new Rect(-18, -40, 12, 180))
        {
            Transform = new RotateTransform(26, 50, 50)
        };
        var diagonalBrush = new SolidColorBrush(ToColor(WithAlpha(primaryHex, (byte)(isLight ? 14 : 20))));
        group.Children.Add(new GeometryDrawing(diagonalBrush, null, diagonal));

        return CreateDrawingBrush(group);
    }

    private static Brush CreateCyberpunkBackdropBrush(string baseHex, string primaryHex, bool isLight)
    {
        var group = new DrawingGroup();
        var canvas = new RectangleGeometry(new Rect(0, 0, 100, 100));
        var baseBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        baseBrush.GradientStops.Add(new GradientStop(ToColor(baseHex), 0));
        baseBrush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, primaryHex, isLight ? 0.08 : 0.14)), 0.52));
        baseBrush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, isLight ? "#FFFFFF" : "#000000", isLight ? 0.08 : 0.12)), 1));
        group.Children.Add(new GeometryDrawing(baseBrush, null, canvas));

        var scanPen = new Pen(new SolidColorBrush(ToColor(WithAlpha(primaryHex, (byte)(isLight ? 28 : 42)))), 0.65);
        for (var y = 16; y < 100; y += 16)
            group.Children.Add(new GeometryDrawing(null, scanPen, new LineGeometry(new Point(0, y), new Point(100, y))));

        var diagonal = new RectangleGeometry(new Rect(-14, -32, 7, 180))
        {
            Transform = new RotateTransform(24, 50, 50)
        };
        var diagonalBrush = new SolidColorBrush(ToColor(WithAlpha(primaryHex, (byte)(isLight ? 30 : 38))));
        group.Children.Add(new GeometryDrawing(diagonalBrush, null, diagonal));

        return CreateDrawingBrush(group);
    }

    private static Brush CreateWanderingEarthControlBrush(string baseHex, string primaryHex, bool isLight, bool hover = false)
    {
        var color = Mix(baseHex, primaryHex, hover ? (isLight ? 0.16 : 0.2) : (isLight ? 0.08 : 0.11));
        var brush = new SolidColorBrush(ToColor(color));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateWanderingEarthAccentBrush(string accentHex, bool isLight, bool hover = false)
    {
        var color = hover ? Lighten(accentHex, 10) : accentHex;
        var brush = new SolidColorBrush(ToColor(color));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateWanderingEarthBorderBrush(string baseHex, string primaryHex, bool isLight)
    {
        var cool = isLight ? "#8BB8C8" : "#4F8296";
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        brush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, primaryHex, isLight ? 0.48 : 0.3)), 0));
        brush.GradientStops.Add(new GradientStop(ToColor(cool), 0.5));
        brush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, primaryHex, isLight ? 0.22 : 0.16)), 1));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateWanderingEarthCardBrush(string baseHex, string primaryHex, bool isLight)
    {
        var group = new DrawingGroup();
        var canvas = new RectangleGeometry(new Rect(0, 0, 100, 100));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(ToColor(baseHex)), null, canvas));

        var topLine = new RectangleGeometry(new Rect(0, 0, 100, 1));
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(ToColor(WithAlpha(primaryHex, (byte)(isLight ? 190 : 220)))), null, topLine));

        var bottomLine = new RectangleGeometry(new Rect(0, 99, 100, 1));
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(ToColor(WithAlpha(isLight ? "#6BAFC5" : "#4C9DB8", (byte)(isLight ? 130 : 170)))), null, bottomLine));

        var sideTick = new RectangleGeometry(new Rect(0, 0, 3, 18));
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(ToColor(WithAlpha(primaryHex, (byte)(isLight ? 180 : 210)))), null, sideTick));

        return CreateDrawingBrush(group);
    }

    private static Brush CreateWanderingEarthBackdropBrush(string baseHex, string primaryHex, bool isLight)
    {
        var group = new DrawingGroup();
        var canvas = new RectangleGeometry(new Rect(0, 0, 100, 100));
        var baseBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        baseBrush.GradientStops.Add(new GradientStop(ToColor(baseHex), 0));
        baseBrush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, primaryHex, isLight ? 0.12 : 0.18)), 0.5));
        baseBrush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, isLight ? "#F4F8F7" : "#02070B", isLight ? 0.16 : 0.18)), 1));
        group.Children.Add(new GeometryDrawing(baseBrush, null, canvas));

        var scanPen = new Pen(new SolidColorBrush(ToColor(WithAlpha(isLight ? "#648A95" : "#5B91A4", (byte)(isLight ? 28 : 40)))), 0.55);
        for (var y = 12; y < 100; y += 12)
            group.Children.Add(new GeometryDrawing(null, scanPen, new LineGeometry(new Point(0, y), new Point(100, y))));

        var diagonal = new RectangleGeometry(new Rect(-18, -28, 8, 170))
        {
            Transform = new RotateTransform(22, 50, 50)
        };
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(ToColor(WithAlpha(primaryHex, (byte)(isLight ? 22 : 30)))), null, diagonal));

        return CreateDrawingBrush(group);
    }

    private static Brush CreateDrawingBrush(DrawingGroup group)
    {
        group.Freeze();
        var brush = new DrawingBrush(group)
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.None,
            Viewbox = new Rect(0, 0, 100, 100),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 1, 1),
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox
        };
        brush.Freeze();
        return brush;
    }

    private static Brush CreateBackdropBrush(string baseHex, string primaryHex, bool isLight)
    {
        var group = new DrawingGroup();
        var canvas = new RectangleGeometry(new Rect(0, 0, 100, 100));
        var baseBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        baseBrush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, isLight ? "#C9E2FF" : "#102544", isLight ? 0.72 : 0.55)), 0));
        baseBrush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, primaryHex, isLight ? 0.4 : 0.48)), 0.26));
        baseBrush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, isLight ? "#E4D4FF" : "#402B61", isLight ? 0.62 : 0.52)), 0.52));
        baseBrush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, isLight ? "#C7EFE9" : "#14605C", isLight ? 0.6 : 0.5)), 0.77));
        baseBrush.GradientStops.Add(new GradientStop(ToColor(Mix(baseHex, isLight ? "#B9D5F2" : "#0D1E36", isLight ? 0.72 : 0.6)), 1));
        group.Children.Add(new GeometryDrawing(baseBrush, null, canvas));

        var lightBand = new RectangleGeometry(new Rect(-20, -25, 52, 160))
        {
            Transform = new RotateTransform(-24, 50, 50)
        };
        var lightBandBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        lightBandBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
        lightBandBrush.GradientStops.Add(new GradientStop(ToColor(WithAlpha("#FFFFFF", (byte)(isLight ? 28 : 16))), 0.5));
        lightBandBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        group.Children.Add(new GeometryDrawing(lightBandBrush, null, lightBand));

        var colorBand = new RectangleGeometry(new Rect(-28, -22, 42, 150))
        {
            Transform = new RotateTransform(32, 50, 50)
        };
        var colorBandBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        colorBandBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 110, 105, 255), 0));
        colorBandBrush.GradientStops.Add(new GradientStop(ToColor(WithAlpha(primaryHex, (byte)(isLight ? 26 : 34))), 0.5));
        colorBandBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 110, 105, 255), 1));
        group.Children.Add(new GeometryDrawing(colorBandBrush, null, colorBand));

        return CreateDrawingBrush(group);
    }

    private static Brush CreateGlassBorderBrush(string baseHex, bool isLight)
    {
        var highlight = WithAlpha("#FFFFFF", (byte)(isLight ? 185 : 86));
        var edge = WithAlpha(baseHex, (byte)(isLight ? 185 : 155));
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        brush.GradientStops.Add(new GradientStop(ToColor(highlight), 0));
        brush.GradientStops.Add(new GradientStop(ToColor(WithAlpha(baseHex, (byte)(isLight ? 116 : 132))), 0.48));
        brush.GradientStops.Add(new GradientStop(ToColor(edge), 1));
        brush.Freeze();
        return brush;
    }

    private static string WithAlpha(string hex, byte alpha)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return $"#{alpha:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        catch { return hex; }
    }


    public static string Darken(string hex, int amount)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return $"#{(byte)Math.Max(0, c.R - amount):X2}{(byte)Math.Max(0, c.G - amount):X2}{(byte)Math.Max(0, c.B - amount):X2}";
        }
        catch { return hex; }
    }

    public static string Lighten(string hex, int amount)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return $"#{(byte)Math.Min(255, c.R + amount):X2}{(byte)Math.Min(255, c.G + amount):X2}{(byte)Math.Min(255, c.B + amount):X2}";
        }
        catch { return hex; }
    }

    public static string Mix(string baseHex, string tintHex, double ratio)
    {
        try
        {
            var a = (Color)ColorConverter.ConvertFromString(baseHex);
            var b = (Color)ColorConverter.ConvertFromString(tintHex);
            var r = (byte)(a.R + (b.R - a.R) * ratio);
            var g = (byte)(a.G + (b.G - a.G) * ratio);
            var bl = (byte)(a.B + (b.B - a.B) * ratio);
            return $"#{r:X2}{g:X2}{bl:X2}";
        }
        catch { return baseHex; }
    }
}
