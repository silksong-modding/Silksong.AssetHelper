using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using GameObjectInfo = Silksong.AssetHelper.BundleTools.GameObjectLookup.GameObjectInfo;

namespace Silksong.AssetHelper.BundleTools.Repacking;

/// <summary>
/// Class that repacks scenes by taking a minimal set of objects in the scene that allow
/// all provided game objects to be loaded.
/// 
/// Any game objects whose parents are not needed will be deparented.
/// </summary>
public class StrippedSceneRepacker : SceneRepacker
{
    /// <inheritdoc />
    public override RepackedBundleData Repack(string sceneBundlePath, List<string> objectNames, string outBundlePath)
    {
        objectNames = objectNames.GetHighestNodes();

        RepackedBundleData outData = new();
        AssetsManager mgr = BundleUtils.CreateDefaultManager();

        GetDefaultBundleNames(sceneBundlePath, objectNames, outBundlePath, out string newCabName, out string newBundleName);
        outData.BundleName = newBundleName;
        outData.CabName = newCabName;

        BundleFileInstance sceneBun = mgr.LoadBundleFile(sceneBundlePath);
        if (!mgr.TryFindAssetsFiles(sceneBun, out BundleUtils.SceneBundleInfo sceneBundleInfo))
        {
            throw new NotSupportedException($"Could not find assets files for {sceneBundlePath}");
        }

        AssetsFileInstance mainSceneAfileInst = mgr.LoadAssetsFileFromBundle(sceneBun, sceneBundleInfo.mainAfileInstIndex);
        AssetsFileInstance sceneSharedAssetsFileInst = mgr.LoadAssetsFileFromBundle(sceneBun, sceneBundleInfo.sharedAssetsAfileIndex);
        int mainAfileIdx = sceneBundleInfo.mainAfileInstIndex;

        GameObjectLookup goLookup = GameObjectLookup.CreateFromFile(mgr, mainSceneAfileInst);

        AssetDependencies dependencies = new(mgr, mainSceneAfileInst);
        HashSet<long> includedPathIds = [];

        foreach (string objName in objectNames)
        {
            if (goLookup.TryLookupName(objName, out GameObjectInfo? info))
            {
                includedPathIds.Add(info.GameObjectPathId);
                includedPathIds.UnionWith(dependencies.FindBundleDeps(info.GameObjectPathId).InternalPaths);
            }
            else
            {
                AssetHelperPlugin.InstanceLogger.LogError($"Couldn't find game object {objName}");
            }
        }

        // Collect all game objects that are being included
        List<string> includedGos = [];
        foreach (long pathId in includedPathIds)
        {
            if (goLookup.TryLookupGameObject(pathId, out GameObjectInfo? info))
            {
                includedGos.Add(info.GameObjectName);
            }
        }
        List<string> rootmostGos = includedGos.GetHighestNodes();

        // Generate a path for each rootmost go which has a child in the request
        HashSet<string> includedContainerGos = [];
        foreach (string objName in objectNames)
        {
            if (ObjPathUtil.TryFindAncestor(rootmostGos, objName, out string? ancestor, out _))
            {
                includedContainerGos.Add(ancestor);
            }
            else
            {
                AssetHelperPlugin.InstanceLogger.LogWarning($"Did not find {objName} in bundle");
            }
        }

        // Strip all assets that are not needed
        foreach (AssetFileInfo afileInfo in mainSceneAfileInst.file.AssetInfos.ToList())
        {
            if (!includedPathIds.Contains(afileInfo.PathId))
            {
                mainSceneAfileInst.file.Metadata.RemoveAssetInfo(afileInfo);
            }
        }

        // Determine the new path ID for the asset at path=1
        long newOneAssetPathId = 1;

        if (includedPathIds.Contains(1))
        {
            newOneAssetPathId = -1;
            while (includedPathIds.Contains(newOneAssetPathId))
            {
                newOneAssetPathId--;
            }            
        }

        long updatedPathId(long orig) => orig == 1 ? newOneAssetPathId : orig;

        // Deparent transforms which are now rooted
        foreach (GameObjectInfo current in goLookup)
        {
            if (!includedPathIds.Contains(current.TransformPathId))
            {
                continue;
            }

            if (!current.GameObjectName.TryGetParent(out string parentName))
            {
                // No need to deparent what is already a root go
                continue;
            }

            if (!goLookup.TryLookupName(parentName, out GameObjectInfo? parentInfo))
            {
                AssetHelperPlugin.InstanceLogger.LogWarning($"Unexpectedly failed to find {parentName} from {current.GameObjectName}");
                continue;
            }

            if (includedPathIds.Contains(parentInfo.TransformPathId))
            {
                continue;
            }

            // We now have to deparent the object
            AssetFileInfo afInfo = mainSceneAfileInst.file.GetAssetInfo(current.TransformPathId);
            AssetTypeValueField transformField = mgr.GetBaseField(mainSceneAfileInst, afInfo);
            transformField["m_Father.m_PathID"].AsLong = 0;
            afInfo.SetNewData(transformField);
        }

        // Set up the internal bundle
        AssetFileInfo internalBundle = sceneSharedAssetsFileInst.file.GetAssetsOfType(AssetClassID.AssetBundle).First();
        AssetTypeValueField iBundleData = mgr.GetBaseField(sceneSharedAssetsFileInst, internalBundle);

        // Set simple data
        iBundleData["m_Name"].AsString = newBundleName;
        iBundleData["m_AssetBundleName"].AsString = newBundleName;
        iBundleData["m_IsStreamedSceneAssetBundle"].AsBool = false;
        iBundleData["m_SceneHashes.Array"].Children.Clear();

        // Add objects to the container
        List<AssetTypeValueField> preloadPtrs = [];
        List<AssetTypeValueField> newChildren = [];

        foreach (string containerGo in includedContainerGos)
        {
            GameObjectInfo cgInfo = goLookup.LookupName(containerGo);
            AssetDependencies.ChildPPtrs deps = dependencies.FindBundleDeps(cgInfo.GameObjectPathId);

            int start = preloadPtrs.Count;

            foreach ((int fileId, long pathId) in deps.ExternalPaths)
            {
                AssetTypeValueField depPtr = ValueBuilder.DefaultValueFieldFromArrayTemplate(iBundleData["m_PreloadTable.Array"]);
                depPtr["m_FileID"].AsInt = fileId;
                depPtr["m_PathID"].AsLong = pathId;
                preloadPtrs.Add(depPtr);
            }

            int count = preloadPtrs.Count - start;

            string containerPath = $"{nameof(AssetHelper)}/{containerGo}.prefab";

            AssetTypeValueField newChild = ValueBuilder.DefaultValueFieldFromArrayTemplate(iBundleData["m_Container.Array"]);
            newChild["first"].AsString = containerPath;
            newChild["second.preloadIndex"].AsInt = start;
            newChild["second.preloadSize"].AsInt = count;
            newChild["second.asset.m_FileID"].AsInt = 0;
            newChild["second.asset.m_PathID"].AsLong = updatedPathId(cgInfo.GameObjectPathId);
            newChildren.Add(newChild);
        }

        iBundleData["m_PreloadTable.Array"].Children.Clear();
        iBundleData["m_PreloadTable.Array"].Children.AddRange(preloadPtrs);
        iBundleData["m_Container.Array"].Children.Clear();
        iBundleData["m_Container.Array"].Children.AddRange(newChildren);
        outData.GameObjectAssets = includedContainerGos.ToList();

        // Move the asset at pathId = 1 to newOneAssetPathId
        if (newOneAssetPathId != 1)
        {
            int redirectCount = 0;

            AssetFileInfo toMove = mainSceneAfileInst.file.GetAssetInfo(1);
            mainSceneAfileInst.file.Metadata.RemoveAssetInfo(toMove);
            toMove.PathId = newOneAssetPathId;
            mainSceneAfileInst.file.Metadata.AddAssetInfo(toMove);

            foreach (long pathId in includedPathIds)
            {
                if (pathId == 1) continue;

                if (!dependencies.FindImmediateDeps(pathId).InternalPaths.Contains(1))
                {
                    continue;
                }

                redirectCount += mgr.Redirect(mainSceneAfileInst, mainSceneAfileInst.file.GetAssetInfo(pathId), 1, newOneAssetPathId);
            }

            int locRedirect = mgr.Redirect(mainSceneAfileInst, toMove, 1, newOneAssetPathId);  // Just in case
            AssetHelperPlugin.InstanceLogger.LogInfo($"Redirected {redirectCount} references plus {locRedirect} self-references");
        }

        // Move updated internal bundle into the main assets file
        // Copy the asset bundle type tree from the shared assets to the main bundle
        if (!mainSceneAfileInst.file.Metadata.TypeTreeTypes.Any(x => x.TypeId == (int)AssetClassID.AssetBundle))
        {
            TypeTreeType t = sceneSharedAssetsFileInst.file.Metadata.TypeTreeTypes.First(x => x.TypeId == (int)AssetClassID.AssetBundle);
            mainSceneAfileInst.file.Metadata.TypeTreeTypes.Add(t);
        }

        AssetFileInfo newInternalBundle = AssetFileInfo.Create(mainSceneAfileInst.file, mainAfileIdx, (int)AssetClassID.AssetBundle);
        newInternalBundle.SetNewData(iBundleData);
        mainSceneAfileInst.file.Metadata.AddAssetInfo(newInternalBundle);

        sceneBun.file.BlockAndDirInfo.DirectoryInfos[mainAfileIdx].SetNewData(mainSceneAfileInst.file);
        sceneBun.file.BlockAndDirInfo.DirectoryInfos[mainAfileIdx].Name = newCabName;

        int tot = sceneBun.file.BlockAndDirInfo.DirectoryInfos.Count;
        for (int i = 0; i < tot; i++)
        {
            if (i == mainAfileIdx) { continue; }
            sceneBun.file.BlockAndDirInfo.DirectoryInfos[i].SetRemoved();
        }

        using (AssetsFileWriter writer = new(outBundlePath))
        {
            sceneBun.file.Write(writer);
        }

        return outData;
    }
}
