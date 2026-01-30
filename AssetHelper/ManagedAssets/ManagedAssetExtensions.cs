using System;

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
    /// This method does not check if there was an exception when loading the asset.
    /// </remarks>
    public static void EnsureLoaded<T>(this ManagedAsset<T> asset)
    {
        if (!asset.HasBeenLoaded)
        {
            AssetHelperPlugin.InstanceLogger.LogWarning(
                $"{nameof(EnsureLoaded)} has been called on {asset.Key} before loading the asset!");
            asset.Load();
        }
        if (!asset.IsLoaded)
        {
            asset.Handle.WaitForCompletion();
        }
    }

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
}
