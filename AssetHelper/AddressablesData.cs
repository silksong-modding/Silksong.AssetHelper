using BepInEx.Logging;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper;

/// <summary>
/// Class managing data about the bundles Addressables database.
/// </summary>
public static class AddressablesData
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"{nameof(AssetHelper)}.{nameof(AddressablesData)}");

    /// <summary>
    /// Lookup (lowercase bundle path relative to root bundles folder, with no .bundle suffix) -> (IResourceLocation in the default catalog)
    /// </summary>
    private static Dictionary<string, IResourceLocation>? _bundleLocations;

    /// <summary>
    /// Mapping from bundle file name to the <see cref="IResourceLocation"/> in the default Addressables catalog.
    /// </summary>
    public static IReadOnlyDictionary<string, IResourceLocation>? BundleLocations
    {
        get
        {
            if (_bundleLocations is null)
            {
                return null;
            }
            return new ReadOnlyDictionary<string, IResourceLocation>(_bundleLocations);
        }
    }

    /// <summary>
    /// Get the <see cref="IResourceLocation"/> for a given scene name.
    /// </summary>
    public static bool TryGetLocationForScene(string sceneName, [NotNullWhen(true)] out IResourceLocation? location)
    {
        sceneName = sceneName.ToLowerInvariant();
        string key = $"scenes_scenes_scenes/{sceneName}";
        if (BundleLocations is null)
        {
            location = default;
            return false;
        }

        if (!BundleLocations.TryGetValue(key, out location))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Mapping from bundle file name to the key Addressables uses to load it.
    /// 
    /// This may change when the game updates but does not otherwise.
    /// </summary>
    public static Dictionary<string, string>? BundleKeys => BundleLocations?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.PrimaryKey);
            
    /// <summary>
    /// This is <see langword="true"/> if Addressables has loaded the catalog, <see langword="false"/> otherwise.
    /// </summary>
    public static bool IsAddressablesLoaded => _bundleLocations != null;

    private static DelayedAction _afterAddressablesLoaded = new();

    /// <summary>
    /// Invoke this action once Addressables has loaded the catalog.
    /// 
    /// If Addressables has already loaded the catalog, the action will be invoked immediately.
    /// 
    /// This is a safe way to execute code that depends on Addressables.
    /// </summary>
    public static void InvokeAfterAddressablesLoaded(Action a) => _afterAddressablesLoaded.Subscribe(a);

    
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
        Dictionary<string, IResourceLocation> locations = [];

        Stopwatch sw = Stopwatch.StartNew();

        IResourceLocator? defaultLocator = Addressables.ResourceLocators.FirstOrDefault(x => x.AllLocations.Any());

        if (defaultLocator == null)
        {
            Log.LogWarning($"Addressables not loaded yet");
            return false;
        }

        foreach (IResourceLocation location in defaultLocator.AllLocations)
        {
            if (location.ResourceType != typeof(IAssetBundleResource))
            {
                continue;
            }

            if (!TryStrip(location.PrimaryKey, out string? stripped)) continue;

            locations[stripped] = location;
        }

        sw.Stop();

        Log.LogInfo($"Loaded {locations.Count} bundle locations in {sw.ElapsedMilliseconds} ms");

        if (locations.Count == 0)
        {
            return false;
        }

        _bundleLocations = locations;

        _afterAddressablesLoaded.Activate();

        return true;
    }

    /// <summary>
    /// Convert a name to an asset bundle key.
    /// </summary>
    public static string ToBundleKey(string name)
    {
        if (_bundleLocations == null)
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

        if (_bundleLocations.TryGetValue(name, out IResourceLocation location))
        {
            return location.PrimaryKey;
        }

        throw new Exception($"Bundle {name} not found in lookup.");
    }
}
