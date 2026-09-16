using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public sealed class LoginService
{
    private readonly SettingsService _settings;

    public LoginService(SettingsService settings)
    {
        _settings = settings;
    }

    public void UseOffline(string playerName)
    {
        var name = playerName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("请输入玩家名。", nameof(playerName));

        _settings.Data.AuthMode = AuthModes.Offline;
        _settings.Data.AuthPlayerName = name;
        _settings.Data.PlayerName = name;
        _settings.Save();
    }

    public async Task<AuthProfile> LoginExternalAsync(string server, string account, string password)
    {
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(account))
            throw new ArgumentException("请填写认证服务器和账号。");

        var profile = await AuthService.AuthenticateAsync(server, account, password);
        _settings.Data.AuthMode = AuthModes.External;
        _settings.Data.AuthServer = profile.Server;
        _settings.Data.AuthAccount = profile.Account;
        _settings.Data.AuthPlayerName = profile.PlayerName;
        _settings.Data.AuthUuid = profile.Uuid;
        _settings.Data.AuthAccessToken = profile.AccessToken;
        _settings.Data.AuthClientToken = profile.ClientToken;
        _settings.Data.AuthInjectorPath = profile.InjectorPath;
        _settings.Data.AuthAvatarPath = profile.AvatarPath;
        _settings.Save();
        return profile;
    }
}
