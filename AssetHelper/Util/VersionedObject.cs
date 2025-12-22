using System;
using System.Collections.Generic;
using System.Text;

namespace Silksong.AssetHelper.Util;

internal class VersionedObject<T>(string version, T? value)
{
    public string? Version { get; set; } = version;

    public T? Value { get; set; } = value;
}
