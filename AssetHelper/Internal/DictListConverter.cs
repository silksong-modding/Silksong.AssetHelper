using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Silksong.AssetHelper.Internal;

internal class DictListConverter<T, U> : JsonConverter<Dictionary<T, U>>
{
    public override Dictionary<T, U>? ReadJson(
        JsonReader reader,
        Type objectType,
        Dictionary<T, U>? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        List<(T, U)>? list = serializer.Deserialize<List<(T, U)>>(reader);

        if (list == null)
        {
            return null;
        }

        return list.ToDictionary(x => x.Item1, x => x.Item2);
    }

    public override void WriteJson(
        JsonWriter writer,
        Dictionary<T, U>? value,
        JsonSerializer serializer
    )
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        List<(T, U)> list = value.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        serializer.Serialize(writer, list);
    }
}
