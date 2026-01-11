using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper.CatalogTools;

/// <summary>
/// Resource provider that returns a child gameobject of its first dependency.
/// 
/// The internal ID must be of the following form:
/// {relativePath}/{InternalIdSeparator}/{whatever}
/// relativePath is the path of the child relative to the parent, so it can be found
/// with parent.transform.Find(relativePath).
/// </summary>
internal class ChildGameObjectProvider : ResourceProviderBase
{
    public static string ClassProviderId => "Silksong.AssetHelper.CatalogTools.ChildObjectProvider";
    public static string InternalIdSeparator => "AssetHelper-ChildGameObject-Split";

    public override Type GetDefaultType(IResourceLocation location)
    {
        return typeof(GameObject);
    }

    public override string ProviderId => ClassProviderId;

    public override void Provide(ProvideHandle provideHandle)
    {
        List<object> deps = [];
        provideHandle.GetDependencies(deps);

        string internalId = provideHandle.Location.InternalId;
        string relativePath = internalId.Split($"/{InternalIdSeparator}/").First();

        GameObject parent = provideHandle.GetDependency<GameObject>(0);

        if (parent != null)
        {
            Transform childTransform = parent.transform.Find(relativePath);
            if (childTransform != null)
            {
                provideHandle.Complete(childTransform.gameObject, true, null);
            }
            else
            {
                provideHandle.Complete<GameObject>(null!, false, new Exception($"Child '{relativePath}' not found in {parent.name}"));
            }
        }
        else
        {
            provideHandle.Complete<GameObject>(null!, false, new Exception("Parent dependency failed to load or is not a GameObject."));
        }
    }
}