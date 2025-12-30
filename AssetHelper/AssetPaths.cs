using BepInEx;
using Silksong.AssetHelper.BundleTools;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Silksong.AssetHelper;

/// <summary>
/// Static class defining paths used by AssetHelper.
/// </summary>
public static class AssetPaths
{
    private static string? _bundleFolder;
    private static string? _catalogFolder;
    private static string? _cacheDirectory;
    private static string? _silksongVersion;

    /// <summary>
    /// The folder containing asset bundles.
    /// </summary>
    public static string BundleFolder
    {
        get
        {
            _bundleFolder ??= GetBundleFolder();
            return _bundleFolder;
        }
    }

    /// <summary>
    /// Path to the catalog folder
    /// </summary>
    public static string CatalogFolder
    {
        get
        {
            _catalogFolder ??= GetCatalogFolder();
            return _catalogFolder;
        }
    }

    private static string GetCatalogFolder()
    {
        return Path.Combine(
                AssetPaths.CacheDirectory,
                "Catalogs"
        );
    }

    private static string GetBundleFolder()
    {
        string rootFolder = Application.streamingAssetsPath;
        string osFolder = Application.platform switch
        {
            RuntimePlatform.WindowsPlayer => "StandaloneWindows64",
            RuntimePlatform.OSXPlayer => "StandaloneOSX",
            RuntimePlatform.LinuxPlayer => "StandaloneLinux64",
            _ => ""
        };
        return Path.Combine(rootFolder, "aa", osFolder);
    }

    /// <summary>
    /// Get the path to the scene bundle for a given scene.
    /// </summary>
    public static string GetScenePath(string sceneName) => Path.Combine(BundleFolder, "scenes_scenes_scenes", $"{sceneName.ToLowerInvariant()}.bundle");

    /// <summary>
    /// The Silksong version. This is calculated using reflection so it is not hardcoded.
    /// </summary>
    public static string SilksongVersion
    {
        get
        {
            _silksongVersion ??= GetSilksongVersion();
            return _silksongVersion;
        }
    }

    private static string GetSilksongVersion() => typeof(Constants)
        .GetField(nameof(Constants.GAME_VERSION), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
        ?.GetRawConstantValue()
        as string
        ?? "UNKNOWN";

    /// <summary>
    /// Directory storing cached information for this version of Silksong.
    /// </summary>
    internal static string CacheDirectory
    {
        get
        {
            if (_cacheDirectory is not null) return _cacheDirectory;

            string silksongVersion = GetSilksongVersion();
            string dir = Path.Combine(
                Paths.CachePath,
#if DEBUG
                $"{nameof(AssetHelper)}_DEBUG",
#else
                nameof(AssetHelper),
#endif
                $"v{silksongVersion}");

            Directory.CreateDirectory(dir);

            _cacheDirectory = dir;
            return dir;
        }
    }

    internal static string AssemblyFolder => Directory.GetParent(typeof(DebugTools).Assembly.Location).FullName;
}
