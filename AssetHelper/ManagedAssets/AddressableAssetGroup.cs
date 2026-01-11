using Silksong.AssetHelper.Plugin;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Silksong.AssetHelper.ManagedAssets;

/// <summary>
/// Class representing a collection of Addressable assets of the same type that are
/// loaded together.
/// 
/// This is more efficient than loading them separately.
/// 
/// It is important to check the OperationException of the <see cref="Handle"/> on this object
/// because indexing might fail if one or more of the assets failed to load. 
/// </summary>
public class AddressableAssetGroup<T>
{
    private List<string> _keys;
    private Dictionary<string, int> _indexLookup;

    /// <summary>
    /// Construct an Addressable asset group from a mapping {name -> key}.
    /// </summary>
    /// <param name="keyLookup">A mapping name -> key.
    /// The name should be a string used to access the individual asset; names should be unique but
    /// their values do not matter.
    /// The key should be an Addressables key.</param>
    public AddressableAssetGroup(Dictionary<string, string> keyLookup)
    {
        _indexLookup = [];
        _keys = [];
        int count = 0;
        foreach ((string name, string key) in keyLookup)
        {
            _indexLookup[name] = count;
            _keys.Add(key);
            count++;
        }
    }

    /// <summary>
    /// Request keys for the given scene and/or non-scene assets, and create an <see cref="AddressableAssetGroup{T}"></see>
    /// managing them.
    /// </summary>
    /// <param name="sceneAssets">A mapping (key) -> </param>
    /// <param name="nonSceneAssets"></param>
    /// <exception cref="InvalidOperationException">Exception thrown if the request is made after plugins have finished Awake-ing.</exception>
    public static AddressableAssetGroup<T> RequestAndCreate(
        List<(string name, string sceneName, string objPath)>? sceneAssets = null,
        List<(string name, string bundleName, string assetName)>? nonSceneAssets = null)
    {
        if (!AssetRequestAPI.RequestApiAvailable)
        {
            throw new InvalidOperationException("Asset requests should be made during or before a plugin's Awake method!");
        }

        Dictionary<string, string> keyLookup = [];

        if (sceneAssets != null)
        {
            if (typeof(T) != typeof(GameObject))
            {
                AssetHelperPlugin.InstanceLogger.LogWarning($"{nameof(AddressableAssetGroup<>)} instances for scene assets should have GameObject as the type argument!");
            }

            foreach ((string name, string sceneName, string objPath) in sceneAssets)
            {
                AssetRequestAPI.RequestSceneAsset(sceneName, objPath);
                keyLookup.Add(name, CatalogKeys.GetKeyForSceneAsset(sceneName, objPath));
            }
        }

        if (nonSceneAssets != null)
        {
            foreach ((string name, string bundleName, string assetName) in nonSceneAssets)
            {
                AssetRequestAPI.RequestNonSceneAsset<T>(bundleName, assetName);
                keyLookup.Add(name, CatalogKeys.GetKeyForNonSceneAsset(assetName));
            }
        }

        return new(keyLookup);
    }

    private AsyncOperationHandle<IList<T>>? _handle;

    /// <summary>
    /// The operation handle containing the asset. This will be null if the asset has not been loaded.
    /// 
    /// This handle should not be unloaded manually; instead, the <see cref="Unload"/> method
    /// on this instance should be used.
    /// </summary>
    /// <exception cref="InvalidOperationException">Exception thrown if this instance has not been loaded when accessing the handle.</exception>
    public AsyncOperationHandle<IList<T>> Handle => _handle.HasValue
        ? _handle.Value
        : throw new InvalidOperationException($"Addressable asset group must be loaded before accessing the handle!");


    /// <summary>
    /// Load the underlying asset. This operation is idempotent.
    /// 
    /// This should be called prior to using the asset.
    /// </summary>
    /// <returns>The handle used to load the asset.</returns>
    public AsyncOperationHandle<IList<T>> Load()
    {
        if (_handle == null)
        {
            _handle = Addressables.LoadAssetsAsync<T>(_keys, null, Addressables.MergeMode.Union);
        }
        return Handle;
    }

    /// <summary>
    /// Access a loaded asset by name.
    /// </summary>
    /// <param name="name">The name as provided when creating this instance.</param>
    public T this[string name]
    {
        get
        {
            return Handle.Result[_indexLookup[name]];
        }
    }

    /// <summary>
    /// Unload the underlying asset. This operation is idempotent.
    /// 
    /// This should not be called if the asset is still in use.
    /// </summary>
    public void Unload()
    {
        if (_handle.HasValue)
        {
            Addressables.Release(_handle.Value);
            _handle = null;
        }
    }

    /// <summary>
    /// Whether or not the asset has finished loading.
    /// </summary>
    public bool IsLoaded => HasBeenLoaded && Handle.IsDone;

    /// <summary>
    /// Whether or not the asset load request has been made.
    /// </summary>
    public bool HasBeenLoaded => _handle.HasValue;
}
