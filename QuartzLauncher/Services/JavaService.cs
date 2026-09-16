using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public sealed record JavaScanProgress(double Fraction, string Message, int FoundCount);

public sealed record JavaDownloadOption(
    int MajorVersion,
    string Version,
    string DownloadUrl,
    string Sha256,
    long Size,
    string FileName)
{
    public string Label => $"Java {MajorVersion} · Temurin {Version}";
}

public static class JavaService
{
    private const int ProbeTimeoutMs = 15000;
    private const string JavaInstallRoot = @"C:\QuartzLauncher\Java";
    private static readonly HttpClient AdoptiumClient = CreateAdoptiumClient();
    private static readonly SemaphoreSlim InstallLock = new(1, 1);

    public static string InstallDirectory => JavaInstallRoot;

    private static HttpClient CreateAdoptiumClient()
    {
        var client = HttpClients.Create(TimeSpan.FromMinutes(2));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QuartzLauncher/1.0");
        return client;
    }

    public static List<JavaInfo> DetectAll()
    {
        var folders = GetCandidateFolders();
        var trustedFolders = GetTrustedFolders();
        var candidates = SearchJavaFolders(folders, trustedFolders);
        candidates.AddRange(GetPathJavaFolders());
        candidates = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new ConcurrentBag<JavaInfo>();

        Parallel.ForEach(candidates, new ParallelOptions { MaxDegreeOfParallelism = 4 }, folder =>
        {
            var info = ProbeJava(Path.Combine(folder, "java.exe"));
            if (info != null) result.Add(info);
        });

        return result
            .GroupBy(info => info.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(info => Math.Abs(info.MajorVersion - 21))
            .ThenByDescending(info => info.MajorVersion)
            .ThenBy(info => info.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static JavaInfo? DetectByMajor(int major) =>
        DetectAll().FirstOrDefault(info => info.MajorVersion == major);

    public static int RequiredMajorVersion(string mcVersion)
    {
        try
        {
            var parts = mcVersion.Split('.');
            var major = int.Parse(parts[0]);
            var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            var patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            if (major >= 26) return 25;
            if (major == 1 && minor <= 16) return 8;
            if (major == 1 && minor <= 20 && patch < 5) return 17;
            if (major == 1 && minor <= 21 && patch < 9) return 21;
            if (major == 1 && minor >= 22) return 25;
            return 21;
        }
        catch
        {
            return 21;
        }
    }

    public static JavaInfo? ProbeJava(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            var javaPath = Path.GetFullPath(path.Trim().Trim('"'));
            if (!File.Exists(javaPath)) return null;

            var psi = new ProcessStartInfo
            {
                FileName = javaPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-XshowSettings:properties");
            psi.ArgumentList.Add("-version");

            using var process = Process.Start(psi);
            if (process == null) return null;

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ProbeTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            var output = string.Join(Environment.NewLine,
                standardError.GetAwaiter().GetResult(),
                standardOutput.GetAwaiter().GetResult());
            if (string.IsNullOrWhiteSpace(output) || process.ExitCode != 0) return null;

            var lowerOutput = output.ToLowerInvariant();
            if (lowerOutput.Contains("/lib/ext exists")
                || lowerOutput.Contains("a fatal error")
                || lowerOutput.Contains("error: ")) return null;
            if (!lowerOutput.Contains("64-bit")) return null;

            var version = ParseJavaVersion(output);
            if (version == null) return null;

            return new JavaInfo
            {
                Path = javaPath,
                Version = version,
                MajorVersion = ParseMajorVersion(version)
            };
        }
        catch
        {
            return null;
        }
    }

    public static JavaInfo? FindForVersion(string mcVersion)
    {
        var requiredMajor = RequiredMajorVersion(mcVersion);
        return SelectForMajor(requiredMajor);
    }

    public static bool IsCompatible(JavaInfo? java, int requiredMajor) =>
        java != null && requiredMajor > 0 && java.MajorVersion == requiredMajor;

    public static JavaInfo? SelectForMajor(
        int requiredMajor,
        string? instancePath = null,
        string? globalPath = null,
        IEnumerable<JavaInfo>? detected = null,
        bool autoSelect = true)
    {
        if (requiredMajor <= 0) return null;

        var instanceJava = ProbeJava(instancePath ?? "");
        if (IsCompatible(instanceJava, requiredMajor)) return instanceJava;

        if (autoSelect)
        {
            if (detected != null)
            {
                foreach (var cached in detected.Where(java => java.MajorVersion == requiredMajor))
                {
                    var java = ProbeJava(cached.Path);
                    if (IsCompatible(java, requiredMajor)) return java;
                }
            }

            try
            {
                var scanned = DetectAll();
                var match = scanned.FirstOrDefault(java => IsCompatible(java, requiredMajor));
                if (match != null) return match;
            }
            catch
            {
            }
        }

        var globalJava = ProbeJava(globalPath ?? "");
        return IsCompatible(globalJava, requiredMajor) ? globalJava : null;
    }

    public static Task<JavaDownloadOption?> GetDownloadOptionAsync(
        int majorVersion,
        CancellationToken cancellationToken = default) =>
        majorVersion < 8
            ? Task.FromResult<JavaDownloadOption?>(null)
            : GetLatestDownloadAsync(majorVersion, cancellationToken);

    public static async Task<List<JavaDownloadOption>> GetDownloadOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var releases = await GetAvailableReleaseNumbersAsync(cancellationToken);
        var requests = releases.Select(release => GetLatestDownloadAsync(release, cancellationToken));
        var results = await Task.WhenAll(requests);
        return results
            .Where(option => option != null)
            .Cast<JavaDownloadOption>()
            .OrderByDescending(option => option.MajorVersion)
            .ThenByDescending(option => option.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static JavaInfo? FindInstalled(int majorVersion)
    {
        var installRoot = GetInstallRoot(majorVersion);
        if (!Directory.Exists(installRoot)) return null;
        try
        {
            return Directory.EnumerateFiles(installRoot, "java.exe", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(Path.Combine("bin", "java.exe"), StringComparison.OrdinalIgnoreCase))
                .Select(ProbeJava)
                .FirstOrDefault(info => info != null);
        }
        catch
        {
            return null;
        }
    }

    public static DownloadItem CreateDownloadItem(JavaDownloadOption option)
    {
        Directory.CreateDirectory(JavaInstallRoot);
        var archivePath = Path.Combine(JavaInstallRoot, option.FileName);
        return new DownloadItem(option.DownloadUrl, archivePath, Size: option.Size, Sha256: option.Sha256);
    }

    public static async Task<JavaInfo> InstallDownloadedAsync(JavaDownloadOption option)
    {
        await InstallLock.WaitAsync();
        try
        {
            var archivePath = Path.Combine(JavaInstallRoot, option.FileName);
            if (!File.Exists(archivePath))
                throw new FileNotFoundException($"Java {option.MajorVersion} 下载文件不存在", archivePath);
            if (option.Size > 0 && new FileInfo(archivePath).Length != option.Size)
                throw new IOException($"Java {option.MajorVersion} 下载文件大小校验失败");

            var checksum = await ComputeSha256Async(archivePath);
            if (!checksum.Equals(option.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Java {option.MajorVersion} SHA-256 校验失败");

            var installRoot = GetInstallRoot(option.MajorVersion);
            var stagingRoot = installRoot + ".staging";
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
            Directory.CreateDirectory(stagingRoot);
            try
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, stagingRoot));
                var javaPath = Directory.EnumerateFiles(stagingRoot, "java.exe", SearchOption.AllDirectories)
                    .FirstOrDefault(path => path.EndsWith(Path.Combine("bin", "java.exe"), StringComparison.OrdinalIgnoreCase));
                if (javaPath == null)
                    throw new IOException($"Java {option.MajorVersion} 解压完成，但未找到 bin\\java.exe");

                if (Directory.Exists(installRoot)) Directory.Delete(installRoot, true);
                Directory.Move(stagingRoot, installRoot);
            }
            catch
            {
                if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
                throw;
            }

            File.Delete(archivePath);
            var installedJava = FindInstalled(option.MajorVersion);
            return installedJava ?? throw new IOException($"Java {option.MajorVersion} 安装完成，但无法验证 Java 可执行文件");
        }
        catch (Exception ex) when (ex is not IOException || ex.InnerException != null)
        {
            throw new IOException($"安装 Java {option.MajorVersion} 失败: {ex.Message}", ex);
        }
        finally
        {
            InstallLock.Release();
        }
    }

    private static async Task<List<int>> GetAvailableReleaseNumbersAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await AdoptiumClient.GetAsync(
                "https://api.adoptium.net/v3/info/available_releases", cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (json.RootElement.TryGetProperty("available_releases", out var releases)
                && releases.ValueKind == JsonValueKind.Array)
            {
                var available = releases.EnumerateArray()
                    .Where(value => value.TryGetInt32(out var release) && release >= 8)
                    .Select(value => value.GetInt32())
                    .Distinct()
                    .ToList();
                if (available.Count > 0) return available;
            }
        }
        catch
        {
            // The fixed range below keeps the catalog useful if the metadata endpoint is unavailable.
        }

        return Enumerable.Range(8, 19).ToList();
    }

    private static async Task<JavaDownloadOption?> GetLatestDownloadAsync(
        int majorVersion, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.adoptium.net/v3/assets/latest/{majorVersion}/hotspot"
                      + "?architecture=x64&image_type=jdk&jvm_impl=hotspot&os=windows"
                      + "&vendor=eclipse&heap_size=normal&project=jdk&release_type=ga";
            using var response = await AdoptiumClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Array) return null;

            foreach (var release in json.RootElement.EnumerateArray())
            {
                if (!release.TryGetProperty("binary", out var binary)
                    || !binary.TryGetProperty("package", out var package)
                    || !package.TryGetProperty("link", out var link)
                    || !package.TryGetProperty("name", out var name)
                    || !package.TryGetProperty("checksum", out var checksum)
                    || !package.TryGetProperty("size", out var size)
                    || !release.TryGetProperty("version", out var version)
                    || !version.TryGetProperty("semver", out var semver)) continue;

                var downloadUrl = link.GetString();
                var fileName = name.GetString();
                var sha256 = checksum.GetString();
                var releaseVersion = semver.GetString();
                if (string.IsNullOrWhiteSpace(downloadUrl)
                    || string.IsNullOrWhiteSpace(fileName)
                    || string.IsNullOrWhiteSpace(sha256)
                    || string.IsNullOrWhiteSpace(releaseVersion)
                    || !size.TryGetInt64(out var packageSize)
                    || packageSize <= 0) return null;

                return new JavaDownloadOption(
                    majorVersion, releaseVersion, downloadUrl, sha256, packageSize, fileName);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
        return null;
    }

    private static string GetInstallRoot(int majorVersion) =>
        Path.Combine(JavaInstallRoot, $"temurin-{majorVersion}");

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static List<JavaInfo> DetectInDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return [];
        try
        {
            var root = Path.GetFullPath(directory.Trim().Trim('"'));
            if (!Directory.Exists(root)) return [];
            var candidates = SearchJavaFolders([root], []);
            var result = new ConcurrentBag<JavaInfo>();
            Parallel.ForEach(candidates, new ParallelOptions { MaxDegreeOfParallelism = 4 }, folder =>
            {
                var info = ProbeJava(Path.Combine(folder, "java.exe"));
                if (info != null) result.Add(info);
            });
            return result
                .GroupBy(info => info.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(info => Math.Abs(info.MajorVersion - 21))
                .ThenByDescending(info => info.MajorVersion)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static async Task<List<JavaInfo>> ScanAllDrivesAsync(
        IProgress<JavaScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var drives = new List<DriveInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady) drives.Add(drive);
            }
            catch { }
        }
        drives = drives.OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (drives.Count == 0) return [];

        var candidates = new ConcurrentBag<string>();
        for (var index = 0; index < drives.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var drive = drives[index];
            var scannedDirectories = 0;
            IProgress<double> driveProgress = new Progress<double>(fraction =>
            {
                var overall = 0.96 * (index + Math.Clamp(fraction, 0, 1)) / drives.Count;
                progress?.Report(new JavaScanProgress(overall,
                    $"正在扫描 {drive.Name} ({index + 1}/{drives.Count})", candidates.Count));
            });

            await Task.Run(() =>
            {
                var pending = new Stack<string>();
                pending.Push(drive.RootDirectory.FullName);
                var reportWatch = Stopwatch.StartNew();
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var directory = pending.Pop();
                    scannedDirectories++;
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(directory, "java.exe", SearchOption.TopDirectoryOnly))
                        {
                            if (!HasReparsePoint(file)) candidates.Add(Path.GetFullPath(file));
                        }

                        foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                        {
                            if (!HasReparsePoint(child)) pending.Push(child);
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                    catch (IOException) { }

                    // The total directory count is unknown during a full scan; animate toward each drive's endpoint.
                    if (reportWatch.ElapsedMilliseconds >= 100)
                    {
                        driveProgress.Report(0.02 + 0.94 * (1 - Math.Exp(-scannedDirectories / 1600d)));
                        reportWatch.Restart();
                    }
                }
                driveProgress.Report(1);
            }, cancellationToken);
        }

        var paths = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new ConcurrentBag<JavaInfo>();
        var completed = 0;
        await Task.Run(() => Parallel.ForEach(paths, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 4
        }, path =>
        {
            var info = ProbeJava(path);
            if (info != null) result.Add(info);
            var done = Interlocked.Increment(ref completed);
            var fraction = paths.Count == 0 ? 1 : (double)done / paths.Count;
            progress?.Report(new JavaScanProgress(
                0.96 + fraction * 0.04, "正在验证扫描到的 Java", result.Count));
        }), cancellationToken);

