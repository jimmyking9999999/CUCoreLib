using CUCoreLib.Registries;
using CUCoreLib.Networking;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class TileRegistryPatches
    {
        [HarmonyPatch(typeof(WorldGeneration), "Awake")]
        [HarmonyPostfix]
        private static void InjectCustomTiles(WorldGeneration __instance)
        {
            TileRegistry.InjectRegisteredTiles(__instance);
        }

        [HarmonyPatch(typeof(WorldGeneration), "GenerateOres")]
        [HarmonyPostfix]
        private static void GenerateRegisteredTileOres(WorldGeneration __instance)
        {
            TileRegistry.GenerateWorldTiles(__instance);
        }

        [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.GetBlockInfo))]
        [HarmonyPrefix]
        private static bool GetCustomBlockInfo(ushort block, ref BlockInfo __result)
        {
            if (!TileRegistry.TryGetDefinition(block, out var definition)) return true;

            __result = TileRegistry.CreateBlockInfo(block, definition);
            return false;
        }

        [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.DamageBlock), typeof(Vector2Int), typeof(float),
            typeof(bool), typeof(bool), typeof(bool))]
        [HarmonyPrefix]
        private static void TrackCustomTileBreak(
            WorldGeneration __instance,
            Vector2Int pos,
            float dmg,
            bool bonusMetal,
            bool ignoreLoot,
            out BreakDropState __state)
        {
            __state = null;
            if (__instance == null || ignoreLoot) return;

            var tileIndex = __instance.GetBlock(pos);
            if (!TileRegistry.TryGetDefinition(tileIndex, out var definition)) return;
            if (definition.Drops == null || definition.Drops.Length == 0) return;

            __state = new BreakDropState
            {
                TileIndex = tileIndex,
                ShouldSpawn = TileRegistry.WillBreak(__instance, pos, dmg, bonusMetal)
            };
        }

        [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.DamageBlock), typeof(Vector2Int), typeof(float),
            typeof(bool), typeof(bool), typeof(bool))]
        [HarmonyPostfix]
        private static void SpawnCustomTileDrops(WorldGeneration __instance, Vector2Int pos, BreakDropState __state)
        {
            if (__state == null || !__state.ShouldSpawn || __instance == null || __instance.GetBlock(pos) != 0)
            {
                return;
            }

            // Multiplayer clients replay block damage from the server for visuals only.
            if (MultiplayerBridge.IsAvailable && MultiplayerBridge.IsClient)
            {
                return;
            }

            TileRegistry.SpawnDrops(__instance, pos, __state.TileIndex);
        }

        private sealed class BreakDropState
        {
            public bool ShouldSpawn;
            public ushort TileIndex;
        }
    }
}