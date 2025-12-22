using BepInEx.Logging;
using System;

namespace Silksong.AssetHelper.Internal;

internal static class ActionUtil
{
    private static readonly ManualLogSource Log = Logger.CreateLogSource($"AssetHelper.{nameof(ActionUtil)}");

    public static void SafeInvoke(Action? a)
    {
        if (a == null) return;

        try
        {
            a.Invoke();
        }
        catch (Exception ex)
        {
            Log.LogError($"Error invoking action {a.Method.Name}\n" + ex);
        }
    }

    public static void SafeInvoke<T>(Action<T>? a, T arg)
    {
        if (a == null) return;

        try
        {
            a.Invoke(arg);
        }
        catch (Exception ex)
        {
            Log.LogError($"Error invoking action {a.Method.Name}\n" + ex);
        }
    }
}
