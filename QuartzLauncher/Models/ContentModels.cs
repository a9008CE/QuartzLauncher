namespace QuartzLauncher.Models;

public record DownloadItem(string Url, string Target, string Sha1 = "", long Size = 0, string Sha256 = "");

public record ModInfo(
    string Path,
    string Name,
    string Version,
    string Loader,
    bool Enabled,
    string Description = "",
    string FileName = "",
    long FileSize = 0);

public record Finding(string Severity, string Title, string Evidence, string Solution);

public record AnalysisReport(string InstanceName, string[] Sources, Finding[] Findings);

public record AuthProfile(string Server, string Account, string PlayerName, string Uuid,
    string AccessToken, string ClientToken, string InjectorPath, string AvatarPath);
