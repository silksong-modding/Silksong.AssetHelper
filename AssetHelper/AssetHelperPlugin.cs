using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Silksong.AssetHelper.BundleTools;
using Silksong.AssetHelper.Internal;
using Silksong.AssetHelper.Plugin;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;

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
    private Hook _atChook;

    private void Awake()
    {
        Instance = this;
        InstanceLogger = this.Logger;
        
        BundleDeps.Setup();

        GameEvents.Hook();

        AssetRepackManager.Hook();

        // TODO - remove this when assetstools.net gets updated
        _atHook = new ILHook(typeof(AssetTypeValueIterator).GetMethod(nameof(AssetTypeValueIterator.ReadNext)), PatchATVI);
        _atChook = new Hook(typeof(Net35Polyfill).GetMethod(nameof(Net35Polyfill.CopyToCompat)), PatchC2C);

        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
    }

    private void PatchC2C(Action<Stream, Stream, long, int> orig, Stream input, Stream output, long bytes, int bufferSize)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        int read;

        // set to largest value so we always go over buffer (hopefully)
        if (bytes == -1)
            bytes = long.MaxValue;

        // bufferSize will always be an int so if bytes is larger, it's also under the size of an int
        while (bytes > 0 && (read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, bytes))) > 0)
        {
            output.Write(buffer, 0, read);
            bytes -= read;
        }
        ArrayPool<byte>.Shared.Return(buffer);
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
        AssetRequestAPI.RequestApiAvailable = false;

        // Addressables isn't initialized until the next frame
        yield return null;

        while (true)
        {
            // Check this just in case
            bool b = AssetsData.TryLoadBundleKeys();
            if (b)
            {
                break;
            }

            yield return null;
        }
    }

    private void OnApplicationQuit()
    {
        GameEvents.AfterQuitApplication();
    }
}
