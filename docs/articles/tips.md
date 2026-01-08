# Tips, tricks and common pitfalls

## Gotchas

The following are common issues that can happen with loaded assets and AssetHelper.

* Never modify the asset directly! 
This can cause issues for anyone else - including the base game - who uses
the asset. Instead, if it is a GameObject, you should always Instantiate the
asset before modifying it.

* Make sure to apply necessary modifications to the clone
The cloned game object may or may not have properties you don't want. Often
you will need to:
  - Set the name of the cloned game object
  - Set the position of the cloned game object
  - Remove certain components from the game object
  In particular, a PersistentBoolItem can sometimes cause problems if not intended.

### Requesting assets

When requesting assets using the @"Silksong.AssetHelper.Plugin.AssetRequestAPI" API,
the following bits of advice might be useful.

* List exactly the game objects you want to spawn
For example, if you want to spawn the following two enemies from scene Memory_Coral_Tower:
  - `Battle Scenes/Battle Scene Chamber 2/Wave 10/Coral Brawler (1)`
  - `Battle Scenes/Battle Scene Chamber 2/Wave 1/Coral Hunter`
it can be tempting to request the `Battle Scenes/Battle Scene Chamber 2` parent
and find the children directly. This is less efficient than requesting the children
you need separately, and AssetHelper will create Addressables paths for both individual enemies.

Of course, if you need access to the `Battle Scenes/Battle Scene Chamber 2` asset itself,
then you should request it.

* Distinguish between scenes and sub-scenes
For example, if you wish to load Moorwing from Greymoor_08, you will not find it;
that is because it is stored in Greymoor_08_boss. The correct scene/sub-scene name
is required for AssetHelper.

* Game object paths may change at runtime
Some game objects change their parent at runtime. For example, in scene Dust_Chef,
the kitchen_string object has path `Battle Parent/Kitchen Pipe Gong/kitchen_string_offset/kitchen_string`
in the bundle, but during the Awake method of one of its child components it sets its
parent to null. AssetHelper needs to know the path of the game object in the bundle,
not the path at runtime!

## Testing checklist

It is not necessary to check everything on the following list, but there are several
different scenarios in which you may want to test your asset.

* In the scene that the base game object usually appears
* In a scene that the base game object does not appear
* Load the asset and then change scene
* Load the asset and then return to menu
* Load the asset, return to menu, re-enter game and then spawn the asset again

Warnings of the following types indicate a dependency issue and may indicate a bug with AssetHelper.
```
[Warning: Unity Log] The referenced script (Unknown) on this Behaviour is missing!
[Warning: Unity Log] The referenced script on this Behaviour (...) is missing!
```
