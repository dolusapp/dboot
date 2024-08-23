using System.Text.Json.Serialization;
namespace BootstrapperShared;


public partial record CatalogFile(string Path, string Hash);
public partial record VersionInfo(string ReleasePath, string ReleaseHash, List<CatalogFile> Files);
public partial record BranchInfo(string Name, string CurrentVersion, Dictionary<string, VersionInfo> Versions)
{

    public VersionInfo GetCurrentVersionInfo()
    {
        if (Versions.TryGetValue(CurrentVersion, out var info))
        {
            return info;
        }
        throw new FileNotFoundException("Could not find current version info");
    }

    public VersionInfo? GetVersionInfo(string version)
    {
        if (Versions.TryGetValue(version, out var info))
        {
            return info;
        }
        return null;
    }
}
public partial record Catalog(Dictionary<string, BranchInfo> Branches)
{
    public BranchInfo? GetBranch(string branch)
    {
        if (Branches.TryGetValue(branch, out var branchInfo))
        {
            return branchInfo;
        }
        return null;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, Converters = [typeof(CatalogJsonConverter)])]
[JsonSerializable(typeof(Catalog))]
[JsonSerializable(typeof(BranchInfo))]
[JsonSerializable(typeof(VersionInfo))]
[JsonSerializable(typeof(CatalogFile))]
internal partial class SourceGenerationContext : JsonSerializerContext
{

}