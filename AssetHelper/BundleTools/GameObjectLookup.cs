using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
    /// <param name="GameObjectName">The path to the game object in the form root/.../grandparent/parent/object.</param>
    public record GameObjectInfo(long GameObjectPathId, long TransformPathId, string GameObjectName);

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
        Dictionary<long, GameObjectInfo> fromTransformLookup = [];

        GameObjectInfo DoAdd(long tPathId)
        {
            if (fromTransformLookup.TryGetValue(tPathId, out GameObjectInfo info))
            {
                return info;
            }

            AssetTypeValueField tValueField = mgr.GetBaseField(afileInst, tPathId);
            long goPathId = tValueField["m_GameObject.m_PathID"].AsLong;
            AssetTypeValueField goValueField = mgr.GetBaseField(afileInst, goPathId);
            string goName = goValueField["m_Name"].AsString;
            long parentTransformPathId = tValueField["m_Father.m_PathID"].AsLong;

            if (parentTransformPathId == 0)
            {
                GameObjectInfo newInfo = new(goPathId, tPathId, goName);
                fromTransformLookup[tPathId] = newInfo;
                return newInfo;
            }

            GameObjectInfo parentInfo = DoAdd(parentTransformPathId);
            GameObjectInfo childInfo = new(goPathId, tPathId, $"{parentInfo.GameObjectName}/{goName}");
            fromTransformLookup[tPathId] = childInfo;
            return childInfo;
        }

        foreach (AssetFileInfo transform in afileInst.file.GetAllTransforms())
        {
            DoAdd(transform.PathId);
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
        return _fromName.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
