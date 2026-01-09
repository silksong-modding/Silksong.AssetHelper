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
/// <param name="Key">The Addressables Key used to load the asset.</param>
public class AddressableAsset<T>(string Key)
{
    /// <summary>
    /// The operation handle containing the asset. This will be null if the asset has not been loaded.
    /// 
    /// This handle should not be unloaded manually; instead, the <see cref="Unload"/> method
    /// on this instance should be used.
    /// </summary>
    public AsyncOperationHandle<T>? Handle { get; private set; }

    /// <summary>
    /// Construct an instance for the given scene asset.
    /// 
    /// Doing this during your plugin's Awake method will cause it to be requested automatically
    /// via the <see cref="AssetRequestAPI"/> API.
    /// </summary>
    /// <param name="sceneName">The name of the scene.</param>
    /// <param name="objPath">The hierarchical path to the game object.</param>
    /// <returns></returns>
    public static AddressableAsset<T> FromSceneAsset(string sceneName, string objPath)
    {
        if (typeof(T) != typeof(GameObject))
        {
            AssetHelperPlugin.InstanceLogger.LogWarning($"{nameof(AddressableAsset<>)} instances for scene assets should have GameObject as the type argument!");
        }

        if (AssetRequestAPI.RequestApiAvailable)
        {
            AssetRequestAPI.RequestSceneAsset(sceneName, objPath);
        }

        string key = CatalogKeys.GetKeyForSceneAsset(sceneName, objPath);
        return new(key);
    }

    /// <summary>
    /// Construct an instance for the given non-scene asset.
    ///     
    /// Doing this during your plugin's Awake method will cause it to be requested automatically
    /// via the <see cref="AssetRequestAPI"/> API, provided the
    /// <paramref name="bundleName"/> argument is supplied.
    /// </summary>
    /// <param name="assetName">The name of the asset in its bundle.</param>
    /// <param name="bundleName">The name of the bundle containing the asset.</param>
    /// <returns></returns>
    public static AddressableAsset<T> FromNonSceneAsset(string assetName, string? bundleName = null)
    {
        if (AssetRequestAPI.RequestApiAvailable && !string.IsNullOrEmpty(bundleName))
        {
            AssetRequestAPI.RequestNonSceneAsset<T>(bundleName, assetName);
        }

        string key = CatalogKeys.GetKeyForNonSceneAsset(assetName);
        return new(key);
    }


    /// <summary>
    /// Load the underlying asset. This operation is idempotent.
    /// 
    /// This should be called prior to using the asset.
    /// </summary>
    public void Load()
    {
        if (Handle == null)
        {
            Handle = Addressables.LoadAssetAsync<T>(Key);
        }
    }

    /// <summary>
    /// Unload the underlying asset. This operation is idempotent.
    /// 
    /// This should not be called if the asset is still in use.
    /// </summary>
    public void Unload()
    {
        if (Handle.HasValue)
        {
            Addressables.Release(Handle.Value);
            Handle = null;
        }
    }

    /// <summary>
    /// Whether or not the asset has finished loading.
    /// </summary>
    public bool IsLoaded => Handle.HasValue && Handle.Value.IsDone;

    /// <summary>
    /// Whether or not the asset load request has been made.
    /// </summary>
    public bool HasBeenLoaded => Handle.HasValue;
}
