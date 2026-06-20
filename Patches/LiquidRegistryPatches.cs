using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class LiquidRegistryPatches
    {
        [HarmonyPatch(typeof(Liquids), nameof(Liquids.LiquidExists))]
        [HarmonyPostfix]
        private static void IncludeCustomLiquids(string id, ref bool __result)
        {
            if (__result) return;
            __result = LiquidRegistry.EnsureLiquidInjected(id);
        }
    }
}