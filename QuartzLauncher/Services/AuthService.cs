using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public static class AuthService
{
    private const string InjectorMetadata = "https://authlib-injector.yushi.moe/artifact/latest.json";

    public static string GenerateOfflineUuid(string playerName)
    {
        var md5 = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + playerName));
        var hex = Convert.ToHexString(md5).ToLower();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}";
    }

    public static async Task<string> FetchAvatarAsync(string uuid)
    {
        var clean = uuid.Replace("-", "").ToLower();
        var urls = new[]
        {
            $"https://crafatar.com/avatars/{clean}?size=64&default=MHF_Steve&overlay",
            $"https://mc-heads.net/avatar/{clean}/64"
        };
        using var http = HttpClients.Create(TimeSpan.FromSeconds(15));
        foreach (var url in urls)
        {
            try
            {
                var data = await http.GetByteArrayAsync(url);
                if (data.Length > 0)
                {
                    var cacheDir = Path.Combine(AppPaths.Default().CacheDir, "avatars");
                    Directory.CreateDirectory(cacheDir);
                    var target = Path.Combine(cacheDir, $"avatar-{clean}.png");
                    await File.WriteAllBytesAsync(target, data);
                    return target;
                }
            }
            catch { }
        }
        return "";
    }

    public static async Task<AuthProfile> AuthenticateAsync(string server, string account, string password)
    {
        server = NormalizeServer(server);
        var clientToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var payload = new
        {
            agent = new { name = "Minecraft", version = 1 },
            username = account.Trim(),
            password,
            clientToken,
            requestUser = true
        };
        var data = await PostJsonAsync(server + "/authserver/authenticate", payload);
        var profilesJson = data.ContainsKey("availableProfiles") ? JsonConvert.SerializeObject(data["availableProfiles"]) : "[]";
        var profiles = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(profilesJson) ?? new();
        Dictionary<string, object>? selected = null;
        if (data.ContainsKey("selectedProfile") && data["selectedProfile"] != null)
            selected = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(data["selectedProfile"]));

        if (profiles.Count == 1 && selected == null) selected = profiles[0];
        if (selected == null) throw new Exception("Authentication succeeded but no profile available");

        var accessToken = data["accessToken"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("No accessToken received");

        var injectorPath = await EnsureInjectorAsync();
        var profileId = selected["id"]?.ToString()?.Replace("-", "").ToLower() ?? "";
        var avatarPath = await FetchAvatarAsync(profileId);

        return new AuthProfile(
            Server: server,
            Account: account.Trim(),
            PlayerName: selected["name"]?.ToString() ?? account.Trim(),
            Uuid: profileId,
            AccessToken: accessToken,
            ClientToken: data["clientToken"]?.ToString() ?? clientToken,
            InjectorPath: injectorPath,
            AvatarPath: avatarPath
        );
    }

    public static async Task<string> EnsureInjectorAsync()
    {
        using var http = HttpClients.Create(TimeSpan.FromSeconds(120));
        var json = await http.GetStringAsync(InjectorMetadata);
        var meta = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        var downloadUrl = meta?["download_url"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(downloadUrl)) throw new Exception("Cannot get authlib-injector download URL");

        var cacheDir = AppPaths.Default().CacheDir;
        Directory.CreateDirectory(cacheDir);
        var target = Path.Combine(cacheDir, "authlib-injector.jar");
        if (File.Exists(target)) return target;

        var data = await http.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(target, data);
        return target;
    }

    public static string NormalizeServer(string server)
    {
        var value = server.Trim().TrimEnd('/');
        var uri = new Uri(value);
        if (uri.Scheme != "https" || string.IsNullOrEmpty(uri.Host))
            throw new Exception("Auth API must be a valid HTTPS address");
        return value;
    }

    private static async Task<Dictionary<string, object>> PostJsonAsync(string url, object payload)
    {
        using var http = HttpClients.Create(TimeSpan.FromSeconds(30));
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await http.PostAsync(url, content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var errorData = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            throw new Exception(errorData?["errorMessage"]?.ToString() ?? $"HTTP {(int)response.StatusCode}");
        }
        return JsonConvert.DeserializeObject<Dictionary<string, object>>(body) ?? new();
    }
}
