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
    private static string CreateIfNeeded(this string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string? _silksongVersion;

    /// <summary>
    /// The base game folder containing asset bundles.
    /// </summary>
    public static string BundleFolder
    {
        get
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
    }

    /// <summary>
    /// Path to the AssetHelper catalog folder.
    /// </summary>
    public static string CatalogFolder => Path.Combine(CacheDirectory, "Catalogs").CreateIfNeeded();

    /// <summary>
    /// Get the path to the base game scene bundle for a given scene.
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
    internal static string CacheDirectory => Path.Combine(Paths.CachePath, CacheSubfolder).CreateIfNeeded();

    /// <summary>
    /// Directory storing repacked scenes.
    /// </summary>
    internal static string RepackedSceneBundleDir => Path.Combine(CacheDirectory, "repacked_scenes").CreateIfNeeded();

    /// <summary>
    /// Directory containing this assembly.
    /// </summary>
    internal static string AssemblyFolder => Directory.GetParent(typeof(AssetPaths).Assembly.Location).FullName;

    private static string _debugDataDir = Path.Combine(AssemblyFolder, "DebugData");

    /// <summary>
    /// The directory where functions in <see cref="DebugTools"/> write their output.
    /// 
    /// This defaults to a subfolder of this plugin's assembly, but can be changed.
    /// </summary>
    public static string DebugDataDir
    {
        get => _debugDataDir.CreateIfNeeded();
        set => _debugDataDir = value;
    }
}
