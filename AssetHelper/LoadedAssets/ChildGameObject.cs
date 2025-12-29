using Silksong.AssetHelper.Internal;
using Silksong.UnityHelper.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Silksong.AssetHelper.LoadedAssets;

/// <summary>
/// Loadable asset wrapper representing a child of another loadable asset.
/// </summary>
/// <param name="parent">The parent loadable asset.</param>
/// <param name="path">The path from the parent to the child, with intermediate ancestors separated
/// by forward slashes.</param>
internal class ChildGameObject(ILoadableAsset<GameObject> parent, string path) : ILoadableAsset<GameObject>
{
    public GameObject? Asset
    {
        get
        {
            if (parent.Asset == null) return null;
            return parent.Asset.FindChild(path);
        }
    }

    public bool Loaded => parent.Loaded;

    private readonly List<Action<ChildGameObject>> _toInvokeWhenLoaded = [];

    /// <summary>
    /// Execute the supplied action when this asset is loaded.
    /// 
    /// If it is already loaded, execute the action immediately.
    /// </summary>
    public void ExecuteWhenLoaded(Action<ChildGameObject> toInvoke)
    {
        if (Loaded)
        {
            ActionUtil.SafeInvoke(toInvoke, this);
            return;
        }
        _toInvokeWhenLoaded.Add(toInvoke);
    }

    void ILoadableAsset<GameObject>.ExecuteWhenLoaded(Action<ILoadableAsset<GameObject>> toInvoke) => ExecuteWhenLoaded(toInvoke);

    private void OnLoadedCallback()
    {
        foreach (Action<ChildGameObject> toInvoke in _toInvokeWhenLoaded)
        {
            ActionUtil.SafeInvoke(toInvoke, this);
        }
        _toInvokeWhenLoaded.Clear();
    }

    public void Load()
    {
        if (Loaded) return;
        parent.LoadAndExecute(_ => OnLoadedCallback());
    }

    public IEnumerator LoadAsync()
    {
        if (Loaded) yield break;
        yield return parent.LoadAsync();
        OnLoadedCallback();
    }

    public void LoadImmediate()
    {
        if (Loaded) return;
        parent.LoadImmediate();
        OnLoadedCallback();
    }

    public void Unload()
    {
        parent.Unload();
    }
}
