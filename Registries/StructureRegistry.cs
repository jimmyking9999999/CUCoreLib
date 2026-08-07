using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using CUCoreLib.ContentReload;
using CUCoreLib.Helpers;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CUCoreLib.Registries
{
    public static class StructureRegistry
    {
        private const int SupportedSchemaVersion = 4;
        private const int LegacySchemaVersion = 2;
        private const int SpawnDepthCount = 5;
        private const int NonOverlapPlacementMaxAttempts = 24;
        private const int LargeStructureCellWarningThreshold = 4096;
        private const int LargeSpawnCountWarningThreshold = 40;
        private const string ConditionPercentKey = "conditionPercent";
        private const string HealthKey = "health";

        private static readonly Dictionary<string, RegisteredStructureDefinition> RegisteredDefinitions =
            new Dictionary<string, RegisteredStructureDefinition>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<char, int> GlobalBlockMap = new Dictionary<char, int>
        {
            { ' ', -1 }, { '.', 0 }, { 'R', 1 }, { 'g', 2 }, { 'S', 3 }, { 'T', 4 },
            { '#', 5 }, { 'M', 6 }, { 'G', 7 }, { 'r', 8 }, { 'P', 9 }, { 'H', 10 },
            { 'W', 11 }, { 's', 12 }, { 'd', 13 }, { 'I', 14 }, { 'c', 15 }, { 'o', 16 },
            { 'n', 17 }, { 'm', 18 }, { 'l', 19 }, { 'B', 20 }, { 'X', 21 }, { 'x', 22 },
            { 'v', 23 }, { 'L', 24 }, { 'e', 25 }, { 'w', 26 }, { '=', 27 }, { '-', 28 },
            { 'p', 29 }, { 'h', 30 }, { 'f', 31 }, { 'b', 32 }, { '^', 33 }, { 'C', 34 },
            { 'i', 35 }
        };

        private static readonly Dictionary<int, char> GlobalBlockReverseMap = BuildGlobalBlockReverseMap();

        private static readonly Dictionary<char, int> GlobalLiquidMap = new Dictionary<char, int>
        {
            { '~', 1 },
            { '&', 2 },
            { '!', 3 },
            { '$', 4 },
            { '?', 5 },
            { 'A', 6 }
        };

        private static readonly HashSet<string> MissingSpawnIdsLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static GameObject BackgroundTemplate;
        private static bool RegisteredStructureSummaryLogged;

        public static bool RegisterFromJson(string id, string json, string sourceLabel = null)
        {
            ContentReloadSession.AssertNotActive("StructureRegistry.RegisterFromJson()",
                "Multi-block structure registration is excluded from strict content reload.");

            if (string.IsNullOrWhiteSpace(id))
            {
                CUCoreLibPlugin.Log?.LogWarning("Ignored structure registration with no ID.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                CUCoreLibPlugin.Log?.LogWarning("Ignored structure registration for '" + id.Trim() +
                                                "' because the JSON payload was empty.");
                return false;
            }

            try
            {
                var definition = ParseDefinition(id.Trim(), json, sourceLabel ?? "json");
                if (definition == null) return false;

                RegisteredDefinitions[definition.ID] = definition;
                return true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("Failed to register structure '" + id.Trim() + "' from " +
                                                (sourceLabel ?? "json") + ": " + ex.Message);
                return false;
            }
        }

        public static bool RegisterFromEmbeddedJson(string id, string resourcePath, Assembly sourceAssembly = null)
        {
            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            var json = AssetLoader.LoadEmbeddedText(resourcePath, sourceAssembly);
            if (string.IsNullOrWhiteSpace(json))
            {
                CUCoreLibPlugin.Log?.LogWarning("Failed to load embedded structure JSON '" + resourcePath +
                                                "' for '" + id + "'.");
                return false;
            }

            return RegisterFromJson(id, json, "embedded resource '" + resourcePath + "'");
        }

        public static bool RegisterFromFile(string id, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                CUCoreLibPlugin.Log?.LogWarning("Failed to register structure '" + id + "' because the file path was empty.");
                return false;
            }

            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                CUCoreLibPlugin.Log?.LogWarning("Structure JSON file not found at '" + fullPath + "'.");
                return false;
            }

            return RegisterFromJson(id, File.ReadAllText(fullPath), "file '" + fullPath + "'");
        }

        public static IEnumerable<string> GetRegisteredIds()
        {
            return RegisteredDefinitions.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static bool TrySetSpawnCounts(string id, params int[] spawnCounts)
        {
            if (!TryGetDefinition(id, out var definition)) return false;

            definition.SpawnCounts = NormalizeSpawnCounts(spawnCounts);
            return true;
        }

        public static bool TryGetSpawnCounts(string id, out int[] spawnCounts)
        {
            spawnCounts = null;
            if (!TryGetDefinition(id, out var definition)) return false;

            spawnCounts = definition.SpawnCounts.ToArray();
            return true;
        }

        public static bool Place(string id, Vector2 position, int? seedOverride = null)
        {
            if (!TryGetDefinition(id, out var definition)) return false;

            LogRegisteredStructureSummaryIfNeeded();

            using (seedOverride.HasValue ? StructureSeededRandom.PushOverride(seedOverride.Value) : null)
            {
                PlaceStructure(position, definition);
            }

            return true;
        }

        internal static IEnumerator GenerateRegisteredStructures(IEnumerator original, WorldGeneration world)
        {
            while (original.MoveNext())
            {
                yield return original.Current;
            }

            if (world == null || RegisteredDefinitions.Count == 0) yield break;
            if (IsTutorialWorld(world)) yield break;

            LogRegisteredStructureSummaryIfNeeded();
            StructureSeededRandom.InitializeForStructures();

            var currentDepth = world.biomeDepth;
            var occupiedRects = new List<StructurePlacementRect>();
            var totalRequestedSpawnCount = 0;
            var limitDebugWorldSpawns = IsDebugWorld(world);
            foreach (var definition in RegisteredDefinitions.Values.OrderBy(entry => entry.ID, StringComparer.OrdinalIgnoreCase))
            {
                if (definition.SpawnCounts == null || currentDepth < 0 || currentDepth >= definition.SpawnCounts.Length)
                    continue;

                var countToSpawn = Mathf.Max(0, definition.SpawnCounts[currentDepth]);
                if (limitDebugWorldSpawns) countToSpawn = Mathf.Min(countToSpawn, 1);
                if (countToSpawn <= 0) continue;

                totalRequestedSpawnCount += countToSpawn;
                if (totalRequestedSpawnCount > LargeSpawnCountWarningThreshold)
                {
                    CUCoreLibPlugin.Log?.LogWarning("Registered structure worldgen requested " + totalRequestedSpawnCount +
                                                    " placements on depth " + currentDepth +
                                                    ". Large counts can noticeably extend world generation.");
                    totalRequestedSpawnCount = int.MinValue;
                }

                for (var i = 0; i < countToSpawn; i++)
                {
                    if (!TryFindSpawnPosition(world, definition, occupiedRects, out var pos, out var placedRect))
                        continue;

                    PlaceStructure(pos, definition);
                    if (HasArea(placedRect)) occupiedRects.Add(placedRect);

                    world.SetLoadingTextNoLocale("Generating CUCoreLib Structures..\n " + definition.ID + " (" +
                                                 (i + 1) + "/" + countToSpawn + ")\n\n");
                    yield return null;
                }
            }
        }

        private static bool IsTutorialWorld(WorldGeneration world)
        {
            return world != null && world.biomeOverride == WorldGeneration.OverrideSceneType.Tutorial;
        }

        private static bool IsDebugWorld(WorldGeneration world)
        {
            if (world == null || WorldGeneration.runSettings == null) return false;

            try
            {
                return WorldGeneration.GetRunSettingBool("debugworld");
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetDefinition(string id, out RegisteredStructureDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            return RegisteredDefinitions.TryGetValue(id.Trim(), out definition);
        }

        private static RegisteredStructureDefinition ParseDefinition(string id, string json, string sourceLabel)
        {
            var root = JObject.Parse(json);
            var metadata = root["metadata"] as JObject;
            var schemaVersion = metadata?.Value<int?>("schemaVersion") ?? 0;
            if (schemaVersion != LegacySchemaVersion && schemaVersion != SupportedSchemaVersion)
                throw new InvalidOperationException("Expected metadata.schemaVersion to be 2 or " + SupportedSchemaVersion + ".");

            var width = Mathf.Max(1, root.Value<int?>("width") ?? 0);
            var height = Mathf.Max(1, root.Value<int?>("height") ?? 0);

            var normalizedLayers = NormalizeLayers(root["layers"] as JArray, width, height);
            if (normalizedLayers.Count == 0)
                throw new InvalidOperationException("No usable layers were present in the structure payload.");

            ComposeVisibleLayers(normalizedLayers, width, height, out var composedFg, out var composedBg,
                out var topFgLayerIds);

            if (composedFg.All(ch => ch == '.'))
                throw new InvalidOperationException("The composed foreground layer was empty.");

            var shape = LayerCharsToRows(composedFg, width, height);
            var backgroundShape = composedBg.Any(ch => ch != '.') ? LayerCharsToRows(composedBg, width, height) : null;

            ParseCustomIds(metadata?["customIds"] as JObject, out var entityMap, out var sequentialItems);
            var itemAssignments = BuildItemAssignmentsByCell(root["itemsByCell"] as JObject, topFgLayerIds, composedFg,
                width, height);
            var objectAssignments = BuildObjectAssignmentsByCell(root["objectsByCell"] as JObject, topFgLayerIds,
                composedFg, width, height);
            BuildCustomPropertiesByCell(root["customPropertiesByCell"] as JObject, out var itemCustomProperties,
                out var objectCustomProperties);
            var precisePlacements = BuildPrecisePlacements(root["precisePlacements"] as JArray);
            var lootRulesByMarker = BuildLootRulesByMarker(root["lootRules"] as JArray);
            var lootPools = BuildLootPools(root["lootPools"] as JObject);

            var definition = new RegisteredStructureDefinition
            {
                ID = id,
                Shape = shape,
                BackgroundShape = backgroundShape,
                AvoidOverlap = metadata?.Value<bool?>("avoidOverlap") == true,
                EntityMap = entityMap,
                SequentialItems = sequentialItems,
                ItemAssignmentsByCell = itemAssignments,
                ObjectAssignmentsByCell = objectAssignments,
                ItemCustomPropertiesByCell = itemCustomProperties,
                ObjectCustomPropertiesByCell = objectCustomProperties,
                PrecisePlacements = precisePlacements,
                SpawnCounts = NormalizeSpawnCounts(metadata?["spawnCounts"] as JArray),
                TerrainGenAreaCount = (root["terrainGenAreas"] as JArray)?.Count ?? 0,
                LootRulesByMarker = lootRulesByMarker,
                LootPools = lootPools
            };

            definition.CompiledStructure = CompileStructure(definition);
            if (definition.CompiledStructure == null)
                throw new InvalidOperationException("The structure could not be compiled.");

            definition.CompiledLootMarkers = CompileLootMarkers(definition);

            WarnIfDefinitionLooksExpensive(definition, sourceLabel);
            return definition;
        }

        private static void WarnIfDefinitionLooksExpensive(RegisteredStructureDefinition definition, string sourceLabel)
        {
            if (definition?.CompiledStructure == null) return;

            var cellCount = definition.CompiledStructure.Width * definition.CompiledStructure.Height;
            if (cellCount > LargeStructureCellWarningThreshold)
                CUCoreLibPlugin.Log?.LogWarning("Structure '" + definition.ID + "' from " + sourceLabel + " is " +
                                                definition.CompiledStructure.Width + "x" +
                                                definition.CompiledStructure.Height + " (" + cellCount +
                                                " cells). Large structures can slow world generation.");

            var totalSpawnCount = definition.SpawnCounts?.Sum() ?? 0;
            if (totalSpawnCount > LargeSpawnCountWarningThreshold)
                CUCoreLibPlugin.Log?.LogWarning("Structure '" + definition.ID + "' requests " + totalSpawnCount +
                                                " total placements across its five spawn depths. Large totals can slow world generation.");

            if (definition.TerrainGenAreaCount > 0)
                CUCoreLibPlugin.Log?.LogInfo("Structure '" + definition.ID + "' includes " +
                                             definition.TerrainGenAreaCount +
                                             " terrainGenAreas entries. CUCoreLib v1 imports and preserves compatibility with the payload shape, but does not execute terrainGenAreas yet.");
        }

        private static void LogRegisteredStructureSummaryIfNeeded()
        {
            if (RegisteredStructureSummaryLogged || RegisteredDefinitions.Count <= 0) return;

            RegisteredStructureSummaryLogged = true;
            CUCoreLibPlugin.Log?.LogInfo("Added " + RegisteredDefinitions.Count + " custom structures.");
        }

        private static bool HasUnsupportedDynamicGeneration(RegisteredStructureDefinition definition, JObject root,
            out string reason)
        {
            reason = null;
            if (definition == null) return false;

            if (definition.ItemAssignmentsByCell == null) return false;

            foreach (var assignment in definition.ItemAssignmentsByCell.Values)
            {
                if (assignment == null) continue;
                if (!string.Equals(assignment.Mode, "single", StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Only deterministic single-item assignments are supported in CUCoreLib multi-block structures v1.";
                    return true;
                }

                if (assignment.Entries == null || assignment.Entries.Count != 1)
                {
                    reason = "Only deterministic single-entry item assignments are supported in CUCoreLib multi-block structures v1.";
                    return true;
                }

                var entry = assignment.Entries[0];
                if (entry == null) continue;
                if (!Mathf.Approximately(Mathf.Clamp(entry.Percent, 0f, 100f), 100f))
                {
                    reason = "Probabilistic item assignment percentages are not supported in CUCoreLib multi-block structures v1.";
                    return true;
                }
            }

            return false;
        }

        private static CustomStructure CompileStructure(RegisteredStructureDefinition definition)
        {
            var structure = ParseStringGrid(
                definition.Shape,
                GlobalBlockMap,
                definition.EntityMap,
                definition.ItemAssignmentsByCell,
                definition.ObjectAssignmentsByCell,
                definition.ItemCustomPropertiesByCell,
                definition.ObjectCustomPropertiesByCell,
                definition.SequentialItems,
                definition.PrecisePlacements,
                GlobalLiquidMap);

            if (structure == null || definition.BackgroundShape == null) return structure;

            var backgroundStructure = ParseStringGrid(
                definition.BackgroundShape,
                GlobalBlockMap,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                GlobalLiquidMap);

            if (backgroundStructure == null) return structure;

            structure.BackgroundIDs = backgroundStructure.BlockIDs;
            if (backgroundStructure.LiquidIDs == null) return structure;

            for (var x = 0; x < structure.Width; x++)
            {
                for (var y = 0; y < structure.Height; y++)
                {
                    if (backgroundStructure.LiquidIDs[x, y] > 0)
                        structure.LiquidIDs[x, y] = backgroundStructure.LiquidIDs[x, y];
                }
            }

            return structure;
        }

        private static List<CompiledLootMarker> CompileLootMarkers(RegisteredStructureDefinition definition)
        {
            if (definition?.LootRulesByMarker == null || definition.LootRulesByMarker.Count == 0 ||
                definition.Shape == null || definition.Shape.Length == 0)
                return null;

            var height = definition.Shape.Length;
            var width = definition.Shape[0].Length;
            var heightMinusOne = height - 1;
            Dictionary<int, EntityPrecisePos> preciseByCell = null;

            if (definition.PrecisePlacements != null && definition.PrecisePlacements.Count > 0)
            {
                preciseByCell = new Dictionary<int, EntityPrecisePos>(definition.PrecisePlacements.Count);
                for (var i = 0; i < definition.PrecisePlacements.Count; i++)
                {
                    var precise = definition.PrecisePlacements[i];
                    if (precise == null) continue;
                    if (precise.GridX < 0 || precise.GridX >= width || precise.GridY < 0 || precise.GridY >= height)
                        continue;

                    preciseByCell[(precise.GridY * width) + precise.GridX] = precise;
                }
            }

            var compiled = new List<CompiledLootMarker>();
            for (var y = 0; y < height; y++)
            {
                var row = definition.Shape[y] ?? string.Empty;
                for (var x = 0; x < width; x++)
                {
                    if (x >= row.Length) continue;

                    var marker = row[x];
                    if (!definition.LootRulesByMarker.TryGetValue(marker, out var rule) || rule == null) continue;

                    var linearIndex = y * width + x;
                    var worldY = heightMinusOne - y;
                    var finalX = (float)x;
                    var finalY = (float)worldY;
                    if (preciseByCell != null && preciseByCell.TryGetValue(linearIndex, out var precise))
                    {
                        finalX += precise.OffsetX;
                        finalY += precise.OffsetY;
                    }

                    compiled.Add(new CompiledLootMarker
                    {
                        Marker = marker,
                        Rule = rule,
                        X = finalX,
                        Y = finalY
                    });
                }
            }

            return compiled.Count > 0 ? compiled : null;
        }

        private static List<NormalizedLayer> NormalizeLayers(JArray layers, int width, int height)
        {
            var normalized = new List<NormalizedLayer>();
            if (layers == null) return normalized;

            for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                if (!(layers[layerIndex] is JObject layerToken)) continue;

                var id = layerToken.Value<string>("id");
                if (string.IsNullOrWhiteSpace(id)) id = "layer_" + layerIndex;

                var kind = NormalizeLayerKind(layerToken.Value<string>("kind"));
                var visible = layerToken.Value<bool?>("visible") != false;
                var cells = new char[width * height];
                for (var i = 0; i < cells.Length; i++) cells[i] = '.';

                if (layerToken["rows"] is JArray rowsToken && rowsToken.Count > 0)
                {
                    var rowCount = Mathf.Min(height, rowsToken.Count);
                    for (var y = 0; y < rowCount; y++)
                    {
                        var row = rowsToken[y]?.Value<string>() ?? string.Empty;
                        var maxX = Mathf.Min(width, row.Length);
                        for (var x = 0; x < maxX; x++) cells[y * width + x] = row[x];
                    }
                }
                else if (layerToken["data"] is JArray dataToken && dataToken.Count > 0)
                {
                    var max = Mathf.Min(cells.Length, dataToken.Count);
                    for (var i = 0; i < max; i++) cells[i] = ResolvePaletteChar(dataToken[i]?.Value<int?>() ?? 0);
                }

                normalized.Add(new NormalizedLayer
                {
                    ID = id.Trim(),
                    Kind = kind,
                    Visible = visible,
                    Cells = cells
                });
            }

            return normalized;
        }

        private static string NormalizeLayerKind(string raw)
        {
            if (string.Equals(raw, "bg", StringComparison.OrdinalIgnoreCase)) return "bg";
            if (string.Equals(raw, "fg", StringComparison.OrdinalIgnoreCase)) return "fg";
            return "layer";
        }

        private static char ResolvePaletteChar(int tileId)
        {
            return GlobalBlockReverseMap.TryGetValue(tileId, out var mapped) ? mapped : '.';
        }

        private static void ComposeVisibleLayers(List<NormalizedLayer> layers, int width, int height, out char[] fg,
            out char[] bg, out string[] topFgLayerIds)
        {
            var total = width * height;
            fg = new char[total];
            bg = new char[total];
            topFgLayerIds = new string[total];
            for (var i = 0; i < total; i++)
            {
                fg[i] = '.';
                bg[i] = '.';
            }

            var visibleLayers = layers.Where(layer => layer.Visible).ToList();
            var fgLayers = visibleLayers.Where(layer => layer.Kind == "fg" || layer.Kind == "layer").ToList();
            var bgLayers = visibleLayers.Where(layer => layer.Kind == "bg").ToList();

            for (var i = 0; i < total; i++)
            {
                for (var layerIndex = fgLayers.Count - 1; layerIndex >= 0; layerIndex--)
                {
                    var candidate = fgLayers[layerIndex].Cells[i];
                    if (candidate == '.') continue;

                    fg[i] = candidate;
                    topFgLayerIds[i] = fgLayers[layerIndex].ID;
                    break;
                }

                for (var layerIndex = bgLayers.Count - 1; layerIndex >= 0; layerIndex--)
                {
                    var candidate = bgLayers[layerIndex].Cells[i];
                    if (candidate == '.') continue;

                    bg[i] = candidate;
                    break;
                }
            }
        }

        private static string[] LayerCharsToRows(char[] chars, int width, int height)
        {
            var rows = new string[height];
            for (var y = 0; y < height; y++)
            {
                var row = new char[width];
                Array.Copy(chars, y * width, row, 0, width);
                rows[y] = new string(row);
            }

            return rows;
        }

        private static void ParseCustomIds(JObject customIds, out Dictionary<char, string> entityMap,
            out string sequentialItems)
        {
            entityMap = null;
            sequentialItems = null;
            if (customIds == null) return;

            var map = new Dictionary<char, string>();
            foreach (var property in customIds.Properties())
            {
                if (property.Name == "*")
                {
                    sequentialItems = property.Value?.Value<string>() ?? string.Empty;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(property.Name) || property.Name.Length != 1) continue;
                var value = property.Value?.Value<string>();
                if (string.IsNullOrWhiteSpace(value)) continue;

                map[property.Name[0]] = value.Trim();
            }

            entityMap = map.Count > 0 ? map : null;
        }

        private static Dictionary<int, ItemAssignmentDef> BuildItemAssignmentsByCell(JObject rawAssignments,
            string[] topFgLayerIds, char[] composedFg, int width, int height)
        {
            if (rawAssignments == null || rawAssignments.Count == 0) return null;

            var flattened = new Dictionary<int, ItemAssignmentDef>();
            var scopedIndices = BuildScopedAssignmentIndexSet(rawAssignments);
            var total = width * height;

            for (var index = 0; index < total; index++)
            {
                if (composedFg[index] != '*') continue;

                var resolved = ResolveLayerAwareAssignment(rawAssignments, scopedIndices, topFgLayerIds[index], index);
                if (resolved == null) continue;

                var normalized = NormalizeItemAssignment(resolved);
                if (normalized != null && normalized.Entries.Count > 0) flattened[index] = normalized;
            }

            return flattened.Count > 0 ? flattened : null;
        }

        private static ItemAssignmentDef NormalizeItemAssignment(JObject source)
        {
            if (source == null) return null;

            var normalized = new ItemAssignmentDef
            {
                Mode = string.Equals(source.Value<string>("mode"), "list", StringComparison.OrdinalIgnoreCase)
                    ? "list"
                    : "single",
                MaxDrops = Mathf.Max(1, source.Value<int?>("maxDrops") ?? 1),
                RollIndependent = source.Value<bool?>("rollIndependent") != false
            };

            if (!(source["entries"] is JArray entries)) return null;

            for (var i = 0; i < entries.Count; i++)
            {
                if (!(entries[i] is JObject entryToken)) continue;

                var value = entryToken.Value<string>("value");
                if (string.IsNullOrWhiteSpace(value)) continue;

                normalized.Entries.Add(new ItemAssignmentEntryDef
                {
                    Value = value.Trim(),
                    Percent = float.IsNaN(entryToken.Value<float?>("percent") ?? 100f)
                        ? 100f
                        : Mathf.Clamp(entryToken.Value<float?>("percent") ?? 100f, 0f, 100f),
                    ConditionPercent = float.IsNaN(entryToken.Value<float?>("conditionPercent") ?? 100f)
                        ? 100f
                        : Mathf.Clamp(entryToken.Value<float?>("conditionPercent") ?? 100f, 0f, 200f)
                });
            }

            return normalized.Entries.Count > 0 ? normalized : null;
        }

        private static Dictionary<int, string> BuildObjectAssignmentsByCell(JObject rawAssignments,
            string[] topFgLayerIds, char[] composedFg, int width, int height)
        {
            if (rawAssignments == null || rawAssignments.Count == 0) return null;

            var flattened = new Dictionary<int, string>();
            var scopedIndices = BuildScopedAssignmentIndexSet(rawAssignments);
            var total = width * height;

            for (var index = 0; index < total; index++)
            {
                if (composedFg[index] != '0') continue;

                var resolved = ResolveLayerAwareAssignment(rawAssignments, scopedIndices, topFgLayerIds[index], index);
                if (!(resolved?["id"] is JValue idValue)) continue;

                var id = idValue.Value<string>();
                if (string.IsNullOrWhiteSpace(id)) continue;
                flattened[index] = id.Trim();
            }

            return flattened.Count > 0 ? flattened : null;
        }

        private static JObject ResolveLayerAwareAssignment(JObject assignments, HashSet<int> scopedIndices,
            string activeLayerId, int index)
        {
            if (assignments == null) return null;

            var scopedKey = !string.IsNullOrWhiteSpace(activeLayerId)
                ? activeLayerId + ":" + index.ToString(CultureInfo.InvariantCulture)
                : null;
            if (!string.IsNullOrWhiteSpace(scopedKey) && assignments.TryGetValue(scopedKey, out var scopedToken) &&
                scopedToken is JObject scopedObject)
                return scopedObject;

            if (scopedIndices != null && scopedIndices.Contains(index)) return null;

            var indexKey = index.ToString(CultureInfo.InvariantCulture);
            return assignments.TryGetValue(indexKey, out var unscopedToken) ? unscopedToken as JObject : null;
        }

        private static HashSet<int> BuildScopedAssignmentIndexSet(JObject assignments)
        {
            if (assignments == null || assignments.Count == 0) return null;

            HashSet<int> scopedIndices = null;
            foreach (var property in assignments.Properties())
            {
                var key = property.Name;
                if (string.IsNullOrEmpty(key)) continue;

                var separator = key.LastIndexOf(':');
                if (separator < 0 || separator >= key.Length - 1) continue;

                var suffix = key.Substring(separator + 1);
                if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                    continue;

                if (scopedIndices == null) scopedIndices = new HashSet<int>();
                scopedIndices.Add(index);
            }

            return scopedIndices;
        }

        private static void BuildCustomPropertiesByCell(JObject source,
            out Dictionary<int, Dictionary<string, string>> itemCustomPropertiesByCell,
            out Dictionary<int, Dictionary<string, string>> objectCustomPropertiesByCell)
        {
            itemCustomPropertiesByCell = null;
            objectCustomPropertiesByCell = null;
            if (source == null || source.Count == 0) return;

            var itemMap = new Dictionary<int, Dictionary<string, string>>();
            var objectMap = new Dictionary<int, Dictionary<string, string>>();

            foreach (var property in source.Properties())
            {
                if (!TryParseCellIndex(property.Name, out var index) || index < 0) continue;
                if (!(property.Value is JObject bucket)) continue;

                var cleanedItems = CleanPropertyDictionary(bucket["item"] as JObject);
                if (cleanedItems != null) itemMap[index] = cleanedItems;

                var cleanedObjects = CleanPropertyDictionary(bucket["object"] as JObject);
                if (cleanedObjects != null) objectMap[index] = cleanedObjects;
            }

            itemCustomPropertiesByCell = itemMap.Count > 0 ? itemMap : null;
            objectCustomPropertiesByCell = objectMap.Count > 0 ? objectMap : null;
        }

        private static Dictionary<string, string> CleanPropertyDictionary(JObject source)
        {
            if (source == null || source.Count == 0) return null;

            var cleaned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in source.Properties())
            {
                var key = property.Name?.Trim();
                var value = property.Value?.Value<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;
                cleaned[key] = value;
            }

            return cleaned.Count > 0 ? cleaned : null;
        }

        private static bool TryParseCellIndex(string rawKey, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(rawKey)) return false;

            var trimmed = rawKey.Trim();
            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                return index >= 0;

            var split = trimmed.LastIndexOf(':');
            if (split < 0 || split >= trimmed.Length - 1) return false;

            return int.TryParse(trimmed.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture,
                       out index) &&
                   index >= 0;
        }

        private static List<EntityPrecisePos> BuildPrecisePlacements(JArray source)
        {
            if (source == null || source.Count == 0) return null;

            var list = new List<EntityPrecisePos>(source.Count);
            for (var i = 0; i < source.Count; i++)
            {
                if (!(source[i] is JObject token)) continue;

                list.Add(new EntityPrecisePos
                {
                    GridX = token.Value<int?>("gridX") ?? 0,
                    GridY = token.Value<int?>("gridY") ?? 0,
                    OffsetX = token.Value<float?>("offsetX") ?? 0f,
                    OffsetY = token.Value<float?>("offsetY") ?? 0f,
                    Rotation = token.Value<float?>("rotation") ?? 0f,
                    Scale = Mathf.Approximately(token.Value<float?>("scale") ?? 1f, 0f)
                        ? 1f
                        : token.Value<float?>("scale") ?? 1f,
                    FlipX = token.Value<bool?>("flipX") == true,
                    FlipY = token.Value<bool?>("flipY") == true
                });
            }

            return list.Count > 0 ? list : null;
        }

        private static int[] NormalizeSpawnCounts(JArray raw)
        {
            return NormalizeSpawnCounts(raw?.Select(token => token.Value<int?>() ?? 4).ToArray());
        }

        private static int[] NormalizeSpawnCounts(int[] raw)
        {
            var spawnCounts = raw ?? new[] { 4, 4, 4, 4, 4 };
            if (spawnCounts.Length < SpawnDepthCount)
            {
                var expanded = spawnCounts.ToList();
                while (expanded.Count < SpawnDepthCount) expanded.Add(4);
                spawnCounts = expanded.ToArray();
            }
            else if (spawnCounts.Length > SpawnDepthCount)
            {
                spawnCounts = spawnCounts.Take(SpawnDepthCount).ToArray();
            }

            return spawnCounts.Select(count => Mathf.Max(0, count)).ToArray();
        }

        private static Dictionary<char, LootRuleDef> BuildLootRulesByMarker(JArray source)
        {
            if (source == null || source.Count == 0) return null;

            var rules = new Dictionary<char, LootRuleDef>();
            for (var i = 0; i < source.Count; i++)
            {
                if (!(source[i] is JObject token)) continue;

                var marker = token.Value<string>("marker");
                if (string.IsNullOrWhiteSpace(marker) || marker.Length != 1) continue;

                var poolId = token.Value<string>("poolId");
                if (string.IsNullOrWhiteSpace(poolId)) continue;

                rules[marker[0]] = new LootRuleDef
                {
                    Marker = marker[0],
                    Chance = Mathf.Clamp01(token.Value<float?>("chance") ?? 0f),
                    PoolID = poolId.Trim(),
                    Min = Mathf.Max(1, token.Value<int?>("min") ?? 1),
                    Max = Mathf.Max(1, token.Value<int?>("max") ?? 1)
                };
            }

            return rules.Count > 0 ? rules : null;
        }

        private static Dictionary<string, LootPoolDef> BuildLootPools(JObject source)
        {
            if (source == null || source.Count == 0) return null;

            var pools = new Dictionary<string, LootPoolDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in source.Properties())
            {
                if (!(property.Value is JArray entries) || entries.Count == 0) continue;

                var compiledEntries = new List<LootPoolEntryDef>();
                var cumulativeWeights = new List<int>();
                var totalWeight = 0;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (!(entries[i] is JObject entryToken)) continue;
                    var itemId = SpawnIdHelpers.NormalizeSpawnId(entryToken.Value<string>("itemId"));
                    if (string.IsNullOrWhiteSpace(itemId)) continue;

                    var weight = Mathf.Max(0, entryToken.Value<int?>("weight") ?? 0);
                    if (weight <= 0) continue;

                    totalWeight += weight;
                    compiledEntries.Add(new LootPoolEntryDef
                    {
                        ItemID = itemId,
                        Weight = weight
                    });
                    cumulativeWeights.Add(totalWeight);
                }

                if (compiledEntries.Count == 0 || totalWeight <= 0) continue;

                pools[property.Name] = new LootPoolDef
                {
                    ID = property.Name,
                    Entries = compiledEntries,
                    CumulativeWeights = cumulativeWeights,
                    TotalWeight = totalWeight
                };
            }

            return pools.Count > 0 ? pools : null;
        }

        private static CustomStructure ParseStringGrid(string[] rows, Dictionary<char, int> blockMap,
            Dictionary<char, string> entityMap = null,
            Dictionary<int, ItemAssignmentDef> itemAssignmentsByCell = null,
            Dictionary<int, string> objectAssignmentsByCell = null,
            Dictionary<int, Dictionary<string, string>> itemCustomPropertiesByCell = null,
            Dictionary<int, Dictionary<string, string>> objectCustomPropertiesByCell = null,
            string sequentialItems = null, List<EntityPrecisePos> precisePositions = null,
            Dictionary<char, int> liquidMap = null)
        {
            var height = rows.Length;
            var width = rows[0].Length;
            var heightMinusOne = height - 1;
            var hasEntityMap = entityMap != null && entityMap.Count > 0;
            var hasLiquidMap = liquidMap != null && liquidMap.Count > 0;
            var structure = new CustomStructure
            {
                Width = width,
                Height = height,
                BlockIDs = new int[width, height],
                LiquidIDs = new int[width, height]
            };

            var itemQueue = new Queue<string>();
            if (!string.IsNullOrWhiteSpace(sequentialItems))
            {
                foreach (var value in sequentialItems.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    itemQueue.Enqueue(value.Trim());
            }

            Dictionary<int, EntityPrecisePos> preciseByCell = null;
            if (precisePositions != null && precisePositions.Count > 0)
            {
                preciseByCell = new Dictionary<int, EntityPrecisePos>(precisePositions.Count);
                for (var i = 0; i < precisePositions.Count; i++)
                {
                    var precise = precisePositions[i];
                    if (precise == null) continue;
                    if (precise.GridX < 0 || precise.GridX >= width || precise.GridY < 0 || precise.GridY >= height)
                        continue;

                    preciseByCell[(precise.GridY * width) + precise.GridX] = precise;
                }
            }

            for (var y = 0; y < height; y++)
            {
                var row = rows[y] ?? string.Empty;
                for (var x = 0; x < width; x++)
                {
                    if (x >= row.Length) continue;

                    var marker = row[x];
                    var linearIndex = y * width + x;
                    var worldY = heightMinusOne - y;
                    string mappedEntityId = null;
                    var hasMappedEntity = hasEntityMap && entityMap.TryGetValue(marker, out mappedEntityId);

                    var blockId = -1;
                    var liquidId = 0;
                    var isItemMarker = marker == '*';
                    var isObjectMarker = marker == '0';

                    if (hasMappedEntity || isItemMarker || isObjectMarker)
                    {
                        blockId = 0;
                    }
                    else if (hasLiquidMap && liquidMap.TryGetValue(marker, out liquidId))
                    {
                        blockId = 0;
                    }
                    else if (blockMap.TryGetValue(marker, out var mappedBlockId))
                    {
                        blockId = mappedBlockId;
                    }

                    structure.BlockIDs[x, worldY] = blockId;
                    structure.LiquidIDs[x, worldY] = liquidId;

                    string entityId = null;
                    Dictionary<string, string> customProperties = null;

                    if (isObjectMarker)
                    {
                        if (objectAssignmentsByCell != null &&
                            objectAssignmentsByCell.TryGetValue(linearIndex, out var mappedObjectId) &&
                            !string.IsNullOrWhiteSpace(mappedObjectId))
                            entityId = mappedObjectId;
                        else if (hasMappedEntity) entityId = mappedEntityId;

                        if (objectCustomPropertiesByCell != null &&
                            objectCustomPropertiesByCell.TryGetValue(linearIndex, out var objectCustom))
                            customProperties = new Dictionary<string, string>(objectCustom,
                                StringComparer.OrdinalIgnoreCase);
                    }
                    else if (isItemMarker)
                    {
                        if (itemAssignmentsByCell != null &&
                            itemAssignmentsByCell.TryGetValue(linearIndex, out var assignment) &&
                            assignment?.Entries != null &&
                            assignment.Entries.Count > 0)
                        {
                            var entry = assignment.Entries[0];
                            entityId = entry?.Value;
                            if (itemCustomPropertiesByCell != null &&
                                itemCustomPropertiesByCell.TryGetValue(linearIndex, out var itemCustom))
                                customProperties = new Dictionary<string, string>(itemCustom,
                                    StringComparer.OrdinalIgnoreCase);

                            if (entry != null)
                            {
                                if (customProperties == null)
                                    customProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                customProperties[ConditionPercentKey] =
                                    entry.ConditionPercent.ToString(CultureInfo.InvariantCulture);
                            }
                        }
                        else if (itemQueue.Count > 0)
                        {
                            entityId = itemQueue.Dequeue();
                        }
                    }
                    else if (hasMappedEntity)
                    {
                        entityId = mappedEntityId;
                        if (objectCustomPropertiesByCell != null &&
                            objectCustomPropertiesByCell.TryGetValue(linearIndex, out var legacyCustom))
                            customProperties = new Dictionary<string, string>(legacyCustom,
                                StringComparer.OrdinalIgnoreCase);
                    }

                    if (string.IsNullOrWhiteSpace(entityId)) continue;

                    AddEntity(structure, entityId, x, worldY, preciseByCell, width, customProperties);
                }
            }

            return structure;
        }

        private static void AddEntity(CustomStructure structure, string entityId, int x, int worldY,
            Dictionary<int, EntityPrecisePos> preciseByCell, int structureWidth,
            Dictionary<string, string> customProperties)
        {
            entityId = SpawnIdHelpers.NormalizeSpawnId(entityId);
            if (string.IsNullOrWhiteSpace(entityId)) return;

            var finalX = (float)x;
            var finalY = (float)worldY;
            var rotation = 0f;
            var flipX = false;
            var flipY = false;
            var scale = 1f;

            if (preciseByCell != null)
            {
                var preciseIndex = (worldY * structureWidth) + x;
                if (preciseByCell.TryGetValue(preciseIndex, out var precise))
                {
                    finalX += precise.OffsetX;
                    finalY += precise.OffsetY;
                    rotation = precise.Rotation;
                    flipX = precise.FlipX;
                    flipY = precise.FlipY;
                    scale = Mathf.Approximately(precise.Scale, 0f) ? 1f : precise.Scale;
                }
            }

            structure.Entities.Add(new StructureEntityDef
            {
                ID = entityId,
                CustomProperties = customProperties != null
                    ? new Dictionary<string, string>(customProperties, StringComparer.OrdinalIgnoreCase)
                    : null,
                X = finalX,
                Y = finalY,
                Rotation = rotation,
                FlipX = flipX,
                FlipY = flipY,
                Scale = scale
            });
        }

        private static void PlaceStructure(Vector2 worldPos, RegisteredStructureDefinition definition)
        {
            var structure = definition?.CompiledStructure;
            var world = WorldGeneration.world;
            if (world == null || structure == null) return;

            TileRegistry.InjectRegisteredTiles(world);

            var center = world.WorldToBlockPos(worldPos);
            var startX = center.x - (structure.Width / 2);
            var startY = center.y - (structure.Height / 2);
            var worldWidth = (int)world.width;
            var worldHeight = (int)world.height;
            var minX = Mathf.Max(0, -startX);
            var minY = Mathf.Max(0, -startY);
            var maxXExclusive = Mathf.Min(structure.Width, worldWidth - startX);
            var maxYExclusive = Mathf.Min(structure.Height, worldHeight - startY);

            for (var x = minX; x < maxXExclusive; x++)
            {
                var globalX = startX + x;
                for (var y = minY; y < maxYExclusive; y++)
                {
                    var blockId = structure.BlockIDs[x, y];
                    if (blockId == -1) continue;

                    world.SetBlock(new Vector2Int(globalX, startY + y), (ushort)blockId);
                }
            }

            if (structure.LiquidIDs != null && FluidManager.main != null)
            {
                for (var x = minX; x < maxXExclusive; x++)
                {
                    var globalX = startX + x;
                    for (var y = minY; y < maxYExclusive; y++)
                    {
                        var liquidId = structure.LiquidIDs[x, y];
                        if (liquidId <= 0) continue;

                        FluidManager.main.SetLiquid(globalX, startY + y, (byte)liquidId);
                    }
                }
            }

            if (structure.BackgroundIDs != null)
            {
                for (var x = minX; x < maxXExclusive; x++)
                {
                    var globalX = startX + x;
                    for (var y = minY; y < maxYExclusive; y++)
                    {
                        var backgroundId = structure.BackgroundIDs[x, y];
                        if (backgroundId <= 0) continue;

                        var exactPos = world.BlockToWorldPos(new Vector2Int(globalX, startY + y));
                        SpawnBackgroundTile(backgroundId, exactPos);
                    }
                }
            }

            var worldBase = world.BlockToWorldPos(new Vector2Int(startX, startY));
            foreach (var entity in structure.Entities)
            {
                if (entity == null) continue;
                SpawnEntity(entity, worldBase + new Vector2(entity.X, entity.Y));
            }

            SpawnLootMarkers(definition, worldBase);
        }

        private static void SpawnLootMarkers(RegisteredStructureDefinition definition, Vector2 worldBase)
        {
            if (definition?.CompiledLootMarkers == null || definition.CompiledLootMarkers.Count == 0) return;

            for (var i = 0; i < definition.CompiledLootMarkers.Count; i++)
            {
                var marker = definition.CompiledLootMarkers[i];
                if (marker?.Rule == null) continue;
                if (StructureSeededRandom.Value > Mathf.Clamp01(marker.Rule.Chance)) continue;

                var minCount = Mathf.Max(1, marker.Rule.Min);
                var maxCount = Mathf.Max(minCount, marker.Rule.Max);
                var rollCount = StructureSeededRandom.Range(minCount, maxCount + 1);
                var spawnPos = worldBase + new Vector2(marker.X, marker.Y);

                for (var rollIndex = 0; rollIndex < rollCount; rollIndex++)
                {
                    var rolledItem = RollLootItem(marker.Rule, definition.LootPools);
                    if (string.IsNullOrWhiteSpace(rolledItem)) continue;

                    var instance = CustomInstantiate.InstantiateReturn(rolledItem, spawnPos, Quaternion.identity);
                    if (instance == null)
                    {
                        if (MissingSpawnIdsLogged.Add(rolledItem))
                            CUCoreLibPlugin.Log?.LogWarning("Structure loot placement could not resolve item ID '" +
                                                            rolledItem + "'.");
                    }
                }
            }
        }

        private static string RollLootItem(LootRuleDef rule, Dictionary<string, LootPoolDef> pools)
        {
            if (rule == null || pools == null || string.IsNullOrWhiteSpace(rule.PoolID)) return null;
            if (!pools.TryGetValue(rule.PoolID, out var pool) || pool == null || pool.TotalWeight <= 0 ||
                pool.Entries == null || pool.Entries.Count == 0)
                return null;

            var roll = StructureSeededRandom.Range(0, pool.TotalWeight);
            for (var i = 0; i < pool.CumulativeWeights.Count; i++)
            {
                if (roll >= pool.CumulativeWeights[i]) continue;
                return pool.Entries[i].ItemID;
            }

            return pool.Entries[pool.Entries.Count - 1].ItemID;
        }

        private static void SpawnEntity(StructureEntityDef definition, Vector2 position)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.ID)) return;

            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(definition.ID);
            if (string.IsNullOrWhiteSpace(normalizedId)) return;

            float? condition = null;
            if (definition.CustomProperties != null &&
                definition.CustomProperties.TryGetValue(ConditionPercentKey, out var rawCondition) &&
                float.TryParse(rawCondition, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedCondition))
                condition = Mathf.Clamp(parsedCondition, 0f, 200f) * 0.01f;

            var rotation = Quaternion.Euler(0f, 0f, -definition.Rotation);
            var instance = CustomInstantiate.InstantiateReturn(normalizedId, position, rotation, condition);
            if (instance == null)
            {
                try
                {
                    instance = Utils.Create(normalizedId, position, 0f);
                }
                catch
                {
                    instance = null;
                }
            }

            if (instance == null)
            {
                if (MissingSpawnIdsLogged.Add(normalizedId))
                    CUCoreLibPlugin.Log?.LogWarning("Structure placement could not resolve entity or item ID '" +
                                                    normalizedId + "'.");
                return;
            }

            var scaleX = definition.FlipX ? -definition.Scale : definition.Scale;
            var scaleY = definition.FlipY ? -definition.Scale : definition.Scale;
            if (!Mathf.Approximately(scaleX, 1f) || !Mathf.Approximately(scaleY, 1f))
                instance.transform.localScale = new Vector3(scaleX, scaleY, instance.transform.localScale.z);

            if (instance.TryGetComponent<BuildingEntity>(out var building))
            {
                building.blockPlacedOn = WorldGeneration.world.WorldToBlockPos(position + Vector2.down * 0.5f);
                building.requireGround = false;

                if (definition.CustomProperties != null &&
                    definition.CustomProperties.TryGetValue(HealthKey, out var rawHealth) &&
                    float.TryParse(rawHealth, NumberStyles.Float, CultureInfo.InvariantCulture, out var health) &&
                    health > 0f)
                    building.health = health;
            }
        }

        private static void SpawnBackgroundTile(int blockId, Vector2 position)
        {
            var world = WorldGeneration.world;
            if (world?.tiles == null || blockId < 0 || blockId >= world.tiles.Length) return;
            if (!(world.tiles[blockId] is Tile tile) || tile.sprite == null) return;

            var template = GetBackgroundTemplate();
            if (template == null) return;

            var backgroundObject = UnityEngine.Object.Instantiate(template, position, Quaternion.identity);
            backgroundObject.SetActive(true);
            backgroundObject.name = "CUCoreLib_BGTile_" + blockId;

            if (world.worldGrid != null) backgroundObject.transform.SetParent(world.worldGrid.transform);

            var renderer = backgroundObject.GetComponent<SpriteRenderer>();
            renderer.sprite = tile.sprite;
            renderer.material = world.defaultMat;
            renderer.color = world.GetBackgroundColor();
            renderer.sortingOrder = -998;
        }

        private static GameObject GetBackgroundTemplate()
        {
            if (BackgroundTemplate != null) return BackgroundTemplate;

            BackgroundTemplate = new GameObject("CUCoreLib_BackgroundTileTemplate");
            BackgroundTemplate.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(BackgroundTemplate);
            BackgroundTemplate.AddComponent<SpriteRenderer>();
            return BackgroundTemplate;
        }

        private static bool TryFindSpawnPosition(WorldGeneration world, RegisteredStructureDefinition definition,
            List<StructurePlacementRect> occupiedRects, out Vector2 position, out StructurePlacementRect chosenRect)
        {
            position = Vector2.zero;
            chosenRect = default;

            var worldWidth = (int)world.width;
            var worldHeight = (int)world.height;
            if (worldWidth <= 100 || worldHeight <= 100) return false;

            var lastRandX = 0;
            var lastRandY = 0;
            var lastTriedRect = default(StructurePlacementRect);
            var requireNoOverlap = definition != null && definition.AvoidOverlap;

            for (var attempt = 0; attempt < NonOverlapPlacementMaxAttempts; attempt++)
            {
                lastRandX = StructureSeededRandom.Range(50, worldWidth - 50);
                lastRandY = StructureSeededRandom.Range(50, worldHeight - 50);
                lastTriedRect = BuildPlacementRect(lastRandX, lastRandY, definition.CompiledStructure.Width,
                    definition.CompiledStructure.Height, worldWidth, worldHeight);

                if (!requireNoOverlap || !OverlapsAny(lastTriedRect, occupiedRects))
                {
                    position = world.BlockToWorldPos(new Vector2Int(lastRandX, lastRandY));
                    chosenRect = lastTriedRect;
                    return true;
                }
            }

            position = world.BlockToWorldPos(new Vector2Int(lastRandX, lastRandY));
            chosenRect = lastTriedRect;
            return true;
        }

        private static StructurePlacementRect BuildPlacementRect(int centerX, int centerY, int structureWidth,
            int structureHeight, int worldWidth, int worldHeight)
        {
            var startX = centerX - (structureWidth / 2);
            var startY = centerY - (structureHeight / 2);
            var endXExclusive = startX + structureWidth;
            var endYExclusive = startY + structureHeight;

            return new StructurePlacementRect
            {
                MinX = Mathf.Max(0, startX),
                MinY = Mathf.Max(0, startY),
                MaxXExclusive = Mathf.Min(worldWidth, endXExclusive),
                MaxYExclusive = Mathf.Min(worldHeight, endYExclusive)
            };
        }

        private static bool OverlapsAny(StructurePlacementRect candidate, List<StructurePlacementRect> occupied)
        {
            if (!HasArea(candidate) || occupied == null || occupied.Count == 0) return false;

            for (var i = 0; i < occupied.Count; i++)
            {
                var other = occupied[i];
                if (!HasArea(other)) continue;

                var separated =
                    candidate.MaxXExclusive <= other.MinX ||
                    candidate.MinX >= other.MaxXExclusive ||
                    candidate.MaxYExclusive <= other.MinY ||
                    candidate.MinY >= other.MaxYExclusive;

                if (!separated) return true;
            }

            return false;
        }

        private static bool HasArea(StructurePlacementRect rect)
        {
            return rect.MaxXExclusive > rect.MinX && rect.MaxYExclusive > rect.MinY;
        }

        private static Dictionary<int, char> BuildGlobalBlockReverseMap()
        {
            var reverse = new Dictionary<int, char>();
            reverse[-1] = ' ';
            reverse[0] = '.';
            reverse[1] = 'R';
            reverse[2] = 'g';
            reverse[3] = 'S';
            reverse[4] = 'T';
            reverse[5] = '#';
            reverse[6] = 'M';
            reverse[7] = 'G';
            reverse[8] = 'r';
            reverse[9] = 'P';
            reverse[10] = 'H';
            reverse[11] = 'W';
            reverse[12] = 's';
            reverse[13] = 'd';
            reverse[14] = 'I';
            reverse[15] = 'c';
            reverse[16] = 'o';
            reverse[17] = 'n';
            reverse[18] = 'm';
            reverse[19] = 'l';
            reverse[20] = 'B';
            reverse[21] = 'X';
            reverse[22] = 'x';
            reverse[23] = 'v';
            reverse[24] = 'L';
            reverse[25] = 'e';
            reverse[26] = 'w';
            reverse[27] = '=';
            reverse[28] = '-';
            reverse[29] = 'p';
            reverse[30] = 'h';
            reverse[31] = 'f';
            reverse[32] = 'b';
            reverse[33] = '^';
            reverse[34] = 'C';
            reverse[35] = 'i';
            return reverse;
        }

        private sealed class RegisteredStructureDefinition
        {
            public string ID;
            public string[] Shape;
            public string[] BackgroundShape;
            public bool AvoidOverlap;
            public Dictionary<char, string> EntityMap;
            public Dictionary<int, ItemAssignmentDef> ItemAssignmentsByCell;
            public Dictionary<int, string> ObjectAssignmentsByCell;
            public Dictionary<int, Dictionary<string, string>> ItemCustomPropertiesByCell;
            public Dictionary<int, Dictionary<string, string>> ObjectCustomPropertiesByCell;
            public string SequentialItems;
            public List<EntityPrecisePos> PrecisePlacements;
            public int[] SpawnCounts;
            public int TerrainGenAreaCount;
            public Dictionary<char, LootRuleDef> LootRulesByMarker;
            public Dictionary<string, LootPoolDef> LootPools;
            public CustomStructure CompiledStructure;
            public List<CompiledLootMarker> CompiledLootMarkers;
        }

        private sealed class NormalizedLayer
        {
            public string ID;
            public string Kind;
            public bool Visible;
            public char[] Cells;
        }

        private sealed class CustomStructure
        {
            public int Width;
            public int Height;
            public int[,] BlockIDs;
            public int[,] BackgroundIDs;
            public int[,] LiquidIDs;
            public List<StructureEntityDef> Entities = new List<StructureEntityDef>();
        }

        private sealed class StructureEntityDef
        {
            public string ID;
            public Dictionary<string, string> CustomProperties;
            public float X;
            public float Y;
            public float Rotation;
            public bool FlipX;
            public bool FlipY;
            public float Scale = 1f;
        }

        private sealed class EntityPrecisePos
        {
            public int GridX;
            public int GridY;
            public float OffsetX;
            public float OffsetY;
            public float Rotation;
            public bool FlipX;
            public bool FlipY;
            public float Scale = 1f;
        }

        private sealed class ItemAssignmentEntryDef
        {
            public string Value;
            public float Percent = 100f;
            public float ConditionPercent = 100f;
        }

        private sealed class ItemAssignmentDef
        {
            public string Mode = "single";
            public int MaxDrops = 1;
            public bool RollIndependent = true;
            public List<ItemAssignmentEntryDef> Entries = new List<ItemAssignmentEntryDef>();
        }

        private sealed class LootPoolEntryDef
        {
            public string ItemID;
            public int Weight;
        }

        private sealed class LootPoolDef
        {
            public string ID;
            public List<LootPoolEntryDef> Entries = new List<LootPoolEntryDef>();
            public List<int> CumulativeWeights = new List<int>();
            public int TotalWeight;
        }

        private sealed class LootRuleDef
        {
            public char Marker;
            public float Chance = 1f;
            public string PoolID;
            public int Min = 1;
            public int Max = 1;
        }

        private sealed class CompiledLootMarker
        {
            public char Marker;
            public LootRuleDef Rule;
            public float X;
            public float Y;
        }

        private struct StructurePlacementRect
        {
            public int MinX;
            public int MinY;
            public int MaxXExclusive;
            public int MaxYExclusive;
        }

        private static class StructureSeededRandom
        {
            private static bool Initialized;
            private static bool QolModPresent;
            private static Type SeedManagerType;
            private static FieldInfo IsSeededField;
            private static FieldInfo CurrentSeedField;
            private static Type SeededRunPatcherType;
            private static MethodInfo GetSeededRangeInt;
            private static System.Random IsolatedRng;
            private static readonly Stack<System.Random> RngOverrides = new Stack<System.Random>();

            public static IDisposable PushOverride(int seed)
            {
                RngOverrides.Push(new System.Random(seed));
                return new SeededRandomOverrideScope();
            }

            public static void InitializeForStructures()
            {
                EnsureInitialized();
                if (IsSeeded && WorldGeneration.world != null)
                {
                    unchecked
                    {
                        var totalTraveled = WorldGeneration.world.totalTraveled;
                        var structureSeed = CurrentSeed + (totalTraveled * 265443576) + 99999;
                        IsolatedRng = new System.Random(structureSeed);
                    }
                }
                else
                {
                    IsolatedRng = null;
                }
            }

            public static int Range(int min, int max)
            {
                EnsureInitialized();

                if (RngOverrides.Count > 0) return RngOverrides.Peek().Next(min, max);
                if (IsolatedRng != null) return IsolatedRng.Next(min, max);

                if (QolModPresent && IsSeeded && GetSeededRangeInt != null)
                {
                    try
                    {
                        return (int)GetSeededRangeInt.Invoke(null, new object[] { min, max });
                    }
                    catch
                    {
                    }
                }

                return UnityEngine.Random.Range(min, max);
            }

            public static float Value
            {
                get
                {
                    EnsureInitialized();

                    if (RngOverrides.Count > 0) return (float)RngOverrides.Peek().NextDouble();
                    if (IsolatedRng != null) return (float)IsolatedRng.NextDouble();

                    return UnityEngine.Random.value;
                }
            }

            private static bool IsSeeded
            {
                get
                {
                    EnsureInitialized();
                    if (!QolModPresent || IsSeededField == null) return false;
                    return (bool)IsSeededField.GetValue(null);
                }
            }

            private static int CurrentSeed
            {
                get
                {
                    EnsureInitialized();
                    if (!QolModPresent || CurrentSeedField == null) return 0;
                    return (int)CurrentSeedField.GetValue(null);
                }
            }

            private static void EnsureInitialized()
            {
                if (Initialized) return;
                Initialized = true;

                try
                {
                    if (!Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.qolunknown")) return;

                    SeedManagerType = AccessTools.TypeByName("QoL_Unknown.SeedManager");
                    SeededRunPatcherType = AccessTools.TypeByName("QoL_Unknown.SeededRunPatcher");
                    if (SeedManagerType != null)
                    {
                        IsSeededField = AccessTools.Field(SeedManagerType, "IsSeeded");
                        CurrentSeedField = AccessTools.Field(SeedManagerType, "CurrentSeed");
                    }

                    if (SeededRunPatcherType != null)
                        GetSeededRangeInt = AccessTools.Method(SeededRunPatcherType, "GetSeededRange",
                            new[] { typeof(int), typeof(int) });

                    QolModPresent = SeedManagerType != null && IsSeededField != null;
                }
                catch
                {
                    QolModPresent = false;
                }
            }

            private sealed class SeededRandomOverrideScope : IDisposable
            {
                private bool disposed;

                public void Dispose()
                {
                    if (disposed) return;
                    disposed = true;

                    if (RngOverrides.Count > 0) RngOverrides.Pop();
                }
            }
        }
    }
}
