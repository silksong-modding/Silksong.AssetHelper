# Finding assets to load

- Guide on how to find assets you might want to load
  - Search through the GlobalSettings.Gameplay class
    to see if you can get it the normal way
  - Use UnityExplorer
  - Paths may change at runtime so double check with UABE or
    DebugTools.DumpGameObjectPaths
  - Search through the output of DebugTools.DumpAllAssetNames
    to see if your asset is available as a prefab that does
	not need to be repacked
