using BepInEx.Logging;
using Silksong.AssetHelper.LoadedAssets;
using Silksong.AssetHelper.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using NameListLookup = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>;

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Class providing tools to help find information about the asset database.
/// </summary>
public static class DebugTools
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"AssetHelper.{nameof(DebugTools)}");

    private static DirectoryInfo AssemblyFolder => Directory.GetParent(typeof(DebugTools).Assembly.Location);

    /// <summary>
    /// Dump all addressable keys to the bundle_keys.json file next to this assembly.
    /// 
    /// The keys in this dictionary should be used to construct a <see cref="AssetBundleGroup"/>.
    /// </summary>
    public static void DumpAddressablesKeys()
    {
        string dumpFile = Path.Combine(AssemblyFolder.FullName, "bundle_keys.json");

        AssetsData.InvokeAfterAddressablesLoaded(() => AssetsData.BundleKeys.SerializeToFileInBackground(dumpFile));
    }

    /// <summary>
    /// Dump all asset names to the asset_names.json file next to this assembly.
    /// 
    /// This function loads all asset bundles, so is quite slow.
    /// </summary>
    public static void DumpAllAssetNames()
    {
        AssetsData.InvokeAfterAddressablesLoaded(DumpAllAssetNamesInternal);
    }

    private static void DumpAllAssetNamesInternal()
    {
        string dumpFile = Path.Combine(AssemblyFolder.FullName, "asset_names.json");

        NameListLookup assetNames = [];
        NameListLookup sceneNames = [];
        
        Stopwatch sw = Stopwatch.StartNew();
        foreach ((string name, string key) in AssetsData.BundleKeys!)
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
}
