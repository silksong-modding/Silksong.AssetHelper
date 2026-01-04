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
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
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

    internal static string CreateCatalog(RepackDataCollection data)
    {
        // TODO - clean this up

        Stopwatch sw = Stopwatch.StartNew();
        void Log(string msg, [CallerLineNumber] int lineno = -1) => AssetHelperPlugin.InstanceLogger.LogInfo($"{msg} [@{lineno}] [{sw.ElapsedMilliseconds} ms]");
        Log("Started");

        List<ContentCatalogDataEntry> addedEntries = [];
        Dictionary<string, ContentCatalogDataEntry> bundleLookup = [];
        Dictionary<string, string> pkLookup = [];
        HashSet<string> bundlesToInclude = [];

        foreach (IResourceLocation location in Addressables.ResourceLocators.First().AllLocations)
        {
            if (location.ResourceType != typeof(IAssetBundleResource)) continue;
            if (location.PrimaryKey.StartsWith("scenes_scenes_scenes")) continue;

            if (!AssetsData.TryStrip(location.PrimaryKey, out string? stripped)) continue;
            bundleLookup[stripped] = CatalogEntryUtils.CreateEntryFromLocation(location, out string newPrimaryKey);
            pkLookup[stripped] = newPrimaryKey;
        }

        Log($"Created {bundleLookup.Count} existing bundle entries");

        foreach ((string scene, RepackedBundleData bundleData) in data)
        {
            // Create an entry for the bundle
            string repackedSceneBundleKey = $"AssetHelper/RepackedScenes/{scene}";

            ContentCatalogDataEntry bundleEntry = CatalogEntryUtils.CreateBundleEntry(
                repackedSceneBundleKey,
                Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", $"repacked_{scene}.bundle"),
                bundleData.BundleName!,
                []);
            addedEntries.Add(bundleEntry);

            List<string> dependencyKeys = [repackedSceneBundleKey];
            foreach (string dep in BundleDeps.DetermineDirectDeps($"scenes_scenes_scenes/{scene}.bundle"))
            {
                string depKey = dep.Replace(".bundle", "");
                bundlesToInclude.Add(depKey);
                dependencyKeys.Add(pkLookup[depKey]);
            }

            foreach ((string containerPath, string objPath) in bundleData.GameObjectAssets ?? [])
            {
                ContentCatalogDataEntry entry = CatalogEntryUtils.CreateAssetEntry(
                    containerPath,
                    typeof(GameObject),
                    dependencyKeys,
                    $"AssetHelper/RepackedAssets/{scene}/{objPath}"
                    );
                addedEntries.Add(entry);
            }
        }

        List<ContentCatalogDataEntry> allEntries = new();
        allEntries.AddRange(bundlesToInclude.Select(x => bundleLookup[x]));
        allEntries.AddRange(addedEntries);

        Log($"Placed {allEntries.Count} entries in catalog list");

        string catalogPath = CatalogUtils.WriteCatalog(allEntries, "repackedSceneCatalog");

        Log("Wrote catalog");

        return catalogPath;
    }
}
