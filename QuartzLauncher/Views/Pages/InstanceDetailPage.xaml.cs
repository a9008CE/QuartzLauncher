using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using QuartzLauncher.Models;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

public partial class InstanceDetailPage : Page
{
    public event EventHandler? InstanceVisualChanged;
    public event EventHandler? InstanceDeleted;
    private readonly string _instanceId;
    private readonly string _instanceName;
    private readonly List<IconChoice> _iconChoices;
    private Instance? _instance;
    private bool _loadingVersionIsolation;
    private bool _loadingIcon;
    private bool _loadingJava;
    private string _selectedIconKey = "auto";

    public InstanceDetailPage(Instance instance)
    {
        _instanceId = instance.Id;
        _instanceName = instance.Name;
        _instance = instance;
        InitializeComponent();
        _iconChoices = InstanceIconService.Presets
            .Select(preset => new IconChoice(preset.Key, preset.Name, InstanceIconService.LoadPreset(preset.Key)))
            .Append(new IconChoice("custom", "自定义", InstanceIconService.LoadPreset("redstoneblock")))
            .ToList();
        IconCombo.ItemsSource = _iconChoices;
        Loaded += OnLoaded;
    }

    public InstanceDetailPage(string instanceId, string instanceName)
    {
        _instanceId = instanceId;
        _instanceName = instanceName;
        InitializeComponent();
        _iconChoices = InstanceIconService.Presets
            .Select(preset => new IconChoice(preset.Key, preset.Name, InstanceIconService.LoadPreset(preset.Key)))
            .Append(new IconChoice("custom", "自定义", InstanceIconService.LoadPreset("redstoneblock")))
            .ToList();
        IconCombo.ItemsSource = _iconChoices;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var metadataRoot = Path.Combine(App.Paths.InstancesDir, _instanceId);
        var instanceFile = Path.Combine(metadataRoot, "instance.json");
        if (File.Exists(instanceFile))
            _instance = JsonConvert.DeserializeObject<Instance>(File.ReadAllText(instanceFile));
        if (_instance == null) return;
        var root = InstancePathService.GetGameDirectory(App.Paths, App.Settings.Data, _instance);

        var isolated = _instance.VersionIsolation
                       ?? InstancePathService.GetDefaultIsolation(App.Settings.Data, _instance);
        _loadingVersionIsolation = true;
        VersionIsolationCombo.SelectedIndex = !isolated
            ? 2
            : _instance.UsesVersionDirectory == true ? 0 : 1;
        _loadingVersionIsolation = false;
        VersionIsolationStatus.Text = GetIsolationStatus(isolated, _instance.UsesVersionDirectory == true, root);

        InstanceTitle.Text = _instance.Name;
        InstanceSubtitle.Text = $"{_instance.McVersion} · {_instance.Id}";
        InstanceIcon.Source = InstanceIconService.Load(_instance);
        UpdateCustomIconChoice();
        var iconKey = !string.IsNullOrWhiteSpace(_instance.IconPath)
            ? "custom"
            : InstanceIconService.Presets.Any(preset => preset.Key.Equals(_instance.IconKey, StringComparison.OrdinalIgnoreCase))
                ? _instance.IconKey
                : "auto";
        SelectIconKey(iconKey);

        var baseVer = string.IsNullOrEmpty(_instance.McVersion) ? _instance.VersionId : _instance.McVersion;
        InfoMinecraft.Text = $"Minecraft 版本: {baseVer}";

        if (_instance.Loader == "vanilla")
            InfoLoader.Text = "加载器: 原版 (无 Mod 加载器)";
        else
        {
            var loaderName = char.ToUpper(_instance.Loader[0]) + _instance.Loader[1..];
            var ver = string.IsNullOrEmpty(_instance.LoaderVersion) ? "" : $" [{_instance.LoaderVersion}]";
            InfoLoader.Text = $"加载器: {loaderName}{ver}";
        }

        var instanceDir = new DirectoryInfo(root);
        InfoCreated.Text = $"创建时间: {instanceDir.CreationTime:yyyy-MM-dd HH:mm}";
        InfoDir.Text = !string.IsNullOrWhiteSpace(_instance.CustomGameDir)
            ? $"游戏目录: {_instance.CustomGameDir} (外部导入)"
            : root;

        JavaPathBox.Text = _instance.JavaPath;
        LoadJavaChoices(_instance.JavaPath);
        MemoryBox.Text = _instance.MemoryMb > 0 ? _instance.MemoryMb.ToString() : "";
        JvmArgumentsBox.Text = _instance.JvmArguments;
        GameArgumentsBox.Text = _instance.GameArguments;
        LaunchSettingsStatus.Text = "留空的项目会继承全局设置。";

        var modsDir = Path.Combine(root, "mods");
        var modCount = Directory.Exists(modsDir) ? Directory.GetFiles(modsDir, "*.jar").Length : 0;
        StatMods.Text = $"Mod 数量: {modCount} 个";

        var rpDir = Path.Combine(root, "resourcepacks");
        var rpCount = Directory.Exists(rpDir)
            ? Directory.GetDirectories(rpDir).Length + Directory.GetFiles(rpDir, "*.zip").Length
            : 0;
        StatResourcePacks.Text = $"资源包: {rpCount} 个";

        var spDir = Path.Combine(root, "shaderpacks");
        var spCount = Directory.Exists(spDir)
            ? Directory.GetDirectories(spDir).Length + Directory.GetFiles(spDir, "*.zip").Length
            : 0;
        StatShaderPacks.Text = $"光影包: {spCount} 个";

        var savesDir = Path.Combine(root, "saves");
        var saveCount = Directory.Exists(savesDir) ? Directory.GetDirectories(savesDir).Length : 0;
        StatSaves.Text = $"存档数量: {saveCount} 个";

        var crashDir = Path.Combine(root, "crash-reports");
        var crashCount = Directory.Exists(crashDir) ? Directory.GetFiles(crashDir).Length : 0;
        StatCrashes.Text = $"崩溃报告: {crashCount} 份";
        UpdateChineseSettingStatus();

    }

