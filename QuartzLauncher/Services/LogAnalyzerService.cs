using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;
using Newtonsoft.Json;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public sealed record RecentLogFile(string Path, string DisplayName, DateTime LastWriteTime);

public class LogAnalyzer
{
    public static IReadOnlyList<RecentLogFile> GetRecentLogs(string root, int limit = 15)
    {
        if (!Directory.Exists(root) || limit <= 0) return Array.Empty<RecentLogFile>();

        var files = new List<string>();
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root, "logs", SearchOption.AllDirectories))
                files.AddRange(Directory.EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly));
            foreach (var directory in Directory.EnumerateDirectories(root, "crash-reports", SearchOption.AllDirectories))
            {
                files.AddRange(Directory.EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly));
                files.AddRange(Directory.EnumerateFiles(directory, "*.txt", SearchOption.TopDirectoryOnly));
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new
            {
                Path = path,
                LastWriteTime = File.GetLastWriteTime(path)
            })
            .OrderByDescending(item => item.LastWriteTime)
            .Take(limit)
            .Select(item => new RecentLogFile(
                item.Path,
                $"{Path.GetFileName(item.Path)} · {item.LastWriteTime:yyyy-MM-dd HH:mm:ss} · {GetRelativePath(root, item.Path)}",
                item.LastWriteTime))
            .ToArray();
    }

    public static string ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Decode(bytes);
    }

    public AnalysisReport Analyze(string instanceRoot, string instanceName, string liveLog = "")
    {
        var sources = new List<string>();
        var chunks = new List<string>();

        var logsDir = Path.Combine(instanceRoot, "logs");
        var crashDir = Path.Combine(instanceRoot, "crash-reports");

        foreach (var candidate in new[]
        {
            Path.Combine(logsDir, "latest.log"),
            Path.Combine(logsDir, "debug.log")
        })
        {
            if (!File.Exists(candidate)) continue;
            var bytes = File.ReadAllBytes(candidate);
            var tail = bytes.Length > 2 * 1024 * 1024 ? bytes[^ (2 * 1024 * 1024)..] : bytes;
            chunks.Add(Decode(tail));
            sources.Add(candidate);
        }

        if (!string.IsNullOrWhiteSpace(liveLog))
        {
            sources.Add("Live log");
            chunks.Add(liveLog.Length > 2 * 1024 * 1024 ? liveLog[^ (2 * 1024 * 1024)..] : liveLog);
        }

        var text = string.Join("\n", chunks);
        var findings = AnalyzeText(text);
        findings.AddRange(AnalyzeMods(instanceRoot, instanceName));
        findings = Deduplicate(findings);

        if (findings.Count == 0)
        {
            var title = string.IsNullOrEmpty(text) ? "未找到日志" : "未发现异常";
            var evidence = string.IsNullOrEmpty(text) ? "当前实例还没有生成日志。" : "最近日志未发现已知异常。";
            findings.Add(new Finding("Info", title, evidence,
                string.IsNullOrEmpty(text) ? "请先启动一次游戏。" : "日志检查完成，未发现异常。"));
        }

        findings.Sort((a, b) => SeverityOrder(a.Severity).CompareTo(SeverityOrder(b.Severity)));
        return new AnalysisReport(instanceName, sources.ToArray(), findings.ToArray());
    }

    private static int SeverityOrder(string s) => s switch
    {
        "Critical" => 0, "严重" => 0,
        "Warning" => 1, "警告" => 1,
        _ => 2
    };

    private static string Decode(byte[] data)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(data);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("gb18030").GetString(data);
        }
    }

    private static string GetRelativePath(string root, string path)
    {
        try
        {
            return Path.GetRelativePath(root, path);
        }
        catch
        {
            return path;
        }
    }

    private List<Finding> AnalyzeText(string text)
    {
        var findings = new List<Finding>();
        var checks = new (string Pattern, string Severity, string Title, string Evidence, string Solution)[]
        {
            (@"Could not find or load main class", "严重", "找不到主类（客户端核心文件缺失）", "Java 无法从客户端 JAR 中加载主类。", "运行版本修复，并确认客户端 JAR 文件存在。"),
            (@"OutOfMemoryError|Java heap space|GC overhead limit", "严重", "内存不足（Java 堆内存耗尽）", "日志中出现 OutOfMemoryError。", "在启动器设置中增大最大内存（建议 4–8 GB），并确保使用 64 位 Java。"),
            (@"UnsupportedClassVersionError", "严重", "Java 版本不兼容", "当前 Java 无法读取目标字节码版本。", "检查该 Minecraft 版本所需的 Java 版本，并在设置中切换到对应 Java。"),
            (@"Mixin apply failed|MixinApplyError|InjectionError", "严重", "模组冲突（Mixin 注入失败）", "某个模组的 Mixin 注入失败。", "升级或移除发生冲突的模组。"),
            (@"NoClassDefFoundError|ClassNotFoundException", "严重", "缺失类或前置依赖", "日志中出现 ClassNotFound。", "安装缺失的前置模组或依赖库。"),
            (@"AccessDeniedException|Permission denied", "Warning", "文件访问被拒绝", "游戏无法读写某个文件。", "关闭占用该文件的软件，并检查目录读写权限。"),
            (@"ZipException|invalid LOC header", "严重", "JAR 或资源包损坏", "ZIP/JAR 结构已损坏。", "删除损坏的文件后重新下载。"),
            (@"GLFW error|OpenGL.*not supported", "严重", "显卡驱动不兼容", "Minecraft 无法创建 OpenGL 上下文。", "更新显卡驱动，并确认显卡支持所需的 OpenGL 版本。"),
        };

        foreach (var (pattern, severity, title, evidence, solution) in checks)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var context = GetContext(text, match.Index);
                findings.Add(new Finding(severity, title, context, solution));
            }
        }
        return findings;
    }

    private List<Finding> AnalyzeMods(string root, string instanceName)
    {
        var findings = new List<Finding>();
        var modsDir = Path.Combine(root, "mods");
        if (!Directory.Exists(modsDir)) return findings;
        return findings;
    }

    private static string GetContext(string text, int position)
    {
        var start = Math.Max(0, text.LastIndexOf('\n', Math.Max(0, position - 240)));
        var end = text.IndexOf('\n', Math.Min(text.Length, position + 360));
        return text[(start + 1)..(end >= 0 ? end : text.Length)].Trim();
    }

    private static List<Finding> Deduplicate(List<Finding> findings)
    {
        var seen = new HashSet<(string, string)>();
        return findings.Where(f => seen.Add((f.Title, f.Evidence))).ToList();
    }
}
