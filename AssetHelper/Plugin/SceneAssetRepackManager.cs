using MonoDetour.HookGen;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.BundleTools.Repacking;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RepackDataCollection = System.Collections.Generic.Dictionary<string, Silksong.AssetHelper.BundleTools.RepackedBundleData>;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Class managing the scene repacking.
/// </summary>
[MonoDetourTargets(typeof(StartManager))]
internal static class SceneAssetRepackManager
{
    #region Hooks
    internal static void Hook()
    {
        Md.StartManager.Start.Postfix(PrependStartManagerStart);
    }

    private static void PrependStartManagerStart(StartManager self, ref IEnumerator returnValue)
    {
        bool shouldRepack = Prepare(SceneAssetAPI.SceneAssetRequest);
        if (!shouldRepack)
        {
            return;
        }

        returnValue = WrapStartManagerStart(self, returnValue);
    }

    private static IEnumerator WrapStartManagerStart(StartManager self, IEnumerator original)
    {
        // TODO - turn this into a coroutine
        // TODO - Add progress bar
        Run();

        yield return original;
    }
    #endregion

    private static readonly Version _lastAcceptablePluginVersion = Version.Parse("0.1.0");
    private static string _repackDataPath = Path.Combine(AssetPaths.RepackedSceneBundleDir, "repack_data.json");
    
    private static RepackDataCollection _repackData = [];

    /// <summary>
    /// Event raised each time a single scene is repacked.
    /// </summary>
    internal static event Action? SingleRepackOperationCompleted;

    /// <summary>
    /// Prepare the repacking request.
    /// </summary>
    /// <param name="toRepack">The collection of {scene: object names} in the request.</param>
    /// <returns>True if there is any repacking to be done.</returns>
    internal static bool Prepare(Dictionary<string, HashSet<string>> toRepack)
    {
        if (JsonExtensions.TryLoadFromFile(_repackDataPath, out RepackDataCollection? repackData))
        {
            _repackData = repackData;
        }
        else
        {
            _repackData = [];
        }

        Dictionary<string, HashSet<string>> updatedToRepack = [];

        foreach ((string scene, HashSet<string> request) in toRepack)
        {
            if (!_repackData.TryGetValue(scene, out RepackedBundleData existingBundleData))
            {
                updatedToRepack[scene] = request;
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
                updatedToRepack[scene] = request;
                continue;
            }

            if (request.All(x => existingBundleData.TriedToRepack(x)))
            {
                // No need to re-repack as there's nothing new to try
                continue;
            }

            updatedToRepack[scene] = new(request
                .Union(existingBundleData.GameObjectAssets?.Values ?? Enumerable.Empty<string>())
                .Union(existingBundleData.NonRepackedAssets ?? Enumerable.Empty<string>())
                );
        }

        return true;
    }

    /// <summary>
    /// Run the repacking procedure so that by the end, anything in the request which could be repacked has been.
    /// </summary>
    internal static void Run()
    {
        throw null;
        Dictionary<string, HashSet<string>> updatedToRepack = [];

        SceneRepacker repacker = new StrippedSceneRepacker();

        AssetHelperPlugin.InstanceLogger.LogInfo($"Repacking {updatedToRepack.Count} scenes");
        foreach ((string scene, HashSet<string> request) in updatedToRepack)
        {
            AssetHelperPlugin.InstanceLogger.LogInfo($"Repacking {request.Count} objects in scene {scene}");
            RepackedBundleData newData = repacker.Repack(scene, request.ToList(), Path.Combine(AssetPaths.RepackedSceneBundleDir, $"repacked_{scene}.bundle"));
            _repackData[scene] = newData;
            _repackData.SerializeToFile(_repackDataPath);
            SingleRepackOperationCompleted?.Invoke();
        }
    }
}
