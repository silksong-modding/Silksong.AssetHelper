namespace Silksong.AssetHelper.Internal;

internal class VersionedObject<T>(string version, T? value)
{
    public string? Version { get; set; } = version;

    public T? Value { get; set; } = value;
}
