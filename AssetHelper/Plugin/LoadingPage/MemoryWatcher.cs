using System;
using UnityEngine;
using UnityEngine.UI;

namespace Silksong.AssetHelper.Plugin.LoadingPage;


/// <summary>
/// Unscientific class to watch memory usage.
/// </summary>
[RequireComponent(typeof(Text))]
internal class MemoryWatcher : MonoBehaviour
{
    private long maxSoFar = 0;

    private Text _text;

    void Awake()
    {
        _text = GetComponent<Text>();
    }

    void Update()
    {
        long mem = GC.GetTotalMemory(forceFullCollection: false);
        maxSoFar = mem > maxSoFar ? mem : maxSoFar;
        _text.text = $"Memory: {mem:E}\nMax {maxSoFar:E}";
    }
}
