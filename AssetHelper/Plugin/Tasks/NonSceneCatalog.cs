using Silksong.AssetHelper.Core;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Silksong.AssetHelper.Plugin.Tasks;

/// <summary>
/// Run the routine to create and load the catalog with requested non-scene assets.
/// </summary>
internal class NonSceneCatalog : BaseStartupTask
{
    // Invalidate all data on disk that's older than this version
    private static readonly Version _lastAcceptablePluginVersion = Version.Parse("0.1.0");

    // Path to the non-scene catalog .bin file
    private static string NonSceneCatalogPath => Path.Combine(AssetPaths.CatalogFolder, $"{CatalogKeys.NonSceneCatalogId}.bin");

    public override IEnumerator Run(LoadingBar loadingBar)
    {
        return CreateAndLoadCatalog();
    }

    private IEnumerator CreateAndLoadCatalog()
    {
        IEnumerator nonSceneCatalogCreate = CreateNonSceneAssetCatalog();
        while (nonSceneCatalogCreate.MoveNext())
        {
            yield return null;
        }
        yield return null;

        if (File.Exists(NonSceneCatalogPath) && AssetRequestAPI.RequestedNonSceneAssets.Count > 0)
        {
            AssetHelperPlugin.InstanceLogger.LogInfo($"Loading non-scene catalog");
            AsyncOperationHandle<IResourceLocator> catalogLoadOp = Addressables.LoadContentCatalogAsync(NonSceneCatalogPath);
            yield return catalogLoadOp;
            AssetRequestAPI.NonSceneAssetLocator = catalogLoadOp.Result;
        }
        yield return null;
    }

    private IEnumerator CreateNonSceneAssetCatalog()
    {
        string catalogMetadataPath = Path.ChangeExtension(NonSceneCatalogPath, ".json");

        AssetHelperPlugin.InstanceLogger.LogInfo($"Creating NS catalog");

        Dictionary<(string bundleName, string assetName), Type> toCatalog = [];
        bool shouldWriteCatalog = false;

        if (JsonExtensions.TryLoadFromFile(catalogMetadataPath, out NonSceneCatalogMetadata? existingCatalogData)
            && existingCatalogData.SilksongVersion == VersionData.SilksongVersion
            && Version.TryParse(existingCatalogData.PluginVersion ?? string.Empty, out Version oldPluginVersion)
            && oldPluginVersion <= Version.Parse(AssetHelperPlugin.Version)
            && oldPluginVersion >= _lastAcceptablePluginVersion)
        {
            toCatalog = existingCatalogData.CatalogAssets;
        }

        foreach ((var assetKey, Type value) in AssetRequestAPI.RequestedNonSceneAssets)
        {
            if (!toCatalog.ContainsKey(assetKey))
            {
                toCatalog.Add(assetKey, value);
                shouldWriteCatalog = true;
                continue;
            }

            Type existingT = toCatalog[assetKey];
            if (existingT != value)
            {
                AssetHelperPlugin.InstanceLogger.LogInfo($"Replacing {assetKey} :: {toCatalog[assetKey]} -> {value}");

                // Existing type is probably incorrect, take the newer type
                toCatalog[assetKey] = value;
                shouldWriteCatalog = true;
                continue;
            }
        }

        if (!shouldWriteCatalog)
        {
            AssetHelperPlugin.InstanceLogger.LogInfo($"Not writing non-scene catalog: no new assets added");
            yield break;
        }

        NonSceneCatalogMetadata metadata = new();

        CustomCatalogBuilder cbr = new(CatalogKeys.NonSceneCatalogId);

        foreach (((string bundleName, string assetName), Type assetType) in AssetRequestAPI.RequestedNonSceneAssets)
        {
            cbr.AddAssets(bundleName, [(assetName, assetType)]);
            metadata.CatalogAssets.Add((bundleName, assetName), assetType);
        }

        yield return null;

        AssetHelperPlugin.InstanceLogger.LogInfo($"Writing catalog");
        cbr.Build();
        metadata.SerializeToFile(catalogMetadataPath);
    }
}
