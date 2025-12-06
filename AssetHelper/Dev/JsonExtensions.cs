using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Silksong.AssetHelper.Dev;

public static class JsonExtensions
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
}
