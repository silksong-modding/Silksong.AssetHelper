using BepInEx;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;
using System;
using System.Diagnostics;
using Silksong.AssetHelper.Dev;
using BepInEx.Configuration;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace AssetHelperMenu;

[BepInAutoPlugin(id: "org.silksong-modding.assethelpermenu")]
[BepInDependency(Silksong.ModMenu.ModMenuPlugin.Id)]
[BepInDependency(Silksong.AssetHelper.AssetHelperPlugin.Id)]
public partial class AssetHelperMenuPlugin : BaseUnityPlugin, IModMenuCustomMenu
{
    public ConfigEntry<bool> PreferCompressed;

    private void Awake()
    {
        PreferCompressed = Config.Bind("General", nameof(PreferCompressed), true,
            "Whether to prefer compressed scene dumps by default");

        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
    }

    public string ModMenuName() => "AssetHelper";

    public AbstractMenuScreen BuildCustomMenu()
    {
        PaginatedMenuScreenBuilder builder = new(ModMenuName());

        ConfigEntryFactory factory = new();

        // Open folder button
        builder.AddButton("Open debug folder", () => Process.Start(DebugTools.DebugDataDir));
        
        // Simple dumps
        builder.AddButton("Dump asset names", DebugTools.DumpAllAssetNames);
        builder.AddButton("Dump asset request", () => DebugTools.SerializeAssetRequest());

        // TODO - consider option to include dependency names in addressable asset dump
        builder.AddButton("Dump addressable assets", DebugTools.DumpAllAddressableAssets);        

        // Loaded asset bundles
        builder.AddButton("Dump loaded bundle names", DebugTools.DumpLoadedBundleNames);

        // Scene paths dump in subpage so everything is together
        {
            PaginatedMenuScreenBuilder sceneSubpageBuilder = new("Scene hierarchy tools");

            if (factory.GenerateMenuElement(PreferCompressed, out MenuElement? preferCompressedToggle))
            {
                sceneSubpageBuilder.Add(preferCompressedToggle);
            }
            sceneSubpageBuilder.AddButton("Dump current scene hierarchy", () => DumpGameObjectPaths(USceneManager.GetActiveScene().name));
            sceneSubpageBuilder.AddButton("Dump loaded scene hierarchies", () =>
            {
                for (int i = 0; i < USceneManager.sceneCount; i++)
                {
                    DumpGameObjectPaths(USceneManager.GetSceneAt(i).name);
                }
            });

            // TODO - add a button to dump a scene by name - blocked by ModMenu needing a text entry field

            PaginatedMenuScreen sceneSubpage = sceneSubpageBuilder.Build();

            builder.AddSubpageButton(sceneSubpage);
        }

        return builder.Build();
    }

    private void DumpGameObjectPaths(string sceneName)
    {
        try
        {
            Logger.LogInfo($"Dumping scene hierarchy for scene {sceneName}");
            DebugTools.DumpGameObjectPaths(sceneName, PreferCompressed.Value);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error dumping paths for scene {sceneName}\n" + ex);
        }
    }
}

file static class ModMenuExtensions
{
    // TODO - add description; blocked by https://github.com/silksong-modding/Silksong.ModMenu/issues/25
    public static TextButton AddButton(this PaginatedMenuScreenBuilder self, string label, Action onClick)
    {
        TextButton button = new(label);
        button.OnSubmit += onClick;

        self.Add(button);
        return button;
    }

    public static TextButton AddSubpageButton(this PaginatedMenuScreenBuilder self, AbstractMenuScreen subpage, string? label = null)
    {
        label ??= subpage.TitleText.text;
        TextButton button = self.AddButton(label, () => MenuScreenNavigation.Show(subpage));
        return button;
    }
}
