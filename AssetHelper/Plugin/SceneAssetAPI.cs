using System;
using System.Collections.Generic;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// API entrypoints for repacked scene assets.
/// </summary>
public static class SceneAssetAPI
{
    internal static bool RequestApiAvailable { get; set; } = true;

    internal static Dictionary<string, HashSet<string>> SceneAssetRequest { get; } = [];

    /// <summary>
    /// Request the given asset paths in the given scene to be repacked.
    /// 
    /// This function must be called during a plugin's Awake method.
    /// </summary>
    /// <param name="sceneName">The name of the scene.</param>
    /// <param name="assetPaths">A list of asset paths to be repacked. They should be given in the hierarchy.</param>
    public static void RequestSceneAssets(string sceneName, IEnumerable<string> assetPaths)
    {
        if (!RequestApiAvailable)
        {
            throw new InvalidOperationException($"Scene asset requests must be made during a plugin's Awake method! Scene {sceneName}");
        }

        sceneName = sceneName.ToLowerInvariant();

        HashSet<string> updated = SceneAssetRequest.TryGetValue(sceneName, out HashSet<string> request) ? request : [];
        updated.UnionWith(assetPaths);
        SceneAssetRequest[sceneName] = updated;
    }

    /// <summary>
    /// Request that the given asset in the given scene be repacked.
    /// 
    /// This function must be called during a plugin's Awake method.
    /// </summary>
    /// <param name="sceneName">The name of the scene.</param>
    /// <param name="assetPath">An asset path to be repacked.</param>
    public static void RequestSceneAsset(string sceneName, string assetPath) => RequestSceneAssets(sceneName, [assetPath]);

    /// <summary>
    /// Request that the given assets in the given scenes be repacked.
    /// 
    /// This function must be called during a plugin's Awake method.
    /// </summary>
    /// <param name="assetData">A lookup (scene : list of paths) of game objects that should be repacked.</param>
    public static void RequestSceneAssets(Dictionary<string, List<string>> assetData)
    {
        foreach ((string sceneName, List<string> assetPaths) in assetData)
        {
            RequestSceneAssets(sceneName, assetPaths);
        }
    }

    // TODO - asset retrieval
}
