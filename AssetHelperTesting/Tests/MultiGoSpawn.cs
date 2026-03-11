using Silksong.AssetHelper.ManagedAssets;
using Silksong.AssetHelper.Plugin;
using Silksong.UnityHelper.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AssetHelperTesting.Tests;

/// <summary>
/// Test associated with multiple game objects having the same name.
/// </summary>
public class MultiGoSpawn : MonoBehaviour
{
    public KeyCode RootHotkey { get; set; }

    private static ManagedAssetList<GameObject> _groupAsset;

    public static void Prepare(KeyCode rootHotkey = KeyCode.H)
    {
        GameObject go = new("Group Spawner");
        DontDestroyOnLoad(go);
        MultiGoSpawn component = go.AddComponent<MultiGoSpawn>();
        component.RootHotkey = rootHotkey;
    }

    void Awake()
    {
        _groupAsset = ManagedAssetList<GameObject>.FromSceneAsset(sceneName: "Weave_08", objPath: "Group");
        Md.HeroController.Start.Postfix(DoLoad);
    }

    private void DoLoad(HeroController self)
    {
        _groupAsset.Load();
    }


    void Update()
    {
        if (!Input.GetKeyDown(RootHotkey)) return;

        _groupAsset.EnsureLoaded();

        GameObject spawnedGroup = _groupAsset.InstantiateAsset(go => go.FindChild("Inspect Region (1)") != null);
        spawnedGroup.transform.position = HeroController.instance.transform.position + new Vector3(5, 0, 0);
        spawnedGroup.SetActive(true);
    }
}
