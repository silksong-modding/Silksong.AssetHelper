# Quickstart

The easiest way to use AssetHelper to load assets is as follows.

## Request assets during your plugin's Awake method

Use the @"Silksong.AssetHelper.ManagedAssets.ManagedAsset`1.FromSceneAsset(System.String,System.String)"
and @"Silksong.AssetHelper.ManagedAssets.ManagedAsset`1.FromNonSceneAsset(System.String,System.String)"
functions during your plugin's Awake method to create wrappers around any assets you want to access.
These will automatically be requested via the @"Silksong.AssetHelper.Plugin.AssetRequestAPI"
API

## Load up assets

You can call the @"Silksong.AssetHelper.ManagedAssets.ManagedAsset`1.Load"
method to load up the assets. The absolute earliest this can theoretically be done is in
a callback to @"Silksong.AssetHelper.Plugin.AssetRequestAPI.InvokeAfterBundleCreation(System.Action)".
However, this will often break; in general, it is best to wait either until the player enters a scene where your
asset should be in use or until the player enters the game, but at the very least it
should not be prior to GameManager.Awake.

* Never modify the asset directly! 

This will cause issues for anyone else - including the base game - who uses
the asset. Instead, if it is a GameObject, you should always Instantiate the
asset before modifying it.

## Instantiate your assets

The assets can be instantiated at any time from the ManagedAsset instance (provided it has been loaded),
for example by using
@"Silksong.AssetHelper.ManagedAssets.ManagedAssetExtensions.InstantiateAsset``1(Silksong.AssetHelper.ManagedAssets.ManagedAsset{``0})".

## Example

Here, we present an example plugin which represents a typical usage pattern - loading
an asset from one scene, and spawning it in another scene.

```cs
using Silksong.AssetHelper.ManagedAssets;
using BepInEx;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[BepInAutoPlugin(id: "io.github.username.my-mod")]
[BepInDependency(AssetHelperPlugin.Id)]
public class FleaSignPlugin : BaseUnityPlugin
{
    private ManagedAsset<GameObject> _fleaSign;

    void Awake()
    {
        // Constructing this object during Awake means it is automatically requested to be repacked.
        _fleaSign = ManagedAsset<GameObject>.FromSceneAsset(
            // Note - scene names are case-insensitive, this could also be Bone_02
            sceneName: "bone_02",
            // If the object had a parent, you'd need to put it as parentName/caravan_signpost
            objPath: "caravan_signpost");

        // Spawn the asset whenever they enter Bonetown
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += SpawnAsset;
    }

    private void SpawnAsset(Scene oldScene, Scene newScene)
    {
        if (newScene.name != "Bonetown")
        {
            return;
        }

        StartCoroutine(SpawnAssetRoutine());
    }

    private IEnumerator SpawnAssetRoutine()
    {
        // Load the asset if it is not loaded yet.
        // If it is already loaded, this function will do nothing, so there is no need to check.
        _fleaSign.Load();

        // Wait for the sign to be loaded before doing anything.
        yield return _fleaSign.Handle;
        
        // Check if there was an error loading the asset.
        if (_fleaSign.Handle.OperationException != null)
        {
            Logger.LogError($"Error loading asset: {_fleaSign.Handle.OperationException}");
            // No reason to continue the coroutine because there's no asset to spawn
            yield break;
        }

        // Spawn the sign. We can apply any modifications to the copy before setting it active.
        // This is the same as doing Instantiate(_fleaSign.Handle.Result);
        GameObject spawnedSign = _fleaSign.InstantiateAsset();

        // It is good practice to change the name of the object we spawn to something
        // that indicates where it came from
        spawnedSign.name = "Bonetown FleaSign";

        // Set the position of the spawned sign to something we want
        // Note: In the original scene, Hornet's y coordinate is 35.57 and the original sign's is 35.48
        // In the new scene, Hornet's y position is 7.57 so we subtract the same number (0.09) to
        // get it to look right
        spawnedSign.transform.SetPositionX(295f);
        spawnedSign.transform.SetPositionY(7.48f);

        // Apply any other modifications. You will need to figure out which modifications are
        // needed for your own use case!
		// Here, the TestGameObjectActivator will deactivate the gameObject under certain conditions,
		// so we remove the component.
        Destroy(spawnedSign.GetComponent<TestGameObjectActivator>());

        spawnedSign.SetActive(true);
    }
}
```
