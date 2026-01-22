using AssetHelperLib.Repacking;
using Silksong.AssetHelper.CatalogTools;
using Silksong.AssetHelper.Internal;
using AssetHelperLib.Util;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using RepackDataCollection = System.Collections.Generic.Dictionary<string, Silksong.AssetHelper.Plugin.RepackedSceneBundleData>;
using Silksong.AssetHelper.Core;
using AssetHelperLib.PreloadTable;
using CPPCache = System.Collections.Generic.Dictionary<string, AssetHelperLib.PreloadTable.ContainerPointerPreloadsBundleData>;
using System;

namespace Silksong.AssetHelper.Plugin.Tasks;

/// <summary>
/// Run the routine to repack scene assets, create a catalog and load it.
/// </summary>
internal class SceneRepacking : BaseStartupTask
{
    // Path to the metadata for the repacked scene bundles
    private static string _repackDataPath = Path.Combine(AssetPaths.RepackedSceneBundleDir, "repack_data.json");

    // Path to the scene catalog .bin file
    private static string SceneCatalogPath => Path.Combine(AssetPaths.CatalogFolder, $"{CatalogKeys.SceneCatalogId}.bin");

    // (scene, gameObjs) that need to be repacked
    private Dictionary<string, HashSet<string>> _toRepack = [];

    // Data about the repacked assets in the bundles on disk
    private RepackDataCollection _repackData = [];

    private bool _didRepack = false;

    public override IEnumerator Run(LoadingBar loadingBar)
    {
        return RepackAndCatalogScenes(loadingBar);
    }

    private IEnumerator RepackAndCatalogScenes(LoadingBar bar)
    {
        IEnumerator repack = PrepareAndRun(bar);

        bar.SetText(LanguageKeys.REPACKING_SCENE.GetLocalized());
        yield return null;

        while (repack.MoveNext())
        {
            // Yield after each repack op is done
            yield return null;
        }
        bar.SetProgress(1f);

        bar.SetText(LanguageKeys.BULDING_SCENE.GetLocalized());
        yield return null;

        IEnumerator catalogCreate = CreateSceneAssetCatalog(_repackData);
        while (catalogCreate.MoveNext())
        {
            yield return null;
        }

        yield return null;

        // Only load the catalog if anyone's requested scene assets
        if (_repackData.Count > 0)
        {
            bar.SetText(LanguageKeys.LOADING_SCENE.GetLocalized());
            yield return null;

            AssetHelperPlugin.InstanceLogger.LogInfo($"Loading scene catalog");
            AsyncOperationHandle<IResourceLocator> catalogLoadOp = Addressables.LoadContentCatalogAsync(SceneCatalogPath);
            yield return catalogLoadOp;
            AssetRequestAPI.SceneAssetLocator = catalogLoadOp.Result;
        }
        yield return null;
    }

    private IEnumerator PrepareAndRun(LoadingBar bar)
    {
        Prepare();

        if (_toRepack.Count > 0)
        {
            _didRepack = true;
            return RunRepacking(bar);
        }
        else
        {
            return Enumerable.Empty<object>().GetEnumerator();
        }
    }

