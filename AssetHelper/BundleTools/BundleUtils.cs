using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Linq;
using PPtrData = (int fileId, long pathId);

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Utility functions for asset bundles.
/// </summary>
public static class BundleUtils
{
    /// <summary>
    /// Record representing an AssetFileInfo with its associated AssetTypeValueField.
    /// </summary>
    /// <param name="Info"></param>
    /// <param name="ValueField"></param>
    public record AssetData(AssetFileInfo Info, AssetTypeValueField ValueField);

    /// <summary>
    /// Record representing a collection of PPtrs associated with an asset.
    /// </summary>
    /// <param name="InternalPaths">Path IDs within the current file.</param>
    /// <param name="ExternalPaths">Pairs (file ID, path ID) external to the current file.</param>
    public record ChildPPtrs(HashSet<long> InternalPaths, HashSet<PPtrData> ExternalPaths)
    {
        /// <summary>
        /// Add a new PPtr to the collection.
        /// </summary>
        public bool Add(int fileId, long pathId)
        {
            if (pathId == 0) return false;
            if (fileId == 0) return InternalPaths.Add(pathId);
            return ExternalPaths.Add((fileId, pathId));
        }

        /// <inheritdoc cref="Add(int, long)" />
        public bool Add(AssetTypeValueField valueField) => Add(valueField["m_FileID"].AsInt, valueField["m_PathID"].AsLong);
    }

    /// <summary>
    /// Create an <see cref="AssetsManager"/> with the default settings for AssetHelper.
    /// </summary>
    public static AssetsManager CreateDefaultManager() => new()
    {
        UseQuickLookup = true,
        UseMonoTemplateFieldCache = true,
        UseRefTypeManagerCache = true,
        UseTemplateFieldCache = true,
    };

    /// <summary>
    /// Asset class IDs representing a transform.
    /// </summary>
    public static List<AssetClassID> TransformClassIds { get; } = [AssetClassID.Transform, AssetClassID.RectTransform];

    /// <summary>
    /// Enumerate transforms in this bundle, only including transforms with no parent.
    /// </summary>
    public static IEnumerable<AssetData> GetRootTransforms(this AssetsManager mgr, AssetsFileInstance afileInst)
    {
        foreach (AssetClassID cid in TransformClassIds)
        {
            foreach (AssetFileInfo info in afileInst.file.GetAssetsOfType(cid))
            {
                AssetTypeValueField transform = mgr.GetBaseField(afileInst, info);
                AssetTypeValueField parent = transform["m_Father"];

                if (parent["m_PathID"].AsLong == 0)
                {
                    yield return new(info, transform);
                }
            }
        }
    }

    /// <summary>
    /// Find root game objects matching the given object names.
    /// </summary>
    public static Dictionary<string, AssetData> FindRootGameObjects(
        this AssetsManager mgr,
        AssetsFileInstance afileInst,
        List<string> objectNames,
        out List<string> missingObjects)
    {
        Dictionary<string, AssetData> gameObjects = new();

        foreach (AssetData data in mgr.GetRootTransforms(afileInst))
        {
            AssetFileInfo goInfo = afileInst.file.GetAssetInfo(data.ValueField["m_GameObject.m_PathID"].AsLong);
            AssetTypeValueField goValueField = mgr.GetBaseField(afileInst, goInfo);
            string goName = goValueField["m_Name"].AsString;

            if (objectNames.Contains(goName))
            {
                gameObjects[goName] = new(goInfo, goValueField);
            }
        }

        missingObjects = objectNames.Where(x => !gameObjects.ContainsKey(x)).ToList();
        return gameObjects;
    }

    /// <inheritdoc cref="GetTransformName(AssetsManager, AssetsFileInstance, AssetTypeValueField)" />
    public static string GetTransformName(this AssetsManager mgr, AssetsFileInstance afileInst, AssetFileInfo info)
    {
        AssetTypeValueField transform = mgr.GetBaseField(afileInst, info);
        return mgr.GetTransformName(afileInst, transform);
    }

    /// <summary>
    /// Get the name of the gameobject associated with a given transform.
    /// </summary>
    public static string GetTransformName(this AssetsManager mgr, AssetsFileInstance afileInst, AssetTypeValueField transform)
    {
        long goPathId = transform["m_GameObject.m_PathID"].AsLong;
        AssetTypeValueField go = mgr.GetBaseField(afileInst, goPathId);
        return go["m_Name"].AsString;
    }

