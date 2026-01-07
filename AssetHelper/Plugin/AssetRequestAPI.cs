using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// API entrypoints for repacked scene assets.
/// </summary>
public static class AssetRequestAPI
{
    internal static bool RequestApiAvailable { get; set; } = true;

    internal static DelayedAction AfterBundleCreationComplete = new();

    /// <summary>
    /// Invoke this action once AssetHelper has built the repacked scene bundles and loaded the new catalog.
    /// 
    /// If repacking has already been completed, the action will be invoked immediately.
    /// </summary>
    public static void InvokeAfterBundleCreation(Action a) => AfterBundleCreationComplete.Subscribe(a);

    #region Scene Assets
    internal static Dictionary<string, HashSet<string>> SceneAssetRequest { get; } = [];

    /// <summary>
    /// The <see cref="IResourceLocator"/> containing scene assets.
    /// </summary>
    public static IResourceLocator? SceneAssetLocator { get; internal set; }

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
    #endregion

    #region Non scene assets
    /// <summary>
    /// The <see cref="IResourceLocator"/> containing catalogued non-scene assets.
    /// </summary>
    public static IResourceLocator? NonSceneAssetLocator { get; internal set; }


    internal static bool FullNonSceneCatalogRequested { get; private set; }

    internal record NonSceneAssetInfo(string bundleName, string assetName, Type assetType);

    internal static List<NonSceneAssetInfo> RequestedNonSceneAssets { get; } = [];

    /// <summary>
    /// Request the full catalog of non-scene assets to be created.
    /// 
    /// Generating the full catalog is significantly slower than generating a catalog for specific assets,
    /// so using <see cref="RequestNonSceneAsset{T}(string, string)"/> is generally preferred.
    /// </summary>
    public static void RequestFullNonSceneCatalog() => FullNonSceneCatalogRequested = true;

    /// <summary>
    /// Request that the given asset is made available via Addressables.
    /// </summary>
    /// <typeparam name="T">The unity type of the asset.</typeparam>
    /// <param name="bundleName">The name of the bundle containing the asset.
    /// This is the path to the bundle, relative to the Standalone??? dir.</param>
    /// <param name="assetName">The name of the asset within the bundle container.</param>
    public static void RequestNonSceneAsset<T>(string bundleName, string assetName) where T : UObject 
        => RequestedNonSceneAssets.Add(new(bundleName, assetName, typeof(T)));

    /// <summary>
    /// Request that the given assets of the same type within the same bundle are made available via Addressables.
    /// </summary>
    public static void RequestNonSceneAssets<T>(string bundleName, IEnumerable<string> assetNames) where T: UObject
    {
        foreach (string assetName in assetNames)
        {
            RequestNonSceneAsset<T>(bundleName, assetName);
        }
    }
    #endregion

}