    private void ToggleChinese_Click(object sender, RoutedEventArgs e)
    {
        if (_instance == null) return;
        _instance.AutoSetChinese = !_instance.AutoSetChinese;
        new InstanceStore(App.Paths.InstancesDir).Create(_instance);
        UpdateChineseSettingStatus();
    }

    private void VersionIsolation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingVersionIsolation || _instance == null || VersionIsolationCombo.SelectedIndex < 0)
            return;

        var option = (VersionIsolationCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var isolated = option is "version" or "legacy";
        var usesVersionDirectory = option == "version";
        var current = _instance.VersionIsolation
                      ?? InstancePathService.GetDefaultIsolation(App.Settings.Data, _instance);
        var currentUsesVersionDirectory = _instance.UsesVersionDirectory == true;
        if (isolated == current && (!isolated || usesVersionDirectory == currentUsesVersionDirectory)) return;

        var result = AnimatedMessageBox.Show(
            "调整版本隔离后，需要手动迁移该版本的存档、Mods、资源包和设置文件。\n\n如果发现内容消失，把这项设置改回来即可恢复原目录。",
            "版本隔离警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            _loadingVersionIsolation = true;
            VersionIsolationCombo.SelectedIndex = !current
                ? 2
                : currentUsesVersionDirectory ? 0 : 1;
            _loadingVersionIsolation = false;
            return;
        }

        _instance.VersionIsolation = isolated;
        _instance.UsesVersionDirectory = usesVersionDirectory;
        new InstanceStore(App.Paths.InstancesDir).Create(_instance);
        InstancePathService.EnsureGameDirectory(App.Paths, App.Settings.Data, _instance);
        OnLoaded(this, new RoutedEventArgs());
    }

    private static string GetIsolationStatus(bool isolated, bool usesVersionDirectory, string root) =>
        !isolated
            ? $"与其他未隔离版本共用: {root}"
            : usesVersionDirectory
                ? $"独立游戏目录: {root}"
                : $"兼容旧实例目录: {root}";

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_instance == null) return;
        var dialog = new RenameInstanceDialog(_instance.Name) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        var nextName = dialog.InstanceName.Trim();
        if (string.IsNullOrWhiteSpace(nextName)) return;
        if (new InstanceStore(App.Paths.InstancesDir).List()
            .Any(instance => instance.Id != _instance.Id
                             && string.Equals(instance.Name, nextName, StringComparison.OrdinalIgnoreCase)))
        {
            AnimatedMessageBox.Show("已存在同名版本，请换一个名称。", "重命名失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _instance.Name = nextName;
        new InstanceStore(App.Paths.InstancesDir).Create(_instance);
        InstanceTitle.Text = _instance.Name;
        InstanceVisualChanged?.Invoke(this, EventArgs.Empty);
    }

    private void IconCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingIcon || _instance == null || IconCombo.SelectedItem is not IconChoice choice) return;
        var previousKey = _selectedIconKey;
        if (choice.Key == "custom")
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择版本图标",
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true)
            {
                SelectIconKey(previousKey);
                return;
            }
            try
            {
                InstanceIconService.Set(_instance, dialog.FileName);
                InstanceIcon.Source = InstanceIconService.Load(_instance);
                UpdateCustomIconChoice();
                _selectedIconKey = "custom";
                IconCombo.Items.Refresh();
                SelectIconKey("custom");
                InstanceVisualChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                SelectIconKey(previousKey);
                AnimatedMessageBox.Show($"设置版本图标失败：{ex.Message}", "版本图标", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return;
        }

        try
        {
            InstanceIconService.SetPreset(_instance, choice.Key);
            InstanceIcon.Source = InstanceIconService.Load(_instance);
            _selectedIconKey = choice.Key;
            InstanceVisualChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SelectIconKey(previousKey);
            AnimatedMessageBox.Show($"设置版本图标失败：{ex.Message}", "版本图标", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteVersion_Click(object sender, RoutedEventArgs e)
    {
        if (_instance == null) return;
        if (!ConfirmDelete(_instance)) return;

        try
        {
            InstanceDeletionService.Delete(App.Paths, App.Settings.Data, _instance);
            var hadSubscriber = InstanceDeleted != null;
            InstanceDeleted?.Invoke(this, EventArgs.Empty);
            if (!hadSubscriber && Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateTo(mainWindow.HomePage);
        }
        catch (Exception ex)
        {
            AnimatedMessageBox.Show($"删除版本失败：{ex.Message}", "删除版本", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveFromList_Click(object sender, RoutedEventArgs e)
    {
        if (_instance == null) return;

        var result = AnimatedMessageBox.Show(
            $"确定要将「{_instance.Name}」从启动器列表中移除吗？\n\n游戏文件不会被删除。",
            "从列表移除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            var store = new InstanceStore(App.Paths.InstancesDir);
            store.Delete(_instance.Id);
            var hadSubscriber = InstanceDeleted != null;
            InstanceDeleted?.Invoke(this, EventArgs.Empty);
            if (!hadSubscriber && Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateTo(mainWindow.HomePage);
        }
        catch (Exception ex)
        {
            AnimatedMessageBox.Show($"移除失败：{ex.Message}", "从列表移除", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool ConfirmDelete(Instance instance)
    {
        var confirmations = new[]
        {
            ($"确定要删除版本“{instance.Name}”吗？", "删除版本 - 第 1 次确认"),
            ($"版本“{instance.Name}”的游戏文件、存档、Mod、资源包等内容可能会被删除。\n\n确定继续吗？", "删除版本 - 第 2 次确认"),
            ($"最后确认：永久删除版本“{instance.Name}”？\n\n此操作不可撤销。", "删除版本 - 第 3 次确认")
        };
        foreach (var (message, title) in confirmations)
        {
            if (AnimatedMessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return false;
        }
        return true;
    }

    private void SelectIconKey(string key)
    {
        _loadingIcon = true;
        IconCombo.SelectedValue = key;
        _loadingIcon = false;
        _selectedIconKey = key;
    }

    private void UpdateCustomIconChoice()
    {
        var custom = _iconChoices.First(choice => choice.Key == "custom");
        custom.Icon = InstanceIconService.Load(_instance!);
    }

    private void OpenPath_Click(object sender, RoutedEventArgs e)
    {
        var root = _instance == null
            ? InstancePathService.GetGameDirectory(App.Paths, App.Settings.Data, _instanceId)
            : InstancePathService.GetGameDirectory(App.Paths, App.Settings.Data, _instance);
        try
        {
            Directory.CreateDirectory(root);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{root}\"",
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            AnimatedMessageBox.Show($"打开路径失败: {ex.Message}", "打开路径", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateChineseSettingStatus()
    {
        var enabled = _instance?.AutoSetChinese == true;
        ChineseSettingStatus.Text = enabled
            ? "当前：已开启。每次启动前自动设置为简体中文。"
            : "当前：已关闭。启动时不修改游戏语言。";
        ChineseSettingButton.BorderThickness = enabled ? new Thickness(2) : new Thickness(1);
        ChineseSettingButton.SetResourceReference(
            Control.BorderBrushProperty,
            enabled ? "PrimaryBrush" : "BorderBrush");
        ChineseSettingButton.SetResourceReference(
            Control.ForegroundProperty,
            enabled ? "PrimaryBrush" : "TextBrush");
    }

    private void LoadJavaChoices(string selectedPath)
    {
        var choices = App.Settings.Data.DetectedJavas
            .Where(java => !string.IsNullOrWhiteSpace(java.Path) && File.Exists(java.Path))
            .GroupBy(java => java.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(java => Math.Abs(java.MajorVersion - 21))
            .ThenByDescending(java => java.MajorVersion)
            .ThenBy(java => java.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _loadingJava = true;
        InstanceJavaSelector.Items.Clear();
        var inheritItem = new ComboBoxItem
        {
            Content = App.Settings.Data.AutoSelectJava ? "按版本自动选择 Java" : "继承全局 Java",
            Tag = ""
        };
        InstanceJavaSelector.Items.Add(inheritItem);
        foreach (var java in choices)
        {
            InstanceJavaSelector.Items.Add(new ComboBoxItem
            {
                Content = $"Java {java.MajorVersion} ({java.Version})  {java.Path}",
                Tag = java.Path,
                ToolTip = java.Path
            });
        }

        if (!string.IsNullOrWhiteSpace(selectedPath)
            && !choices.Any(java => java.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            InstanceJavaSelector.Items.Add(new ComboBoxItem
            {
                Content = $"当前路径  {selectedPath}",
                Tag = selectedPath,
                ToolTip = selectedPath
            });
        }

        var selectedItem = InstanceJavaSelector.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), selectedPath, StringComparison.OrdinalIgnoreCase));
        InstanceJavaSelector.SelectedItem = selectedItem ?? inheritItem;
        JavaPathBox.Text = selectedPath;
        _loadingJava = false;
        UpdateJavaSelectionStatus();
    }

    private async void RefreshJava_Click(object sender, RoutedEventArgs e)
    {
        JavaSelectionStatus.Text = "正在检测 Java...";
        try
        {
            var found = await Task.Run(() => JavaService.DetectAll());
            App.Settings.Data.DetectedJavas = found;
            App.Settings.Save();
            LoadJavaChoices(JavaPathBox.Text.Trim());
            JavaSelectionStatus.Text = found.Count == 0
                ? "未检测到可用 Java，启动时可直接下载所需版本。"
                : $"已刷新 Java 列表，共 {found.Count} 个安装。";
        }
        catch (Exception ex)
        {
            JavaSelectionStatus.Text = $"检测 Java 失败: {ex.Message}";
        }
    }

    private void InstanceJavaSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingJava || InstanceJavaSelector.SelectedItem is not ComboBoxItem item) return;
        JavaPathBox.Text = item.Tag?.ToString() ?? "";
        UpdateJavaSelectionStatus();
    }

    private void UpdateJavaSelectionStatus()
    {
        var selectedPath = JavaPathBox?.Text.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            if (App.Settings.Data.AutoSelectJava)
            {
                JavaSelectionStatus.Text = "当前：启动时按 Minecraft 版本自动选择匹配的 Java。";
            }
            else
            {
                var globalPath = App.Settings.Data.JavaPath;
                JavaSelectionStatus.Text = string.IsNullOrWhiteSpace(globalPath)
                    ? "当前：继承全局设置，但尚未选择 Java。"
                    : $"当前：继承全局 Java ({globalPath})。";
            }
        }
        else
        {
            var baseVersion = string.IsNullOrWhiteSpace(_instance?.McVersion)
                ? _instance?.VersionId ?? ""
                : _instance.McVersion;
            var requiredMajor = JavaService.RequiredMajorVersion(baseVersion);
            var selectedJava = JavaService.ProbeJava(selectedPath);
            JavaSelectionStatus.Text = App.Settings.Data.AutoSelectJava
                && !JavaService.IsCompatible(selectedJava, requiredMajor)
                    ? $"当前指定的 Java 不匹配；启动时将自动选择 Java {requiredMajor}。"
                    : "当前：此实例优先使用上方选择的 Java。";
        }
    }

    private void BrowseJava_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择实例 Java",
            Filter = "Java (*.exe)|java.exe|所有文件 (*.*)|*.*",
            FileName = "java.exe"
        };
        if (dialog.ShowDialog() != true) return;

        var java = JavaService.ProbeJava(dialog.FileName);
        if (java == null)
        {
            AnimatedMessageBox.Show("选择的文件不是可用的 64 位 Java。", "Java 路径",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var detected = App.Settings.Data.DetectedJavas
            .Where(item => !item.Path.Equals(java.Path, StringComparison.OrdinalIgnoreCase))
            .Append(java)
            .ToList();
        App.Settings.Data.DetectedJavas = detected;
        LoadJavaChoices(java.Path);
    }

    private void SaveLaunchSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_instance == null) return;
        var javaPath = JavaPathBox.Text.Trim();
        if (!string.IsNullOrEmpty(javaPath) && !File.Exists(javaPath))
        {
            AnimatedMessageBox.Show("实例 Java 路径不存在，请重新选择。", "保存失败",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var memoryText = MemoryBox.Text.Trim();
        var memory = 0;
        if (!string.IsNullOrEmpty(memoryText)
            && (!int.TryParse(memoryText, out memory) || memory < 512 || memory > 131072))
        {
            AnimatedMessageBox.Show("内存必须是 512 到 131072 之间的整数 MB。", "保存失败",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _instance.JavaPath = javaPath;
        _instance.MemoryMb = memory;
        _instance.JvmArguments = JvmArgumentsBox.Text.Trim();
        _instance.GameArguments = GameArgumentsBox.Text.Trim();
        new InstanceStore(App.Paths.InstancesDir).Create(_instance);
        LaunchSettingsStatus.Text = "实例启动设置已保存。";
    }

    private void Saves_Click(object sender, RoutedEventArgs e) => NavigateTo(new SavesPage(_instanceId));
    private void Mods_Click(object sender, RoutedEventArgs e) => NavigateTo(new ModsPage(_instanceId));
    private void ResourcePacks_Click(object sender, RoutedEventArgs e) => NavigateTo(new ResourcePacksPage(_instanceId));
    private void ShaderPacks_Click(object sender, RoutedEventArgs e) => NavigateTo(new ShaderPacksPage(_instanceId));
    private void NavigateTo(Page page)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateTo(page);
    }

    private sealed class IconChoice
    {
        public IconChoice(string key, string name, ImageSource icon)
        {
            Key = key;
            Name = name;
            Icon = icon;
        }

        public string Key { get; }
        public string Name { get; }
        public ImageSource Icon { get; set; }
    }
}

public class RenameInstanceDialog : Window
{
    public string InstanceName => _nameBox.Text;
    private readonly TextBox _nameBox;

    public RenameInstanceDialog(string currentName)
    {
        Title = "当前版本重命名";
        Width = 360;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "版本名称", Margin = new Thickness(0, 0, 0, 6) });
        _nameBox = new TextBox { Text = currentName, Height = 32, VerticalContentAlignment = VerticalAlignment.Center };
        panel.Children.Add(_nameBox);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "确定", Width = 70, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "取消", Width = 70 };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => { _nameBox.Focus(); _nameBox.SelectAll(); };
    }
}
