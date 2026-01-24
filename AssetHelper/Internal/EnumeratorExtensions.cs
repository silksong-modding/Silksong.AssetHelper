using System.Collections;

namespace Silksong.AssetHelper.Internal;

internal static class EnumeratorExtensions
{
    public static void Consume(this IEnumerator self)
    {
        while (self.MoveNext()) { }
    }
}
