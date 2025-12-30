using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Data about a repacked bundle.
/// </summary>
public class RepackedBundleData
{
    /// <summary>
    /// The name of the internal asset bundle.
    /// </summary>
    public string? BundleName { get; set; }

    /// <summary>
    /// The CAB name of the bundle file.
    /// </summary>
    public string? CabName { get; set; }

    /// <summary>
    /// A list of asset paths in the asset bundle container.
    /// </summary>
    public List<string>? GameObjectAssets { get; set; }

    /// <summary>
    /// Get the ancestor of the given game object within the repacked bundle.
    /// 
    /// If bun is a <see cref="UnityEngine.AssetBundle"/> object loaded from this bundle, then
    /// bun.LoadAsset&lt;GameObject&gt;(ancestorName).FindChild(relativePath) will
    /// retrieve the requested game object.
    /// </summary>
    /// <param name="objName">The name of a game object from the original scene.
    /// This should be a path of the form root/.../grandparent/parent/object, with no leading slash.</param>
    /// <param name="ancestorName">The asset name representing the Ancestor.</param>
    /// <param name="relativePath"></param>
    /// <returns>False if the supplied game object has no ancestor in the repacked bundle.</returns>
    public bool TryGetAncestor(string objName, [MaybeNullWhen(false)] out string ancestorName, [MaybeNullWhen(false)] out string relativePath)
    {
        List<string> ancestorPaths = [];
        Dictionary<string, string> pathToKey = [];

        foreach (string assetName in GameObjectAssets ?? Enumerable.Empty<string>())
        {
            string assetPath = assetName[(nameof(AssetHelper).Length + 1)..];
            if (assetPath.EndsWith(".prefab"))
            {
                assetPath = assetPath[..^".prefab".Length];
            }
            ancestorPaths.Add(assetPath);
            pathToKey[assetPath] = assetName;
        }

        if (!ObjPathUtil.TryFindAncestor(ancestorPaths, objName, out string? ancestorPath, out relativePath))
        {
            ancestorName = null;
            return false;
        }

        ancestorName = pathToKey[ancestorPath];
        return true;
    }
}
