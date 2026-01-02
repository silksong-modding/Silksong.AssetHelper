using AssetsTools.NET;
using BepInEx;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Silksong.AssetHelper.BundleTools;
using System.Collections;
using System.Collections.Generic;

namespace Silksong.AssetHelper;

[BepInAutoPlugin(id: "io.github.flibber-hk.assethelper")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public partial class AssetHelperPlugin : BaseUnityPlugin
{
    public static AssetHelperPlugin Instance { get; private set; }
    #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    internal static ManualLogSource InstanceLogger { get; private set; }

    private static readonly Dictionary<string, string> Keys = [];

    private ILHook _atHook;

    private void Awake()
    {
        Instance = this;
        InstanceLogger = this.Logger;
        
        BundleDeps.Setup();

        GameEvents.Hook();

        // TODO - remove this when assetstools.net gets updated
        _atHook = new ILHook(typeof(AssetTypeValueIterator).GetMethod(nameof(AssetTypeValueIterator.ReadNext)), PatchATVI);
        
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
    }

    /// <summary>
    /// Fixes a bug with AssetTypeValueIterator where it moves 4 bytes forward when reading a double rather than 8
    /// </summary>
    private void PatchATVI(ILContext il)
    {
        ILCursor cursor = new(il);

        if (!cursor.TryGotoNext(MoveType.After,
            i => i.MatchCallvirt<AssetTypeTemplateField>($"get_{nameof(AssetTypeTemplateField.ValueType)}"),
            i => i.MatchStloc(out _),
            i => i.MatchLdloc(out _),
            i => i.MatchLdcI4(out _),
            i => i.MatchSub(),
            i => i.MatchSwitch(out _)
            ))
        {
            return;
        }

        ILLabel[] switchArgs = (ILLabel[])cursor.Prev.Operand;
        switchArgs[(int)AssetValueType.Double - 1] = switchArgs[(int)AssetValueType.Int64 - 1];
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

    private void OnApplicationQuit()
    {
        GameEvents.AfterQuitApplication();
    }
}
