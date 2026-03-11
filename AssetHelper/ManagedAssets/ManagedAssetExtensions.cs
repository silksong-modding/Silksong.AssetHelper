using System;
using System.ComponentModel;

namespace Silksong.AssetHelper.ManagedAssets;

/// <summary>
/// Extensions for working with instances of <see cref="ManagedAsset{T}"/>.
/// </summary>
public static class ManagedAssetExtensions
{
    /// <summary>
    /// Instantiate the asset managed by this instance.
    /// </summary>
    public static T InstantiateAsset<T>(this ManagedAsset<T> asset)
        where T : UObject
    {
        if (!asset.IsLoaded)
        {
            throw new InvalidOperationException($"The asset has not finished loading!");
        }

        return UObject.Instantiate(asset.Handle.Result);
    }

    /// <summary>
    /// Load the asset if it hasn't been loaded already, and block the main thread
    /// until it has finished loading.
    /// 
    /// This function is a no-op if the asset has already finished loading.
    /// </summary>
    /// <remarks>
    /// This method should not be used to load the asset. The expected use case is
    /// that you are loading the asset elsewhere (e.g. when the player enters a save file),
    /// and want to guard against a slim chance of the asset not having been loaded.
    /// 
    /// This method will write an error message to the log if there was an exception during loading,
    /// but this method will not throw.
    /// </remarks>
    public static void EnsureLoaded<T>(this ManagedAssetBase<T> asset)
    {
        if (!asset.HasBeenLoaded)
        {
            AssetHelperPlugin.InstanceLogger.LogWarning(
                $"{nameof(EnsureLoaded)} has been called on {asset.Identifier} before loading the asset!");
            asset.Load();
        }
        if (!asset.IsLoaded)
        {
            asset.Handle.WaitForCompletion();
        }

        if (asset.Handle.OperationException != null)
        {
            AssetHelperPlugin.InstanceLogger.LogError(
                $"Operation exception when loading asset {asset.Identifier}\n" + asset.Handle.OperationException);
        }
    }

    // Kept for backward compatibility
    /// <inheritdoc cref="EnsureLoaded{T}(ManagedAssetBase{T})" />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void EnsureLoaded<T>(this ManagedAsset<T> asset)
        => EnsureLoaded<T>((ManagedAssetBase<T>)asset);

    /// <summary>
    /// Instantiate an asset in this group accessed by key.
    /// </summary>
    public static T InstantiateAsset<T>(this ManagedAssetGroup<T> group, string key)
        where T : UObject
    {
        if (!group.IsLoaded)
        {
            throw new InvalidOperationException($"The group has not finished loading!");
        }

        return UObject.Instantiate(group[key].Result);
    }

    /// <summary>
    /// Instantiate an asset from a <see cref="ManagedAssetList{T}"/>.
    /// If multiple assets match the predicate then the first will be instantiated;
    /// which one this is will be arbitrary.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="asset"></param>
    /// <param name="predicate">Function used to check if a given asset is the one being looked for. Commonly
    /// this will inspect the children or components of the given asset.
    /// This function should not mutate the argument.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">If none of the assets match the predicate.</exception>
    public static T InstantiateAsset<T>(this ManagedAssetList<T> asset, Func<T, bool> predicate) where T : UObject
    {
        if (!asset.IsLoaded)
        {
            throw new InvalidOperationException($"The asset has not finished loading!");
        }

        foreach (T t in asset.Handle.Result)
        {
            if (predicate(t))
            {
                return UObject.Instantiate(t);
            }
        }

        throw new InvalidOperationException($"No matching asset for managed asset list with key {asset.Key} was found!");
    }
}
