using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.ContentReload;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(ConsoleScript), "RegisterAllCommands")]
    internal static class ConsolePatch
    {
        private static readonly System.Reflection.FieldInfo RegisteredSpawnEntitiesField =
            AccessTools.Field(typeof(ConsoleScript), "registeredSpawnEntities");

        internal static void RefreshRuntimeAutofill()
        {
            RefreshSpawnAutofill();
            RefreshCustomSpawnAutofill();
            RefreshAddLiquidAutofill();
            RefreshDebugWatchAutofill();
            RefreshReloadContentAutofill();
            RefreshSetTileAutofill();
        }

        [HarmonyPostfix]
        private static void AddBuiltInCommands(ConsoleScript __instance)
        {
            var existingCustomSpawn = ConsoleScript.Commands.FirstOrDefault(c => c.name == "cuspawn");
            if (existingCustomSpawn != null) ConsoleScript.Commands.Remove(existingCustomSpawn);
            var existingSpawnCategory = ConsoleScript.Commands.FirstOrDefault(c => c.name == "spawncategory");
            if (existingSpawnCategory != null) ConsoleScript.Commands.Remove(existingSpawnCategory);
            var existingSetTile = ConsoleScript.Commands.FirstOrDefault(c => c.name == "settile");
            if (existingSetTile != null) ConsoleScript.Commands.Remove(existingSetTile);

            ConsoleScript.Commands.Add(new Command("spawncategory",
                "Spawns all items from a given loot pool with zero gravity.",
                delegate(string[] args)
                {
                    CUCoreUtils.ConsoleCheckForWorld(__instance);
                    if (args == null || args.Length < 2)
                        throw new Exception("Usage: spawncategory [category] [position] [modGUID]");

                    var category = args[1];
                    Vector2 position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    if (args.Length > 2)
                        position = ParsePositionOrThrow(__instance, args[2]);

                    var modGuid = args.Length > 3 ? args[3] : null;
                    var items = string.Equals(category, "modded", StringComparison.OrdinalIgnoreCase)
                        ? GetModdedSpawnCategoryIds(modGuid)
                        : (ItemLootPool.AllItemsFromPool(category) ??
                           throw new Exception("Invalid item category \"" + category + "\"."))
                        .Select(entry => entry.Item1);

                    items = items
                        .Select(SpawnIdHelpers.NormalizeSpawnId)
                        .Where(id => !string.IsNullOrWhiteSpace(id) && !BuildingEntityRegistry.IsRegistered(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var itemId in items)
                    {
                        var obj = Utils.Create(itemId,
                            position + UnityEngine.Random.insideUnitCircle * 3f, 0f);
                        var body = obj != null ? obj.GetComponent<Rigidbody2D>() : null;
                        if (body != null) body.gravityScale = 0f;
                    }

                    CUCoreUtils.ConsoleLog(__instance,
                        $"Spawned all items from category \"{category}\" at {position}.");
                }, BuildSpawnCategoryAutofill(), ("category", "The ID of the category to spawn from."),
                ("position", "Where to spawn the item"),
                ("modGUID", "Optional BepInEx plugin GUID filter for the modded category. Use normal for no filter.")));

            ConsoleScript.Commands.Add(new Command("cuspawn",
                "Spawns a vanilla prefab or CUCoreLib-registered item/building.",
                delegate(string[] args)
                {
                    if (args.Length < 2) throw new Exception("Usage: cuspawn [id]");

                    var query = args[1];
                    Vector2 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);  // maybe null
                    if (args.Length > 2 && TryParsePosition(__instance, args[2], out var parsedPosition))
                        pos = parsedPosition;

                    float? condition = null;
                    if (args.Length > 3 && float.TryParse(args[3], out var parsedCondition))
                        condition = parsedCondition;

                    var count = 1;
                    if (args.Length > 4) int.TryParse(args[4], out count);

                    var bestMatch = FindBestMatch(query);

                    if (string.IsNullOrEmpty(bestMatch))
                        throw new Exception($"Could not find entity '{query}'.");

                    var successCount = 0;
                    for (var i = 0; i < count; i++)
                    {
                        var obj = CustomInstantiate.InstantiateReturn(bestMatch, pos, Quaternion.identity, condition);
                        if (obj != null) successCount++;
                    }

                    // CUCoreLibPlugin.Log.LogInfo($"Spawned {successCount}x '{bestMatch}' at {pos}.");
                }, BuildCustomSpawnAutofill(), ("id", "Item or object ID."), ("position", "Spawn position."),
                ("condition", "Item condition."), ("count", "Number of objects to spawn.")));

            ConsoleScript.Commands.Add(new Command("settile",
                "Places a CUCoreLib-registered tile at the chosen block position.",
                delegate(string[] args)
                {
                    CUCoreUtils.ConsoleCheckForWorld(__instance);

                    if (args.Length < 2) throw new Exception("Usage: settile [tileIndex] [position]");
                    if (!ushort.TryParse(args[1], out var tileIndex))
                        throw new Exception($"'{args[1]}' is not a valid tile index.");

                    if (!TileRegistry.TryGetDefinition(tileIndex, out var definition))
                        throw new Exception($"Tile index '{tileIndex}' is not registered.");

                    Vector2 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);    // maybe null
                    if (args.Length > 2 && TryParsePosition(__instance, args[2], out var parsedPosition))
                        worldPosition = parsedPosition;

                    var blockPosition = WorldGeneration.world.WorldToBlockPos(worldPosition);
                    if (!TileRegistry.SetBlock(WorldGeneration.world, blockPosition, tileIndex))
                        throw new Exception($"Failed to place tile '{tileIndex}' at block {blockPosition}.");

                    CUCoreUtils.ConsoleLog(__instance,
                        $"Placed tile {tileIndex} ({definition.ID}) at {blockPosition.x},{blockPosition.y}.");
                }, BuildSetTileAutofill(), ("tileIndex", "Registered custom tile index."),
                ("position", "Tile position.")));

            ConsoleCommandRegistry.InjectRegisteredCommands();
            RefreshRuntimeAutofill();
        }

        private static Dictionary<int, List<string>> BuildSpawnAutofill()
        {
            var itemIds = new List<string>();

            foreach (var id in GetVanillaSpawnIds())
                if (!itemIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    itemIds.Add(id);

            foreach (var id in ItemRegistry.GetRegisteredItemIds())
                if (!itemIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    itemIds.Add(id);

            foreach (var id in BuildingEntityRegistry.GetRegisteredIds())
                if (!itemIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    itemIds.Add(id);

            return new Dictionary<int, List<string>>
            {
                { 0, itemIds }
            };
        }

        private static Dictionary<int, List<string>> BuildCustomSpawnAutofill()
        {
            var itemIds = new List<string>();

            foreach (var id in ItemRegistry.GetRegisteredItemIds())
                if (!itemIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    itemIds.Add(id);

            foreach (var id in BuildingEntityRegistry.GetRegisteredIds())
                if (!itemIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    itemIds.Add(id);

            return new Dictionary<int, List<string>>
            {
                { 0, itemIds }
            };
        }

        [HarmonyPatch(typeof(ConsoleScript), "RegisterSpawnEntities")]
        [HarmonyPrefix]
        private static bool GuardDuplicateVanillaSpawnAutofill(ConsoleScript __instance)
        {
            var spawnCommand = ConsoleScript.SearchExact("spawn");
            if (spawnCommand?.argAutofill == null) return true;
            if (!spawnCommand.argAutofill.ContainsKey(0)) return true;

            RegisteredSpawnEntitiesField?.SetValue(__instance, true);
            return false;
        }

        [HarmonyPatch(typeof(ConsoleScript), "RegisterSpawnEntities")]
        [HarmonyPostfix]
        private static void AppendSpawnAutofill(ConsoleScript __instance)
        {
            RefreshSpawnAutofill();
        }

        private static void RefreshSpawnAutofill()
        {
            var spawnCommand = ConsoleScript.SearchExact("spawn");
            if (spawnCommand == null) return;

            if (spawnCommand.argAutofill == null) spawnCommand.argAutofill = new Dictionary<int, List<string>>();

            if (!spawnCommand.argAutofill.TryGetValue(0, out var spawnIds))
            {
                spawnIds = new List<string>();
                spawnCommand.argAutofill[0] = spawnIds;
            }

            foreach (var id in BuildSpawnAutofill()[0].Where(id => !spawnIds.Contains(id, StringComparer.OrdinalIgnoreCase)))
                spawnIds.Add(id);
        }

        private static Dictionary<int, List<string>> BuildSpawnCategoryAutofill()
        {
            var categories = ItemLootPool.pool != null
                ? ItemLootPool.pool.Keys.ToList()
                : new List<string>();

            if (!categories.Contains("modded", StringComparer.OrdinalIgnoreCase))
                categories.Add("modded");

            return new Dictionary<int, List<string>>
            {
                { 0, categories },
                { 2, BuildReloadContentAutofill()[0].Prepend("normal").ToList() }
            };
        }

        private static void RefreshSpawnCategoryAutofill()
        {
            var spawnCategoryCommand = ConsoleScript.SearchExact("spawncategory");
            if (spawnCategoryCommand == null) return;

            spawnCategoryCommand.argAutofill = BuildSpawnCategoryAutofill();
        }

        private static bool HasRegisteredSpawnEntities(ConsoleScript console)
        {
            if (console == null) return false;

            return RegisteredSpawnEntitiesField != null && RegisteredSpawnEntitiesField.GetValue(console) is bool registered &&
                   registered;
        }

        private static void RefreshCustomSpawnAutofill()
        {
            var customSpawnCommand = ConsoleScript.SearchExact("cuspawn");
            if (customSpawnCommand == null) return;

            customSpawnCommand.argAutofill = BuildCustomSpawnAutofill();
        }

        private static IEnumerable<string> GetVanillaSpawnIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prefab in Resources.LoadAll<GameObject>(""))
            {
                if (prefab == null) continue;

                if (prefab.GetComponent<Item>() == null && prefab.GetComponent<BuildingEntity>() == null) continue;

                ids.Add(prefab.name);
            }

            return ids;
        }

        private static Dictionary<int, List<string>> BuildSetTileAutofill()
        {
            return new Dictionary<int, List<string>>
            {
                { 0, TileRegistry.GetRegisteredIndices().Select(index => index.ToString()).ToList() }
            };
        }

        private static void RefreshAddLiquidAutofill()
        {
            LiquidRegistry.InjectRegisteredLiquids(true);

            var addLiquidCommand = ConsoleScript.SearchExact("addliquid");
            if (addLiquidCommand == null) return;

            if (addLiquidCommand.argAutofill == null)
                addLiquidCommand.argAutofill = new Dictionary<int, List<string>>();

            if (!addLiquidCommand.argAutofill.TryGetValue(0, out var liquidIds))
            {
                liquidIds = new List<string>();
                addLiquidCommand.argAutofill[0] = liquidIds;
            }

            liquidIds.RemoveAll(id => LiquidRegistry.RegisteredLiquids.ContainsKey(id));

            foreach (var id in LiquidRegistry.GetRegisteredLiquidIds())
                if (!liquidIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    liquidIds.Add(id);
        }

        private static void RefreshSetTileAutofill()
        {
            var setTileCommand = ConsoleScript.SearchExact("settile");
            if (setTileCommand == null) return;

            setTileCommand.argAutofill = BuildSetTileAutofill();
        }

        private static Dictionary<int, List<string>> BuildReloadContentAutofill()
        {
            return new Dictionary<int, List<string>>
            {
                { 0, ContentReloadManager.GetLoadedModGuids().ToList() }
            };
        }

        private static Dictionary<int, List<string>> BuildDebugWatchAutofill()
        {
            return new Dictionary<int, List<string>>
            {
                { 0, DebugWatchService.GetRootAutofill().ToList() },
                { 1, DebugWatchService.GetAvailableWatchNames().ToList() }
            };
        }

        private static void RefreshReloadContentAutofill()
        {
            var reloadContentCommand = ConsoleScript.SearchExact("reloadcontent");
            if (reloadContentCommand == null) return;

            reloadContentCommand.argAutofill = BuildReloadContentAutofill();
        }

        private static void RefreshDebugWatchAutofill()
        {
            var debugWatchCommand = ConsoleScript.SearchExact("debugwatch");
            if (debugWatchCommand == null) return;

            debugWatchCommand.argAutofill = BuildDebugWatchAutofill();
        }

        private static string FindBestMatch(string query)
        {
            // 1. Exact Match (Fastest)
            if (ItemRegistry.RegisteredItems.ContainsKey(query)) return query;
            if (BuildingEntityRegistry.IsRegistered(query) || Resources.Load<GameObject>(query) != null) return query;

            // 2. Build List
            var candidates = new List<string>();
            candidates.AddRange(ItemRegistry.RegisteredItems.Keys);
            candidates.AddRange(BuildingEntityRegistry.GetRegisteredIds());
            ResourceCache.TryInitialize();
            candidates.AddRange(ResourceCache.AllPrefabs.Keys);

            // 3. Levenshtein Search
            return FindClosestMatch(query, candidates);
        }

        private static bool TryParsePosition(ConsoleScript console, string value, out Vector2 position)
        {
            position = default;
            var parsePosition = AccessTools.Method(typeof(ConsoleScript), "ParsePosition", new[] { typeof(string) });
            if (console == null || parsePosition == null) return false;

            try
            {
                position = (Vector2)parsePosition.Invoke(console, new object[] { value });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Vector2 ParsePositionOrThrow(ConsoleScript console, string value)
        {
            var parsePosition = AccessTools.Method(typeof(ConsoleScript), "ParsePosition", new[] { typeof(string) });
            if (console == null || parsePosition == null)
                throw new Exception("Could not access ConsoleScript.ParsePosition().");

            return (Vector2)parsePosition.Invoke(console, new object[] { value });
        }

        private static void EnsureArgumentCount(ConsoleScript console, string[] args, int desired)
        {
            var checkArgumentCount = AccessTools.Method(typeof(ConsoleScript), "CheckArgumentCount",
                new[] { typeof(string[]), typeof(int) });
            if (console == null || checkArgumentCount == null)
                throw new Exception("Could not access ConsoleScript.CheckArgumentCount().");

            checkArgumentCount.Invoke(console, new object[] { args, desired });
        }

        private static List<string> GetModdedSpawnCategoryIds(string modGuid = null)
        {
            var moddedIds = new List<string>();
            var normalizedModGuid = NormalizeSpawnCategoryModGuid(modGuid);
            var ownerFilteredIds = normalizedModGuid == null
                ? null
                : new HashSet<string>(ItemRegistry.GetRegisteredItemIdsForOwner(normalizedModGuid),
                    StringComparer.OrdinalIgnoreCase);

            if (Item.GlobalItems != null)
                foreach (var id in Item.GlobalItems.Keys)
                    if (CUCoreUtils.IsModdedItem(id) &&
                        (ownerFilteredIds == null || ownerFilteredIds.Contains(id)) &&
                        !moddedIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                        moddedIds.Add(id);

            foreach (var id in ItemRegistry.GetRegisteredItemIds())
                if (CUCoreUtils.IsModdedItem(id) &&
                    (ownerFilteredIds == null || ownerFilteredIds.Contains(id)) &&
                    !moddedIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    moddedIds.Add(id);

            return moddedIds;
        }

        private static string NormalizeSpawnCategoryModGuid(string modGuid)
        {
            if (string.IsNullOrWhiteSpace(modGuid)) return null;

            var normalized = modGuid.Trim();
            return string.Equals(normalized, "normal", StringComparison.OrdinalIgnoreCase)
                ? null
                : normalized;
        }

        private static string FindClosestMatch(string query, List<string> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            string bestMatch = null;
            var lowestDistance = int.MaxValue;
            var lowerQuery = query.ToLower();

            foreach (var candidate in candidates)
            {
                var lowerCandidate = candidate.ToLower();

                // Exact match ignore case
                if (lowerCandidate == lowerQuery) return candidate;

                // Substring match priority (e.g. "sword" matches "cardboard_sword")
                if (lowerCandidate.Contains(lowerQuery))
                {
                    var dist = Math.Abs(candidate.Length - query.Length);
                    if (dist < lowestDistance)
                    {
                        lowestDistance = dist;
                        bestMatch = candidate;
                    }

                    continue;
                }

                // Levenshtein Math
                var levDist = LevenshteinDistance(lowerQuery, lowerCandidate);
                if (levDist >= lowestDistance) continue;
                lowestDistance = levDist;
                bestMatch = candidate;
            }

            return bestMatch;
        }

        public static int LevenshteinDistance(string s, string t)
        {
            var n = s.Length;
            var m = t.Length;
            var d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (var i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (var j = 0; j <= m; d[0, j] = j++)
            {
            }

            for (var i = 1; i <= n; i++)
            for (var j = 1; j <= m; j++)
            {
                var cost = t[j - 1] == s[i - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }

            return d[n, m];
        }

        [HarmonyPatch(typeof(ConsoleScript), "RegisterPlayerDetails")]
        internal static class SpawnCategoryAutofillPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                RefreshSpawnCategoryAutofill();
            }
        }

        [HarmonyPatch(typeof(ConsoleScript), "RegisterAllCommands")]
        internal static class LiquidAutofillPatch
        {
            [HarmonyPostfix]
            private static void AddCustomLiquidsToAutofill()
            {
                RefreshAddLiquidAutofill();
            }
        }
    }
}
