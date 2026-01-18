---
uid: DepsArticle
---

# Dependencies

There are two types of dependencies involved with asset bundles.

## Bundle-level dependencies

The bundle-level dependencies of a bundle are the list of asset bundles that contain assets
such that something in the original bundle depends on that asset. The list of dependency
bundles has to be included in the Addressables catalog.

Each bundle contains a list of "externals", so it is easy to compute the list of
direct dependencies, and standard bfs/dfs algorithms can turn this to a list of
transitive dependencies (which is what Addressables need to be told).

## Asset-level dependencies

The asset-level dependencies of an asset in a non-scene bundle is a list of assets
which must be loaded for the asset to function. For example, if an enemy in a scene bundle
has projectiles and a corpse in separate bundles, then the projectiles and corpse are
dependencies of the enemy.

For a non-scene bundle asset which is loadable, it will be listed in the `m_Container` list
in the internal asset bundle, which is the asset as pathID = 1 in the bundle. The entry
in the m_Container list also defines a slice of the m_PreloadTable, which enumerates pointers
to the asset-level dependencies for the asset.

AssetHelper makes a "best guess" as to the asset-level dependencies that need to be listed,
but there are tradeoffs involved; computing the full list of required asset-level dependencies
is quite costly time-wise at repack time, and taking a conservative guess (i.e. over-listing)
will cause the asset to take longer to load at run-time.

If asset-level dependencies are failing to load, then often the game will crash when you instantiate
the loaded asset with errors of at least one of the following types:
```
[Warning: Unity Log] The referenced script ... on this Behaviour ... is missing!
[Error  : Unity Log] The file ... is corrupted! Remove it and launch unity again!
```

This is technically a bug with AssetHelper because the preload table has been improperly filled out.
That said, it is generally not too hard to fix in your mod by following the following checklist.

* Check if the asset is available in a non-scene bundle. This will usually not work,
but the asset-level dependencies are filled out by Unity for non-scene bundles
so will typically be correct.

* Check the player.log for the crash. This file can be found in the saves dir (the value of `Application.persistentDataPath`)
and often contains C# method names, that can indicate which unity object is null. For example, the following block may appear:
```cs
(Mono JIT Code) (wrapper managed-to-native) UnityEngine.Object:Internal_CloneSingleWithParent_Injected (intptr,intptr,bool)
(Mono JIT Code) UnityEngine.Object:Internal_CloneSingleWithParent (UnityEngine.Object,UnityEngine.Transform,bool)
(Mono JIT Code) UnityEngine.Object:Instantiate (UnityEngine.Object,UnityEngine.Transform,bool)
(Mono JIT Code) UnityEngine.Object:Instantiate<T_REF> (T_REF,UnityEngine.Transform,bool)
(Mono JIT Code) ObjectPool:CreatePooledObjects (UnityEngine.GameObject,int,System.Collections.Generic.ICollection`1<UnityEngine.GameObject>,bool,UnityEngine.Vector3,UnityEngine.Quaternion)
(Mono JIT Code) ObjectPool:CreatePool (UnityEngine.GameObject,int,bool,UnityEngine.Vector3,UnityEngine.Quaternion,bool)
(Mono JIT Code) ObjectPool:CreatePool (UnityEngine.GameObject,int,bool)
(Mono JIT Code) PersonalObjectPool:CreatePool (UnityEngine.GameObject,int,bool,bool)
(Mono JIT Code) PersonalObjectPool:CreateStartupPools ()
(Mono JIT Code) PersonalObjectPool:EnsurePooledInSceneFinished (UnityEngine.GameObject)
(Mono JIT Code) HealthManager:OnStart ()
```
The innermost C# method here is ObjectPool.CreatePooledObjects, so there is something in this method which is null
(i.e. has failed to be declared as a dependency). Hooking/patching this method (or one of the other methods) with a prefix
to log the name of the game object can indicate what is having issues.

* Manually load the missing dependencies before loading the target object.
This is best illustrated with an example. Suppose you want to load the `Crowman Dagger (1)` asset from `greymoor_15`.
This can cause a crash, and investigation will indicate that the missing asset is a component on the
`Collectable Item Pickup Feather` non-scene asset. To load the Crowman, you can load the collectable first as follows.

```cs
// in the mod class
private ManagedAsset<GameObject> _cip;
private ManagedAsset<GameObject> _crowman;

// in Awake
_cip = ManagedAsset<GameObject>.FromNonSceneAsset(
    "localpoolprefabs_assets_areagreymoor", "Assets/Prefabs/Items/Collectable Item Pickup Feather.prefab");
_crowman = ManagedAsset<GameObject>.FromSceneAsset(
    "Crowman Dagger (1)", "greymoor_15");

// To load the crowman, the dependency must be loaded first
private IEnumerator LoadCrowman()
{
    yield return _cip.Load();  // This should be loaded first
	yield return _crowman.Load();
	
	// Instantiate the crowman and do stuff with it as normal
}
```
