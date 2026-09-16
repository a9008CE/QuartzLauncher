using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace QuartzLauncher.Services;

public sealed class UpdateManifest
{
    public string Version { get; set; } = "";
    public string PackageUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class UpdateAnnouncement
{
    public string Version { get; set; } = "";
    public string Notes { get; set; } = "";
}

public static class UpdateService
{
    public const string CurrentVersion = "1.0.1";
    public static string DisplayVersion => "星落 LaunCher 1.0.1";
    private static readonly HttpClient Http = HttpClients.Create(TimeSpan.FromSeconds(30));
    private const string PendingAnnouncementFileName = "update-announcement.pending.json";
    private const string AnnouncementFileName = "update-announcement.json";

    public static UpdateAnnouncement LoadAnnouncement()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Launcher", AnnouncementFileName);
        try
        {
            if (File.Exists(path))
            {
                var announcement = JsonConvert.DeserializeObject<UpdateAnnouncement>(File.ReadAllText(path));
                if (announcement != null && !string.IsNullOrWhiteSpace(announcement.Version))
                    return announcement;
            }
        }
        catch
        {
        }

        return new UpdateAnnouncement
        {
            Version = CurrentVersion,
            Notes = "星落 LaunCher 1.0.1\n\n【分析日志 · 焕新】 • 分析结果不再是一长串日志，直接给出「原因 + 解决方法」 • 输出完毕后自动汉化，全中文显示 • 联网 AI 分析同样只保留「原因 / 解决方法」两段\n\n【启动体验】 • 首页新增常驻启动进度条：未启动显示「请启动游戏」，启动后显示「游戏已启动」，崩溃时进度条变红并提示「游戏异常」 • 启动时始终自动检查更新，不再依赖手动开关\n\n【主题与动画】 • 重做深浅色 / UI 风格切换过渡，只保留一套干净的遮罩动画 • 修复切换时卡片提前变色的问题，现在卡片跟随动画一起变色 • UI 风格切换也接入同一套过渡动画\n\n【Mod 下载】 • 下载目录自动遵循版本隔离：开启隔离 → 实例独立 mods；未开启 → 公共 .minecraft/mods • 前置 Mod 自动匹配适配版本（游戏版本 + 加载器），不再拉最新版 • 下载确认框显示完整目标路径\n\n【界面优化】 • 移除玩家 UUID 显示"
        };
    }

