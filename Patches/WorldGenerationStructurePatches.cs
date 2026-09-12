using System.Collections;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    internal static class WorldGenerationStructurePatches
    {
        [HarmonyPatch(typeof(WorldGeneration), "WorldGenerateWorldBorders")]
        [HarmonyPostfix]
        private static IEnumerator DistributeRegisteredStructures(IEnumerator __result, WorldGeneration __instance)
        {
            return StructureRegistry.GenerateRegisteredStructures(__result, __instance);
        }
    }
}