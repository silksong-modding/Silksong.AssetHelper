# AssetHelperMenu

Plugin adding a mod menu which exposes developer tools associated with
[AssetHelper](https://github.com/silksong-modding/Silksong.AssetHelper).

All of the tools here are available in the DebugTools.Dev namespace in AssetHelper,
and more detailed documentation can be found in the
[AssetHelper docs](https://docs.silksong-modding.org/Silksong.AssetHelper/api/Silksong.AssetHelper.Dev.DebugTools.html).

Additional notes:
* Any of the functions here can freeze the game as they do computation and processing.
* The current scene hierarchy dump gives the hierarchy for game objects in the current active scene.
Paths in here are paths in the bundle and are the paths
needed to request assets with AssetHelper; these may not match the paths in the scene
if changes have been made at runtime.

