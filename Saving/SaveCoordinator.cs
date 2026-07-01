using System;
using System.Collections.Generic;
using System.IO;
using CUCoreLib.Registries;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Saving;

internal sealed class SaveItemEntry
{
    public Item Item;
    public string Key;
}

internal static class SaveCoordinator
{
    // Seralization my beloathed
    private const string RootKey = "CUCoreLib";
    private const string VersionKey = "version";
    private const string PayloadKey = "payload";
    private const int SchemaVersion = 1;

    private static JObject PendingRestoreRoot;

    internal static void ClearPendingRestore()
    {
        PendingRestoreRoot = null;
    }

    internal static JObject CaptureNetworkSnapshot()
    {
        return CaptureRoot();
    }

    internal static void ApplyNetworkSnapshot(JObject snapshot)
    {
        if (snapshot == null) return;

        // Reuse the same restore pipeline as save-file loads
        PendingRestoreRoot = snapshot;
        ApplyPendingRestore();
    }

    internal static void EmbedIntoSaveFile()
    {
        try
        {
            var path = GetSavePath();
            if (!File.Exists(path)) return;

            var json = SaveSystem.Unzip(File.ReadAllBytes(path));
            var saveRoot = JObject.Parse(json);
            var cuRoot = CaptureRoot();
            if (cuRoot == null) return;

            saveRoot[RootKey] = cuRoot;
            var updatedJson = JsonConvert.SerializeObject(saveRoot, Formatting.None);
            File.WriteAllBytes(path, SaveSystem.Zip(updatedJson));
        }
        catch (Exception ex)
        {
            SaveLogger.Warn("Failed to append CUCoreLib payload to save file.", ex);
        }
    }

    internal static void PrepareRestoreFromSaveFile()
    {
        ClearPendingRestore();

        try
        {
            var path = GetSavePath();
            if (!File.Exists(path)) return;

            var json = SaveSystem.Unzip(File.ReadAllBytes(path));
            var saveRoot = JObject.Parse(json);
            // Only cache the CUCoreLib section so restore ignores the rest of the save file
            PendingRestoreRoot = saveRoot[RootKey] as JObject;
        }
        catch (Exception ex)
        {
            SaveLogger.Warn("Failed to read CUCoreLib payload from save file.", ex);
        }
    }

    internal static void ApplyPendingRestore()
    {
        if (PendingRestoreRoot == null) return;

        try
        {
            var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;


            var restoreContext = new SaveRestoreContext();
            WarnUnknownProviders(PendingRestoreRoot["global"] as JObject, SaveRegistry.GlobalProviderKeys,
                "global");
            WarnUnknownProviders(PendingRestoreRoot["body"] as JObject, SaveRegistry.BodyProviderKeys, "body");
            WarnUnknownProviders(PendingRestoreRoot["world"] as JObject, SaveRegistry.WorldProviderKeys, "world");

            ApplyGlobalProviders(PendingRestoreRoot["global"] as JObject, restoreContext);
            ApplyBodyProviders(body, PendingRestoreRoot["body"] as JObject, restoreContext);
            ApplyLimbProviders(body, PendingRestoreRoot["limbs"] as JArray, restoreContext);
            ApplyItemProviders(body, PendingRestoreRoot["items"] as JObject, restoreContext);
            ApplyWorldProviders(PendingRestoreRoot["world"] as JObject, restoreContext);

            restoreContext.ExecuteDeferred();
        }
        catch (Exception ex)
        {
            SaveLogger.Warn("Unexpected failure while restoring CUCoreLib save payload D:", ex);
        }
        finally
        {
            ClearPendingRestore();
        }
    }

    private static JObject CaptureRoot()
    {
        var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
        if (body == null)
        {
            SaveLogger.Warn("Skipping custom save capture because the player body is not ready.");
            return null;
        }

        var root = new JObject
        {
            ["version"] = SchemaVersion
        };

        var global = CaptureGlobalProviders();
        var bodyPayload = CaptureBodyProviders(body);
        var limbs = CaptureLimbProviders(body);
        var items = CaptureItemProviders(body);
        var world = CaptureWorldProviders();

        // Need all sections present
        root["global"] = global ?? new JObject();
        root["body"] = bodyPayload ?? new JObject();
        root["limbs"] = limbs ?? [];
        root["items"] = items ?? new JObject();
        root["world"] = world ?? new JObject();

        return root;
    }

    private static JObject CaptureGlobalProviders()
    {
        var result = new JObject();

        foreach (var entry in SaveRegistry.GlobalProviders)
            CaptureProvider(entry.Key, entry.Value, result, () => entry.Value.Capture());

        return result;
    }

    private static JObject CaptureBodyProviders(Body body)
    {
        var result = new JObject();

        foreach (var entry in SaveRegistry.BodyProviders)
            CaptureProvider(entry.Key, entry.Value, result, () => entry.Value.Capture(body));

        return result;
    }

