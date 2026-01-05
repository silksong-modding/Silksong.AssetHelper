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
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using RepackDataCollection = System.Collections.Generic.Dictionary<string, Silksong.AssetHelper.BundleTools.RepackedBundleData>;

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
        bool shouldRepack = Prepare();

        returnValue = WrapStartManagerStart(self, returnValue, shouldRepack);
    }

    private static IEnumerator WrapStartManagerStart(StartManager self, IEnumerator original, bool shouldRepack)
    {
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

        _toRepack = [];

        foreach ((string scene, HashSet<string> request) in AssetRequestAPI.SceneAssetRequest)
        {
            if (!_repackData.TryGetValue(scene, out RepackedBundleData existingBundleData))
            {
                _toRepack[scene] = request;
                continue;
            }

            // TODO - accept silksong version changes if the bundle hasn't changed
            Version current = Version.Parse(AssetHelperPlugin.Version);
            if (existingBundleData.SilksongVersion != AssetPaths.SilksongVersion
                || !Version.TryParse(existingBundleData.PluginVersion ?? string.Empty, out Version oldPluginVersion)
                || oldPluginVersion > current
                || oldPluginVersion < _lastAcceptablePluginVersion
                )
            {
                _toRepack[scene] = request;
                continue;
            }

            if (request.All(x => existingBundleData.TriedToRepack(x)))
            {
                // No need to re-repack as there's nothing new to try
                continue;
            }

            _toRepack[scene] = new(request
                .Union(existingBundleData.GameObjectAssets?.Values ?? Enumerable.Empty<string>())
                .Union(existingBundleData.NonRepackedAssets ?? Enumerable.Empty<string>())
                );
        }

        return _toRepack.Count > 0;
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
            RepackedBundleData newData = repacker.Repack(
                AssetPaths.GetScenePath(scene),
                request.ToList(),
                Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{scene}.bundle")
                );
            _repackData[scene] = newData;
            _repackData.SerializeToFile(_repackDataPath);
            AssetHelperPlugin.InstanceLogger.LogInfo($"Repacked {scene} in {sw.ElapsedMilliseconds} ms");

            yield return null;
        }
    }

    internal static IEnumerator CreateCatalog(RepackDataCollection data)
    {
        AssetHelperPlugin.InstanceLogger.LogInfo($"Creating catalog");
        CustomCatalogBuilder cbr = new(SCENE_ASSET_CATALOG_KEY);
        foreach ((string sceneName, RepackedBundleData bunData) in data)
        {
            string bundlePath = Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{sceneName}.bundle");
            cbr.AddRepackedSceneData(sceneName, bunData, bundlePath);
        }

        // TODO - add in requested child paths

        yield return null;

        string catalogPath = cbr.Build();
    }
}
