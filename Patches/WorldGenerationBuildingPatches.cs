using CUCoreLib.Data;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches;

[HarmonyPatch(typeof(WorldGeneration))]
internal static class WorldGenerationBuildingPatches
{
    [HarmonyPatch("PlaceCrystals")]
    [HarmonyPostfix]
    private static void DistributeRegisteredBuildings(WorldGeneration __instance)
    {
        foreach (var id in BuildingEntityRegistry.GetRegisteredIds())
        {
            if (!BuildingEntityRegistry.TryGetDefinition(id, out var definition)) continue;
            if (definition.GenerationStyle == BuildingGenerationStyle.None) continue;

            BuildingEntityRegistry.DistributeInWorld(id, __instance);
        }
    }
}