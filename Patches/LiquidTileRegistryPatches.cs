using System;
using System.Collections;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class LiquidTileRegistryPatches
    {
        private static bool _generatingTerrain;
        private static bool _generatedLiquidTiles;

        [HarmonyPatch(typeof(WorldGeneration), "WorldGenerateTerrain")]
        [HarmonyPostfix]
        private static IEnumerator TrackTerrainGeneration(IEnumerator __result)
        {
            _generatingTerrain = true;
            _generatedLiquidTiles = false;
            try
            {
                while (__result.MoveNext()) yield return __result.Current;
            }
            finally
            {
                _generatingTerrain = false;
            }
        }

        [HarmonyPatch(typeof(WorldGeneration), "GenerateOres")]
        [HarmonyPostfix]
        private static void GenerateRegisteredLiquidTiles(WorldGeneration __instance)
        {
            if (_generatingTerrain && _generatedLiquidTiles) return;
            _generatedLiquidTiles = true;

            try
            {
                LiquidTileRegistry.GenerateWorldTiles(__instance);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogError("Failed to generate custom liquid tiles!.\n" + ex);
            }
        }

        [HarmonyPatch(typeof(FluidManager), "Start")]
        [HarmonyPostfix]
        private static void ExpandFluidVisuals(FluidManager __instance)
        {
            try
            {
                LiquidTileRegistry.EnsureVisualCapacity(__instance);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogError("Failed to finalize liquid visuals!\n" + ex);
            }
        }

        [HarmonyPatch(typeof(FluidManager), nameof(FluidManager.RenderFluids))]
        [HarmonyPrefix]
        private static bool RenderCustomLiquidTiles(FluidManager __instance)
        {
            return !LiquidTileRegistry.RenderFluids(__instance);
        }

        //Harmony reports pos.x/y are modified, `GetLiquid` doesn't modify
        #pragma warning disable Harmony003
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
        #pragma warning restore Harmony003

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
