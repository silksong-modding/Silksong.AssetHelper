using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.BundleTools.Repacking;
using Silksong.AssetHelper.Internal;
using Silksong.AssetHelper.LoadedAssets;
using Silksong.UnityHelper.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Silksong.AssetHelper;

// TODO - probably remove this class before making a release
/// <summary>
/// Utility methods to test aspects of the codebase.
/// </summary>
internal static class TestExecutor
{
    public static int Completed { get;private set; }

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
                RepackedBundleData dat = r.Repack(AssetPaths.GetScenePath(scene), objs, Path.Combine(AssetPaths.AssemblyFolder, "ser_dump", $"repacked_{scene}.bundle"));
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

    public static IEnumerator InstantiateAsset(string bundleFile, string sceneName, string assetPath)
    {
        Stopwatch sw = Stopwatch.StartNew();

        AssetHelperPlugin.InstanceLogger.LogInfo($"Start with loaded bundle count: {AssetBundle.GetAllLoadedAssetBundles().Count()}: {sw.ElapsedMilliseconds} ms");

        // Load dependencies
        AssetBundleGroup? dependencyGrp = AssetBundleGroup.CreateForScene(sceneName, false);  // Set to true for shallow bundle

        yield return dependencyGrp.LoadAsync();

        AssetHelperPlugin.InstanceLogger.LogInfo($"Deps loaded, new count: {AssetBundle.GetAllLoadedAssetBundles().Count()}: {sw.ElapsedMilliseconds} ms");

        // Load bundle
        var req = AssetBundle.LoadFromFileAsync(bundleFile);
        yield return req;
        AssetBundle loadedModBundle = req.assetBundle;

        AssetHelperPlugin.InstanceLogger.LogInfo($"Main Bundle loaded: {sw.ElapsedMilliseconds} ms");

        // Spawn mask shard
        GameObject theAsset = loadedModBundle.LoadAsset<GameObject>(assetPath);
        AssetHelperPlugin.InstanceLogger.LogInfo($"Asset loaded: {sw.ElapsedMilliseconds} ms");

        GameObject go = UObject.Instantiate(theAsset);
        go.name = $"SpawnedAsset-{GetRandomString()}";

        if (HeroController.instance != null)
        {
            go.transform.position = HeroController.instance.transform.position + new Vector3(0, 3, 0);
        }

        go.SetActive(true);

        AssetHelperPlugin.InstanceLogger.LogInfo($"Spawned: {sw.ElapsedMilliseconds} ms");

        yield return null;

        static string GetRandomString()
        {
            string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            char[] res = new char[10];
            System.Random rng = new();

            for (int i = 0; i < 10; i++)
                res[i] = chars[rng.Next(chars.Length)];

            return new string(res);
        }
    }
}
