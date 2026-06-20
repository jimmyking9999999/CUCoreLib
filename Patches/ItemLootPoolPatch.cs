using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class ItemLootPoolPatch
    {
        private static bool hasLoggedInjection;

        [HarmonyPatch(typeof(ItemLootPool), nameof(ItemLootPool.InitializePool))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void InjectLoot()
        {
            // This runs twice due to mod compatibility reasons

            if (ItemLootPool.pool == null || ItemRegistry.RegisteredItems == null) return;

            var injectedCount = ItemRegistry.RegisteredItems
                .Sum(kvp => EnsureItemInLootPool(kvp.Key, kvp.Value));

            if (injectedCount <= 0) return;

            if (!hasLoggedInjection)
            {
                hasLoggedInjection = true;
            }

            CUCoreLibPlugin.Log.LogInfo($"LootPool injected {injectedCount} custom items.");
        }

        internal static int EnsureItemInLootPool(string id, ItemInfo def)
        {
            if (ItemLootPool.pool == null || string.IsNullOrWhiteSpace(id) || def == null) return 0;

            var category = def.category;
            if (string.IsNullOrEmpty(category) || category == "nospawn") return 0;

            RemoveExistingEntries(id);

            if (!ItemLootPool.pool.ContainsKey(category)) ItemLootPool.pool.Add(category, new List<string>());

            var frequency = 1;
            if (def is CustomItemInfo customInfo) frequency = Mathf.Max(0, customInfo.SpawnFrequency);

            for (var i = 0; i < frequency; i++) ItemLootPool.pool[category].Add(id);

            return frequency;
        }

        private static void RemoveExistingEntries(string id)
        {
            if (ItemLootPool.pool == null || string.IsNullOrWhiteSpace(id)) return;

            foreach (var poolItems in ItemLootPool.pool.Values)
                poolItems.RemoveAll(itemId => string.Equals(itemId, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}