using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Class capable of looking up the path ID of a game object, its transform and its name based on
/// one of the three.
/// </summary>
public class GameObjectLookup : IEnumerable<GameObjectLookup.GameObjectInfo>
{
    /// <summary>
    /// Record encapsulating information about a game object.
    /// </summary>
    /// <param name="GameObjectPathId">The path ID to the game object.</param>
    /// <param name="TransformPathId">The path ID to the transform.</param>
    /// <param name="GameObjectName">The name of the game object in the form root/.../grandparent/parent/object.</param>
    /// <param name="ParentPathId">The path ID of the parent transform, or 0 if it is a root game object.</param>
    public record GameObjectInfo(long GameObjectPathId, long TransformPathId, string GameObjectName, long ParentPathId);

    private readonly Dictionary<string, GameObjectInfo> _fromName;
    private readonly Dictionary<long, GameObjectInfo> _fromGameObject;
    private readonly Dictionary<long, GameObjectInfo> _fromTransform;

    private GameObjectLookup(Dictionary<string, GameObjectInfo> fromName, Dictionary<long, GameObjectInfo> fromGameObject, Dictionary<long, GameObjectInfo> fromTransform)
    {
        _fromName = fromName;
        _fromGameObject = fromGameObject;
        _fromTransform = fromTransform;
    }

    /// <summary>
    /// Create a GameObjectLookup from a collection of <see cref="GameObjectInfo"/> records.
    /// </summary>
    /// <param name="infos"></param>
    /// <returns></returns>
    public static GameObjectLookup CreateFromInfos(IEnumerable<GameObjectInfo> infos)
    {
        Dictionary<string, GameObjectInfo> fromName = [];
        Dictionary<long, GameObjectInfo> fromGameObject = [];
        Dictionary<long, GameObjectInfo> fromTransform = [];

        foreach (GameObjectInfo info in infos)
        {
            fromName[info.GameObjectName] = info;
            fromGameObject[info.GameObjectPathId] = info;
            fromTransform[info.TransformPathId] = info;
        }

        return new(fromName, fromGameObject, fromTransform);
    }

    /// <summary>
    /// Create a GameObjectLookup from an assets file instance.
    /// </summary>
    public static GameObjectLookup CreateFromFile(AssetsManager mgr, AssetsFileInstance afileInst)
    {
        Dictionary<long, string> go2name = [];
        Dictionary<long, long> tf2parent = [];
        Dictionary<long, long> tf2go = [];

        // Iterate over the assets files once
        foreach (AssetFileInfo info in afileInst.file.AssetInfos)
        {
            if (info.TypeId == (int)AssetClassID.GameObject)
            {
                AssetTypeValueField goValueField = mgr.GetBaseField(afileInst, info);
                go2name[info.PathId] = goValueField["m_Name"].AsString;
            }
            else if (info.TypeId == (int)AssetClassID.Transform || info.TypeId == (int)AssetClassID.RectTransform)
            {
                AssetTypeValueField tValueField = mgr.GetBaseField(afileInst, info);
                tf2parent[info.PathId] = tValueField["m_Father.m_PathID"].AsLong;
                tf2go[info.PathId] = tValueField["m_GameObject.m_PathID"].AsLong;
            }
        }

        Dictionary<long, GameObjectInfo> fromTransformLookup = [];

        GameObjectInfo DoAdd(long tPathId)
        {
            if (fromTransformLookup.TryGetValue(tPathId, out GameObjectInfo info))
            {
                return info;
            }

            long parentTransformPathId = tf2parent[tPathId];
            long goPathId = tf2go[tPathId];
            string goName = go2name[goPathId];

            if (parentTransformPathId == 0)
            {
                GameObjectInfo goinfo = new(goPathId, tPathId, goName, 0);
                fromTransformLookup[tPathId] = goinfo;

                return goinfo;
            }
            else
            {
                string parentName = DoAdd(parentTransformPathId).GameObjectName;

                GameObjectInfo goinfo = new(goPathId, tPathId, $"{parentName}/{goName}", parentTransformPathId);
                fromTransformLookup[tPathId] = goinfo;

                return goinfo;
            }
        }

        foreach (long pathId in tf2parent.Keys)
        {
            DoAdd(pathId);
        }

        return CreateFromInfos(fromTransformLookup.Values);
    }

