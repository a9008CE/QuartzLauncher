using System.IO;
using Newtonsoft.Json;

namespace QuartzLauncher.Models;

public class Instance
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string VersionId { get; set; } = "";
    public string McVersion { get; set; } = "";
    public string Loader { get; set; } = "vanilla";
    public string LoaderVersion { get; set; } = "";
    public bool AutoSetChinese { get; set; } = true;
    public bool? VersionIsolation { get; set; }
    public bool? UsesVersionDirectory { get; set; }
    public string JavaPath { get; set; } = "";
    public int MemoryMb { get; set; }
    public string JvmArguments { get; set; } = "";
    public string GameArguments { get; set; } = "";
    public string IconPath { get; set; } = "";
    public string IconKey { get; set; } = "";
    public string CustomGameDir { get; set; } = "";

    public string Label
    {
        get
        {
            var baseVer = string.IsNullOrEmpty(McVersion) ? VersionId : McVersion;
            if (Loader == "vanilla")
                return $"{Name}  \u00b7  {baseVer}";
            var loaderDisplay = char.ToUpper(Loader[0]) + Loader[1..];
            var ver = LoaderVersion ?? "";
            var tag = string.IsNullOrEmpty(ver) ? loaderDisplay : $"{loaderDisplay}[{ver}]";
            return $"{Name}  \u00b7  {baseVer} {tag}";
        }
    }
}

public class InstanceStore
{
    private readonly string _instancesDir;

    public InstanceStore(string instancesDir) => _instancesDir = instancesDir;

    public List<Instance> List()
    {
        var result = new List<Instance>();
        if (!Directory.Exists(_instancesDir)) return result;
        foreach (var dir in Directory.GetDirectories(_instancesDir).OrderBy(d => d))
        {
            var file = Path.Combine(dir, "instance.json");
            if (!File.Exists(file)) continue;
            try
            {
                var json = File.ReadAllText(file);
                var inst = JsonConvert.DeserializeObject<Instance>(json);
                if (inst != null) result.Add(inst);
            }
            catch { continue; }
        }
        return result;
    }

    public string Create(Instance instance)
    {
        var root = Path.Combine(_instancesDir, instance.Id);
        Directory.CreateDirectory(root);
        var json = JsonConvert.SerializeObject(instance, Formatting.Indented);
        File.WriteAllText(Path.Combine(root, "instance.json"), json);
        return root;
    }

    public void Delete(string instanceId)
    {
        var root = Path.Combine(_instancesDir, instanceId);
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }
}
