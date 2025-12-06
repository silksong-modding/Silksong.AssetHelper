using System.IO;

namespace Silksong.AssetHelper.Dev;

/// <summary>
/// Class providing tools to help find information about the asset database.
/// </summary>
public static class DebugTools
{
    private static DirectoryInfo AssemblyFolder => Directory.GetParent(typeof(DebugTools).Assembly.Location);

    /// <summary>
    /// Dump all addressable keys to the bundle_keys.json file next to this assembly.
    /// 
    /// The keys in this dictionary should be used as the keys in methods on <see cref="AssetLoadUtil"/>.
    /// </summary>
    public static void DumpAddressablesKeys()
    {
        string dumpFile = Path.Combine(AssemblyFolder.FullName, "bundle_keys.json");

        AssetsData.InvokeAfterAddressablesLoaded(() => AssetsData.BundleKeys.SerializeToFileInBackground(dumpFile));
    }
}
