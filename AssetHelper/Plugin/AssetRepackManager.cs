using AssetsTools.NET;
using AssetsTools.NET.Extra;
using MonoDetour.HookGen;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.BundleTools.Repacking;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.U2D;
using RepackDataCollection = System.Collections.Generic.Dictionary<string, Silksong.AssetHelper.Plugin.RepackedSceneBundleData>;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Class managing the scene repacking.
/// </summary>
[MonoDetourTargets(typeof(StartManager))]
internal static class AssetRepackManager
{
    #region Hooks
    internal static void Hook()
    {
        Md.StartManager.Start.Postfix(PrependStartManagerStart);
    }

    private static void PrependStartManagerStart(StartManager self, ref IEnumerator returnValue)
    {
        returnValue = WrapStartManagerStart(self, returnValue);
    }

    private static IEnumerator WrapStartManagerStart(StartManager self, IEnumerator original)
    {
        // This should already be the case, but we should check just in case it matters.
        yield return new WaitUntil(() => AddressablesData.IsAddressablesLoaded);

        bool shouldRepack = Prepare();

        if (shouldRepack)
        {
            IEnumerator runner = Run();

            while (runner.MoveNext())
            {
                // Yield after each repack op is done
                yield return null;
            }

            IEnumerator catalogCreate = CreateSceneAssetCatalog(_repackData);
            while (catalogCreate.MoveNext())
            {
                yield return null;
            }

            yield return null;
        }

        if (_repackData.Any())
        {
            // Only load the catalog if anyone's requested scene assets
            AssetHelperPlugin.InstanceLogger.LogInfo($"Loading scene catalog");
            AsyncOperationHandle<IResourceLocator> catalogLoadOp = Addressables.LoadContentCatalogAsync(SceneCatalogPath);
            yield return catalogLoadOp;
            AssetRequestAPI.SceneAssetLocator = catalogLoadOp.Result;
        }
        yield return null;

        IEnumerator nonSceneCatalogCreate = CreateNonSceneAssetCatalog();
        while (nonSceneCatalogCreate.MoveNext())
        {
            yield return null;
        }
        yield return null;

        if (File.Exists(NonSceneCatalogPath) && AssetRequestAPI.AnyNonSceneCatalogRequested)
        {
            AssetHelperPlugin.InstanceLogger.LogInfo($"Loading non-scene catalog");
            AsyncOperationHandle<IResourceLocator> catalogLoadOp = Addressables.LoadContentCatalogAsync(NonSceneCatalogPath);
            yield return catalogLoadOp;
            AssetRequestAPI.NonSceneAssetLocator = catalogLoadOp.Result;
        }
        yield return null;

        AssetHelperPlugin.InstanceLogger.LogInfo($"{nameof(AssetHelper)} prep complete!");
        AssetRequestAPI.AfterBundleCreationComplete.Activate();

        yield return original;
        yield break;
    }
    #endregion

    // Invalidate all data on disk that's older than this version
    private static readonly Version _lastAcceptablePluginVersion = Version.Parse("0.1.0");

    // Path to the metadata for the repacked scene bundles
    private static string _repackDataPath = Path.Combine(AssetPaths.RepackedSceneBundleDir, "repack_data.json");

    // Path to the scene catalog .bin file
    private static string SceneCatalogPath => Path.Combine(AssetPaths.CatalogFolder, $"{CatalogKeys.SceneCatalogId}.bin");

    // (scene, gameObjs) that need to be repacked
    private static Dictionary<string, HashSet<string>> _toRepack = [];

    // Data about the repacked assets in the bundles on disk
    private static RepackDataCollection _repackData = [];