    /// <summary>
    /// Prepare the repacking request.
    /// </summary>
    /// <returns>True if there is any repacking to be done.</returns>
    private void Prepare()
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
        // Also remove data if they have manually deleted the file, as a way to individually
        // reset scene data
        _repackData = _repackData
            .Where(kvp => !MetadataMismatch(kvp.Key, kvp.Value) && File.Exists(GetBundlePathForScene(kvp.Key)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        _toRepack = [];

        foreach ((string scene, HashSet<string> request) in AssetRequestAPI.Request.SceneAssets)
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
                // I don't think we should try to repack failed assets unless they're re-requested
                // .Union(existingBundleData.Data.NonRepackedAssets ?? Enumerable.Empty<string>())
                );
        }
    }

    private static string GetBundlePathForScene(string sceneName)
    {
        return Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{sceneName}.bundle");
    }

    /// <summary>
    /// Check if we have to completely repack because of a metadata change.
    /// </summary>
    private static bool MetadataMismatch(string scene, RepackedSceneBundleData existingData)
    {
        if (!VersionData.EarliestAcceptableSceneRepackVersion.AllowCachedData(existingData.PluginVersion))
        {
            // Mismatch: the version of the plugin used to repack needs to be after the last acceptable version.
            // We do not accept versions from the future.
            return true;
        }

        if (existingData.SilksongVersion == VersionData.SilksongVersion)
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

    private static bool ResolveCab(string cabName, out string? bundlePath)
    {
        bundlePath = null;

        if (cabName.Contains("unity"))
        {
            // this isn't a game bundle (not a cab name) so we should skip
            return true;
        }

        if (!BundleMetadata.CabLookup.TryGetValue(cabName.ToLowerInvariant(), out string? bundleName))
        {
            return false;
        }

        if (bundleName.Contains("monoscripts") || bundleName.Contains("builtinassets"))
        {
            // Skip these because we shouldn't follow any deps there
            return true;
        }

        // Technically we can't touch this bundle until AT.NET gets updated (or I write another IL hook)
        // but this bundle isn't useful anyway
        if (bundleName.Contains("audioobjects"))
        {
            return true;
        }

        bundlePath = Path.Combine(AssetPaths.BundleFolder, bundleName);
        return true;
    }

    /// <summary>
    /// Run the repacking procedure so that by the end, anything in the request which could be repacked has been.
    /// </summary>
    private IEnumerator RunRepacking(LoadingBar bar)
    {
        CachedObject<CPPCache> SyncedCppCache = CachedObject<CPPCache>.CreateSynced(
            "container_pointer_preloads_cache.json", () => new(), mutable: true, out IDisposable? handle);

        ContainerPointerPreloads cpp = new(ResolveCab) { Cache = SyncedCppCache.Value };
        PreloadTableResolver resolver = new([new DefaultPreloadTableResolver(), cpp]);

        SceneRepacker repacker = new StrippedSceneRepacker(resolver);

        Stopwatch mainSw = Stopwatch.StartNew();

        int total = _toRepack.Count;
        int count = 0;

        AssetHelperPlugin.InstanceLogger.LogInfo($"Repacking {_toRepack.Count} scenes");
        foreach ((string scene, HashSet<string> request) in _toRepack)
        {
            Stopwatch sw = Stopwatch.StartNew();
            AssetHelperPlugin.InstanceLogger.LogInfo($"Repacking {request.Count} objects in scene {scene}");

            RepackingParams rParams = new()
            {
                SceneBundlePath = AssetPaths.GetScenePath(scene),
                ObjectNames = request.ToList(),
                ContainerPrefix = $"{nameof(AssetHelper)}/{scene}",
                OutBundlePath = GetBundlePathForScene(scene),
            };
            RepackedBundleData repackData = repacker.Repack(rParams);

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

            count++;
            bar.SetProgress((float)count / (float)total);

            yield return null;
        }

        mainSw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Finished scene repacking after {mainSw.ElapsedMilliseconds} ms");

        handle?.Dispose();
    }

    private IEnumerator CreateSceneAssetCatalog(RepackDataCollection data)
    {
        string catalogMetadataPath = Path.ChangeExtension(SceneCatalogPath, ".json");

        if (!_didRepack
            && JsonExtensions.TryLoadFromFile(catalogMetadataPath, out SceneCatalogMetadata? oldMeta)
            && oldMeta.SilksongVersion == VersionData.SilksongVersion
            && VersionData.EarliestAcceptableSceneRepackVersion.AllowCachedData(oldMeta.PluginVersion)
            )
        {
            // We can skip only if there's no change in the repacked bundles and no change to the version metadata
            yield break;
        }

        AssetHelperPlugin.InstanceLogger.LogInfo($"Creating catalog");

        Stopwatch sw = Stopwatch.StartNew();

        CustomCatalogBuilder cbr = new(CatalogKeys.SceneCatalogId);
        foreach ((string sceneName, RepackedSceneBundleData repackBunData) in data)
        {
            if (repackBunData.Data == null) continue;
            string bundlePath = GetBundlePathForScene(sceneName);
            cbr.AddRepackedSceneData(sceneName, repackBunData.Data, bundlePath);

            // Add in requested child paths
            if (AssetRequestAPI.Request.SceneAssets.TryGetValue(sceneName, out HashSet<string> requested))
            {
                foreach (string child in requested)
                {
                    if (!ObjPathUtil.TryFindAncestor(
                        repackBunData.Data.GameObjectAssets?.Values.ToList(),
                        child,
                        out string? ancestorPath,
                        out string? relativePath
                        ))
                    {
                        AssetHelperPlugin.InstanceLogger.LogWarning($"Failed to find {child} in bundle for {sceneName} as loadable");
                        continue;
                    }
                    if (string.IsNullOrEmpty(relativePath))
                    {
                        // Directly in bundle so no need to include as child
                        continue;
                    }

                    string parentKey = CatalogKeys.GetKeyForSceneAsset(sceneName, ancestorPath);
                    string childKey = CatalogKeys.GetKeyForSceneAsset(sceneName, child);
                    ContentCatalogDataEntry entry = CatalogEntryUtils.CreateChildGameObjectEntry(parentKey, relativePath, childKey);
                    cbr.AddCatalogEntry(entry);
                }
            }
        }

        sw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Prepared catalog in {sw.ElapsedMilliseconds} ms");
        
        yield return null;

        sw = Stopwatch.StartNew();
        cbr.Build();
        sw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Finished writing catalog in {sw.ElapsedMilliseconds} ms");

        SceneCatalogMetadata metadata = new();
        metadata.SerializeToFile(catalogMetadataPath);

        yield return null;
    }
}
