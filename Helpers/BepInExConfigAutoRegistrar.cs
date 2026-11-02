using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using Newtonsoft.Json;
using UnityEngine;

namespace CUCoreLib.Helpers
{
    internal static class BepInExConfigAutoRegistrar
    {
        private static bool hasRun;
        private static readonly string CachePath = Path.Combine(Application.persistentDataPath, "cucorelib_config_cache.json");

        internal static void RunOnce()
        {
            // Disabled — crashes some end users. Re-enable when stable.
            return;

            if (hasRun) return;
            hasRun = true;

            try
            {
                if (Chainloader.PluginInfos == null || Chainloader.PluginInfos.Count == 0) return;

                if (TryLoadFromCache()) return;

                var totalRegistered = 0;
                foreach (var kvp in Chainloader.PluginInfos)
                {
                    var pluginInfo = kvp.Value;
                    if (pluginInfo?.Instance == null) continue;

                    var plugin = pluginInfo.Instance;
                    var configFile = plugin.Config;
                    if (configFile == null) continue;

                    var modGuid = pluginInfo.Metadata?.GUID;
                    if (string.IsNullOrWhiteSpace(modGuid)) continue;

                    var modName = pluginInfo.Metadata?.Name ?? modGuid;
                    var registered = ProcessConfigFile(configFile, modGuid, modName);
                    totalRegistered += registered;
                }

                if (totalRegistered > 0)
                    CUCoreLibPlugin.Log?.LogInfo($"Auto-registered {totalRegistered} mod config settings.");

                SaveToCache();
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogError($"BepInEx config auto-registrar failed: {ex.Message}");
            }
        }

        private static bool TryLoadFromCache()
        {
            try
            {
                if (!File.Exists(CachePath)) return false;

                var json = File.ReadAllText(CachePath);
                var cacheData = JsonConvert.DeserializeObject<CacheData>(json);
                if (cacheData == null || cacheData.Entries == null) return false;

                if (cacheData.PluginCount != Chainloader.PluginInfos.Count) return false;

                foreach (var entry in cacheData.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.ModGuid) || string.IsNullOrWhiteSpace(entry.Key)) continue;

                    var configFile = GetConfigFileForMod(entry.ModGuid);
                    if (configFile == null) continue;

                    var configEntry = FindConfigEntry(configFile, entry.Section, entry.Key);
                    if (configEntry == null) continue;

                    var option = CreateOptionFromEntry(entry.ModGuid, configEntry);
                    if (option != null) ModOptionsRegistry.Register(option);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveToCache()
        {
            try
            {
                var cacheData = new CacheData
                {
                    PluginCount = Chainloader.PluginInfos.Count,
                    Entries = new List<CachedEntry>()
                };

                foreach (var option in ModOptionsRegistry.RegisteredOptions)
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.CustomCategory)) continue;

                    cacheData.Entries.Add(new CachedEntry
                    {
                        ModGuid = option.CustomCategory,
                        Section = option.Id.Contains(".") ? option.Id.Substring(0, option.Id.IndexOf('.')) : "",
                        Key = option.Id.Contains(".") ? option.Id.Substring(option.Id.IndexOf('.') + 1) : option.Id
                    });
                }

