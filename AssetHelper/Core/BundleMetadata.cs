using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper.Core;

/// <summary>
/// Helpers for determining bundle metadata, with the results automatically cached.
/// 
/// This class is not safe to use until Addressables has loaded.
/// </summary>
public static class BundleMetadata
{
    internal static void Setup()
    {
        CabLookup = CachedObject<IReadOnlyDictionary<string, string>>
            .CreateSynced("cabs.json", GenerateCabLookup, mutable: false, out _)
            .Value;
        DirectDependencyLookup = CachedObject<Dictionary<string, List<string>>>.CreateSynced(
            "direct_deps.json",
            () => [],
            mutable: true,
            out _
        );
    }

    /// <summary>
    /// Lookup for cab name to bundle path.
    /// 
    /// Keys: CAB name (lowercase)
    /// Values: Bundle path, relative to the bundle folder, with .bundle at the end.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CabLookup { get; private set; } = null!;

    private static Dictionary<string, string> GenerateCabLookup()
    {
        Stopwatch sw = Stopwatch.StartNew();

        AssetsManager mgr = new();

        string bundleFolder = AssetPaths.BundleFolder;

        Dictionary<string, string> lookup = [];

        foreach (string f in AddressablesData.BundleKeys!.Keys)
        {
            string key = f.Replace("\\", "/");
            if (!key.EndsWith(".bundle"))
            {
                key += ".bundle";

            }

            string filepath = Path.Combine(bundleFolder, key);

            BundleFileInstance bun = mgr.LoadBundleFile(filepath);
            string cab = bun.file.GetFileName(0).Split(".")[0].ToLowerInvariant();

            if (lookup.ContainsKey(cab))
            {
                AssetHelperPlugin.InstanceLogger.LogWarning($"Duplicate cab detected! {cab}: {lookup[cab]} -> {key}");
            }
            else
            {
                lookup[cab] = key;
            }
            mgr.UnloadAll();
        }

        sw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo(
            $"Generated CAB lookup in {sw.ElapsedMilliseconds} ms"
        );

        return lookup;
    }

    private static CachedObject<
        Dictionary<string, List<string>>
    > DirectDependencyLookup { get; set; } = null!;

    /// <summary>
    /// Determine the direct dependencies for a given bundle.
    /// </summary>
    /// <param name="bundleName"></param>
    /// <returns>A list of bundle paths, with .bundle included.</returns>
    public static List<string> DetermineDirectDeps(string bundleName)
        => DetermineDirectDepsInternal(bundleName, out _);

    internal static List<string> DetermineDirectDepsInternal(string bundleName, out bool cacheHit)
    {
        string bundleFile = bundleName;
        if (!bundleFile.EndsWith(".bundle"))
        {
            bundleFile = bundleFile + ".bundle";
        }

        if (DirectDependencyLookup.Value.TryGetValue(bundleFile, out List<string> deps))
        {
            cacheHit = true;
            return [.. deps];
        }

        cacheHit = false;

        AssetsManager mgr = new();
        string sceneBundlePath = Path.Combine(AssetPaths.BundleFolder, bundleFile);
        using MemoryStream ms = new(File.ReadAllBytes(sceneBundlePath));
        BundleFileInstance bun = mgr.LoadBundleFile(ms, sceneBundlePath);

        AssetsFileInstance afileInst = mgr.LoadAssetsFileFromBundle(bun, 0, false);
        AssetsFile afile = afileInst.file;

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

        mgr.UnloadAll();
        return computedDeps;
    }

    /// <summary>
    /// Determine all dependencies of the given bundle, direct and transitive,
    /// </summary>
    /// <param name="bundleName"></param>
    /// <returns>A list of bundle paths with .bundle included.</returns>
    public static List<string> DetermineTransitiveDeps(string bundleName)
    {
        string bundleFile = bundleName;
        if (!bundleFile.EndsWith(".bundle"))
        {
            bundleFile = bundleFile + ".bundle";
        }

        HashSet<string> seen = [bundleFile];
        Queue<string> toProcess = new();
        toProcess.Enqueue(bundleFile);

        while (toProcess.TryDequeue(out string current))
        {
            foreach (string dep in DetermineDirectDeps(current))
            {
                if (seen.Add(dep))
                {
                    toProcess.Enqueue(dep);
                }
            }
        }

        seen.Remove(bundleFile);

        return seen.ToList();
    }

    /// <summary>
    /// Determine all dependencies of the given bundle, direct and transitive, as listed in
    /// the Addressables catalog entry for the scene.
    /// 
    /// The scene bundle itself will not be included in the list.
    /// </summary>
    /// <param name="sceneBundle">The scene name or bundle path.</param>
    /// <returns>A list of bundle paths with .bundle at the end.</returns>
    public static List<string> DetermineCatalogDeps(string sceneBundle)
    {
        sceneBundle = sceneBundle.Replace(".bundle", "");
        if (!sceneBundle.StartsWith("scenes_scenes_scenes/"))
        {
            sceneBundle = "scenes_scenes_scenes/" + sceneBundle;
        }

        string sceneName = sceneBundle.Substring("scenes_scenes_scenes/".Length);

        if (AddressablesData.MainLocator == null)
        {
            throw new InvalidOperationException("Cannot inspect catalog until after Addressables has loaded");
        }

        string key = AddressablesData.MainLocator.Keys.OfType<string>().Where(s =>
            s.StartsWith("Scenes/")
            && s.Substring(7).Equals(sceneName, StringComparison.InvariantCultureIgnoreCase)
        ).First();

        AddressablesData.MainLocator.Locate(key, typeof(SceneInstance), out IList<IResourceLocation> locations);
        IResourceLocation location = locations.First();

        List<string> deps = [];
        foreach (IResourceLocation depLoc in location.Dependencies)
        {
            string pkey = depLoc.PrimaryKey;
            AddressablesData.TryStrip(pkey, out string? pkeyStripped);
            if (pkeyStripped == sceneBundle)
            {
                continue;
            }
            deps.Add(pkeyStripped! + ".bundle");
        }

        return deps;
    }
}
