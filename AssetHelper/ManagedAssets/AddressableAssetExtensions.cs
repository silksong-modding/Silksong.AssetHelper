using System;

namespace Silksong.AssetHelper.ManagedAssets;

/// <summary>
/// Extensions for working with instances of <see cref="AddressableAsset{T}"/>.
/// </summary>
public static class AddressableAssetExtensions
{
    /// <summary>
    /// Instantiate the asset managed by this instance.
    /// </summary>
    public static T InstantiateAsset<T>(this AddressableAsset<T> asset) where T : UObject
    {
        if (!asset.IsLoaded)
        {
            throw new InvalidOperationException($"The asset has not finished loading!");
        }

        return UObject.Instantiate(asset.Handle.Result);
    }
}
