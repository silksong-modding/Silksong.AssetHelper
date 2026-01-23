using System;
using System.Buffers;
using System.IO;
using System.Reflection;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Silksong.AssetHelper;

// TODO - remove this when assetstools.net gets updated
internal static class AssetsToolsPatch
{
    private static readonly MethodInfo _readNextMethod 
        = typeof(AssetTypeValueIterator).GetMethod(nameof(AssetTypeValueIterator.ReadNext));

    private static ILHook? _atIteratorHook;
    private static ILHook? _atInteratorHook2;
    private static Hook? _atCopyHook;

    public static void Init()
    {
        _atIteratorHook = new ILHook(
            _readNextMethod,
            PatchATVI
        );

        _atInteratorHook2 = new ILHook(
            _readNextMethod,
            PatchATVI2
        );

        _atCopyHook = new Hook(
            typeof(Net35Polyfill).GetMethod(nameof(Net35Polyfill.CopyToCompat)),
            PatchC2C
        );
    }

    /// <summary>
    /// See https://github.com/nesrak1/AssetsTools.NET/commit/57357f193fa4532c31b7569d4b0f9b74d04d12e8
    /// </summary>
    private static void PatchATVI2(ILContext il)
    {
        ILCursor cursor = new(il);

        Instruction? newTarget = null;
        Instruction? source = null;

        if (!cursor.TryGotoNext(
            MoveType.Before,
            i => i.MatchLdloc(0),
            i => i.MatchCallOrCallvirt(out _),
            i => i.MatchCallvirt<AssetTypeTemplateField>($"get_{nameof(AssetTypeTemplateField.IsArray)}"),
            i => { source = i; return i.MatchBrfalse(out _); },

            i => i.MatchLdloc(0),
            i => i.MatchCallOrCallvirt(out _),
            i => i.MatchCallvirt<AssetTypeTemplateField>($"get_{nameof(AssetTypeTemplateField.ValueType)}"),
            i => i.MatchLdcI4((int)AssetValueType.ByteArray),
            i => i.MatchBeq(out _),

            i => { newTarget = i; return i.MatchLdloc(0); },
            i => i.MatchCallOrCallvirt(out _),
            i => i.MatchCallvirt<AssetTypeTemplateField>($"get_{nameof(AssetTypeTemplateField.IsAligned)}"),
            i => i.MatchBrfalse(out _)
            ))
        {
            AssetHelperPlugin.InstanceLogger.LogWarning($"ATVI IL hook 2: failed to find instrs");
            return;
        }

        if (source == null || newTarget == null)
        {
            AssetHelperPlugin.InstanceLogger.LogWarning($"ATVI IL hook 2: failed to assign source/target");
            return;
        }

        cursor.Goto(newTarget);
        ILLabel label = cursor.MarkLabel();

        source.Operand = label;
    }

    /// <summary>
    /// Use array pooling to plug a memory leak; this memory leak doesn't happen in modern versions of
    /// .NET with a better garbage collector.
    /// </summary>
    private static void PatchC2C(
        Action<Stream, Stream, long, int> orig,
        Stream input,
        Stream output,
        long bytes,
        int bufferSize
    )
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
    /// Fixes a bug with AssetTypeValueIterator where it moves 4 bytes forward when reading a double rather than 8.
    /// </summary>
    private static void PatchATVI(ILContext il)
    {
        ILCursor cursor = new(il);

        if (
            !cursor.TryGotoNext(
                MoveType.After,
                i =>
                    i.MatchCallvirt<AssetTypeTemplateField>(
                        $"get_{nameof(AssetTypeTemplateField.ValueType)}"
                    ),
                i => i.MatchStloc(out _),
                i => i.MatchLdloc(out _),
                i => i.MatchLdcI4(out _),
                i => i.MatchSub(),
                i => i.MatchSwitch(out _)
            )
        )
        {
            return;
        }

        ILLabel[] switchArgs = (ILLabel[])cursor.Prev.Operand;
        switchArgs[(int)AssetValueType.Double - 1] = switchArgs[(int)AssetValueType.Int64 - 1];
    }
}
