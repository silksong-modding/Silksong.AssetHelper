using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Silksong.AssetHelper.Plugin;

internal class AssetRequest
{
    public Dictionary<string, HashSet<string>> SceneAssets { get; set; } = [];

    public Dictionary<(string bundleName, string assetName), Type> NonSceneAssets { get; set; } = [];

    [JsonIgnore]
    public bool AnyRequestMade => SceneAssets.Count > 0 || NonSceneAssets.Count > 0;
}
