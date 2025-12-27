using BepInEx;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.LoadedAssets;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Silksong.AssetHelper;

[BepInAutoPlugin(id: "io.github.flibber-hk.assethelper")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public partial class AssetHelperPlugin : BaseUnityPlugin
{
    public static AssetHelperPlugin Instance { get; private set; }
    #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    private static readonly Dictionary<string, string> Keys = [];

    private void Awake()
    {
        Instance = this;
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");

        Deps.Setup();

        GameEvents.Hook();

#if DEBUG
        // TESTING CODE - should delete
        BundleCreate.DoDebug();

        GameEvents.OnQuitToMenu += () =>
        {
            _loadedModBundle?.Unload(true);
            _loadedModBundle = null;
        };
#endif
    }

#if DEBUG
    private AssetBundle? _loadedModBundle;
    private AssetBundleGroup? dependencyGrp;


    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.H)) return;

        StartCoroutine(LoadAndSpawn());
    }

    IEnumerator LoadAndSpawn()
    {
        Stopwatch sw = Stopwatch.StartNew();
        Logger.LogInfo($"Start {AssetBundle.GetAllLoadedAssetBundles().Count()}: {sw.ElapsedMilliseconds} ms");


        // Load dependencies
        if (dependencyGrp is null)
        {
            dependencyGrp = AssetBundleGroup.CreateWithDependencies("scenes_scenes_scenes/dust_02");
        }
        yield return dependencyGrp.LoadAsync();

        Logger.LogInfo($"Deps loaded {AssetBundle.GetAllLoadedAssetBundles().Count()}: {sw.ElapsedMilliseconds} ms");

        // Load bundle
        if (_loadedModBundle == null)
        {
            var req = AssetBundle.LoadFromFileAsync(Path.Combine(AssetPaths.AssemblyFolder, "repacked_rfs.bundle"));
            yield return req;
            _loadedModBundle = req.assetBundle;
        }

        Logger.LogInfo($"MB loaded: {sw.ElapsedMilliseconds} ms");

        // Spawn mask shard
        GameObject go = UObject.Instantiate(_loadedModBundle.LoadAsset<GameObject>("AssetHelper/Roachfeeder Short.prefab"));
        go.name = $"RFS-{GetRandomString()}";

        go.transform.position = HeroController.instance.transform.position + new Vector3(0, 3, 0);
        go.SetActive(true);

        Logger.LogInfo($"Spawned: {sw.ElapsedMilliseconds} ms");

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
#endif

    private IEnumerator Start()
    {
        // Addressables isn't initialized until the next frame
        yield return null;

        while (true)
        {
            // Check this just in case
            bool b = AssetsData.TryLoadBundleKeys();
            if (b)
            {
                yield break;
            }

            yield return null;
        }
    }

    private void OnApplicationQuit()
    {
        GameEvents.AfterQuitApplication();
    }
}
