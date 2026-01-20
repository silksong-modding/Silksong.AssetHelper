# Tips, tricks and common pitfalls

## Gotchas

The following are common issues that can happen with loaded assets and AssetHelper.

* Never modify the asset directly! 

This will cause issues for anyone else - including the base game - who uses
the asset. Instead, if it is a GameObject, you should always Instantiate the
asset before modifying it.

* Make sure to apply necessary modifications to the clone.

The cloned game object may or may not have properties you don't want. Often
you will need to:
  - Set the name of the cloned game object
  - Set the position of the cloned game object
  - Remove certain components from the game object. Common components to remove include:
    - PersistentBoolItem (which the base game uses to disable the game object if it has already been "activated")
    - TestGameObjectActivator, DeactivateIfPlayerdataTrue, DeactivateIfPlayerdataFalse
    - ConstrainPosition
	
* Don't yield multiple asset loads sequentially.

It is a more efficient to start of each load at once, and only yield once. As an example:

```cs
ManagedAsset<GameObject> object1 = ...;
ManagedAsset<GameObject> object2 = ...;
ManagedAsset<GameObject> object3 = ...;

// Bad
IEnumerator LoadAll()
{
    // Start loading object1, and pause the coroutine until object1 is loaded
    yield return object1.Load();
    // Start loading object2, and pause the coroutine until object2 is loaded
    yield return object2.Load();
    // Start loading object3, and pause the coroutine until object3 is loaded
    yield return object3.Load();
}

// Better
IEnumerator LoadAll()
{
    // Start loading object1
    object1.Load();
    // Start loading object2
    object2.Load();
    // Start loading object3
    object3.Load();
    // {ause the coroutine until they are all loaded
    yield return new WaitUntil(() => object1.IsLoaded && object2.IsLoaded && object3.IsLoaded);
}
```

## Requesting assets

When requesting assets using the @"Silksong.AssetHelper.Plugin.AssetRequestAPI" API,
the following bits of advice might be useful.

* List exactly the game objects you want to spawn.

For example, if you want to spawn the following two enemies from scene Memory_Coral_Tower:
  - `Battle Scenes/Battle Scene Chamber 2/Wave 10/Coral Brawler (1)`
  - `Battle Scenes/Battle Scene Chamber 2/Wave 1/Coral Hunter`
it can be tempting to request the `Battle Scenes/Battle Scene Chamber 2` parent
and find the children directly. This is less efficient than requesting the children
you need separately, and AssetHelper will create Addressables paths for both individual enemies.

Of course, if you need access to the `Battle Scenes/Battle Scene Chamber 2` asset itself,
then you should request it.

* Distinguish between scenes and sub-scenes.

For example, if you wish to load Moorwing from Greymoor_08, you will not find it;
that is because it is stored in Greymoor_08_boss. The correct scene/sub-scene name
is required for AssetHelper.

* Game object paths may change at runtime.

Some game objects change their parent at runtime. For example, in scene Dust_Chef,
the kitchen_string object has path `Battle Parent/Kitchen Pipe Gong/kitchen_string_offset/kitchen_string`
in the bundle, but during the Awake method of one of its child components it sets its
parent to null. AssetHelper needs to know the path of the game object in the bundle,
not the path at runtime!

* Prefer non-scene assets.

Most assets are either only available in scenes or only available in non-scene bundles.
However, there are a few assets which are available in both forms. For example, to load
a Roachcatcher, you can load it with one of the following two ways:
* A non-scene asset called `Assets/Prefabs/Hornet Enemies/Roachfeeder Short.prefab` in
the `localpoolprefabs_assets_areadust` bundle
* A scene asset called `Roachfeeder Short` in scenes such as Dust_02

It is always more efficient to load the non-scene asset, if possible.

* Prefer loading from fewer/smaller bundles.

It's hard to predict exactly how long it will take to repack a scene bundle
but bundles that take up more space on disk are more likely to take longer
to repack. For example, the coral_23.bundle file is significantly larger
than the coral_44.bundle file so if you just need one asset that
is in both bundles it is better to choose coral_44.

That said, it is also faster to load fewer bundles, so given the choice
between two separate bundles or one bundle for two objects it is typically
better to choose the latter.

## Testing checklist

It is not necessary to check everything on the following list, but there are several
different scenarios in which you may want to test your asset.

* In the scene that the base game object usually appears
* In a scene that the base game object does not appear
* Load the asset and then change scene
* Load the asset and then return to menu
* Load the asset, return to menu, re-enter game and then spawn the asset again

Warnings of the following types typically indicate a dependency issue;
see <xref:DepsArticle> for more information.
```
[Warning: Unity Log] The referenced script ... on this Behaviour ... is missing!
[Error  : Unity Log] The file ... is corrupted! Remove it and launch unity again!
```
These warnings may also appear if you have loaded the asset too early.

Many other warnings and errors in the log happen in the base game. If there are warnings that
appear in the log when the asset appears, but they also appear when you enter the asset's
base game scene, then they can probably be safely ignored.
