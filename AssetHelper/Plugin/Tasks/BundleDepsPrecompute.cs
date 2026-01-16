using Silksong.AssetHelper.Core;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Silksong.AssetHelper.Plugin.Tasks;

internal class BundleDepsPrecompute : BaseStartupTask
{
    public override IEnumerator Run(LoadingBar loadingBar)
    {
        if (AssetRequestAPI.RequestedNonSceneAssets.Count == 0)
        {
            // Only need to precompute bundle deps if there're non-scene assets requested
            yield break;
        }

        loadingBar.SetText(LanguageKeys.COMPUTING_BUNDLE_DEPS.GetLocalized());
        yield return null;
        AssetHelperPlugin.InstanceLogger.LogInfo("Computing bundle deps");

        Stopwatch sw = Stopwatch.StartNew();

        List<string> bundles = AddressablesData.BundleKeys!.Keys.Where(x => !x.Contains("scenes_scenes_scenes")).ToList();

        loadingBar.SetProgress(0);
        int ct = 0;
        int misses = 0;

        foreach (string s in bundles)
        {
            BundleMetadata.DetermineDirectDepsInternal(s, out bool cacheHit);

            ct++;

            loadingBar.SetProgress((float)ct / (float)bundles.Count);
            
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
        AssetHelperPlugin.InstanceLogger.LogInfo($"Time {sw.ElapsedMilliseconds} ms");
    }
}
