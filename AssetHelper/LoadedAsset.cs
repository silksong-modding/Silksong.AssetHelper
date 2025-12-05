using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UObject = UnityEngine.Object;

namespace Silksong.AssetHelper;

/// <summary>
/// Object representing a Unity object loaded from an asset bundle via <see cref="Addressables"/>.
/// </summary>
/// <typeparam name="T">The type of the <see cref="UObject"/> managed by this instance.</typeparam>
public class LoadedAsset<T> : IDisposable
    where T : UObject
{
    internal LoadedAsset(T asset, IList<AssetBundle> bundles)
    {
        _asset = asset;
        _loadedBundles = [.. bundles];
    }

    /// <summary>
    /// The asset managed by this instance.
    /// </summary>
    public T Asset => _asset;

    private readonly T _asset;
    private readonly AssetBundle[] _loadedBundles;

    /// <summary>
    /// Release the Asset Bundles used to load this asset.
    /// 
    /// This method should be called when the underlying asset is no longer needed.
    /// </summary>
    public void Dispose()
    {
        foreach (AssetBundle bundle in _loadedBundles)
        {
            Addressables.Release(bundle);
        }
        GC.SuppressFinalize(this);
    }
}
