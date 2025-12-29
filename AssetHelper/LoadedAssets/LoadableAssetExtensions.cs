using System;

namespace Silksong.AssetHelper.LoadedAssets;

/// <summary>
/// Extension methods to control when Loadable Assets are loaded.
/// </summary>
public static class LoadableAssetExtensions
{
    private class GameplayAssetHandle<T>(ILoadableAsset<T> asset) : IDisposable where T : UObject
    {
        private bool isDisposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    GameEvents.OnEnterGame -= asset.Load;
                }
                isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Ensure that this asset is always loaded while in-game.
    /// 
    /// If currently in game, will load the asset.
    /// </summary>
    /// <param name="asset">The asset to set.</param>
    /// <param name="handle">Dispose the handle to undo the SetGameplayAsset operation.</param>
    public static ILoadableAsset<T> SetGameplayAsset<T>(
        this ILoadableAsset<T> asset,
        out IDisposable handle)
        where T : UObject
    {
        GameEvents.OnEnterGame += asset.Load;

        if (GameEvents.IsInGame)
        {
            asset.Load();
        }

        handle = new GameplayAssetHandle<T>(asset);

        return asset;
    }

    /// <summary>
    /// Execute the supplied action, ensuring that the loadable asset is loaded.
    /// </summary>
    /// <param name="asset">The asset to ensure loaded.</param>
    /// <param name="toInvoke">The action to invoke.</param>
    public static void LoadAndExecute<T>(this ILoadableAsset<T> asset, Action<ILoadableAsset<T>> toInvoke) where T : UObject
    {
        asset.Load();
        asset.ExecuteWhenLoaded(toInvoke);
    }
}
