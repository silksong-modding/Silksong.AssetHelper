using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.CatalogTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
            ContentCatalogDataEntry entry = CatalogEntryUtils.CreateEntryFromLocation(location, primaryKey);

            _baseBundleEntries.Add(bundleName, entry);
            _basePrimaryKeys.Add(bundleName, primaryKey);
        }
    }

    public void AddRepackedSceneData(string sceneName, RepackedBundleData data, string bundlePath)
    {
        // Create an entry for the bundle
        string repackedSceneBundleKey = $"{_primaryKeyPrefix}/SceneBundles/{sceneName}";

        ContentCatalogDataEntry bundleEntry = CatalogEntryUtils.CreateBundleEntry(
                repackedSceneBundleKey,
                bundlePath,
                data.BundleName!,
                []);
        _addedEntries.Add(bundleEntry);

        // Get dependency list
        List<string> dependencyKeys = [repackedSceneBundleKey];
        foreach (string dep in BundleDeps.DetermineDirectDeps($"scenes_scenes_scenes/{sceneName}.bundle"))
        {
            string depKey = dep.Replace(".bundle", "");
            _includedBaseBundles.Add(depKey);
            dependencyKeys.Add(_basePrimaryKeys[depKey]);
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
        string bundleKey = bundle.Replace(".bundle", "");
        _includedBaseBundles.Add(bundleKey);
        List<string> dependencyKeys = [_basePrimaryKeys[bundleKey]];
        foreach (string dep in BundleDeps.DetermineDirectDeps(bundle))
        {
            string depKey = dep.Replace(".bundle", "");
            _includedBaseBundles.Add(depKey);
            dependencyKeys.Add(_basePrimaryKeys[depKey]);
        }

        foreach ((string asset, Type assetType) in data)
        {
            ContentCatalogDataEntry entry = CatalogEntryUtils.CreateAssetEntry(
                asset, assetType, dependencyKeys, $"{_primaryKeyPrefix}/{asset}");
            _addedEntries.Add(entry);
        }
    }

    // TODO - this should produce information about the catalog
    public string Build(string? catalogId = null)
    {
        catalogId ??= _primaryKeyPrefix;

        List<ContentCatalogDataEntry> allEntries = [.. _includedBaseBundles.Select(x => _baseBundleEntries[x]), .. _addedEntries];

        string catalogPath = CatalogUtils.WriteCatalog(allEntries, catalogId);

        return catalogPath;
    }
}
