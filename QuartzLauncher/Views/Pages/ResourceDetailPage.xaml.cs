using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class ResourceDetailPage : Page
{
    private readonly ModItem _mod;
    private readonly List<ModVersionItem> _versions;
    private readonly Func<ModVersionItem, Task> _downloadHandler;
    private bool _loadingSelection;

    public ResourceDetailPage(ModItem mod, List<ModVersionItem> versions, Func<ModVersionItem, Task> downloadHandler)
    {
        _mod = mod;
        _versions = versions;
        _downloadHandler = downloadHandler;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadHeader();
        LoadIntro();
        LoadVersions();
        WireLinks();
    }

    private void LoadHeader()
    {
        TitleText.Text = _mod.Name;
        OriginalNameText.Visibility = !string.IsNullOrWhiteSpace(_mod.OriginalName)
                                      && !string.Equals(_mod.OriginalName, _mod.Name, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
        OriginalNameText.Text = _mod.OriginalName;

        if (!string.IsNullOrWhiteSpace(_mod.IconUrl) || !string.IsNullOrEmpty(_mod.PageUrl))
        {
            LoadCover();
        }
        else
        {
            CoverFallback.Text = _mod.Name.Length > 0 ? _mod.Name[..1].ToUpperInvariant() : "M";
        }

        TagPanel.Children.Clear();
        AddTag(!string.IsNullOrEmpty(_mod.McmodPageUrl)
            ? "MC百科"
            : _mod.Source switch
            {
                ModSource.Modrinth => "Modrinth 下载",
                ModSource.CurseForge => "CurseForge 下载",
                _ => "资源中心"
            }, false);
        if (_mod.Source is ModSource.Modrinth or ModSource.CurseForge)
            AddTag(_mod.Source == ModSource.Modrinth ? "Modrinth" : "CurseForge", false);
        foreach (var category in _mod.Categories.Take(4))
            AddTag(category, true);

        AuthorText.Visibility = _mod.Authors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        AuthorText.Text = $"作者: {string.Join(", ", _mod.Authors)}";
        StatsText.Text = $"相关下载版本: {_versions.Count} 个";
        if (_mod.Source is ModSource.Modrinth or ModSource.CurseForge)
            StatsText.Text += $"  ·  下载量: {FormatCount(_mod.Downloads)}";
    }

    private void AddTag(string text, bool muted)
    {
        var tag = new Border
        {
            Background = muted
                ? new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF))
                : new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x96, 0xFF)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 6)
        };
        tag.Child = new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = muted ? (Brush)FindResource("TextMutedBrush") : new SolidColorBrush(Color.FromRgb(0x00, 0x96, 0xFF))
        };
        TagPanel.Children.Add(tag);
    }

    private async void LoadCover()
    {
        var url = _mod.IconUrl;
        if (string.IsNullOrWhiteSpace(url) && _mod.Source == ModSource.Modrinth && !string.IsNullOrEmpty(_mod.Id))
            url = (await ModrinthService.GetModAsync(_mod.Id))?.IconUrl ?? "";
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            using var http = HttpClients.Create(TimeSpan.FromSeconds(15));
            using var response = await http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new System.IO.MemoryStream(bytes);
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 256;
            bitmap.EndInit();
            bitmap.Freeze();
            CoverImage.Source = bitmap;
            CoverImage.Opacity = 1;
            CoverFallback.Visibility = Visibility.Collapsed;
        }
        catch
        {
        }
    }

    private void LoadIntro()
    {
        var detailText = !string.IsNullOrWhiteSpace(_mod.Description) ? _mod.Description : _mod.Summary;
        if (!string.IsNullOrWhiteSpace(_mod.DetailHtml))
        {
            IntroText.Visibility = Visibility.Collapsed;
            IntroHtml.Content = CreateOverview(_mod.DetailHtml);
            IntroHtml.Visibility = Visibility.Visible;
        }
        else if (!string.IsNullOrWhiteSpace(detailText))
        {
            IntroText.Text = detailText;
            IntroText.Visibility = Visibility.Visible;
        }
        else
        {
            IntroText.Text = "暂无详细资料";
            IntroText.Visibility = Visibility.Collapsed;
        }

        if (_mod.Source == ModSource.MCmod
            && (!string.IsNullOrWhiteSpace(_mod.McmodStatus)
                || !string.IsNullOrWhiteSpace(_mod.McmodSourceType)
                || _mod.McmodInternalInfo.Count > 0))
        {
            McmodInfoHeader.Visibility = Visibility.Visible;
            McmodInfoPanel.Visibility = Visibility.Visible;
            var content = McmodInfoContent;
            var tags = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var value in new[] { _mod.McmodStatus, _mod.McmodSourceType })
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x96, 0xFF)),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 0, 6, 5)
                };
                chip.Child = new TextBlock
                {
                    Text = value,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4D, 0xB6, 0xFF))
                };
                tags.Children.Add(chip);
            }
            if (tags.Children.Count > 0)
                content.Children.Add(tags);
            foreach (var item in _mod.McmodInternalInfo)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var label = new TextBlock
                {
                    Text = item.Label,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextMutedBrush")
                };
                var value = new TextBlock
                {
                    Text = item.Value,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextBrush"),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(label, 0);
                Grid.SetColumn(value, 2);
                row.Children.Add(label);
                row.Children.Add(value);
                content.Children.Add(row);
            }
        }
    }

    private void LoadVersions()
    {
        VersionLabel.Text = _versions.Count > 0
            ? $"选择下载版本 ({_versions.Count})"
            : _mod.Source == ModSource.MCmod ? "未匹配到可靠的下载项目" : "可用版本 (0)";
        if (_versions.Count == 0)
        {
            NoVersionsText.Text = _mod.Source == ModSource.MCmod
                ? "MC百科是知识库网站，没有公开下载接口。请把下载源切换为「Modrinth」或「CurseForge」重新搜索，即可在启动器内下载。"
                : "该资源没有可下载的文件版本。";
            NoVersionsText.Visibility = Visibility.Visible;
            return;
        }

        VersionPicker.Visibility = Visibility.Visible;

        var gameVersions = _versions
            .SelectMany(GetSupportedGameVersions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(GetMinecraftVersionSortKey)
            .ToList();
        foreach (var gameVersion in gameVersions)
            GameVersionCombo.Items.Add(new ComboBoxItem { Content = gameVersion, Tag = gameVersion });

        if (GameVersionCombo.Items.Count > 0)
            GameVersionCombo.SelectedIndex = 0;
    }

    private void GameVersion_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSelection || GameVersionCombo.SelectedItem is not ComboBoxItem item
            || item.Tag is not string gameVersion) return;
        _loadingSelection = true;
        LoaderCombo.Items.Clear();
        LoaderCombo.Items.Add(new ComboBoxItem { Content = "全部加载器", Tag = "" });
        foreach (var loader in _versions
            .Where(version => GetSupportedGameVersions(version).Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
            .SelectMany(GetSupportedLoaders)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(loader => loader))
        {
            LoaderCombo.Items.Add(new ComboBoxItem { Content = loader, Tag = loader });
        }
        LoaderCombo.SelectedIndex = 0;
        _loadingSelection = false;
        RefreshFileVersions();
    }

    private void Loader_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSelection) return;
        RefreshFileVersions();
    }

    private void RefreshFileVersions()
    {
        _loadingSelection = true;
        try
        {
            if (GameVersionCombo.SelectedItem is not ComboBoxItem selected
                || selected.Tag is not string gameVersion) return;
            var selectedLoader = LoaderCombo.SelectedItem is ComboBoxItem loaderItem
                ? loaderItem.Tag as string
                : "";
            FileVersionCombo.Items.Clear();
            foreach (var version in _versions
                .Where(version => GetSupportedGameVersions(version).Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                .Where(version => string.IsNullOrEmpty(selectedLoader)
                    || GetSupportedLoaders(version).Contains(selectedLoader, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(version => version.DateUploaded))
            {
                FileVersionCombo.Items.Add(new ComboBoxItem
                {
                    Content = FormatVersionOption(version),
                    Tag = version,
                    ToolTip = version.FileName
                });
            }
            if (FileVersionCombo.Items.Count > 0)
                FileVersionCombo.SelectedIndex = 0;
            UpdateDependencyText();
        }
        finally
        {
            _loadingSelection = false;
        }
    }

    private void FileVersion_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdateDependencyText();
    }

    private void UpdateDependencyText()
    {
        if (FileVersionCombo.SelectedItem is not ComboBoxItem selected
            || selected.Tag is not ModVersionItem version)
        {
            DependencyText.Text = "";
            return;
        }
        var required = version.Dependencies
            .Where(dependency => dependency.Type == "required")
            .ToList();
        var optional = version.Dependencies
            .Where(dependency => dependency.Type == "optional")
            .ToList();
        var lines = new List<string>();
        if (required.Count > 0)
            lines.Add($"★ 必需前置: {string.Join("、", required.Select(DisplayDependency))}");
        if (optional.Count > 0)
            lines.Add($"可选前置: {string.Join("、", optional.Select(DisplayDependency))}");
        lines.Add("下载时将自动获取并安装所有必需前置 Mod。");
        DependencyText.Text = string.Join(Environment.NewLine, lines);
    }

    private static string DisplayDependency(ModDependency dependency) =>
        !string.IsNullOrWhiteSpace(dependency.ProjectName)
            ? dependency.ProjectName
            : !string.IsNullOrWhiteSpace(dependency.ProjectSlug)
                ? dependency.ProjectSlug
                : dependency.ProjectId;

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (FileVersionCombo.SelectedItem is not ComboBoxItem selected
            || selected.Tag is not ModVersionItem version) return;
        DownloadBtn.IsEnabled = false;
        try
        {
            await _downloadHandler(version);
        }
        finally
        {
            DownloadBtn.IsEnabled = true;
        }
    }

    private void WireLinks()
    {
        McmodLinkBtn.Visibility = !string.IsNullOrEmpty(_mod.McmodPageUrl) ? Visibility.Visible : Visibility.Collapsed;
        OfficialLinkBtn.Visibility = !string.IsNullOrEmpty(_mod.PageUrl) && _mod.Source != ModSource.MCmod
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (McmodLinkBtn.Visibility == Visibility.Visible)
            McmodLinkBtn.Click += (_, _) => OpenUrl(_mod.McmodPageUrl);
        if (OfficialLinkBtn.Visibility == Visibility.Visible)
            OfficialLinkBtn.Click += (_, _) => OpenUrl(_mod.PageUrl);
    }

    private FrameworkElement CreateOverview(string html)
    {
        var contentMatch = System.Text.RegularExpressions.Regex.Match(html,
            @"<main\b[^>]*>(?<content>.*?)</main>",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var content = contentMatch.Success ? contentMatch.Groups["content"].Value : html;
        var root = new StackPanel();
        var blocks = System.Text.RegularExpressions.Regex.Matches(content,
            @"<p\b[^>]*>.*?</p>|<ul\b[^>]*>.*?</ul>|<ol\b[^>]*>.*?</ol>|<table\b[^>]*>.*?</table>|<img\b[^>]*>",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match block in blocks)
        {
            var value = block.Value.Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^<p\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                root.Children.Add(CreateParagraph(System.Text.RegularExpressions.Regex.Replace(value,
                    @"^<p\b[^>]*>|</p>$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline)));
            else if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^<(?:ul|ol)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                root.Children.Add(CreateList(value));
            else if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^<table\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                root.Children.Add(CreateParagraph(CleanMarkup(value)));
            else
                AddImage(root, value);
        }
        if (root.Children.Count == 0)
            root.Children.Add(CreateParagraph(content));
        return root;
    }

    private TextBlock CreateParagraph(string text)
    {
        return new TextBlock
        {
            Text = CleanMarkup(text),
            FontSize = 13,
            LineHeight = 21,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.92,
            Margin = new Thickness(0, 0, 0, 8)
        };
    }

    private TextBlock CreateList(string text)
    {
        var items = System.Text.RegularExpressions.Regex.Matches(text, @"<li\b[^>]*>(?<item>.*?)</li>",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(match => $"  ·  {CleanMarkup(match.Groups["item"].Value)}");
        return new TextBlock
        {
            Text = string.Join(Environment.NewLine, items),
            FontSize = 13,
            LineHeight = 20,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.92,
            Margin = new Thickness(4, 0, 0, 8)
        };
    }

    private void AddImage(StackPanel panel, string imgTag)
    {
        var match = System.Text.RegularExpressions.Regex.Match(imgTag, @"<(?:data-src|data-original|src)\s*=\s*[""'](?<url>[^""']+)[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return;
        var url = match.Groups["url"].Value;
        if (string.IsNullOrWhiteSpace(url) || url.Contains("loading", StringComparison.OrdinalIgnoreCase)) return;
        if (url.StartsWith("//")) url = "https:" + url;
        var image = new System.Windows.Controls.Image
        {
            Width = 420,
            MaxHeight = 300,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, 10),
            Source = null
        };
        image.Source = new System.Windows.Media.ImageSourceConverter().ConvertFromString(url) as ImageSource;
        if (image.Source != null)
            panel.Children.Add(image);
    }

    private static string CleanMarkup(string text)
    {
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<(?:br\s*/?>|/p|/div|p[^>]*|/li|li[^>]*|/tr|/td|td[^>]*|/th|th[^>]*)[^>]*>", "\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text).Replace('\u00a0', ' ');
        var lines = text.Split('\n')
            .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"[ \t]+", " ").Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> GetSupportedGameVersions(ModVersionItem version)
    {
        var values = version.GameVersions.Count > 0
            ? version.GameVersions
            : new List<string> { version.GameVersion };
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value)
                && !IsLoaderLabel(value)
                && !IsRuntimeLabel(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetSupportedLoaders(ModVersionItem version)
    {
        var values = version.Loaders.Count > 0
            ? version.Loaders
            : new List<string> { version.Loader };
        var inferred = version.GameVersions.Where(IsLoaderLabel);
        var supported = values.Concat(inferred)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return supported.Count > 0 ? supported : new[] { "通用" };
    }

    private static bool IsLoaderLabel(string value) => value.Trim().ToLowerInvariant() switch
    {
        "forge" or "fabric" or "neoforge" or "quilt" or "liteloader" or "cauldron" => true,
        _ => false
    };

    private static bool IsRuntimeLabel(string value) => value.Trim().ToLowerInvariant() switch
    {
        "client" or "server" or "minecraft client" or "minecraft server" => true,
        _ => false
    };

    private static Version GetMinecraftVersionSortKey(string gameVersion)
    {
        var match = System.Text.RegularExpressions.Regex.Match(gameVersion, @"\d+(?:\.\d+){0,2}");
        return match.Success && Version.TryParse(match.Value, out var value)
            ? value
            : new Version(0, 0);
    }

    private static string FormatVersionOption(ModVersionItem version)
    {
        var name = !string.IsNullOrWhiteSpace(version.Name)
            ? version.Name
            : !string.IsNullOrWhiteSpace(version.VersionNumber) ? version.VersionNumber : version.FileName;
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(version.VersionNumber)
            && !string.Equals(name, version.VersionNumber, StringComparison.OrdinalIgnoreCase))
            details.Add(version.VersionNumber);
        var loaders = GetSupportedLoaders(version).Where(loader => loader != "通用").ToList();
        if (loaders.Count > 0)
            details.Add(string.Join("/", loaders));
        if (version.FileSize > 0)
            details.Add(FormatSize(version.FileSize));
        return details.Count == 0 ? name : $"{name}  ·  {string.Join(" · ", details)}";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        >= 1024 => $"{bytes / 1024.0:F0} KB",
        _ => $"{bytes} B"
    };

    private static string FormatCount(long count) => count switch
    {
        >= 100_000_000 => $"{count / 100_000_000.0:F1}亿",
        >= 100_000 => $"{count / 10_000.0:F1}万",
        _ => count.ToString()
    };

    private static void OpenUrl(string url)
    {
        try { ExternalOpenService.OpenUrl(url); }
        catch { }
    }
}