    /// <summary>
    /// Find the given game object in the bundle. The name should be of the form .../grandparent/parent/object,
    /// with no leading slash.
    /// </summary>
    public static AssetData FindTransform(this AssetsManager mgr, AssetsFileInstance afileInst, string name)
    {
        string[] parts = name.Split('/');
        string first = parts[0];
        string[] rest = parts[1..];

        AssetData? rootData = null;
        foreach (AssetData dataT in mgr.GetRootTransforms(afileInst))
        {
            if (mgr.GetTransformName(afileInst, dataT.ValueField) == first)
            {
                rootData = dataT;
                break;
            }
        }
        if (rootData == null)
        {
            throw new Exception($"Did not find root game object {first}");
        }

        AssetData current = rootData;
        
        foreach (string part in rest)
        {
            bool foundChild = false;
            foreach (AssetTypeValueField childVf in current.ValueField["m_Children.Array"].Children)
            {
                long childPptr = childVf["m_PathID"].AsLong;
                AssetFileInfo childInfo = afileInst.file.GetAssetInfo(childPptr);
                AssetTypeValueField childValueField = mgr.GetBaseField(afileInst, childInfo);

                if (mgr.GetTransformName(afileInst, childValueField) == part)
                {
                    foundChild = true;
                    current = new(childInfo, childValueField);
                    break;
                }
            }
            if (foundChild == false)
            {
                throw new Exception($"Did not find part {part}");
            }
        }

        return current;
    }

    /// <summary>
    /// Find all PPtr nodes pointed to by the given asset.
    /// </summary>
    /// <param name="mgr">The AssetsManager in use.</param>
    /// <param name="afileInst">The Assets file instance.</param>
    /// <param name="assetPathId">The path ID for the asset to check.</param>
    /// <param name="followParent">If false (default), will not check the parent for a transform.</param>
    public static ChildPPtrs FindPPtrNodes(
        this AssetsManager mgr,
        AssetsFileInstance afileInst,
        long assetPathId,
        bool followParent = false
        )
    {
        AssetFileInfo info = afileInst.file.GetAssetInfo(assetPathId);

        HashSet<long> internalPPtrs = [];
        HashSet<PPtrData> externalPPtrs = [];
        ChildPPtrs childPPtrs = new(internalPPtrs, externalPPtrs);

        if (followParent && (
            info.TypeId == (int)AssetClassID.Transform
            || info.TypeId == (int)AssetClassID.RectTransform
            ))
        {
            AssetTypeValueField tfValueField = mgr.GetBaseField(afileInst, info);

            childPPtrs.Add(tfValueField["m_GameObject"]);

            foreach (AssetTypeValueField childVf in tfValueField["m_Children.Array"].Children)
            {
                childPPtrs.Add(childVf);
            }
            return childPPtrs;
        }

        AssetTypeTemplateField templateField = mgr.GetTemplateBaseField(afileInst, info);
        RefTypeManager refMan = mgr.GetRefTypeManager(afileInst);

        long assetPos = info.GetAbsoluteByteOffset(afileInst.file);
        AssetTypeValueIterator atvIterator = new(templateField, afileInst.file.Reader, assetPos, refMan);

        while (atvIterator.ReadNext())
        {
            string typeName = atvIterator.TempField.Type;

            if (!typeName.StartsWith("PPtr<")) continue;

            AssetTypeValueField valueField = atvIterator.ReadValueField();
            childPPtrs.Add(valueField);
        }

        return new(internalPPtrs, externalPPtrs);
    }

    /// <summary>
    /// Enumerate all pptrs that are dependencies of this asset. PPtrs within the current bundle will be followed
    /// but external pptrs will not.
    /// </summary>
    /// <param name="mgr">The AssetsManager in use.</param>
    /// <param name="afileInst">The Assets file instance.</param>
    /// <param name="assetPathId">The path ID for the asset to check.</param>
    /// <param name="followParent">If false (default), will not check the parent for a transform.</param>
    public static ChildPPtrs FindBundleDependentObjects(
        this AssetsManager mgr,
        AssetsFileInstance afileInst,
        long assetPathId,
        bool followParent = false
        )
    {
        HashSet<long> internalSeen = new([assetPathId]);
        HashSet<PPtrData> externalSeen = [];

        Queue<long> toProcess = new();
        toProcess.Enqueue(assetPathId);

        // Aquire the lock for the whole procedure
        lock (afileInst.LockReader)
        {
            while (toProcess.TryDequeue(out long current))
            {
                ChildPPtrs childPptrs = mgr.FindPPtrNodes(afileInst, current, followParent);

                externalSeen.UnionWith(childPptrs.ExternalPaths);

                foreach (long pathId in childPptrs.InternalPaths)
                {
                    if (internalSeen.Add(pathId))
                    {
                        toProcess.Enqueue(pathId);
                    }
                }
            }
        }

        internalSeen.Remove(assetPathId);

        return new(internalSeen, externalSeen);
    }
}
