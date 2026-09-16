using System.IO;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public static class MicrosoftAuthService
{
    public const string ClientId = "032800b7-b7a4-4cef-8b74-beda535e58af";
    private const string OAuthBase = "https://login.microsoftonline.com/consumers/oauth2/v2.0";
    private const string RedirectUri = "http://localhost";
    private const int RedirectPort = 80;
    private static readonly HttpClient Http = CreateClient();

    private sealed record OAuthTokens(string AccessToken, string RefreshToken);
    public sealed record MicrosoftProfile(string Name, string Uuid, string AccessToken, long ExpiresAt, string RefreshToken, string AvatarPath);

    public static IReadOnlyList<MicrosoftAccount> GetAccounts()
    {
        var settings = App.Settings.Data;
        MigrateCurrentAccount(settings);
        return settings.MicrosoftAccounts;
    }

    public static async Task<MicrosoftProfile> LoginWithBrowserAsync(
        CancellationToken cancellationToken = default)
    {
        var oauth = await AuthorizeInBrowserAsync(cancellationToken);
        return await ExchangeForMinecraftAsync(oauth, cancellationToken);
    }

    public static async Task EnsureSessionAsync()
    {
        var settings = App.Settings.Data;
        var account = GetActiveAccount(settings);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (settings.AuthMode != AuthModes.Microsoft || string.IsNullOrEmpty(settings.AuthUuid)) return;
        if (settings.MicrosoftTokenExpiresAt > now + 300) return;
        var protectedRefreshToken = account?.RefreshToken ?? settings.MicrosoftRefreshToken;
        if (string.IsNullOrEmpty(protectedRefreshToken))
            throw new Exception("正版登录已过期，请在设置中重新登录");

        string refreshToken;
        try
        {
            refreshToken = Unprotect(protectedRefreshToken);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            throw new Exception("正版登录凭据来自其他电脑或 Windows 用户，请在设置中重新登录");
        }
        using var content = Form(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["scope"] = "XboxLive.signin offline_access"
        });
        var data = await PostFormAsync($"{OAuthBase}/token", content);
        var oauth = new OAuthTokens(
            data.Value<string>("access_token") ?? throw new Exception("Microsoft 会话刷新失败"),
            data.Value<string>("refresh_token") ?? refreshToken);
        var profile = await ExchangeForMinecraftAsync(oauth);
        SaveProfile(profile);
    }

    public static void SaveProfile(MicrosoftProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.RefreshToken))
            throw new Exception("Microsoft 登录未返回刷新令牌，请重新登录");
        var settings = App.Settings.Data;
        MigrateCurrentAccount(settings);
        var account = settings.MicrosoftAccounts.FirstOrDefault(item =>
            item.Uuid.Equals(profile.Uuid, StringComparison.OrdinalIgnoreCase));
        if (account == null)
        {
            account = new MicrosoftAccount();
            settings.MicrosoftAccounts.Add(account);
        }

        account.PlayerName = profile.Name;
        account.Uuid = profile.Uuid;
        account.AccessToken = profile.AccessToken;
        account.TokenExpiresAt = profile.ExpiresAt;
        account.RefreshToken = Protect(profile.RefreshToken);
        account.AvatarPath = profile.AvatarPath;
        settings.ActiveMicrosoftAccountId = account.Id;
        ApplyAccount(settings, account);
        App.Settings.Save();
    }

    public static void UseAccount(string accountId)
    {
        var settings = App.Settings.Data;
        MigrateCurrentAccount(settings);
        var account = settings.MicrosoftAccounts.FirstOrDefault(item => item.Id == accountId)
                      ?? throw new Exception("找不到所选正版账户");
        settings.ActiveMicrosoftAccountId = account.Id;
        ApplyAccount(settings, account);
        App.Settings.Save();
    }

    public static void RemoveAccount(string accountId)
    {
        var settings = App.Settings.Data;
        MigrateCurrentAccount(settings);
        var wasCurrent = settings.AuthMode == AuthModes.Microsoft
                         && settings.ActiveMicrosoftAccountId == accountId;
        settings.MicrosoftAccounts.RemoveAll(item => item.Id == accountId);
        if (settings.ActiveMicrosoftAccountId == accountId)
        {
            var replacement = settings.MicrosoftAccounts.FirstOrDefault();
            settings.ActiveMicrosoftAccountId = replacement?.Id ?? "";
            if (!wasCurrent)
            {
                App.Settings.Save();
                return;
            }

            if (replacement == null)
            {
                settings.AuthMode = AuthModes.Offline;
                settings.AuthPlayerName = "";
                settings.AuthUuid = "";
                settings.AuthAccessToken = "";
                settings.MicrosoftRefreshToken = "";
                settings.MicrosoftTokenExpiresAt = 0;
                settings.AuthAvatarPath = "";
            }
            else
            {
                ApplyAccount(settings, replacement);
            }
        }
        App.Settings.Save();
    }

    private static void MigrateCurrentAccount(Settings settings)
    {
        settings.MicrosoftAccounts ??= new List<MicrosoftAccount>();
        if (settings.MicrosoftAccounts.Count > 0 || settings.AuthMode != AuthModes.Microsoft
            || string.IsNullOrWhiteSpace(settings.AuthUuid)) return;

        var account = new MicrosoftAccount
        {
            PlayerName = settings.AuthPlayerName,
            Uuid = settings.AuthUuid,
            AccessToken = settings.AuthAccessToken,
            TokenExpiresAt = settings.MicrosoftTokenExpiresAt,
            RefreshToken = settings.MicrosoftRefreshToken,
            AvatarPath = settings.AuthAvatarPath
        };
        settings.MicrosoftAccounts.Add(account);
        settings.ActiveMicrosoftAccountId = account.Id;
        App.Settings.Save();
    }

    private static MicrosoftAccount? GetActiveAccount(Settings settings)
    {
        MigrateCurrentAccount(settings);
        return settings.MicrosoftAccounts.FirstOrDefault(item => item.Id == settings.ActiveMicrosoftAccountId)
               ?? settings.MicrosoftAccounts.FirstOrDefault(item =>
                   item.Uuid.Equals(settings.AuthUuid, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyAccount(Settings settings, MicrosoftAccount account)
    {
        settings.AuthMode = AuthModes.Microsoft;
        settings.AuthAccount = "Microsoft Account";
        settings.AuthPlayerName = account.PlayerName;
        settings.AuthUuid = account.Uuid;
        settings.AuthAccessToken = account.AccessToken;
        settings.AuthClientToken = "";
        settings.AuthInjectorPath = "";
        settings.AuthAvatarPath = account.AvatarPath;
        settings.MicrosoftTokenExpiresAt = account.TokenExpiresAt;
        settings.MicrosoftRefreshToken = account.RefreshToken;
    }

    private static async Task<OAuthTokens> AuthorizeInBrowserAsync(CancellationToken cancellationToken)
    {
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizationUrl = $"{OAuthBase}/authorize?" + BuildQuery(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri,
            ["response_mode"] = "query",
            ["scope"] = "XboxLive.signin offline_access",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
            ["prompt"] = "select_account"
        });

        using var listener = new TcpListener(IPAddress.IPv6Loopback, RedirectPort);
        listener.Server.DualMode = true;
        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            throw new IOException("无法启动 Microsoft 登录回调，因为本机 80 端口已被占用。", ex);
        }

        try
        {
            ExternalOpenService.OpenUrl(authorizationUrl);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            TcpClient callbackClient;
            try
            {
                callbackClient = await listener.AcceptTcpClientAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Microsoft 网页授权超时，请重试。");
            }

            using (callbackClient)
            using (var stream = callbackClient.GetStream())
            {
                var callback = await ReadBrowserCallbackAsync(stream, timeout.Token);
                var returnedState = callback.GetValueOrDefault("state") ?? "";
                var error = callback.GetValueOrDefault("error");
                var errorDescription = callback.GetValueOrDefault("error_description");
                var code = callback.GetValueOrDefault("code");
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(returnedState), Encoding.UTF8.GetBytes(state)))
            {
                await WriteBrowserResultAsync(stream, false, "登录状态校验失败，请返回启动器重试。", timeout.Token);
                throw new Exception("Microsoft 登录状态校验失败");
            }
            if (!string.IsNullOrWhiteSpace(error))
            {
                await WriteBrowserResultAsync(stream, false, "授权未完成，请返回启动器查看详情。", timeout.Token);
                throw new Exception(errorDescription ?? error);
            }
            if (string.IsNullOrWhiteSpace(code))
            {
                await WriteBrowserResultAsync(stream, false, "未收到授权结果，请返回启动器重试。", timeout.Token);
                throw new Exception("Microsoft 未返回授权码");
            }

            await WriteBrowserResultAsync(stream, true, "授权完成，可以关闭此页面并返回启动器。", timeout.Token);
            using var content = Form(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = verifier,
                ["scope"] = "XboxLive.signin offline_access"
            });
            var token = await PostFormAsync($"{OAuthBase}/token", content, cancellationToken);
            return new OAuthTokens(
                token.Value<string>("access_token") ?? throw new Exception("Microsoft 未返回访问令牌"),
                token.Value<string>("refresh_token") ?? throw new Exception("Microsoft 未返回刷新令牌"));
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<MicrosoftProfile> ExchangeForMinecraftAsync(OAuthTokens oauth, CancellationToken cancellationToken = default)
    {
        var xbox = await PostJsonAsync("https://user.auth.xboxlive.com/user/authenticate", new
        {
            Properties = new { AuthMethod = "RPS", SiteName = "user.auth.xboxlive.com", RpsTicket = "d=" + oauth.AccessToken },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        }, cancellationToken);
        var xboxToken = xbox.Value<string>("Token") ?? throw new Exception("Xbox Live 登录失败");

        var xsts = await PostJsonAsync("https://xsts.auth.xboxlive.com/xsts/authorize", new
        {
            Properties = new { SandboxId = "RETAIL", UserTokens = new[] { xboxToken } },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        }, cancellationToken);
        var xstsToken = xsts.Value<string>("Token") ?? throw new Exception("XSTS 授权失败");
        var userHash = xsts["DisplayClaims"]?["xui"]?.First?["uhs"]?.ToString()
                       ?? throw new Exception("XSTS 未返回用户标识");

        var minecraft = await PostJsonAsync("https://api.minecraftservices.com/authentication/login_with_xbox", new
        {
            identityToken = $"XBL3.0 x={userHash};{xstsToken}"
        }, cancellationToken);
        var minecraftToken = minecraft.Value<string>("access_token") ?? throw new Exception("Minecraft 登录失败");
        var expiresIn = minecraft.Value<int?>("expires_in") ?? 86400;

        var entitlements = await GetJsonAsync("https://api.minecraftservices.com/entitlements/mcstore", minecraftToken, cancellationToken);
        if (entitlements["items"] is not JArray { Count: > 0 })
            throw new Exception("此 Microsoft 账户未拥有 Minecraft: Java Edition");

        var profile = await GetJsonAsync("https://api.minecraftservices.com/minecraft/profile", minecraftToken, cancellationToken);
        var uuid = profile.Value<string>("id") ?? throw new Exception("未找到 Minecraft Java 档案");
        var name = profile.Value<string>("name") ?? throw new Exception("未找到正版玩家名");
        var avatar = await AuthService.FetchAvatarAsync(uuid);
        return new MicrosoftProfile(name, uuid, minecraftToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn, oauth.RefreshToken, avatar);
    }

    private static async Task<JObject> PostFormAsync(
        string url, HttpContent content, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync();
        var data = JObject.Parse(body);
        if (!response.IsSuccessStatusCode)
            throw new Exception(data.Value<string>("error_description") ?? data.Value<string>("error") ?? $"HTTP {(int)response.StatusCode}");
        return data;
    }

    private static async Task<JObject> PostJsonAsync(string url, object payload, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");
        var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden &&
                url.Contains("api.minecraftservices.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    "Minecraft Services 拒绝了此登录。请移除已保存的正版账户后重新授权；" +
                    "如果应用刚通过审核，请等待权限同步后重试。客户端 ID: " + ClientId);
            }
            var error = JObject.Parse(body);
            var xerr = error.Value<long?>("XErr");
            throw new Exception(xerr switch
            {
                2148916233 => "此 Microsoft 账户没有 Xbox 档案",
                2148916238 => "此账户是未成年人账户，需要家庭组授权",
                _ => error.Value<string>("Message") ?? $"认证服务返回 HTTP {(int)response.StatusCode}"
            });
        }
        return JObject.Parse(body);
    }

    private static async Task<JObject> GetJsonAsync(string url, string bearerToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new Exception(
                    "Minecraft Services 拒绝访问正版档案。请重新授权账户；" +
                    "如果应用刚通过审核，请等待权限同步后重试。");
            throw new Exception($"Minecraft 服务返回 HTTP {(int)response.StatusCode}");
        }
        return JObject.Parse(body);
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> values) =>
        string.Join("&", values.Select(value =>
            $"{Uri.EscapeDataString(value.Key)}={Uri.EscapeDataString(value.Value)}"));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task<Dictionary<string, string?>> ReadBrowserCallbackAsync(
        Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken)
                           ?? throw new Exception("未收到 Microsoft 网页授权回调");
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
        {
        }

        var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !parts[0].Equals("GET", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Microsoft 网页授权回调格式无效");

        var target = parts[1].StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(parts[1])
            : new Uri("http://localhost" + parts[1]);
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in target.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('=');
            var key = separator >= 0 ? item[..separator] : item;
            var value = separator >= 0 ? item[(separator + 1)..] : "";
            query[DecodeQueryValue(key)] = DecodeQueryValue(value);
        }
        return query;
    }

    private static string DecodeQueryValue(string value) =>
        Uri.UnescapeDataString(value.Replace('+', ' '));

    private static async Task WriteBrowserResultAsync(
        Stream stream, bool success, string message, CancellationToken cancellationToken)
    {
        var color = success ? "#18794e" : "#b42318";
        var title = success ? "Microsoft 登录完成" : "Microsoft 登录未完成";
        var html = $$"""
                    <!doctype html>
                    <html lang="zh-CN">
                    <head>
                      <meta charset="utf-8">
                      <meta name="viewport" content="width=device-width,initial-scale=1">
                      <title>{{title}}</title>
                      <style>
                        body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f4f6f8; color: #1f2937; font-family: "Segoe UI", "Microsoft YaHei", sans-serif; }
                        main { width: min(420px, calc(100vw - 40px)); box-sizing: border-box; padding: 28px; background: white; border: 1px solid #d8dee6; border-radius: 8px; }
                        h1 { margin: 0 0 12px; color: {{color}}; font-size: 22px; letter-spacing: 0; }
                        p { margin: 0; line-height: 1.7; font-size: 14px; }
                      </style>
                    </head>
                    <body><main><h1>{{title}}</h1><p>{{WebUtility.HtmlEncode(message)}}</p></main></body>
                    </html>
                    """;
        var bytes = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static string Protect(string value) => Convert.ToBase64String(
        ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));

    private static string Unprotect(string value) => Encoding.UTF8.GetString(
        ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser));

    private static HttpClient CreateClient()
    {
        var client = HttpClients.Create(TimeSpan.FromSeconds(30));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QuartzLauncher/1.0");
        return client;
    }
}
