using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUCoreLib.Helpers;

internal static class ResourceCache
{
    //  Prefab Name -> GameObject dict
    public static Dictionary<string, GameObject> AllPrefabs = new(StringComparer.OrdinalIgnoreCase);

    private static bool _isInitialized;

    public static void TryInitialize()
    {
        if (_isInitialized) return;

        // We are allowed one heavy hit per mod, and here it is :)
        var allResources = Resources.LoadAll<GameObject>("");

        foreach (var go in allResources)
            if (go != null && !AllPrefabs.ContainsKey(go.name))
                AllPrefabs.Add(go.name, go);

        _isInitialized = true;
    }
}