    /// <summary>
    /// Prepare the repacking request.
    /// </summary>
    /// <returns>True if there is any repacking to be done.</returns>
    internal static bool Prepare()
    {
        if (JsonExtensions.TryLoadFromFile(_repackDataPath, out RepackDataCollection? repackData))
        {
            _repackData = repackData;
        }
        else
        {
            _repackData = [];
        }

        // Any data with a metadata mismatch should be removed from the dictionary
        _repackData = _repackData.Where(kvp => !MetadataMismatch(kvp.Key, kvp.Value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        _toRepack = [];

        foreach ((string scene, HashSet<string> request) in AssetRequestAPI.SceneAssetRequest)
        {
            if (!_repackData.TryGetValue(scene, out RepackedSceneBundleData existingBundleData))
            {
                _toRepack[scene] = request;
                continue;
            }
            if (existingBundleData.Data is null)
            {
                _toRepack[scene] = request;
                continue;
            }

            if (request.All(x => existingBundleData.Data.TriedToRepack(x)))
            {
                // No need to re-repack as there's nothing new to try
                continue;
            }

            // Include everything from the old bundle - perhaps this should be a config option?
            _toRepack[scene] = new(request
                .Union(existingBundleData.Data.GameObjectAssets?.Values ?? Enumerable.Empty<string>())
                .Union(existingBundleData.Data.NonRepackedAssets ?? Enumerable.Empty<string>())
                );
        }

        return _toRepack.Count > 0;
    }

    /// <summary>
    /// Check if we have to completely repack because of a metadata change.
    /// </summary>
    private static bool MetadataMismatch(string scene, RepackedSceneBundleData existingData)
    {
        if (!Version.TryParse(existingData.PluginVersion ?? string.Empty, out Version oldPluginVersion)
                || oldPluginVersion > Version.Parse(AssetHelperPlugin.Version)
                || oldPluginVersion < _lastAcceptablePluginVersion)
        {
            // Mismatch: the version of the plugin used to repack needs to be after the last acceptable version.
            // We do not accept versions from the future.
            return true;
        }

        if (existingData.SilksongVersion == AssetPaths.SilksongVersion)
        {
            // If the Silksong version matches, then we're definitely fine.
            return false;
        }

        if (AddressablesData.TryGetLocationForScene(scene, out IResourceLocation? location)
            && location.Data is AssetBundleRequestOptions opts
            && !string.IsNullOrEmpty(opts.Hash)
            && !string.IsNullOrEmpty(existingData.BundleHash)
            && opts.Hash == existingData.BundleHash)
        {
            // Hash matches, so we can accept the mismatched silksong version
            return false;
        }

        return true;
    }

    /// <summary>
    /// Run the repacking procedure so that by the end, anything in the request which could be repacked has been.
    /// </summary>
    internal static IEnumerator Run()
    {
        SceneRepacker repacker = new StrippedSceneRepacker();

        AssetHelperPlugin.InstanceLogger.LogInfo($"Repacking {_toRepack.Count} scenes");
        foreach ((string scene, HashSet<string> request) in _toRepack)
        {
            Stopwatch sw = Stopwatch.StartNew();
            AssetHelperPlugin.InstanceLogger.LogInfo($"Repacking {request.Count} objects in scene {scene}");
            RepackedBundleData repackData = repacker.Repack(
                AssetPaths.GetScenePath(scene),
                request.ToList(),
                $"{nameof(AssetHelper)}/{scene}",
                Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{scene}.bundle")
                );

            string? hash = null;
            if (AddressablesData.TryGetLocationForScene(scene, out IResourceLocation? location) && location.Data is AssetBundleRequestOptions opts)
            {
                hash = opts.Hash;
            }

            RepackedSceneBundleData sceneRepackData = new()
            {
                SceneName = scene,
                BundleHash = hash,
                Data = repackData
            };

            _repackData[scene] = sceneRepackData;
            _repackData.SerializeToFile(_repackDataPath);
            AssetHelperPlugin.InstanceLogger.LogInfo($"Repacked {scene} in {sw.ElapsedMilliseconds} ms");

            yield return null;
        }
    }

    internal static IEnumerator CreateSceneAssetCatalog(RepackDataCollection data)
    {
        // TODO - check metadata, this should include all game objects
        string catalogMetadataPath = Path.ChangeExtension(SceneCatalogPath, ".json");

        AssetHelperPlugin.InstanceLogger.LogInfo($"Creating catalog");
        CustomCatalogBuilder cbr = new(CatalogKeys.SceneCatalogId);
        foreach ((string sceneName, RepackedSceneBundleData repackBunData) in data)
        {
            if (repackBunData.Data == null) continue;
            string bundlePath = Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{sceneName}.bundle");
            cbr.AddRepackedSceneData(sceneName, repackBunData.Data, bundlePath);
        }

        // TODO - add in requested child paths

        yield return null;

        cbr.Build();

        SceneCatalogMetadata metadata = new();
        metadata.SerializeToFile(catalogMetadataPath);

        yield return null;
    }

    // Path to the non-scene catalog .bin file
    private static string NonSceneCatalogPath => Path.Combine(AssetPaths.CatalogFolder, $"{CatalogKeys.NonSceneCatalogId}.bin");

    internal static IEnumerator CreateNonSceneAssetCatalog()
    {
        // TODO - implement full non scene catalog option
        // TODO - probably easiest to implement as a separate catalog
        
        string catalogMetadataPath = Path.ChangeExtension(NonSceneCatalogPath, ".json");

        AssetHelperPlugin.InstanceLogger.LogInfo($"Creating NS catalog");

        Dictionary<(string bundleName, string assetName), Type> toCatalog = [];
        bool shouldWriteCatalog = false;

        if (JsonExtensions.TryLoadFromFile(catalogMetadataPath, out NonSceneCatalogMetadata? existingCatalogData)
            && existingCatalogData.SilksongVersion == AssetPaths.SilksongVersion
            && Version.TryParse(existingCatalogData.PluginVersion ?? string.Empty, out Version oldPluginVersion)
            && oldPluginVersion <= Version.Parse(AssetHelperPlugin.Version)
            && oldPluginVersion >= _lastAcceptablePluginVersion)
        {
            toCatalog = existingCatalogData.CatalogAssets;
        }

        foreach ((var assetKey, Type value) in AssetRequestAPI.RequestedNonSceneAssets)
        {
            if (!toCatalog.ContainsKey(assetKey))
            {
                toCatalog.Add(assetKey, value);
                shouldWriteCatalog = true;
                continue;
            }

            Type existingT = toCatalog[assetKey];
            if (existingT != value)
            {
                AssetHelperPlugin.InstanceLogger.LogInfo($"Replacing {assetKey} :: {toCatalog[assetKey]} -> {value}");

                // Existing type is probably incorrect, take the newer type
                toCatalog[assetKey] = value;
                shouldWriteCatalog = true;
                continue;
            }
        }

        if (!shouldWriteCatalog)
        {
            AssetHelperPlugin.InstanceLogger.LogInfo($"Not writing non-scene catalog: no new assets added");
            yield break;
        }

        NonSceneCatalogMetadata metadata = new();

        CustomCatalogBuilder cbr = new(CatalogKeys.NonSceneCatalogId);

        foreach (((string bundleName, string assetName), Type assetType) in AssetRequestAPI.RequestedNonSceneAssets)
        {
            cbr.AddAssets(bundleName, [(assetName, assetType)]);
            metadata.CatalogAssets.Add((bundleName, assetName), assetType);
        }

        yield return null;

        AssetHelperPlugin.InstanceLogger.LogInfo($"Writing catalog");
        cbr.Build();
        metadata.SerializeToFile(catalogMetadataPath);
    }
}
