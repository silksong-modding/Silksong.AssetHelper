using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper.CatalogTools;

internal static class CatalogEntryUtils
{

    /// <summary>
    /// Creates a catalog entry for repacked scene bundles.
    /// 
    /// </summary>
    /// <param name="key">The primary key of the bundle.</param>
    /// <param name="bundlePath">The fully qualified path to the bundle on the filesystem</param>
    /// <param name="internalBundleName">The name of the AssetBundle asset in the bundle.</param>
    /// <param name="dependencyKeys">List of all the primary keys of the bundle dependencies.
    /// Note: All keys found in the dependencies must have their corresponding entry in the catalog.</param>
    public static ContentCatalogDataEntry CreateBundleEntry(string key, string bundlePath, string internalBundleName, List<string> dependencyKeys)
    {

        AssetBundleRequestOptions requestOptions = new AssetBundleRequestOptions();
        requestOptions.AssetLoadMode = AssetLoadMode.RequestedAssetAndDependencies;
        requestOptions.BundleName = internalBundleName;
        requestOptions.ChunkedTransfer = false;
        requestOptions.RetryCount = 0;
        requestOptions.RedirectLimit = 32;
        requestOptions.Timeout = 0;
        requestOptions.BundleSize = 0;
        requestOptions.ClearOtherCachedVersionsWhenLoaded = false;
        requestOptions.Crc = 0;
        requestOptions.UseCrcForCachedBundle = true;
        requestOptions.BundleSize = (new FileInfo(bundlePath)).Length;

        ContentCatalogDataEntry bundleEntry = new(
            typeof(IAssetBundleResource),
            bundlePath,
            "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider",
            new object[] { key },
            dependencyKeys,
            requestOptions
        );

        return bundleEntry;
    }

    /// <summary>
    /// Creates a catalog entry for a bundled asset.
    /// </summary>
    /// <param name="addressablePath">Addressable path of the asset. This primary key is both the key of an asset in a bundle and the key used to load the asset using the Addressables package</param>
    /// <param name="assetType">Unity type of the asset. Eg: GameObject</param>
    /// <param name="ownerBundleKey">Primary key of the bundle containing the asset.</param>
    public static ContentCatalogDataEntry CreateAssetEntry(string addressablePath, Type assetType, string ownerBundleKey)
        => CreateAssetEntry(addressablePath, assetType, [ownerBundleKey]);

    /// <summary>
    /// Creates a catalog entry for a bundled asset.
    /// </summary>
    /// <param name="addressablePath">Addressable path of the asset. This primary key is both the key of an asset in a bundle and the key used to load the asset using the Addressables package</param>
    /// <param name="assetType">Unity type of the asset. Eg: GameObject</param>
    /// <param name="dependencyKeys">List of all the primary keys of the bundle dependencies.
    /// Note: All keys found in the dependencies must have their corresponding entry in the catalog.</param>
    public static ContentCatalogDataEntry CreateAssetEntry(string addressablePath, Type assetType, List<string> dependencyKeys)
    {
        object[] deps = dependencyKeys.Cast<object>().ToArray();

        return new ContentCatalogDataEntry(
            assetType,
            addressablePath,
            "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider",
            new object[] { addressablePath },
            deps,
            null
        );
    }

    public static ContentCatalogDataEntry CreateEntryFromLocation(IResourceLocation location)
    {
        return new ContentCatalogDataEntry(
            location.ResourceType,
            location.InternalId,
            location.ProviderId,
            new object[] { $"{nameof(AssetHelper)}:{location.PrimaryKey}" },
            null,
            location.Data
        );
    }

}
