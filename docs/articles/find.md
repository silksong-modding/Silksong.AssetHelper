# Finding assets to load

There are several ways to find an asset you want to load.

## Browse base game classes for references

Many assets that are needed often can be found referenced by base game classes. For instance:
* Shiny items, rosaries, shell shards etc can be found on the GlobalSettings.Gameplay class
* Collectable items can be found in the CollectableItemManager, and there are
similar manager classes for many other item types

If this applies to your use case, you may not need to use AssetHelper.

## Browse the asset list

You can use the @"Silksong.AssetHelper.BundleTools.DebugTools.DumpAllAssetNames" function
from within your plugin to create a list of all asset names in non-scene bundles in the AssetHelper directory.
This gives a list of all non-scene assets that can be loaded via AssetHelper.

You may be able to find the asset you're looking for in this file.

## Dump asset lists per scene

You can use the @"Silksong.AssetHelper.BundleTools.DebugTools.DumpGameObjectPaths(System.String,System.Boolean)"
function to create a list of all gameObject names in a particular scene in the AssetHelper directory.
This is a good way to find the game object path that should be passed to AssetHelper.

## Use external tools

Two of the most useful tools are UnityExplorer and UABEANext.

UnityExplorer lets you select a gameObject in the scene and find out its name.
In this case, you can usually pass the path to this gameObject directly to AssetHelper,
although you may wish to verify that the path hasn't changed (some objects change their parent,
in which case the path in UnityExplorer may not match the actual path needed by AssetHelper).

[UABEANext](https://github.com/nesrak1/UABEANext) lets you browse an asset bundle without needing the game to be open. You can discover
the path to a game object by clicking the hierarchy tab in the bottom left, then select a
game object in the middle panel and click View Scene.
