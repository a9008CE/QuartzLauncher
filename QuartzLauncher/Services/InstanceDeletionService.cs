using System.IO;
using QuartzLauncher.Models;

namespace QuartzLauncher.Services;

public static class InstanceDeletionService
{
    public static void Delete(AppPaths paths, Settings settings, Instance instance)
    {
        var versionId = string.IsNullOrWhiteSpace(instance.VersionId) ? instance.McVersion : instance.VersionId;
        if (instance.Id.StartsWith("local-", StringComparison.OrdinalIgnoreCase))
        {
            var syntheticMetadataRoot = Path.GetFullPath(Path.Combine(paths.InstancesDir, instance.Id));
            EnsureDirectChild(paths.InstancesDir, syntheticMetadataRoot);
            new InstanceStore(paths.InstancesDir).Delete(instance.Id);
            if (string.IsNullOrWhiteSpace(versionId)) return;
            var localVersionRoot = Path.GetFullPath(Path.Combine(paths.VersionsDir, versionId));
            EnsureDirectChild(paths.VersionsDir, localVersionRoot);
            if (Directory.Exists(localVersionRoot)) Directory.Delete(localVersionRoot, true);
            return;
        }

        var gameRoot = Path.GetFullPath(InstancePathService.GetGameDirectory(paths, settings, instance));
        var metadataRoot = Path.GetFullPath(Path.Combine(paths.InstancesDir, instance.Id));
        var versionRoot = string.IsNullOrWhiteSpace(versionId)
            ? ""
            : Path.GetFullPath(Path.Combine(paths.VersionsDir, versionId));
        EnsureDirectChild(paths.InstancesDir, metadataRoot);
        if (!string.IsNullOrWhiteSpace(versionRoot)) EnsureDirectChild(paths.VersionsDir, versionRoot);
        new InstanceStore(paths.InstancesDir).Delete(instance.Id);

        var remainingInstances = new InstanceStore(paths.InstancesDir).List();
        var sharedGameRoot = PathsEqual(gameRoot, paths.MinecraftDir);
        if (!PathsEqual(gameRoot, metadataRoot)
            && !sharedGameRoot
            && IsDirectChild(paths.VersionsDir, gameRoot)
            && !remainingInstances.Any(other => PathsEqual(
                InstancePathService.GetGameDirectory(paths, settings, other), gameRoot)))
            DeleteDirectory(gameRoot);

        var versionStillReferenced = !string.IsNullOrWhiteSpace(versionId)
            && remainingInstances.Any(other =>
                string.Equals(other.VersionId, versionId, StringComparison.OrdinalIgnoreCase)
                || PathsEqual(InstancePathService.GetGameDirectory(paths, settings, other), versionRoot));
        if (!versionStillReferenced && !string.IsNullOrWhiteSpace(versionRoot)) DeleteDirectory(versionRoot);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private static void EnsureDirectChild(string parent, string child)
    {
        if (!IsDirectChild(parent, child))
            throw new InvalidDataException("版本路径不安全，已取消删除。");
    }

    private static bool IsDirectChild(string parent, string child)
    {
        var parentFull = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var childFull = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar);
        return childFull.StartsWith(parentFull, StringComparison.OrdinalIgnoreCase)
               && !childFull[parentFull.Length..].Contains(Path.DirectorySeparatorChar);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
