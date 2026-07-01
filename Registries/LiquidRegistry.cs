using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Registries;

public static class LiquidRegistry
{
    internal static Dictionary<string, CustomLiquidInfo> RegisteredLiquids = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> LiquidOwners = new(StringComparer.OrdinalIgnoreCase);

    private static bool LoggedInitialInjection;
    private static string ActiveOwnerId;

    public static void Register(string id, CustomLiquidInfo info)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            CUCoreLibPlugin.Log.LogWarning("Ignored custom liquid registration with no ID.");
            return;
        }

        info ??= new CustomLiquidInfo();

        id = id.Trim();
        RegisteredLiquids[id] = info;
        var ownerId = !string.IsNullOrWhiteSpace(ActiveOwnerId)
            ? ActiveOwnerId
            : ContentReloadSession.ResolveAmbientOwnerId();
        if (!string.IsNullOrWhiteSpace(ownerId)) LiquidOwners[id] = ownerId;

        InjectSingleLiquid(id, info);
        LogInitialInjectionSummary();
    }

    public static IDisposable BeginOwnerRegistration(string ownerId)
    {
        return new OwnerScope(ownerId);
    }

    internal static int InjectRegisteredLiquids(bool logSummary = false)
    {
        var injected = RegisteredLiquids.Count(kvp => InjectSingleLiquid(kvp.Key, kvp.Value));

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

        info.onDrink ??= (amount, body) => { };

        info.onHealthUse ??= (amount, limb) => { };

        var wasPresent = Liquids.Registry.ContainsKey(id);
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
            qualities = info.qualities ?? []
        };

        if (!string.IsNullOrEmpty(info.name)) LocaleRegistry.Register("liquid", id, info.name);

        if (!string.IsNullOrEmpty(info.description)) LocaleRegistry.Register("liquid", id + "dsc", info.description);

        return !wasPresent;
    }

    public static IEnumerable<string> GetRegisteredLiquidIds()
    {
        return RegisteredLiquids.Keys.ToArray();
    }

    internal static Dictionary<string, CustomLiquidInfo> CaptureOwnerEntries(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return new Dictionary<string, CustomLiquidInfo>(StringComparer.OrdinalIgnoreCase);

        var normalizedOwnerId = ownerId.Trim();
        return LiquidOwners
            .Where(entry => string.Equals(entry.Value, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Key)
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
        var ids = LiquidOwners
            .Where(entry => string.Equals(entry.Value, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var id in ids)
        {
            RegisteredLiquids.Remove(id);
            LiquidOwners.Remove(id);
            Liquids.Registry?.Remove(id);
        }

        if (ids.Length > 0)
            result?.AddInfo("Cleared " + ids.Length + " liquid registrations owned by '" + normalizedOwnerId + "'.");
    }

    internal static JObject CaptureNetworkSnapshot()
    {
        var root = new JObject();
        foreach (var entry in RegisteredLiquids)
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
                ["qualities"] = NetworkSnapshotSerialization.WriteCraftingQualities(info.qualities)
            };
        }

        return root;
    }

    internal static void ApplyNetworkSnapshot(JObject snapshot)
    {
        if (snapshot == null) return;

        foreach (var property in snapshot.Properties())
        {
            if (property.Value is not JObject obj) continue;

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
        return !string.IsNullOrWhiteSpace(id) && RegisteredLiquids.TryGetValue(id.Trim(), out info);
    }

    private sealed class OwnerScope : IDisposable
    {
        private readonly string previousOwnerId;

        public OwnerScope(string ownerId)
        {
            previousOwnerId = ActiveOwnerId;
            ActiveOwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId.Trim();
        }

        public void Dispose()
        {
            ActiveOwnerId = previousOwnerId;
        }
    }
}