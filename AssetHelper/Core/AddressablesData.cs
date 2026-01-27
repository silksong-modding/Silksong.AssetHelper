using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using Silksong.AssetHelper.Internal;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper.Core;

/// <summary>
/// Class managing data about the bundles Addressables database.
/// </summary>
public static class AddressablesData
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource(
        $"{nameof(AssetHelper)}.{nameof(AddressablesData)}"
    );

    /// <summary>
    /// Lookup (lowercase bundle path relative to root bundles folder, with no .bundle suffix) -> (bundle primary key)
    /// </summary>
    private static Dictionary<string, string>? _bundleKeys;

    /// <summary>
    /// The main Addressables resource locator used by the game.
    /// </summary>
    public static IResourceLocator? MainLocator { get; private set; }

    /// <summary>
    /// Mapping from bundle file name to the key Addressables uses to load it.
    /// 
    /// The keys in this dictionary are paths relative to the bundle directory, with no .bundle suffix.
    /// The values are strings that can be used as primary keys in Addressables.
    ///
    /// The values may change when the game updates but does not otherwise.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? BundleKeys =>
        _bundleKeys == null ? null : new ReadOnlyDictionary<string, string>(_bundleKeys);

    /// <summary>
    /// Get the <see cref="IResourceLocation"/> for a given bundle name.
    /// </summary>
    public static bool TryGetLocation(
        string bundleName,
        [NotNullWhen(true)] out IResourceLocation? location
    )
    {
        string key = ToBundleKey(bundleName);
        IList<IResourceLocation> locations = Array.Empty<IResourceLocation>();
        if (!MainLocator?.Locate(key, typeof(IAssetBundleResource), out locations) ?? false)
        {
            location = default;
            return false;
        }

        location = locations.FirstOrDefault();
        return location != null;
    }

    /// <summary>
    /// Get the <see cref="IResourceLocation"/> for a given scene name.
    /// </summary>
    public static bool TryGetLocationForScene(
        string sceneName,
        [NotNullWhen(true)] out IResourceLocation? location
    ) => TryGetLocation($"scenes_scenes_scenes/{sceneName.ToLowerInvariant()}", out location);

    /// <summary>
    /// This is <see langword="true"/> if Addressables has loaded the catalog, <see langword="false"/> otherwise.
    /// </summary>
    public static bool IsAddressablesLoaded => _bundleKeys != null;

    private static DelayedAction _afterAddressablesLoaded = new();

    /// <summary>
    /// Invoke this action once Addressables has loaded the catalog.
    ///
    /// If Addressables has already loaded the catalog, the action will be invoked immediately.
    ///
    /// This is a safe way to execute code that depends on Addressables.
    /// </summary>
    public static void InvokeAfterAddressablesLoaded(Action a) =>
        _afterAddressablesLoaded.Subscribe(a);

    private static readonly string BundleSuffix = @"_[0-9a-fA-F]{32}\.bundle+$";
    private static readonly Regex BundleSuffixRegex = new(BundleSuffix, RegexOptions.Compiled);

    internal static bool TryStrip(string key, [MaybeNullWhen(false)] out string stripped)
    {
        if (BundleSuffixRegex.IsMatch(key))
        {
            stripped = BundleSuffixRegex.Replace(key, "");
            return true;
        }
        else
        {
            stripped = key;
            return false;
        }
    }

    internal static bool TryLoadBundleKeys()
    {
        Dictionary<string, string> bundleKeys = [];

        Stopwatch sw = Stopwatch.StartNew();

        MainLocator = Addressables.ResourceLocators.FirstOrDefault(x => x.Keys.Any());

        if (MainLocator == null)
        {
            Log.LogWarning($"Addressables not loaded yet");
            return false;
        }

        foreach (string key in MainLocator.Keys.OfType<string>())
        {
            if (!TryStrip(key, out string? stripped))
                continue;

            bundleKeys[stripped] = key;
        }

        sw.Stop();

        Log.LogInfo($"Loaded {bundleKeys.Count} bundle locations in {sw.ElapsedMilliseconds} ms");

        if (bundleKeys.Count == 0)
        {
            return false;
        }

        _bundleKeys = bundleKeys;

        _afterAddressablesLoaded.Activate();

        return true;
    }

    /// <summary>
    /// Convert a name to an asset bundle key.
    /// </summary>
    public static string ToBundleKey(string name)
    {
        if (_bundleKeys == null)
        {
            Log.LogWarning($"{nameof(ToBundleKey)} called before addressables loaded");
            return name;
        }

        if (TryStrip(name, out string? stripped))
        {
            name = stripped;
        }
        else if (name.EndsWith(".bundle"))
        {
            name = name[..^7];
        }

        if (_bundleKeys.TryGetValue(name, out string primaryKey))
        {
            return primaryKey;
        }

        throw new Exception($"Bundle {name} not found in lookup.");
    }
}
