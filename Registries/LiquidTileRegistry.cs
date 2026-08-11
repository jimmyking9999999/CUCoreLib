using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using CUCoreLib.Registries.Infrastructure;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CUCoreLib.Registries
{
    public static class LiquidTileRegistry
    {
        private const byte FirstCustomWorldByte = 7;

        private static readonly Dictionary<string, CustomLiquidTileInfo> RegisteredTiles =
            new Dictionary<string, CustomLiquidTileInfo>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, byte> TileIdToWorldByte =
            new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<byte, string> WorldByteToTileId =
            new Dictionary<byte, string>();

        private static readonly RegistrationOwnershipIndex<string> TileOwners =
            new RegistrationOwnershipIndex<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> WarnedUnknownLiquidIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<int, LiquidTileTouchState> BodyTouchStates =
            new Dictionary<int, LiquidTileTouchState>();

        private static readonly FieldInfo LiquidParticlesField =
            AccessTools.Field(typeof(FluidManager), "liquidParticles");

        private static bool _summaryLogged;

        static LiquidTileRegistry()
        {
            RegisterVanillaMappings();
        }

        public static int AllSpawnLayersMask => SpawnLayerMask.All;

        public static void Register(string id, CustomLiquidTileInfo info)
        {
            ContentReloadSession.AssertNotActive("LiquidTileRegistry.Register()",
                "Liquid tile registration is excluded from strict content reload.");

            if (string.IsNullOrWhiteSpace(id))
            {
                CUCoreLibPlugin.Log?.LogWarning("Ignored liquid tile registration with no ID.");
                return;
            }

            id = id.Trim();
            info = info ?? new CustomLiquidTileInfo();

            if (string.IsNullOrWhiteSpace(info.LiquidId))
                info.LiquidId = id;

            if (string.IsNullOrWhiteSpace(info.FillLiquidId))
                info.FillLiquidId = info.LiquidId;

            if (!LiquidExists(info.LiquidId))
                WarnUnknownLogicalLiquid(id, info.LiquidId, nameof(info.LiquidId));

            if (!LiquidExists(info.FillLiquidId))
                WarnUnknownLogicalLiquid(id, info.FillLiquidId, nameof(info.FillLiquidId));

            RegisteredTiles[id] = info;
            TileOwners.Assign(id, ContentReloadSession.ResolveAmbientOwnerId());
            EnsureWorldByteAssigned(id);
            EnsureFluidMappings();
            EnsureVisualCapacity(FluidManager.main);
            LogSummary();
        }

        public static IDisposable BeginOwnerRegistration(string ownerId)
        {
            return TileOwners.BeginScope(ownerId);
        }

        public static bool TryGet(string id, out CustomLiquidTileInfo info)
        {
            info = null;
            return !string.IsNullOrWhiteSpace(id) && RegisteredTiles.TryGetValue(id.Trim(), out info);
        }

        public static IEnumerable<string> GetRegisteredIds()
        {
            return RegisteredTiles.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static bool Place(string id, Vector2 worldPos)
        {
            if (WorldGeneration.world == null) return false;
            return Place(id, WorldGeneration.world.WorldToBlockPos(worldPos));
        }

        public static bool Place(string id, Vector2Int blockPos)
        {
            if (FluidManager.main == null || WorldGeneration.world == null) return false;
            if (!TryGetWorldByte(id, out var worldByte)) return false;
            if (blockPos.x < 0 || blockPos.y < 0 || blockPos.x >= WorldGeneration.world.width ||
                blockPos.y >= WorldGeneration.world.height) return false;
            if (WorldGeneration.world.GetBlock(blockPos) != 0) return false;

            FluidManager.main.SetLiquid(blockPos.x, blockPos.y, worldByte);
            QueueWorldBroadcast();
            return true;
        }

        public static bool FloodFill(string id, Vector2Int start, int maxFill = -1)
        {
            if (FluidManager.main == null || WorldGeneration.world == null) return false;
            if (!TryGetWorldByte(id, out var worldByte)) return false;

            if (TryGet(id, out var info))
                maxFill = maxFill > 0 ? maxFill : Mathf.Max(1, info.MaxFloodFill);
            else
                maxFill = Mathf.Max(1, maxFill);

            FluidManager.main.StartFill(start, worldByte, maxFill);
            QueueWorldBroadcast();
            return true;
        }

        public static void GenerateWorldTiles(WorldGeneration world)
        {
            if (world == null || RegisteredTiles.Count == 0 || FluidManager.main == null) return;

            foreach (var entry in RegisteredTiles.Where(entry => entry.Value != null && entry.Value.SpawnAmount > 0f))
            {
                if (!CanSpawnInLayer(entry.Value, world.biomeDepth)) continue;
                if (!TryGetWorldByte(entry.Key, out var worldByte)) continue;

                world.PlaceLiquids(entry.Value.SpawnAmount, worldByte, Mathf.Max(1, entry.Value.MaxFloodFill));
            }
        }

        public static bool TryGetWorldByte(string id, out byte worldByte)
        {
            worldByte = 0;
            return !string.IsNullOrWhiteSpace(id) && TileIdToWorldByte.TryGetValue(id.Trim(), out worldByte);
        }

        public static bool TryGetTileId(byte worldByte, out string id)
        {
            id = null;
            return WorldByteToTileId.TryGetValue(worldByte, out id);
        }

        public static bool TryGetWaterInfo(byte worldByte, out float buoyancy, out float drag, out int type)
        {
            buoyancy = 0f;
            drag = 0f;
            type = 0;

            if (!TryGetTileInfo(worldByte, out var info)) return false;
            buoyancy = info.Buoyancy;
            drag = info.Drag;
            type = worldByte;
            return true;
        }

        public static bool TryGetDisplayColor(byte worldByte, out Color color)
        {
            color = Color.clear;
            if (!TryGetTileInfo(worldByte, out var info)) return false;

            color = ResolveDisplayColor(worldByte, info);
            return true;
        }

        public static bool TryGetDisplayName(byte worldByte, out string name, out string description)
        {
            name = null;
            description = null;
            if (!TryGetTileInfo(worldByte, out var info)) return false;

            var liquidId = !string.IsNullOrWhiteSpace(info.LiquidId) ? info.LiquidId : WorldByteToTileId[worldByte];
            name = Locale.GetOther(liquidId);
            description = Locale.GetOther(liquidId + "dsc");
            return true;
        }

        public static bool TryDrinkLiquid(Vector2Int pos, Body body)
        {
            if (FluidManager.main == null || body == null) return false;

            var worldByte = FluidManager.main.GetLiquid(pos.x, pos.y);
            if (!TryGetTileInfo(worldByte, out var info)) return false;
            if (!ResolveDrinkLiquidType(info, out var liquidType)) return false;

            if (info.ConsumeOnDrink)
                FluidManager.main.SetLiquid(pos.x, pos.y, 0);

            var amount = 200f;
            if (info.OnDrinkOverride != null) info.OnDrinkOverride(amount, body);
            else liquidType.onDrink(amount, body);

            Sound.Play("drink", body.transform.position);
            QueueWorldBroadcast();
            return true;
        }

        public static void ApplyBodyTouch(Body body, float deltaTime)
        {
            if (body == null || WorldGeneration.world == null || FluidManager.main == null) return;
            if (body.limbs == null || body.limbs.Length == 0 || body.limbs[0] == null) return;

            var pos = WorldGeneration.world.WorldToBlockPos(body.limbs[0].transform.position);
            var worldByte = FluidManager.main.GetLiquid(pos.x, pos.y);
            var touchedCustom = TryGetTileInfo(worldByte, out var info);

            if (!touchedCustom)
            {
                if (BodyTouchStates.TryGetValue(body.GetInstanceID(), out var oldState))
                {
                    if (TryGetTileInfo(oldState.WorldByte, out var oldInfo) && oldInfo?.OnExit != null)
                    {
                        oldInfo.OnExit(body, new LiquidTileTouchContext
                        {
                            BlockPosition = oldState.BlockPosition,
                            WorldPosition = WorldGeneration.world.BlockToWorldPos(oldState.BlockPosition),
                            WorldByte = oldState.WorldByte,
                            Exited = true
                        });
                    }

                    BodyTouchStates.Remove(body.GetInstanceID());
                }

                return;
            }

            var entered = !BodyTouchStates.TryGetValue(body.GetInstanceID(), out var state) ||
                          state.WorldByte != worldByte || state.BlockPosition != pos;

            var context = new LiquidTileTouchContext
            {
                BlockPosition = pos,
                WorldPosition = WorldGeneration.world.BlockToWorldPos(pos),
                WorldByte = worldByte,
                DeltaTime = Mathf.Max(0f, deltaTime),
                Entered = entered,
                InWater = true
            };

            if (entered && info.OnEnter != null) info.OnEnter(body, context);
            ApplyBuiltInTouch(body, info, context.DeltaTime);
            info.OnTouch?.Invoke(body, context);

            BodyTouchStates[body.GetInstanceID()] = new LiquidTileTouchState
            {
                WorldByte = worldByte,
                BlockPosition = pos
            };

            if (info.PushBodies)
                body.inWater = true;
        }

        public static void EnsureVisualCapacity(FluidManager manager)
        {
            if (manager == null) return;

            EnsureLiquidColorsCapacity(manager);
            var particles = LiquidParticlesField?.GetValue(manager) as List<ParticleSystem>;
            if (particles == null) return;

            var maxByte = GetMaxAssignedWorldByte();
            while (manager.LiquidParticlePrefabs.Count <= maxByte)
            {
                var nextWorldByte = (byte)manager.LiquidParticlePrefabs.Count;
                var prefab = manager.LiquidParticlePrefabs[GetVisualPrefabIndex(manager, nextWorldByte)];
                manager.LiquidParticlePrefabs.Add(prefab);

                var clone = Object.Instantiate(prefab, manager.transform);
                particles.Add(clone.GetComponent<ParticleSystem>());
                ApplyVisualOverride(clone, (byte)manager.LiquidParticlePrefabs.Count);
            }
        }

        public static bool RenderFluids(FluidManager manager)
        {
            if (manager == null) return false;
            EnsureVisualCapacity(manager);

            var particles = LiquidParticlesField?.GetValue(manager) as List<ParticleSystem>;
            if (particles == null || particles.Count == 0) return false;

            var range = manager.SimulationRange();
            var byType = new List<ParticleSystem.Particle>[particles.Count];
            for (var i = 0; i < byType.Length; i++)
                byType[i] = new List<ParticleSystem.Particle>();

            for (var x = range.Item1.min; x < range.Item1.max; x++)
            {
                for (var y = range.Item2.min; y < range.Item2.max; y++)
                {
                    var worldByte = manager.GetLiquid(x, y);
                    if (worldByte == 0) continue;

                    var index = worldByte - 1;
                    if (index < 0 || index >= byType.Length) continue;

                    var openTop = manager.GetLiquid(x, y + 1) == 0 &&
                                  (manager.GetLiquid(x + 1, y) == 0 || manager.GetLiquid(x - 1, y) == 0);
                    var color = TryGetDisplayColor(worldByte, out var customColor) ? customColor : Color.white;
                    byType[index].Add(new ParticleSystem.Particle
                    {
                        position = WorldGeneration.world.BlockToWorldPos(new Vector2Int(x, y)) +
                                   (openTop ? new Vector2(0f, -0.3125f) : Vector2.zero),
                        startLifetime = 999f,
                        remainingLifetime = 999f,
                        startColor = color,
                        startSize3D = new Vector2(1.25f, openTop ? 0.625f : 1.25f)
                    });
                }
            }

            for (var i = 0; i < particles.Count; i++)
                particles[i].SetParticles(byType[i].ToArray());

            return true;
        }

        public static JObject CaptureNetworkSnapshot()
        {
            return new JObject
            {
                ["definitions"] = CaptureDefinitionSnapshot(),
                ["mapping"] = CaptureMappingSnapshot(),
                ["world"] = CaptureWorldStateSnapshot()
            };
        }

        public static void ApplyNetworkSnapshot(JObject snapshot)
        {
            if (snapshot == null) return;
            ApplyDefinitionSnapshot(snapshot["definitions"] as JObject);
            ApplyMappingSnapshotInternal(snapshot["mapping"] as JObject);
            ApplyWorldStateSnapshot(snapshot["world"] as JObject);
        }

        public static void RegisterBuiltIns()
        {
            SaveRegistry.RegisterWorldProvider("cucorelib.liquidTiles", new BuiltInLiquidTileSaveProvider());
        }

        internal static JObject CaptureDefinitionSnapshot()
        {
            var root = new JObject();
            foreach (var entry in RegisteredTiles)
            {
                var info = entry.Value;
                if (info == null) continue;

                root[entry.Key] = new JObject
                {
                    ["liquidId"] = info.LiquidId ?? string.Empty,
                    ["fillLiquidId"] = info.FillLiquidId ?? string.Empty,
                    ["buoyancy"] = info.Buoyancy,
                    ["drag"] = info.Drag,
                    ["pushBodies"] = info.PushBodies,
                    ["wetnessPerSecond"] = info.WetnessPerSecond,
                    ["temperaturePerSecond"] = info.TemperaturePerSecond,
                    ["sicknessPerSecond"] = info.SicknessPerSecond,
                    ["dirtynessPerSecond"] = info.DirtynessPerSecond,
                    ["disinfectPerSecond"] = info.DisinfectPerSecond,
                    ["slipPerSecond"] = info.SlipPerSecond,
                    ["ragdollBarDrainPerSecond"] = info.RagdollBarDrainPerSecond,
                    ["visualMode"] = (int)info.VisualMode,
                    ["existingVisualLiquidByte"] = info.ExistingVisualLiquidByte,
                    ["tint"] = NetworkSnapshotSerialization.WriteColor(info.Tint),
                    ["spawnAmount"] = info.SpawnAmount,
                    ["spawnLayers"] = info.SpawnLayers,
                    ["maxFloodFill"] = info.MaxFloodFill,
                    ["consumeOnDrink"] = info.ConsumeOnDrink,
                    ["consumeOnFill"] = info.ConsumeOnFill,
                    ["visualSprite"] = NetworkSnapshotSerialization.WriteSprite(info.VisualSprite)
                };
            }

            return root;
        }

        internal static JObject CaptureMappingSnapshot()
        {
            var root = new JObject();
            foreach (var entry in TileIdToWorldByte)
                root[entry.Key] = entry.Value;
            return root;
        }

        internal static JObject CaptureWorldStateSnapshot()
        {
            var root = new JObject();
            if (FluidManager.main == null || WorldGeneration.world == null || FluidManager.main.fluid == null)
                return root;

            var cells = new JArray();
            for (var x = 0; x < FluidManager.main.fluid.GetLength(0); x++)
            {
                for (var y = 0; y < FluidManager.main.fluid.GetLength(1); y++)
                {
                    var worldByte = FluidManager.main.fluid[x, y];
                    if (!IsCustomWorldByte(worldByte)) continue;

                    cells.Add(new JObject
                    {
                        ["x"] = x,
                        ["y"] = y,
                        ["b"] = worldByte
                    });
                }
            }

            root["cells"] = cells;
            return root;
        }

        internal static Dictionary<string, CustomLiquidTileInfo> CaptureOwnerEntries(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return new Dictionary<string, CustomLiquidTileInfo>(StringComparer.OrdinalIgnoreCase);

            return TileOwners.GetKeys(ownerId)
                .Where(id => RegisteredTiles.TryGetValue(id, out _))
                .ToDictionary(id => id, id => RegisteredTiles[id], StringComparer.OrdinalIgnoreCase);
        }

        internal static void RestoreOwnerEntries(string ownerId, IDictionary<string, CustomLiquidTileInfo> entries)
        {
            if (entries == null || entries.Count == 0) return;
            foreach (var entry in entries) Register(entry.Key, entry.Value);
        }

        internal static void ClearOwnerEntries(string ownerId, ContentReloadResult result)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return;

            var normalizedOwnerId = ownerId.Trim();
            var ids = TileOwners.GetKeys(normalizedOwnerId);

            foreach (var id in ids)
            {
                if (TileIdToWorldByte.TryGetValue(id, out var worldByte))
                {
                    TileIdToWorldByte.Remove(id);
                    WorldByteToTileId.Remove(worldByte);
                    FluidManager.WorldFluidToLiquidID.Remove(worldByte);
                }

                RegisteredTiles.Remove(id);
                TileOwners.Remove(id);
            }

            if (ids.Length > 0)
                result?.AddInfo("Cleared " + ids.Length + " liquid tile registrations owned by '" + normalizedOwnerId + "'.");
        }

        private static void ApplyDefinitionSnapshot(JObject snapshot)
        {
            if (snapshot == null) return;

            foreach (var property in snapshot.Properties())
            {
                if (!(property.Value is JObject obj)) continue;

                Register(property.Name, new CustomLiquidTileInfo
                {
                    LiquidId = obj.Value<string>("liquidId"),
                    FillLiquidId = obj.Value<string>("fillLiquidId"),
                    Buoyancy = obj.Value<float?>("buoyancy") ?? 0.6f,
                    Drag = obj.Value<float?>("drag") ?? 0.915f,
                    PushBodies = obj.Value<bool?>("pushBodies") ?? true,
                    WetnessPerSecond = obj.Value<float?>("wetnessPerSecond") ?? 20f,
                    TemperaturePerSecond = obj.Value<float?>("temperaturePerSecond") ?? 0f,
                    SicknessPerSecond = obj.Value<float?>("sicknessPerSecond") ?? 0f,
                    DirtynessPerSecond = obj.Value<float?>("dirtynessPerSecond") ?? 0f,
                    DisinfectPerSecond = obj.Value<float?>("disinfectPerSecond") ?? 0f,
                    SlipPerSecond = obj.Value<float?>("slipPerSecond") ?? 0f,
                    RagdollBarDrainPerSecond = obj.Value<float?>("ragdollBarDrainPerSecond") ?? 0f,
                    VisualMode = (LiquidTileVisualMode)(obj.Value<int?>("visualMode") ?? 0),
                    ExistingVisualLiquidByte = (byte)(obj.Value<int?>("existingVisualLiquidByte") ?? 1),
                    Tint = NetworkSnapshotSerialization.ReadColor(obj["tint"], Color.white),
                    SpawnAmount = obj.Value<float?>("spawnAmount") ?? 0f,
                    SpawnLayers = obj.Value<int?>("spawnLayers") ?? AllSpawnLayersMask,
                    MaxFloodFill = obj.Value<int?>("maxFloodFill") ?? 128,
                    ConsumeOnDrink = obj.Value<bool?>("consumeOnDrink") ?? true,
                    ConsumeOnFill = obj.Value<bool?>("consumeOnFill") ?? true,
                    VisualSprite = NetworkSnapshotSerialization.ReadSprite(obj["visualSprite"])
                });
            }
        }

        private static void ApplyWorldStateSnapshot(JObject snapshot)
        {
            if (snapshot == null || FluidManager.main == null || WorldGeneration.world == null || FluidManager.main.fluid == null)
                return;

            ClearCustomWorldBytes();

            if (!(snapshot["cells"] is JArray cells)) return;
            foreach (var token in cells.OfType<JObject>())
            {
                var x = token.Value<int?>("x") ?? -1;
                var y = token.Value<int?>("y") ?? -1;
                var worldByte = (byte)(token.Value<int?>("b") ?? 0);
                if (!IsCustomWorldByte(worldByte)) continue;
                if (x < 0 || y < 0 || x >= FluidManager.main.fluid.GetLength(0) ||
                    y >= FluidManager.main.fluid.GetLength(1)) continue;

                FluidManager.main.fluid[x, y] = worldByte;
            }
        }

        private static bool CanSpawnInLayer(CustomLiquidTileInfo definition, int biomeDepth)
        {
            if (definition == null) return false;

            var spawnLayers = definition.SpawnLayers;
            if (spawnLayers == 0) return false;
            if (spawnLayers == AllSpawnLayersMask) return true;

            var layerMask = SpawnLayerMask.FromLayerNumber(biomeDepth + 1);
            return layerMask != 0 && (spawnLayers & layerMask) != 0;
        }

        private static void ApplyBuiltInTouch(Body body, CustomLiquidTileInfo info, float deltaTime)
        {
            if (body == null || info == null) return;

            var dt = Mathf.Max(0f, deltaTime);
            body.wetness = Mathf.Clamp(body.wetness + info.WetnessPerSecond * dt, 0f, 100f);
            body.temperature += info.TemperaturePerSecond * dt;
            body.sicknessAmount += info.SicknessPerSecond * dt;
            body.dirtyness += info.DirtynessPerSecond * dt;
            body.liquidSlipTime = Mathf.Clamp01(body.liquidSlipTime + info.SlipPerSecond * dt);
            body.liquidRagdollBar = Mathf.Clamp01(body.liquidRagdollBar - info.RagdollBarDrainPerSecond * dt);
            if (info.DisinfectPerSecond != 0f && body.limbs != null)
            {
                foreach (var limb in body.limbs)
                    if (limb != null)
                        limb.SetDisinfect(Mathf.Max(limb.disinfectionTime, info.DisinfectPerSecond * dt));
            }
        }

        private static void QueueWorldBroadcast()
        {
            if (MultiplayerBridge.IsAvailable && MultiplayerBridge.IsServer)
                MultiplayerSyncRegistry.QueueHostSnapshotBroadcast();
        }

        private static void EnsureWorldByteAssigned(string id)
        {
            if (TileIdToWorldByte.ContainsKey(id)) return;

            for (var candidate = FirstCustomWorldByte; candidate < byte.MaxValue; candidate++)
            {
                if (WorldByteToTileId.ContainsKey(candidate)) continue;

                TileIdToWorldByte[id] = candidate;
                WorldByteToTileId[candidate] = id;
                return;
            }

            throw new InvalidOperationException("CUCoreLib ran out of available custom liquid bytes.");
        }

        private static void RegisterVanillaMappings()
        {
            WorldByteToTileId[1] = "water";
            WorldByteToTileId[2] = "groundwater";
            WorldByteToTileId[3] = "lumalgae";
            WorldByteToTileId[4] = "oil";
            WorldByteToTileId[5] = "sap";
            WorldByteToTileId[6] = "dirtywater";
            FluidManager.WorldFluidToLiquidID[0] = "water";
            FluidManager.WorldFluidToLiquidID[1] = "groundwater";
            FluidManager.WorldFluidToLiquidID[2] = "lumalgae";
            FluidManager.WorldFluidToLiquidID[3] = "oil";
            FluidManager.WorldFluidToLiquidID[4] = "sap";
            FluidManager.WorldFluidToLiquidID[5] = "dirtywater";
        }

        private static void EnsureFluidMappings()
        {
            foreach (var entry in TileIdToWorldByte)
            {
                if (!RegisteredTiles.TryGetValue(entry.Key, out var info) || info == null) continue;
                FluidManager.WorldFluidToLiquidID[entry.Value] = info.FillLiquidId;
            }
        }

        private static void EnsureLiquidColorsCapacity(FluidManager manager)
        {
            if (manager == null) return;

            var maxAssigned = GetMaxAssignedWorldByte();
            if (maxAssigned <= 6) return;

            if (manager.liquidColors != null && manager.liquidColors.Length <= maxAssigned)
                Array.Resize(ref manager.liquidColors, maxAssigned + 1);

            var particles = LiquidParticlesField?.GetValue(manager) as List<ParticleSystem>;
            if (particles == null) return;

            while (manager.LiquidParticlePrefabs.Count <= maxAssigned)
            {
                var nextWorldByte = (byte)manager.LiquidParticlePrefabs.Count;
                var prefab = manager.LiquidParticlePrefabs[GetVisualPrefabIndex(manager, nextWorldByte)];
                manager.LiquidParticlePrefabs.Add(prefab);

                var clone = Object.Instantiate(prefab, manager.transform);
                particles.Add(clone.GetComponent<ParticleSystem>());
                ApplyVisualOverride(clone, (byte)manager.LiquidParticlePrefabs.Count);
            }
        }

        private static int GetVisualPrefabIndex(FluidManager manager, byte worldByte)
        {
            if (manager == null || manager.LiquidParticlePrefabs == null || manager.LiquidParticlePrefabs.Count == 0)
                return 0;

            if (!TryGetTileInfo(worldByte, out var info) || info == null)
                return 0;

            var requestedByte = info.ExistingVisualLiquidByte <= 0 ? (byte)1 : info.ExistingVisualLiquidByte;
            if (requestedByte >= FirstCustomWorldByte && TryGetTileInfo(requestedByte, out var chainedInfo) &&
                chainedInfo != null)
                requestedByte = chainedInfo.ExistingVisualLiquidByte <= 0
                    ? (byte)1
                    : chainedInfo.ExistingVisualLiquidByte;

            var prefabIndex = Mathf.Clamp(requestedByte - 1, 0, manager.LiquidParticlePrefabs.Count - 1);
            return prefabIndex;
        }

        private static void ApplyVisualOverride(GameObject instance, byte worldByte)
        {
            if (instance == null || !TryGetTileInfo(worldByte, out var info)) return;

            var renderer = instance.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            switch (info.VisualMode)
            {
                case LiquidTileVisualMode.Material:
                    if (info.VisualMaterial != null)
                        renderer.sharedMaterial = new Material(info.VisualMaterial);
                    break;
                case LiquidTileVisualMode.Sprite:
                    if (info.VisualSprite != null && renderer.sharedMaterial != null)
                    {
                        var spriteMaterial = new Material(renderer.sharedMaterial)
                        {
                            mainTexture = info.VisualSprite.texture
                        };
                        renderer.sharedMaterial = spriteMaterial;
                    }

                    break;
                case LiquidTileVisualMode.HighResImageGenerated:
                    if (info.HighResImage != null && renderer.sharedMaterial != null)
                    {
                        var highResMaterial = new Material(renderer.sharedMaterial)
                        {
                            mainTexture = info.HighResImage
                        };
                        renderer.sharedMaterial = highResMaterial;
                    }

                    break;
            }
        }

        private static Color ResolveDisplayColor(byte worldByte, CustomLiquidTileInfo info)
        {
            var baseColor = Color.white;
            if (!string.IsNullOrWhiteSpace(info.LiquidId) &&
                Liquids.Registry.TryGetValue(info.LiquidId, out var liquid))
                baseColor = liquid.color;

            return new Color(
                Mathf.Clamp01(baseColor.r * info.Tint.r),
                Mathf.Clamp01(baseColor.g * info.Tint.g),
                Mathf.Clamp01(baseColor.b * info.Tint.b),
                Mathf.Clamp01(baseColor.a * info.Tint.a));
        }

        private static bool TryGetTileInfo(byte worldByte, out CustomLiquidTileInfo info)
        {
            info = null;
            return WorldByteToTileId.TryGetValue(worldByte, out var id) && RegisteredTiles.TryGetValue(id, out info);
        }

        private static bool ResolveDrinkLiquidType(CustomLiquidTileInfo info, out LiquidType liquidType)
        {
            liquidType = null;
            return info != null && !string.IsNullOrWhiteSpace(info.LiquidId) &&
                   Liquids.Registry.TryGetValue(info.LiquidId, out liquidType);
        }

        private static bool LiquidExists(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   (LiquidRegistry.TryGetCustomInfo(id, out _) || Liquids.Registry.ContainsKey(id));
        }

        private static int GetMaxAssignedWorldByte()
        {
            return WorldByteToTileId.Count == 0 ? 0 : WorldByteToTileId.Keys.Max(key => (int)key);
        }

        private static void ApplyMappingSnapshotInternal(JObject snapshot)
        {
            if (snapshot == null) return;

            foreach (var property in snapshot.Properties())
            {
                if (string.IsNullOrWhiteSpace(property.Name)) continue;
                if (!RegisteredTiles.ContainsKey(property.Name)) continue;

                var worldByte = (byte)(property.Value.Value<int?>() ?? 0);
                if (worldByte < FirstCustomWorldByte) continue;

                TileIdToWorldByte[property.Name] = worldByte;
                WorldByteToTileId[worldByte] = property.Name;
            }

            EnsureFluidMappings();
        }

        public static bool IsCustomWorldByte(byte worldByte)
        {
            return worldByte >= FirstCustomWorldByte && WorldByteToTileId.ContainsKey(worldByte);
        }

        private static void ClearCustomWorldBytes()
        {
            var fluid = FluidManager.main != null ? FluidManager.main.fluid : null;
            if (fluid == null) return;

            for (var x = 0; x < fluid.GetLength(0); x++)
            for (var y = 0; y < fluid.GetLength(1); y++)
                if (IsCustomWorldByte(fluid[x, y]))
                    fluid[x, y] = 0;
        }

        private static void WarnUnknownLogicalLiquid(string tileId, string liquidId, string field)
        {
            var key = tileId + "|" + field + "|" + liquidId;
            if (!WarnedUnknownLiquidIds.Add(key)) return;

            CUCoreLibPlugin.Log?.LogWarning("Liquid tile '" + tileId + "' references unknown liquid '" + liquidId +
                                            "' in " + field + ".");
        }

        private static void LogSummary()
        {
            if (_summaryLogged || RegisteredTiles.Count == 0) return;

            _summaryLogged = true;
            CUCoreLibPlugin.Log?.LogInfo("Added " + RegisteredTiles.Count + " liquid tiles.");
        }

        public static bool IsRegistered(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && RegisteredTiles.ContainsKey(id.Trim());
        }

        private sealed class LiquidTileTouchState
        {
            public byte WorldByte;
            public Vector2Int BlockPosition;
        }
    }
}