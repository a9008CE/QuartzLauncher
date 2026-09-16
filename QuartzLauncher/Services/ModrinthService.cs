using System.Net.Http;
using Newtonsoft.Json.Linq;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public static class ModrinthService
{
    private static readonly HttpClient Http = HttpClients.Create(TimeSpan.FromSeconds(30));
    private const string BaseUrl = "https://api.modrinth.com/v2";

    public static async Task<List<ModItem>> SearchModsAsync(string keyword, string? gameVersion = null, string? loader = null, int count = 20, string projectType = "mod", string? category = null, int offset = 0)
    {
        try
        {
            var facets = new List<string> { $"[\"project_type:{projectType}\"]" };
            if (!string.IsNullOrEmpty(gameVersion))
                facets.Add($"[\"versions:{gameVersion}\"]");
            if (!string.IsNullOrEmpty(loader))
                facets.Add($"[\"categories:{loader.ToLower()}\"]");
            if (!string.IsNullOrEmpty(category))
                facets.Add($"[\"categories:{category.ToLower()}\"]");

            var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(keyword)}&limit={count}";
            if (offset > 0) url += $"&offset={offset}";
            if (facets.Count > 0)
                url += $"&facets=[{string.Join(",", facets)}]";

            var json = await Http.GetStringAsync(url);
            var obj = JObject.Parse(json);
            var hits = obj["hits"] ?? new JArray();
            return hits.Select(ParseModItem).ToList();
        }
        catch
        {
            return new();
        }
    }

    public static async Task<(List<ModItem> Items, int Total)> SearchPageAsync(string keyword, string? gameVersion, string? loader, int count, string projectType, string? category, int offset)
    {
        try
        {
            var facets = new List<string> { $"[\"project_type:{projectType}\"]" };
            if (!string.IsNullOrEmpty(gameVersion))
                facets.Add($"[\"versions:{gameVersion}\"]");
            if (!string.IsNullOrEmpty(loader))
                facets.Add($"[\"categories:{loader.ToLower()}\"]");
            if (!string.IsNullOrEmpty(category))
                facets.Add($"[\"categories:{category.ToLower()}\"]");

            var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(keyword)}&limit={count}&offset={offset}";
            url += $"&facets=[{string.Join(",", facets)}]";
            var obj = JObject.Parse(await Http.GetStringAsync(url));
            var hits = (obj["hits"] as JArray) ?? new JArray();
            var total = obj["total_hits"]?.ToObject<int>() ?? 0;
            return (hits.Select(ParseModItem).ToList(), total);
        }
        catch
        {
            return (new(), 0);
        }
    }

    public static async Task<(List<ModItem> Items, int Total)> GetPopularPageAsync(int count, string projectType, string? category, int offset, string? gameVersion = null)
    {
        try
        {
            var facets = new List<string> { $"[\"project_type:{projectType}\"]" };
            if (!string.IsNullOrEmpty(gameVersion))
                facets.Add($"[\"versions:{gameVersion}\"]");
            if (!string.IsNullOrEmpty(category))
                facets.Add($"[\"categories:{category.ToLower()}\"]");
            var url = $"{BaseUrl}/search?limit={count}&offset={offset}&facets=[{string.Join(",", facets)}]&index=downloads";
            var obj = JObject.Parse(await Http.GetStringAsync(url));
            var hits = (obj["hits"] as JArray) ?? new JArray();
            var total = obj["total_hits"]?.ToObject<int>() ?? 0;
            return (hits.Select(ParseModItem).ToList(), total);
        }
        catch
        {
            return (new(), 0);
        }
    }

    public static async Task<ModItem?> GetModAsync(string id)
    {
        try
        {
            var json = await Http.GetStringAsync($"{BaseUrl}/project/{id}");
            return ParseModItemFull(JObject.Parse(json));
        }
        catch
        {
            return null;
        }
    }

    public static async Task<List<ModVersionItem>> GetVersionsAsync(string projectId, string? gameVersion = null, string? loader = null)
    {
        try
        {
            var versions = new List<ModVersionItem>();
            const int pageSize = 100;
            var offset = 0;
            while (true)
            {
                var url = $"{BaseUrl}/project/{projectId}/version?limit={pageSize}&offset={offset}&include_changelog=false";
                var arr = JArray.Parse(await Http.GetStringAsync(url));
                if (arr.Count == 0) break;

                foreach (var v in arr)
                {
                    var gv = v["game_versions"]?.ToObject<List<string>>() ?? new();
                    var ld = v["loaders"]?.ToObject<List<string>>() ?? new();

                    if (!string.IsNullOrEmpty(gameVersion) && !gv.Contains(gameVersion)) continue;
                    if (!string.IsNullOrEmpty(loader) && !ld.Contains(loader.ToLowerInvariant())) continue;

                    var files = v["files"]?.ToObject<List<JObject>>() ?? new();
                    if (files.Count == 0) continue;
                    var uploaded = DateTimeOffset.TryParse(v["date_published"]?.ToString(), out var date)
                        ? date.ToUnixTimeSeconds()
                        : 0;
                    var dependencies = (v["dependencies"] as JArray)?.OfType<JObject>()
                        .Select(node => new ModDependency
                        {
                            ProjectId = node.Value<string>("project_id") ?? "",
                            ProjectSlug = node.Value<string>("project_id") ?? "",
                            ProjectName = node.Value<string>("project_name") ?? "",
                            VersionId = node.Value<string>("version_id") ?? "",
                            VersionNumber = node.Value<string>("version_number") ?? "",
                            Type = node.Value<string>("dependency_type") ?? "",
                            Source = ModSource.Modrinth
                        }).ToList() ?? new();
                    foreach (var file in files)
                    {
                        versions.Add(new ModVersionItem
                        {
                            ProjectId = projectId,
                            Id = v["id"]?.ToString() ?? "",
                            Name = v["name"]?.ToString() ?? "",
                            VersionNumber = v["version_number"]?.ToString() ?? "",
                            GameVersion = gv.FirstOrDefault() ?? "",
                            GameVersions = gv,
                            Loader = ld.FirstOrDefault() ?? "",
                            Loaders = ld,
                            FileSize = file["size"]?.ToObject<long>() ?? 0,
                            Sha1 = file["hashes"]?["sha1"]?.ToString() ?? "",
                            DownloadUrl = file["url"]?.ToString() ?? "",
                            FileName = file["filename"]?.ToString() ?? "",
                            DateUploaded = uploaded,
                            Source = ModSource.Modrinth,
                            Dependencies = dependencies
                        });
                    }
                }

                if (arr.Count < pageSize) break;
                offset += arr.Count;
            }

            return versions;
        }
        catch
        {
            return new();
        }
    }

    public static async Task<ModVersionItem?> GetVersionAsync(string versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId)) return null;
        try
        {
            var version = JObject.Parse(await Http.GetStringAsync($"{BaseUrl}/version/{versionId}"));
            var files = version["files"]?.OfType<JObject>().ToList() ?? new();
            var file = files.FirstOrDefault(item => item.Value<bool?>("primary") == true)
                       ?? files.FirstOrDefault();
            if (file == null) return null;
            var gameVersions = version["game_versions"]?.ToObject<List<string>>() ?? new();
            var loaders = version["loaders"]?.ToObject<List<string>>() ?? new();
            var uploaded = DateTimeOffset.TryParse(version["date_published"]?.ToString(), out var date)
                ? date.ToUnixTimeSeconds()
                : 0;
            return new ModVersionItem
            {
                ProjectId = version.Value<string>("project_id") ?? "",
                Id = version.Value<string>("id") ?? versionId,
                Name = version.Value<string>("name") ?? "",
                VersionNumber = version.Value<string>("version_number") ?? "",
                GameVersion = gameVersions.FirstOrDefault() ?? "",
                GameVersions = gameVersions,
                Loader = loaders.FirstOrDefault() ?? "",
                Loaders = loaders,
                FileSize = file.Value<long?>("size") ?? 0,
                Sha1 = file["hashes"]?["sha1"]?.ToString() ?? "",
                DownloadUrl = file.Value<string>("url") ?? "",
                FileName = file.Value<string>("filename") ?? "",
                DateUploaded = uploaded,
                Source = ModSource.Modrinth,
                Dependencies = ParseDependencies(version)
            };
        }
        catch
        {
            return null;
        }
    }

    public static async Task<List<ModItem>> GetPopularModsAsync(int count = 20, string projectType = "mod", string? category = null, int offset = 0)
    {
        try
        {
            var facets = new List<string> { $"[\"project_type:{projectType}\"]" };
            if (!string.IsNullOrEmpty(category))
                facets.Add($"[\"categories:{category.ToLower()}\"]");
            var url = $"{BaseUrl}/search?limit={count}&facets=[{string.Join(",", facets)}]&index=downloads";
            if (offset > 0) url += $"&offset={offset}";
            var json = await Http.GetStringAsync(url);
            var obj = JObject.Parse(json);
            var hits = obj["hits"] ?? new JArray();
            return hits.Select(ParseModItem).ToList();
        }
        catch
        {
            return new();
        }
    }


    private static ModItem ParseModItem(JToken hit)
    {
        return new ModItem
        {
            Id = hit["project_id"]?.ToString() ?? hit["slug"]?.ToString() ?? "",
            Slug = hit["slug"]?.ToString() ?? "",
            Name = hit["title"]?.ToString() ?? "",
            Summary = hit["description"]?.ToString() ?? "",
            Downloads = hit["downloads"]?.ToObject<long>() ?? 0,
            IconUrl = hit["icon_url"]?.ToString() ?? "",
            PageUrl = $"https://modrinth.com/mod/{hit["slug"]}",
            Authors = new List<string>(),
            Categories = hit["categories"]?.ToObject<List<string>>() ?? new(),
            Versions = hit["versions"]?.ToObject<List<string>>() ?? new(),
            Loaders = hit["loaders"]?.ToObject<List<string>>() ?? new(),
            Source = ModSource.Modrinth
        };
    }

    private static List<ModDependency> ParseDependencies(JToken version) =>
        (version["dependencies"] as JArray)?.OfType<JObject>()
        .Select(node => new ModDependency
        {
            ProjectId = node.Value<string>("project_id") ?? "",
            ProjectSlug = node.Value<string>("project_id") ?? "",
            ProjectName = node.Value<string>("project_name") ?? "",
            VersionId = node.Value<string>("version_id") ?? "",
            VersionNumber = node.Value<string>("version_number") ?? "",
            Type = node.Value<string>("dependency_type") ?? "",
            Source = ModSource.Modrinth
        }).ToList() ?? new();

    private static ModItem ParseModItemFull(JObject obj)
    {
        return new ModItem
        {
            Id = obj["slug"]?.ToString() ?? "",
            Slug = obj["slug"]?.ToString() ?? "",
            Name = obj["title"]?.ToString() ?? "",
            Summary = obj["description"]?.ToString() ?? "",
            Description = obj["body"]?.ToString() ?? "",
            Downloads = obj["downloads"]?.ToObject<long>() ?? 0,
            IconUrl = obj["icon_url"]?.ToString() ?? "",
            PageUrl = $"https://modrinth.com/mod/{obj["slug"]}",
            Authors = obj["team"]?.ToObject<List<string>>() ?? new(),
            Categories = obj["categories"]?.ToObject<List<string>>() ?? new(),
            Versions = obj["versions"]?.ToObject<List<string>>() ?? new(),
            Loaders = obj["loaders"]?.ToObject<List<string>>() ?? new(),
            Source = ModSource.Modrinth
        };
    }
}
