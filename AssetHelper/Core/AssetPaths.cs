using System.IO;
using BepInEx;
using UnityEngine;

namespace Silksong.AssetHelper.Core;

/// <summary>
/// Static class defining paths used by AssetHelper.
/// </summary>
public static class AssetPaths
{
    private static string CreateIfNeeded(this string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// The folder within StreamingAssets/aa containing the bundles.
    /// </summary>
    public static string OSFolderName =>
        Application.platform switch
        {
            RuntimePlatform.WindowsPlayer => "StandaloneWindows64",
            RuntimePlatform.OSXPlayer => "StandaloneOSX",
            RuntimePlatform.LinuxPlayer => "StandaloneLinux64",
            _ => "",
        };

    /// <summary>
    /// The base game folder containing asset bundles.
    /// </summary>
    public static string BundleFolder
    {
        get
        {
            string rootFolder = Application.streamingAssetsPath;
            return Path.Combine(rootFolder, "aa", OSFolderName);
        }
    }

    /// <summary>
    /// Get the path to the base game scene bundle for a given scene.
    /// </summary>
    public static string GetScenePath(string sceneName) =>
        Path.Combine(
            BundleFolder,
            "scenes_scenes_scenes",
            $"{sceneName.ToLowerInvariant()}.bundle"
        );

    private static string CacheSubfolder
    {
        get
        {
            string s;
#if DEBUG
            s = $"{nameof(AssetHelper)}_DEBUG";
#else
            s = nameof(AssetHelper);
#endif
            return s;
        }
    }

    /// <summary>
    /// Directory storing cached information and data for this version of Silksong.
    /// </summary>
    internal static string CacheDirectory =>
        Path.Combine(Paths.CachePath, CacheSubfolder).CreateIfNeeded();

    /// <summary>
    /// Directory storing repacked scenes.
    /// </summary>
    /// <remarks>This is public so that it can be accessed by Addressables; it should probably not be used
    /// by user code.</remarks>
    public static string RepackedSceneBundleDir =>
        Path.Combine(CacheDirectory, "repacked_scenes").CreateIfNeeded();

    /// <summary>
    /// Path to the AssetHelper catalog folder.
    /// </summary>
    internal static string CatalogFolder =>
        Path.Combine(CacheDirectory, "Catalogs").CreateIfNeeded();

    /// <summary>
    /// Directory containing this assembly.
    /// </summary>
    internal static string AssemblyFolder =>
        Directory.GetParent(typeof(AssetPaths).Assembly.Location).FullName;
}
