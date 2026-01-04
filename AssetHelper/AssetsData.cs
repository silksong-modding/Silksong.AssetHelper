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

namespace Silksong.AssetHelper;

/// <summary>
/// Class managing data about the Addressables database.
/// </summary>
public static class AssetsData
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"{nameof(AssetsData)}");

    private static Dictionary<string, string>? _bundleKeys;

    /// <summary>
    /// Mapping from bundle file name to the key Addressables uses to load it.
    /// 
    /// This may change when the game updates but does not otherwise.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? BundleKeys
    {
        get
        {
            if (_bundleKeys == null)
            {
                return null;
            }
            return new ReadOnlyDictionary<string, string>(_bundleKeys);
        }
    }
        
    private static readonly List<Action> _toInvokeAfterAddressablesLoaded = [];
    
    /// <summary>
    /// This is <see langword="true"/> if Addressables has loaded the catalog, <see langword="false"/> otherwise.
    /// </summary>
    public static bool IsAddressablesLoaded => _bundleKeys != null;

    /// <summary>
    /// Invoke this action once Addressables has loaded the catalog.
    /// 
    /// If Addressables has already loaded the catalog, the action will be invoked immediately.
    /// 
    /// This is a safe way to execute code that depends on Addressables.
    /// </summary>
    public static void InvokeAfterAddressablesLoaded(Action a)
    {
        if (!IsAddressablesLoaded)
        {
            _toInvokeAfterAddressablesLoaded.Add(a);
        }
        else
        {
            ActionUtil.SafeInvoke(a);
        }
    }

    
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
        Dictionary<string, string> keys = [];

        Stopwatch sw = Stopwatch.StartNew();

        foreach (IResourceLocator locator in Addressables.ResourceLocators)
        {
            foreach (string key in locator.Keys.OfType<string>())
            {
                if (!TryStrip(key, out string? stripped)) continue;

                keys[stripped] = key;
            }
        }

        sw.Stop();

        Log.LogInfo($"Loaded {keys.Count} keys in {sw.ElapsedMilliseconds} ms");

        if (keys.Count == 0)
        {
            return false;
        }

        _bundleKeys = keys;
        foreach (Action a in _toInvokeAfterAddressablesLoaded)
        {
            ActionUtil.SafeInvoke(a);
        }

        return true;
    }

    /// <summary>
    /// Convert a name to an asset bundle key.
    /// </summary>
    public static string ToBundleKey(string name)
    {
        // TODO - return null if keys can't be decoded

        if (_bundleKeys == null)
        {
            Log.LogWarning($"{nameof(ToBundleKey)} called before addressables loaded");
            return name;
        }

        if (name.EndsWith(".bundle"))
        {
            name = name[..^7];
        }
        if (_bundleKeys.TryGetValue(name, out string key))
        {
            return key;
        }

        return name;
    }
}
