using System;
using System.Collections;
using System.Collections.Generic;
using MonoDetour.HookGen;
using Silksong.AssetHelper.Core;
using Silksong.AssetHelper.Plugin.Tasks;
using UnityEngine;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Class managing the scene repacking.
/// </summary>
[MonoDetourTargets(typeof(StartManager))]
internal static class AssetRepackManager
{
    internal static void Hook()
    {
        Md.StartManager.Start.Postfix(PrependStartManagerStart);
    }

    private static void PrependStartManagerStart(StartManager self, ref IEnumerator returnValue)
    {
        returnValue = WrapStartManagerStart(self, returnValue);
    }

    private static List<BaseStartupTask> _tasks = [new SceneRepacking(), new NonSceneCatalog()];

    private static IEnumerator WrapStartManagerStart(StartManager self, IEnumerator original)
    {
        // This should already be the case, but we should check just in case it matters.
        yield return new WaitUntil(() => AddressablesData.IsAddressablesLoaded);

        LoadingBar bar = LoadingBar.Create();

        bool failed = false;
        foreach (BaseStartupTask task in _tasks)
        {
            // We have to enumerate like this because you can't yield from within a try-catch block
            IEnumerator enumerator = task.Run(bar);

            while (true)
            {
                bool b;
                try
                {
                    b = enumerator.MoveNext();
                }
                catch (Exception ex)
                {
                    AssetHelperPlugin.InstanceLogger.LogError(
                        $"Error during startup task of type {task.GetType()}\n" + ex
                    );
                    failed = true;
                    break;
                }
                if (b)
                {
                    yield return enumerator.Current;
                }
                else
                {
                    break;
                }
            }

            if (failed)
            {
                break;
            }
        }

        if (!failed)
        {
            AssetHelperPlugin.InstanceLogger.LogInfo($"{nameof(AssetHelper)} prep complete!");
            AssetRequestAPI.AfterBundleCreationComplete.Activate();
            bar.SetProgress(1);
        }
        else
        {
            AssetHelperPlugin.InstanceLogger.LogWarning(
                $"An error occurred during startup, and AssetHelper did not finish."
            );
        }

        // Even if there was an error, still let them into the game normally
        UObject.Destroy(bar);
        yield return null;

        yield return original;
        yield break;
    }
}
