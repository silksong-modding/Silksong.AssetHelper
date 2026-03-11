using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Silksong.AssetHelper.ManagedAssets;

/// <summary>
/// Base class for objects that wrap an Addressables key and load the underlying asset.
/// </summary>
/// <typeparam name="THandleResult">The type parameter of the AsyncOperationHandle loaded by Addressables.</typeparam>
public abstract class ManagedAssetBase<THandleResult> : IManagedAsset
{
    /// <summary>
    /// An identifier associated with this asset.
    /// </summary>
    protected internal abstract string Identifier { get; }

    private AsyncOperationHandle<THandleResult>? _handle;

    /// <summary>
    /// The operation handle containing the asset. This will be null if the asset has not been loaded.
    ///
    /// This handle should not be unloaded manually; instead, the <see cref="Unload"/> method
    /// on this instance should be used.
    /// </summary>
    /// <exception cref="InvalidOperationException">Exception thrown if this instance has not been loaded when accessing the handle.</exception>
    public AsyncOperationHandle<THandleResult> Handle =>
        _handle.HasValue
            ? _handle.Value
            : throw new InvalidOperationException(
                $"Addressable asset with identifier {Identifier} must be loaded before accessing the handle!"
            );

    /// <summary>
    /// Load the underlying asset. This operation is idempotent.
    ///
    /// This should be called prior to using the asset.
    /// </summary>
    /// <returns>The handle used to load the asset.</returns>
    public AsyncOperationHandle<THandleResult> Load()
    {
        if (_handle == null)
        {
            _handle = DoLoad();
        }
        return Handle;
    }

    /// <summary>
    /// Function to load the underlying asset.
    /// </summary>
    protected abstract AsyncOperationHandle<THandleResult> DoLoad();

    object? IManagedAsset.Load() => Load();

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
