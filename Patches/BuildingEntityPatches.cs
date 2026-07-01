using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches;

[HarmonyPatch(typeof(BuildingEntity))]
internal static class BuildingEntityPatches
{
    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    private static void PreserveRegisteredBuildingLocale(BuildingEntity __instance)
    {
        if (__instance == null ||
            !BuildingEntityRegistry.TryGetDefinition(__instance.id, out var definition)) return;

        if (!string.IsNullOrEmpty(definition.Name)) __instance.fullName = definition.Name;

        if (!__instance.skipDescriptionSet && !string.IsNullOrEmpty(definition.Description))
            __instance.description = definition.Description;
    }
}