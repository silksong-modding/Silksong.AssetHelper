using System;
using UnityEngine.AddressableAssets;
using UObject = UnityEngine.Object;
using AssetBundleLoadHandle = UnityEngine.ResourceManagement.AsyncOperations
    .AsyncOperationHandle<
        System.Collections.Generic.IList<
            UnityEngine.ResourceManagement.ResourceProviders.IAssetBundleResource
            >
        >;
using UnityEngine;

namespace Silksong.AssetHelper;

/// <summary>
/// Object representing a Unity object loaded from an asset bundle via <see cref="Addressables"/>.
/// </summary>
/// <typeparam name="T">The type of the <see cref="UObject"/> managed by this instance.</typeparam>
public class LoadedAsset<T> : IDisposable
    where T : UObject
{
    internal LoadedAsset(T asset, AssetBundle bundle, AssetBundleLoadHandle bundleHandle)
    {
        _asset = asset;
        _bundle = bundle;
        _bundleHandle = bundleHandle;
    }

    /// <summary>
    /// The asset managed by this instance.
    /// </summary>
    public T Asset => _asset;

    /// <summary>
    /// The bundle from which <see cref="Asset"/> was loaded.
    /// </summary>
    public AssetBundle Bundle => _bundle;

    private readonly T _asset;
    private readonly AssetBundle _bundle;
    private readonly AssetBundleLoadHandle _bundleHandle;

    #region Dispose boilerplate
    private bool _disposed = false;

    /// <summary>
    /// Virtual dispose method following the standard Dispose pattern.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        Addressables.Release(_bundleHandle);

        _disposed = true;
    }

    ~LoadedAsset() => Dispose(false);
    #endregion

    /// <summary>
    /// Release the Asset Bundles used to load this asset.
    /// 
    /// This method should be called when the underlying asset is no longer needed. 
    /// Note that this includes clones of the underlying asset via <see cref="UObject.Instantiate"/>;
    /// disposing this instance will cause any clones of the underlying asset to break.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Dispose(true);
    }
}
