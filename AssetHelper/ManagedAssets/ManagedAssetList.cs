using Silksong.AssetHelper.Plugin;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Silksong.AssetHelper.ManagedAssets;

/// <summary>
/// Class that wraps the load of a group of addressable assets with the same primary key.
/// 
/// This is commonly used when multiple game objects in a (repacked) scene have the same name.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="key"></param>
public class ManagedAssetList<T>(string key) : ManagedAssetBase<IList<T>>
{
    /// <summary>
    /// The Addressables Key used to load the asset.
    /// </summary>
    public string Key { get; } = key;

    /// <inheritdoc />
    protected internal override string Identifier => Key;


    /// <inheritdoc />
    protected override AsyncOperationHandle<IList<T>> DoLoad()
        => Addressables.LoadAssetsAsync<T>(Key);

    /// <summary>
    /// Create a new <see cref="ManagedAssetList{T}" /> instance that wraps the same underlying asset.
    /// The new instance starts out unloaded.
    /// </summary>
    public ManagedAssetList<T> Clone()
    {
        return new(Key);
    }

    /// <summary>
    /// Construct an instance for the given scene asset.
    ///
    /// Doing this during your plugin's Awake method will cause it to be requested automatically.
    /// via the <see cref="AssetRequestAPI"/> API.
    /// </summary>
    /// <param name="sceneName">The name of the scene.</param>
    /// <param name="objPath">The hierarchical path to the game object.</param>
    /// <returns></returns>
    public static ManagedAssetList<T> FromSceneAsset(string sceneName, string objPath)
    {
        if (typeof(T) != typeof(GameObject))
        {
            AssetHelperPlugin.InstanceLogger.LogWarning(
                $"{nameof(ManagedAssetList<>)} instances for scene assets should have GameObject as the type argument!"
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
                    $"Constructing managed asset list from scene {sceneName}, {objPath} after Awake may not work unless the asset has been requested first!");
            }
        }

        string key = CatalogKeys.GetKeyForSceneAsset(sceneName, objPath);
        return new(key);
    }
}
