using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using CUCoreLib.Patches;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Random = System.Random;

namespace CUCoreLib.Registries
{
    internal static class DropPoolRegistry
    {
        private static readonly Dictionary<DropPool, List<string>> ExplicitPools =
            new Dictionary<DropPool, List<string>>();

        private static readonly Random PoolRandom = new Random();

        private static readonly Dictionary<string, Dictionary<string, double>>
            GenerationRolls =
                new Dictionary<string, Dictionary<string, double>>(StringComparer
                    .OrdinalIgnoreCase);

        private static readonly Dictionary<string, Dictionary<string, int>>
            ResolvedCounts =
                new Dictionary<string, Dictionary<string, int>>(StringComparer
                    .OrdinalIgnoreCase);

        private static readonly Dictionary<string, Dictionary<string, int>> HostCounts =
            new Dictionary<string, Dictionary<string, int>>(StringComparer
                .OrdinalIgnoreCase);

        private static string generationId;
        private static bool generationActive;

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

        private static bool IsRemoteClient =>
            MultiplayerBridge.IsRunning && !MultiplayerBridge.IsServer;

        internal static float ValidateSpawnFrequency(string id, float frequency)
        {
            if (float.IsNaN(frequency)
                || float.IsInfinity(frequency)
                || Math.Ceiling(frequency) > int.MaxValue)
            {
                CUCoreLibPlugin.Log?.LogWarning("Custom item '"
                    + id
                    + "' disabled pooled spawning: spawnFrequency must be finite and fit an integer entry count.");
                return 0f;
            }

            return Math.Max(0f, frequency);
        }

        internal static int GetEntryCount(string id, CustomItemInfo info, string poolKey)
        {
            id = SpawnIdHelpers.NormalizeSpawnId(id);
            // Keep the legacy integer path exact, including values not exactly representable as floats.
            var frequency = info.FractionalSpawnFrequency.HasValue
                ? (double)info.FractionalSpawnFrequency.Value
                : Math.Max(0, info.SpawnFrequency);
            var count = (int)Math.Floor(frequency);
            var remainder = frequency - count;

            if (IsRemoteClient)
            {
                if (HostCounts.TryGetValue(id, out var pools)
                    && pools.TryGetValue(poolKey, out var hostCount)
                    && hostCount >= count
                    && hostCount <= Math.Ceiling(frequency))
                    count = hostCount;
            }
            else if (generationActive && remainder > 0d)
            {
                if (!GenerationRolls.TryGetValue(id, out var rolls))
                    GenerationRolls[id] = rolls =
                        new Dictionary<string, double>(StringComparer.Ordinal);
                if (!rolls.TryGetValue(poolKey, out var roll))
                    rolls[poolKey] = roll = PoolRandom.NextDouble();
                if (roll < remainder) count++;
            }

            if (!ResolvedCounts.TryGetValue(id, out var counts))
                ResolvedCounts[id] =
                    counts = new Dictionary<string, int>(StringComparer.Ordinal);
            counts[poolKey] = count;
            return count;
        }

        internal static void BeginGeneration()
        {
            if (IsRemoteClient)
            {
                // A snapshot may precede local world initialization. Reuse it and request the current host state.
                RefreshPools();
                MultiplayerSyncRegistry.RequestInitialSnapshotForNewSession();
                return;
            }

            generationId = Guid.NewGuid().ToString("N");
            generationActive = true;
            GenerationRolls.Clear();
            HostCounts.Clear();
            RefreshPools();
            if (MultiplayerBridge.IsServer) MultiplayerSyncRegistry.BroadcastSnapshot();
        }

        internal static void ResetGeneration()
        {
            generationId = null;
            generationActive = false;
            GenerationRolls.Clear();
            HostCounts.Clear();
            RefreshPools();
        }

        private static void RefreshPools()
        {
            Rebuild();
            foreach (var entry in ItemRegistry.RegisteredItems.OrderBy(entry => entry.Key,
                StringComparer.Ordinal))
                ItemLootPoolPatch.EnsureItemInLootPool(entry.Key, entry.Value);
        }

