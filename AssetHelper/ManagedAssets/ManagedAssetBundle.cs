using Silksong.AssetHelper.Core;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper.ManagedAssets;

/// <summary>
/// Class wrapping an asset bundle that can be loaded by Addressables.
/// 
/// Typically assets should be loaded directly (e.g. via a <see cref="ManagedAsset{T}"/>)
/// because loading bundles in this way may not load dependencies.
/// </summary>
/// <param name="bundleName">The name of the bundle, given as a path relative to
/// the StandaloneX folder.</param>
public class ManagedAssetBundle(string bundleName) : ManagedAssetBase<IAssetBundleResource>
{
    /// <summary>
    /// The name of the bundle, given as a path relative to
    /// the StandaloneX folder.
    /// </summary>
    public string BundleName { get; } = bundleName;

    /// <inheritdoc />
    protected internal override string Identifier => BundleName;

    /// <inheritdoc />
    protected override AsyncOperationHandle<IAssetBundleResource> DoLoad()
        => Addressables.LoadAssetAsync<IAssetBundleResource>(
            AddressablesData.ToBundleKey(BundleName));

    /// <summary>
    /// Create a new <see cref="ManagedAssetBundle" /> instance that wraps the same underlying asset.
    /// The new instance starts out unloaded.
    /// </summary>
    public ManagedAssetBundle Clone()
    {
        return new(BundleName);
    }

}
