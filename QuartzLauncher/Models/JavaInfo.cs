using Newtonsoft.Json;

namespace QuartzLauncher.Models;

public class JavaInfo
{
    public string Path { get; set; } = "";
    public string Version { get; set; } = "";
    public int MajorVersion { get; set; }

    [JsonIgnore]
    public string Label => $"Java {Version}  ({Path})";
}
