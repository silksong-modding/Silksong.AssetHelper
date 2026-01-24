using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Silksong.AssetHelper.Core;
using Silksong.AssetHelper.Internal;
using Silksong.AssetHelper.Plugin.LoadingPage;

namespace Silksong.AssetHelper.Plugin.Tasks;

internal class BundleDepsPrecompute : BaseStartupTask
{
    public override IEnumerator Run(ILoadingScreen loadingScreen)
    {
        if (AssetRequestAPI.Request.NonSceneAssets.Count == 0)
        {
            // Only need to precompute bundle deps if there're non-scene assets requested
            yield break;
        }

        loadingScreen.SetText(LanguageKeys.COMPUTING_BUNDLE_DEPS.GetLocalized());
        yield return null;
        AssetHelperPlugin.InstanceLogger.LogInfo("Computing bundle deps");

        Stopwatch sw = Stopwatch.StartNew();

        List<string> bundles = AddressablesData
            .BundleKeys!.Keys.Where(x => !x.Contains("scenes_scenes_scenes"))
            .ToList();

        loadingScreen.SetProgress(0);
        int ct = 0;
        int misses = 0;

        foreach (string s in bundles)
        {
            BundleMetadata.DetermineDirectDepsInternal(s, out bool cacheHit);

            ct++;

            loadingScreen.SetProgress((float)ct / (float)bundles.Count);

            if (!cacheHit)
            {
                misses++;
                if (misses % 5 == 0)
                {
                    // Yield after batches because at this scale the fps is contributing more than 1/3 of the time
                    yield return null;
                }
            }
        }

        sw.Stop();
        AssetHelperPlugin.InstanceLogger.LogInfo(
            $"Computed bundle deps in {sw.ElapsedMilliseconds} ms."
        );
    }
}
