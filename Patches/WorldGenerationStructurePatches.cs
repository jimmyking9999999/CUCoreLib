using System.Collections;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(WorldGeneration), "WorldGenerateWorldBorders")]
    internal static class WorldGenerationStructurePatches
    {
        [HarmonyPostfix]
        private static IEnumerator DistributeRegisteredStructures(IEnumerator __result, WorldGeneration __instance)
        {
            return StructureRegistry.GenerateRegisteredStructures(__result, __instance);
        }
    }
}