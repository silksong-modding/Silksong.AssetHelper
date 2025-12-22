using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Silksong.AssetHelper.Util;

internal static class CacheManager
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"{nameof(AssetHelper)}.{nameof(CacheManager)}");

    // Use reflection to get the non-constant value of a constant field
    private static string GetSilksongVersion() => typeof(Constants)
        .GetField(nameof(Constants.GAME_VERSION), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
        ?.GetRawConstantValue()
        as string
        ?? "UNKNOWN";


    private static string? _cacheDirectory = null;

    /// <summary>
    /// Directory storing cached information for this version of Silksong.
    /// </summary>
    public static string CacheDirectory
    {
        get
        {
            if (_cacheDirectory is not null) return _cacheDirectory;
            
            string silksongVersion = GetSilksongVersion();
            string dir = Path.Combine(
                Paths.CachePath,
#if DEBUG
                $"{nameof(AssetHelper)}_DEBUG",
#else
                nameof(AssetHelper),
#endif
                $"v{silksongVersion}");

            Directory.CreateDirectory(dir);

            _cacheDirectory = dir;
            return dir;
        }
    }

    // TODO - only recalculate if major/minor version changes

    public static void WriteObj<T>(T obj, string filename) where T : class
    {
        string filePath = Path.Combine(CacheDirectory, filename);

        VersionedObject<T> toCache = new(AssetHelperPlugin.Version, obj);
        toCache?.SerializeToFile(filePath);
    }

    /// <summary>
    /// If the filename exists in the cache folder, load the object using Newtonsoft.Json.
    /// If the filename does not exist, compute the object, store it in the file and return it.
    /// 
    /// The function will be recalculated each time the Silksong version or
    /// <see cref="AssetHelperPlugin"/> version changes.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="generator">The function used to generate the object.</param>
    /// <param name="filename">The name of the cache file.</param>
    /// <returns>The object.</returns>
    public static T GetCached<T>(
        Func<T> generator,
        string filename) where T : class
    {
        string filePath = Path.Combine(CacheDirectory, filename);

        if (JsonExtensions.TryLoadFromFile<VersionedObject<T>>(filePath, out VersionedObject<T>? fromCache))
        {
            if (fromCache.Version == AssetHelperPlugin.Version && fromCache.Value is not null)
            {
                return fromCache.Value;
            }
        }

        T generated = generator();
        WriteObj(generated, filename);

        return generated;
    }
}
