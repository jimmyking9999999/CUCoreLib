using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using CUCoreLib.Util;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CUCoreLib.Patches;

// The root of all evil...
internal static class KrokMpCompatibilityPatches
{
    private const string KrokMpPluginGuid = "KrokoshaCasualtiesMP";
    private const string GOSyncPacketTypeName = "KrokoshaCasualtiesMP.GOSyncPacket";
    private const string SyncInfoTypeName = "KrokoshaCasualtiesMP.SyncInfo";
    private const string NetObjectRegistryTypeName = "KrokoshaCasualtiesMP.NetObjectRegistry";
    private const string NewObjectSystemTypeName = "KrokoshaCasualtiesMP.NewCoolerObjectPacketWriteReadSystem";
    private static bool _installed;
    private static bool _retryScheduled;
    private static Hook _applyHook;
    private static bool _newLoaderPatched;
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

        var patchedAnything = false;

        var packetType = ResolveLoadedType(GOSyncPacketTypeName);
        if (packetType != null)
        {
            var apply = AccessTools.Method(packetType, "Apply", [typeof(string), typeof(uint)]);
            if (apply != null && TryResolveReflection(packetType))
            {
                var replacement = CreateApplyReplacement(packetType);
                if (replacement != null)
                {
                    _applyHook = new Hook(apply, replacement);
                    patchedAnything = true;
                }
            }
        }

        if (!_newLoaderPatched)
        {
            var newObjectSystemType = ResolveLoadedType(NewObjectSystemTypeName);
            var loadObjectResource = AccessTools.Method(newObjectSystemType, "LoadObjectResource");
            if (loadObjectResource != null)
            {
                harmony.Patch(loadObjectResource,
                    new HarmonyMethod(typeof(KrokMpCompatibilityPatches), nameof(LoadObjectResource_Prefix)));
                _newLoaderPatched = true;
                patchedAnything = true;
            }
        }

        if (patchedAnything)
        {
            _installed = true;
            CUCoreLibPlugin.Log?.LogInfo("CUCoreLib KrokMP compatibility patch installed.");
            return;
        }

        ScheduleRetry(harmony);
    }

    private static DynamicMethod CreateApplyReplacement(Type packetType)
    {
        if (packetType == null || _syncInfoType == null) return null;

        var helper = AccessTools.Method(typeof(KrokMpCompatibilityPatches), nameof(ApplyReplacementBoxed));
        if (helper == null) return null;

        var method = new DynamicMethod(
            "CUCoreLib_KrokMP_GOSyncPacket_ApplyReplacement",
            _syncInfoType,
            [
                packetType.MakeByRefType(),
                typeof(string),
                typeof(uint)
            ],
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
            [typeof(GameObject), typeof(uint)]);
        _unregisterGoMethod = AccessTools.Method(_netObjectRegistryType, "_UnregisterGO", [typeof(uint)]);
        _clientGetRequestedExistenceObjFromIdMethod = AccessTools.Method(_netObjectRegistryType,
            "Client_GetRequestedExistenceObjFromId", [typeof(uint)]);
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
                _clientGetRequestedExistenceObjFromIdMethod.Invoke(null, [request_response]) as
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

            _unregisterGoMethod.Invoke(null, [(uint)_syncInfoSyncIdField.GetValue(syncInfo)]);
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
        return result is true;
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
            _registerGoMethod.Invoke(null, [instance, (uint)_netSyncIdField.GetValue(packet)]);
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
        return false;
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

        if (!normalized.StartsWith("KMPSR_", StringComparison.Ordinal)) return false;
        var stripped = normalized.Substring("KMPSR_".Length);
        var strippedBaseId = SpawnIdHelpers.NormalizeSpawnId(stripped);
        if (!ItemRegistry.TryGetCustomInfo(strippedBaseId, out _) &&
            !BuildingEntityRegistry.TryGetDefinition(strippedBaseId, out _)) return false;
        prefab = CustomInstantiate.ResolvePrefab(strippedBaseId);
        return prefab != null;
    }

    private static bool LoadObjectResource_Prefix(string resourceid, Vector2 pos, ref GameObject __result)
    {
        if (!TryResolveResourcePrefab(resourceid, out var prefab, out var parentToWorldGrid) ||
            prefab == null) return true;

        var instance = Object.Instantiate(prefab, pos, Quaternion.identity);
        if (instance == null)
        {
            __result = null;
            return false;
        }

        if (parentToWorldGrid && WorldGeneration.world != null && WorldGeneration.world.worldGrid != null)
            instance.transform.SetParent(WorldGeneration.world.worldGrid.transform);

        instance.SetActive(true);
        __result = instance;
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
        CoroutineUtils.DelayCall(1f, () =>
        {
            _retryScheduled = false;
            if (_installed) return;

            Install(harmony);
        });
    }

    private static bool IsKrokMpExpected()
    {
        return ResolveLoadedType(GOSyncPacketTypeName) != null ||
               Chainloader.PluginInfos.ContainsKey(KrokMpPluginGuid);
    }

    private static Type ResolveLoadedType(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;

        return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(type => type != null);
    }
}