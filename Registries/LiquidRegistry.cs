using System.Collections.Generic;
using System.Linq;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Registries
{
    public static class LiquidRegistry
    {
        internal static Dictionary<string, CustomLiquidInfo> RegisteredLiquids =
            new Dictionary<string, CustomLiquidInfo>(System.StringComparer.OrdinalIgnoreCase);
        private static bool LoggedInitialInjection;

        public static void Register(string id, CustomLiquidInfo info)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                CUCoreLibPlugin.Log.LogWarning("Ignored custom liquid registration with no ID.");
                return;
            }

            if (info == null)
            {
                info = new CustomLiquidInfo();
            }

            id = id.Trim();
            RegisteredLiquids[id] = info;

            InjectSingleLiquid(id, info);
            LogInitialInjectionSummary();
        }

        internal static int InjectRegisteredLiquids(bool logSummary = false)
        {
            int injected = 0;
            foreach (var kvp in RegisteredLiquids)
            {
                if (InjectSingleLiquid(kvp.Key, kvp.Value))
                {
                    injected++;
                }
            }

            if (logSummary) LogInitialInjectionSummary();

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
            InjectSingleLiquid(id.Trim(), info);
            return true;
        }

        internal static bool InjectSingleLiquid(string id, CustomLiquidInfo info)
        {
            if (string.IsNullOrWhiteSpace(id) || info == null) return false;

            if (info.onDrink == null)
            {
                info.onDrink = (amount, body) => { };
            }

            if (info.onHealthUse == null)
            {
                info.onHealthUse = (amount, limb) => { };
            }

            bool wasPresent = Liquids.Registry.ContainsKey(id);
            Liquids.Registry[id] = new LiquidType
            {
                localeName = id,
                color = info.color,
                valuePerLiter = info.valuePerLiter,
                onDrink = info.onDrink,
                onHealthUse = info.onHealthUse,
                healthUsable = info.healthUsable,
                injectable = info.injectable,
                injectionSickness = info.injectionSickness,
                localeFromItem = info.localeFromItem,
                qualities = info.qualities ?? new List<CraftingQuality>()
            };

            if (!string.IsNullOrEmpty(info.name))
            {
                LocaleRegistry.Register("other", id, info.name);
            }

            if (!string.IsNullOrEmpty(info.description))
            {
                LocaleRegistry.Register("other", id + "dsc", info.description);
            }

            return !wasPresent;
        }

        public static IEnumerable<string> GetRegisteredLiquidIds()
        {
            return RegisteredLiquids.Keys.ToArray();
        }

        internal static JObject CaptureNetworkSnapshot()
        {
            JObject root = new JObject();
            foreach (var entry in RegisteredLiquids)
            {
                CustomLiquidInfo info = entry.Value;
                if (info == null)
                {
                    continue;
                }

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
                    ["qualities"] = NetworkSnapshotSerialization.WriteCraftingQualities(info.qualities)
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
                JObject obj = property.Value as JObject;
                if (obj == null)
                {
                    continue;
                }

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
                    qualities = NetworkSnapshotSerialization.ReadCraftingQualities(obj["qualities"])
                });
            }
        }

        public static bool TryGetCustomInfo(string id, out CustomLiquidInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            return RegisteredLiquids.TryGetValue(id.Trim(), out info);
        }
    }
}