        internal static JObject CaptureNetworkSnapshot()
        {
            var items = new JObject();
            foreach (var item in ResolvedCounts.OrderBy(entry => entry.Key,
                StringComparer.Ordinal))
            {
                var pools = new JObject();
                foreach (var pool in item.Value.OrderBy(entry => entry.Key,
                    StringComparer.Ordinal))
                    pools[pool.Key] = pool.Value;
                items[item.Key] = pools;
            }

            return new JObject { ["generation"] = generationId, ["items"] = items };
        }

        internal static void ApplyNetworkSnapshot(JObject snapshot)
        {
            if (!IsRemoteClient || !(snapshot?["items"] is JObject items)) return;

            HostCounts.Clear();
            generationId = snapshot.Value<string>("generation");
            foreach (var item in items.Properties())
            {
                if (string.IsNullOrWhiteSpace(item.Name)
                    || !(item.Value is JObject pools)) continue;
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var pool in pools.Properties())
                {
                    if (pool.Value.Type != JTokenType.Integer) continue;
                    if (int.TryParse(pool.Value.ToString(), out var count) && count >= 0)
                        counts[pool.Name] = count;
                }

                HostCounts[SpawnIdHelpers.NormalizeSpawnId(item.Name)] = counts;
            }

            RefreshPools();
        }

        internal static void Rebuild()
        {
            ExplicitPools.Clear();
            WorldSpawnConfigs.Clear();
            ResolvedCounts.Clear();

            if (ItemRegistry.RegisteredItems == null) return;

            foreach (var entry in ItemRegistry.RegisteredItems.OrderBy(entry => entry.Key,
                StringComparer.Ordinal))
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
                poolItems.RemoveAll(itemId => string.Equals(itemId, normalizedId,
                    StringComparison.OrdinalIgnoreCase));

            WorldSpawnConfigs.Remove(normalizedId);
            ResolvedCounts.Remove(normalizedId);
            // Keep fractional draws through unregister/re-register cycles during content reload.
        }

        internal static bool UsesVanillaCategoryFallback(ItemInfo info)
        {
            if (!(info is CustomItemInfo customInfo)) return true;

            return !customInfo.DropPool.HasValue
                && !customInfo.WorldSpawnPerChunk.HasValue;
        }

        internal static bool TryGetRandomItemId(DropPool source, string fallbackCategory,
            out string itemId)
        {
            itemId = null;

            List<string> fallbackItems = null;
            if (!string.IsNullOrWhiteSpace(fallbackCategory) && ItemLootPool.pool != null)
                ItemLootPool.pool.TryGetValue(fallbackCategory, out fallbackItems);

            if (fallbackItems != null && fallbackItems.Count == 0) fallbackItems = null;

            ExplicitPools.TryGetValue(source, out var explicitItems);
            if (explicitItems != null && explicitItems.Count == 0) explicitItems = null;

            var fallbackCount = fallbackItems?.Count ?? 0;
            var explicitCount = explicitItems?.Count ?? 0;
            var totalCount = fallbackCount + explicitCount;
            if (totalCount == 0) return false;

            var index = UnityEngine.Random.Range(0, totalCount);
            if (fallbackItems != null && explicitItems != null)
                itemId = index < fallbackCount
                    ? fallbackItems[index]
                    : explicitItems[index - fallbackCount];
            return !string.IsNullOrWhiteSpace(itemId);
        }

        internal static void ScatterWorldSpawns(WorldGeneration world)
        {
            if (world == null
                || world.biomeOverride != WorldGeneration.OverrideSceneType.None) return;
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
            switch (info?.DropPool)
            {
                case DropPool.Corpse:
                case DropPool.MedicalCrate:
                case DropPool.FoodCrate:
                case DropPool.ContainerCrate:
                case DropPool.Trader1:
                case DropPool.Trader2:
                case DropPool.Trader3:
                case DropPool.AllTraders:
                case DropPool.DropCapsule:
                case DropPool.CapsuleContainer:
                case DropPool.All:
                    break;
                case null:
                case DropPool.None:
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            foreach (var source in SingleSourceFlags)
            {
                if (!info.DropPool.Value.HasFlag(source)) continue;

                var frequency = GetEntryCount(id, info, "source:" + source);

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

            var hit = Physics2D.Raycast(randomPos, Vector2.down,
                WorldGeneration.CHUNKSIZE, GroundMask);
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
