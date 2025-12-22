namespace Silksong.AssetHelper.Internal;

/// <summary>
/// Object that is loaded from cache if possible, and instantiated if not.
/// 
/// The object is saved when 
/// </summary>
/// <typeparam name="T"></typeparam>
internal class CachedObject<T> where T : class, new()
{
    public CachedObject(string filename)
    {
        Filename = filename;
        Value = CacheManager.GetCached<T>(() => new T(), filename);

        GameEvents.OnQuitApplication += SerializeObject;
    }

    public void SerializeObject() => CacheManager.WriteObj(Value, Filename);

    public string Filename { get; set; }
    public T Value { get; set; }
}
