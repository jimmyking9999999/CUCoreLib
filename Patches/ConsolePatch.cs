using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
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

        private static Command healCommand;
        private static Command setBodyFieldCommand;
        private static Command setLimbFieldCommand;

        internal static void RefreshRuntimeAutofill()
        {
            RefreshSpawnAutofill();
            RefreshCustomSpawnAutofill();
            RefreshAddLiquidAutofill();
            RefreshFloodFillAutofill();
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
                "Places a vanilla or CUCoreLib-registered tile at the chosen block position.",
                delegate(string[] args)
                {
                    CUCoreUtils.ConsoleCheckForWorld(__instance);

                    if (args.Length < 2) throw new Exception("Usage: settile [tileIndex or tileId] [position]");

                    var customTile = TileRegistry.TryGetIndex(args[1], out var tileIndex);
                    if (!customTile && !ushort.TryParse(args[1], out tileIndex))
                        throw new Exception($"'{args[1]}' is not a valid tile index or registered tile ID.");

                    Vector2 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);    // maybe null
                    if (args.Length > 2 && TryParsePosition(__instance, args[2], out var parsedPosition))
                        worldPosition = parsedPosition;

                    var blockPosition = WorldGeneration.world.WorldToBlockPos(worldPosition);
                    string tileLabel;
                    if (!customTile && tileIndex < TileRegistry.FirstCustomTileIndex)
                    {
                        WorldGeneration.world.SetBlock(blockPosition, tileIndex);
                        tileLabel = "vanilla";
                    }
                    else
                    {
                        if (!TileRegistry.TryGetDefinition(tileIndex, out var definition))
                            throw new Exception($"Tile index '{tileIndex}' is not registered.");

                        if (!TileRegistry.SetBlock(WorldGeneration.world, blockPosition, tileIndex))
                            throw new Exception($"Failed to place tile '{tileIndex}' at block {blockPosition}.");

                        tileLabel = definition.ID;
                    }

                    CUCoreUtils.ConsoleLog(__instance,
                        $"Placed tile {tileIndex} ({tileLabel}) at {blockPosition.x},{blockPosition.y}.");
                }, BuildSetTileAutofill(), ("tileIndex", "Vanilla index or registered custom tile ID/index."),
                ("position", "Tile position.")));

            ConsoleCommandRegistry.InjectRegisteredCommands();
            RefreshRuntimeAutofill();
            HookHealCommand();
            HookStatusFieldCommands(__instance);
        }

        [HarmonyPatch(typeof(ConsoleScript), "TryExecuteCommand")]
        [HarmonyPostfix]
        private static void NotifyMultiplayerHeal(string[] args)
        {
            if (args == null || args.Length == 0 || !string.Equals(args[0], "heal", StringComparison.OrdinalIgnoreCase) ||
                !MultiplayerBridge.IsRunning || !MultiplayerBridge.IsServer || args.Length > 1) return;

            PlayerEventPatches.NotifyHeal(PlayerCamera.main != null ? PlayerCamera.main.body : null);
        }

        private static void HookHealCommand()
        {
            var command = ConsoleScript.SearchExact("heal");
            if (command == null || ReferenceEquals(command, healCommand)) return;

            var originalAction = command.action;
            command.action = args =>
            {
                originalAction?.Invoke(args);
                if (!MultiplayerBridge.IsRunning)
                    PlayerEventPatches.NotifyHeal(PlayerCamera.main != null ? PlayerCamera.main.body : null);
            };
            healCommand = command;
        }

        private static void HookStatusFieldCommands(ConsoleScript console)
        {
            var bodyCommand = ConsoleScript.SearchExact("setbodyfield");
            if (bodyCommand != null && !ReferenceEquals(bodyCommand, setBodyFieldCommand))
            {
                var originalAction = bodyCommand.action;
                bodyCommand.action = args =>
                {
                    if (TrySetBodyStatusField(console, args)) return;
                    originalAction?.Invoke(args);
                };
                setBodyFieldCommand = bodyCommand;
            }

            var limbCommand = ConsoleScript.SearchExact("setlimbfield");
            if (limbCommand != null && !ReferenceEquals(limbCommand, setLimbFieldCommand))
            {
                var originalAction = limbCommand.action;
                limbCommand.action = args =>
                {
                    if (TrySetLimbStatusField(console, args)) return;
                    originalAction?.Invoke(args);
                };
                setLimbFieldCommand = limbCommand;
            }
        }

        private static bool TrySetBodyStatusField(ConsoleScript console, string[] args)
        {
            if (args == null || args.Length < 3 || typeof(Body).GetField(args[1]) != null) return false;

            var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (body == null || !TrySetStatusField(StatusRegistry.EnumerateBodyStatuses(body), args[1], args[2],
                    out var value)) return false;

            CUCoreUtils.ConsoleLog(console, "Set player body field \"" + args[1] + "\" to \"" + value + "\".");
            return true;
        }

        private static bool TrySetLimbStatusField(ConsoleScript console, string[] args)
        {
            if (args == null || args.Length < 4 || typeof(Limb).GetField(args[2]) != null) return false;

            var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            var limb = body != null ? body.LimbByName(args[1]) : null;
            if (limb == null || !TrySetStatusField(StatusRegistry.EnumerateLimbStatuses(limb), args[2], args[3],
                    out var value)) return false;

            CUCoreUtils.ConsoleLog(console,
                "Set \"" + limb.fullName + "\" field \"" + args[2] + "\" to \"" + value + "\".");
            return true;
        }

        private static bool TrySetStatusField<TStatus>(IEnumerable<KeyValuePair<Type, TStatus>> statuses,
            string fieldQuery, string rawValue, out object value) where TStatus : StatusBase
        {
            value = null;
            if (statuses == null || string.IsNullOrWhiteSpace(fieldQuery)) return false;

            var separator = fieldQuery.LastIndexOf('.');
            var statusName = separator > 0 ? fieldQuery.Substring(0, separator) : null;
            var fieldName = separator > 0 ? fieldQuery.Substring(separator + 1) : fieldQuery;
            FieldInfo matchedField = null;
            TStatus matchedStatus = null;

            foreach (var entry in statuses)
            {
                if (entry.Value == null || (statusName != null && !StatusNameMatches(entry.Key, statusName))) continue;

                var field = entry.Key.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
                if (field == null) continue;

                if (matchedField != null)
                    throw new Exception("Status field \"" + fieldQuery +
                                        "\" is ambiguous. Use StatusType.Field to choose one.");

                matchedField = field;
                matchedStatus = entry.Value;
            }

            if (matchedField == null) return false;

            value = TypeDescriptor.GetConverter(matchedField.FieldType).ConvertFromInvariantString(rawValue);
            matchedField.SetValue(matchedStatus, value);
            return true;
        }

        private static bool StatusNameMatches(Type statusType, string statusName)
        {
            if (statusType == null) return false;

            if (string.Equals(statusType.Name, statusName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(statusType.FullName, statusName, StringComparison.OrdinalIgnoreCase)) return true;

            var options = statusType.GetCustomAttribute<StatusOptionsAttribute>();
            return options != null && string.Equals(options.Key, statusName, StringComparison.OrdinalIgnoreCase);
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
                {
                    0,
                    Enumerable.Range(0, TileRegistry.FirstCustomTileIndex)
                        .Select(index => index.ToString())
                        .Concat(TileRegistry.GetRegisteredIds())
                        .Concat(TileRegistry.GetRegisteredIndices().Select(index => index.ToString()))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                }
            };
        }

        private static Dictionary<int, List<string>> BuildFloodFillAutofill()
        {
            var values = new HashSet<byte>();

            for (var liquidByte = 1; liquidByte <= 6; liquidByte++)
                values.Add((byte)liquidByte);

            foreach (var id in LiquidTileRegistry.GetRegisteredIds())
            {
                if (!LiquidTileRegistry.TryGetWorldByte(id, out var worldByte)) continue;
                if (worldByte == 0) continue;
                values.Add(worldByte);
            }

            var sortedValues = values
                .OrderBy(value => value)
                .Select(value => value.ToString())
                .ToList();

            return new Dictionary<int, List<string>>
            {
                { 0, new List<string> { "cursor", "player", "random", "#.#" } },
                { 1, sortedValues }
            };
        }

        private static bool TryGetVanillaTileVisual(ushort tileIndex, out string tileId, out string displayName)
        {
            tileId = null;
            displayName = null;

            switch (tileIndex)
            {
                case 0:
                    tileId = "air";
                    break;
                case 1:
                    tileId = "lightrock";
                    break;
                case 2:
                    tileId = "gravel";
                    break;
                case 3:
                    tileId = "scrappile";
                    break;
                case 4:
                    tileId = "trashpile";
                    break;
                case 5:
                    tileId = "concretetile";
                    break;
                case 6:
                    tileId = "steeltile";
                    break;
                case 7:
                    tileId = "glass";
                    break;
                case 8:
                    tileId = "rubber";
                    break;
                case 9:
                    tileId = "plastic";
                    break;
                case 10:
                    tileId = "heatresistantalloy";
                    break;
                case 11:
                    tileId = "wood";
                    break;
                case 12:
                    tileId = "sand";
                    break;
                case 13:
                    tileId = "sandstone";
                    break;
                case 14:
                    tileId = "infinirock";
                    break;
                case 15:
                    tileId = "clay";
                    break;
                case 16:
                    tileId = "soil";
                    break;
                case 17:
                    tileId = "granite";
                    break;
                case 18:
                    tileId = "marble";
                    break;
                case 19:
                    tileId = "limestone";
                    break;
                case 20:
                    tileId = "bricks";
                    break;
                case 21:
                    tileId = "scaffolding";
                    break;
                case 22:
                    tileId = "toxirock";
                    break;
                case 23:
                    tileId = "grass";
                    break;
                case 24:
                    tileId = "log";
                    break;
                case 25:
                    tileId = "leaves";
                    break;
                case 26:
                    tileId = "snow";
                    break;
                case 27:
                    tileId = "ice";
                    break;
                case 28:
                    tileId = "thinice";
                    break;
                case 29:
                    tileId = "powdersnow";
                    break;
                case 30:
                    tileId = "heavyrock";
                    break;
                case 31:
                    tileId = "fungus";
                    break;
                case 32:
                    tileId = "mushroombody";
                    break;
                case 33:
                    tileId = "mushroomcap";
                    break;
                case 34:
                    tileId = "copper";
                    break;
                case 35:
                    tileId = "ilmenite";
                    break;
                default:
                    return false;
            }

            displayName = Locale.GetOther(tileId);
            return true;
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

        private static void RefreshFloodFillAutofill()
        {
            var floodFillCommand = ConsoleScript.SearchExact("floodfill");
            if (floodFillCommand == null) return;

            floodFillCommand.argAutofill = BuildFloodFillAutofill();
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

        private static bool TryGetFloodFillVisual(byte liquidByte, out string liquidId, out string displayName)
        {
            liquidId = null;
            displayName = null;

            switch (liquidByte)
            {
                case 1:
                    liquidId = "water";
                    displayName = Locale.GetOther("water");
                    return true;
                case 2:
                    liquidId = "algae";
                    displayName = Locale.GetOther("algae");
                    return true;
                case 3:
                    liquidId = "oil";
                    displayName = Locale.GetOther("oil");
                    return true;
                case 4:
                    liquidId = "sap";
                    displayName = Locale.GetOther("sap");
                    return true;
                case 5:
                    liquidId = "dirtywater";
                    displayName = Locale.GetOther("dirtywater");
                    return true;
                case 6:
                    liquidId = "magma";
                    displayName = Locale.GetOther("magma");
                    return true;
            }

            if (!LiquidTileRegistry.TryGetTileId(liquidByte, out liquidId) || string.IsNullOrWhiteSpace(liquidId))
            {
                return false;
            }

            CustomLiquidInfo customInfo;
            if (LiquidRegistry.TryGetCustomInfo(liquidId, out customInfo) &&
                customInfo != null &&
                !string.IsNullOrWhiteSpace(customInfo.name))
            {
                displayName = customInfo.name;
                return true;
            }

            LiquidType liquidType;
            if (Liquids.Registry != null && Liquids.Registry.TryGetValue(liquidId, out liquidType) && liquidType != null)
            {
                var localeKey = !string.IsNullOrWhiteSpace(liquidType.localeName) ? liquidType.localeName : liquidId;
                displayName = Locale.GetOther(localeKey);
                return true;
            }

            displayName = Locale.GetOther(liquidId);
            return true;
        }

        private static string FormatFloodFillAutofill(string value)
        {
            byte liquidByte;
            if (!byte.TryParse(value, out liquidByte)) return value;

            string liquidId;
            string displayName;
            if (!TryGetFloodFillVisual(liquidByte, out liquidId, out displayName) || string.IsNullOrWhiteSpace(liquidId))
                return value;

            if (string.IsNullOrWhiteSpace(displayName) ||
                string.Equals(displayName, liquidId, StringComparison.OrdinalIgnoreCase))
                return value + " - " + liquidId;

            return value + " - " + liquidId + " - " + displayName;
        }

        private static string FormatSetTileAutofill(string value)
        {
            ushort tileIndex;
            if (!ushort.TryParse(value, out tileIndex)) return value;

            string vanillaTileId;
            string vanillaDisplayName;
            if (TryGetVanillaTileVisual(tileIndex, out vanillaTileId, out vanillaDisplayName) &&
                !string.IsNullOrWhiteSpace(vanillaTileId))
            {
                if (string.IsNullOrWhiteSpace(vanillaDisplayName) ||
                    string.Equals(vanillaDisplayName, vanillaTileId, StringComparison.OrdinalIgnoreCase))
                    return value + " - " + vanillaTileId;

                return value + " - " + vanillaTileId + " - " + vanillaDisplayName;
            }

            CustomTileDefinition definition;
            if (!TileRegistry.TryGetDefinition(tileIndex, out definition) || definition == null ||
                string.IsNullOrWhiteSpace(definition.ID))
                return value;

            if (string.IsNullOrWhiteSpace(definition.Name) ||
                string.Equals(definition.Name, definition.ID, StringComparison.OrdinalIgnoreCase))
                return value + " - " + definition.ID;

            return value + " - " + definition.ID + " - " + definition.Name;
        }

        private static bool TryGetAutofillFormatter(string commandName, int argumentIndex,
            out Func<string, string> formatter)
        {
            formatter = null;

            if (string.Equals(commandName, "floodfill", StringComparison.OrdinalIgnoreCase) && argumentIndex == 1)
            {
                formatter = FormatFloodFillAutofill;
                return true;
            }

            if (string.Equals(commandName, "settile", StringComparison.OrdinalIgnoreCase) && argumentIndex == 0)
            {
                formatter = FormatSetTileAutofill;
                return true;
            }

            return false;
        }

        private static string FormatAutofillSuggestionLine(string line, Func<string, string> formatter)
        {
            if (string.IsNullOrEmpty(line) || formatter == null) return line;

            var prefix = string.Empty;
            var suffix = string.Empty;
            var content = line;

            while (content.StartsWith("<color=", StringComparison.Ordinal) ||
                   content.StartsWith("</color>", StringComparison.Ordinal))
            {
                if (content.StartsWith("<color=", StringComparison.Ordinal))
                {
                    var tagEnd = content.IndexOf('>');
                    if (tagEnd < 0) break;

                    prefix += content.Substring(0, tagEnd + 1);
                    content = content.Substring(tagEnd + 1);
                    continue;
                }

                prefix += "</color>";
                content = content.Substring("</color>".Length);
            }

            while (content.EndsWith("</color>", StringComparison.Ordinal))
            {
                suffix = "</color>" + suffix;
                content = content.Substring(0, content.Length - "</color>".Length);
            }

            var formatted = formatter(content);
            return string.Equals(formatted, content, StringComparison.Ordinal) ? line : prefix + formatted + suffix;
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

        [HarmonyPatch(typeof(ConsoleScript), "HandleDescriptionText")]
        [HarmonyPostfix]
        private static void LabelCommandAutofill(ConsoleScript __instance, string[] args)
        {
            if (__instance?.descriptionText == null || args == null || args.Length < 2) return;

            Func<string, string> formatter;
            if (!TryGetAutofillFormatter(args[0], args.Length - 2, out formatter)) return;

            var text = __instance.descriptionText.text;
            var newlineIndex = text.IndexOf('\n');
            if (newlineIndex < 0) return;

            var lines = text.Substring(newlineIndex + 1).Split('\n');
            for (var i = 0; i < lines.Length; i++)
                lines[i] = FormatAutofillSuggestionLine(lines[i], formatter);

            __instance.descriptionText.text = text.Substring(0, newlineIndex + 1) + string.Join("\n", lines);
        }

        [HarmonyPatch(typeof(ConsoleScript), "RegisterPlayerDetails")]
        internal static class SpawnCategoryAutofillPatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                var spawnCategoryCommand = ConsoleScript.SearchExact("spawncategory");
                if (spawnCategoryCommand?.argAutofill == null) return;

                spawnCategoryCommand.argAutofill.Remove(0);
            }

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
