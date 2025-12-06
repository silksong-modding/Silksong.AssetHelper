using BepInEx;
using System.Collections;
using System.Collections.Generic;

namespace Silksong.AssetHelper;

[BepInAutoPlugin(id: "io.github.flibber-hk.assethelper")]
public partial class AssetHelperPlugin : BaseUnityPlugin
{
    private static readonly Dictionary<string, string> Keys = [];
    
    public static AssetHelperPlugin Instance { get;private set; }

    private void Awake()
    {
        Instance = this;
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
    }

    private IEnumerator Start()
    {
        // Addressables isn't initialized until the next frame
        yield return null;

        while (true)
        {
            // Check this just in case
            bool b = AssetsData.TryLoadBundleKeys();
            if (b)
            {
                yield break;
            }

            yield return null;
        }
    }
}
