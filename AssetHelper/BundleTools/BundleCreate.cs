using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Tools for creating repacked asset bundles.
/// </summary>
public static class BundleCreate
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"{nameof(AssetHelper)}.{nameof(BundleCreate)}");

    /// <summary>
    /// Given a scene bundle instance, find the main assets file and the sharedAssets file
    /// within the bundle.
    /// </summary>
    private static bool TryFindAssetsFiles(
        AssetsManager mgr,
        BundleFileInstance sceneBun, 
        [MaybeNullWhen(false)] out AssetsFileInstance mainAfileInst,
        [MaybeNullWhen(false)] out AssetsFileInstance sharedAssetsFileInst)
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

        if (mainAfileIdx == -1 || sharedAssetsAfileIdx == -1)
        {
            mainAfileInst = default;
            sharedAssetsFileInst = default;
            return false;
        }

        mainAfileInst = mgr.LoadAssetsFileFromBundle(sceneBun, mainAfileIdx, false);
        sharedAssetsFileInst = mgr.LoadAssetsFileFromBundle(sceneBun, sharedAssetsAfileIdx, false);
        return true;
    }

    /// <summary>
    /// Determine sensible cab and bundle names for the given bundle.
    /// 
    /// These don't matter, but these ones look like the ones made by unity.
    /// </summary>
    /// <param name="sceneBundlePath"></param>
    /// <param name="objectNames"></param>
    /// <param name="outBundlePath"></param>
    /// <param name="cabName"></param>
    /// <param name="bundleName"></param>
    private static void GetBundleNames(
        string sceneBundlePath,
        List<string> objectNames,
        string outBundlePath,
        out string cabName,
        out string bundleName)
    {
        const string salt = "AssetHelperSalt\n";

        using SHA256 sha256 = SHA256.Create();

        StringBuilder inputSb = new();
        inputSb.AppendLine(salt);
        inputSb.AppendLine(sceneBundlePath ?? string.Empty);

        foreach (string name in objectNames)
        {
            inputSb.AppendLine($"\n{name}");
        }

        inputSb.AppendLine(outBundlePath);

        string saltedInput = inputSb.ToString();

        byte[] inputBytes = Encoding.UTF8.GetBytes(saltedInput);
        byte[] hashBytes = sha256.ComputeHash(inputBytes);

        StringBuilder sb = new(64);
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        string fullHash = sb.ToString();

        cabName = $"CAB-{fullHash.Substring(0, 32)}";
        bundleName = $"{fullHash.Substring(32, 32)}.bundle";
    }

    /// <summary>
    /// Create a shallow bundle that can be used to spawn objects from the provided scene bundle.
    /// </summary>
    /// <param name="sceneBundlePath">A path to the scene bundle.</param>
    /// <param name="objectNames">A list of game objects to spawn. Only root game objects are supported.</param>
    /// <param name="nonSceneBundlePath">A path to a non scene bundle to be used as a template.
    /// The content of this bundle does not matter.
    /// If null, a sensible default for silksong will be selected.</param>
    /// <param name="outBundlePath">A path to the created bundle.</param>
    /// <returns>An object encapsulating information about the written bundle.</returns>
    public static RepackedBundleData CreateShallowSceneBundle(
        string sceneBundlePath,
        List<string> objectNames,
        string outBundlePath,
        string? nonSceneBundlePath = null
        )
    {
        RepackedBundleData outData = new();
        AssetsManager mgr = BundleUtils.CreateDefaultManager();

        GetBundleNames(sceneBundlePath, objectNames, outBundlePath, out string newCabName, out string newBundleName);
        outData.BundleName = newBundleName;
        outData.CabName = newCabName;

        // TODO - avoid hardcoding this. I'd like something with no aux internal files, I think...
        nonSceneBundlePath ??= Path.Combine(AssetPaths.BundleFolder, "toolui_assets_all.bundle");

        // Load the scene bundle
        BundleFileInstance sceneBun = mgr.LoadBundleFile(sceneBundlePath);
        if (!TryFindAssetsFiles(mgr, sceneBun, out AssetsFileInstance? mainSceneAfileInst, out AssetsFileInstance? sceneSharedAssetsFileInst))
        {
            throw new NotSupportedException($"Could not find assets files for {sceneBundlePath}");
        }

        AssetsFile sceneAfile = mainSceneAfileInst.file;
        AssetsFile sharedAssetsAfile = sceneSharedAssetsFileInst.file;
        string sceneCab = mainSceneAfileInst.name;

        Dictionary<string, BundleUtils.AssetData> gameObjects = mgr.FindRootGameObjects(
            mainSceneAfileInst, objectNames, out List<string> missingObjects);

        if (missingObjects.Count > 0)
        {
            Log.LogWarning($"Missing objects for bundle {sceneBundlePath}");
            Log.LogWarning(string.Join(", ", missingObjects));
        }

        // Load a non-scene bundle to modify
        BundleFileInstance modBun = mgr.LoadBundleFile(nonSceneBundlePath);
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
        bundleData["m_Name"].AsString = newBundleName;
        bundleData["m_AssetBundleName"].AsString = newBundleName;

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

        // TODO - do this and the container together
        // Fix up the preload table
        List<string> gameObjectNames = gameObjects.Keys.ToList();
        List<AssetTypeValueField> preloadPtrs = [];
        List<(int start, int count)> depCounts = new();

        foreach (string objName in gameObjectNames)
        {
            BundleUtils.AssetData goData = gameObjects[objName];

            int start = preloadPtrs.Count;

            // Collect dependent pptrs
            BundleUtils.ChildPPtrs childPPtrs = mgr.FindBundleDependentObjects(mainSceneAfileInst, goData.Info.PathId);
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

            depCounts.Add((start, count));
        }

        bundleData["m_PreloadTable.Array"].Children.Clear();
        bundleData["m_PreloadTable.Array"].Children.AddRange(preloadPtrs);

        // Add new assets to the container
        List<string> containerPaths = [];
        AssetTypeValueField assetPtr = bundleData["m_Container.Array"][0];

        List<AssetTypeValueField> newChildren = [];

        for (int i = 0; i < objectNames.Count; i++)
        {
            string objName = objectNames[i];
            BundleUtils.AssetData goData = gameObjects[objName];
            (int start, int count) = depCounts[i];

            string containerPath = $"{nameof(AssetHelper)}/{objName}.prefab";
            containerPaths.Add(containerPath);

            AssetTypeValueField newChild = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_Container.Array"]);
            newChild["first"].AsString = containerPath;
            newChild["second.preloadIndex"].AsInt = start;
            newChild["second.preloadSize"].AsInt = count;
            newChild["second.asset.m_FileID"].AsInt = 1;
            newChild["second.asset.m_PathID"].AsLong = goData.Info.PathId;
            newChildren.Add(newChild);
        }

        bundleData["m_Container.Array"].Children.Clear();
        bundleData["m_Container.Array"].Children.AddRange(newChildren);
        outData.GameObjectAssets = containerPaths;

        // Finish up
        internalBundle.SetNewData(bundleData);

        modBunF.BlockAndDirInfo.DirectoryInfos[0].SetNewData(modAfile);
        modBunF.BlockAndDirInfo.DirectoryInfos[0].Name = newCabName;

        using (AssetsFileWriter writer = new(outBundlePath))
        {
            modBunF.Write(writer);
        }

        return outData;
    }
}
