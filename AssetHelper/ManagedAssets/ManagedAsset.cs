using System.Collections.Generic;
using Silksong.AssetHelper.Plugin;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Silksong.AssetHelper.ManagedAssets;

/// <summary>
/// Class which is a wrapper around an Addressables key, which can be freely loaded and unloaded.
///
/// This class can be instantiated at any time, but cannot be loaded until after the catalogs have been built.
/// If loading before reaching the main menu, it should be done in a callback to <see cref="AssetRequestAPI.InvokeAfterBundleCreation"/>.
/// </summary>
/// <typeparam name="T">The type of the asset to load.</typeparam>
/// <param name="key">The Addressables Key used to load the asset.</param>
public class ManagedAsset<T>(string key) : ManagedAssetBase<T>
{
    /// <summary>
    /// The Addressables Key used to load the asset.
    /// </summary>
    public string Key { get; } = key;

    /// <inheritdoc />
    protected internal override string Identifier => Key;

    /// <summary>
    /// Construct an instance for the given scene asset.
    ///
    /// Doing this during your plugin's Awake method will cause it to be requested automatically.
    /// via the <see cref="AssetRequestAPI"/> API.
    /// </summary>
    /// <param name="sceneName">The name of the scene.</param>
    /// <param name="objPath">The hierarchical path to the game object.</param>
    /// <returns></returns>
    public static ManagedAsset<T> FromSceneAsset(string sceneName, string objPath)
    {
        if (typeof(T) != typeof(GameObject))
        {
            AssetHelperPlugin.InstanceLogger.LogWarning(
                $"{nameof(ManagedAsset<>)} instances for scene assets should have GameObject as the type argument!"
            );
        }

        if (AssetRequestAPI.RequestApiAvailable)
        {
            AssetRequestAPI.RequestSceneAsset(sceneName, objPath);
        }
        else
        {
            if (!AssetRequestAPI.Request.SceneAssets.TryGetValue(sceneName.ToLowerInvariant(), out HashSet<string> objNames)
                || !objNames.Contains(objPath))
            {
                AssetHelperPlugin.InstanceLogger.LogWarning(
                    $"Constructing managed asset from scene {sceneName}, {objPath} after Awake may not work unless the asset has been requested first!");
            }
        }

        string key = CatalogKeys.GetKeyForSceneAsset(sceneName, objPath);
        return new(key);
    }

    /// <summary>
    /// Construct an instance for the given non-scene asset.
    ///
    /// Doing this during your plugin's Awake method will cause it to be requested automatically.
    /// via the <see cref="AssetRequestAPI"/> API, provided the
    /// <paramref name="bundleName"/> argument is supplied.
    /// </summary>
    /// <param name="assetName">The name of the asset in its bundle.</param>
    /// <param name="bundleName">The name of the bundle containing the asset.</param>
    /// <returns></returns>
    public static ManagedAsset<T> FromNonSceneAsset(string assetName, string? bundleName = null)
    {
        if (AssetRequestAPI.RequestApiAvailable && !string.IsNullOrEmpty(bundleName))
        {
            AssetRequestAPI.RequestNonSceneAsset<T>(bundleName, assetName);
        }
        // TODO - include log message if this is constructed too late and the key isn't in the request;
        // this requires more complex normalization so I am skipping for now

        string key = CatalogKeys.GetKeyForNonSceneAsset(assetName);
        return new(key);
    }

    /// <inheritdoc />
    protected override AsyncOperationHandle<T> DoLoad()
        => Addressables.LoadAssetAsync<T>(Key);

    /// <summary>
    /// Create a new <see cref="ManagedAsset{T}" /> instance that wraps the same underlying asset.
    /// The new instance starts out unloaded.
    /// </summary>
    public ManagedAsset<T> Clone()
    {
        return new(Key);
    }
}
