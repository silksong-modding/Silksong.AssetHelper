# Unity Crash Course

This article contains information related to asset loading in Unity and Silksong
that is relevant for modding. It is not too important to read this for modding
but the knowledge may be useful for certain applications.

## Asset bundles

An asset bundle is a file containing game assets, such as game objects, components,
sprites and audio clips. In Silksong, they are stored in the game folder
at `<GameDir>/Hollow Knight Silksong_Data/StreamingAssets/aa/<OSFolder>`,
where `<OSFolder>` is StandaloneWindows64, StandaloneOSX or StandaloneLinux64.

Each asset bundle can only be loaded once. If you try to load an asset bundle through
the normal AssetBundle.LoadFromFile API, and it has already been loaded, Unity
will throw an error.

## Scene vs non-scene bundles

There are two types of asset bundles: scene bundles and non-scene bundles.

In a non-scene bundle, many assets can be accessed easily, as long as you know
the name of the asset (this typically includes any root game object and most
scriptable objects contained within the bundle) - which assets are accessible
in this way is determined at build time.

In a scene bundle, the only thing that can be accessed is the scene itself,
and any game objects within the scene can only be accessed by loading the
entire scene. This is an optimization for unmodded gameplay, as Unity doesn't need
to figure out how to access those game objects individually, but is inconvenient for
modded because it makes placing those objects in different scenes harder.

In Silksong, every bundle in the scenes_scenes_scenes folder is a scene bundle,
and all other bundles are non-scene bundles.

Typically, game objects can be found in non-scene bundles if they are spawned by other
objects; this includes things like projectiles, enemy corpses and certain
enemies.

## Dependencies

Asset bundles often have dependencies, which is where one asset bundle references assets
located in another asset bundle. For example, the game object for an enemy might be located
in one asset bundle, along with all of its components, but those components might reference
assets in another bundle - common examples might be the enemy has its sprite (referenced by
the SpriteRenderer component) in another bundle, or an FSM on the enemy might reference
its projectiles, which are located in another bundle.

Unity automatically associates the assets with their references, provided the corresponding
asset bundles are loaded. For example, if an enemy in bundle A references projectiles in bundle B,
then as long as bundles A and B are loaded the projectiles will work correctly - you don't
need to do any additional work to get this working.

If bundle B is unloaded, then the projectiles will no longer work properly - unless the
unloadAllLoadedObjects parameter is set to false when the bundle is unloaded.

## Addressables

Addressables is a library published by Unity to help with asset loading, and Team Cherry
use this to manage their asset bundles. There are several advantages to working
with Addressables:

* Addressables counts references to asset bundles.
For example, if you load a bundle three times using Addressables, Unity will only actually
load it once, and if you unload it twice, Unity won't unload the bundle because Addressables
knows it's still in use.

* Addressables lets you load individual assets automatically.
This only applies to certain assets set at build time (by Team Cherry). An example in Silksong
is the audio played by psalm cylinders; the CollectableRelic item has a gramaphoneClipRef
field, which holds a key to an AudioClip that can be freely loaded with Addressables. (And this
AudioClip can be loaded whenever you try to play the psalm cylinder in Whispering Vaults or Bellhome).

* Addressables can handle dependencies.
Any addressables asset can be associated with dependencies,
and Addressables will load the dependencies whenever it tries to load the asset.

## AssetHelper

AssetHelper provides two main functions:

- Repacking scene bundles to non-scene bundles.
Any scene assets requested via the API are repacked into non-scene bundles when the game first loads.
This only needs to be done once (until the base game gets updated and the bundles change), and allows
the repacked assets to be loaded easily at runtime.

- Creating Addressables keys for non-scene assets.
Note that this includes all scene assets repacked via the above API, as well as any base game non-scene
assets for which an Addressables key was requested.

## Links

* [Asset bundle docs](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetBundlesIntro.html)
* [Addressables docs](https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/index.html)
