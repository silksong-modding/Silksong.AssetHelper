using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    /// Enumerate all transforms in the given file.
    /// </summary>
    public static IEnumerable<AssetFileInfo> GetAllTransforms(this AssetsFile afile)
    {
        foreach (AssetClassID cid in TransformClassIds)
        {
            foreach (AssetFileInfo info in afile.GetAssetsOfType(cid))
            {
                yield return info;
            }
        }
    }

    /// <summary>
    /// Enumerate transforms in this bundle, only including transforms with no parent.
    /// </summary>
    public static IEnumerable<AssetData> GetRootTransforms(this AssetsManager mgr, AssetsFileInstance afileInst)
    {
        foreach (AssetFileInfo info in afileInst.file.GetAllTransforms())
        {
            AssetTypeValueField transform = mgr.GetBaseField(afileInst, info);
            AssetTypeValueField parent = transform["m_Father"];

            if (parent["m_PathID"].AsLong == 0)
            {
                yield return new(info, transform);
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
    /// Info about the assets files in a scene bundle.
    /// </summary>
    /// <param name="mainAfileInstIndex">The index of the main assets file.</param>
    /// <param name="sharedAssetsAfileIndex">The index of the shared assets file.</param>
    public record SceneBundleInfo(
        int mainAfileInstIndex,
        int sharedAssetsAfileIndex);

    /// <summary>
    /// Given a scene bundle instance, find the main assets file and the sharedAssets file
    /// within the bundle.
    /// </summary>
    public static bool TryFindAssetsFiles(
        this AssetsManager mgr,
        BundleFileInstance sceneBun,
        out SceneBundleInfo info)
    {
        int mainAfileIdx = -1;
        int sharedAssetsAfileIdx = -1;

        List<string> names = sceneBun.file.GetAllFileNames();
        for (int i = 0; i < names.Count; i++)
        {
            if (!names[i].Contains('.'))
            {
                mainAfileIdx = i;
            }
            else if (names[i].EndsWith(".sharedAssets"))
            {
                sharedAssetsAfileIdx = i;
            }
        }

        info = new(mainAfileIdx, sharedAssetsAfileIdx);

        if (mainAfileIdx == -1 || sharedAssetsAfileIdx == -1)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Create an <see cref="AssetTypeValueIterator"></see> for the current asset file info.
    /// 
    /// This should only be done while the <see cref="AssetsFileInstance.LockReader"/> of the afileinst is held.
    /// </summary>
    public static AssetTypeValueIterator CreateIterator(this AssetsManager mgr, AssetsFileInstance afileinst, AssetFileInfo info)
    {
        AssetTypeTemplateField templateField = mgr.GetTemplateBaseField(afileinst, info);

        RefTypeManager refMan = mgr.GetRefTypeManager(afileinst);

        long assetPos = info.GetAbsoluteByteOffset(afileinst.file);
        AssetTypeValueIterator atvIterator = new(templateField, afileinst.file.Reader, assetPos, refMan);

        return atvIterator;
    }

    /// <summary>
    /// Redirect any references from the current assetfileinfo that point to source within the current bundle
    /// to instead point to target.
    /// 
    /// This should be run for each asset that referenced the asset at pathID = source if it is being moved to pathID = target.
    /// </summary>
    public static int Redirect(this AssetsManager mgr, AssetsFileInstance afileinst, AssetFileInfo info, long source, long target)
    {
        int replaceCount = 0;

        lock (afileinst.LockReader)
        {
            byte[] globalAssetData = mgr.GetBaseField(afileinst, info).WriteToByteArray();
            AssetTypeValueIterator atvIterator = mgr.CreateIterator(afileinst, info);

            while (atvIterator.ReadNext())
            {
                string typeName = atvIterator.TempField.Type;

                if (!typeName.StartsWith("PPtr<")) continue;

                AssetTypeValueField valueField = atvIterator.ReadValueField();

                if (valueField["m_PathID"].AsLong != source || valueField["m_FileID"].AsInt != 0)
                {
                    continue;                    
                }

                valueField["m_PathID"].AsLong = target;
                byte[] newData = valueField.WriteToByteArray();

                int assetStart = atvIterator.ReadPosition - newData.Length;
                Array.Copy(newData, 0, globalAssetData, assetStart, newData.Length);

                replaceCount++;
            }

            info.SetNewData(globalAssetData);
        }

        return replaceCount;
    }

    /// <summary>
    /// Write the given asset bundle to the given file.
    /// </summary>
    public static void WriteBundleToFile(this AssetBundleFile bunFile, string outBundlePath)
    {
        // Going via memory stream and performing a single large write is a lot more
        // efficient for certain systems than writing directly.

        using MemoryStream ms = new();
        using AssetsFileWriter writer = new(ms);
        bunFile.Write(writer);

        using FileStream fileStream = new(outBundlePath, FileMode.Create, FileAccess.Write);
        byte[] internalBuffer = ms.GetBuffer();
        fileStream.Write(internalBuffer, 0, (int)ms.Length);
    }

    /// <summary>
    /// Enumerate all assets in the container of a given bundle.
    /// 
    /// The bundle should be a standard non-scene bundle with the internal asset bundle at index 1.
    /// </summary>
    public static IEnumerable<(string name, int assetTypeId)> EnumerateContainer(this AssetsManager mgr, AssetsFileInstance afi)
    {
        AssetTypeValueField iBundle = mgr.GetBaseField(afi, 1);

        foreach (AssetTypeValueField ctrEntry in iBundle["m_Container.Array"].Children)
        {
            string name = ctrEntry["first"].AsString;
            long pathId = ctrEntry["second.asset.m_PathID"].AsLong;
            int assetType = afi.file.GetAssetInfo(pathId).TypeId;
            yield return (name, assetType);
        }
    }
}
