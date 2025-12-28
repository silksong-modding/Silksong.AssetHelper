using BepInEx.Logging;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper.LoadedAssets;

/// <summary>
/// Class representing a collection of asset bundles that can be loaded and unloaded
/// together.
/// </summary>
/// <param name="bundleNames">The Addressables keys to the asset bundles.
/// It is expected that the first key represents the main bundle
/// and subsequent keys represent dependencies.</param>
public class AssetBundleGroup(List<string> bundleNames)
{
    /// <summary>
    /// Create an AssetBundleGroup which loads the given bundle as well as its direct dependencies.
    /// </summary>
    public static AssetBundleGroup CreateWithDependencies(string mainBundle, bool includeMainBundle = true)
    {
        if (GameEvents.IsInGame)
        {
            Log.LogWarning(
                "Checking dependencies may be slow and should not be done while in game."
                );
        }
        List<string> deps = Deps.DetermineDirectDeps(mainBundle).Where(x => x != mainBundle).ToList();
        
        if (includeMainBundle)
        {
            List<string> bundles = [mainBundle, .. deps];
            return new(bundles);
        }
        else
        {
            return new(deps);
        }
    }

    /// <summary>
    /// Create an asset bundle group for the given scene.
    /// </summary>
    /// <param name="sceneName">The scene.</param>
    /// <param name="includeScene">Whether the scene bundle itself should be included in the group.</param>
    /// <returns></returns>
    public static AssetBundleGroup CreateForScene(string sceneName, bool includeScene)
    {
        string key = $"scenes_scenes_scenes/{sceneName.ToLowerInvariant()}";

        return CreateWithDependencies(key, includeScene);
    }

    private static readonly ManualLogSource Log = Logger.CreateLogSource(nameof(AssetBundleGroup));

    private readonly List<string> _bundleNames = [.. bundleNames];

    private List<string>? _bundleKeys;

    /// <summary>
    /// The Addressables keys for the bundles in this group.
    /// </summary>
    public List<string> BundleKeys
    {
        get
        {
            if (_bundleKeys != null) return _bundleKeys;

            List<string> keys = [];
            bool failure = false;
            foreach (string name in _bundleNames)
            {
                string key = AssetsData.ToBundleKey(name);
                if (key == null)
                {
                    failure = true;
                    Log.LogError($"Could not determine bundle key: {name}");
                }
                else
                {
                    keys.Add(key);
                }
            }

            if (failure)
            {
                throw new Exception("Could not determine all bundle keys");
            }

            _bundleKeys = keys;
            return _bundleKeys;
        }
    }

    /// <summary>
    /// Whether the bundle group has been loaded.
    /// </summary>
    public bool Loaded { get; private set; }
    private AsyncOperationHandle<IList<IAssetBundleResource>>? _opHandle;

    /// <summary>
    /// The list of <see cref="IAssetBundleResource"/> objects
    /// wrapping the bundles.
    /// </summary>
    public IList<IAssetBundleResource>? BundleResources
    {
        get
        {
            if (!Loaded) return null;
            if (_opHandle == null) return null;

            return _opHandle.Value.Result;
        }
    }

    /// <summary>
    /// The main bundle loaded by this object.
    /// </summary>
    public AssetBundle? MainBundle
    {
        get
        {
            if (BundleResources == null) return null;
            if (BundleResources.Count == 0)
            {
                throw new Exception("No bundles loaded");
            }

            return BundleResources[0].GetAssetBundle();
        }
    }

    /// <summary>
    /// Load the bundles.
    /// </summary>
    /// <param name="callback">Optional callback to be executed on load.</param>
    public void Load(Action<AssetBundleGroup>? callback = null)
    {
        if (Loaded)
        {
            ActionUtil.SafeInvoke(callback, this);
            return;
        }

        _opHandle = Addressables.LoadAssetsAsync<IAssetBundleResource>(
            BundleKeys, null, Addressables.MergeMode.Union);
        _opHandle.Value.Completed += _ =>
        {
            Loaded = true;
            ActionUtil.SafeInvoke(callback, this);
        };
    }

    /// <summary>
    /// Coroutine to load the bundles.
    /// </summary>
    /// <param name="callback">Optional callback to execute on load.</param>
    public IEnumerator LoadAsync(Action<AssetBundleGroup>? callback = null)
    {
        if (Loaded)
        {
            ActionUtil.SafeInvoke(callback, this);
            yield break;
        }

        _opHandle = Addressables.LoadAssetsAsync<IAssetBundleResource>(
            BundleKeys, null, Addressables.MergeMode.Union);

        yield return _opHandle;
        Loaded = true;
        ActionUtil.SafeInvoke(callback, this);
    }

    /// <summary>
    /// Load the bundles, and block the main thread until they are loaded.
    /// </summary>
    public void LoadImmediate()
    {
        if (Loaded)
        {
            return;
        }

        _opHandle = Addressables.LoadAssetsAsync<IAssetBundleResource>(
            BundleKeys, null, Addressables.MergeMode.Union);
        _opHandle.Value.WaitForCompletion();

        Loaded = true;
    }

    /// <summary>
    /// Unload the bundles.
    /// </summary>
    public void Unload()
    {
        if (!Loaded)
        {
            return;
        }

        Addressables.Release(_opHandle);
        _opHandle = null;
        Loaded = false;
    }
}
