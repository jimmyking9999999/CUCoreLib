using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using CUCoreLib.Patches;
using CUCoreLib.Registries.Infrastructure;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Registries
{
    public static class LiquidRegistry
    {
        internal enum HealthUseMode
        {
            None,
            ApplyToLimb,
            Inject
        }

        internal static Dictionary<string, CustomLiquidInfo> RegisteredLiquids =
            new Dictionary<string, CustomLiquidInfo>(StringComparer.OrdinalIgnoreCase);

        private static readonly RegistrationOwnershipIndex<string> LiquidOwners =
            new RegistrationOwnershipIndex<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly AsyncLocal<HealthUseMode> CurrentHealthUseMode = new AsyncLocal<HealthUseMode>();

        private static bool LoggedInitialInjection;

        public static void Register(string id, CustomLiquidInfo info)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                CUCoreLibPlugin.Log?.LogWarning("Ignored custom liquid registration with no ID.");
                return;
            }

            if (info == null) info = new CustomLiquidInfo();

            id = id.Trim();
            RegisteredLiquids[id] = info;
            try
            {
                LiquidOwners.Assign(id, ContentReloadSession.ResolveAmbientOwnerId());
            }
            catch
            {
            }

            try
            {
                InjectSingleLiquid(id, info);
            }
            catch
            {
            }

            KrokMpCompatibilityPatches.RefreshLiquidRegistry();
            try
            {
                MultiplayerSyncRegistry.QueueHostSnapshotBroadcast();
            }
            catch
            {
            }

            try
            {
                LogInitialInjectionSummary();
            }
            catch
            {
            }
        }

        public static IDisposable BeginOwnerRegistration(string ownerId)
        {
            return LiquidOwners.BeginScope(ownerId);
        }

        internal static int InjectRegisteredLiquids(bool logSummary = false)
        {
            var injected = 0;
            foreach (var entry in RegisteredLiquids.ToArray())
            {
                try
                {
                    if (InjectSingleLiquid(entry.Key, entry.Value)) injected++;
                }
                catch
                {
                }
            }

            KrokMpCompatibilityPatches.RefreshLiquidRegistry();

            if (logSummary)
                try
                {
                    LogInitialInjectionSummary();
                }
                catch
                {
                }

            return injected;
        }

        internal static void LogInitialInjectionSummary()
        {
            if (LoggedInitialInjection || RegisteredLiquids.Count == 0) return;

            CUCoreLibPlugin.Log.LogInfo($"Added {RegisteredLiquids.Count} liquids");
            LoggedInitialInjection = true;
        }

        internal static bool EnsureLiquidInjected(string id)
        {
            if (!TryGetCustomInfo(id, out var info)) return false;
            if (Liquids.Registry == null) return false;

            try
            {
                if (InjectSingleLiquid(id.Trim(), info)) KrokMpCompatibilityPatches.RefreshLiquidRegistry();
            }
            catch
            {
                return false;
            }
            return true;
        }

        internal static bool InjectSingleLiquid(string id, CustomLiquidInfo info)
        {
            if (string.IsNullOrWhiteSpace(id) || info == null || Liquids.Registry == null) return false;

            if (info.onDrink == null) info.onDrink = (amount, body) => { };

            if (info.onHealthUse == null) info.onHealthUse = (amount, limb) => { };

            try
            {
                LocaleRegistry.RegisterCraftingQualities(info.qualities);
            }
            catch
            {
            }

            var wasPresent = Liquids.Registry.ContainsKey(id);
            Liquids.Registry[id] = new LiquidType
            {
                localeName = id,
                color = info.color,
                valuePerLiter = info.valuePerLiter,
                onDrink = info.onDrink,
                onHealthUse = CreateHealthUseDispatcher(info),
                healthUsable = info.healthUsable,
                injectable = info.injectable,
                injectionSickness = info.injectionSickness,
                localeFromItem = info.localeFromItem,
                qualities = info.qualities ?? new List<CraftingQuality>()
            };

            if (!string.IsNullOrEmpty(info.name))
                try
                {
                    LocaleRegistry.Register("liquid", id, info.name);
                }
                catch
                {
                }

            if (!string.IsNullOrEmpty(info.description))
                try
                {
                    LocaleRegistry.Register("liquid", id + "dsc", info.description);
                }
                catch
                {
                }

            return !wasPresent;
        }

        internal static HealthUseMode PushHealthUseMode(HealthUseMode mode)
        {
            var previousMode = CurrentHealthUseMode.Value;
            CurrentHealthUseMode.Value = mode;
            return previousMode;
        }

        internal static void PopHealthUseMode(HealthUseMode previousMode)
        {
            CurrentHealthUseMode.Value = previousMode;
        }

        internal static HealthUseMode GetCurrentHealthUseMode()
        {
            return CurrentHealthUseMode.Value;
        }

        private static LiquidType.OnHealthUse CreateHealthUseDispatcher(CustomLiquidInfo info)
        {
            return (amount, limb) =>
            {
                var handler = ResolveHealthUseHandler(info);
                handler?.Invoke(amount, limb);
            };
        }

        private static LiquidType.OnHealthUse ResolveHealthUseHandler(CustomLiquidInfo info)
        {
            if (info == null) return null;

            switch (CurrentHealthUseMode.Value)
            {
                case HealthUseMode.ApplyToLimb:
                    return info.onApplyToLimb ?? info.onHealthUse;
                case HealthUseMode.Inject:
                    return info.onInject ?? info.onHealthUse;
                default:
                    return info.onHealthUse ?? info.onApplyToLimb ?? info.onInject;
            }
        }

        public static IEnumerable<string> GetRegisteredLiquidIds()
        {
            return RegisteredLiquids.Keys.ToArray();
        }

        internal static Dictionary<string, CustomLiquidInfo> CaptureOwnerEntries(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return new Dictionary<string, CustomLiquidInfo>(StringComparer.OrdinalIgnoreCase);

            return LiquidOwners.GetKeys(ownerId)
                .Where(id => RegisteredLiquids.TryGetValue(id, out _))
                .ToDictionary(id => id, id => RegisteredLiquids[id], StringComparer.OrdinalIgnoreCase);
        }

        internal static void RestoreOwnerEntries(string ownerId, IDictionary<string, CustomLiquidInfo> entries)
        {
            if (entries == null || entries.Count == 0) return;

            foreach (var entry in entries) Register(entry.Key, entry.Value);
        }

        internal static void ClearOwnerEntries(string ownerId, ContentReloadResult result)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return;

            var normalizedOwnerId = ownerId.Trim();
            var ids = LiquidOwners.GetKeys(normalizedOwnerId);

            foreach (var id in ids)
            {
                RegisteredLiquids.Remove(id);
                LiquidOwners.Remove(id);
                Liquids.Registry?.Remove(id);
            }

            KrokMpCompatibilityPatches.RefreshLiquidRegistry();

            if (ids.Length > 0)
                result?.AddInfo("Cleared " + ids.Length + " liquid registrations owned by '" + normalizedOwnerId + "'.");
        }

        internal static JObject CaptureNetworkSnapshot()
        {
            var root = new JObject();
            foreach (var entry in RegisteredLiquids.ToArray())
            {
                try
                {
                    var info = entry.Value;
                    if (info == null) continue;

                    root[entry.Key] = new JObject
                    {
                        ["name"] = info.name ?? string.Empty,
                        ["description"] = info.description ?? string.Empty,
                        ["color"] = NetworkSnapshotSerialization.WriteColor(info.color),
                        ["valuePerLiter"] = info.valuePerLiter,
                        ["healthUsable"] = info.healthUsable,
                        ["injectable"] = info.injectable,
                        ["injectionSickness"] = info.injectionSickness,
                        ["localeFromItem"] = info.localeFromItem,
                        ["unobtainable"] = info.unobtainable,
                        ["qualities"] = NetworkSnapshotSerialization.WriteCraftingQualities(info.qualities)
                    };
                }
                catch
                {
                }
            }

            return root;
        }

        internal static void ApplyNetworkSnapshot(JObject snapshot)
        {
            if (snapshot == null) return;

            foreach (var property in snapshot.Properties())
            {
                if (!(property.Value is JObject obj)) continue;

                try
                {
                    Register(property.Name, new CustomLiquidInfo
                    {
                        name = obj.Value<string>("name"),
                        description = obj.Value<string>("description"),
                        color = NetworkSnapshotSerialization.ReadColor(obj["color"], Color.white),
                        valuePerLiter = obj.Value<float?>("valuePerLiter") ?? 0f,
                        healthUsable = obj.Value<bool?>("healthUsable") ?? false,
                        injectable = obj.Value<bool?>("injectable") ?? false,
                        injectionSickness = obj.Value<float?>("injectionSickness") ?? 1f,
                        localeFromItem = obj.Value<bool?>("localeFromItem") ?? false,
                        unobtainable = obj.Value<bool?>("unobtainable") ?? false,
                        qualities = NetworkSnapshotSerialization.ReadCraftingQualities(obj["qualities"])
                    });
                }
                catch
                {
                }
            }
        }

        public static bool TryGetCustomInfo(string id, out CustomLiquidInfo info)
        {
            info = null;
            return !string.IsNullOrWhiteSpace(id) && RegisteredLiquids.TryGetValue(id.Trim(), out info);
        }

        internal static Dictionary<string, LiquidType> GetMiniBarrelLiquids()
        {
            var registry = Liquids.Registry;
            if (registry == null || !RegisteredLiquids.Values.Any(info => info != null && info.unobtainable))
                return registry;

            return registry
                .Where(entry => !TryGetCustomInfo(entry.Key, out var info) || info == null || !info.unobtainable)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        }

    }
}
