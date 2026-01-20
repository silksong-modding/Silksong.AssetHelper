using AssetHelperLib.Repacking;
using Silksong.AssetHelper.Internal;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Data about a repacked scene bundle used by AssetHelper.
/// </summary>
public sealed class RepackedSceneBundleData
{
    /// <summary>
    /// The Silksong version used to create the bundle.
    /// </summary>
    public string SilksongVersion { get; init; } = VersionData.SilksongVersion;

    /// <summary>
    /// The Asset Helper version used to create the bundle.
    /// </summary>
    public string PluginVersion { get; init; } = AssetHelperPlugin.Version;

    /// <summary>
    /// The name of the scene used to generate the bundle.
    /// </summary>
    public required string SceneName { get; init; }

    /// <summary>
    /// The hash of the original bundle as it appears in the default Addressables catalog.
    /// </summary>
    public string? BundleHash { get; init; }

    /// <summary>
    /// The data generated for the repacked bundle.
    /// </summary>
    public RepackedBundleData? Data { get; set; } = null;
}
