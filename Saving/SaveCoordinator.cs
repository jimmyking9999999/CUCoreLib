using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using CUCoreLib.Registries;

namespace CUCoreLib.Saving
{
    internal sealed class SaveItemEntry
    {
        public string Key;
        public Item Item;
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
            if (snapshot == null)
            {
                return;
            }

            // Reuse the same restore pipeline as save-file loads
            PendingRestoreRoot = snapshot;
            ApplyPendingRestore();
        }

        internal static void EmbedIntoSaveFile()
        {
            try
            {
                string path = GetSavePath();
                if (!File.Exists(path))
                {
                    return;
                }

                string json = SaveSystem.Unzip(File.ReadAllBytes(path));
                JObject saveRoot = JObject.Parse(json);
                JObject cuRoot = CaptureRoot();
                if (cuRoot == null)
                {
                    return;
                }

                saveRoot[RootKey] = cuRoot;
                string updatedJson = JsonConvert.SerializeObject(saveRoot, Formatting.None);
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
                string path = GetSavePath();
                if (!File.Exists(path))
                {
                    return;
                }

                string json = SaveSystem.Unzip(File.ReadAllBytes(path));
                JObject saveRoot = JObject.Parse(json);
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
            if (PendingRestoreRoot == null)
            {
                return;
            }

            try
            {
                Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
                

                SaveRestoreContext restoreContext = new SaveRestoreContext();
                WarnUnknownProviders(PendingRestoreRoot["global"] as JObject, SaveRegistry.GlobalProviderKeys, "global");
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
            Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (body == null)
            {
                SaveLogger.Warn("Skipping custom save capture because the player body is not ready.");
                return null;
            }

            JObject root = new JObject
            {
                ["version"] = SchemaVersion
            };

            JObject global = CaptureGlobalProviders();
            JObject bodyPayload = CaptureBodyProviders(body);
            JArray limbs = CaptureLimbProviders(body);
            JObject items = CaptureItemProviders(body);
            JObject world = CaptureWorldProviders();

            // Need all sections present
            root["global"] = global ?? new JObject();
            root["body"] = bodyPayload ?? new JObject();
            root["limbs"] = limbs ?? new JArray();
            root["items"] = items ?? new JObject();
            root["world"] = world ?? new JObject();

            return root;
        }

        private static JObject CaptureGlobalProviders()
        {
            JObject result = new JObject();

            foreach (KeyValuePair<string, ICustomSaveProvider> entry in SaveRegistry.GlobalProviders)
            {
                CaptureProvider(entry.Key, entry.Value, result, delegate
                {
                    return entry.Value.Capture();
                });
            }

            return result;
        }

        private static JObject CaptureBodyProviders(Body body)
        {
            JObject result = new JObject();

            foreach (KeyValuePair<string, IBodySaveProvider> entry in SaveRegistry.BodyProviders)
            {
                CaptureProvider(entry.Key, entry.Value, result, delegate
                {
                    return entry.Value.Capture(body);
                });
            }

            return result;
        }

        private static JArray CaptureLimbProviders(Body body)
        {
            JArray result = new JArray();
            Limb[] limbs = body.limbs ?? Array.Empty<Limb>();
            for (int i = 0; i < limbs.Length; i++)
            {
                JObject limbObject = new JObject();
                Limb limb = limbs[i];

                foreach (KeyValuePair<string, ILimbSaveProvider> entry in SaveRegistry.LimbProviders)
                {
                    int limbIndex = i;
                    CaptureProvider(entry.Key, entry.Value, limbObject, delegate
                    {
                        return entry.Value.Capture(limb, limbIndex);
                    });
                }

                result.Add(limbObject);
            }

            return result;
        }

        private static JObject CaptureItemProviders(Body body)
        {
            JObject result = new JObject();
            foreach (SaveItemEntry entry in BuildItemEntries(body))
            {
                JObject itemObject = new JObject();

                foreach (KeyValuePair<string, IItemSaveProvider> providerEntry in SaveRegistry.ItemProviders)
                {
                    string itemKey = entry.Key;
                    Item item = entry.Item;
                    CaptureProvider(providerEntry.Key, providerEntry.Value, itemObject, delegate
                    {
                        return providerEntry.Value.Capture(item, itemKey);
                    });
                }

                if (itemObject.HasValues)
                {
                    result[entry.Key] = itemObject;
                }
            }

            return result;
        }

        private static JObject CaptureWorldProviders()
        {
            JObject result = new JObject();
            WorldSaveContext context = new WorldSaveContext();

            foreach (KeyValuePair<string, IWorldSaveProvider> entry in SaveRegistry.WorldProviders)
            {
                CaptureProvider(entry.Key, entry.Value, result, delegate
                {
                    return entry.Value.Capture(context);
                });
            }

            return result;
        }

        private static void CaptureProvider(string key, object provider, JObject destination, Func<JToken> capture)
        {
            try
            {
                JToken payload = capture();
                if (payload == null || payload.Type == JTokenType.Null)
                {
                    return;
                }

                int version = GetProviderVersion(provider);
                destination[key] = WrapPayload(payload, version);
            }
            catch (Exception ex)
            {
                SaveLogger.Warn("Provider '" + key + "' failed during save capture.", ex);
            }
        }

        private static void ApplyGlobalProviders(JObject payloadRoot, SaveRestoreContext context)
        {
            if (payloadRoot == null)
            {
                return;
            }

            foreach (JProperty property in payloadRoot.Properties())
            {
                if (!SaveRegistry.GlobalProviders.TryGetValue(property.Name, out ICustomSaveProvider provider))
                {
                    SaveLogger.Warn("Skipping missing global save provider '" + property.Name + "'.");
                    continue;
                }

                RestoreProvider(property.Name, property.Value as JObject, delegate(JToken payload, int version)
                {
                    provider.Restore(payload, version, context);
                });
            }
        }

        private static void ApplyBodyProviders(Body body, JObject payloadRoot, SaveRestoreContext context)
        {
            if (payloadRoot == null)
            {
                return;
            }

            foreach (JProperty property in payloadRoot.Properties())
            {
                if (!SaveRegistry.BodyProviders.TryGetValue(property.Name, out IBodySaveProvider provider))
                {
                    SaveLogger.Warn("Skipping missing body save provider '" + property.Name + "'.");
                    continue;
                }

                RestoreProvider(property.Name, property.Value as JObject, delegate(JToken payload, int version)
                {
                    provider.Restore(body, payload, version, context);
                });
            }
        }

        private static void ApplyLimbProviders(Body body, JArray payloadRoot, SaveRestoreContext context)
        {
            if (payloadRoot == null || body.limbs == null)
            {
                return;
            }

            for (int i = 0; i < payloadRoot.Count; i++)
            {
                if (i >= body.limbs.Length)
                {
                    SaveLogger.Warn("Skipping saved limb payload at index " + i + " because that limb no longer exists.");
                    continue;
                }

                JObject limbObject = payloadRoot[i] as JObject;
                if (limbObject == null)
                {
                    continue;
                }

                WarnUnknownProviders(limbObject, SaveRegistry.LimbProviderKeys, "limb[" + i + "]");

                foreach (JProperty property in limbObject.Properties())
                {
                    if (!SaveRegistry.LimbProviders.TryGetValue(property.Name, out ILimbSaveProvider provider))
                    {
                        SaveLogger.Warn("Skipping missing limb save provider '" + property.Name + "' for limb index " + i + ".");
                        continue;
                    }

                    int limbIndex = i;
                    Limb limb = body.limbs[i];
                    RestoreProvider(property.Name, property.Value as JObject, delegate(JToken payload, int version)
                    {
                        provider.Restore(limb, limbIndex, payload, version, context);
                    });
                }
            }
        }

        private static void ApplyItemProviders(Body body, JObject payloadRoot, SaveRestoreContext context)
        {
            if (payloadRoot == null)
            {
                return;
            }

            Dictionary<string, Item> itemLookup = new Dictionary<string, Item>(StringComparer.Ordinal);
            foreach (SaveItemEntry entry in BuildItemEntries(body))
            {
                itemLookup[entry.Key] = entry.Item;
            }

            foreach (JProperty itemProperty in payloadRoot.Properties())
            {
                if (!itemLookup.TryGetValue(itemProperty.Name, out Item item) || item == null)
                {
                    SaveLogger.Warn("Skipping saved item payload for missing item key '" + itemProperty.Name + "'.");
                    continue;
                }

                JObject itemObject = itemProperty.Value as JObject;
                if (itemObject == null)
                {
                    continue;
                }

                WarnUnknownProviders(itemObject, SaveRegistry.ItemProviderKeys, "item[" + itemProperty.Name + "]");

                foreach (JProperty providerProperty in itemObject.Properties())
                {
                    if (!SaveRegistry.ItemProviders.TryGetValue(providerProperty.Name, out IItemSaveProvider provider))
                    {
                        SaveLogger.Warn("Skipping missing item save provider '" + providerProperty.Name + "' for item key '" + itemProperty.Name + "'.");
                        continue;
                    }

                    string itemKey = itemProperty.Name;
                    RestoreProvider(providerProperty.Name, providerProperty.Value as JObject, delegate(JToken payload, int version)
                    {
                        provider.Restore(item, itemKey, payload, version, context);
                    });
                }
            }
        }

        private static void ApplyWorldProviders(JObject payloadRoot, SaveRestoreContext context)
        {
            if (payloadRoot == null)
            {
                return;
            }

            WorldSaveContext worldContext = new WorldSaveContext();

            foreach (JProperty property in payloadRoot.Properties())
            {
                if (!SaveRegistry.WorldProviders.TryGetValue(property.Name, out IWorldSaveProvider provider))
                {
                    SaveLogger.Warn("Skipping missing world save provider '" + property.Name + "'.");
                    continue;
                }

                RestoreProvider(property.Name, property.Value as JObject, delegate(JToken payload, int version)
                {
                    provider.Restore(worldContext, payload, version, context);
                });
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
                int version = wrappedPayload.Value<int?>(VersionKey) ?? 0;
                JToken payload = wrappedPayload[PayloadKey];
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
            if (provider is ICustomSaveProvider custom)
            {
                return Math.Max(0, custom.GetVersion());
            }

            if (provider is IItemSaveProvider item)
            {
                return Math.Max(0, item.GetVersion());
            }

            if (provider is IBodySaveProvider body)
            {
                return Math.Max(0, body.GetVersion());
            }

            if (provider is ILimbSaveProvider limb)
            {
                return Math.Max(0, limb.GetVersion());
            }

            if (provider is IWorldSaveProvider world)
            {
                return Math.Max(0, world.GetVersion());
            }

            return 0;
        }

        private static List<SaveItemEntry> BuildItemEntries(Body body)
        {
            List<SaveItemEntry> entries = new List<SaveItemEntry>();
            if (body == null)
            {
                return entries;
            }

            // Item keys -> IDs for slot/wearables/containers/content.
            if (body.slots != null)
            {
                for (int i = 0; i < body.slots.Length; i++)
                {
                    if (!body.HoldingItem(i))
                    {
                        continue;
                    }

                    Item heldItem = body.GetItem(i);
                    if (heldItem == null)
                    {
                        continue;
                    }

                    string key = "slot:" + i;
                    entries.Add(new SaveItemEntry { Key = key, Item = heldItem });
                    AppendContainerEntries(entries, heldItem, key);
                }
            }

            foreach (Item wearable in body.GetAllWearables())
            {
                if (wearable == null || wearable.Stats == null)
                {
                    continue;
                }

                string wearSlot = string.IsNullOrWhiteSpace(wearable.Stats.wearSlotId) ? "unknown" : wearable.Stats.wearSlotId;
                string key = "wear:" + wearSlot;
                entries.Add(new SaveItemEntry { Key = key, Item = wearable });
                AppendContainerEntries(entries, wearable, key);
            }

            return entries;
        }

        private static void AppendContainerEntries(List<SaveItemEntry> entries, Item parentItem, string parentKey)
        {
            if (entries == null || parentItem == null || !parentItem.GetComponent<Container>())
            {
                return;
            }

            // Nested container items must inherit the parent key
            int nestedIndex = 0;
            foreach (Transform child in parentItem.transform)
            {
                if (child == null || !child.TryGetComponent<Item>(out Item nestedItem))
                {
                    continue;
                }

                string key = parentKey + "/container:" + nestedIndex;
                entries.Add(new SaveItemEntry { Key = key, Item = nestedItem });
                nestedIndex++;
            }
        }

        private static void WarnUnknownProviders(JObject payloadRoot, IEnumerable<string> knownKeys, string scope)
        {
            if (payloadRoot == null)
            {
                return;
            }

            HashSet<string> known = new HashSet<string>(knownKeys, StringComparer.Ordinal);
            foreach (JProperty property in payloadRoot.Properties())
            {
                if (!known.Contains(property.Name))
                {
                    SaveLogger.Warn("Skipping unknown " + scope + " save provider '" + property.Name + "'.");
                }
            }
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
}
