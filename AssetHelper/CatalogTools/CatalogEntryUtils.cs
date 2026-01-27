using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
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
    /// <param name="primaryKey">The primary key of the bundle.</param>
    /// <param name="bundlePath">The fully qualified path to the bundle on the filesystem</param>
    /// <param name="internalBundleName">The name of the AssetBundle asset in the bundle.</param>
    /// <param name="dependencyKeys">List of all the primary keys of the bundle dependencies.
    /// Note: All keys found in the dependencies must have their corresponding entry in the catalog.</param>
    /// <param name="serializedBundlePath">The bundle path as it appears in the catalog.
    /// This may be different to the bundle path if the bundle path should be serialized relative
    /// to a prefix; see AddressablesRuntimeProperties.EvaluateProperty.</param>
    public static ContentCatalogDataEntry CreateBundleEntry(
        string primaryKey,
        string bundlePath,
        string internalBundleName,
        List<string> dependencyKeys,
        string? serializedBundlePath = null
    )
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
            serializedBundlePath ?? bundlePath,
            "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider",
            new object[] { primaryKey },
            dependencyKeys,
            requestOptions
        );

        return bundleEntry;
    }

    /// <inheritdoc cref="CreateAssetEntry(string, Type, List{string}, out string)" />
    public static ContentCatalogDataEntry CreateAssetEntry(
        string internalId,
        Type assetType,
        List<string> dependencyKeys,
        out string primaryKey
    )
    {
        primaryKey = internalId;

        return CreateAssetEntry(internalId, assetType, dependencyKeys, primaryKey);
    }

    /// <summary>
    /// Creates a catalog entry for a bundled asset.
    /// </summary>
    /// <param name="internalId">The internal ID of the asset. This is the name of the asset within the bundle.</param>
    /// <param name="assetType">Unity type of the asset. Eg: GameObject</param>
    /// <param name="dependencyKeys">Primary keys of the bundle dependencies. These should be in the catalog.</param>
    /// <param name="primaryKey">The primary key used to access the asset with Addressables.</param>
    public static ContentCatalogDataEntry CreateAssetEntry(
        string internalId,
        Type assetType,
        List<string> dependencyKeys,
        string primaryKey
    )
    {
        object[] deps = dependencyKeys.Cast<object>().ToArray();

        return new ContentCatalogDataEntry(
            assetType,
            internalId,
            "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider",
            new object[] { primaryKey },
            deps,
            null
        );
    }

    /// <summary>
    /// Create a catalog entry representing a child gameobject of
    /// the gameObject loaded by parentPrimaryKey.
    /// </summary>
    /// <param name="parentPrimaryKey">The primary key of the parent.</param>
    /// <param name="relativePath">The path of the child relative to the parent, with no leading slash.</param>
    /// <param name="primaryKey">The primary key of the added entry.</param>
    public static ContentCatalogDataEntry CreateChildGameObjectEntry(
        string parentPrimaryKey,
        string relativePath,
        out string primaryKey
    )
    {
        int lastDot = parentPrimaryKey.LastIndexOf('.');
        int lastSlash = parentPrimaryKey.LastIndexOf('/');

        // Normal case for AssetHelper, path is of the form {stuff}.prefab
        if (lastDot != -1 && lastDot > lastSlash)
        {
            string mainId = parentPrimaryKey.Substring(0, lastDot);
            string suffix = parentPrimaryKey.Substring(lastDot);
            primaryKey = $"{mainId}/{relativePath}{suffix}";
        }
        // This would be weird but we should avoid double slash
        else if (parentPrimaryKey.EndsWith('/'))
        {
            primaryKey = $"{parentPrimaryKey}{relativePath}";
        }
        else
        {
            primaryKey = $"{parentPrimaryKey}/{relativePath}";
        }

        return CreateChildGameObjectEntry(parentPrimaryKey, relativePath, primaryKey);
    }

    /// <inheritdoc cref="CreateChildGameObjectEntry(string, string, out string)" />
    public static ContentCatalogDataEntry CreateChildGameObjectEntry(
        string parentPrimaryKey,
        string relativePath,
        string primaryKey
    )
    {
        object[] deps = new object[] { parentPrimaryKey };

        return new ContentCatalogDataEntry(
            typeof(GameObject),
            // Put the parent primary key to ensure the internal ID is unique
            $"{relativePath}/{ChildGameObjectProvider.InternalIdSeparator}/{parentPrimaryKey}",
            ChildGameObjectProvider.ClassProviderId,
            new object[] { primaryKey },
            deps,
            null
        );
    }

    /// <inheritdoc cref="CreateEntryFromLocation(IResourceLocation, string)" />
    public static ContentCatalogDataEntry CreateEntryFromLocation(
        IResourceLocation location,
        out string primaryKey
    )
    {
        primaryKey = $"{nameof(AssetHelper)}:{location.PrimaryKey}";

        return CreateEntryFromLocation(location, primaryKey);
    }

    /// <summary>
    /// Create a catalog entry based on the given location.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="primaryKey">The primary key for the new catalog entry.</param>
    /// <returns></returns>
    public static ContentCatalogDataEntry CreateEntryFromLocation(
        IResourceLocation location,
        string primaryKey
    )
    {
        return new ContentCatalogDataEntry(
            location.ResourceType,
            location.InternalId,
            location.ProviderId,
            new object[] { primaryKey },
            null,
            location.Data
        );
    }
}
