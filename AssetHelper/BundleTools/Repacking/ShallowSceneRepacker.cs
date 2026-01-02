using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Silksong.AssetHelper.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Silksong.AssetHelper.BundleTools.Repacking;

/// <summary>
/// Repacker that creates a shallow bundle; that is, a bundle with no objects but with the metadata to load
/// objects from a given scene bundle, as long as that scene bundle and its dependencies are in memory.
/// </summary>
public class ShallowSceneRepacker : SceneRepacker
{
    private readonly string? _nonSceneBundlePath;

    /// <summary>
    /// Instantiate a ShallowSceneRepacker.
    /// </summary>
    /// <param name="nonSceneBundlePath">A path to a non scene bundle to be used as a template.
    /// The content of this bundle does not matter.
    /// If null, a sensible default for silksong will be selected.</param>
    public ShallowSceneRepacker(string nonSceneBundlePath)
    {
        _nonSceneBundlePath = nonSceneBundlePath;
    }

    /// <summary>
    /// Instantiate a ShallowSceneRepacker.
    /// </summary>
    public ShallowSceneRepacker() : this(Path.Combine(AssetPaths.BundleFolder, "toolui_assets_all.bundle")) { }
    // This is a sensible default for silksong but it would be nice to avoid the need to hardcode...


    /// <inheritdoc />
    public override void Repack(string sceneBundlePath, List<string> objectNames, string outBundlePath, ref RepackedBundleData outData)
    {
        // Only support root objects
        List<string> rootObjects = objectNames.Select(x => x.Split('/')[0]).Distinct().ToList();

        AssetsManager mgr = BundleUtils.CreateDefaultManager();

        // Load the scene bundle
        MemoryStream ms = new(File.ReadAllBytes(sceneBundlePath));
        BundleFileInstance sceneBun = mgr.LoadBundleFile(ms, sceneBundlePath);

        if (!mgr.TryFindAssetsFiles(sceneBun, out BundleUtils.SceneBundleInfo sceneBundleInfo))
        {
            throw new NotSupportedException($"Could not find assets files for {sceneBundlePath}");
        }

        AssetsFileInstance mainSceneAfileInst = mgr.LoadAssetsFileFromBundle(sceneBun, sceneBundleInfo.mainAfileInstIndex);
        AssetsFileInstance sceneSharedAssetsFileInst = mgr.LoadAssetsFileFromBundle(sceneBun, sceneBundleInfo.sharedAssetsAfileIndex);

        AssetsFile sceneAfile = mainSceneAfileInst.file;
        AssetsFile sharedAssetsAfile = sceneSharedAssetsFileInst.file;
        string sceneCab = mainSceneAfileInst.name;

        Dictionary<string, BundleUtils.AssetData> gameObjects = mgr.FindRootGameObjects(
            mainSceneAfileInst, rootObjects, out List<string> missingObjects);

        if (missingObjects.Count > 0)
        {
            AssetHelperPlugin.InstanceLogger.LogWarning($"Missing objects for bundle {sceneBundlePath}");
            AssetHelperPlugin.InstanceLogger.LogWarning(string.Join(", ", missingObjects));
        }

        List<string> nonRepackedAssets = [];
        foreach (string objName in objectNames)
        {
            if (missingObjects.Any(x => objName.HasPrefix(x)))
            {
                nonRepackedAssets.Add(objName);
            }
        }
        outData.NonRepackedAssets = nonRepackedAssets;

        // Load a non-scene bundle to modify
        MemoryStream nsms = new(File.ReadAllBytes(_nonSceneBundlePath));
        BundleFileInstance modBun = mgr.LoadBundleFile(nsms, _nonSceneBundlePath);
        AssetBundleFile modBunF = modBun.file;
        AssetsFileInstance modAfileInst = mgr.LoadAssetsFileFromBundle(modBun, 0, false);  // TODO - check index
        AssetsFile modAfile = modAfileInst.file;

        // Update externals on the new bundle
        modAfile.Metadata.Externals.Clear();
        modAfile.Metadata.Externals.Add(new()
        {
            VirtualAssetPathName = "",
            Guid = new() { data0 = 0, data1 = 0, data2 = 0, data3 = 0 },
            Type = AssetsFileExternalType.Normal,
            PathName = $"archive:/{sceneCab}/{sceneCab}",
            OriginalPathName = $"archive:/{sceneCab}/{sceneCab}",
        });

        foreach (AssetsFileExternal extcab in sceneAfile.Metadata.Externals)
        {
            modAfile.Metadata.Externals.Add(new()
            {
                VirtualAssetPathName = "",
                Guid = new() { data0 = 0, data1 = 0, data2 = 0, data3 = 0 },
                Type = AssetsFileExternalType.Normal,
                PathName = extcab.PathName,
                OriginalPathName = extcab.OriginalPathName,
            });
        }

        // Remove asset infos other than the bundle
        foreach (AssetFileInfo afi in modAfile.AssetInfos.Where(info => info.TypeId != (int)AssetClassID.AssetBundle).ToList())
        {
            modAfile.Metadata.RemoveAssetInfo(afi);
        }

        // Update the internal bundle
        // Update the name
        AssetFileInfo internalBundle = modAfile.AssetInfos.Where(info => info.TypeId == (int)AssetClassID.AssetBundle).First();
        AssetTypeValueField bundleData = mgr.GetBaseField(modAfileInst, internalBundle);
        bundleData["m_Name"].AsString = outData.BundleName;
        bundleData["m_AssetBundleName"].AsString = outData.BundleName;

        // Update the dependencies on the internal bundle
        AssetTypeValueField childString = bundleData["m_Dependencies.Array"].Children[0];
        childString.AsString = sceneCab.ToLowerInvariant();
        bundleData["m_Dependencies.Array"].Children.Clear();
        bundleData["m_Dependencies.Array"].Children.Add(childString);
        foreach (AssetsFileExternal extcab in sharedAssetsAfile.Metadata.Externals)
        {
            string cab = extcab.OriginalPathName.Split('/')[^1].ToLowerInvariant();
            if (cab.StartsWith("cab-") && !cab.Contains('.'))
            {
                AssetTypeValueField newChildString = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_Dependencies.Array"]);
                newChildString.AsString = cab;
                bundleData["m_Dependencies.Array"].Children.Add(newChildString);
            }
        }

        // Add assets to container, and fix the preload table
        List<AssetTypeValueField> preloadPtrs = [];
        Dictionary<string, string> containerPaths = [];
        List<AssetTypeValueField> newChildren = [];

        AssetDependencies dependencies = new(mgr, mainSceneAfileInst);

        foreach (string objName in gameObjects.Keys)
        {
            BundleUtils.AssetData goData = gameObjects[objName];

            int start = preloadPtrs.Count;

            // Collect dependent pptrs
            AssetDependencies.ChildPPtrs childPPtrs = dependencies.FindBundleDeps(goData.Info.PathId);
            foreach ((int fileId, long pathId) in childPPtrs.ExternalPaths)
            {
                AssetTypeValueField depPtr = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_PreloadTable.Array"]);
                depPtr["m_FileID"].AsInt = 1 + fileId;
                depPtr["m_PathID"].AsLong = pathId;
                preloadPtrs.Add(depPtr);
            }

            // Add a dependency on the current asset
            AssetTypeValueField goPtr = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_PreloadTable.Array"]);
            goPtr["m_FileID"].AsInt = 1;
            goPtr["m_PathID"].AsLong = goData.Info.PathId;
            preloadPtrs.Add(goPtr);

            int count = preloadPtrs.Count - start;

            string containerPath = $"{nameof(AssetHelper)}/{objName}.prefab";
            containerPaths.Add(containerPath, objName);

            AssetTypeValueField newChild = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_Container.Array"]);
            newChild["first"].AsString = containerPath;
            newChild["second.preloadIndex"].AsInt = start;
            newChild["second.preloadSize"].AsInt = count;
            newChild["second.asset.m_FileID"].AsInt = 1;
            newChild["second.asset.m_PathID"].AsLong = goData.Info.PathId;
            newChildren.Add(newChild);
        }

        bundleData["m_PreloadTable.Array"].Children.Clear();
        bundleData["m_PreloadTable.Array"].Children.AddRange(preloadPtrs);
        bundleData["m_Container.Array"].Children.Clear();
        bundleData["m_Container.Array"].Children.AddRange(newChildren);
        outData.GameObjectAssets = containerPaths;

        // Finish up
        internalBundle.SetNewData(bundleData);

        modBunF.BlockAndDirInfo.DirectoryInfos[0].SetNewData(modAfile);
        modBunF.BlockAndDirInfo.DirectoryInfos[0].Name = outData.CabName;

        modBunF.WriteBundleToFile(outBundlePath);
        mgr.UnloadAll();
    }
}
