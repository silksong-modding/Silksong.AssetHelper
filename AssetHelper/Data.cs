using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Silksong.AssetHelper;

public static class Data
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"AssetHelper.{nameof(Data)}");
    
    private static Dictionary<string, string>? _bundleKeys { get; set; }

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
                throw new InvalidOperationException("Addressables has not loaded yet.");
            }
            return new ReadOnlyDictionary<string, string>(_bundleKeys);
        }
    }
        

    private static void SafeInvoke(Action a)
    {
        try
        {
            a?.Invoke();
        }
        catch (Exception ex)
        {
            Log.LogError($"Error invoking action {a.Method.Name}\n" + ex);
        }
    }

    private static readonly List<Action> _toInvokeAfterAddressablesLoaded = new();
    
    /// <summary>
    /// Invoke this action once Addressables has loaded the catalog.
    /// 
    /// If Addressables has already loaded the catalog, the action will be invoked immediately.
    /// 
    /// This is a safe way to execute code that depends on Addressables.
    /// </summary>
    public static void InvokeAfterAddressablesLoaded(Action a)
    {
        if (_bundleKeys == null)
        {
            _toInvokeAfterAddressablesLoaded.Add(a);
        }
        else
        {
            SafeInvoke(a);
        }
    }

    
    private static readonly string BundleSuffix = @"_[0-9a-fA-F]{32}\.bundle+$";
    private static readonly Regex BundleSuffixRegex = new(BundleSuffix, RegexOptions.Compiled);

    private static bool TryStrip(string key, [MaybeNullWhen(false)] out string stripped)
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
            foreach (string key in locator.Keys)
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
            SafeInvoke(a);
        }

        return true;
    }
}
