using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Silksong.AssetHelper.Internal;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Helpers for determining bundle dependencies
/// </summary>
public static class Deps
{
    internal static void Setup()
    {
        BundleFolder = GetBundleFolder();
        CabLookup = CacheManager.GetCached(GenerateCabLookup, "cabs.json");
        DirectDependencyLookup = new("direct_deps.json");
    }

    /// <summary>
    /// The folder containing asset bundles.
    /// </summary>
    public static string BundleFolder { get; private set; } = null!;
    
    /// <summary>
    /// Lookup for bundle name to cab name.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CabLookup { get; private set; } = null!;

    private static CachedObject<Dictionary<string, List<string>>> DirectDependencyLookup { get; set; } = null!;

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

    private static Dictionary<string, string> GenerateCabLookup()
    {
        AssetsManager mgr = new();

        string bundleFolder = BundleFolder;

        Dictionary<string, string> lookup = [];

        foreach (string f in Directory.EnumerateFiles(bundleFolder, "*.bundle", SearchOption.AllDirectories))
        {
            string key = Path.GetRelativePath(bundleFolder, f).Replace("\\", "/");

            BundleFileInstance bun = mgr.LoadBundleFile(f);
            string cab = bun.file.GetFileName(0).Split(".")[0].ToLowerInvariant();
            lookup[cab] = key;
        }

        return lookup;
    }

    /// <summary>
    /// Determine the direct dependencies for a given bundle.
    /// </summary>
    /// <param name="bundleName"></param>
    /// <returns></returns>
    public static List<string> DetermineDirectDeps(string bundleName)
    {
        string bundleFile = bundleName;
        if (!bundleFile.EndsWith(".bundle"))
        {
            bundleFile = bundleFile + ".bundle";
        }

        if (DirectDependencyLookup.Value.TryGetValue(bundleFile, out List<string> deps))
        {
            return [.. deps];
        }

        AssetsManager mgr = new();

        BundleFileInstance bun = mgr.LoadBundleFile(Path.Combine(BundleFolder, bundleFile));
        AssetsFileInstance afileInst = mgr.LoadAssetsFileFromBundle(bun, 0, false);
        AssetsFile afile = afileInst.file;
        AssetFileInfo assetInfos = afile.GetAssetsOfType(AssetClassID.AssetBundle)[0];

        List<string> computedDeps = [];
        foreach (AssetsFileExternal x in afile.Metadata.Externals)
        {
            string path = x.OriginalPathName;
            string cab = path.Split('/')[^1].Split(".")[0].ToLowerInvariant();
            if (!CabLookup.TryGetValue(cab, out string dep))
            {
                continue;
            }

            computedDeps.Add(dep);
        }

        DirectDependencyLookup.Value[bundleFile] = [.. computedDeps];

        return computedDeps;
    }
}
