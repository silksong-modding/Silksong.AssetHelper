using BepInEx.Logging;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using AssetsTools.NET.Extra;
using UnityEngine.AddressableAssets.ResourceLocators;
using NameListLookup = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>;

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Class providing tools to help find information about the asset database.
/// </summary>
public static class DebugTools
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"AssetHelper.{nameof(DebugTools)}");

    /// <summary>
    /// Dump all addressable keys to the bundle_keys.json file in the debug data dir.
    /// </summary>
    public static void DumpAddressablesKeys()
    {
        string dumpFile = Path.Combine(AssetPaths.DebugDataDir, "bundle_keys.json");

        AddressablesData.InvokeAfterAddressablesLoaded(() => AddressablesData.BundleKeys.SerializeToFileInBackground(dumpFile));
    }

    /// <summary>
    /// Dump all asset names to the asset_names.json file in the debug data dir.
    /// 
    /// This function loads all asset bundles, so is quite slow.
    /// </summary>
    public static void DumpAllAssetNames()
    {
        AddressablesData.InvokeAfterAddressablesLoaded(DumpAllAssetNamesInternal);
    }

    private static void DumpAllAssetNamesInternal()
    {
        string dumpFile = Path.Combine(AssetPaths.DebugDataDir, "asset_names.json");

        NameListLookup assetNames = [];
        NameListLookup sceneNames = [];
        
        Stopwatch sw = Stopwatch.StartNew();
        foreach ((string name, string key) in AddressablesData.BundleKeys!)
        {
            AsyncOperationHandle<IAssetBundleResource> op = Addressables.LoadAssetAsync<IAssetBundleResource>(key);
            IAssetBundleResource rsc = op.WaitForCompletion();
            AssetBundle b = rsc.GetAssetBundle();

            string[] bundleScenePaths = b.GetAllScenePaths();
            if (bundleScenePaths.Length > 0)
            {
                sceneNames[name] = bundleScenePaths.ToList();
            }
            string[] bundleAssetNames = b.GetAllAssetNames();
            if (bundleAssetNames.Length > 0)
            {
                assetNames[name] = bundleAssetNames.ToList();
            }
            Addressables.Release(op);
        }
        sw.Stop();
        Log.LogInfo($"Determined asset names in {sw.ElapsedMilliseconds} ms");

        Dictionary<string, NameListLookup> data = new()
        {
            ["assets"] = assetNames,
            ["scenes"] = sceneNames,
        };

        data.SerializeToFileInBackground(dumpFile);
    }

    /// <summary>
    /// Write a list of all Addressable assets loadable using <see cref="Addressables.LoadAssetAsync{TObject}(object)"/> directly.
    /// 
    /// This only includes base game addressable assets; assets from catalogs added by AssetHelper are not included.
    /// 
    /// The list includes the most important information about each asset.
    /// </summary>
    public static void DumpAllAddressableAssets()
    {
        AddressablesData.InvokeAfterAddressablesLoaded(DumpAllAddressableAssetsInternal);
    }

    private static void DumpAllAddressableAssetsInternal()
    {
        if (AddressablesData.MainLocator != null)
        {
            DumpAllAddressableAssets(AddressablesData.MainLocator, "addressables_main.json");
        }
    }

    /// <summary>
    /// Write a list of all Addressable assets in the catalog given by the provided locator.
    /// </summary>
    /// <param name="locator"></param>
    /// <param name="fileName">The name of the file within the debug data dir.</param>
    /// <param name="includeDependencyNames">Whether to include the names of dependencies.
    /// The dependency names will be in the form `[primaryKey | internalId]`.
    /// This will significantly increase the size of the outputted file.</param>
    public static void DumpAllAddressableAssets(IResourceLocator locator, string fileName, bool includeDependencyNames = false)
    {
        List<AddressablesAssetInfo> assetInfos = [];

        foreach (IResourceLocation loc in locator.AllLocations)
        {
            assetInfos.Add(AddressablesAssetInfo.FromLocation(loc, includeDependencyNames));
        }

        LocatorInfo locatorInfo = new() { LocatorID = locator.LocatorId, Infos = assetInfos };

        locatorInfo.SerializeToFileInBackground(Path.Combine(AssetPaths.DebugDataDir, fileName));
    }

    private class LocatorInfo
    {
        public string? LocatorID { get; init; }
        public List<AddressablesAssetInfo>? Infos { get; init; }
    }

    private class AddressablesAssetInfo
    {
        public string? InternalId { get; init; }
        public string? ProviderId { get; init; }
        public int DependencyCount { get; init; }
        public List<string>? Dependencies { get; init; }
        public string? PrimaryKey { get; init; }
        public Type? ResourceType { get; init; }

        public static AddressablesAssetInfo FromLocation(IResourceLocation loc, bool includeDependencyNames)
        {
            List<string>? depNames = includeDependencyNames
                ? loc.Dependencies?.Select(x => $"[{x?.PrimaryKey} | {x?.InternalId}]").ToList() ?? []
                : null;

            return new()
            {
                InternalId = loc.InternalId,
                ProviderId = loc.ProviderId,
                DependencyCount = loc.Dependencies?.Count ?? 0,
                Dependencies = depNames,
                PrimaryKey = loc.PrimaryKey,
                ResourceType = loc.ResourceType,
            };
        }
    }

    private static Dictionary<string, string>? _bundleNameLookup;

    private static Dictionary<string, string> GenerateBundleNameLookup()
    {
        Dictionary<string, string> lookup = [];

        foreach ((string name, string key) in AddressablesData.BundleKeys!)
        {
            AsyncOperationHandle<IAssetBundleResource> op = Addressables.LoadAssetAsync<IAssetBundleResource>(key);
            op.WaitForCompletion();
            lookup[op.Result.GetAssetBundle().name] = name;
            Addressables.Release(op);
        }

        return lookup;
    }

    /// <summary>
    /// Get a readable list of loaded bundle names (paths relative to the bundle base dir).
    /// 
    /// The first time this is used in each Silksong version, it will generate a lookup of bundle name -> readable name,
    /// which can be quite slow.
    /// </summary>
    /// <exception cref="InvalidOperationException">Raised if this is called before Addressables is ready.</exception>
    public static LoadedBundleNames GetLoadedBundleNames(out List<string> names, out List<string> unknown)
    {
        if (!AddressablesData.IsAddressablesLoaded)
        {
            throw new InvalidOperationException($"{nameof(GetLoadedBundleNames)} cannot be called until Addressables is loaded!");
        }

        _bundleNameLookup ??= CachedObject<Dictionary<string, string>>.CreateSynced("bundle_name_lookup.json", GenerateBundleNameLookup).Value;

        names = [];
        unknown = [];

        foreach (string bunName in AssetBundle.GetAllLoadedAssetBundles().Select(b => b.name))
        {
            if (_bundleNameLookup.TryGetValue(bunName, out string name))
            {
                names.Add(name);
            }
            else
            {
                unknown.Add(bunName);
            }
        }

        return new(names, unknown);
    }

    /// <summary>
    /// Dump all game object paths to paths_{sceneName}.json in the debug data dir.
    /// If <paramref name="compressed"/> is true, the output file will be paths_{sceneName}_compressed.json.
    /// </summary>
    /// <param name="sceneName">The name of the scene.</param>
    /// <param name="compressed">If <see langword="true"/>, dump as entries "objPath [goPathId, transformPathId]".
    /// If <see langword="false"/>, dump as full json.</param>
    public static void DumpGameObjectPaths(string sceneName, bool compressed)
    {
        string name = $"paths_{sceneName}" + (compressed ? "_compressed.json" : ".json");
        string outPath = Path.Combine(AssetPaths.DebugDataDir, name);

        AssetsManager mgr = BundleUtils.CreateDefaultManager();
        BundleFileInstance bunInst = mgr.LoadBundleFile(AssetPaths.GetScenePath(sceneName));
        if (!mgr.TryFindAssetsFiles(bunInst, out BundleUtils.SceneBundleInfo sceneBundleInfo))
        {
            return;
        }

        AssetsFileInstance mainFileInst = mgr.LoadAssetsFileFromBundle(bunInst, sceneBundleInfo.mainAfileInstIndex);

        GameObjectLookup lookup = GameObjectLookup.CreateFromFile(mgr, mainFileInst);
        List<GameObjectLookup.GameObjectInfo> infos = lookup.TraverseOrdered().ToList();
        if (!compressed)
        {
            infos.SerializeToFile(outPath);
            return;
        }

        List<string> cInfos = infos.Select(x => $"{x.GameObjectName} [{x.GameObjectPathId}, {x.TransformPathId}]").ToList();
        cInfos.SerializeToFile(outPath);

        mgr.UnloadAll();
    }
}