    private static JArray CaptureLimbProviders(Body body)
    {
        var result = new JArray();
        var limbs = body.limbs ?? [];
        for (var i = 0; i < limbs.Length; i++)
        {
            var limbObject = new JObject();
            var limb = limbs[i];

            foreach (var entry in SaveRegistry.LimbProviders)
            {
                var limbIndex = i;
                CaptureProvider(entry.Key, entry.Value, limbObject,
                    () => entry.Value.Capture(limb, limbIndex));
            }

            result.Add(limbObject);
        }

        return result;
    }

    private static JObject CaptureItemProviders(Body body)
    {
        var result = new JObject();
        foreach (var entry in BuildItemEntries(body))
        {
            var itemObject = new JObject();

            foreach (var providerEntry in SaveRegistry.ItemProviders)
            {
                var itemKey = entry.Key;
                var item = entry.Item;
                CaptureProvider(providerEntry.Key, providerEntry.Value, itemObject,
                    () => providerEntry.Value.Capture(item, itemKey));
            }

            if (itemObject.HasValues) result[entry.Key] = itemObject;
        }

        return result;
    }

    private static JObject CaptureWorldProviders()
    {
        var result = new JObject();
        var context = new WorldSaveContext();

        foreach (var entry in SaveRegistry.WorldProviders)
            CaptureProvider(entry.Key, entry.Value, result, () => entry.Value.Capture(context));

        return result;
    }

    private static void CaptureProvider(string key, object provider, JObject destination, Func<JToken> capture)
    {
        try
        {
            var payload = capture();
            if (payload == null || payload.Type == JTokenType.Null) return;

            var version = GetProviderVersion(provider);
            destination[key] = WrapPayload(payload, version);
        }
        catch (Exception ex)
        {
            SaveLogger.Warn("Provider '" + key + "' failed during save capture.", ex);
        }
    }

    private static void ApplyGlobalProviders(JObject payloadRoot, SaveRestoreContext context)
    {
        if (payloadRoot == null) return;

        foreach (var property in payloadRoot.Properties())
        {
            if (!SaveRegistry.GlobalProviders.TryGetValue(property.Name, out var provider))
            {
                SaveLogger.Warn("Skipping missing global save provider '" + property.Name + "'.");
                continue;
            }

            RestoreProvider(property.Name, property.Value as JObject,
                delegate(JToken payload, int version) { provider.Restore(payload, version, context); });
        }
    }

    private static void ApplyBodyProviders(Body body, JObject payloadRoot, SaveRestoreContext context)
    {
        if (payloadRoot == null) return;

        foreach (var property in payloadRoot.Properties())
        {
            if (!SaveRegistry.BodyProviders.TryGetValue(property.Name, out var provider))
            {
                SaveLogger.Warn("Skipping missing body save provider '" + property.Name + "'.");
                continue;
            }

            RestoreProvider(property.Name, property.Value as JObject,
                delegate(JToken payload, int version) { provider.Restore(body, payload, version, context); });
        }
    }

    private static void ApplyLimbProviders(Body body, JArray payloadRoot, SaveRestoreContext context)
    {
        if (payloadRoot == null || body.limbs == null) return;

        for (var i = 0; i < payloadRoot.Count; i++)
        {
            if (i >= body.limbs.Length)
            {
                SaveLogger.Warn(
                    "Skipping saved limb payload at index " + i + " because that limb no longer exists.");
                continue;
            }

            if (payloadRoot[i] is not JObject limbObject) continue;

            WarnUnknownProviders(limbObject, SaveRegistry.LimbProviderKeys, "limb[" + i + "]");

            foreach (var property in limbObject.Properties())
            {
                if (!SaveRegistry.LimbProviders.TryGetValue(property.Name, out var provider))
                {
                    SaveLogger.Warn("Skipping missing limb save provider '" + property.Name + "' for limb index " +
                                    i + ".");
                    continue;
                }

                var limbIndex = i;
                var limb = body.limbs[i];
                RestoreProvider(property.Name, property.Value as JObject,
                    delegate(JToken payload, int version)
                    {
                        provider.Restore(limb, limbIndex, payload, version, context);
                    });
            }
        }
    }

    private static void ApplyItemProviders(Body body, JObject payloadRoot, SaveRestoreContext context)
    {
        if (payloadRoot == null) return;

        var itemLookup = new Dictionary<string, Item>(StringComparer.Ordinal);
        foreach (var entry in BuildItemEntries(body)) itemLookup[entry.Key] = entry.Item;

        foreach (var itemProperty in payloadRoot.Properties())
        {
            if (!itemLookup.TryGetValue(itemProperty.Name, out var item) || item == null)
            {
                SaveLogger.Warn("Skipping saved item payload for missing item key '" + itemProperty.Name + "'.");
                continue;
            }

            if (itemProperty.Value is not JObject itemObject) continue;

            WarnUnknownProviders(itemObject, SaveRegistry.ItemProviderKeys, "item[" + itemProperty.Name + "]");

            foreach (var providerProperty in itemObject.Properties())
            {
                if (!SaveRegistry.ItemProviders.TryGetValue(providerProperty.Name, out var provider))
                {
                    SaveLogger.Warn("Skipping missing item save provider '" + providerProperty.Name +
                                    "' for item key '" + itemProperty.Name + "'.");
                    continue;
                }

                var itemKey = itemProperty.Name;
                RestoreProvider(providerProperty.Name, providerProperty.Value as JObject,
                    delegate(JToken payload, int version)
                    {
                        provider.Restore(item, itemKey, payload, version, context);
                    });
            }
        }
    }

