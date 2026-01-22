using System;
using System.Collections.Generic;
using System.Linq;
using Silksong.AssetHelper.Plugin;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using SysTask = System.Threading.Tasks.Task;

namespace Silksong.AssetHelper.ManagedAssets;

/// <summary>
/// Class representing a collection of Addressable assets of the same type that are
/// loaded together.
/// </summary>
public class ManagedAssetGroup<T> : IManagedAsset
{
    /// <summary>
    /// Record representing a scene asset.
    /// </summary>
    /// <param name="SceneName">The name of the scene.</param>
    /// <param name="ObjPath">The hierarchical path to the object.</param>
    public record SceneAssetInfo(string SceneName, string ObjPath);

    /// <summary>
    /// Record representing a non-scene asset.
    /// </summary>
    /// <param name="BundleName">The name of the bundle containing the asset.</param>
    /// <param name="AssetName">The name of the asset within the bundle.</param>
    public record NonSceneAssetInfo(string BundleName, string AssetName);

    private Dictionary<string, string> _keyLookup;

    /// <summary>
    /// Construct an Addressable asset group from a mapping {name -> key}.
    /// </summary>
    /// <param name="keyLookup">A mapping name -> key.
    /// The name should be a string used to access the individual asset; names should be unique but
    /// their values do not matter.
    /// The key should be an Addressables key.</param>
    public ManagedAssetGroup(Dictionary<string, string> keyLookup)
    {
        _keyLookup = keyLookup;
    }

    /// <summary>
    /// Request keys for the given scene and/or non-scene assets, and create an <see cref="ManagedAssetGroup{T}"></see>
    /// managing them.
    /// </summary>
    /// <param name="sceneAssets">A mapping (key) -> </param>
    /// <param name="nonSceneAssets"></param>
    /// <exception cref="InvalidOperationException">Exception thrown if the request is made after plugins have finished Awake-ing.</exception>
    public static ManagedAssetGroup<T> RequestAndCreate(
        Dictionary<string, SceneAssetInfo>? sceneAssets = null,
        Dictionary<string, NonSceneAssetInfo>? nonSceneAssets = null
    )
    {
        if (!AssetRequestAPI.RequestApiAvailable)
        {
            throw new InvalidOperationException(
                "Asset requests should be made during or before a plugin's Awake method!"
            );
        }

        Dictionary<string, string> keyLookup = [];

        if (sceneAssets != null)
        {
            if (typeof(T) != typeof(GameObject))
            {
                AssetHelperPlugin.InstanceLogger.LogWarning(
                    $"{nameof(ManagedAssetGroup<>)} instances for scene assets should have GameObject as the type argument!"
                );
            }

            foreach ((string name, SceneAssetInfo asset) in sceneAssets)
            {
                AssetRequestAPI.RequestSceneAsset(asset.SceneName, asset.ObjPath);
                keyLookup.Add(
                    name,
                    CatalogKeys.GetKeyForSceneAsset(asset.SceneName, asset.ObjPath)
                );
            }
        }

        if (nonSceneAssets != null)
        {
            foreach ((string name, NonSceneAssetInfo asset) in nonSceneAssets)
            {
                AssetRequestAPI.RequestNonSceneAsset<T>(asset.BundleName, asset.AssetName);
                keyLookup.Add(name, CatalogKeys.GetKeyForNonSceneAsset(asset.AssetName));
            }
        }

        return new(keyLookup);
    }

    private Dictionary<string, AsyncOperationHandle<T>>? _handles;

    /// <summary>
    /// Get a <see cref="CustomYieldInstruction"/> that can be used to wait for the assets to finish loading.
    ///
    /// Calling `yield return group.GetYieldInstruction()` in an IEnumerator will cause Unity to pause the coroutine
    /// until all assets are loaded.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">If this function is called before calling <see cref="Load"/>.</exception>
    public CustomYieldInstruction GetYieldInstruction()
    {
        if (_handles == null)
        {
            throw new InvalidOperationException(
                $"This {nameof(ManagedAssetGroup<>)} must be loaded before awaiting!"
            );
        }

        return new WaitUntil(() => this.IsLoaded);
    }

    /// <summary>
    /// Load the underlying asset. This operation is idempotent.
    ///
    /// This should be called prior to using the asset.
    /// </summary>
    /// <returns>The output of <see cref="GetYieldInstruction"/>.</returns>
    public CustomYieldInstruction Load()
    {
        if (_handles != null)
        {
            return GetYieldInstruction();
        }

        _handles = [];
        foreach ((string name, string key) in _keyLookup)
        {
            _handles[name] = Addressables.LoadAssetAsync<T>(key);
        }

        return GetYieldInstruction();
    }

    object? IManagedAsset.Load() => Load();

    /// <summary>
    /// Get an awaitable for the load operation to be used in an <see langword="async"/>/<see langword="await"/> context.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">If this function is called before calling <see cref="Load"/>.</exception>
    public SysTask GetTask()
    {
        if (_handles == null)
        {
            throw new InvalidOperationException(
                $"This {nameof(ManagedAssetGroup<>)} must be loaded before awaiting!"
            );
        }

        return SysTask.WhenAll(_handles.Values.Select(x => x.Task));
    }

    /// <summary>
    /// Access a loaded asset by name.
    /// </summary>
    /// <param name="name">The name as provided when creating this instance.</param>
    public AsyncOperationHandle<T> this[string name]
    {
        get
        {
            if (_handles == null)
            {
                throw new InvalidOperationException(
                    "Handles can not be accessed until this instance has started loading"
                );
            }

            return _handles![name];
        }
    }

    /// <summary>
    /// Unload the underlying assets. This operation is idempotent.
    ///
    /// This should not be called if the asset is still in use.
    /// </summary>
    public void Unload()
    {
        if (_handles != null)
        {
            foreach (AsyncOperationHandle<T> handle in _handles.Values)
            {
                Addressables.Release(handle);
            }

            _handles = null;
        }
    }

    /// <summary>
    /// Whether or not the assets have finished loading.
    /// </summary>
    public bool IsLoaded => HasBeenLoaded && _handles!.Values.All(x => x.IsDone);

    /// <summary>
    /// Whether or not the asset load request has been made.
    /// </summary>
    public bool HasBeenLoaded => _handles != null;
}
