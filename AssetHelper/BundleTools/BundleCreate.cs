using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx.Logging;
using InControl.UnityDeviceProfiles;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.UIElements;

namespace Silksong.AssetHelper.BundleTools;

internal static class BundleCreate
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"{nameof(AssetHelper)}.{nameof(BundleCreate)}");

    public class TheData
    {
        public List<(int fileId, long pathId, string extCab, string extFileName)> NsbDeps = new();
        public List<(int fileId, long pathId, string extCab, string extFileName)> DeterminedDeps = new();
    }

    private static void InternalDebug()
    {

        {
            string sceneBunPath = Path.Combine(AssetPaths.BundleFolder, "scenes_scenes_scenes", "peak_04c.bundle");
            string outPath = Path.Combine(AssetPaths.AssemblyFolder, "repacked_heart_piece.bundle");
            CreateShallowSceneBundle(sceneBunPath, ["Heart Piece"], null, outPath);
        }
        {
            string sceneBunPath = Path.Combine(AssetPaths.BundleFolder, "scenes_scenes_scenes", "dust_02.bundle");
            string outPath = Path.Combine(AssetPaths.AssemblyFolder, "repacked_rfs.bundle");
            CreateShallowSceneBundle(sceneBunPath, ["Roachfeeder Short"], null, outPath);
        }


        TheData data = new();

        string bunPath = Path.Combine(AssetPaths.BundleFolder, "localpoolprefabs_assets_areadust.bundle");

        AssetsManager mgr = new();

        BundleFileInstance modBun = mgr.LoadBundleFile(bunPath);
        AssetBundleFile modBunF = modBun.file;
        AssetsFileInstance modAfileInst = mgr.LoadAssetsFileFromBundle(modBun, 0, false);
        AssetsFile modAfile = modAfileInst.file;

        AssetTypeValueField atvf = mgr.GetBaseField(modAfileInst, 1);

        for (int i = 53; i < 53 + 96; i++)
        {
            var pair = atvf["m_PreloadTable.Array"][i];
            int fileId = pair["m_FileID"].AsInt;
            long pathId = pair["m_PathID"].AsLong;
            string cab;
            string fileName;

            if (fileId == 0 || fileId > modAfile.Metadata.Externals.Count)
            {
                cab = "";
                fileName = "";
            }
            else
            {
                cab = modAfile.Metadata.Externals[fileId - 1].OriginalPathName;
                string actualCab = cab.Split("/")[^1];
                if (!Deps.CabLookup.TryGetValue(actualCab.ToLowerInvariant(), out fileName))
                {
                    fileName = "???";
                }
            }

            data.NsbDeps.Add((fileId, pathId, cab, fileName));
        }

        Deps.FindDirectDependentObjects(mgr, modAfileInst, -322844142981861729L, out var internalPaths, out var externalPaths);

        foreach (long pathId in internalPaths)
        {
            data.DeterminedDeps.Add((0, pathId, "", ""));
        }

        foreach ((int fileId, long pathId) in externalPaths)
        {
            string cab;
            string fileName;

            cab = modAfile.Metadata.Externals[fileId - 1].OriginalPathName;
            string actualCab = cab.Split("/")[^1];
            if (!Deps.CabLookup.TryGetValue(actualCab.ToLowerInvariant(), out fileName))
            {
                fileName = "???";
            }

            data.DeterminedDeps.Add((fileId, pathId, cab, fileName));
        }

        data.NsbDeps.Sort();
        data.DeterminedDeps.Sort();
        data.SerializeToFile(Path.Combine(AssetPaths.AssemblyFolder, "roachfeederDeps.json"));
    }

    public static void DoDebug()
    {
        Stopwatch sw = Stopwatch.StartNew();
        InternalDebug();
        sw.Stop();
        Log.LogInfo($"Finished debug test in {sw.ElapsedMilliseconds} ms");
    }

    public static bool TryFindAssetsFiles(
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
    /// Determine sensible cab and bundle names.
    /// 
    /// These don't matter, but these ones look like the ones made by unity.
    /// </summary>
    /// <param name="sceneBundlePath"></param>
    /// <param name="objectNames"></param>
    /// <param name="outBundlePath"></param>
    /// <param name="cabName"></param>
    /// <param name="bundleName"></param>
    public static void GetBundleNames(
        string sceneBundlePath,
        List<string> objectNames,
        string outBundlePath,
        out string cabName,
        out string bundleName)
    {
        // Define a unique salt (usually a GUID or a random string) 
        // to prevent hash collisions with other systems.
        const string salt = "AssetHelperSalt";

        using (SHA256 sha256 = SHA256.Create())
        {
            // Combine the salt and the path to create the pre-image
            string saltedInput = salt + (sceneBundlePath ?? string.Empty);
            byte[] inputBytes = Encoding.UTF8.GetBytes(saltedInput);
            byte[] hashBytes = sha256.ComputeHash(inputBytes);

            // Convert the 32-byte hash to a 64-character hex string
            StringBuilder sb = new(64);
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }

            string fullHash = sb.ToString();

            // X = first 32 chars, Y = last 32 chars
            cabName = $"CAB-{fullHash.Substring(0, 32)}";
            bundleName = $"{fullHash.Substring(32, 32)}.bundle";
        }
    }

    // TODO - remove the nonSceneBundlePath requirement
    /// <summary>
    /// Create a shallow bundle that can be used to spawn objects from the provided scene bundle.
    /// No caching is done here.
    /// </summary>
    /// <param name="sceneBundlePath">A path to the scene bundle.</param>
    /// <param name="objectNames">A list of game objects to spawn. Currently only root game objects are supported.</param>
    /// <param name="nonSceneBundlePath">A path to a non scene bundle to be used as a template.
    /// The content of this bundle does not matter.
    /// If null, a sensible default for silksong will be selected.</param>
    /// <param name="outBundlePath">A path to the created bundle.</param>
    public static void CreateShallowSceneBundle(
        string sceneBundlePath,
        List<string> objectNames,
        string? nonSceneBundlePath,
        string outBundlePath
        )
    {
        AssetsManager mgr = new();

        GetBundleNames(sceneBundlePath, objectNames, outBundlePath, out string newCabName, out string newBundleName);

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

        List<(AssetFileInfo asset, string name)> gameObjects = GetGameObjects(mgr, mainSceneAfileInst, [.. objectNames]).ToList();

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

        foreach (AssetsFileExternal extcab in sharedAssetsAfile.Metadata.Externals)
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

        // Update the dependencies
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

        // Fix up the preload table - TODO only the preloads needed by the game objects? Should nuuvestigate
        List<AssetTypeValueField> preloadPtrs = [];

        foreach ((AssetFileInfo asset, string name) in gameObjects)
        {
            AssetTypeValueField newPtr = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_PreloadTable.Array"]);
            newPtr["m_FileID"].AsInt = 1;
            newPtr["m_PathID"].AsLong = asset.PathId;
            preloadPtrs.Add(newPtr);
        }

        AssetFileInfo preloadTable = sceneSharedAssetsFileInst.file.GetAssetsOfType(AssetClassID.PreloadData).First();
        AssetTypeValueField ptField = mgr.GetBaseField(sceneSharedAssetsFileInst, preloadTable);
        foreach (AssetTypeValueField preloadAsset in ptField["m_Assets.Array"].Children)
        {
            AssetTypeValueField newPtr = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_PreloadTable.Array"]);
            newPtr["m_FileID"].AsInt = 1 + preloadAsset["m_FileID"].AsInt;
            newPtr["m_PathID"].AsLong = preloadAsset["m_PathID"].AsLong;
            preloadPtrs.Add(newPtr);
        }

        bundleData["m_PreloadTable.Array"].Children.Clear();
        bundleData["m_PreloadTable.Array"].Children.AddRange(preloadPtrs);

        // Add new assets to the container - TODO only the preloads needed by the game object?
        AssetTypeValueField assetPtr = bundleData["m_Container.Array"][0];

        List<AssetTypeValueField> newChildren = [];

        foreach ((AssetFileInfo asset, string name) in gameObjects)
        {
            AssetTypeValueField newChild = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_Container.Array"]);
            newChild["first"].AsString = $"{nameof(AssetHelper)}/{name}.prefab";
            newChild["second.preloadIndex"].AsInt = 0;
            newChild["second.preloadSize"].AsInt = preloadPtrs.Count;
            newChild["second.asset.m_FileID"].AsInt = 1;
            newChild["second.asset.m_PathID"].AsLong = asset.PathId;
            newChildren.Add(newChild);
        }

        bundleData["m_Container.Array"].Children.Clear();
        bundleData["m_Container.Array"].Children.AddRange(newChildren);

        internalBundle.SetNewData(bundleData);

        modBunF.BlockAndDirInfo.DirectoryInfos[0].SetNewData(modAfile);
        modBunF.BlockAndDirInfo.DirectoryInfos[0].Name = newCabName;

        using (AssetsFileWriter writer = new(outBundlePath))
        {
            modBunF.Write(writer);
        }
    }

    public static IEnumerable<(AssetFileInfo asset, string name)> GetGameObjects(AssetsManager mgr, AssetsFileInstance afileInst, HashSet<string> goNames)
    {
        foreach ((AssetFileInfo asset, string name) in GetRootGameObjects(mgr, afileInst))
        {
            if (goNames.Contains(name))
            {
                yield return (asset, name);
            }
        }
    }

    public static IEnumerable<(AssetFileInfo asset, string name)> GetRootGameObjects(AssetsManager mgr, AssetsFileInstance afileInst)
    {
        foreach (AssetFileInfo asset in afileInst.file.GetAssetsOfType(AssetClassID.GameObject))
        {
            // Find transform
            AssetTypeValueField assetBase = mgr.GetBaseField(afileInst, asset);
            AssetTypeValueField transformPtr = assetBase["m_Component.Array"][0]["component"];
            int fileId = transformPtr["m_FileID"].AsInt;
            long pathId = transformPtr["m_PathID"].AsLong;

            if (fileId != 0)
            {
                throw new NotSupportedException($"Nonzero fileID not supported! fileID = {fileId}");
            }

            AssetTypeValueField transform = mgr.GetBaseField(afileInst, pathId);
            AssetTypeValueField parent = transform["m_Father"];

            if (parent["m_PathID"].AsLong == 0)
            {
                string name = assetBase["m_Name"].AsString;
                yield return (asset, name);
            }
        }
    }
}
