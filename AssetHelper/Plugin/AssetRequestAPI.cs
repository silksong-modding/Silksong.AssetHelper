using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Silksong.AssetHelper.Internal;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// API entrypoints for repacked scene assets.
/// </summary>
public static class AssetRequestAPI
{
    internal static bool RequestApiAvailable { get; set; } = true;

    private static void VerifyRequest([CallerMemberName] string? caller = null)
    {
        if (!RequestApiAvailable)
        {
            throw new InvalidOperationException(
                $"Requests made through {caller} should be made during a plugin's Awake method!"
            );
        }
    }

    internal static DelayedAction AfterBundleCreationComplete = new();

    /// <summary>
    /// Invoke this action once AssetHelper has built the repacked scene bundles and loaded the new catalog.
    ///
    /// If repacking has already been completed, the action will be invoked immediately.
    ///
    /// This moment is typically too early to load any assets; this should mainly be used for debugging.
    /// </summary>
    public static void InvokeAfterBundleCreation(Action a) =>
        AfterBundleCreationComplete.Subscribe(a);

    internal static bool AnyRequestMade => (RequestedNonSceneAssets.Count > 0) || (SceneAssetRequest.Count > 0);

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
        VerifyRequest();

        sceneName = sceneName.ToLowerInvariant();

        HashSet<string> updated = SceneAssetRequest.TryGetValue(
            sceneName,
            out HashSet<string> request
        )
            ? request
            : [];
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
    public static void RequestSceneAsset(string sceneName, string assetPath) =>
        RequestSceneAssets(sceneName, [assetPath]);

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

    internal static Dictionary<
        (string bundleName, string assetName),
        Type
    > RequestedNonSceneAssets { get; } = [];

    /// <summary>
    /// Request that the given asset is made available via Addressables.
    /// </summary>
    /// <typeparam name="T">The unity type of the asset.</typeparam>
    /// <param name="bundleName">The name of the bundle containing the asset.
    /// This is the path to the bundle, relative to the Standalone??? dir.</param>
    /// <param name="assetName">The name of the asset within the bundle container.</param>
    public static void RequestNonSceneAsset<T>(string bundleName, string assetName) => RequestNonSceneAsset(bundleName, assetName, typeof(T));

    /// <inheritdoc cref="RequestNonSceneAsset{T}(string, string)" />
    public static void RequestNonSceneAsset(string bundleName, string assetName, Type assetType)
    {
        VerifyRequest();

        bundleName = bundleName.ToLowerInvariant();
        if (bundleName.EndsWith(".bundle"))
        {
            bundleName = bundleName[..^7];
        }

        if (RequestedNonSceneAssets.TryGetValue((bundleName, assetName), out Type t))
        {
            if (t != assetType)
            {
                AssetHelperPlugin.InstanceLogger.LogError(
                    $"Asset {bundleName} - {assetName} requested with both {t.Name} and {assetType.Name}"
                );
            }
        }

        // Always prefer the newer type, regardless of the error
        RequestedNonSceneAssets[(bundleName, assetName)] = assetType;
    }

    /// <summary>
    /// Request that the given assets of the same type within the same bundle are made available via Addressables.
    /// </summary>
    public static void RequestNonSceneAssets<T>(string bundleName, IEnumerable<string> assetNames)
        where T : UObject
    {
        foreach (string assetName in assetNames)
        {
            RequestNonSceneAsset<T>(bundleName, assetName);
        }
    }
    #endregion
}
