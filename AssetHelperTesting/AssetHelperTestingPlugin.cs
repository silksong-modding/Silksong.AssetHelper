using AssetHelperTesting.Tests;
using BepInEx;
using BepInEx.Logging;
using Silksong.AssetHelper.Dev;
using Silksong.AssetHelper.Plugin;

namespace AssetHelperTesting
{
    // TODO - adjust the plugin guid as needed
    [BepInAutoPlugin(id: "org.silksong-modding.assethelpertesting")]
    public partial class AssetHelperTestingPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource InstanceLogger { get; private set; }

        private void Awake()
        {
            InstanceLogger = Logger;

            PrepareTests();

            // Put your initialization logic here
            Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");

            AssetRequestAPI.InvokeAfterBundleCreation(
                () => DebugTools.DumpAllAddressableAssets(AssetRequestAPI.SceneAssetLocator!, "scene_locator.json")
            );
        }

        private void PrepareTests()
        {
            MultiGoSpawn.Prepare();
        }
    }
}
