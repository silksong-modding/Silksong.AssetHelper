using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AssetHelperLib.Repacking;
using Silksong.AssetHelper.CatalogTools;
using Silksong.AssetHelper.Core;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Class to help in building a custom catalog with base game dependencies.
///
/// This class should only be used after the base game catalog has loaded.
/// </summary>
internal class CustomCatalogBuilder
{
    private readonly string _primaryKeyPrefix;

    private readonly Dictionary<string, ContentCatalogDataEntry> _baseBundleEntries;
    private readonly HashSet<string> _includedBaseBundles = [];
    private readonly Dictionary<string, string> _basePrimaryKeys = [];

    private readonly List<ContentCatalogDataEntry> _addedEntries = [];

    public CustomCatalogBuilder(string primaryKeyPrefix = "AssetHelper")
    {
        _primaryKeyPrefix = primaryKeyPrefix;
        _baseBundleEntries = [];
        foreach (string key in AddressablesData.BundleKeys!.Keys)
        {
            if (!AddressablesData.TryGetLocation(key, out IResourceLocation? location))
            {
                continue;
            }

            if (location.ResourceType != typeof(IAssetBundleResource))
            {
                continue;
            }
            if (location.PrimaryKey.StartsWith("scenes_scenes_scenes"))
            {
                continue;
            }

            if (!AddressablesData.TryStrip(location.PrimaryKey, out string? bundleName))
            {
                continue;
            }

            string primaryKey = $"{_primaryKeyPrefix}/DependencyBundles/{bundleName}";
            ContentCatalogDataEntry entry = CatalogEntryUtils.CreateEntryFromLocation(
                location,
                primaryKey
            );

            _baseBundleEntries.Add(bundleName, entry);
            _basePrimaryKeys.Add(bundleName, primaryKey);
        }
    }

    /// <summary>
    /// Declare that the given base game bundle must be included in the catalog.
    /// </summary>
    /// <param name="bundleName">The name of the bundle (including or excluding .bundle).</param>
    /// <param name="primaryKey">The primary key of the bundle.</param>
    /// <returns>False if the given bundle was not found.</returns>
    public bool TryDeclareBundleDep(string bundleName, [NotNullWhen(true)] out string? primaryKey)
    {
        string bundleKey = bundleName.ToLowerInvariant().Replace(".bundle", "");

        if (!_basePrimaryKeys.TryGetValue(bundleKey, out primaryKey))
        {
            return false;
        }

        _includedBaseBundles.Add(bundleKey);
        return true;
    }

    public void AddRepackedSceneData(string sceneName, RepackedBundleData data, string bundlePath)
    {
        // Create an entry for the bundle
        string repackedSceneBundleKey = $"{_primaryKeyPrefix}/SceneBundles/{sceneName}";

        ContentCatalogDataEntry bundleEntry = CatalogEntryUtils.CreateBundleEntry(
            repackedSceneBundleKey,
            bundlePath,
            data.BundleName!,
            []
        );
        _addedEntries.Add(bundleEntry);

        // Get dependency list
        List<string> dependencyKeys = [repackedSceneBundleKey];
        foreach (
            string dep in BundleMetadata.DetermineCatalogDeps(
                $"scenes_scenes_scenes/{sceneName}.bundle"
            )
        )
        {
            if (!TryDeclareBundleDep(dep, out string? primaryKey))
            {
                AssetHelperPlugin.InstanceLogger.LogWarning(
                    $"Error adding asset from scene {sceneName} with alien dep {dep}"
                );
                continue;
            }

            dependencyKeys.Add(primaryKey);
        }

        // Create entries for the assets
        foreach ((string containerPath, string objPath) in data.GameObjectAssets ?? [])
        {
            ContentCatalogDataEntry entry = CatalogEntryUtils.CreateAssetEntry(
                containerPath,
                typeof(GameObject),
                dependencyKeys,
                $"{_primaryKeyPrefix}/Assets/{sceneName}/{objPath}"
            );
            _addedEntries.Add(entry);
        }
    }

    public void AddAssets(string bundle, List<(string asset, Type assetType)> data)
    {
        if (!TryDeclareBundleDep(bundle, out string? mainPkey))
        {
            throw new ArgumentException(
                $"Error adding assets from bundle {bundle}: bundle not recognized"
            );
        }
        List<string> dependencyKeys = [mainPkey];

        foreach (string dep in BundleMetadata.DetermineTransitiveDeps(bundle))
        {
            if (!TryDeclareBundleDep(dep, out string? pkey))
            {
                AssetHelperPlugin.InstanceLogger.LogWarning(
                    $"Error adding asset from {bundle}: unrecognized T-dep {dep}"
                );
                continue;
            }

            dependencyKeys.Add(pkey);
        }

        foreach ((string asset, Type assetType) in data)
        {
            ContentCatalogDataEntry entry = CatalogEntryUtils.CreateAssetEntry(
                asset,
                assetType,
                dependencyKeys,
                $"{_primaryKeyPrefix}/{asset}"
            );
            _addedEntries.Add(entry);
        }
    }

    public void AddCatalogEntry(ContentCatalogDataEntry entry) => _addedEntries.Add(entry);

    public IEnumerator<float> BuildRoutine(string? catalogId = null)
    {
        catalogId ??= _primaryKeyPrefix;

        List<ContentCatalogDataEntry> allEntries =
        [
            .. _includedBaseBundles.Select(x => _baseBundleEntries[x]),
            .. _addedEntries,
        ];

        using IEnumerator<int> serializationRoutine = CatalogUtils.WriteCatalogRoutine(allEntries, catalogId);

        while (serializationRoutine.MoveNext())
        {
            yield return (float)serializationRoutine.Current / allEntries.Count;
        }
    }
}
