using System;
using System.Collections.Generic;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Networking
{
    public static class MultiplayerSyncRegistry
    {
        internal const string RequestKind = "request";
        internal const string ResponseKind = "response";
        internal const string EventKind = "event";

        private const string SnapshotChannel = "cucorelib.sync.snapshot";
        private const string SnapshotModuleKey = "modules";

        private static readonly Dictionary<string, Func<JObject>> CaptureModules =
            new Dictionary<string, Func<JObject>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Action<JObject>> ApplyModules =
            new Dictionary<string, Action<JObject>>(StringComparer.Ordinal);

        private static bool _builtInsRegistered;
        private static bool _initialSnapshotRequested;
        private static bool _initialSnapshotScheduled;
        private static JObject _cachedSnapshot;
        private static bool _retryScheduled;
        private static bool _hostSnapshotBroadcastQueued;

        public static void RegisterModule(string key, Func<JObject> capture, Action<JObject> apply = null)
        {
            if (string.IsNullOrWhiteSpace(key) || capture == null) return;

            key = key.Trim();
            CaptureModules[key] = capture;
            if (apply != null) ApplyModules[key] = apply;
        }

        public static JObject CaptureSnapshot()
        {
            var root = new JObject
            {
                ["version"] = 1,
                ["generatedAt"] = DateTime.UtcNow.ToString("O")
            };

            var modules = new JObject();
            foreach (var entry in CaptureModules)
                try
                {
                    modules[entry.Key] = entry.Value?.Invoke() ?? new JObject();
                }
                catch (Exception ex)
                {
                    CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayer snapshot capture failed for module '" +
                                                    entry.Key + "'.\n" + ex);
                }

            root[SnapshotModuleKey] = modules;
            return root;
        }

        public static void ApplySnapshot(JObject snapshot)
        {
            if (snapshot == null) return;

            _cachedSnapshot = snapshot;
            ApplySnapshotInternal(snapshot);
            ScheduleReplayIfNeeded();
        }

        private static void ApplySnapshotInternal(JObject snapshot)
        {
            if (snapshot == null) return;

            var modules = snapshot[SnapshotModuleKey] as JObject ?? snapshot;
            if (modules == null) return;

            foreach (var property in modules.Properties())
            {
                if (!ApplyModules.TryGetValue(property.Name, out var apply)) continue;

                try
                {
                    apply(property.Value as JObject);
                }
                catch (Exception ex)
                {
                    CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayer snapshot apply failed for module '" +
                                                    property.Name + "'.\n" + ex);
                }
            }
        }

        private static void ScheduleReplayIfNeeded()
        {
            if (_retryScheduled || _cachedSnapshot == null) return;

            _retryScheduled = true;
            CUCoreUtils.CallWhen(
                () => MultiplayerBridge.IsAvailable && CUCoreUtils.IsInWorld(),
                ReplayCachedSnapshot,
                1f);
        }

        private static void ReplayCachedSnapshot()
        {
            _retryScheduled = false;
            if (_cachedSnapshot == null) return;

            ApplySnapshotInternal(_cachedSnapshot);
            if (!CUCoreUtils.IsInWorld())
            {
                ScheduleReplayIfNeeded();
                return;
            }

            _cachedSnapshot = null;
        }

        public static void RegisterBuiltIns()
        {
            if (_builtInsRegistered) return;

            _builtInsRegistered = true;

            RegisterModule("items", CaptureItemManifest, ItemRegistry.ApplyNetworkSnapshot);
            RegisterModule("tiles", CaptureTileManifest, TileRegistry.ApplyNetworkSnapshot);
            RegisterModule("buildings", CaptureBuildingManifest, BuildingEntityRegistry.ApplyNetworkSnapshot);
            RegisterModule("liquids", CaptureLiquidManifest, LiquidRegistry.ApplyNetworkSnapshot);
            RegisterModule("statuses", StatusRegistry.CaptureNetworkSnapshot, StatusRegistry.ApplyNetworkSnapshot);
            RegisterModule("moodles", MoodleRegistry.CaptureNetworkSnapshot, MoodleRegistry.ApplyNetworkSnapshot);
            RegisterModule("settings", ModOptionsRegistry.CaptureNetworkSnapshot,
                ModOptionsRegistry.ApplyNetworkSnapshot);
            RegisterModule("save", SaveCoordinator.CaptureNetworkSnapshot, SaveCoordinator.ApplyNetworkSnapshot);

            MultiplayerBridge.RegisterServerHandler(SnapshotChannel, _ => CaptureSnapshot());
            MultiplayerBridge.RegisterClientHandler(SnapshotChannel, payload =>
            {
                if (payload is JObject snapshotObject) ApplySnapshot(snapshotObject);
            });
        }

        public static void ScheduleInitialSnapshot()
        {
            if (_initialSnapshotScheduled) return;

            _initialSnapshotScheduled = true;
            CUCoreUtils.CallWhen(
                () => MultiplayerBridge.IsAvailable && MultiplayerBridge.IsClient,
                RequestInitialSnapshot,
                1f);
        }

        public static void RequestInitialSnapshot()
        {
            if (_initialSnapshotRequested || !MultiplayerBridge.IsAvailable || !MultiplayerBridge.IsClient) return;

            _initialSnapshotRequested = true;
            MultiplayerBridge.RequestServer(
                SnapshotChannel,
                null,
                snapshot =>
                {
                    if (snapshot is JObject snapshotObject) ApplySnapshot(snapshotObject);
                });
        }

        public static bool BroadcastSnapshot(bool includeHost = false)
        {
            if (!MultiplayerBridge.IsAvailable || !MultiplayerBridge.IsServer) return false;

            return MultiplayerBridge.Broadcast(
                SnapshotChannel,
                CaptureSnapshot(),
                includeHost);
        }

        public static void QueueHostSnapshotBroadcast()
        {
            if (_hostSnapshotBroadcastQueued) return;

            _hostSnapshotBroadcastQueued = true;
            CUCoreUtils.CallWhen(
                () => MultiplayerBridge.IsAvailable && MultiplayerBridge.IsServer,
                () =>
                {
                    _hostSnapshotBroadcastQueued = false;
                    BroadcastSnapshot();
                },
                1f);
        }

        private static JObject CaptureItemManifest()
        {
            return ItemRegistry.CaptureNetworkSnapshot();
        }

        private static JObject CaptureTileManifest()
        {
            var root = new JObject();
            foreach (var index in TileRegistry.GetRegisteredIndices())
            {
                if (!TileRegistry.TryGetDefinition(index, out var definition)) continue;

                var tile = new JObject
                {
                    ["index"] = index,
                    ["id"] = definition.ID ?? string.Empty,
                    ["name"] = definition.Name ?? string.Empty,
                    ["description"] = definition.Description ?? string.Empty,
                    ["health"] = definition.Health,
                    ["hitSound"] = definition.HitSound ?? string.Empty,
                    ["stepSound"] = definition.StepSound ?? string.Empty,
                    ["metallic"] = definition.Metallic,
                    ["toxicity"] = definition.Toxicity,
                    ["slippery"] = definition.Slippery
                };

                root[index.ToString()] = tile;
            }

            return root;
        }

        private static JObject CaptureBuildingManifest()
        {
            var root = new JObject();
            var buildings = new JArray();

            foreach (var entry in BuildingEntityRegistry.GetRegisteredDefinitions())
            {
                var definition = entry.Value;
                if (definition == null) continue;

                var building = new JObject
                {
                    ["id"] = entry.Key,
                    ["name"] = definition.Name ?? string.Empty,
                    ["description"] = definition.Description ?? string.Empty,
                    ["health"] = definition.Health,
                    ["placement"] = definition.Placement.ToString(),
                    ["generationStyle"] = definition.GenerationStyle.ToString(),
                    ["dropChanceMultiplier"] = definition.DropChanceMultiplier,
                    ["surfaceOffset"] = definition.SurfaceOffset,
                    ["spawnMinPerChunk"] = definition.SpawnMinPerChunk,
                    ["spawnMaxPerChunk"] = definition.SpawnMaxPerChunk
                };

                root[entry.Key] = building;
            }

            return root;
        }

        private static JObject CaptureLiquidManifest()
        {
            var root = new JObject();
            var liquids = new JArray();

            foreach (var id in LiquidRegistry.GetRegisteredLiquidIds())
            {
                if (!LiquidRegistry.TryGetCustomInfo(id, out var info)) continue;

                var liquid = new JObject
                {
                    ["id"] = id,
                    ["name"] = info.name ?? string.Empty,
                    ["description"] = info.description ?? string.Empty,
                    ["valuePerLiter"] = info.valuePerLiter,
                    ["healthUsable"] = info.healthUsable,
                    ["injectable"] = info.injectable,
                    ["injectionSickness"] = info.injectionSickness,
                    ["localeFromItem"] = info.localeFromItem
                };

                root[id] = liquid;
            }

            return root;
        }
    }
}