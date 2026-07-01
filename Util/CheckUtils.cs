using System;
using CUCoreLib.Registries;
using Object = UnityEngine.Object;

namespace CUCoreLib.Util;

public static class CheckUtils
{
    public static bool IsMainMenuReady()
    {
        if (WorldGeneration.world != null) return false;

        return Object.FindObjectOfType<PreRunScript>() != null;
    }

    public static bool IsWorldGenerationReady()
    {
        var world = WorldGeneration.world;
        if (world == null) return false;

        return world.worldExists && !world.generatingWorld;
    }

    public static bool IsInWorld()
    {
        return IsWorldGenerationReady() && PlayerCamera.main != null && PlayerCamera.main.body != null;
    }

    public static bool IsModdedItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        var normalizedId = itemId.Trim();
        if (ItemRegistry.TryGetCustomInfo(normalizedId, out _)) return true;

        return normalizedId.StartsWith("glassworks.", StringComparison.OrdinalIgnoreCase) ||
               normalizedId.StartsWith("cucorelib.", StringComparison.OrdinalIgnoreCase);
    }
}