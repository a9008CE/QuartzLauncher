using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuartzLauncher.Services;

public sealed record ServerDirectoryItem(
    string Id,
    string Name,
    string Description,
    string Players,
    string Ping,
    string CountryCode,
    string IconUrl,
    string PageUrl);

public sealed record ServerDirectoryResult(List<ServerDirectoryItem> Items, int Page, int TotalPages);

public static class MinecraftServerDirectoryService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Regex ItemRegex = new("<li class=\"list_item\">(?<item>.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex IdRegex = new("href=\"/sv(?<id>\\d+)\\.html\"", RegexOptions.IgnoreCase);
    private static readonly Regex IconRegex = new("class=\"favicon\" src=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex NameRegex = new("<h5 class=\"title\"[^>]*>(?<value>.*?)</h5>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex DescriptionRegex = new("<div class=\"description\"[^>]*>(?<value>.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex PlayerRegex = new("<div class=\"player\">(?<value>.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex PingRegex = new("data-original-title=\"(?<value>\\d+)ms\"", RegexOptions.IgnoreCase);
    private static readonly Regex CountryRegex = new("title=\"国家/地区:\\s*(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex PagesRegex = new("当前\\s*(?<page>\\d+)\\s*/\\s*(?<total>\\d+)\\s*页", RegexOptions.IgnoreCase);
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Singleline);

    public static async Task<ServerDirectoryResult> GetOnlineServersAsync(int page = 1)
    {
        var filter = JsonConvert.SerializeObject(new { page, showOffline = 0, showModonly = 0 });
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = filter });
        using var response = await Http.PostAsync("https://play.mcmod.cn/frame/serverList/", content);
        response.EnsureSuccessStatusCode();
        var payload = JObject.Parse(await response.Content.ReadAsStringAsync());
        if (payload.Value<int?>("state") != 0) return new ServerDirectoryResult(new(), page, page);

        var html = payload.Value<string>("html") ?? "";
        var results = new List<ServerDirectoryItem>();
        foreach (Match match in ItemRegex.Matches(html))
        {
            var block = match.Groups["item"].Value;
            var id = IdRegex.Match(block).Groups["id"].Value;
            var name = Clean(NameRegex.Match(block).Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
            var icon = WebUtility.HtmlDecode(IconRegex.Match(block).Groups["value"].Value);
            if (icon.StartsWith("//")) icon = "https:" + icon;
            results.Add(new ServerDirectoryItem(
                id,
                name,
                Clean(DescriptionRegex.Match(block).Groups["value"].Value),
                Clean(PlayerRegex.Match(block).Groups["value"].Value),
                PingRegex.Match(block).Groups["value"].Value,
                CountryRegex.Match(block).Groups["value"].Value.Trim().ToUpperInvariant(),
                icon,
                $"https://play.mcmod.cn/sv{id}.html"));
        }
        var pagesMatch = PagesRegex.Match(Clean(html));
        var totalPages = pagesMatch.Success && int.TryParse(pagesMatch.Groups["total"].Value, out var parsedTotal)
            ? parsedTotal
            : page;
        return new ServerDirectoryResult(results.Take(25).ToList(), page, totalPages);
    }

    private static string Clean(string html)
        => WebUtility.HtmlDecode(TagRegex.Replace(html, "")).Replace('\u00a0', ' ').Trim();

    private static HttpClient CreateClient()
    {
        var client = HttpClients.Create(TimeSpan.FromSeconds(20));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) QuartzLauncher/1.0");
        return client;
    }
}
