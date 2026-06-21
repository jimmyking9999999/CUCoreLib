using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace CUCoreLib.Patches
{
    // The root of all evil...
    internal static class KrokMpCompatibilityPatches
    {
        private const string KrokMpPluginGuid = "KrokoshaCasualtiesMP";
        private const string GOSyncPacketTypeName = "KrokoshaCasualtiesMP.GOSyncPacket";
        private const string SyncInfoTypeName = "KrokoshaCasualtiesMP.SyncInfo";
        private const string NetObjectRegistryTypeName = "KrokoshaCasualtiesMP.NetObjectRegistry";
        private static bool _installed;
        private static bool _retryScheduled;
        private static Hook _applyHook;
        private static Type _syncInfoType;
        private static Type _netObjectRegistryType;
        private static MethodInfo _getSyncInfoMethod;
        private static MethodInfo _registerGoMethod;
        private static MethodInfo _unregisterGoMethod;
        private static MethodInfo _clientGetRequestedExistenceObjFromIdMethod;
        private static FieldInfo _netSyncIdField;
        private static FieldInfo _objTypeField;
        private static FieldInfo _posField;
        private static FieldInfo _angleField;
        private static FieldInfo _scaleXField;
        private static FieldInfo _scaleYField;
        private static FieldInfo _syncInfoGoField;
        private static FieldInfo _syncInfoLastUpdateTimeField;
        private static FieldInfo _syncInfoObjTypeField;
        private static FieldInfo _syncInfoSyncIdField;
        private static FieldInfo _knownEntitiesWithNonUniqueIdField;
        private static MethodInfo _syncInfoIsIgnoredMethod;

        internal static void Install(Harmony harmony)
        {
            if (harmony == null || _installed) return;

            var packetType = ResolveLoadedType(GOSyncPacketTypeName);
            if (packetType == null)
            {
                ScheduleRetry(harmony);
                return;
            }

            var apply = AccessTools.Method(packetType, "Apply", new[] { typeof(string), typeof(uint) });
            if (apply == null)
            {
                ScheduleRetry(harmony);
                return;
            }

            if (!TryResolveReflection(packetType))
            {
                ScheduleRetry(harmony);
                return;
            }

            var replacement = CreateApplyReplacement(packetType);
            if (replacement == null)
            {
                ScheduleRetry(harmony);
                return;
            }

            _applyHook = new Hook(apply, replacement);
            _installed = true;
            CUCoreLibPlugin.Log?.LogInfo("CUCoreLib KrokMP compatibility patch installed.");
        }

        private static DynamicMethod CreateApplyReplacement(Type packetType)
        {
            if (packetType == null || _syncInfoType == null) return null;

            var helper = AccessTools.Method(typeof(KrokMpCompatibilityPatches), nameof(ApplyReplacementBoxed));
            if (helper == null) return null;

            var method = new DynamicMethod(
                "CUCoreLib_KrokMP_GOSyncPacket_ApplyReplacement",
                _syncInfoType,
                new[]
                {
                    packetType.MakeByRefType(),
                    typeof(string),
                    typeof(uint)
                },
                typeof(KrokMpCompatibilityPatches).Module,
                true);

            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldobj, packetType);
            il.Emit(OpCodes.Box, packetType);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, helper);
            il.Emit(OpCodes.Castclass, _syncInfoType);
            il.Emit(OpCodes.Ret);
            return method;
        }

        private static bool TryResolveReflection(Type packetType)
        {
            _syncInfoType = ResolveLoadedType(SyncInfoTypeName);
            _netObjectRegistryType = ResolveLoadedType(NetObjectRegistryTypeName);
            if (_syncInfoType == null || _netObjectRegistryType == null) return false;

            _getSyncInfoMethod = AccessTools.Method(packetType, "GetSyncInfo");
            _registerGoMethod = AccessTools.Method(_netObjectRegistryType, "_RegisterGO",
                new[] { typeof(GameObject), typeof(uint) });
            _unregisterGoMethod = AccessTools.Method(_netObjectRegistryType, "_UnregisterGO", new[] { typeof(uint) });
            _clientGetRequestedExistenceObjFromIdMethod = AccessTools.Method(_netObjectRegistryType,
                "Client_GetRequestedExistenceObjFromId", new[] { typeof(uint) });
            _netSyncIdField = AccessTools.Field(packetType, "net_syncid");
            _objTypeField = AccessTools.Field(packetType, "objtype");
            _posField = AccessTools.Field(packetType, "pos");
            _angleField = AccessTools.Field(packetType, "angle");
            _scaleXField = AccessTools.Field(packetType, "scale_x");
            _scaleYField = AccessTools.Field(packetType, "scale_y");
            _syncInfoGoField = AccessTools.Field(_syncInfoType, "go");
            _syncInfoLastUpdateTimeField = AccessTools.Field(_syncInfoType, "last_update_time");
            _syncInfoObjTypeField = AccessTools.Field(_syncInfoType, "objtype");
            _syncInfoSyncIdField = AccessTools.Field(_syncInfoType, "syncid");
            _syncInfoIsIgnoredMethod = AccessTools.Method(_syncInfoType, "IsIgnored");
            var scavMultiBuildingSynchronizerType =
                ResolveLoadedType("KrokoshaCasualtiesMP.ScavMultiBuildingSynchronizer");
            _knownEntitiesWithNonUniqueIdField =
                AccessTools.Field(scavMultiBuildingSynchronizerType, "known_entities_with_nonunique_id");

            return _getSyncInfoMethod != null &&
                   _registerGoMethod != null &&
                   _unregisterGoMethod != null &&
                   _clientGetRequestedExistenceObjFromIdMethod != null &&
                   _netSyncIdField != null &&
                   _objTypeField != null &&
                   _posField != null &&
                   _angleField != null &&
                   _scaleXField != null &&
                   _scaleYField != null &&
                   _syncInfoGoField != null &&
                   _syncInfoLastUpdateTimeField != null &&
                   _syncInfoObjTypeField != null &&
                   _syncInfoSyncIdField != null &&
                   _syncInfoIsIgnoredMethod != null;
        }

        private static object ApplyReplacementBoxed(object packet, string resource_stringid, uint request_response)
        {
            if (packet == null) return null;

            if (request_response != 0u)
            {
                var requested =
                    _clientGetRequestedExistenceObjFromIdMethod.Invoke(null, new object[] { request_response }) as
                        GameObject;
                if (requested != null)
                {
                    var requestedSyncInfo = RegisterPacketObject(packet, requested);
                    if (requestedSyncInfo == null || IsIgnored(requestedSyncInfo)) return null;

                    ApplyTransform(packet, requested);
                    TouchSyncInfo(requestedSyncInfo);
                    return requestedSyncInfo;
                }
            }

            var syncInfo = _getSyncInfoMethod.Invoke(packet, null);
            if (syncInfo != null)
            {
                if (IsIgnored(syncInfo)) return null;

                TouchSyncInfo(syncInfo);
                var existing = _syncInfoGoField.GetValue(syncInfo) as GameObject;
                if (existing != null)
                {
                    ApplyTransform(packet, existing);
                    return syncInfo;
                }

                _unregisterGoMethod.Invoke(null, new object[] { (uint)_syncInfoSyncIdField.GetValue(syncInfo) });
            }

            if (string.IsNullOrWhiteSpace(resource_stringid)) return null;

            if (!TryResolveResourcePrefab(resource_stringid, out var prefab, out var parentToWorldGrid) ||
                prefab == null)
            {
                LogMissingCustomResolution(resource_stringid);
                return null;
            }

            var position = (Vector2)_posField.GetValue(packet);
            var instance = Object.Instantiate(prefab, position, Quaternion.identity);
            if (instance == null) return null;

            if (parentToWorldGrid && WorldGeneration.world != null && WorldGeneration.world.worldGrid != null)
                instance.transform.SetParent(WorldGeneration.world.worldGrid.transform);

            instance.SetActive(true);
            var registered = RegisterPacketObject(packet, instance);
            if (registered == null)
            {
                Object.Destroy(instance);
                return null;
            }

            ApplyTransform(packet, instance);
            TouchSyncInfo(registered);
            return registered;
        }

        private static bool IsIgnored(object syncInfo)
        {
            var result = _syncInfoIsIgnoredMethod.Invoke(syncInfo, null);
            return result is bool flag && flag;
        }

        private static void ApplyTransform(object packet, GameObject instance)
        {
            if (instance == null) return;

            var position = (Vector2)_posField.GetValue(packet);
            var angle = (float)_angleField.GetValue(packet);
            var scaleX = (float)_scaleXField.GetValue(packet);
            var scaleY = (float)_scaleYField.GetValue(packet);
            instance.transform.localScale = new Vector3(scaleX, scaleY, instance.transform.localScale.z);
            if (instance.TryGetComponent(out Rigidbody2D rigidbody) && rigidbody.bodyType == RigidbodyType2D.Dynamic)
            {
                rigidbody.position = position;
                rigidbody.rotation = angle;
                return;
            }

            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static object RegisterPacketObject(object packet, GameObject instance)
        {
            var registered =
                _registerGoMethod.Invoke(null, new object[] { instance, (uint)_netSyncIdField.GetValue(packet) });
            if (registered != null) _syncInfoObjTypeField.SetValue(registered, _objTypeField.GetValue(packet));

            return registered;
        }

        private static void TouchSyncInfo(object syncInfo)
        {
            _syncInfoLastUpdateTimeField.SetValue(syncInfo, Time.realtimeSinceStartupAsDouble);
        }

        private static bool TryResolveResourcePrefab(string resourceStringId, out GameObject prefab,
            out bool parentToWorldGrid)
        {
            prefab = null;
            parentToWorldGrid = false;
            if (string.IsNullOrWhiteSpace(resourceStringId)) return false;

            var normalized = resourceStringId.Trim();
            if (TryResolveCustomPrefab(normalized, out prefab)) return prefab != null;

            prefab = Resources.Load<GameObject>(normalized);
            if (prefab != null) return true;

            if (!normalized.StartsWith("KMPSR_", StringComparison.Ordinal)) return false;

            if (_knownEntitiesWithNonUniqueIdField != null &&
                _knownEntitiesWithNonUniqueIdField.GetValue(null) is IDictionary knownEntities &&
                knownEntities.Contains(normalized))
            {
                prefab = knownEntities[normalized] as GameObject;
                parentToWorldGrid = prefab != null && prefab.TryGetComponent(out Tilemap _);
                return prefab != null;
            }

            prefab = Resources.Load<GameObject>(normalized.Substring("KMPSR_".Length));
            return prefab != null;
        }

        private static bool TryResolveCustomPrefab(string resourceStringId, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(resourceStringId)) return false;

            var normalized = resourceStringId.Trim();
            var baseId = SpawnIdHelpers.NormalizeSpawnId(normalized);
            var isCustomId = ItemRegistry.TryGetCustomInfo(baseId, out _) ||
                             BuildingEntityRegistry.TryGetDefinition(baseId, out _);
            if (isCustomId)
            {
                prefab = CustomInstantiate.ResolvePrefab(baseId);
                if (prefab != null) return true;
            }

            if (normalized.StartsWith("KMPSR_", StringComparison.Ordinal))
            {
                var stripped = normalized.Substring("KMPSR_".Length);
                var strippedBaseId = SpawnIdHelpers.NormalizeSpawnId(stripped);
                if (ItemRegistry.TryGetCustomInfo(strippedBaseId, out _) ||
                    BuildingEntityRegistry.TryGetDefinition(strippedBaseId, out _))
                {
                    prefab = CustomInstantiate.ResolvePrefab(strippedBaseId);
                    if (prefab != null) return true;
                }
            }

            return false;
        }

        private static void LogMissingCustomResolution(string resourceStringId)
        {
            var normalized = SpawnIdHelpers.NormalizeSpawnId(resourceStringId);
            var hasItem = ItemRegistry.TryGetCustomInfo(normalized, out _);
            var hasBuilding = BuildingEntityRegistry.TryGetDefinition(normalized, out _);
            if (hasItem || hasBuilding)
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib could not build KrokMP custom resource '" +
                                                resourceStringId.Trim() + "' even though it is registered.");
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

        private static bool IsKrokMpExpected()
        {
            if (ResolveLoadedType(GOSyncPacketTypeName) != null) return true;

            return Chainloader.PluginInfos.ContainsKey(KrokMpPluginGuid);
        }

        private static Type ResolveLoadedType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }

            return null;
        }
    }
}