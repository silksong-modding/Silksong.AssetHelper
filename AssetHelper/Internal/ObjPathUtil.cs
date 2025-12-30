using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Silksong.AssetHelper.Internal;

internal static class ObjPathUtil
{
    /// <summary>
    /// Return true if self == maybePrefix, or self is of the form {maybePrefix}/...
    /// </summary>
    public static bool HasPrefix(this string self, string? maybePrefix)
    {
        if (maybePrefix is null)
        {
            return false;
        }

        if (!self.StartsWith(maybePrefix))
        {
            return false;
        }

        if (self == maybePrefix)
        {
            return true;
        }

        if (self[maybePrefix.Length] == '/')
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Given a collection of strings representing game object paths, return a list
    /// of strings such that none of them has an ancestor in the collection.
    /// </summary>
    public static List<string> GetHighestNodes(this ICollection<string> objPaths)
    {
        List<string> nodes = [];

        string? last = null;

        foreach (string path in objPaths.OrderBy(x => x))
        {
            if (path.HasPrefix(last))
            {
                continue;
            }

            last = path;
            nodes.Add(path);
        }

        return nodes;
    }

    /// <summary>
    /// Get the ancestor of the given game object within the collection of paths.
    /// 
    /// It is assumed that paths is a set of highest nodes, see <see cref="GetHighestNodes(ICollection{string})" />.
    /// </summary>
    /// <param name="paths">A list of paths of candidate ancestors.</param>
    /// <param name="objName">A path to check.</param>
    /// <param name="ancestorPath">The path representing the ancestor.</param>
    /// <param name="relativePath">The path relative to the ancestor.</param>
    /// <returns>False if the supplied game object has no ancestor in the repacked bundle.</returns>
    public static bool TryFindAncestor(List<string> paths, string objName, [MaybeNullWhen(false)] out string ancestorPath, [MaybeNullWhen(false)] out string relativePath)
    {
        foreach (string path in paths ?? Enumerable.Empty<string>())
        {
            if (objName == path)
            {
                ancestorPath = objName;
                relativePath = string.Empty;
                return true;
            }

            if (objName.HasPrefix(path))
            {
                ancestorPath = path;
                relativePath = objName[(1 + path.Length)..];
                return true;
            }
        }

        ancestorPath = null;
        relativePath = null;
        return false;
    }

    /// <summary>
    /// Given the name to a game object in the hierarchy, returns its parent's name.
    /// </summary>
    /// <param name="objName">The name of the object.</param>
    /// <param name="parent">The name of the parent.</param>
    /// <returns>True if the object is not a root game object; false otherwise.</returns>
    public static bool TryGetParent(this string objName, out string parent)
    {
        int lastSlashIndex = objName.LastIndexOf('/');

        if (lastSlashIndex == -1)
        {
            parent = string.Empty;
            return false;
        }

        parent = objName.Substring(0, lastSlashIndex);
        return true;
    }
}
