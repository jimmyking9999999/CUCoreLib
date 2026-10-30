using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    // The root of all evil...
    internal static class KrokMpCompatibilityPatches
    {
        private const string KrokMpPluginGuid = "CasualtiesMP";
        private const string WorldChunkSyncTypeName = "Together.WorldChunkSync";
        private const string NetObjectRegistryTypeName = "Together.NetObjectRegistry";
        private const string SyncInfoTypeName = "Together.SyncInfo";

        private const string ItemSetupListenerTypeName =
            "Together.Item_SetupItems_Listener";

        private const string CraftCommitTypeName = "Together.CraftCommit";
        private static bool _installed;
        private static bool _retryScheduled;
        private static bool _chunkRetryScheduled;
        private static bool _syncInfoPatched;
        private static bool _liquidRegistryPatched;
        private static bool _craftAttemptPatched;
        private static FieldInfo _craftAttemptRecipeField;
        private static List<string> _canonicalLiquidOrder;
        private static MethodInfo _serverEnsureItemNetworkRegisteredMethod;

        internal static void Install(Harmony harmony)
        {
            if (harmony != null)
            {
                var chunkType = ResolveLoadedType(WorldChunkSyncTypeName);
                KrokMpWorldChunkPatches.Install(harmony, chunkType);
                if (!KrokMpWorldChunkPatches.IsInstalled
                    && chunkType == null
                    && IsKrokMpExpected())
                    ScheduleChunkRetry(harmony);

                KrokMpHealPatches.Install(harmony);
            }

            if (harmony == null || _installed) return;

            if (!_syncInfoPatched)
            {
                var syncInfoType = ResolveLoadedType(SyncInfoTypeName);
                if (syncInfoType != null)
                {
                    var instantiateResource = AccessTools.GetDeclaredMethods(syncInfoType)
                        ?.FirstOrDefault(method =>
                            string.Equals(method.Name, "InstantiateResource",
                                StringComparison.Ordinal));
                    if (instantiateResource != null)
                    {
                        harmony.Patch(instantiateResource,
                            new HarmonyMethod(typeof(KrokMpCompatibilityPatches),
                                nameof(InstantiateResource_Prefix)));
                        _syncInfoPatched = true;
                    }
                }
            }

            if (!_liquidRegistryPatched
                && ResolveLoadedType(ItemSetupListenerTypeName) != null)
            {
                var setupItems = AccessTools.Method(typeof(Item), "SetupItems");
                if (setupItems != null)
                {
                    harmony.Patch(setupItems,
                        postfix: new HarmonyMethod(typeof(KrokMpCompatibilityPatches),
                            nameof(RefreshLiquidRegistry_AfterItemSetup))
                        {
                            priority = Priority.Last
                        });
                    _liquidRegistryPatched = true;
                }
            }

            if (!_craftAttemptPatched)
            {
                var craftCommitType = ResolveLoadedType(CraftCommitTypeName);
                var sendAttempt = craftCommitType != null
                    ? AccessTools.Method(craftCommitType, "SendAttempt")
                    : null;
                var attemptType = sendAttempt?.GetParameters().FirstOrDefault()
                    ?.ParameterType;
                // Craft by recipe index
                _craftAttemptRecipeField = attemptType != null
                    ? AccessTools.Field(attemptType, "recipe")
                    : null;
                if (_craftAttemptRecipeField != null
                    && _craftAttemptRecipeField.FieldType == typeof(ushort))
                {
                    harmony.Patch(sendAttempt,
                        new HarmonyMethod(typeof(KrokMpCompatibilityPatches),
                            nameof(SendAttempt_Prefix)),
                        new HarmonyMethod(typeof(KrokMpCompatibilityPatches),
                            nameof(SendAttempt_Postfix)));
                    _craftAttemptPatched = true;
                }
            }

            if (!_syncInfoPatched && !_liquidRegistryPatched && !_craftAttemptPatched)
            {
                ScheduleRetry(harmony);
                return;
            }

            _installed = true;
            RefreshLiquidRegistry();
            CUCoreLibPlugin.Log?.LogInfo("CUCoreLib, with friends!");
        }

        internal static void SetCanonicalLiquidOrder(List<string> order)
        {
            _canonicalLiquidOrder = order != null && order.Count > 0
                ? order
                : null;
            RefreshLiquidRegistry();
        }

        internal static void RefreshLiquidRegistry()
        {
            try
            {
                if (Liquids.Registry == null) return;

                var listenerType = ResolveLoadedType(ItemSetupListenerTypeName);
                if (listenerType == null) return;

                var idsField = AccessTools.Field(listenerType, "LiquidIdRegistry");
                var reverseIdsField = AccessTools.Field(listenerType, "LiquidNetIdToId");
                if (idsField == null || reverseIdsField == null) return;

                if (Liquids.Registry.Count > byte.MaxValue + 1) return;

                var orderedIds = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                if (_canonicalLiquidOrder != null)
                    orderedIds.AddRange(_canonicalLiquidOrder.Where(seen.Add));

                orderedIds.AddRange(Liquids.Registry.Keys.Where(seen.Add));

                var count = Math.Min(orderedIds.Count, byte.MaxValue + 1);
                var ids = new Dictionary<string, byte>(count, StringComparer.Ordinal);
                var reverseIds = new string[count];
                for (var index = 0; index < count; index++)
                {
                    // Unknown IDs keep their slot so a missing mod cannot shift every later liquid.
                    if (Liquids.Registry.ContainsKey(orderedIds[index]))
                        ids[orderedIds[index]] = (byte)index;
                    reverseIds[index] = orderedIds[index];
                }

                idsField.SetValue(null, ids);
                reverseIdsField.SetValue(null, reverseIds);
            }
            catch
            {
                // ignored
            }
        }

        internal static void EnsureItemNetworkRegistered(GameObject item)
        {
            if (item == null
                || !MultiplayerBridge.IsRunning
                || !MultiplayerBridge.IsServer) return;

            try
            {
                if (_serverEnsureItemNetworkRegisteredMethod == null)
                {
                    var registryType = ResolveLoadedType(NetObjectRegistryTypeName);
                    _serverEnsureItemNetworkRegisteredMethod = AccessTools.Method(
                        registryType,
                        "Server_EnsureItemIsNetworkRegistered",
                        new[] { typeof(GameObject) });
                }

                _serverEnsureItemNetworkRegisteredMethod?.Invoke(null,
                    new object[] { item });
            }
            catch
            {
                // ignored
            }
        }

        private static void RefreshLiquidRegistry_AfterItemSetup()
        {
            RefreshLiquidRegistry();
        }

        // Swap the local recipe index for the host's while the attempt is written. Should be okay
        private static void SendAttempt_Prefix(object[] __args, out int __state)
        {
            __state = -1;
            var attempt = __args != null && __args.Length > 0
                ? __args[0]
                : null;
            if (attempt == null) return;

            var localIndex = (ushort)_craftAttemptRecipeField.GetValue(attempt);
            if (!RecipeRegistry.TryGetHostRecipeIndex(localIndex, out var hostIndex)
                || hostIndex == localIndex) return;

            __state = localIndex;
            _craftAttemptRecipeField.SetValue(attempt, hostIndex);
        }

        private static void SendAttempt_Postfix(object[] __args, int __state)
        {
            if (__state < 0) return;
            _craftAttemptRecipeField.SetValue(__args[0], (ushort)__state);
        }

        private static bool TryResolveResourcePrefab(string resourceStringId,
            out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(resourceStringId)) return false;

            var normalized = resourceStringId.Trim();
            if (TryResolveCustomPrefab(normalized, out prefab)) return prefab != null;
            return false;
        }

        private static bool TryResolveCustomPrefab(string resourceStringId,
            out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(resourceStringId)) return false;

            var normalized = resourceStringId.Trim();
            var baseId = SpawnIdHelpers.NormalizeSpawnId(normalized);
            var isCustomId = ItemRegistry.TryGetCustomInfo(baseId, out _)
                || BuildingEntityRegistry.TryGetDefinition(baseId, out _);
            if (isCustomId)
            {
                prefab = CustomInstantiate.ResolvePrefab(baseId);
                if (prefab != null) return true;
            }

            if (!normalized.StartsWith("KMPSR_", StringComparison.Ordinal)) return false;
            var stripped = normalized.Substring("KMPSR_".Length);
            var strippedBaseId = SpawnIdHelpers.NormalizeSpawnId(stripped);
            if (!ItemRegistry.TryGetCustomInfo(strippedBaseId, out _)
                && !BuildingEntityRegistry.TryGetDefinition(strippedBaseId, out _))
                return false;
            prefab = CustomInstantiate.ResolvePrefab(strippedBaseId);
            return prefab != null;
        }

        private static bool InstantiateResource_Prefix(string resourceId, object[] __args,
            ref GameObject __result)
        {
            var pos = default(Vector2);
            if (__args != null && __args.Length > 1 && __args[1] is Vector2 vector)
                pos = vector;

            if (!TryResolveResourcePrefab(resourceId, out var prefab) || prefab == null)
                return true;

            var instance = CustomInstantiate.PrepareInstantiatedObject(
                CustomInstantiate.InstantiateInActiveScene(prefab, pos,
                    Quaternion.identity));
            if (instance == null)
            {
                __result = null;
                return false;
            }

            __result = instance;
            return false;
        }

        private static void ScheduleRetry(Harmony harmony)
        {
            if (_retryScheduled || !IsKrokMpExpected()) return;

            _retryScheduled = true;
            CUCoreUtils.DelayCall(1f, () =>
            {
                _retryScheduled = false;
                if (_installed) return;

                Install(harmony);
            });
        }

        private static void ScheduleChunkRetry(Harmony harmony)
        {
            if (_chunkRetryScheduled) return;

            _chunkRetryScheduled = true;
            CUCoreUtils.DelayCall(1f, () =>
            {
                _chunkRetryScheduled = false;
                if (!KrokMpWorldChunkPatches.IsInstalled)
                    KrokMpWorldChunkPatches.Install(harmony,
                        ResolveLoadedType(WorldChunkSyncTypeName));
                if (!KrokMpWorldChunkPatches.IsInstalled && IsKrokMpExpected())
                    ScheduleChunkRetry(harmony);
            });
        }

        private static bool IsKrokMpExpected()
        {
            return Chainloader.PluginInfos.ContainsKey(KrokMpPluginGuid);
        }

        private static Type ResolveLoadedType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;

            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
    }
}
