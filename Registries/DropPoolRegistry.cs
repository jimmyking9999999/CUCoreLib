using System;
using System.Collections.Generic;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using UnityEngine;

namespace CUCoreLib.Registries
{
    internal static class DropPoolRegistry
    {
        private static readonly Dictionary<DropPool, List<string>> ExplicitPools =
            new Dictionary<DropPool, List<string>>();

        private static readonly Dictionary<string, WorldSpawnConfig> WorldSpawnConfigs =
            new Dictionary<string, WorldSpawnConfig>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> WarnedInvalidWorldSpawn =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly DropPool[] SingleSourceFlags =
        {
            DropPool.Corpse,
            DropPool.MedicalCrate,
            DropPool.FoodCrate,
            DropPool.ContainerCrate,
            DropPool.Trader1,
            DropPool.Trader2,
            DropPool.Trader3,
            DropPool.DropCapsule,
            DropPool.CapsuleContainer
        };

        private static readonly int GroundMask = LayerMask.GetMask("Ground");

        internal static void Rebuild()
        {
            ExplicitPools.Clear();
            WorldSpawnConfigs.Clear();

            if (ItemRegistry.RegisteredItems == null) return;

            foreach (var entry in ItemRegistry.RegisteredItems)
                RegisterItem(entry.Key, entry.Value);
        }

        internal static void RegisterItem(string id, CustomItemInfo info)
        {
            RemoveItem(id);

            if (string.IsNullOrWhiteSpace(id) || info == null) return;

            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(id);
            RegisterFixedSources(normalizedId, info);
            RegisterWorldSpawn(normalizedId, info);
        }

        internal static void RemoveItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(id);

            foreach (var poolItems in ExplicitPools.Values)
                poolItems.RemoveAll(itemId => string.Equals(itemId, normalizedId, StringComparison.OrdinalIgnoreCase));

            WorldSpawnConfigs.Remove(normalizedId);
        }

        internal static bool UsesVanillaCategoryFallback(ItemInfo info)
        {
            if (!(info is CustomItemInfo customInfo)) return true;

            return !customInfo.DropPool.HasValue &&
                   !customInfo.WorldSpawnPerChunk.HasValue;
        }

        internal static bool TryGetRandomItemId(DropPool source, string fallbackCategory, out string itemId)
        {
            itemId = null;

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(fallbackCategory) &&
                ItemLootPool.pool != null &&
                ItemLootPool.pool.TryGetValue(fallbackCategory, out var fallbackItems) &&
                fallbackItems != null &&
                fallbackItems.Count > 0)
                candidates.AddRange(fallbackItems);

            if (ExplicitPools.TryGetValue(source, out var explicitItems) &&
                explicitItems != null &&
                explicitItems.Count > 0)
                candidates.AddRange(explicitItems);

            if (candidates.Count == 0) return false;

            itemId = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return !string.IsNullOrWhiteSpace(itemId);
        }

        internal static void ScatterWorldSpawns(WorldGeneration world)
        {
            if (world == null || world.biomeOverride != WorldGeneration.OverrideSceneType.None) return;
            if (WorldSpawnConfigs.Count == 0) return;

            foreach (var entry in WorldSpawnConfigs)
            {
                var count = Mathf.RoundToInt(
                    world.chunkWidth * world.chunkHeight * entry.Value.PerChunk);

                for (var i = 0; i < count; i++)
                    TrySpawnLooseWorldItem(world, entry.Key);
            }
        }

        private static void RegisterFixedSources(string id, CustomItemInfo info)
        {
            if (info == null || !info.DropPool.HasValue) return;

            var frequency = Mathf.Max(0, info.SpawnFrequency);
            if (frequency <= 0 || info.DropPool.Value == DropPool.None) return;

            foreach (var source in SingleSourceFlags)
            {
                if (!info.DropPool.Value.HasFlag(source)) continue;

                if (!ExplicitPools.TryGetValue(source, out var poolItems))
                {
                    poolItems = new List<string>();
                    ExplicitPools[source] = poolItems;
                }

                for (var i = 0; i < frequency; i++)
                    poolItems.Add(id);
            }
        }

        private static void RegisterWorldSpawn(string id, CustomItemInfo info)
        {
            if (info == null || !info.WorldSpawnPerChunk.HasValue) return;

            var perChunk = info.WorldSpawnPerChunk.Value;
            if (perChunk < 0f)
            {
                WarnInvalidWorldSpawnConfig(id,
                    "DropPool world spawn requires WorldSpawnPerChunk >= 0.");
                return;
            }

            WorldSpawnConfigs[id] = new WorldSpawnConfig(perChunk);
        }

        private static void WarnInvalidWorldSpawnConfig(string id, string message)
        {
            if (!WarnedInvalidWorldSpawn.Add(id)) return;

            CUCoreLibPlugin.Log?.LogWarning(
                "Custom item '" + id + "' skipped world-spawn registration. " + message);
        }

        private static void TrySpawnLooseWorldItem(WorldGeneration world, string itemId)
        {
            var randomPos = new Vector2(
                UnityEngine.Random.Range(-(float)world.halfWidth, world.halfWidth),
                UnityEngine.Random.Range(-(float)world.halfHeight, world.halfHeight));

            if (Physics2D.OverlapPoint(randomPos, GroundMask)) return;

            var hit = Physics2D.Raycast(randomPos, Vector2.down, WorldGeneration.CHUNKSIZE, GroundMask);
            if (!hit) return;

            var instance = CustomInstantiate.InstantiateReturn(
                itemId,
                hit.point + Vector2.up,
                Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)),
                1f);

            if (instance == null) return;

            if (instance.TryGetComponent<Item>(out var item)) item.SetCondition(1f);
        }

        private readonly struct WorldSpawnConfig
        {
            internal readonly float PerChunk;

            internal WorldSpawnConfig(float perChunk)
            {
                PerChunk = perChunk;
            }
        }
    }
}
