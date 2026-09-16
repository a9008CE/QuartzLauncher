using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public sealed record ModpackDescriptor(
    string Format,
    string Name,
    string MinecraftVersion,
    string Loader,
    string LoaderVersion,
    string Summary,
    int MemoryMb = 0,
    string JvmArguments = "",
    string GameArguments = "");

public sealed record ModpackExportOptions(
    bool IncludeMods,
    bool IncludeResourcePacks,
    bool IncludeShaderPacks,
    bool IncludeSaves,
    bool IncludeCrashes,
    bool IncludeGameSettings);

public sealed class ModpackService
{
    private const string QuartzManifest = "quartzpack.json";

    public ModpackDescriptor Inspect(string packagePath)
    {
        if (!File.Exists(packagePath)) throw new FileNotFoundException("整合包文件不存在。", packagePath);
        using var archive = ZipFile.OpenRead(packagePath);
        var manifest = ReadEntry(archive, QuartzManifest);
        if (manifest != null)
        {
            var data = JObject.Parse(manifest);
            if (!string.Equals(data.Value<string>("format"), "quartzpack", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("QuartzPack 清单格式不受支持。");
            if (data.Value<int?>("formatVersion") != 1)
                throw new InvalidDataException("QuartzPack 版本不受支持。");
            return new ModpackDescriptor(
                "qpack",
                data.Value<string>("name") ?? Path.GetFileNameWithoutExtension(packagePath),
                Required(data, "minecraft"),
                data.Value<string>("loader") ?? "vanilla",
                data.Value<string>("loaderVersion") ?? "",
                "QuartzLauncher 原生整合包",
                data.Value<int?>("memoryMb") ?? 0,
                data.Value<string>("jvmArguments") ?? "",
                data.Value<string>("gameArguments") ?? "");
        }

        manifest = ReadEntry(archive, "modrinth.index.json");
        if (manifest != null)
        {
            var data = JObject.Parse(manifest);
            var dependencies = data["dependencies"] as JObject ?? new JObject();
            var loader = dependencies.Properties()
                .Select(property => property.Name)
                .FirstOrDefault(name => name is "fabric-loader" or "forge" or "neoforge" or "quilt-loader") ?? "vanilla";
            var loaderVersion = loader == "vanilla" ? "" : dependencies.Value<string>(loader) ?? "";
            return new ModpackDescriptor(
                "mrpack",
                data.Value<string>("name") ?? Path.GetFileNameWithoutExtension(packagePath),
                Required(dependencies, "minecraft"),
                loader.Replace("-loader", "", StringComparison.OrdinalIgnoreCase),
                loaderVersion,
                "Modrinth 整合包");
        }

        manifest = ReadEntry(archive, "manifest.json");
        if (manifest != null)
        {
            var data = JObject.Parse(manifest);
            var minecraft = data["minecraft"] as JObject ?? new JObject();
            var loaderEntry = (minecraft["modLoaders"] as JArray)?.OrderByDescending(item =>
                item.Type == JTokenType.Object && item.Value<bool?>("primary") == true).FirstOrDefault();
            var loader = loaderEntry?.Type == JTokenType.Object
                ? loaderEntry.Value<string>("id") ?? "vanilla"
                : loaderEntry?.Value<string>() ?? "vanilla";
            var split = loader.Split('-', 2);
            return new ModpackDescriptor(
                "curseforge",
                data.Value<string>("name") ?? Path.GetFileNameWithoutExtension(packagePath),
                Required(minecraft, "version"),
                split[0],
                split.Length == 2 ? split[1] : "",
                "CurseForge 整合包");
        }

        throw new InvalidDataException("未识别的整合包格式。支持 QuartzPack、Modrinth .mrpack 和 CurseForge manifest.zip。");
    }

    public async Task<List<DownloadItem>> PrepareRemoteFilesAsync(string packagePath, string instanceRoot)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var manifestText = ReadEntry(archive, "modrinth.index.json");
        var result = new List<DownloadItem>();
        if (manifestText != null)
        {
            var data = JObject.Parse(manifestText);
            foreach (var file in (data["files"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var path = NormalizeEntry(file.Value<string>("path") ?? "");
                var url = file["downloads"]?.Values<string>().FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (path == null || IsSensitivePath(path) || string.IsNullOrWhiteSpace(url)) continue;
                if (string.Equals(file["env"]?["client"]?.ToString(), "unsupported", StringComparison.OrdinalIgnoreCase)) continue;
                var hashes = file["hashes"] as JObject;
                result.Add(new DownloadItem(
                    url,
                    Path.Combine(instanceRoot, path.Replace('/', Path.DirectorySeparatorChar)),
                    hashes?.Value<string>("sha1") ?? "",
                    file.Value<long?>("fileSize") ?? 0));
            }
        }

        var curseManifest = ReadEntry(archive, "manifest.json");
        if (curseManifest != null)
        {
            var data = JObject.Parse(curseManifest);
            foreach (var file in (data["files"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var projectId = file.Value<long?>("projectID") ?? 0;
                var fileId = file.Value<long?>("fileID") ?? 0;
                if (projectId <= 0 || fileId <= 0) continue;
                var resolved = await CurseForgeService.GetPackFileAsync(projectId, fileId);
                var path = NormalizeEntry(resolved.RelativePath)
                           ?? throw new InvalidDataException("CurseForge 返回了非法文件路径。");
                if (IsSensitivePath(path))
                    throw new InvalidDataException("CurseForge 返回了不允许导入的账户文件。");
                result.Add(new DownloadItem(resolved.DownloadUrl,
                    Path.Combine(instanceRoot, path.Replace('/', Path.DirectorySeparatorChar)),
                    resolved.Sha1, resolved.Size));
            }
        }
        return result
            .GroupBy(item => item.Target, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public void Export(string packagePath, Instance instance, string instanceRoot, ModpackExportOptions options)
    {
        if (!Directory.Exists(instanceRoot)) throw new DirectoryNotFoundException(instanceRoot);
        var included = new List<string>();
        if (options.IncludeMods) included.Add("mods");
        if (options.IncludeResourcePacks) included.Add("resourcepacks");
        if (options.IncludeShaderPacks) included.Add("shaderpacks");
        if (options.IncludeSaves) included.Add("saves");
        if (options.IncludeCrashes) included.Add("crash-reports");
        if (options.IncludeCrashes) included.Add("crashes");
        included.Add("config");

        var manifest = new JObject
        {
            ["format"] = "quartzpack",
            ["formatVersion"] = 1,
            ["name"] = instance.Name,
            ["minecraft"] = instance.McVersion,
            ["loader"] = instance.Loader,
            ["loaderVersion"] = instance.LoaderVersion,
            ["memoryMb"] = instance.MemoryMb,
            ["jvmArguments"] = instance.JvmArguments,
            ["gameArguments"] = instance.GameArguments,
            ["content"] = new JArray(included),
            ["createdAt"] = DateTimeOffset.UtcNow.ToString("O")
        };

        if (File.Exists(packagePath)) File.Delete(packagePath);
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        var files = new JArray();
        foreach (var directory in included.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var source = Path.Combine(instanceRoot, directory);
            if (Directory.Exists(source)) AddDirectory(archive, source, directory, files);
        }

        if (options.IncludeGameSettings)
        {
            foreach (var file in new[] { "options.txt", "servers.dat", "servers.json" })
            {
                var source = Path.Combine(instanceRoot, file);
                if (!File.Exists(source)) continue;
                archive.CreateEntryFromFile(source, file, CompressionLevel.Optimal);
                files.Add(FileRecord(source, file));
            }
        }
        manifest["files"] = files;
        WriteText(archive, QuartzManifest, manifest.ToString(Formatting.Indented));
    }

    public void ImportContent(string packagePath, string instanceRoot)
    {
        Directory.CreateDirectory(instanceRoot);
        using var archive = ZipFile.OpenRead(packagePath);
        var descriptor = Inspect(packagePath);
        if (descriptor.Format == "qpack")
        {
            var quartzManifest = JObject.Parse(ReadEntry(archive, QuartzManifest)
                                         ?? throw new InvalidDataException("QuartzPack 缺少清单。"));
            var allowed = (quartzManifest["files"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(record => NormalizeEntry(record.Value<string>("path") ?? ""))
                .Where(path => path != null)
                .Select(path => path!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            ExtractEntries(archive, instanceRoot, null, allowed);
            VerifyQuartzFiles(archive, instanceRoot);
            return;
        }

        if (descriptor.Format == "mrpack")
        {
            ExtractEntries(archive, instanceRoot, "overrides/");
            ExtractEntries(archive, instanceRoot, "client-overrides/");
            return;
        }

        var manifest = JObject.Parse(ReadEntry(archive, "manifest.json") ?? "{}");
        var overridesName = NormalizeEntry(manifest.Value<string>("overrides") ?? "overrides")
                            ?? throw new InvalidDataException("CurseForge overrides 路径无效。");
        var overrides = overridesName + "/";
        ExtractEntries(archive, instanceRoot, overrides);
    }

    private static void ExtractEntries(
        ZipArchive archive,
        string instanceRoot,
        string? prefix,
        IReadOnlySet<string>? allowed = null)
    {
        const long maxExtractedBytes = 8L * 1024 * 1024 * 1024;
        long extractedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            var entryName = NormalizeEntry(entry.FullName);
            if (entryName == null)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                throw new InvalidDataException($"整合包包含非法路径: {entry.FullName}");
            }
            if (entryName.Equals(QuartzManifest, StringComparison.OrdinalIgnoreCase)
                || entryName.Equals("modrinth.index.json", StringComparison.OrdinalIgnoreCase)
                || entryName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
            if (prefix != null)
            {
                if (!entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                entryName = entryName[prefix.Length..];
            }
            if (string.IsNullOrWhiteSpace(entryName) || string.IsNullOrEmpty(entry.Name)) continue;
            if (IsSensitivePath(entryName))
                throw new InvalidDataException($"整合包包含不允许导入的账户文件: {entryName}");
            if (allowed != null && !allowed.Contains(entryName))
                throw new InvalidDataException($"QuartzPack 包含清单之外的文件: {entryName}");
            extractedBytes += entry.Length;
            if (extractedBytes > maxExtractedBytes)
                throw new InvalidDataException("整合包解压后的内容超过 8 GB 安全上限。");
            var target = GetSafePath(instanceRoot, entryName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
    }

    private static void AddDirectory(ZipArchive archive, string source, string archiveRoot, JArray records)
    {
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint) || entry is not FileInfo file) continue;
            var relative = Path.GetRelativePath(source, file.FullName).Replace('\\', '/');
            var name = NormalizeEntry($"{archiveRoot}/{relative}") ?? throw new InvalidDataException("实例目录含有非法路径。");
            archive.CreateEntryFromFile(file.FullName, name, CompressionLevel.Optimal);
            records.Add(FileRecord(file.FullName, name));
        }
    }

    private static JObject FileRecord(string source, string path) => new()
    {
        ["path"] = path,
        ["size"] = new FileInfo(source).Length,
        ["sha1"] = DownloadService.Sha1File(source)
    };

    private static void VerifyQuartzFiles(ZipArchive archive, string instanceRoot)
    {
        var manifest = JObject.Parse(ReadEntry(archive, QuartzManifest)
                                     ?? throw new InvalidDataException("QuartzPack 缺少清单。"));
        foreach (var record in (manifest["files"] as JArray ?? new JArray()).OfType<JObject>())
        {
            var relative = NormalizeEntry(record.Value<string>("path") ?? "")
                           ?? throw new InvalidDataException("QuartzPack 清单包含非法路径。");
            var target = GetSafePath(instanceRoot, relative);
            if (!File.Exists(target)) throw new InvalidDataException($"整合包缺少文件: {relative}");
            var size = record.Value<long?>("size") ?? 0;
            if (size > 0 && new FileInfo(target).Length != size)
                throw new InvalidDataException($"整合包文件大小校验失败: {relative}");
            var sha1 = record.Value<string>("sha1") ?? "";
            if (!string.IsNullOrWhiteSpace(sha1)
                && !DownloadService.Sha1File(target).Equals(sha1, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"整合包文件哈希校验失败: {relative}");
        }
    }

    private static string? ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name);
        if (entry == null) return null;
        if (entry.Length > 4 * 1024 * 1024)
            throw new InvalidDataException($"整合包清单过大: {name}");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static void WriteText(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(text);
    }

    private static string Required(JObject data, string key) =>
        data.Value<string>(key) ?? throw new InvalidDataException($"整合包缺少 {key}。");

    private static string? NormalizeEntry(string value)
    {
        var normalized = value.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Contains(':')) return null;
        var parts = normalized.Split('/');
        if (parts.Any(part => part.Length == 0 || part == "." || part == "..")) return null;
        return string.Join('/', parts);
    }

    private static bool IsSensitivePath(string path)
    {
        var fileName = path.Split('/').LastOrDefault() ?? "";
        return fileName.Equals("launcher_accounts.json", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("launcher_profiles.json", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("launcher_profiles_microsoft_store.json", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSafePath(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("整合包包含越界路径。");
        return target;
    }
}
