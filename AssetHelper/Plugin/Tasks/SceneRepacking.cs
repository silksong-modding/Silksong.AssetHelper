using AssetHelperLib.BundleTools;
using AssetHelperLib.BundleTools.Repacking;
using Silksong.AssetHelper.CatalogTools;
using Silksong.AssetHelper.Internal;
using AssetHelperLib.Util;
using System;
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

namespace Silksong.AssetHelper.Plugin.Tasks;

/// <summary>
/// Run the routine to repack scene assets, create a catalog and load it.
/// </summary>
internal class SceneRepacking : BaseStartupTask
{
    // Invalidate all data on disk that's older than this version
    private static readonly Version _lastAcceptablePluginVersion = Version.Parse("0.1.0");

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

        while (repack.MoveNext())
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

        // Only load the catalog if anyone's requested scene assets
        if (_repackData.Count > 0)
        {
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

            // TODO - Verify that the bundle exists
            // People might delete the existing bundle so we may have to re-repack in that case

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

    /// <summary>
    /// Run the repacking procedure so that by the end, anything in the request which could be repacked has been.
    /// </summary>
    private IEnumerator RunRepacking(LoadingBar bar)
    {
        SceneRepacker repacker = new StrippedSceneRepacker();

        int total = _toRepack.Count;
        int count = 0;

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

            count++;
            bar.SetProgress((float)count / (float)total);

            yield return null;
        }
    }

    private IEnumerator CreateSceneAssetCatalog(RepackDataCollection data)
    {
        // TODO - check game objects in metadata
        // for now we can just check if there's a metadata change
        string catalogMetadataPath = Path.ChangeExtension(SceneCatalogPath, ".json");

        if (!_didRepack
            && JsonExtensions.TryLoadFromFile(catalogMetadataPath, out SceneCatalogMetadata? oldMeta)
            && oldMeta.SilksongVersion == VersionData.SilksongVersion
            && Version.TryParse(oldMeta.PluginVersion, out Version oldVersion)
            && oldVersion <= Version.Parse(AssetHelperPlugin.Version)
            && oldVersion >= _lastAcceptablePluginVersion
            )
        {
            // We can skip only if there's no change in the repacked bundles and no change to the version metadata
            yield break;
        }

        AssetHelperPlugin.InstanceLogger.LogInfo($"Creating catalog");
        CustomCatalogBuilder cbr = new(CatalogKeys.SceneCatalogId);
        foreach ((string sceneName, RepackedSceneBundleData repackBunData) in data)
        {
            if (repackBunData.Data == null) continue;
            string bundlePath = Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{sceneName}.bundle");
            cbr.AddRepackedSceneData(sceneName, repackBunData.Data, bundlePath);

            // Add in requested child paths
            if (AssetRequestAPI.SceneAssetRequest.TryGetValue(sceneName, out HashSet<string> requested))
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

        yield return null;

        cbr.Build();

        SceneCatalogMetadata metadata = new();
        metadata.SerializeToFile(catalogMetadataPath);

        yield return null;
    }
}
