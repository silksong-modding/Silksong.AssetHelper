using System;
using System.Collections;

namespace Silksong.AssetHelper.LoadedAssets;

/// <summary>
/// Common interface representing objects that can be loaded by AssetHelper.
/// </summary>
/// <typeparam name="T">The type of the asset managed by this instance.</typeparam>
public interface ILoadableAsset<T> where T : UObject
{
    /// <summary>
    /// The asset managed by this instance.
    /// </summary>
    T? Asset { get; }

    /// <summary>
    /// If the asset is loaded. If the asset is not loaded, it should be loaded
    /// before doing anything with it using <see cref="Load"/> or a related method.
    /// </summary>
    bool Loaded { get; }

    /// <summary>
    /// Execute the supplied action as soon as the asset is loaded.
    /// If the asset is already loaded, execute it immediately.
    /// </summary>
    void ExecuteWhenLoaded(Action<ILoadableAsset<T>> toInvoke);

    /// <summary>
    /// Load the asset. This function will do nothing if the asset is already loaded.
    /// The asset may or may not be loaded after this function completes,
    /// so anything that requires the asset to be loaded should be executed
    /// by <see cref="ExecuteWhenLoaded(Action{ILoadableAsset{T}})"/>.
    /// </summary>
    void Load();

    /// <summary>
    /// Coroutine to load the asset.
    /// </summary>
    IEnumerator LoadAsync();

    /// <summary>
    /// Load the asset, blocking the main thread until it is loaded.
    /// 
    /// After this function completes, the asset will be loaded.
    /// </summary>
    void LoadImmediate();

    /// <summary>
    /// Unload the asset.
    /// </summary>
    void Unload();
}