                var json = JsonConvert.SerializeObject(cacheData, Formatting.Indented);
                File.WriteAllText(CachePath, json);
            }
            catch
            {
            }
        }

        private static ConfigFile GetConfigFileForMod(string modGuid)
        {
            foreach (var kvp in Chainloader.PluginInfos)
            {
                if (kvp.Value?.Metadata?.GUID == modGuid && kvp.Value.Instance != null)
                    return kvp.Value.Instance.Config;
            }
            return null;
        }

        private static ConfigEntryBase FindConfigEntry(ConfigFile configFile, string section, string key)
        {
            foreach (var kvp in (IDictionary<ConfigDefinition, ConfigEntryBase>)configFile)
            {
                if (kvp.Key.Section == section && kvp.Key.Key == key)
                    return kvp.Value;
            }
            return null;
        }

        [Serializable]
        private class CacheData
        {
            public int PluginCount;
            public List<CachedEntry> Entries;
        }

        [Serializable]
        private class CachedEntry
        {
            public string ModGuid;
            public string Section;
            public string Key;
        }

        private static int ProcessConfigFile(ConfigFile configFile, string modGuid, string modName)
        {
            var entries = ((IDictionary<ConfigDefinition, ConfigEntryBase>)configFile).Values.ToList();
            if (entries.Count == 0) return 0;

            var unregisteredEntries = FilterExistingEntries(entries);
            if (unregisteredEntries.Count <= 1) return 0;

            if (ModOptionsRegistry.RegisteredOptions.Any(opt =>
                opt != null && !string.IsNullOrWhiteSpace(opt.CustomCategory) &&
                opt.CustomCategory.Equals(modGuid, StringComparison.OrdinalIgnoreCase)))
                return 0;

            return RegisterEntriesAsCustomTab(modGuid, modName, unregisteredEntries);
        }

        private static List<ConfigEntryBase> FilterExistingEntries(List<ConfigEntryBase> entries)
        {
            var filtered = new List<ConfigEntryBase>();
            foreach (var entry in entries)
            {
                if (entry?.Definition == null) continue;

                var fullKey = entry.Definition.Section + "." + entry.Definition.Key;
                var suffixKey = entry.Definition.Key;

                var alreadyRegistered = ModOptionsRegistry.RegisteredOptions.Any(opt =>
                {
                    if (opt == null || string.IsNullOrWhiteSpace(opt.Id)) return false;
                    if (opt.Id.Equals(fullKey, StringComparison.OrdinalIgnoreCase)) return true;
                    var optSuffix = opt.Id.Contains(".") ? opt.Id.Substring(opt.Id.LastIndexOf('.') + 1) : opt.Id;
                    return optSuffix.Equals(suffixKey, StringComparison.OrdinalIgnoreCase);
                });

                if (!alreadyRegistered) filtered.Add(entry);
            }

            return filtered;
        }

        private static int RegisterEntriesAsCustomTab(string modGuid, string modName,
            List<ConfigEntryBase> entries)
        {
            var registered = 0;
            foreach (var entry in entries)
            {
                var option = CreateOptionFromEntry(modGuid, entry);
                if (option != null && ModOptionsRegistry.Register(option))
                    registered++;
            }
            return registered;
        }

        private static ModOptionDefinition CreateOptionFromEntry(string modGuid, ConfigEntryBase entry)
        {
            var settingType = entry.SettingType;
            var id = modGuid + "." + entry.Definition.Key;
            var label = entry.Definition.Key;
            var description = entry.Description?.Description ?? string.Empty;
            var category = modGuid;

            if (settingType == typeof(bool))
            {
                var defaultValue = (bool)entry.DefaultValue;
                return ModOptionDefinition.Bool(id, label, description, category, defaultValue,
                    value => entry.BoxedValue = value);
            }

            if (settingType == typeof(int))
            {
                var defaultValue = (int)entry.DefaultValue;
                var bound = defaultValue == 0 ? 10 : Math.Abs(defaultValue) * 10;
                var min = -bound;
                var max = bound;
                ExtractIntRange(entry, ref min, ref max);
                return ModOptionDefinition.Int(id, label, description, category, defaultValue, min, max,
                    value => entry.BoxedValue = value);
            }

            if (settingType == typeof(float))
            {
                var defaultValue = (float)entry.DefaultValue;
                var bound = Mathf.Approximately(defaultValue, 0f) ? 10f : Mathf.Abs(defaultValue) * 10f;
                var min = -bound;
                var max = bound;
                ExtractFloatRange(entry, ref min, ref max);
                return ModOptionDefinition.Float(id, label, description, category, defaultValue, min, max,
                    value => entry.BoxedValue = value);
            }

            if (settingType == typeof(string))
            {
                var defaultValue = entry.DefaultValue?.ToString() ?? string.Empty;
                return ModOptionDefinition.Text(id, label, description, category, defaultValue,
                    value => entry.BoxedValue = value);
            }

            if (settingType == typeof(KeyCode))
            {
                var defaultValue = entry.DefaultValue is KeyCode kc ? kc : KeyCode.None;
                return ModOptionDefinition.Keybind(id, label, description, category, defaultValue,
                    value => entry.BoxedValue = value);
            }

            if (settingType.IsEnum)
            {
                var enumValues = Enum.GetValues(settingType);
                if (enumValues.Length == 0) return null;

                var choices = new ModDropdownChoice[enumValues.Length];
                for (var i = 0; i < enumValues.Length; i++)
                {
                    var enumValue = enumValues.GetValue(i);
                    var enumName = enumValue?.ToString() ?? i.ToString();
                    choices[i] = new ModDropdownChoice(enumName, enumName);
                }

                var defaultIndex = 0;
                if (entry.DefaultValue != null)
                {
                    for (var i = 0; i < enumValues.Length; i++)
                    {
                        if (Equals(enumValues.GetValue(i), entry.DefaultValue))
                        {
                            defaultIndex = i;
                            break;
                        }
                    }
                }

                return ModOptionDefinition.Dropdown(id, label, description, category, defaultIndex, choices,
                    value =>
                    {
                        if (value >= 0 && value < enumValues.Length)
                            entry.BoxedValue = enumValues.GetValue(value);
                    });
            }

            return null;
        }

        private static void ExtractIntRange(ConfigEntryBase entry, ref int min, ref int max)
        {
            try
            {
                var acceptableValue = entry.GetType().GetProperty("AcceptableValue")?.GetValue(entry);
                if (acceptableValue == null) return;

                var rangeType = acceptableValue.GetType();
                if (!rangeType.IsGenericType) return;

                var minProp = rangeType.GetProperty("MinValue");
                var maxProp = rangeType.GetProperty("MaxValue");
                if (minProp == null || maxProp == null) return;

                var minVal = minProp.GetValue(acceptableValue);
                var maxVal = maxProp.GetValue(acceptableValue);
                if (minVal is int minInt) min = minInt;
                if (maxVal is int maxInt) max = maxInt;
            }
            catch
            {
            }
        }

        private static void ExtractFloatRange(ConfigEntryBase entry, ref float min, ref float max)
        {
            try
            {
                var acceptableValue = entry.GetType().GetProperty("AcceptableValue")?.GetValue(entry);
                if (acceptableValue == null) return;

                var rangeType = acceptableValue.GetType();
                if (!rangeType.IsGenericType) return;

                var minProp = rangeType.GetProperty("MinValue");
                var maxProp = rangeType.GetProperty("MaxValue");
                if (minProp == null || maxProp == null) return;

                var minVal = minProp.GetValue(acceptableValue);
                var maxVal = maxProp.GetValue(acceptableValue);
                if (minVal is float minFloat) min = minFloat;
                if (maxVal is float maxFloat) max = maxFloat;
            }
            catch
            {
            }
        }
    }
}
