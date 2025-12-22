// Todo - provide constructors which abstract away the process of getting
// the bundle names <-> bundle keys and dependency list.

using BepInEx.Logging;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Silksong.AssetHelper.LoadedAssets;

/// <summary>
/// Class representing an asset loadable from an asset bundle with Addressables.
/// 
/// The asset will be automatically unloaded when quitting to menu.
/// </summary>
/// <typeparam name="T">The type of the asset.</typeparam>
public class LoadableAsset<T> where T : UObject
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource(nameof(LoadableAsset<UObject>));

    private AssetBundleGroup _bundleGroup;
    private string _assetName;

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
    public LoadableAsset(string assetName, AssetBundleGroup bundleGroup)
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
    public LoadableAsset(string assetName, string mainBundle) : this(assetName, AssetBundleGroup.CreateWithDependencies(mainBundle)) { }

    /// <summary>
    /// Whether the underlying bundles have been loaded.
    /// </summary>
    public bool Loaded => _bundleGroup.Loaded;

    /// <summary>
    /// Event invoked when this asset is loaded.
    /// </summary>
    public event Action<LoadableAsset<T>>? OnLoaded;

    private readonly List<Action<LoadableAsset<T>>> _toInvokeWhenLoaded = [];
    
    /// <summary>
    /// Execute the supplied action when this asset is loaded.
    /// 
    /// If it is already loaded, execute the action immediately.
    /// </summary>
    public void ExecuteWhenLoaded(Action<LoadableAsset<T>> toInvoke)
    {
        if (Loaded)
        {
            ActionUtil.SafeInvoke(toInvoke, this);
            return;
        }
        _toInvokeWhenLoaded.Add(toInvoke);
    }

    private void OnLoadedCallback(Action<LoadableAsset<T>>? callback = null)
    {
        if (OnLoaded != null)
        {
            foreach (Action<LoadableAsset<T>> toInvoke in OnLoaded.GetInvocationList())
            {
                ActionUtil.SafeInvoke(toInvoke, this);
            }
        }

        ActionUtil.SafeInvoke(callback, this);

        foreach (Action<LoadableAsset<T>> toInvoke in _toInvokeWhenLoaded)
        {
            ActionUtil.SafeInvoke(toInvoke, this);
        }
        _toInvokeWhenLoaded.Clear();
    }

    internal void DoLoad() => Load();

    /// <summary>
    /// Load the underlying bundles.
    /// </summary>
    public void Load(Action<LoadableAsset<T>>? callback = null)
    {
        if (Loaded)
        {
            ActionUtil.SafeInvoke(callback, this);
            return;
        }

        _bundleGroup.Load(_ => OnLoadedCallback(callback));
    }

    /// <summary>
    /// Coroutine to load the underlying bundles.
    /// </summary>
    public IEnumerator LoadAsync(Action<LoadableAsset<T>>? callback = null)
    {
        if (Loaded)
        {
            ActionUtil.SafeInvoke(callback, this);
            yield break;
        }

        yield return _bundleGroup.LoadAsync(_ => OnLoadedCallback(callback));
    }

    /// <summary>
    /// Load the underlying bundles, and block the main thread until they are loaded.
    /// </summary>
    public void LoadImmediate()
    {
        _bundleGroup.LoadImmediate();
        OnLoadedCallback(null);
    }

    /// <summary>
    /// Unload the underlying bundles.
    /// </summary>
    public void Unload()
    {
        _asset = null;
        _bundleGroup.Unload();
    }
}
