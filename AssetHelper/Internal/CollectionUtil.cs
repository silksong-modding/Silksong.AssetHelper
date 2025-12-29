using System.Collections.Generic;
using System.Linq;

namespace Silksong.AssetHelper.Internal;

internal static class CollectionUtil
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
}
