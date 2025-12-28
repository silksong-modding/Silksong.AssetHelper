using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx.Logging;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
            string outPath = Path.Combine(AssetPaths.AssemblyFolder, "repacked_hpa.bundle");
            // CreateAssetSceneBundle(sceneBunPath, ["Heart Piece"], null, outPath);
        }

        Stopwatch sw = Stopwatch.StartNew();
        Log.LogInfo($"Starting [{sw.ElapsedMilliseconds} ms]");

        AssetsManager mgr = new();
        string bunPath = Path.Combine(AssetPaths.BundleFolder, "scenes_scenes_scenes", "memory_coral_tower.bundle");
        BundleFileInstance sceneBun = mgr.LoadBundleFile(bunPath);
        if (!TryFindAssetsFiles(mgr, sceneBun, out AssetsFileInstance? mainSceneAfileInst, out AssetsFileInstance? _))
        {
            throw new NotSupportedException($"Could not find assets files for {bunPath}");
        }

        sw.Stop();
        Log.LogInfo($"Found file [{sw.ElapsedMilliseconds} ms]");

        foreach (string goName in new string[]
        {
            // "Battle Scenes",
            // "Boss Scene",
            // "Stalactite Group",
            // "Enemy Activator Groups",
            "Battle Scenes/Battle Scene Chamber 2/Wave 9/Coral Conch Driller",
            "Enemy Activator Groups/Enemy Activator Low/Enemy Folder/Coral Goomba M (2)",
            "Enemy Activator Groups/Enemy Activator Low/Enemy Folder/Coral Goomba L",
            "Battle Scenes/Battle Scene Chamber 3/Wave 5 - fish1/Coral Swimmer Fat (1)",
            "Battle Scenes/Battle Scene Chamber 3/Wave 5 - fish1/Coral Poke Swimmer",
            "Battle Scenes/Battle Scene Chamber 2/Wave 7b - Fish/Coral Spike Swimmer (1)",
            "Battle Scenes/Battle Scene Chamber 2/Wave 2/Coral Warrior (1)",
            "Battle Scenes/Battle Scene Chamber 4/Wave 3/Coral Bubble Brute",
            "Battle Scenes/Battle Scene Chamber 2/Wave 10/Coral Brawler (1)",
            "Battle Scenes/Battle Scene Chamber 2/Wave 1/Coral Hunter",
            "Enemy Activator Groups/Enemy Activator Low/Enemy Folder/Coral Swimmer Small",
            "Battle Scenes/Battle Scene Chamber 3/Wave 15b - double jellyfish/Coral Big Jellyfish",
            "Battle Scenes/Battle Scene Chamber 1/Wave 5/Coral Flyer",
            "Battle Scenes/Battle Scene Chamber 3/Wave 2b/Coral Flyer Throw",
            "Boss Scene/Roar Spikes/Spike Holder 1/Coral Spike"
        })
        {
            long pathId = mgr.FindTransform(mainSceneAfileInst, goName).ValueField["m_GameObject.m_PathID"].AsLong;

            Log.LogInfo($"Starting {goName} at pathId {pathId}");

            BundleUtils.ChildPPtrs kidsFalse = null;
            List<(int fileId, long PathId)> ep = null;

            if (true)
            {
                sw = Stopwatch.StartNew();
                kidsFalse = mgr.FindBundleDependentObjects(mainSceneAfileInst, pathId, followParent: false);
                sw.Stop();
                Log.LogInfo($"KidsFalse: {sw.ElapsedMilliseconds} ms; {kidsFalse.InternalPaths.Count} + {kidsFalse.ExternalPaths.Count}");

            }

            if (false)
            {
                sw = Stopwatch.StartNew();
                var kidsTrue = mgr.FindBundleDependentObjects(mainSceneAfileInst, pathId, followParent: true);
                sw.Stop();
                Log.LogInfo($"KidsTrue: {sw.ElapsedMilliseconds} ms; {kidsTrue.InternalPaths.Count} + {kidsTrue.ExternalPaths.Count}");
            }

            if (false)
            {
                sw = Stopwatch.StartNew();
                Deps.FindDirectDependentObjects(mgr, mainSceneAfileInst, pathId, out var ip, out ep);
                sw.Stop();
                Log.LogInfo($"OG: {sw.ElapsedMilliseconds} ms; {ip.Count} + {ep.Count}");
            }
        }
    }

    private static void TheOtherThing()
    { 
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
    public static void CreateAssetSceneBundle(
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

        // Update the metadata

        // Update externals on the new bundle
        modAfile.Metadata.Externals.Clear();

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

        // Copy over the type tree from the scene bundle... we need to hang on to the type tree entry for the internal asset bundle though
        TypeTreeType internalBundleTypeTree = modAfile.Metadata.TypeTreeTypes.First(x => x.TypeId == (int)AssetClassID.AssetBundle);
        modAfile.Metadata.TypeTreeTypes.Clear();
        modAfile.Metadata.TypeTreeTypes.AddRange(sceneAfile.Metadata.TypeTreeTypes);
        modAfile.Metadata.TypeTreeTypes.Add(internalBundleTypeTree);
        modAfile.Metadata.ScriptTypes.Clear();
        modAfile.Metadata.ScriptTypes.AddRange(sceneAfile.Metadata.ScriptTypes);
        modAfile.Metadata.RefTypes.Clear();
        modAfile.Metadata.RefTypes.AddRange(sceneAfile.Metadata.RefTypes);  // What is this for?

        Log.LogInfo($"Script types count: {modAfile.Metadata.ScriptTypes.Count}");
        Log.LogInfo($"Type tree count: {modAfile.Metadata.TypeTreeTypes.Count}");

        // Remove asset infos other than the bundle
        foreach (AssetFileInfo afi in modAfile.AssetInfos.Where(info => info.TypeId != (int)AssetClassID.AssetBundle).ToList())
        {
            modAfile.Metadata.RemoveAssetInfo(afi);
        }

        // Collect pptrs for objects we want to copy
        HashSet<long> objsToCopy = [];
        foreach ((AssetFileInfo asset, string name) in gameObjects)
        {
            objsToCopy.Add(asset.PathId);
            Deps.FindDirectDependentObjects(mgr, mainSceneAfileInst, asset.PathId, out List<long> internalPaths, out _);
            foreach (long ip in internalPaths)
            {
                objsToCopy.Add(ip);
            }
        }
        // Copy over objects
        long newOne = -1;
        while (objsToCopy.Contains(newOne))
        {
            newOne--;
        }

        // TODO - update any pptrs from 1 to newOne
        // There are none in the mask shard bundle so I don't have to do it yet :zoteSip:
        foreach (long p in objsToCopy)
        {
            AssetFileInfo oldInfo = sceneAfile.GetAssetInfo(p);
            long pathId = p == 1 ? newOne : p;
            AssetFileInfo newInfo = AssetFileInfo.Create(modAfile, pathId, oldInfo.TypeId, oldInfo.GetScriptIndex(modAfile));

            long offset = oldInfo.GetAbsoluteByteOffset(sceneAfile);
            uint size = oldInfo.ByteSize;

            sceneAfile.Reader.Position = offset;
            byte[] data = sceneAfile.Reader.ReadBytes((int)size);
            newInfo.SetNewData(data);
            modAfile.Metadata.AddAssetInfo(newInfo);
        }
        // TODO - this block doesn't work
        if (objsToCopy.Contains(1))
        {
            // Copying over PPtr(pathId=1) so we need to update pointers
            foreach (long p in objsToCopy)
            {
                long pathId = p == 1 ? newOne : p;

                AssetFileInfo info = modAfile.GetAssetInfo(pathId);
                AssetTypeTemplateField templateField = mgr.GetTemplateBaseField(modAfileInst, info);
                RefTypeManager refMan = mgr.GetRefTypeManager(modAfileInst);
                lock (modAfileInst.LockReader)
                {
                    long assetPos = info.GetAbsoluteByteOffset(modAfile);
                    AssetTypeValueIterator atvIterator = new(templateField, modAfile.Reader, assetPos, refMan);

                    while (atvIterator.ReadNext())
                    {
                        string typeName = atvIterator.TempField.Type;

                        if (!typeName.StartsWith("PPtr<")) continue;

                        AssetTypeValueField valueField = atvIterator.ReadValueField();
                        int fileID = valueField["m_FileID"].AsInt;
                        long pathID = valueField["m_PathID"].AsLong;

                        if (pathID == 0 && pathID == 1)
                        {
                            valueField["m_PathID"].AsLong = newOne;
                        }
                    }
                }
            }
        }

        // Update the internal bundle
        // Update the name
        AssetFileInfo internalBundle = modAfile.AssetInfos.Where(info => info.TypeId == (int)AssetClassID.AssetBundle).First();
        AssetTypeValueField bundleData = mgr.GetBaseField(modAfileInst, internalBundle);
        bundleData["m_Name"].AsString = newBundleName;
        bundleData["m_AssetBundleName"].AsString = newBundleName;

        // Update the type id
        internalBundle.TypeIdOrIndex = modAfile.Metadata.TypeTreeTypes.Count - 1;

        // Update the dependencies TODO
        List<AssetTypeValueField> newDeps = [];
        foreach (AssetsFileExternal extcab in sharedAssetsAfile.Metadata.Externals)
        {
            string cab = extcab.OriginalPathName.Split('/')[^1].ToLowerInvariant();
            if (cab.StartsWith("cab-") && !cab.Contains('.'))
            {
                AssetTypeValueField newChildString = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_Dependencies.Array"]);
                newChildString.AsString = cab;
                newDeps.Add(newChildString);
            }
        }
        bundleData["m_Dependencies.Array"].Children.Clear();
        bundleData["m_Dependencies.Array"].Children.AddRange(newDeps);

        // Fix up the preload table - TODO nuuvestigate further
        List<AssetTypeValueField> preloadPtrs = [];
        List<(int start, int count)> depRanges = [];

        foreach ((AssetFileInfo asset, string _) in gameObjects)
        {
            int start = preloadPtrs.Count;
            Deps.FindDirectDependentObjects(mgr, mainSceneAfileInst, asset.PathId, out _, out var externalPaths);

            foreach ((int fileId, long pathId) in externalPaths)
            {
                AssetTypeValueField newPtr = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_PreloadTable.Array"]);
                newPtr["m_FileID"].AsInt = fileId;
                newPtr["m_PathID"].AsLong = pathId;
                preloadPtrs.Add(newPtr);
            }

            depRanges.Add((start, preloadPtrs.Count - start));
        }

        bundleData["m_PreloadTable.Array"].Children.Clear();
        bundleData["m_PreloadTable.Array"].Children.AddRange(preloadPtrs);

        // Add new assets to the container
        AssetTypeValueField assetPtr = bundleData["m_Container.Array"][0];

        List<AssetTypeValueField> newChildren = [];

        foreach ((AssetFileInfo asset, string name) in gameObjects)
        {
            AssetTypeValueField newChild = ValueBuilder.DefaultValueFieldFromArrayTemplate(bundleData["m_Container.Array"]);
            newChild["first"].AsString = $"{nameof(AssetHelper)}/{name}.prefab";
            newChild["second.preloadIndex"].AsInt = 0;
            newChild["second.preloadSize"].AsInt = preloadPtrs.Count;  // TODO
            newChild["second.asset.m_FileID"].AsInt = 0;
            newChild["second.asset.m_PathID"].AsLong = asset.PathId == 1 ? newOne : asset.PathId;
            newChildren.Add(newChild);
        }

        bundleData["m_Container.Array"].Children.Clear();
        bundleData["m_Container.Array"].Children.AddRange(newChildren);

        // Finish up
        internalBundle.SetNewData(bundleData);

        modBunF.BlockAndDirInfo.DirectoryInfos[0].SetNewData(modAfile);
        modBunF.BlockAndDirInfo.DirectoryInfos[0].Name = newCabName;

        using (AssetsFileWriter writer = new(outBundlePath))
        {
            modBunF.Write(writer);
        }
    }

    private static int GetExtIndex(AssetsFile afile, string origPathName)
    {
        for (int i = 0; i < afile.Metadata.Externals.Count; i++)
        {
            if (afile.Metadata.Externals[i].OriginalPathName == origPathName)
            {
                return i;
            }
        }

        throw new IndexOutOfRangeException($"Could not find string {origPathName}");
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