    private bool TryGet<T>(T key, Dictionary<T, GameObjectInfo> lookupDict, [MaybeNullWhen(false)] out GameObjectInfo info)
    {
        return lookupDict.TryGetValue(key, out info);
    }

    /// <summary>
    /// Get the <see cref="GameObjectInfo"/> corresponding to the given transform path ID.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Raised if the key was not found.</exception>
    public GameObjectInfo LookupTransform(long pathId) => TryGet(pathId, _fromTransform, out GameObjectInfo? info)
        ? info 
        : throw new KeyNotFoundException($"Did not find transform key {pathId}");

    /// <summary>
    /// Get the <see cref="GameObjectInfo"/> corresponding to the given game object path ID.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Raised if the key was not found.</exception>
    public GameObjectInfo LookupGameObject(long pathId) => TryGet(pathId, _fromGameObject, out GameObjectInfo? info)
        ? info
        : throw new KeyNotFoundException($"Did not find game object key {pathId}");


    /// <summary>
    /// Get the <see cref="GameObjectInfo"/> corresponding to the given game object name (given in the hierarchy).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Raised if the key was not found.</exception>
    public GameObjectInfo LookupName(string name) => TryGet(name, _fromName, out GameObjectInfo? info)
        ? info
        : throw new KeyNotFoundException($"Did not find name {name}");

    /// <summary>
    /// Try to find the given transform in the lookup.
    /// </summary>
    /// <param name="pathId">The transform path ID</param>
    /// <param name="info">The info if found; undefined if not.</param>
    /// <returns>True or false depending on if the key was found.</returns>
    public bool TryLookupTransfrom(long pathId, [MaybeNullWhen(false)] out GameObjectInfo info) => TryGet(pathId, _fromTransform, out info);

    /// <summary>
    /// Try to find the given gameObject in the lookup.
    /// </summary>
    /// <param name="pathId">The gameObject path ID</param>
    /// <param name="info">The info if found; undefined if not.</param>
    /// <returns>True or false depending on if the key was found.</returns>
    public bool TryLookupGameObject(long pathId, [MaybeNullWhen(false)] out GameObjectInfo info) => TryGet(pathId, _fromGameObject, out info);

    /// <summary>
    /// Try to find the given object name in the lookup.
    /// </summary>
    /// <param name="name">The name (in hierarchy).</param>
    /// <param name="info">The info if found; undefined if not.</param>
    /// <returns>True or false depending on if the key was found.</returns>
    public bool TryLookupName(string name, [MaybeNullWhen(false)] out GameObjectInfo info) => TryGet(name, _fromName, out info);

    /// <summary>
    /// Get an enumerator over the GameObjectInfos covered by this instance.
    /// </summary>
    public IEnumerator<GameObjectInfo> GetEnumerator()
    {
        return _fromGameObject.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// Extension methods for GameObjectLookup instances.
/// </summary>
public static class GameObjectLookupExtensions
{
    /// <summary>
    /// Enumerate the infos in self by depth-first search.
    /// </summary>
    public static IEnumerable<GameObjectLookup.GameObjectInfo> TraverseOrdered(this GameObjectLookup self)
    {
        Dictionary<long, List<GameObjectLookup.GameObjectInfo>> children = [];
        foreach (GameObjectLookup.GameObjectInfo info in self)
        {
            if (!children.TryGetValue(info.ParentPathId, out List<GameObjectLookup.GameObjectInfo> kids))
            {
                kids = [];
                children[info.ParentPathId] = kids;
            }
            kids.Add(info);
        }

        return InnerTraverse(0);

        IEnumerable<GameObjectLookup.GameObjectInfo> InnerTraverse(long pathId)
        {
            if (children.TryGetValue(pathId, out List<GameObjectLookup.GameObjectInfo> kids))
            {
                foreach (GameObjectLookup.GameObjectInfo kid in kids.OrderBy(x => x.GameObjectName).ThenBy(x => x.TransformPathId))
                {
                    yield return kid;
                    foreach (GameObjectLookup.GameObjectInfo decInfo in InnerTraverse(kid.TransformPathId))
                    {
                        yield return decInfo;
                    }
                }
            }
        }
    }

}
