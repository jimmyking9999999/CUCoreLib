using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.ContentReload;
using CUCoreLib.Helpers;
using CUCoreLib.Patches;
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
        internal const string PlayerStatusSnapshotChannel = "cucorelib.sync.statuses.player";
        internal const string PlayerStatusSetFieldChannel = "cucorelib.sync.statuses.setfield";
        private const string SnapshotModuleKey = "modules";

        private static readonly Dictionary<string, Func<JObject>> CaptureModules =
            new Dictionary<string, Func<JObject>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Action<JObject>> ApplyModules =
            new Dictionary<string, Action<JObject>>(StringComparer.Ordinal);

        private static bool _builtInsRegistered;
        private static bool _initialSnapshotRequested;
        private static bool _initialSnapshotScheduled;
        private static bool _initialSnapshotRetryRunning;
        private static JObject _cachedSnapshot;
        private static bool _retryScheduled;
        private static bool _hostSnapshotBroadcastQueued;

        // Set once a snapshot has been applied for the current KrokMP transport session, to prevent redundancy
        private static bool _snapshotAppliedSinceTransport;

        [ThreadStatic] private static string _snapshotLanguage;

        public static void RegisterModule(string key, Func<JObject> capture, Action<JObject> apply = null)
        {
            ContentReloadSession.AssertNotActive("MultiplayerSyncRegistry.RegisterModule()",
                "Multiplayer registration is excluded from strict content reload.");

            if (string.IsNullOrWhiteSpace(key) || capture == null) return;

            key = key.Trim();
            CaptureModules[key] = capture;
            if (apply != null) ApplyModules[key] = apply;
        }

        public static JObject CaptureSnapshot(string targetLanguage = null)
        {
            var root = new JObject
            {
                ["version"] = 1,
                ["generatedAt"] = DateTime.UtcNow.ToString("O")
            };

            var modules = new JObject();
            _snapshotLanguage = targetLanguage;
            try
            {
                using (NetworkSnapshotSerialization.BeginSpriteDedupeScope())
                {
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
                }
            }
            finally
            {
                _snapshotLanguage = null;
            }

            root[SnapshotModuleKey] = modules;
            return root;
        }

        public static void ApplySnapshot(JObject snapshot)
        {
            if (snapshot == null) return;

            _cachedSnapshot = snapshot;
            _snapshotAppliedSinceTransport = true;
            ApplySnapshotInternal(snapshot);
            ScheduleReplayIfNeeded();
        }

        private static void ApplySnapshotInternal(JObject snapshot)
        {
            if (snapshot == null) return;

            var modules = snapshot[SnapshotModuleKey] as JObject ?? snapshot;

            foreach (var property in modules.Properties().OrderBy(property =>
                         property.Name == "locale" ? -1 : property.Name == "lootpools" ? 1 : 0))
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

            if (CUCoreUtils.IsInWorld())
                TileRegistry.InjectRegisteredTiles(WorldGeneration.world);
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
                "Multiplayer registration is excluded from reload :(");

            if (_builtInsRegistered) return;

            _builtInsRegistered = true;

            RegisterModule("locale", () => LocaleRegistry.CaptureNetworkSnapshot(_snapshotLanguage),
                LocaleRegistry.ApplyNetworkSnapshot);
            RegisterModule("liquids", CaptureLiquidManifest, LiquidRegistry.ApplyNetworkSnapshot);
            RegisterModule("items", CaptureItemManifest, ItemRegistry.ApplyNetworkSnapshot);
            RegisterModule("recipes", RecipeRegistry.CaptureNetworkSnapshot, RecipeRegistry.ApplyNetworkSnapshot);
            RegisterModule("lootpools", DropPoolRegistry.CaptureNetworkSnapshot, DropPoolRegistry.ApplyNetworkSnapshot);
            RegisterModule("tiles", TileRegistry.CaptureNetworkSnapshot, TileRegistry.ApplyNetworkSnapshot);
            RegisterModule("buildings", CaptureBuildingManifest, BuildingEntityRegistry.ApplyNetworkSnapshot);
            RegisterModule("liquidtiles", LiquidTileRegistry.CaptureNetworkSnapshot,
                LiquidTileRegistry.ApplyNetworkSnapshot);
            RegisterModule("moodles", MoodleRegistry.CaptureNetworkSnapshot, MoodleRegistry.ApplyNetworkSnapshot);
            RegisterModule("settings", ModOptionsRegistry.CaptureNetworkSnapshot,
                ModOptionsRegistry.ApplyNetworkSnapshot);

            MultiplayerBridge.RegisterServerHandler(SnapshotChannel,
                payload => CaptureSnapshot(payload?.Value<string>("lang")));
            MultiplayerPlayerStatusSync.RegisterServerHandler();
            MultiplayerLoadingProgress.Register();
            MultiplayerBridge.RegisterClientHandler(SnapshotChannel, payload =>
            {
                if (payload is JObject snapshotObject) ApplySnapshot(snapshotObject);
            });
        }

        public static void ScheduleInitialSnapshot()
        {
            if (_initialSnapshotScheduled) return;

            _initialSnapshotScheduled = true;
            StartInitialSnapshotRetry();
            MultiplayerPlayerStatusSync.Schedule();
            SpawnHandshakeRecovery.Schedule();
        }

        // Together flips IsConnected true briefly before the client transport can actually send (its CLIENT_PEER is still null, so IsConnecting() reports false)...
        private static void StartInitialSnapshotRetry()
        {
            if (_initialSnapshotRetryRunning) return;

            _initialSnapshotRetryRunning = true;
            CUCoreUtils.StartCoroutine(InitialSnapshotRetryRoutine());
        }

        private static IEnumerator InitialSnapshotRetryRoutine()
        {
            var wait = new WaitForSecondsRealtime(0.1f);
            while (!RequestInitialSnapshot())
            {
                if (!MultiplayerBridge.IsAvailable || !MultiplayerBridge.IsRunning || !MultiplayerBridge.IsClient)
                    break;

                yield return wait;
            }

            _initialSnapshotRetryRunning = false;
        }

        public static bool RequestInitialSnapshot()
        {
            if (_initialSnapshotRequested) return true;
            if (!MultiplayerBridge.IsAvailable || !MultiplayerBridge.IsRunning ||
                !MultiplayerBridge.IsClient || !MultiplayerBridge.IsConnected) return false;

            _initialSnapshotRequested = MultiplayerBridge.RequestServer(
                SnapshotChannel,
                new JObject { ["lang"] = Locale.currentLangName ?? string.Empty },
                snapshot =>
                {
                    if (snapshot is JObject snapshotObject) ApplySnapshot(snapshotObject);
                });
            return _initialSnapshotRequested;
        }

        internal static void RequestInitialSnapshotForNewSession()
        {
            if (_snapshotAppliedSinceTransport) return;

            _initialSnapshotRequested = false;
            StartInitialSnapshotRetry();
        }

        internal static void MarkNewTransportSession()
        {
            _snapshotAppliedSinceTransport = false;
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
                () => MultiplayerBridge.IsAvailable && MultiplayerBridge.IsServer && !IsHostGeneratingWorld(),
                () =>
                {
                    _hostSnapshotBroadcastQueued = false;
                    BroadcastSnapshot();
                },
                1f);
        }

        private static bool IsHostGeneratingWorld()
        {
            var world = WorldGeneration.world;
            return world != null && world.generatingWorld;
        }

        private static JObject CaptureItemManifest()
        {
            return ItemRegistry.CaptureNetworkSnapshot();
        }

        private static JObject CaptureBuildingManifest()
        {
            return BuildingEntityRegistry.CaptureNetworkSnapshot();
        }

        private static JObject CaptureLiquidManifest()
        {
            var root = new JObject();
            var liquids = new JArray();

            // This peer is the ordering source: publish the exact registry order KrokMP's byte IDs
            // are derived from, so clients rebuild the same indexes instead of their own load order.
            KrokMpCompatibilityPatches.SetCanonicalLiquidOrder(null);
            if (Liquids.Registry != null)
                root["__registryOrder"] = new JArray(Liquids.Registry.Keys.ToArray());

            foreach (var id in LiquidRegistry.GetRegisteredLiquidIds())
            {
                if (!LiquidRegistry.TryGetCustomInfo(id, out var info)) continue;

                var liquid = new JObject
                {
                    ["id"] = id,
                    ["name"] = info.name ?? string.Empty,
                    ["description"] = info.description ?? string.Empty,
                    ["color"] = NetworkSnapshotSerialization.WriteColor(info.color),
                    ["valuePerLiter"] = info.valuePerLiter,
                    ["healthUsable"] = info.healthUsable,
                    ["injectable"] = info.injectable,
                    ["injectionSickness"] = info.injectionSickness,
                    ["localeFromItem"] = info.localeFromItem
                    , ["unobtainable"] = info.unobtainable
                    , ["qualities"] = NetworkSnapshotSerialization.WriteCraftingQualities(info.qualities)
                };

                root[id] = liquid;
            }

            return root;
        }
    }
}
