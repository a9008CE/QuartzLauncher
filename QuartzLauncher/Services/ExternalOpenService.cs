using System.Diagnostics;
using System.IO;

namespace QuartzLauncher.Services;

public static class ExternalOpenService
{
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("只允许打开 HTTP 或 HTTPS 地址。", nameof(url));

        foreach (var browser in BrowserCandidates())
        {
            if (!File.Exists(browser)) continue;
            Process.Start(new ProcessStartInfo
            {
                FileName = browser,
                Arguments = $"\"{uri.AbsoluteUri}\"",
                UseShellExecute = false
            });
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
            return;
        }
        catch (Exception ex)
        {
            throw new FileNotFoundException("无法打开网页，请确认系统已安装可用的浏览器。", url, ex);
        }
    }

    private static IEnumerable<string> BrowserCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe");
        yield return Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe");
        yield return Path.Combine(localAppData, "Microsoft", "Edge", "Application", "msedge.exe");
        yield return Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe");
    }
}
