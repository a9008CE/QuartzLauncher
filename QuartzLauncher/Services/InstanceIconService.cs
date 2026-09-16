using System.IO;
using System.Windows.Media.Imaging;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public sealed class InstanceIconPreset
{
    public InstanceIconPreset(string key, string name, string assetFileName)
    {
        Key = key;
        Name = name;
        AssetFileName = assetFileName;
    }

    public string Key { get; }
    public string Name { get; }
    public string AssetFileName { get; }
}

public static class InstanceIconService
{
    private const string FallbackIconFileName = "grass_block.png";

    public static IReadOnlyList<InstanceIconPreset> Presets { get; } =
    [
        new("auto", "自动选择（跟随版本类型）", "grass.png"),
        new("grass", "原版（草方块）", "grass.png"),
        new("commandblock", "快照（命令方块）", "commandblock.png"),
        new("cobblestone", "远古版本（圆石）", "cobblestone.png"),
        new("anvil", "Forge（铁砧）", "anvil.png"),
        new("neoforge", "NeoForge", "neoforge.png"),
        new("fabric", "Fabric", "fabric.png"),
        new("grasspath", "OptiFine（草径）", "grasspath.png"),
        new("egg", "LiteLoader（鸡蛋）", "egg.png"),
        new("goldblock", "愚人节（金块）", "goldblock.png"),
        new("redstoneblock", "其他（红石块）", "redstoneblock.png")
    ];

    public static BitmapSource Load(Instance instance)
    {
        var customPath = ResolvePath(instance);
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            try { return LoadBitmap(customPath); }
            catch { }
        }

        var key = string.IsNullOrWhiteSpace(instance.IconKey) || instance.IconKey == "auto"
            ? GetDefaultIconKey(instance)
            : instance.IconKey;
        try { return LoadPreset(key); }
        catch { return LoadAsset(FallbackIconFileName); }
    }

    public static BitmapSource LoadPreset(string key)
    {
        var preset = Presets.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (preset == null) throw new InvalidDataException("未知的版本图标。");
        return LoadAsset(preset.AssetFileName);
    }

    public static string GetDefaultIconKey(Instance instance)
    {
        var loader = instance.Loader ?? "";
        if (loader.Equals("forge", StringComparison.OrdinalIgnoreCase)) return "anvil";
        if (loader.Equals("neoforge", StringComparison.OrdinalIgnoreCase)) return "neoforge";
        if (loader.Equals("fabric", StringComparison.OrdinalIgnoreCase)) return "fabric";
        if (loader.Equals("optifine", StringComparison.OrdinalIgnoreCase)) return "grasspath";
        if (loader.Equals("liteloader", StringComparison.OrdinalIgnoreCase)
            || loader.Equals("lite-loader", StringComparison.OrdinalIgnoreCase)) return "egg";

        var version = string.IsNullOrWhiteSpace(instance.McVersion) ? instance.VersionId : instance.McVersion;
        if (string.IsNullOrWhiteSpace(version)) return "redstoneblock";
        if (version.Contains("April", StringComparison.OrdinalIgnoreCase)
            || version.Contains("3D Shareware", StringComparison.OrdinalIgnoreCase)
            || version.Contains("20w14", StringComparison.OrdinalIgnoreCase)) return "goldblock";
        if (version.Contains("snapshot", StringComparison.OrdinalIgnoreCase)
            || version.Contains("experimental", StringComparison.OrdinalIgnoreCase)
            || version.Contains("pre", StringComparison.OrdinalIgnoreCase)
            || version.Contains("rc", StringComparison.OrdinalIgnoreCase)
            || version.Contains('w')) return "commandblock";
        if (version.StartsWith("a", StringComparison.OrdinalIgnoreCase)
            || version.StartsWith("b", StringComparison.OrdinalIgnoreCase)
            || version.StartsWith("c0", StringComparison.OrdinalIgnoreCase)
            || version.StartsWith("rd-", StringComparison.OrdinalIgnoreCase)) return "cobblestone";
        return "grass";
    }

    public static void Set(Instance instance, string sourcePath)
    {
        _ = LoadBitmap(sourcePath);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico"))
            throw new InvalidDataException("请选择 PNG、JPG、BMP、GIF 或 ICO 图片。");

        var metadataRoot = Path.Combine(App.Paths.InstancesDir, instance.Id);
        Directory.CreateDirectory(metadataRoot);
        var fileName = "icon" + extension;
        var target = Path.Combine(metadataRoot, fileName);
        if (!Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            File.Copy(sourcePath, target, true);
        RemoveOtherCustomIcons(metadataRoot, target);

        instance.IconPath = fileName;
        instance.IconKey = "";
        new InstanceStore(App.Paths.InstancesDir).Create(instance);
    }

    public static void SetPreset(Instance instance, string key)
    {
        if (key == "custom" || Presets.All(item => !item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("未知的版本图标。");
        RemoveStoredCustomIcon(instance);
        instance.IconPath = "";
        instance.IconKey = key == "auto" ? "" : key;
        new InstanceStore(App.Paths.InstancesDir).Create(instance);
    }

    public static void Reset(Instance instance)
    {
        RemoveStoredCustomIcon(instance);
        instance.IconPath = "";
        instance.IconKey = "";
        new InstanceStore(App.Paths.InstancesDir).Create(instance);
    }

    private static void RemoveStoredCustomIcon(Instance instance)
    {
        var customPath = ResolvePath(instance);
        if (!Path.IsPathRooted(instance.IconPath)
            && !string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath)) File.Delete(customPath);
    }

    private static void RemoveOtherCustomIcons(string metadataRoot, string target)
    {
        foreach (var existing in Directory.EnumerateFiles(metadataRoot, "icon.*"))
        {
            if (!existing.Equals(target, StringComparison.OrdinalIgnoreCase)) File.Delete(existing);
        }
    }

    private static string ResolvePath(Instance instance)
    {
        if (string.IsNullOrWhiteSpace(instance.IconPath)) return "";
        if (Path.IsPathRooted(instance.IconPath)) return instance.IconPath;
        var metadataRoot = Path.GetFullPath(Path.Combine(App.Paths.InstancesDir, instance.Id));
        var candidate = Path.GetFullPath(Path.Combine(metadataRoot, instance.IconPath));
        return candidate.StartsWith(metadataRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : "";
    }

    private static BitmapSource LoadAsset(string fileName)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri($"pack://application:,,,/QuartzLauncher;component/Assets/{fileName}", UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 256;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 256;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
