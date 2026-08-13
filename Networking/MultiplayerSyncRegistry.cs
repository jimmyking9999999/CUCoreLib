using System;
using System.Collections;
using System.Collections.Generic;
using CUCoreLib.ContentReload;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Networking
{
    public static class MultiplayerSyncRegistry
    {
        internal const string RequestKind = "request";
        internal const string ResponseKind = "response";
        internal const string EventKind = "event";

        private const string SnapshotChannel = "cucorelib.sync.snapshot";
        private const string PlayerStatusSnapshotChannel = "cucorelib.sync.statuses.player";
        private const string SnapshotModuleKey = "modules";
        private const float PlayerStatusSyncSeconds = 1f;

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
        private static bool _playerStatusSyncScheduled;

        public static void RegisterModule(string key, Func<JObject> capture, Action<JObject> apply = null)
        {
            ContentReloadSession.AssertNotActive("MultiplayerSyncRegistry.RegisterModule()",
                "Multiplayer registration is excluded from strict content reload.");

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
            ContentReloadSession.AssertNotActive("MultiplayerSyncRegistry.RegisterBuiltIns()",
                "Multiplayer registration is excluded from strict content reload.");

            if (_builtInsRegistered) return;

            _builtInsRegistered = true;

            RegisterModule("liquids", CaptureLiquidManifest, LiquidRegistry.ApplyNetworkSnapshot);
            RegisterModule("items", CaptureItemManifest, ItemRegistry.ApplyNetworkSnapshot);
            RegisterModule("tiles", TileRegistry.CaptureNetworkSnapshot, TileRegistry.ApplyNetworkSnapshot);
            RegisterModule("buildings", CaptureBuildingManifest, BuildingEntityRegistry.ApplyNetworkSnapshot);
            RegisterModule("liquidtiles", LiquidTileRegistry.CaptureNetworkSnapshot,
                LiquidTileRegistry.ApplyNetworkSnapshot);
            RegisterModule("moodles", MoodleRegistry.CaptureNetworkSnapshot, MoodleRegistry.ApplyNetworkSnapshot);
            RegisterModule("settings", ModOptionsRegistry.CaptureNetworkSnapshot,
                ModOptionsRegistry.ApplyNetworkSnapshot);

            MultiplayerBridge.RegisterServerHandler(SnapshotChannel, _ => CaptureSnapshot());
            MultiplayerBridge.RegisterServerHandler(PlayerStatusSnapshotChannel, (senderClientId, _) =>
            {
                return MultiplayerApi.TryGetBodyFromClientId(senderClientId, out var body)
                    ? StatusRegistry.CaptureBodyNetworkSnapshot(body)
                    : new JObject();
            });
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
            SchedulePlayerStatusSync();
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

        private static void SchedulePlayerStatusSync()
        {
            if (_playerStatusSyncScheduled) return;

            _playerStatusSyncScheduled = true;
            CUCoreUtils.CallWhen(
                () => MultiplayerBridge.IsAvailable && MultiplayerBridge.IsClient && CUCoreUtils.IsInWorld(),
                () => CUCoreUtils.StartCoroutine(SyncLocalPlayerStatuses()),
                1f);
        }

        private static IEnumerator SyncLocalPlayerStatuses()
        {
            // KrokMP has no status-changed event, so each client refreshes only its own authoritative body.
            while (MultiplayerBridge.IsAvailable && MultiplayerBridge.IsClient && CUCoreUtils.IsInWorld())
            {
                MultiplayerBridge.RequestServer(PlayerStatusSnapshotChannel, null, payload =>
                {
                    if (payload is JObject snapshot) StatusRegistry.ApplyNetworkSnapshot(snapshot);
                });
                yield return new WaitForSeconds(PlayerStatusSyncSeconds);
            }

            _playerStatusSyncScheduled = false;
            SchedulePlayerStatusSync();
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
