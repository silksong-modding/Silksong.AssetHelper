using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.BundleTools.Repacking;
using Silksong.AssetHelper.CatalogTools;
using Silksong.AssetHelper.Internal;
using Silksong.AssetHelper.Plugin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper;

// TODO - probably remove this class before making a release
/// <summary>
/// Utility methods to test aspects of the codebase.
/// </summary>
internal static class TestExecutor
{
    public static int Completed { get;private set; }

    public static void GenViaApi()
    {
        if (JsonExtensions.TryLoadFromFile(
            Path.Combine(AssetPaths.AssemblyFolder, "serialization_data.json"),
            out Dictionary<string, List<string>>? data))
        {
            AssetRequestAPI.RequestSceneAssets(data);
        }

    }

    public static void GenFromFile()
    {
        if (!JsonExtensions.TryLoadFromFile(Path.Combine(AssetPaths.AssemblyFolder, "serialization_data.json"), out Dictionary<string, List<string>>? data))
        {
            AssetHelperPlugin.InstanceLogger.LogInfo($"No serialization_data.json found next to this assembly");
            return;
        }

        Gen(data);
    }

    public static void Gen(Dictionary<string, List<string>> rpData)
    {
        Directory.CreateDirectory(Path.Combine(AssetPaths.AssemblyFolder, "ser_dump"));

        SceneRepacker r = new StrippedSceneRepacker();
        Dictionary<string, RepackedBundleData> data = [];

        Stopwatch sw = Stopwatch.StartNew();
        foreach ((string scene, List<string> objs) in rpData!)
        {
            try
            {
                Stopwatch miniSw = Stopwatch.StartNew();
                RepackedBundleData dat = r.Repack(
                    AssetPaths.GetScenePath(scene),
                    objs,
                    $"{nameof(AssetHelper)}/{scene}",
                    Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", $"repacked_{scene}.bundle"));
                data[scene] = dat;
                miniSw.Stop();
                AssetHelperPlugin.InstanceLogger.LogInfo($"Scene {scene} complete {miniSw.ElapsedMilliseconds} ms");
                Completed += 1;
            }
            catch (Exception ex)
            {
                AssetHelperPlugin.InstanceLogger.LogError($"Scene {scene} error\n" + ex);
            }
        }

        data.SerializeToFile(Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", "repack_data.json"));
        AssetHelperPlugin.InstanceLogger.LogInfo($"All scenes complete {sw.ElapsedMilliseconds} ms");
    }


    public static void RunArchitectTest()
    {
        if (!JsonExtensions.TryLoadFromFile<Dictionary<string, RepackedBundleData>>(Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", "repack_data.json"), out var data))
        {
            return;
        }

        List<string> sceneNames = data.Keys.ToList();

        Stopwatch sw = Stopwatch.StartNew();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Determining deps for {sceneNames.Count} scenes");
        foreach (string sceneName in sceneNames)
        {
            BundleDeps.DetermineDirectDeps($"scenes_scenes_scenes/{sceneName}.bundle");
        }
        sw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Determined deps in {sw.ElapsedMilliseconds} ms");
    }

    public static void CreateFullNonSceneCatalog()
    {
        // Create a new catalog with all non-scene bundles and all container assets within those bundles

        Stopwatch gatherSw = Stopwatch.StartNew();
        AssetsManager mgr = new();

        // TODO - clean up primary keys
        List<ContentCatalogDataEntry> bundleLocs = new();
        List<ContentCatalogDataEntry> assetLocs = new();

        Dictionary<string, string> cab2key = new();
        foreach ((string cab, string name) in BundleDeps.CabLookup)
        {
            string origPrimaryKey = AddressablesData.ToBundleKey(name);
            cab2key[cab] = nameof(AssetHelper) + origPrimaryKey;
        }

        foreach (IResourceLocation locn in Addressables.ResourceLocators.First().AllLocations)
        {
            if (locn.ResourceType != typeof(IAssetBundleResource)) continue;
            if (locn.PrimaryKey.StartsWith("scenes_scenes_scenes")) continue;

            bundleLocs.Add(CatalogEntryUtils.CreateEntryFromLocation(locn, nameof(AssetHelper) + locn.PrimaryKey));

            using (MemoryStream ms = new(File.ReadAllBytes(locn.InternalId)))
            {
                BundleFileInstance bun = mgr.LoadBundleFile(ms, locn.PrimaryKey);
                AssetsFileInstance afi = mgr.LoadAssetsFileFromBundle(bun, 0);
                AssetTypeValueField iBundle = mgr.GetBaseField(afi, 1);

                List<string> deps = afi.file.Metadata.Externals
                    .Select(x => x.OriginalPathName.Split("/")[^1].ToLowerInvariant())
                    .Where(x => x.StartsWith("cab"))
                    .Select(x => cab2key[x])
                    .Prepend(nameof(AssetHelper) + locn.PrimaryKey)
                    .ToList();

                foreach (AssetTypeValueField ctrEntry in iBundle["m_Container.Array"].Children)
                {
                    string name = ctrEntry["first"].AsString;

                    // TODO - should figure out the object type properly...
                    assetLocs.Add(CatalogEntryUtils.CreateAssetEntry($"AssetHelper/Addressables/{name}", typeof(UObject), deps, out _));
                }
                mgr.UnloadAll();
            }
        }
        gatherSw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Gathered catalog entries in {gatherSw.ElapsedMilliseconds} ms");

        List<ContentCatalogDataEntry> catalog = [.. bundleLocs, .. assetLocs];

        Stopwatch writeSw = Stopwatch.StartNew();
        string catalogPath = CatalogUtils.WriteCatalog(catalog, "testCatalog");
        writeSw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Wrote catalog in {writeSw.ElapsedMilliseconds} ms");

        Stopwatch loadSw = Stopwatch.StartNew();
        IResourceLocator lr = Addressables.LoadContentCatalogAsync(catalogPath).WaitForCompletion();
        loadSw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo($"Loaded catalog in {loadSw.ElapsedMilliseconds} ms");
        DebugTools.DumpAllAddressableAssets(lr, "full_non_scene.json");
    }
}
