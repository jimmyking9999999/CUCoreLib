using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CUCoreLib.Registries
{
    public static class TileRegistry
    {
        private const string HitSoundTokenPrefix = "CUCoreLib.TileHitSound.";

        public const ushort FirstCustomTileIndex = 36;
        // TODO need to make this dynamic, don't want this to be based off people agreeing to use certain indices
        // I think encoding a string ID to an int is fine

        private static readonly Dictionary<ushort, CustomTileDefinition> RegisteredDefinitions =
            new Dictionary<ushort, CustomTileDefinition>();

        private static readonly Dictionary<string, ushort> RegisteredDefinitionIds =
            new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<ushort, AudioClip> ResolvedHitSounds =
            new Dictionary<ushort, AudioClip>();

        private static readonly HashSet<ushort> ResolvingHitSounds =
            new HashSet<ushort>();

        private static readonly Dictionary<ushort, TileBase> RegisteredTiles =
            new Dictionary<ushort, TileBase>();

        private static readonly Dictionary<ushort, TileBase> ReservedTiles =
            new Dictionary<ushort, TileBase>();

        private static readonly FieldInfo WorldBlocksField =
            AccessTools.Field(typeof(WorldGeneration), "worldBlocks");

        private static readonly TileGenerationStyle[] AllGenerationStyles =
        {
            TileGenerationStyle.Vein,
            TileGenerationStyle.HeavyVeins,
            TileGenerationStyle.Singular,
            TileGenerationStyle.Stripe,
            TileGenerationStyle.Inner,
            TileGenerationStyle.Outskirt
            // Potentially add flags for location/specific. Eg. bottom/left side of the world, near buildingEntites, etc..
            // Custom Structures layer update when? ;)
        };

        public static bool Register(ushort tileIndex, CustomTileDefinition definition)
        {
            if (tileIndex < FirstCustomTileIndex)
            {
                CUCoreLibPlugin.Log?.LogWarning(
                    $"Tile index {tileIndex} is reserved by the base game. Custom tile indices must be {FirstCustomTileIndex} or higher.");
                return false;
            }

            if (definition == null)
            {
                CUCoreLibPlugin.Log?.LogWarning($"Tile registration ignored for index {tileIndex} with no definition.");
                return false;
            }

            if (definition.Sprite == null)
            {
                CUCoreLibPlugin.Log?.LogWarning($"Tile registration ignored for index {tileIndex} with no sprite.");
                return false;
            }

            if (RegisteredDefinitions.ContainsKey(tileIndex))
            {
                CUCoreLibPlugin.Log?.LogWarning($"Tile index {tileIndex} is already registered, potentially from another mod.");
                return false;
            }

            definition.ID = string.IsNullOrWhiteSpace(definition.ID)
                ? "customtile" + tileIndex
                : definition.ID.Trim();

            RegisteredDefinitions.Add(tileIndex, definition);
            RegisteredDefinitionIds[definition.ID] = tileIndex;
            RegisteredTiles.Add(tileIndex, CreateTile(definition));

            if (!string.IsNullOrEmpty(definition.Name))
            {
                LocaleRegistry.Register("other", definition.ID, definition.Name);
            }

            if (!string.IsNullOrEmpty(definition.Description))
            {
                LocaleRegistry.Register("other", definition.ID + "dsc", definition.Description);
            }

            InjectRegisteredTiles(WorldGeneration.world);
            return true;
        }

        public static bool TryGetDefinition(ushort tileIndex, out CustomTileDefinition definition)
        {
            return RegisteredDefinitions.TryGetValue(tileIndex, out definition);
        }

        public static bool TryGetTile(ushort tileIndex, out TileBase tile)
        {
            return RegisteredTiles.TryGetValue(tileIndex, out tile);
        }

        public static IEnumerable<ushort> GetRegisteredIndices()
        {
            return RegisteredDefinitions.Keys.OrderBy(index => index).ToArray();
        }

        public static int AllSpawnLayersMask => -1;

        public static int LayerToMask(int layerNumber)
        {
            if (layerNumber <= 0 || layerNumber > 31)
            {
                return 0;
            }

            return 1 << (layerNumber - 1);
        }

        public static int LayersToMask(params int[] layerNumbers)
        {
            if (layerNumbers == null || layerNumbers.Length == 0)
            {
                return 0;
            }

            int mask = 0;
            foreach (int layerNumber in layerNumbers)
            {
                mask |= LayerToMask(layerNumber);
            }

            return mask;
        }

        public static int AllLayersExcept(params int[] excludedLayerNumbers)
        {
            int mask = AllSpawnLayersMask;
            if (excludedLayerNumbers == null || excludedLayerNumbers.Length == 0)
            {
                return mask;
            }

            foreach (int layerNumber in excludedLayerNumbers)
            {
                int layerMask = LayerToMask(layerNumber);
                if (layerMask != 0)
                {
                    mask &= ~layerMask;
                }
            }

            return mask;
        }

        internal static JObject CaptureNetworkSnapshot()
        {
            JObject root = new JObject();
            foreach (KeyValuePair<ushort, CustomTileDefinition> entry in RegisteredDefinitions)
            {
                CustomTileDefinition definition = entry.Value;
                if (definition == null)
                {
                    continue;
                }

                root[entry.Key.ToString()] = new JObject
                {
                    ["id"] = definition.ID ?? string.Empty,
                    ["name"] = definition.Name ?? string.Empty,
                    ["description"] = definition.Description ?? string.Empty,
                    ["sprite"] = NetworkSnapshotSerialization.WriteSprite(definition.Sprite),
                    ["tileName"] = definition.TileName ?? string.Empty,
                    ["color"] = NetworkSnapshotSerialization.WriteColor(definition.Color),
                    ["colliderType"] = (int)definition.ColliderType,
                    ["health"] = definition.Health,
                    ["hitSound"] = definition.HitSound ?? string.Empty,
                    ["stepSound"] = definition.StepSound ?? string.Empty,
                    ["sleepQuality"] = (int)definition.SleepQuality,
                    ["noVariation"] = definition.NoVariation,
                    ["metallic"] = definition.Metallic,
                    ["toxicity"] = definition.Toxicity,
                    ["slippery"] = definition.Slippery,
                    ["spawnAmount"] = definition.SpawnAmount,
                    ["spawnLayers"] = definition.SpawnLayers,
                    ["generationStyle"] = (byte)definition.GenerationStyle,
                    ["drops"] = JArray.FromObject(definition.Drops),
                    ["customData"] = definition.CustomData != null ? JObject.FromObject(definition.CustomData) : new JObject()
                };
            }

            return root;
        }

        internal static void ApplyNetworkSnapshot(JObject snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (JProperty property in snapshot.Properties())
            {
                if (!ushort.TryParse(property.Name, out ushort tileIndex))
                {
                    continue;
                }

                JObject obj = property.Value as JObject;
                if (obj == null)
                {
                    continue;
                }

                CustomTileDefinition definition = new CustomTileDefinition
                {
                    ID = obj.Value<string>("id"),
                    Name = obj.Value<string>("name"),
                    Description = obj.Value<string>("description"),
                    Sprite = NetworkSnapshotSerialization.ReadSprite(obj["sprite"]),
                    TileName = obj.Value<string>("tileName"),
                    Color = NetworkSnapshotSerialization.ReadColor(obj["color"], Color.white),
                    ColliderType = (Tile.ColliderType)(obj.Value<int?>("colliderType") ?? 1),
                    Health = obj.Value<float?>("health") ?? 100f,
                    HitSound = obj.Value<string>("hitSound"),
                    StepSound = obj.Value<string>("stepSound"),
                    SleepQuality = (Body.SleepQuality)(obj.Value<int?>("sleepQuality") ?? 0),
                    NoVariation = obj.Value<bool?>("noVariation") ?? false,
                    Metallic = obj.Value<bool?>("metallic") ?? false,
                    Toxicity = obj.Value<float?>("toxicity") ?? 0f,
                    Slippery = obj.Value<bool?>("slippery") ?? false,
                    SpawnAmount = obj.Value<float?>("spawnAmount") ?? 0f,
                    SpawnLayers = obj.Value<int?>("spawnLayers") ?? AllSpawnLayersMask,
                    GenerationStyle = (TileGenerationStyle)(obj.Value<byte?>("generationStyle") ?? (byte)TileGenerationStyle.Vein)
                };

                JArray drops = obj["drops"] as JArray;
                if (drops != null)
                {
                    definition.Drops = drops.ToObject<ItemDrop[]>();
                }

                if (obj["customData"] is JObject customData)
                {
                    definition.CustomData = customData.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                }

                Register(tileIndex, definition);
            }
        }

        public static bool TryGetCustomData<T>(ushort tileIndex, string key, out T value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (!RegisteredDefinitions.TryGetValue(tileIndex, out var definition)) return false;
            if (definition.CustomData == null || !definition.CustomData.TryGetValue(key, out object rawValue)) return false;
            if (!(rawValue is T typedValue)) return false;

            value = typedValue;
            return true;
        }

        internal static bool WillBreak(WorldGeneration world, Vector2Int position, float damage, bool bonusMetal)
        {
            if (world == null) return false;

            ushort tileIndex = world.GetBlock(position);
            if (!RegisteredDefinitions.TryGetValue(tileIndex, out var definition)) return false;

            BlockDamage existingDamage = world.GetBlockDamage(position);
            float appliedDamage = damage * (bonusMetal && definition.Metallic ? 10f : 1f);
            return (existingDamage?.damage ?? 0f) + appliedDamage >= definition.Health;
        }

        internal static void SpawnDrops(WorldGeneration world, Vector2Int position, ushort tileIndex)
        {
            if (world == null) return;
            if (!RegisteredDefinitions.TryGetValue(tileIndex, out var definition)) return;
            if (definition.Drops == null || definition.Drops.Length == 0) return;

            Vector3 worldPosition = world.BlockToWorldPos(position);
            foreach (ItemDrop drop in definition.Drops)
            {
                if (drop == null || string.IsNullOrWhiteSpace(drop.id)) continue;
                if (UnityEngine.Random.Range(0f, 1f) >= drop.chance) continue;

                GameObject spawned = CustomInstantiate.InstantiateReturn(
                    drop.id,
                    worldPosition,
                    Quaternion.identity,
                    UnityEngine.Random.Range(drop.conditionMin, drop.conditionMax));

                if (spawned == null)
                {
                    CUCoreLibPlugin.Log?.LogWarning(
                        "Custom tile '" + definition.ID + "' failed to spawn drop '" + drop.id + "'.");
                }
            }
        }

        internal static void GenerateWorldTiles(WorldGeneration world)
        {
            if (world == null || RegisteredDefinitions.Count == 0)
            {
                return;
            }

            ushort[,] worldBlocks = WorldBlocksField?.GetValue(world) as ushort[,];
            if (worldBlocks == null)
            {
                return;
            }

            foreach (KeyValuePair<ushort, CustomTileDefinition> entry in RegisteredDefinitions)
            {
                if (entry.Value == null || entry.Value.SpawnAmount <= 0)
                {
                    continue;
                }

                if (!CanSpawnInLayer(entry.Value, world.biomeDepth))
                {
                    continue;
                }

                GenerateWorldTile(entry.Value, world, worldBlocks, entry.Key);
            }
        }

        public static bool SetBlock(WorldGeneration world, Vector2Int position, ushort tileIndex)
        {
            if (world == null || !RegisteredDefinitions.ContainsKey(tileIndex)) return false;

            InjectRegisteredTiles(world);
            world.SetBlock(position, tileIndex);
            return true;
        }

        public static bool SetBlockNoUpdate(WorldGeneration world, Vector2Int position, ushort tileIndex)
        {
            if (world == null || !RegisteredDefinitions.ContainsKey(tileIndex)) return false;

            InjectRegisteredTiles(world);
            world.SetBlockNoUpdate(position, tileIndex);
            return true;
        }

        internal static void InjectRegisteredTiles(WorldGeneration world)
        {
            if (world == null || RegisteredTiles.Count == 0) return;

            int requiredLength = RegisteredTiles.Keys.Max(index => (int)index) + 1;
            if (world.tiles == null)
            {
                world.tiles = new TileBase[requiredLength];
            }
            else if (world.tiles.Length < requiredLength)
            {
                Array.Resize(ref world.tiles, requiredLength);
            }

            foreach (KeyValuePair<ushort, TileBase> entry in RegisteredTiles)
            {
                world.tiles[entry.Key] = entry.Value;
            }

            for (int i = FirstCustomTileIndex; i < requiredLength; i++)
            {
                if (world.tiles[i] == null)
                {
                    world.tiles[i] = GetReservedTile((ushort)i);
                }
            }
        }

        internal static BlockInfo CreateBlockInfo(ushort tileIndex, CustomTileDefinition definition)
        {
            return new BlockInfo
            {
                name = Locale.GetOther(definition.ID),
                health = definition.Health,
                hitsound = GetHitSoundToken(tileIndex),
                stepsound = definition.StepSound,
                sleep = definition.SleepQuality,
                noVariation = definition.NoVariation,
                metallic = definition.Metallic,
                toxicity = definition.Toxicity,
                slippery = definition.Slippery
            };
        }

        private static TileBase CreateTile(CustomTileDefinition definition)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = definition.Sprite;
            tile.color = definition.Color;
            tile.name = string.IsNullOrWhiteSpace(definition.TileName) ? definition.ID : definition.TileName.Trim();
            tile.colliderType = definition.ColliderType;
            return tile;
        }

        internal static string GetHitSoundToken(ushort tileIndex)
        {
            return HitSoundTokenPrefix + tileIndex;
        }

        internal static bool TryPlayHitSoundToken(string token, Vector2 pos, bool twoDimensional, bool pitchShift, Transform follow, float volume, float pitch, bool noReverb, bool ignoreMixer, out AudioSource audioSource)
        {
            audioSource = null;
            if (!TryGetTokenTileIndex(token, out ushort tileIndex)) return false;

            AudioClip clip = ResolveHitSound(tileIndex);
            if (clip == null) return false;

            audioSource = Sound.Play(clip, pos, twoDimensional, pitchShift, follow, volume, pitch, noReverb, ignoreMixer);
            return audioSource != null;
        }

        private static bool TryGetTokenTileIndex(string token, out ushort tileIndex)
        {
            tileIndex = 0;
            if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(HitSoundTokenPrefix, StringComparison.Ordinal)) return false;

            return ushort.TryParse(token.Substring(HitSoundTokenPrefix.Length), out tileIndex);
        }

        private static AudioClip ResolveHitSound(ushort tileIndex)
        {
            if (ResolvedHitSounds.TryGetValue(tileIndex, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            if (!RegisteredDefinitions.TryGetValue(tileIndex, out CustomTileDefinition definition) || definition == null)
            {
                return null;
            }

            if (!ResolvingHitSounds.Add(tileIndex))
            {
                return null;
            }

            try
            {
                AudioClip clip = definition.HitSoundClip;
                if (clip == null)
                {
                    TryResolveTileHitSoundReference(definition.HitSound, out clip);
                }

                if (clip != null)
                {
                    ResolvedHitSounds[tileIndex] = clip;
                }

                return clip;
            }
            finally
            {
                ResolvingHitSounds.Remove(tileIndex);
            }
        }

        private static bool TryResolveTileHitSoundReference(string reference, out AudioClip resolved)
        {
            resolved = null;
            if (string.IsNullOrWhiteSpace(reference)) return false;

            string normalized = reference.Trim();
            switch (normalized.ToLowerInvariant())
            {
                case "metal":
                    normalized = "turret";
                    break;
                case "rubber":
                    normalized = "glowplant";
                    break;
                case "rustle":
                    normalized = "geotree";
                    break;
                case "crystal":
                    normalized = "BloodCrystal";
                    break;
                case "flesh":
                    normalized = "shadecrawler";
                    break;
                case "pop":
                    normalized = "pop";
                    break;
                case "ice":
                case "glass":
                    normalized = "icestalagmite";
                    break;
                case "stone":
                case "rock":
                    normalized = "stoneplant";
                    break;
                case "chain":
                    normalized = "barbedwirefence";
                    break;
            }

            if (RegisteredDefinitionIds.TryGetValue(normalized, out ushort targetTileIndex))
            {
                resolved = ResolveHitSound(targetTileIndex);
                return resolved != null;
            }

            AudioClip clip = AssetLoader.GetCachedAudioClip(normalized) ?? Resources.Load<AudioClip>("Sounds/" + normalized);
            if (clip != null)
            {
                AssetLoader.CacheAudioClip(normalized, clip);
                resolved = clip;
                return true;
            }

            GameObject buildingReference = Resources.Load<GameObject>(normalized);
            if (buildingReference != null && buildingReference.TryGetComponent(out BuildingEntity building) && building.hitSound != null)
            {
                resolved = building.hitSound;
                if (!string.IsNullOrWhiteSpace(resolved.name))
                {
                    AssetLoader.CacheAudioClip(resolved.name, resolved);
                }
                return true;
            }

            return false;
        }

        private static TileBase GetReservedTile(ushort index)
        {
            if (ReservedTiles.TryGetValue(index, out TileBase reservedTile))
            {
                return reservedTile;
            }

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = "CUCoreLibReservedTile" + index;
            tile.colliderType = Tile.ColliderType.None;
            ReservedTiles[index] = tile;
            return tile;
        }

        private static bool CanSpawnInLayer(CustomTileDefinition definition, int biomeDepth)
        {
            if (definition == null)
            {
                return false;
            }

            int spawnLayers = definition.SpawnLayers;
            if (spawnLayers == 0)
            {
                return false;
            }

            if (spawnLayers == AllSpawnLayersMask)
            {
                return true;
            }

            int layerNumber = biomeDepth + 1;
            int layerMask = LayerToMask(layerNumber);
            return layerMask != 0 && (spawnLayers & layerMask) != 0;
        }

        private static void GenerateWorldTile(CustomTileDefinition definition, WorldGeneration world, ushort[,] worldBlocks, ushort tileIndex)
        {
            TileGenerationStyle styleMask = GetGenerationStyles(definition);
            int styleCount = CountGenerationStyles(styleMask);
            if (styleCount <= 0)
            {
                return;
            }

            float normalizedSpawnAmount = Mathf.Max(0f, definition.SpawnAmount) / styleCount;
            if (normalizedSpawnAmount <= 0f)
            {
                return;
            }

            foreach (TileGenerationStyle style in AllGenerationStyles)
            {
                if ((styleMask & style) == 0)
                {
                    continue;
                }

                GenerateWorldTileStyle(world, worldBlocks, tileIndex, normalizedSpawnAmount, style);
            }
        }

        private static TileGenerationStyle GetGenerationStyles(CustomTileDefinition definition)
        {
            if (definition == null || definition.GenerationStyle == TileGenerationStyle.None)
            {
                return TileGenerationStyle.Vein;
            }

            return definition.GenerationStyle;
        }

        private static int CountGenerationStyles(TileGenerationStyle styleMask)
        {
            int count = 0;
            foreach (TileGenerationStyle style in AllGenerationStyles)
            {
                if ((styleMask & style) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static void GenerateWorldTileStyle(
            WorldGeneration world,
            ushort[,] worldBlocks,
            ushort tileIndex,
            float spawnAmount,
            TileGenerationStyle style)
        {
            switch (style)
            {
                case TileGenerationStyle.HeavyVeins:
                    GenerateOreVeins(world, worldBlocks, tileIndex, spawnAmount, 2f, 18, 42);
                    break;
                case TileGenerationStyle.Singular:
                    GenerateSingular(world, worldBlocks, tileIndex, spawnAmount);
                    break;
                case TileGenerationStyle.Stripe:
                    GenerateStripes(world, worldBlocks, tileIndex, spawnAmount);
                    break;
                case TileGenerationStyle.Inner:
                    GenerateClusteredCircles(world, tileIndex, spawnAmount, innerBias: true);
                    break;
                case TileGenerationStyle.Outskirt:
                    GenerateClusteredCircles(world, tileIndex, spawnAmount, innerBias: false);
                    break;
                case TileGenerationStyle.Vein:
                default:
                    GenerateOreVeins(world, worldBlocks, tileIndex, spawnAmount, 1f, 1, 25);
                    break;
            }
        }

        private static void GenerateOreVeins(
            WorldGeneration world,
            ushort[,] worldBlocks,
            ushort tileIndex,
            float spawnAmount,
            float attemptMultiplier,
            int minSteps,
            int maxStepsExclusive)
        {
            int attempts = GetCopperStyleAttempts(world, spawnAmount * Mathf.Max(0f, attemptMultiplier));

            for (int attempt = attempts; attempt > 0; attempt--)
            {
                Vector2Int position = new Vector2Int(
                    Mathf.FloorToInt(UnityEngine.Random.Range(0f, world.width)),
                    Mathf.FloorToInt(UnityEngine.Random.Range(0f, world.height)));

                for (int steps = UnityEngine.Random.Range(minSteps, maxStepsExclusive); steps > 0; steps--)
                {
                    if (position.x > 0
                        && position.x < world.width - 1
                        && position.y > 0
                        && position.y < world.height - 1
                        && worldBlocks[position.x, position.y] > 0)
                    {
                        worldBlocks[position.x, position.y] = tileIndex;
                    }

                    position += new Vector2Int(
                        UnityEngine.Random.value > 0.5f ? (UnityEngine.Random.value > 0.5f ? 1 : -1) : 0,
                        UnityEngine.Random.value > 0.5f ? (UnityEngine.Random.value > 0.5f ? 1 : -1) : 0);
                }
            }
        }

        private static void GenerateSingular(WorldGeneration world, ushort[,] worldBlocks, ushort tileIndex, float spawnAmount)
        {
            int attempts = GetCopperStyleAttempts(world, spawnAmount);
            for (int attempt = attempts; attempt > 0; attempt--)
            {
                Vector2Int position = new Vector2Int(
                    Mathf.FloorToInt(UnityEngine.Random.Range(0f, world.width)),
                    Mathf.FloorToInt(UnityEngine.Random.Range(0f, world.height)));

                if (position.x <= 0
                    || position.x >= world.width - 1
                    || position.y <= 0
                    || position.y >= world.height - 1)
                {
                    continue;
                }

                if (worldBlocks[position.x, position.y] > 0)
                {
                    worldBlocks[position.x, position.y] = tileIndex;
                }
            }
        }

        private static void GenerateStripes(WorldGeneration world, ushort[,] worldBlocks, ushort tileIndex, float spawnAmount)
        {
            int stripeCount = Mathf.Max(1, Mathf.RoundToInt(GetCopperStyleAttempts(world, spawnAmount) / 12f));
            for (int stripe = 0; stripe < stripeCount; stripe++)
            {
                bool horizontal = UnityEngine.Random.value > 0.5f;
                int stripeWidth = UnityEngine.Random.Range(2, 6);
                int stripeLength = UnityEngine.Random.Range(18, 56);
                Vector2Int origin = new Vector2Int(
                    Mathf.FloorToInt(UnityEngine.Random.Range(0f, world.width)),
                    Mathf.FloorToInt(UnityEngine.Random.Range(0f, world.height)));

                for (int step = 0; step < stripeLength; step++)
                {
                    for (int widthOffset = -stripeWidth; widthOffset <= stripeWidth; widthOffset++)
                    {
                        int x = horizontal ? origin.x + step : origin.x + widthOffset;
                        int y = horizontal ? origin.y + widthOffset : origin.y + step;
                        TrySetGeneratedBlock(world, worldBlocks, x, y, tileIndex);
                    }
                }
            }
        }

        private static void GenerateClusteredCircles(WorldGeneration world, ushort tileIndex, float spawnAmount, bool innerBias)
        {
            int clusterCount = Mathf.Max(1, Mathf.RoundToInt(GetCopperStyleAttempts(world, spawnAmount) / 18f));
            float horizontalRadius = world.width * (innerBias ? 0.18f : 0.42f);
            float verticalRadius = world.height * (innerBias ? 0.18f : 0.42f);
            Vector2 center = new Vector2(world.halfWidth, world.halfHeight);

            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                Vector2 position = center + SampleEllipseOffset(horizontalRadius, verticalRadius, innerBias);
                int size = UnityEngine.Random.Range(innerBias ? 4 : 3, innerBias ? 9 : 7);
                float chance = innerBias ? 0.95f : 0.9f;
                float chanceEnd = innerBias ? 0.45f : 0.15f;
                world.GenerateBlockCircle(position, size, tileIndex, chance, chanceEnd, autoUpdateChunk: false);
            }
        }

        private static Vector2 SampleEllipseOffset(float horizontalRadius, float verticalRadius, bool innerBias)
        {
            float radius = innerBias
                ? Mathf.Sqrt(UnityEngine.Random.value) * 0.7f
                : Mathf.Lerp(0.72f, 1f, Mathf.Sqrt(UnityEngine.Random.value));

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(
                Mathf.Cos(angle) * horizontalRadius * radius,
                Mathf.Sin(angle) * verticalRadius * radius);
        }

        private static int GetCopperStyleAttempts(WorldGeneration world, float spawnAmount)
        {
            float oreAmount = WorldGeneration.GetRunSettingFloat("oreamount");
            return Mathf.RoundToInt((float)((int)(world.chunkWidth * world.chunkHeight) / 2) * oreAmount * Mathf.Max(0f, spawnAmount));
        }

        private static void TrySetGeneratedBlock(WorldGeneration world, ushort[,] worldBlocks, int x, int y, ushort tileIndex)
        {
            if (x <= 0 || x >= world.width - 1 || y <= 0 || y >= world.height - 1)
            {
                return;
            }

            if (worldBlocks[x, y] > 0)
            {
                worldBlocks[x, y] = tileIndex;
            }
        }
    }
}
