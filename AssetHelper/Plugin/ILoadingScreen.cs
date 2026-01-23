using UnityEngine;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Interface defining the contract for a loading screen.
/// </summary>
internal interface ILoadingScreen
{
    public void SetText(string text);

    public void SetSubtext(string text);

    public void SetProgress(float progress);

    public void SetVisible(bool visible);
}

internal static class LoadingScreenExtensions
{
    public static T Create<T>() where T : MonoBehaviour, ILoadingScreen
    {
        GameObject go = new("AssetHelper LoadingScreen");
        T ret = go.AddComponent<T>();
        go.SetActive(true);
        return ret;
    }

    public static void Reset(this ILoadingScreen self)
    {
        self.SetText(string.Empty);
        self.SetSubtext(string.Empty);
        self.SetProgress(0);
        self.SetVisible(true);
    }
}
