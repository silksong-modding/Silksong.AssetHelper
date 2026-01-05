using MonoDetour.HookGen;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.BundleTools.Repacking;
using Silksong.AssetHelper.CatalogTools;
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

            IEnumerator catalogCreate = CreateCatalog(_repackData);
            while (catalogCreate.MoveNext())
            {
                yield return null;
            }

            yield return null;
        }

        AsyncOperationHandle<IResourceLocator> catalogLoadOp = Addressables.LoadContentCatalogAsync(Path.Combine(AssetPaths.CatalogFolder, $"{SCENE_ASSET_CATALOG_KEY}.bin"));
        yield return catalogLoadOp;

        AssetRequestAPI.AfterBundleCreationComplete.Activate();

        yield return original;
        yield break;
    }
    #endregion

    private const string SCENE_ASSET_CATALOG_KEY = "AssetHelper-RepackedSceneData";

    private static readonly Version _lastAcceptablePluginVersion = Version.Parse("0.1.0");
    private static string _repackDataPath = Path.Combine(AssetPaths.RepackedSceneBundleDir, "repack_data.json");

    private static Dictionary<string, HashSet<string>> _toRepack = [];
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

    internal static IEnumerator CreateCatalog(RepackDataCollection data)
    {
        AssetHelperPlugin.InstanceLogger.LogInfo($"Creating catalog");
        CustomCatalogBuilder cbr = new(SCENE_ASSET_CATALOG_KEY);
        foreach ((string sceneName, RepackedSceneBundleData repackBunData) in data)
        {
            if (repackBunData.Data == null) continue;
            string bundlePath = Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{sceneName}.bundle");
            cbr.AddRepackedSceneData(sceneName, repackBunData.Data, bundlePath);
        }

        // TODO - add in requested child paths

        yield return null;

        string catalogPath = cbr.Build();
    }
}
