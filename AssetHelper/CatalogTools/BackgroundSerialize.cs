using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.Util;
using static UnityEngine.AddressableAssets.ResourceLocators.ContentCatalogData;

namespace Silksong.AssetHelper.CatalogTools;

internal static class BackgroundSerialize
{
    /// <summary>
    /// Code copy-pasted from ContentCatalogData.Serializer.Serialize with yields added
    /// </summary>
    /// <returns></returns>
    public static IEnumerator<int> SerializeRoutine(
        this ContentCatalogData.Serializer self,
        BinaryStorageBuffer.Writer writer,
        ContentCatalogData contentCatalogData)
    {
        IList<ContentCatalogDataEntry> entries = contentCatalogData.m_Entries;
        Dictionary<object, List<int>> dictionary = new Dictionary<object, List<int>>();
        for (int i = 0; i < entries.Count; i++)
        {
            foreach (object key in entries[i].Keys)
            {
                if (!dictionary.TryGetValue(key, out var value))
                {
                    dictionary.Add(key, value = new List<int>());
                }
                value.Add(i);
            }
        }
        uint num = writer.Reserve<ResourceLocator.Header>();
        uint num2 = writer.Reserve<ResourceLocator.KeyData>((uint)dictionary.Count);
        ResourceLocator.Header val2 = new ResourceLocator.Header
        {
            magic = kMagic,
            version = 2,
            keysOffset = num2,
            idOffset = writer.WriteString(contentCatalogData.ProviderId),
            instanceProvider = writer.WriteObject(contentCatalogData.InstanceProviderData, serializeTypeData: false),
            sceneProvider = writer.WriteObject(contentCatalogData.SceneProviderData, serializeTypeData: false),
            initObjectsArray = writer.WriteObjects(contentCatalogData.m_ResourceProviderData, serizalizeTypeData: false),
            buildResultHash = writer.WriteString(contentCatalogData.BuildResultHash)
        };
        writer.Write(num, in val2);
        uint[] locationIds = new uint[entries.Count];
        for (int j = 0; j < entries.Count; j++)
        {
            locationIds[j] = writer.WriteObject(new ResourceLocator.ContentCatalogDataEntrySerializationContext
            {
                entry = entries[j],
                allEntries = entries,
                keyToEntryIndices = dictionary
            }, serializeTypeData: false);

            // Added yield return line during the slow loop
            yield return j + 1;
        }
        int num3 = 0;
        ResourceLocator.KeyData[] array = new ResourceLocator.KeyData[dictionary.Count];
        foreach (KeyValuePair<object, List<int>> item in dictionary)
        {
            uint[] values = item.Value.Select((int num4) => locationIds[num4]).ToArray();
            array[num3++] = new ResourceLocator.KeyData
            {
                keyNameOffset = writer.WriteObject(item.Key, serializeTypeData: true),
                locationSetOffset = writer.Write(values, hashElements: true)
            };
        }
        writer.Write(num2, array, hashElements: true);
    }

}
