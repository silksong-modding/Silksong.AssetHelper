using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Silksong.AssetHelper.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace Silksong.AssetHelper.CatalogTools;

/// <summary>
/// Class to write a catalog file that can be loaded by the Addressables package.
/// </summary>
internal static class CatalogUtils
{
    /// <summary>
    /// Hashes the provided data using Unity's Hash128 implementation.
    /// Mainly used to create the catalog hash file.
    /// </summary>
    /// <param name="catalogByteArray">Data buffer to hash.</param>
    private static unsafe Hash128 Hash(byte[] catalogByteArray)
    {
        Hash128 outhash = default;
        ulong u64_ = outhash.u64_0;
        ulong u64_2 = outhash.u64_1;

        fixed (void* p = catalogByteArray)
        {
            SpookyHash.Hash(p, (ulong)catalogByteArray.Length, &u64_, &u64_2);
        }

        outhash = new Hash128(u64_, u64_2);

        return outhash;
    }

    /// <summary>
    /// Serializes the provided entry list into a binary catalog.
    /// Writes the binary catalog and its hash next to each other.
    /// </summary>
    /// <param name="locationEntries">List of entries to serialize into the catalog</param>
    /// <param name="catalogId">Unique name of the catalog.</param>
    /// <returns>
    /// An enumerator that yields the number of location entries that have been serialized.
    /// This enumerator must be consumed in order to finish serialization.
    /// </returns>
    public static IEnumerator<int> WriteCatalogRoutine(
        List<ContentCatalogDataEntry> locationEntries,
        string catalogId
    )
    {
        string catalogName = catalogId;
        if (!catalogName.StartsWith(nameof(AssetHelper)))
        {
            catalogName = $"{nameof(AssetHelper)}-{catalogId}";
        }

        ContentCatalogData ccd = new ContentCatalogData(catalogName);
        ccd.m_Entries = locationEntries;

        ContentCatalogData.Serializer catalogSerializer = new();
        BinaryStorageBuffer catalogBuffer = new();
        BinaryStorageBuffer.Writer catalogWriter = new BinaryStorageBuffer.Writer(
            1048576,
            catalogSerializer
        );

        IEnumerator<int> serializationRoutine = catalogSerializer.SerializeRoutine(catalogWriter, ccd);
        while (serializationRoutine.MoveNext())
        {
            yield return serializationRoutine.Current;
        }
        
        byte[] catalogBytes = catalogWriter.SerializeToByteArray();

        Hash128 outhash = Hash(catalogBytes);

        string catalogBinPath = Path.Combine(AssetPaths.CatalogFolder, $"{catalogId}.bin");
        File.WriteAllBytes(catalogBinPath, catalogBytes);
        File.WriteAllText(
            Path.Combine(AssetPaths.CatalogFolder, $"{catalogId}.hash"),
            outhash.ToString()
        );
    }
}