        return result
            .GroupBy(info => info.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(info => Math.Abs(info.MajorVersion - 21))
            .ThenByDescending(info => info.MajorVersion)
            .ThenBy(info => info.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ParseJavaVersion(string output)
    {
        var match = Regex.Match(output, @"(?im)\bversion\s+""(?<version>[^""]+)""");
        if (!match.Success)
            match = Regex.Match(output, @"(?im)^\s*(?:openjdk|java)\s+(?<version>\d+(?:\.\d+)*(?:[_+][^\s-]+)?)(?:\s|$)");
        if (!match.Success) return null;

        var raw = match.Groups["version"].Value.Trim();
        var beforeBuildLabel = raw.IndexOf('-', StringComparison.Ordinal);
        if (beforeBuildLabel >= 0) raw = raw[..beforeBuildLabel];
        raw = raw.Replace('_', '.').Replace('+', '.');

        if (raw.StartsWith("1.", StringComparison.Ordinal)) raw = raw[2..];
        if (!Regex.IsMatch(raw, @"^\d+(?:\.\d+)*$")) return null;

        while (raw.Split('.').Length < 4) raw += ".0";
        var normalized = raw.Split('.').Take(4).ToArray();
        if (normalized.Any(segment => !int.TryParse(segment, out _))) return null;

        var major = int.Parse(normalized[0]);
        if (major is <= 4 or >= 100) return null;
        return string.Join('.', normalized);
    }

    private static int ParseMajorVersion(string version)
    {
        return int.TryParse(version.Split('.')[0], out var major) ? major : 0;
    }

    private static List<string> SearchJavaFolders(IEnumerable<string> folders, IReadOnlyList<string> trustedFolders)
    {
        var found = new ConcurrentBag<string>();
        Parallel.ForEach(
            folders.Distinct(StringComparer.OrdinalIgnoreCase),
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            folder =>
            {
                try
                {
                    if (!Directory.Exists(folder)) return;
                    foreach (var file in Directory.EnumerateFiles(folder, "java.exe", SearchOption.AllDirectories))
                    {
                        var fullPath = Path.GetFullPath(file);
                        var javaFolder = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(javaFolder)) found.Add(javaFolder);
                    }
                }
                catch
                {
                    // A protected or disconnected candidate folder should not stop other scans.
                }
            });

        var candidates = found
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(folder => new
            {
                Folder = folder,
                HasReparsePoint = !trustedFolders.Any(root => IsPathWithin(folder, root)) && HasReparsePoint(folder),
                IsSpecialPath = IsSpecialPath(folder)
            })
            .ToList();

        if (candidates.Count == 0) return [];
        if (candidates.Any(candidate => !candidate.HasReparsePoint))
            candidates.RemoveAll(candidate => candidate.HasReparsePoint);
        if (candidates.Any(candidate => !candidate.IsSpecialPath))
            candidates.RemoveAll(candidate => candidate.IsSpecialPath);
        return candidates.Select(candidate => candidate.Folder).ToList();
    }

    private static List<string> GetCandidateFolders()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var minecraftDir = GetMinecraftDirectory();
        var roamingMinecraftDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

        var folders = new List<string>
        {
            Path.Combine(minecraftDir, "runtime"),
            Path.Combine(roamingMinecraftDir, "runtime"),
            Path.Combine(AppContext.BaseDirectory, "Launcher", "java"),
            JavaInstallRoot,
            Path.Combine(userProfile, ".hmcl", "java"),
            Path.Combine(userProfile, "ATLauncher", "runtimes", "minecraft"),
            Path.Combine(userProfile, "ModrinthApp", "meta", "java_versions"),
            Path.Combine(userProfile, "PrismLauncher", "java"),
            Path.Combine(userProfile, "curseforge", "minecraft", "Install", "runtime"),
            Path.Combine(userProfile, ".jdks"),
            Path.Combine(userProfile, ".sdkman", "candidates", "java"),
            Path.Combine(localAppData, ".ftba", "bin", "runtime"),
            Path.Combine(localAppData, "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe", "LocalCache", "Local", "runtime"),
            Path.Combine(programFilesX86, "Minecraft Launcher", "runtime"),
            Path.Combine(programFilesX86, "Minecraft", "runtime"),
            Path.Combine(documents, "Curse", "Minecraft", "Install", "runtime"),
            Path.Combine(programFiles, "Java"),
            Path.Combine(programFiles, "Eclipse Adoptium"),
            Path.Combine(programFiles, "Amazon Corretto"),
            Path.Combine(programFiles, "Zulu")
        };

        var microsoftRoot = Path.Combine(programFiles, "Microsoft");
        try
        {
            if (Directory.Exists(microsoftRoot))
            {
                folders.AddRange(Directory.EnumerateDirectories(microsoftRoot, "jdk-*", SearchOption.TopDirectoryOnly));
            }
        }
        catch { }

        var environmentHomes = string.Join(';',
            Environment.GetEnvironmentVariable("JDK_HOME"),
            Environment.GetEnvironmentVariable("JAVA_HOME"));
        folders.AddRange(environmentHomes.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim().Trim('"')));
        folders.AddRange(GetRegistryJavaHomes());

