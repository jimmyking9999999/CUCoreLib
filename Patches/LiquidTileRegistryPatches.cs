using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class LiquidTileRegistryPatches
    {
        [HarmonyPatch(typeof(WorldGeneration), "GenerateOres")]
        [HarmonyPostfix]
        private static void GenerateRegisteredLiquidTiles(WorldGeneration __instance)
        {
            LiquidTileRegistry.GenerateWorldTiles(__instance);
        }

        [HarmonyPatch(typeof(FluidManager), "Start")]
        [HarmonyPostfix]
        private static void ExpandFluidVisuals(FluidManager __instance)
        {
            LiquidTileRegistry.EnsureVisualCapacity(__instance);
        }

        [HarmonyPatch(typeof(FluidManager), nameof(FluidManager.RenderFluids))]
        [HarmonyPrefix]
        private static bool RenderCustomLiquidTiles(FluidManager __instance)
        {
            return !LiquidTileRegistry.RenderFluids(__instance);
        }

        [HarmonyPatch(typeof(FluidManager), nameof(FluidManager.WaterInfo))]
        [HarmonyPostfix]
        private static void CustomWaterInfo(FluidManager __instance, Vector2Int pos,
            ref (float buoyancy, float drag, int type) __result)
        {
            if (__instance == null) return;

            var worldByte = __instance.GetLiquid(pos.x, pos.y);
            if (LiquidTileRegistry.TryGetWaterInfo(worldByte, out var buoyancy, out var drag, out var type))
                __result = (buoyancy, drag, type);
        }

        [HarmonyPatch(typeof(FluidManager), nameof(FluidManager.LiquidColor))]
        [HarmonyPostfix]
        private static void CustomLiquidColor(FluidManager __instance, Vector2Int pos, ref Color __result)
        {
            if (__instance == null) return;

            var worldByte = __instance.GetLiquid(pos.x, pos.y);
            if (LiquidTileRegistry.TryGetDisplayColor(worldByte, out var color))
                __result = color;
        }

        [HarmonyPatch(typeof(FluidManager), nameof(FluidManager.LiquidName))]
        [HarmonyPostfix]
        private static void CustomLiquidName(FluidManager __instance, Vector2Int pos, ref (string, string) __result)
        {
            if (__instance == null) return;

            var worldByte = __instance.GetLiquid(pos.x, pos.y);
            if (LiquidTileRegistry.TryGetDisplayName(worldByte, out var name, out var description))
                __result = (name, description);
        }

        [HarmonyPatch(typeof(FluidManager), nameof(FluidManager.DrinkLiquid))]
        [HarmonyPrefix]
        private static bool DrinkCustomLiquid(FluidManager __instance, Vector2Int pos, Body body)
        {
            return !LiquidTileRegistry.TryDrinkLiquid(pos, body);
        }

        [HarmonyPatch(typeof(Body), "HandleVariableUpdates")]
        [HarmonyPostfix]
        private static void ApplyCustomLiquidTouch(Body __instance)
        {
            LiquidTileRegistry.ApplyBodyTouch(__instance, Time.deltaTime);
        }
    }
}
