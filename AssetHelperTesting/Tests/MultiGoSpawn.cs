using Silksong.AssetHelper.Plugin;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AssetHelperTesting.Tests;

/// <summary>
/// Tests associated with multiple game objects having the same name.
/// </summary>
public class MultiGoSpawn : MonoBehaviour
{
    public KeyCode RootHotkey { get; set; }

    public static void Prepare(KeyCode rootHotkey = KeyCode.H)
    {
        GameObject go = new("Group Spawner");
        DontDestroyOnLoad(go);
        MultiGoSpawn component = go.AddComponent<MultiGoSpawn>();
        component.RootHotkey = rootHotkey;
    }

    void Awake()
    {
        AssetRequestAPI.RequestSceneAsset("Weave_08", "Group");
    }

    void Update()
    {
        if (!Input.GetKeyDown(RootHotkey)) return;

        string key = CatalogKeys.GetKeyForSceneAsset("Weave_08", "Group");

        var handle = Addressables.LoadAssetsAsync<GameObject>(key);
        handle.WaitForCompletion();
        IList<GameObject> result = handle.Result;

        AssetHelperTestingPlugin.InstanceLogger.LogInfo($"Num assets: {result.Count}");

        foreach (GameObject go in result)
        {
            AssetHelperTestingPlugin.InstanceLogger.LogInfo(go.name);
            foreach (Transform t in go.transform)
            {
                AssetHelperTestingPlugin.InstanceLogger.LogInfo($"- {t.gameObject.name}");
            }
            AssetHelperTestingPlugin.InstanceLogger.LogInfo("");
        }
    }

}