        return folders
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path =>
            {
                try { return Path.GetFullPath(path); }
                catch { return null; }
            })
            .Where(path => path != null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetPathJavaFolders()
    {
        var result = new List<string>();
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var folder = entry.Trim().Trim('"');
                var javaPath = Path.Combine(folder, "java.exe");
                if (File.Exists(javaPath)) result.Add(Path.GetFullPath(folder));
            }
            catch { }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> GetRegistryJavaHomes()
    {
        var result = new List<string>();
        if (!OperatingSystem.IsWindows()) return result;

        var keyPaths = new[]
        {
            @"SOFTWARE\JavaSoft\Java Runtime Environment",
            @"SOFTWARE\JavaSoft\Java Development Kit",
            @"SOFTWARE\JavaSoft\JRE",
            @"SOFTWARE\JavaSoft\JDK",
            @"SOFTWARE\Eclipse Adoptium\JDK",
            @"SOFTWARE\Eclipse Adoptium\JRE",
            @"SOFTWARE\Microsoft\JDK",
            @"SOFTWARE\Amazon Corretto",
            @"SOFTWARE\Azul Zulu",
            @"SOFTWARE\BellSoft\Liberica"
        };

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32, RegistryView.Default })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    foreach (var keyPath in keyPaths)
                    {
                        using var key = baseKey.OpenSubKey(keyPath);
                        if (key == null) continue;
                        AddRegistryHome(result, key);
                        foreach (var version in key.GetSubKeyNames())
                        {
                            using var versionKey = key.OpenSubKey(version);
                            if (versionKey != null) AddRegistryHome(result, versionKey);
                        }
                    }
                }
                catch { }
            }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddRegistryHome(List<string> result, RegistryKey key)
    {
        foreach (var valueName in new[] { "JavaHome", "InstallationPath", "Path", "Home" })
        {
            var value = key.GetValue(valueName)?.ToString();
            if (string.IsNullOrWhiteSpace(value)) continue;
            value = value.Trim().Trim('"');
            if (File.Exists(value))
            {
                var directory = Path.GetDirectoryName(value);
                if (!string.IsNullOrWhiteSpace(directory)) result.Add(directory);
            }
            else
            {
                result.Add(value);
            }
        }
    }

    private static IReadOnlyList<string> GetTrustedFolders()
    {
        return
        [
            Path.Combine(GetMinecraftDirectory(), "runtime"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime"),
            Path.Combine(AppContext.BaseDirectory, "Launcher", "java"),
            JavaInstallRoot
        ];
    }

    private static string GetMinecraftDirectory()
    {
        try
        {
            if (App.Paths != null) return App.Paths.MinecraftDir;
        }
        catch { }
        return Path.Combine(AppContext.BaseDirectory, ".minecraft");
    }

    private static bool HasReparsePoint(string folder)
    {
        try
        {
            FileSystemInfo item = Directory.Exists(folder)
                ? new DirectoryInfo(folder)
                : new FileInfo(folder);
            if (item.Attributes.HasFlag(FileAttributes.ReparsePoint)) return true;

            DirectoryInfo? current = item is FileInfo file ? file.Directory : (DirectoryInfo)item;
            while (current != null)
            {
                if (current.Attributes.HasFlag(FileAttributes.ReparsePoint)) return true;
                current = current.Parent;
            }
        }
        catch
        {
            return true;
        }
        return false;
    }

    private static bool IsSpecialPath(string folder)
    {
        var normalized = folder.ToLowerInvariant();
        return normalized.Contains("java8path_target_")
            || normalized.Contains("javapath_target_")
            || normalized.Contains("javatmp")
            || normalized.Contains("system32");
    }

    private static bool IsPathWithin(string path, string parent)
    {
        try
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullParent, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
