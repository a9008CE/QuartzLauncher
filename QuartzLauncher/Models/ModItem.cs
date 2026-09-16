namespace QuartzLauncher.Models;

public enum ModSource
{
    Modrinth,
    CurseForge,
    MCmod
}

public sealed class McmodInfoItem
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

public class ModItem
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string McmodId { get; set; } = "";
    public string McmodPageUrl { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> DetailImageUrls { get; set; } = new();
    public string DetailHtml { get; set; } = "";
    public string McmodStatus { get; set; } = "";
    public string McmodSourceType { get; set; } = "";
    public List<McmodInfoItem> McmodInternalInfo { get; set; } = new();
    public long Downloads { get; set; }
    public string IconUrl { get; set; } = "";
    public string PageUrl { get; set; } = "";
    public List<string> Authors { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<string> Versions { get; set; } = new();
    public List<string> Loaders { get; set; } = new();
    public ModSource Source { get; set; }
}

public class ModVersionItem
{
    public string ProjectId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string VersionNumber { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public List<string> GameVersions { get; set; } = new();
    public string Loader { get; set; } = "";
    public List<string> Loaders { get; set; } = new();
    public long FileSize { get; set; }
    public string Sha1 { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string FileName { get; set; } = "";
    public long DateUploaded { get; set; }
    public ModSource Source { get; set; }
    public List<ModDependency> Dependencies { get; set; } = new();
}

public class ModDependency
{
    public string ProjectId { get; set; } = "";
    public string ProjectSlug { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string VersionId { get; set; } = "";
    public string VersionNumber { get; set; } = "";
    public string Type { get; set; } = ""; // required/optional/embedded/incompatible
    public ModSource Source { get; set; }
}
