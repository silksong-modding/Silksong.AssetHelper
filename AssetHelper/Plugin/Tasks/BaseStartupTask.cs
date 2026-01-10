using System.Collections;

namespace Silksong.AssetHelper.Plugin.Tasks;

/// <summary>
/// Base class for tasks that happen at startup.
/// </summary>
internal abstract class BaseStartupTask
{
    /// <summary>
    /// Run the startup task. The objects yielded by this enumerator will
    /// be passed through to Unity.
    /// </summary>
    /// <param name="loadingBar">A loading bar.</param>
    public abstract IEnumerator Run(LoadingBar loadingBar);
}
