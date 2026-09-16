namespace QuartzLauncher.Models;

public class MicrosoftAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PlayerName { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public long TokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = "";
    public string AvatarPath { get; set; } = "";
}
