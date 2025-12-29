using BepInEx.Logging;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Silksong.AssetHelper.LoadedAssets;

/// <summary>
/// Class representing an asset loadable from an asset bundle.
/// 
/// The asset is loaded by loading the bundles with Addressables, and then
/// loading the asset from the bundle the normal way.
/// 
/// The asset will be automatically unloaded when quitting to menu.
/// </summary>
/// <typeparam name="T">The type of the asset.</typeparam>
public class BundleAsset<T> : ILoadableAsset<T> where T : UObject
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource(nameof(BundleAsset<>));

    private readonly AssetBundleGroup _bundleGroup;
    private readonly string _assetName;

    // TODO - check that this works as-is across scene changes
    private T? _asset;

    /// <summary>
    /// The asset loaded by this object.
    /// </summary>
    public T? Asset
    {
        get
        {
            if (!Loaded) return null;

            if (_asset != null) return _asset;

            AssetBundle? bundle = _bundleGroup.MainBundle;
            if (bundle == null)
            {
                return null;
            }
            string objName = bundle.GetAllAssetNames()
                .FirstOrDefault(x => x.Contains(_assetName));
            if (objName == null)
            {
                Log.LogError($"Could not find name {_assetName} in bundle");
                Log.LogError("Available names:\n" + string.Join(", ", bundle.GetAllAssetNames().ToArray()));
            }

            Log.LogDebug($"Loading asset {objName} from bundle");

            T loaded = bundle.LoadAsset<T>(objName);

            _asset = loaded;
            return loaded;
        }
    }

    /// <summary>
    /// Instantiate a loadable asset.
    /// </summary>
    /// <param name="assetName">The name of the asset.</param>
    /// <param name="bundleGroup">The bundles associated with the asset.</param>
    public BundleAsset(string assetName, AssetBundleGroup bundleGroup)
    {
        _assetName = assetName;
        _bundleGroup = bundleGroup;

        GameEvents.OnQuitToMenu += Unload;
    }

    /// <summary>
    /// Instantiate a loadable asset. Dependent bundles will be automatically determined.
    /// </summary>
    /// <param name="assetName">The name of the asset.</param>
    /// <param name="mainBundle">The bundle containing the asset.</param>
    public BundleAsset(string assetName, string mainBundle) : this(assetName, AssetBundleGroup.CreateWithDependencies(mainBundle)) { }

    /// <summary>
    /// Whether the underlying bundles have been loaded.
    /// </summary>
    public bool Loaded => _bundleGroup.Loaded;

    /// <summary>
    /// Event invoked when this asset is loaded.
    /// 
    /// This event is only invoked if the asset is actually loaded; if the
    /// asset was already loaded, the event will not be raised.
    /// </summary>
    public event Action<BundleAsset<T>>? OnLoaded;

    private readonly List<Action<BundleAsset<T>>> _toInvokeWhenLoaded = [];
    
    /// <summary>
    /// Execute the supplied action when this asset is loaded.
    /// 
    /// If it is already loaded, execute the action immediately.
    /// </summary>
    public void ExecuteWhenLoaded(Action<BundleAsset<T>> toInvoke)
    {
        if (Loaded)
        {
            ActionUtil.SafeInvoke(toInvoke, this);
            return;
        }
        _toInvokeWhenLoaded.Add(toInvoke);
    }

    void ILoadableAsset<T>.ExecuteWhenLoaded(Action<ILoadableAsset<T>> toInvoke) => ExecuteWhenLoaded(toInvoke);

    private void OnLoadedCallback()
    {
        if (OnLoaded != null)
        {
            foreach (Action<BundleAsset<T>> toInvoke in OnLoaded.GetInvocationList())
            {
                ActionUtil.SafeInvoke(toInvoke, this);
            }
        }

        foreach (Action<BundleAsset<T>> toInvoke in _toInvokeWhenLoaded)
        {
            ActionUtil.SafeInvoke(toInvoke, this);
        }
        _toInvokeWhenLoaded.Clear();
    }

    /// <summary>
    /// Load the underlying bundles.
    /// </summary>
    public void Load()
    {
        if (Loaded)
        {
            return;
        }

        _bundleGroup.Load(_ => OnLoadedCallback());
    }

    /// <summary>
    /// Coroutine to load the underlying bundles.
    /// </summary>
    public IEnumerator LoadAsync()
    {
        if (Loaded)
        {
            yield break;
        }

        yield return _bundleGroup.LoadAsync(_ => OnLoadedCallback());
    }

    /// <summary>
    /// Load the underlying bundles, and block the main thread until they are loaded.
    /// </summary>
    public void LoadImmediate()
    {
        _bundleGroup.LoadImmediate();
        OnLoadedCallback();
    }

    /// <summary>
    /// Unload the underlying bundles.
    /// </summary>
    public void Unload()
    {
        if (_asset != null)
        {
            UObject.Destroy(_asset);
        }
        _asset = null;
        _bundleGroup.Unload();
    }
}
