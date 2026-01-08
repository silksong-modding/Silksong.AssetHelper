using Newtonsoft.Json;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;

namespace Silksong.AssetHelper.Plugin;

internal class CatalogMetadata
{
    public string SilksongVersion { get; set; } = AssetPaths.SilksongVersion;

    public string PluginVersion { get; set; } = AssetHelperPlugin.Version;
}

/// <summary>
/// Data about the scene asset catalog written by AssetHelper.
/// </summary>
internal class SceneCatalogMetadata : CatalogMetadata
{
    // TODO - list catalogued objects
}

/// <summary>
/// Data about the non-scene asset catalog written by AssetHelper.
/// </summary>
internal class NonSceneCatalogMetadata : CatalogMetadata
{
    [JsonConverter(typeof(DictListConverter<(string, string), Type>))]
    public Dictionary<(string bundleName, string assetName), Type> CatalogAssets { get; set; } = [];
}