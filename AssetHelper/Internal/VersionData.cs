using System;
using System.Reflection;

namespace Silksong.AssetHelper.Internal;

internal static class VersionData
{
    /// <summary>
    /// The Silksong version. This is calculated using reflection to avoid it being inlined.
    /// </summary>
    public static string SilksongVersion
    {
        get
        {
            _silksongVersion ??= GetSilksongVersion();
            return _silksongVersion;
        }
    }

    private static string? _silksongVersion;

    private static string GetSilksongVersion() =>
        typeof(Constants)
            .GetField(
                nameof(Constants.GAME_VERSION),
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static
            )
            ?.GetRawConstantValue() as string
        ?? "UNKNOWN";

    /// <summary>
    /// The earliest acceptable plugin version for general cached data.
    ///
    /// This should be increased to invalidate old cached data.
    /// </summary>
    internal static Version EarliestAcceptableGeneralVersion { get; } = Version.Parse("0.1.0");

    /// <summary>
    /// The earliest acceptable plugin version for scene repacking.
    ///
    /// This should be increased to invalidate cached repacked bundles and catalogs.
    /// </summary>
    internal static Version EarliestAcceptableSceneRepackVersion { get; } = Version.Parse("0.1.0");

    /// <summary>
    /// The earliest acceptable plugin version for the non-scene catalog.
    ///
    /// This should be increased to invalidate any cached catalog.
    /// </summary>
    internal static Version EarliestAcceptableNonSceneCatalogVersion { get; } =
        Version.Parse("0.1.0");

    /// <summary>
    /// Return false to invalidate the past cached data.
    /// </summary>
    internal static bool AllowCachedData(this Version earliest, string cachedPluginVersion)
    {
        if (!Version.TryParse(cachedPluginVersion, out Version toCheck))
        {
            return false;
        }

        if (toCheck > Version.Parse(AssetHelperPlugin.Version))
        {
            return false;
        }

        if (toCheck < earliest)
        {
            return false;
        }

        return true;
    }
}
