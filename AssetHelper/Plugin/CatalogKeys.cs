namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Class containing information about the keys used in custom Addressables catalogs.
/// </summary>
public static class CatalogKeys
{
    /// <summary>
    /// The ID of the main catalog with repacked scene assets.
    /// </summary>
    public static string SceneCatalogId => $"{nameof(AssetHelper)}-RepackedScenes";

    /// <summary>
    /// Get the primary key for a repacked scene asset.
    /// 
    /// No attempt is made to check that the scene asset is actually available in the catalog,
    /// but this function returns what the key would be if the asset had been repacked.
    /// </summary>
    /// <param name="sceneName">The name of the original scene.</param>
    /// <param name="objPath">The hierarchical path to the game object.</param>
    public static string GetKeyForSceneAsset(string sceneName, string objPath)
    {
        return $"{SceneCatalogId}/Assets/{sceneName.ToLowerInvariant()}/{objPath}";
    }

    /// <summary>
    /// The ID of the main catalog with non-scene assets.
    /// </summary>
    public static string NonSceneCatalogId => $"{nameof(AssetHelper)}-BundleAssets";

    /// <summary>
    /// Get the primary key for a non-scene bundle asset.
    /// 
    /// No attempt is made to check that the asset is actually available in the catalog,
    /// or even if the asset exists, but this function returns what the key would be.
    /// </summary>
    /// <param name="assetName">The name of the asset in its bundle.</param>
    /// <returns></returns>
    public static string GetKeyForNonSceneAsset(string assetName)
    {
        return $"{NonSceneCatalogId}/{assetName}";
    }

    // TODO - function to return a wrapped asset
}
