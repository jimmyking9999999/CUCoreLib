using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Registries
{
    public static class WorldSettingsRegistry
    {
        internal sealed class CategoryEntry
        {
            internal string Name;
            internal WorldSettingCategory? Anchor; // Null defaults to the top
            internal bool Below;
        }

        internal static readonly List<WorldSettingDefinition> RegisteredSettings = new List<WorldSettingDefinition>();
        internal static readonly List<CategoryEntry> CustomCategories = new List<CategoryEntry>();
        internal static readonly Dictionary<string, object> Values = new Dictionary<string, object>(StringComparer.Ordinal);
        internal static readonly List<Action> Refreshers = new List<Action>();

        private static readonly Dictionary<string, WorldSettingDefinition> SettingsById =
            new Dictionary<string, WorldSettingDefinition>(StringComparer.Ordinal);

        public static IReadOnlyList<WorldSettingDefinition> Settings => RegisteredSettings;

        public static bool Register(WorldSettingDefinition setting)
        {
            ContentReloadSession.AssertNotActive("WorldSettingsRegistry.Register()",
                "World settings are excluded from strict content reload.");

            var error = Validate(setting);
            if (!string.IsNullOrWhiteSpace(error))
            {
                CUCoreLibPlugin.Log?.LogError($"World setting registration failed :( {error}");
                return false;
            }

            if (SettingsById.ContainsKey(setting.Id))
            {
                CUCoreLibPlugin.Log?.LogError($"Ignored duplicate world setting '{setting.Id}'.");
                return false;
            }

            if (setting.UsesCustomCategory) GetOrAddCategory(setting.CustomCategory);
            SettingsById[setting.Id] = setting;
            RegisteredSettings.Add(setting);
            RegisterLocale(setting);
            return true;
        }

        // (category "other")
        // worldset<id>, worldset<id>dsc, worldset<id><choiceKey>, worldsetcat<category>
        private static void RegisterLocale(WorldSettingDefinition setting)
        {
            LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, NameKey(setting), setting.Name);
            if (!string.IsNullOrWhiteSpace(setting.Description))
                LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, DescriptionKey(setting), setting.Description);
            if (setting.UsesCustomCategory)
                LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, CategoryKey(setting.CustomCategory), setting.CustomCategory);
            if (setting.Choices == null) return;
            foreach (var choice in setting.Choices)
                LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, ChoiceKey(setting, choice), choice.Label);
        }

        internal static string NameKey(WorldSettingDefinition setting) => "worldset" + setting.Id;
        internal static string DescriptionKey(WorldSettingDefinition setting) => "worldset" + setting.Id + "dsc";
        internal static string ChoiceKey(WorldSettingDefinition setting, ModDropdownChoice choice) => "worldset" + setting.Id + choice.Key;
        internal static string CategoryKey(string category) => "worldsetcat" + category;

        internal static string Localize(string key, string fallback) =>
            string.IsNullOrWhiteSpace(fallback) ? fallback : LocaleRegistry.Get("other", key, fallback);

        public static void PlaceCategory(string category, WorldSettingCategory nextTo, bool below = false)
        {
            if (string.IsNullOrWhiteSpace(category)) return;
            var entry = GetOrAddCategory(category.Trim());
            entry.Anchor = nextTo;
            entry.Below = below;
        }

        private static CategoryEntry GetOrAddCategory(string name)
        {
            var entry = CustomCategories.FirstOrDefault(c => c.Name == name);
            if (entry != null) return entry;
            entry = new CategoryEntry { Name = name };
            CustomCategories.Add(entry);
            return entry;
        }

        private static string Validate(WorldSettingDefinition setting)
        {
            if (setting == null) return "Setting is null.";
            if (string.IsNullOrWhiteSpace(setting.Id) || !setting.Id.Contains("."))
                return $"'{setting.Id}' needs a namespaced ID like 'mymod.something'.";
            switch (setting.Kind)
            {
                case WorldSettingKind.Float:
                case WorldSettingKind.Int:
                    if (setting.Min > setting.Max) return $"'{setting.Id}' has min above max.";
                    break;
                case WorldSettingKind.Dropdown:
                    if (setting.Choices == null || setting.Choices.Length == 0)
                        return $"'{setting.Id}' needs at least one dropdown choice.";
                    if (setting.Choices.Any(c => c == null) ||
                        setting.Choices.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() != setting.Choices.Length)
                        return $"'{setting.Id}' has null or duplicate dropdown choice keys.";
                    var index = (int)setting.DefaultValue;
                    if (index < 0 || index >= setting.Choices.Length)
                        return $"'{setting.Id}' default index {index} is out of range.";
                    break;
            }
            if (setting.PresetDefaults.Count > 0 && !setting.Persists)
                return $"'{setting.Id}' is a button and can't have preset defaults.";
            foreach (var preset in setting.PresetDefaults)
            {
                try
                {
                    Coerce(setting, preset.Value);
                }
                catch (Exception)
                {
                    return $"'{setting.Id}' preset default for '{preset.Key}' ({preset.Value}) doesn't fit a {setting.Kind} setting.";
                }
            }
            return null;
        }

        public static object Get(string id)
        {
            if (id != null && Values.TryGetValue(id, out object value)) return value;
            return Find(id)?.DefaultValue;
        }

        public static T Get<T>(string id, T fallback = default)
        {
            object value = Get(id);
            if (value == null) return fallback;
            if (value is T typed) return typed;
            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static bool IsSet(string id) => id != null && Values.ContainsKey(id);

        public static void Set(string id, object value)
        {
            var setting = Find(id);
            if (setting == null || !setting.Persists) return;

            if (value == null) Values.Remove(id);
            else Values[id] = Coerce(setting, value);
            InvokeApply(setting);
        }

        public static void ResetToDefaults()
        {
            Values.Clear();
            ApplyAll();
        }

        // Mirrors PreRunScript.ApplyPreset: everything resets, then the preset's own defaults apply.
        // They are stored as values, so they save with the run and ride along in preset codes.
        internal static void ApplyPresetDefaults(string presetName)
        {
            Values.Clear();
            if (!string.IsNullOrWhiteSpace(presetName))
            {
                foreach (var setting in RegisteredSettings)
                {
                    if (setting.PresetDefaults.TryGetValue(presetName, out var value))
                        Values[setting.Id] = Coerce(setting, value);
                }
            }
            ApplyAll();
        }

        public static void RefreshDisplays()
        {
            foreach (Action refresh in Refreshers)
            {
                try { refresh(); }
                catch (Exception ex) { CUCoreLibPlugin.Log?.LogWarning($"[WorldSettings] Display refresh failed: {ex.Message}"); }
            }
        }

        private static WorldSettingDefinition Find(string id) =>
            id != null && SettingsById.TryGetValue(id, out var setting) ? setting : null;

        private static void ApplyAll()
        {
            foreach (var setting in RegisteredSettings) InvokeApply(setting);
        }

        private static void InvokeApply(WorldSettingDefinition setting)
        {
            if (setting.Apply == null) return;
            try
            {
                setting.Apply(Get(setting.Id));
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogError($"[WorldSettings] Apply for '{setting.Id}' failed: {ex}");
            }
        }
        private static object Coerce(WorldSettingDefinition setting, object value)
        {
            var culture = CultureInfo.InvariantCulture;
            switch (setting.Kind)
            {
                case WorldSettingKind.Bool:
                    return Convert.ToBoolean(value, culture);
                case WorldSettingKind.Float:
                    return Mathf.Clamp(Convert.ToSingle(value, culture), setting.Min, setting.Max);
                case WorldSettingKind.Int:
                    return Mathf.Clamp(Convert.ToInt32(value, culture), Mathf.RoundToInt(setting.Min), Mathf.RoundToInt(setting.Max));
                case WorldSettingKind.Dropdown:
                    return Mathf.Clamp(Convert.ToInt32(value, culture), 0, setting.Choices.Length - 1);
                default:
                    return Convert.ToString(value, culture);
            }
        }

        internal static bool HasPersistedSettings => RegisteredSettings.Any(s => s.Persists);

        internal static JObject WriteValues()
        {
            var obj = new JObject();
            foreach (var setting in RegisteredSettings)
            {
                if (!setting.Persists || !Values.TryGetValue(setting.Id, out object value) || value == null) continue;
                obj[setting.Id] = JToken.FromObject(value);
            }
            return obj;
        }

        internal static void ReadValues(JObject obj)
        {
            Values.Clear();
            if (obj != null)
            {
                foreach (var setting in RegisteredSettings)
                {
                    if (!setting.Persists) continue;
                    JToken token = obj[setting.Id];
                    if (token == null || token.Type == JTokenType.Null) continue;
                    try
                    {
                        Values[setting.Id] = Coerce(setting, token.ToObject<object>());
                    }
                    catch (Exception ex)
                    {
                        CUCoreLibPlugin.Log?.LogWarning($"Ignoring saved '{setting.Id}': {ex.Message}");
                    }
                }
            }
            ApplyAll();
        }
    }
}
