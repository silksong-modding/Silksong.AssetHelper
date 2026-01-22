using System;
using System.IO;
using Newtonsoft.Json;
using Silksong.AssetHelper.Core;

namespace Silksong.AssetHelper.Internal;

/// <summary>
/// Object that is loaded from cache if possible, and instantiated if not.
///
/// The object is saved when quitting the application.
/// </summary>
internal class CachedObject<T>
    where T : class
{
    private CachedObject() { }

    [JsonProperty]
    public required string SilksongVersion { get; init; }

    [JsonProperty]
    public required string PluginVersion { get; init; }

    [JsonProperty]
    public required T Value { get; set; }

    private bool IsValid()
    {
        if (SilksongVersion == null || PluginVersion == null)
        {
            return false;
        }

        if (VersionData.SilksongVersion != SilksongVersion)
        {
            return false;
        }

        if (!VersionData.EarliestAcceptableGeneralVersion.AllowCachedData(this.PluginVersion))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Create a cached object.
    /// </summary>
    /// <param name="filename">Filename in the cache folder. Should end in .json</param>
    /// <param name="createDefault"></param>
    /// <param name="mutable">If true, the object will be reserialized on application quit.</param>
    /// <param name="syncHandle">Dispose this to serialize the object and no longer serialize on application quit.
    /// This will be null if <paramref name="mutable"/> is false.</param>
    /// <returns></returns>
    public static CachedObject<T> CreateSynced(string filename, Func<T> createDefault, bool mutable, out IDisposable? syncHandle)
    {
        syncHandle = null;

        string filePath = Path.Combine(AssetPaths.CacheDirectory, filename);

        // Check if the object already exists
        if (
            JsonExtensions.TryLoadFromFile<CachedObject<T>>(
                filePath,
                out CachedObject<T>? fromCache
            )
        )
        {
            if (fromCache.Value is not null && fromCache.IsValid())
            {
                if (mutable)
                {
                    syncHandle = new CachedObjectSyncHandle<T>(fromCache, filePath);
                }
                
                return fromCache;
            }
        }

        CachedObject<T> created = new()
        {
            SilksongVersion = VersionData.SilksongVersion,
            PluginVersion = AssetHelperPlugin.Version,
            Value = createDefault(),
        };
        created.SerializeToFile(filePath);
        if (mutable)
        {
            syncHandle = new CachedObjectSyncHandle<T>(created, filePath);
        }
        return created;
    }
}

file class CachedObjectSyncHandle<T> : IDisposable where T : class
{
    private CachedObject<T> _obj;
    private string _filepath;

    public CachedObjectSyncHandle(CachedObject<T> obj, string filepath)
    {
        _obj = obj;
        _filepath = filepath;

        AssetHelperPlugin.OnQuitApplication += SyncObj;
    }

    private void SyncObj() => _obj.SerializeToFile(_filepath);

    public void Dispose()
    {
        SyncObj();
        AssetHelperPlugin.OnQuitApplication -= SyncObj;
    }
}