    public static bool IsLocalServerRunning(int port = 29071)
    {
        try
        {
            using var tcp = new TcpClient();
            var task = tcp.ConnectAsync(IPAddress.Loopback, port);
            return task.Wait(1000) && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static void EnsureLocalServerRunning()
    {
        try
        {
            if (IsLocalServerRunning()) return;
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                executable = Path.Combine(AppContext.BaseDirectory, "QuartzLauncher.exe");
            if (!File.Exists(executable)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--update-server --listen any --port 29071",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            for (var i = 0; i < 25 && !IsLocalServerRunning(); i++)
                Thread.Sleep(200);
        }
        catch
        {
        }
    }

    public static async Task<UpdateManifest?> CheckAsync(string manifestUrl)
    {
        var manifestUri = GetHttpUri(manifestUrl, "更新清单地址");
        string json;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            json = await Http.GetStringAsync(manifestUri, cts.Token);
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException($"获取更新清单超时：{manifestUri}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"无法获取更新清单 {manifestUri}：{ex.Message}", ex);
        }

        UpdateManifest? manifest;
        try
        {
            manifest = JsonConvert.DeserializeObject<UpdateManifest>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("更新清单不是有效的 JSON。", ex);
        }
        if (manifest == null || !IsNewer(manifest.Version, CurrentVersion)) return null;
        if (string.IsNullOrWhiteSpace(manifest.PackageUrl)
            || !Uri.TryCreate(manifestUri, manifest.PackageUrl, out var packageUri)
            || (packageUri.Scheme != Uri.UriSchemeHttp && packageUri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidDataException($"更新清单中的补丁地址无效：{manifest.PackageUrl}");
        manifest.PackageUrl = packageUri.AbsoluteUri;
        return manifest;
    }

    public static async Task<string> DownloadPackageAsync(UpdateManifest manifest, string tempDir,
        IProgress<double>? progress = null)
    {
        var packageUri = GetHttpUri(manifest.PackageUrl, "更新补丁地址");
        Directory.CreateDirectory(tempDir);
        var packagePath = Path.Combine(tempDir, $"update-{Guid.NewGuid():N}.zip");
        HttpResponseMessage response;
        try
        {
            response = await Http.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"无法连接更新补丁地址 {packageUri}：{ex.Message}", ex);
        }
        using (response)
        {
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"获取更新补丁失败 {packageUri}：{ex.Message}", ex);
            }
            var total = response.Content.Headers.ContentLength ?? -1;
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(packagePath);
            var buffer = new byte[128 * 1024];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read));
                received += read;
                if (total > 0) progress?.Report((double)received / total * 100);
            }

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                await output.FlushAsync();
                output.Close();
                using var hashStream = File.OpenRead(packagePath);
                var hash = Convert.ToHexString(SHA256.HashData(hashStream));
                if (!hash.Equals(manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(packagePath);
                    throw new InvalidDataException("更新补丁校验失败。");
                }
            }
        }
        return packagePath;
    }

    public static void StartApplyAndExit(string newExePath, UpdateManifest? manifest = null)
    {
        var logFile = Path.Combine(AppContext.BaseDirectory, "Launcher", "update-apply.log");
        void Log(string msg)
        {
            try { File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n"); } catch { }
        }

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            executable = Path.Combine(AppContext.BaseDirectory, "QuartzLauncher.exe");
        if (!File.Exists(executable))
            throw new FileNotFoundException("找不到启动器可执行文件，无法启动更新器。", executable);

        Log($"准备更新：当前={executable}, 新版={newExePath}");

        if (manifest != null)
            SavePendingAnnouncement(manifest, App.Paths.Root);

        var updater = Path.Combine(App.Paths.Root, "temp", "QuartzLauncher-updater.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(updater)!);
        File.Copy(executable, updater, true);

        Process.Start(new ProcessStartInfo
        {
            FileName = updater,
            Arguments = $"--apply-update \"{newExePath}\" {Environment.ProcessId} \"{executable}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        });
        Log("更新器已启动，准备关闭当前启动器");
        App.Current.Shutdown();
    }

    public static void ApplyFromArguments(string[] args)
    {
        var logFile = Path.Combine(AppContext.BaseDirectory, "Launcher", "update-apply.log");
        void Log(string msg)
        {
            try { File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n"); } catch { }
        }

        if (args.Length < 4)
        {
            Log($"参数不足：args.Length={args.Length}");
            return;
        }
        var newExePath = args[1];
        _ = int.TryParse(args[2], out var parentPid);
        var currentExePath = Path.GetFullPath(args[3]);
        var baseDir = Path.GetDirectoryName(currentExePath) ?? AppContext.BaseDirectory;
        Log($"开始应用更新：newExe={newExePath}, parentPid={parentPid}, currentExe={currentExePath}");

        try
        {
            WaitForProcessExit(parentPid);
            Log("父进程已退出");

            if (!File.Exists(newExePath))
            {
                Log($"新版文件不存在：{newExePath}");
                return;
            }

            var backupPath = currentExePath + ".bak";
            try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch { }
            try { File.Move(currentExePath, backupPath); Log($"备份旧版本：{backupPath}"); } catch { }

            File.Copy(newExePath, currentExePath, true);
            Log("新版已替换");

            try { File.Delete(newExePath); } catch { }
            try { MovePendingAnnouncement(Path.Combine(baseDir, "Launcher")); } catch { }
        }
        catch (Exception ex)
        {
            Log($"更新应用失败：{ex}");
        }

        if (File.Exists(currentExePath))
        {
            Log($"启动新版本：{currentExePath}");
            Process.Start(new ProcessStartInfo(currentExePath) { WorkingDirectory = baseDir, UseShellExecute = true });
        }
        else
        {
            Log($"新版本不存在：{currentExePath}");
        }
    }

    public static void RunLocalServer(string[] args)
    {
        var (host, port, listenOnAny) = ParseServerArguments(args);
        var root = Path.Combine(AppContext.BaseDirectory, "Launcher", "updates");
        Directory.CreateDirectory(root);
        var prefix = $"http://{host}:{port}/";
        var messages = new List<string>
        {
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 更新服务地址：{prefix}",
            $"更新内容目录：{Path.GetFullPath(root)}"
        };
        if (listenOnAny)
        {
            messages.Add("局域网客户端地址：");
            messages.AddRange(GetLocalIPv4Addresses().Select(address => $"  http://{address}:{port}/update.json"));
        }
        else
        {
            messages.Add($"客户端更新清单地址：http://{host}:{port}/update.json");
        }
        LogServerMessages(root, messages);

        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5 && listenOnAny)
        {
            listener.Close();
            var fallbackPrefix = $"http://127.0.0.1:{port}/";
            LogServerMessages(root, new List<string>
            {
                $"[警告] 没有管理员权限，无法监听 {prefix}",
                $"[回退] 改为仅本地监听：{fallbackPrefix}"
            });
            listener = new HttpListener();
            listener.Prefixes.Add(fallbackPrefix);
            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex2)
            {
                listener.Close();
                LogServerMessages(root, new List<string> { $"[放弃] 本地更新服务启动失败：{ex2.Message}" });
                return;
            }
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            listener.Close();
            LogServerMessages(root, new List<string> { $"[放弃] 没有权限监听 {prefix}，跳过本地更新服务" });
            return;
        }
        catch (HttpListenerException ex) when (ex.ErrorCode is 32 or 183 or 10048)
        {
            listener.Close();
            LogServerMessages(root, new List<string> { $"[放弃] 端口 {port} 已被占用，跳过本地更新服务" });
            return;
        }
        catch (HttpListenerException ex)
        {
            listener.Close();
            LogServerMessages(root, new List<string> { $"[放弃] 无法启动更新服务 {prefix}：{ex.Message}" });
            return;
        }

        try
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                _ = Task.Run(() => ServeLocalFile(context, root));
            }
        }
        finally
        {
            listener.Close();
        }
    }

    private static (string Host, int Port, bool ListenOnAny) ParseServerArguments(string[] args)
    {
        var listen = "loopback";
        var port = 29071;
        var positionalPortSeen = false;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--listen", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("参数 --listen 缺少地址，可使用 loopback、127.0.0.1、本机 IPv4 或 any。");
                listen = args[i];
            }
            else if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || !int.TryParse(args[i], out port))
                    throw new ArgumentException("参数 --port 必须是 1 到 65535 的整数。");
            }
            else if (!args[i].StartsWith("--", StringComparison.Ordinal) && !positionalPortSeen
                     && int.TryParse(args[i], out port))
            {
                positionalPortSeen = true;
            }
            else
            {
                throw new ArgumentException($"无法识别更新服务参数：{args[i]}");
            }
        }

        if (port is < 1 or > 65535)
            throw new ArgumentException("更新服务端口必须在 1 到 65535 之间。");

        if (listen.Equals("any", StringComparison.OrdinalIgnoreCase))
            return ("+", port, true);
        if (listen.Equals("loopback", StringComparison.OrdinalIgnoreCase) || listen == "127.0.0.1")
            return ("127.0.0.1", port, false);
        if (!IPAddress.TryParse(listen, out var address) || address.AddressFamily != AddressFamily.InterNetwork
            || IPAddress.IsLoopback(address) || !GetLocalIPv4Addresses().Contains(address.ToString()))
            throw new ArgumentException(
                $"监听地址“{listen}”无效。请使用 loopback、127.0.0.1、本机活动 IPv4 地址或 any。");
        return (address.ToString(), port, false);
    }

    private static IReadOnlyList<string> GetLocalIPv4Addresses()
        => NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up
                              && network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Where(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetwork
                              && !IPAddress.IsLoopback(unicast.Address)
                              && !unicast.Address.Equals(IPAddress.Any))
            .Select(unicast => unicast.Address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void LogServerMessages(string root, IEnumerable<string> messages)
    {
        var text = string.Join(Environment.NewLine, messages);
        Console.WriteLine(text);
        File.AppendAllText(Path.Combine(root, "server.log"), text + Environment.NewLine);
    }

    private static void ServeLocalFile(HttpListenerContext context, string root)
    {
        try
        {
            var relative = Uri.UnescapeDataString(context.Request.Url?.AbsolutePath.TrimStart('/') ?? "");
            var file = Path.GetFullPath(Path.Combine(root, relative));
            var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            if (!file.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            context.Response.ContentType = Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? "application/json; charset=utf-8"
                : "application/octet-stream";
            var data = File.ReadAllBytes(file);
            context.Response.ContentLength64 = data.Length;
            context.Response.OutputStream.Write(data, 0, data.Length);
            context.Response.Close();
        }
        catch
        {
            try { context.Response.StatusCode = 500; context.Response.Close(); } catch { }
        }
    }

    private static bool IsNewer(string candidate, string current)
        => Version.TryParse(candidate.TrimStart('v', 'V'), out var next)
           && Version.TryParse(current.TrimStart('v', 'V'), out var installed)
           && next > installed;

    public static bool IsLoopbackManifestUrl(string value)
    {
        if (!TryGetHttpUri(value, out var uri) || uri is null) return false;
        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }

    public static bool TryGetHttpUri(string value, out Uri? uri)
    {
        var valid = Uri.TryCreate(value?.Trim(), UriKind.Absolute, out uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        if (!valid) uri = null;
        return valid;
    }

    private static Uri GetHttpUri(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"未配置{name}。");
        if (!TryGetHttpUri(value, out var uri))
            throw new InvalidOperationException($"{name}必须是完整的 HTTP 或 HTTPS URL。");
        return uri!;
    }

    private static void WaitForProcessExit(int pid)
    {
        if (pid <= 0) return;
        try
        {
            using var process = Process.GetProcessById(pid);
            process.WaitForExit(30000);
        }
        catch { }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    private static void SavePendingAnnouncement(UpdateManifest manifest, string launcherDir)
    {
        if (string.IsNullOrWhiteSpace(manifest.Version)) return;
        Directory.CreateDirectory(launcherDir);
        var path = Path.Combine(launcherDir, PendingAnnouncementFileName);
        var announcement = new UpdateAnnouncement
        {
            Version = manifest.Version,
            Notes = manifest.Notes
        };
        File.WriteAllText(path, JsonConvert.SerializeObject(announcement, Formatting.Indented));
    }

    private static void MovePendingAnnouncement(string launcherDir)
    {
        var pending = Path.Combine(launcherDir, PendingAnnouncementFileName);
        var current = Path.Combine(launcherDir, AnnouncementFileName);
        if (!File.Exists(pending)) return;

        // Replace the previous announcement so only the latest release remains visible.
        if (File.Exists(current))
            File.Delete(current);
        File.Move(pending, current);
    }
}