    private static void ApplyWorldProviders(JObject payloadRoot, SaveRestoreContext context)
    {
        if (payloadRoot == null) return;

        var worldContext = new WorldSaveContext();

        foreach (var property in payloadRoot.Properties())
        {
            if (!SaveRegistry.WorldProviders.TryGetValue(property.Name, out var provider))
            {
                SaveLogger.Warn("Skipping missing world save provider '" + property.Name + "'.");
                continue;
            }

            RestoreProvider(property.Name, property.Value as JObject,
                delegate(JToken payload, int version) { provider.Restore(worldContext, payload, version, context); });
        }
    }

    private static void RestoreProvider(string key, JObject wrappedPayload, Action<JToken, int> restore)
    {
        if (wrappedPayload == null)
        {
            SaveLogger.Warn("Skipping malformed save payload for provider '" + key + "'.");
            return;
        }

        try
        {
            var version = wrappedPayload.Value<int?>(VersionKey) ?? 0;
            var payload = wrappedPayload[PayloadKey];
            restore(payload, version);
        }
        catch (Exception ex)
        {
            SaveLogger.Warn("Provider '" + key + "' failed during restore.", ex);
        }
    }

    private static JObject WrapPayload(JToken payload, int version)
    {
        return new JObject
        {
            [VersionKey] = version,
            [PayloadKey] = payload.DeepClone()
        };
    }

    private static int GetProviderVersion(object provider)
    {
        return provider switch
        {
            ICustomSaveProvider custom => Math.Max(0, custom.GetVersion()),
            IItemSaveProvider item => Math.Max(0, item.GetVersion()),
            IBodySaveProvider body => Math.Max(0, body.GetVersion()),
            ILimbSaveProvider limb => Math.Max(0, limb.GetVersion()),
            IWorldSaveProvider world => Math.Max(0, world.GetVersion()),
            _ => 0
        };
    }

    private static List<SaveItemEntry> BuildItemEntries(Body body)
    {
        var entries = new List<SaveItemEntry>();
        if (body == null) return entries;

        // Item keys -> IDs for slot/wearables/containers/content.
        if (body.slots != null)
            for (var i = 0; i < body.slots.Length; i++)
            {
                if (!body.HoldingItem(i)) continue;

                var heldItem = body.GetItem(i);
                if (heldItem == null) continue;

                var key = "slot:" + i;
                entries.Add(new SaveItemEntry { Key = key, Item = heldItem });
                AppendContainerEntries(entries, heldItem, key);
            }

        foreach (var wearable in body.GetAllWearables())
        {
            if (wearable == null || wearable.Stats == null) continue;

            var wearSlot = string.IsNullOrWhiteSpace(wearable.Stats.wearSlotId)
                ? "unknown"
                : wearable.Stats.wearSlotId;
            var key = "wear:" + wearSlot;
            entries.Add(new SaveItemEntry { Key = key, Item = wearable });
            AppendContainerEntries(entries, wearable, key);
        }

        return entries;
    }

    private static void AppendContainerEntries(List<SaveItemEntry> entries, Item parentItem, string parentKey)
    {
        if (entries == null || parentItem == null || !parentItem.GetComponent<Container>()) return;

        // Nested container items must inherit the parent key
        var nestedIndex = 0;
        foreach (Transform child in parentItem.transform)
        {
            if (child == null || !child.TryGetComponent(out Item nestedItem)) continue;

            var key = parentKey + "/container:" + nestedIndex;
            entries.Add(new SaveItemEntry { Key = key, Item = nestedItem });
            nestedIndex++;
        }
    }

    private static void WarnUnknownProviders(JObject payloadRoot, IEnumerable<string> knownKeys, string scope)
    {
        if (payloadRoot == null) return;

        var known = new HashSet<string>(knownKeys, StringComparer.Ordinal);
        foreach (var property in payloadRoot.Properties())
            if (!known.Contains(property.Name))
                SaveLogger.Warn("Skipping unknown " + scope + " save provider '" + property.Name + "'.");
    }

    private static string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, "save.sv");
    }
}

internal static class SaveLogger
{
    internal static void Warn(string message)
    {
        CUCoreLibPlugin.Log?.LogWarning("" + message);
    }

    internal static void Warn(string message, Exception ex)
    {
        CUCoreLibPlugin.Log?.LogWarning("" + message + "\n" + ex);
    }
}