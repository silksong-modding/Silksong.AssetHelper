using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Silksong.AssetHelper.Util;

internal static class JsonExtensions
{
    public static void SerializeToFile<T>(this T self, string filePath)
    {
        string json = JsonConvert.SerializeObject(self, Formatting.Indented);

        File.WriteAllText(filePath, json);
    }

    public static void SerializeToFileInBackground<T>(this T self, string filePath)
    {
        Task.Run(() => self.SerializeToFile(filePath));
    }

    public static bool TryLoadFromFile<T>(string filePath, [MaybeNullWhen(false)] out T obj)
    {
        obj = default;

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            obj = JsonConvert.DeserializeObject<T>(json);
            return obj != null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
