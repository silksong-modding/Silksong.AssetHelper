---
uid: assetbundleassets
---

# Assetbundle Assets

AssetBundle assets are typically loaded with Addressables; AssetHelper provides several
utilities for loading assetbundle assets with Addressables without needing to
manually manage memory or learn the Addressables API.

## Loaded Assets

The standard way to load an asset is with @"Silksong.AssetHelper.AssetLoadUtil.LoadAsset`1".
This will return an object of type @"Silksong.AssetHelper.LoadedAssets.LoadedAsset`1".

### Loading a Loaded Asset

To load an asset you need to know four things:

* The name of the bundle containing the asset
* The name of the asset within the bundle
* The type of the asset
* Any dependencies for the asset

The bundle name is typically the filepath, starting from the StreamingAssets/aa/StandaloneX
directory, with .bundle removed from the end of the file (if it ends .bundle).

The name of the asset is generally something like Assets/Prefabs/<obj name>.prefab.
AssetHelper does substring matching so you only need to provide the <obj name>.

The type of the asset is a subtype of UnityEngine.Object; often it will be GameObject.

For information about dependencies, see <xref:dependencies>.

### Important information

The primary asset bundle, as well as all of its dependencies, will be kept in memory
as long as the LoadedAsset is alive. This is because, even if you clone the asset using
UnityEngine.Object.Instantiate, the bundles contain important things like sprites that are
shared between the prefab and all instances of the asset. When you are done with the asset,
call @"Silksong.AssetHelper.LoadedAssets.LoadedAsset`1.Dispose" to clear the dependencies
from memory.

The asset provided by @"Silksong.AssetHelper.LoadedAssets.LoadedAsset`1.Asset" is just a
prefab and cannot be used directly. You will have to call UnityEngine.Object.Instantiate
on it and spawn the instantiated copy.

Loading assets in this way cannot happen until Addressables is loaded, which happens
the next frame after Plugins.Start is called. For convenience the
@"Silksong.AssetHelper.AssetsData.InvokeAfterAddressablesLoaded" function is provided,
which will execute an action immediately after Addressables is loaded (or immediately
if Addressables is already loaded).

## Gameplay Assets

The game tries to unload all asset bundles when returning to menu, which means that any asset bundles
which are kept alive by a LoadedAsset may lose references if their dependencies are not also kept
alive.

For this reason, we provide the @"Silksong.AssetHelper.LoadedAssets.GameplayAsset`1"
convenience class, which represents an asset which is available whenever the player is in-game
and unloaded when the player is in the main menu.

It is safe to create a GameplayAsset instance at any point in the game's lifecycle;
typically it will be created during the plugin's Awake method and never disposed.
The GameplayAsset will automatically handle loading and unloading the asset bundles when needed.

For more complex control (such as only loading the asset if the player enters a save file with certain
mods active), it is recommended to create a @"Silksong.AssetHelper.LoadedAssets.LoadedAsset`1"
instance when needed instead.
