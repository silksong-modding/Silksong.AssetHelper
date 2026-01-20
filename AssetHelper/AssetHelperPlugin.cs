using System;
using System.Collections;
using BepInEx;
using BepInEx.Logging;
using Silksong.AssetHelper.CatalogTools;
using Silksong.AssetHelper.Core;
using Silksong.AssetHelper.Internal;
using Silksong.AssetHelper.Plugin;
using UnityEngine.AddressableAssets;

namespace Silksong.AssetHelper;

// We don't want to create a menu because any config options we add in the future are either
// intended for devs or shouldn't be toggled while in game.
// Adding this for forward compatibility
[AttributeUsage(AttributeTargets.Class)]
internal class ModMenuIgnoreAttribute : Attribute { }

[BepInAutoPlugin(id: "org.silksong-modding.assethelper")]
[BepInDependency("org.silksong-modding.i18n")]
[ModMenuIgnore]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public partial class AssetHelperPlugin : BaseUnityPlugin
{
    public static AssetHelperPlugin Instance { get; private set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Event raised when quitting the application.
    /// </summary>
    public static event Action? OnQuitApplication;

    internal static ManualLogSource InstanceLogger { get; private set; }

    private void Awake()
    {
        Instance = this;
        InstanceLogger = this.Logger;

        InitLibLogging();
        AssetsToolsPatch.Init();
        BundleMetadata.Setup();
        StartupOverrideManager.Hook();
        Addressables.ResourceManager.ResourceProviders.Add(new ChildGameObjectProvider());

        Logger.LogInfo($"Silksong version: {VersionData.SilksongVersion}");
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
    }

    private static void InitLibLogging()
    {
        ManualLogSource libLogger = BepInEx.Logging.Logger.CreateLogSource("AssetHelper.Lib");
        AssetHelperLib.Logging.OnLog += libLogger.LogInfo;
        AssetHelperLib.Logging.OnLogWarning += libLogger.LogWarning;
        AssetHelperLib.Logging.OnLogError += libLogger.LogError;
    }

    private IEnumerator Start()
    {
        AssetRequestAPI.RequestApiAvailable = false;

        // Addressables isn't initialized until the next frame
        yield return null;

        while (true)
        {
            // Check this just in case
            bool b = AddressablesData.TryLoadBundleKeys();
            if (b)
            {
                break;
            }

            yield return null;
        }
    }

    private void OnApplicationQuit()
    {
        foreach (Action a in OnQuitApplication?.GetInvocationList() ?? Array.Empty<Action>())
        {
            ActionUtil.SafeInvoke(a);
        }
    }
}
