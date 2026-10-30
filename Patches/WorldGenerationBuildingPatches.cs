using System.Collections;
using CUCoreLib.Data;
using CUCoreLib.Networking;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class WorldGenerationLootPoolPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldGeneration), "WorldPreprocess")]
        private static IEnumerator RefreshLootPools(IEnumerator __result)
        {
            // Run when the coroutine starts, before structures, corpses and trader stock are created.
            DropPoolRegistry.BeginGeneration();
            while (__result.MoveNext()) yield return __result.Current;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldGeneration), "Awake")]
        private static void ResetLootPools()
        {
            if (!MultiplayerBridge.IsRunning || MultiplayerBridge.IsServer)
                DropPoolRegistry.ResetGeneration();
        }
    }

    [HarmonyPatch(typeof(WorldGeneration), "Clear")]
    internal static class WorldGenerationCleanupPatches
    {
        [HarmonyPrefix]
        private static void ClearCUCoreLibWorldState()
        {
            BuildingEntityRegistry.ClearWorldInstances();
            LiquidTileRegistry.ClearWorldState();
            ItemRegistryPatches.ClearWorldState();
        }
    }

    [HarmonyPatch(typeof(WorldGeneration), "PlaceCrystals")]
    internal static class WorldGenerationBuildingPatches
    {
        [HarmonyPostfix]
        private static void DistributeRegisteredBuildings(WorldGeneration __instance)
        {
            foreach (var id in BuildingEntityRegistry.GetRegisteredIds())
            {
                if (!BuildingEntityRegistry.TryGetDefinition(id, out var definition))
                    continue;
                if (definition.GenerationStyle == BuildingGenerationStyle.None) continue;

                BuildingEntityRegistry.DistributeInWorld(id, __instance);
            }

            DropPoolRegistry.ScatterWorldSpawns(__instance);
        }
    }
}
