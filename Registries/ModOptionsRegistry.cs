using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Registries;

public static class ModOptionsRegistry
{
    private const int CustomCategoryBaseIndex = 100;
    internal static readonly List<ModOptionDefinition> RegisteredOptions = [];
    private static readonly HashSet<string> RegisteredIds = new(StringComparer.Ordinal);
    private static readonly List<ModOptionCategoryEntry> CustomCategories = [];

    private static readonly Dictionary<string, ModOptionCategoryEntry> CustomCategoriesByKey =
        new(StringComparer.Ordinal);

    public static bool Register(ModOptionDefinition option)
    {
        ContentReloadSession.AssertNotActive("ModOptionsRegistry.Register()",
            "Mod options are excluded from strict content reload.");

        var error = Validate(option);
        if (!string.IsNullOrWhiteSpace(error))
        {
            CUCoreLibPlugin.Log?.LogError($"Mod option registration failed :( {error}");
            return false;
        }

        if (!RegisteredIds.Add(option.Id))
        {
            CUCoreLibPlugin.Log?.LogError($"Ignored duplicate mod option '{option.Id}'.");
            return false;
        }

        ResolveCategory(option);
        RegisteredOptions.Add(option);
        RegisterLocale(option);
        MergeIntoLoadedSettings(option);
        SettingsMenuCategoryExtender.RefreshLiveMenu();
        return true;
    }

    private static string CategoryString(Setting.SettingCategory category) => category.ToString().ToLowerInvariant();

    private static string MakeId(string space, Setting.SettingCategory category, string key) =>
        $"{space}.{CategoryString(category)}.{key}";

    private static string MakeId(string space, string customCategory, string key) =>
        $"{space}.{customCategory.ToLowerInvariant()}.{key}";

    private static string LabelText(string space, Setting.SettingCategory category, string key) =>
        MakeId(space, category, key);

    private static string LabelText(string space, string customCategory, string key) => MakeId(space, customCategory, key);

    private static string DescriptionText(string space, Setting.SettingCategory category, string key) =>
        $"{MakeId(space, category, key)}dsc";

    private static string DescriptionText(string space, string customCategory, string key) =>
        $"{MakeId(space, customCategory, key)}dsc";

    public static bool RegisterFloat(string ns, string key, Setting.SettingCategory category,
        float defaultValue, float min, float max, Action<float> apply = null, Func<float, string> formatValue = null)
    {
        return Register(ModOptionDefinition.Float(
            MakeId(ns, category, key), LabelText(ns, category, key), DescriptionText(ns, category, key),
            category, defaultValue, min, max, apply, formatValue));
    }

    public static bool RegisterInt(string ns, string key, Setting.SettingCategory category,
        int defaultValue, int min, int max, Action<int> apply = null)
    {
        return Register(ModOptionDefinition.Int(
            MakeId(ns, category, key), LabelText(ns, category, key), DescriptionText(ns, category, key),
            category, defaultValue, min, max, apply));
    }

    public static bool RegisterBool(string ns, string key, Setting.SettingCategory category,
        bool defaultValue, Action<bool> apply = null)
    {
        return Register(ModOptionDefinition.Bool(
            MakeId(ns, category, key), LabelText(ns, category, key), DescriptionText(ns, category, key),
            category, defaultValue, apply));
    }

    public static bool RegisterDropdown(string ns, string key, Setting.SettingCategory category,
        int defaultValue, ModDropdownChoice[] choices, Action<int> apply = null)
    {
        return Register(ModOptionDefinition.Dropdown(
            MakeId(ns, category, key), LabelText(ns, category, key), DescriptionText(ns, category, key),
            category, defaultValue, choices, apply));
    }

    public static bool RegisterKeybind(string ns, string key, Setting.SettingCategory category,
        KeyCode defaultValue, Action<KeyCode> apply = null)
    {
        return Register(ModOptionDefinition.Keybind(
            MakeId(ns, category, key), LabelText(ns, category, key), DescriptionText(ns, category, key),
            category, defaultValue, apply));
    }

    public static bool RegisterFloat(string ns, string key, string customCategory,
        float defaultValue, float min, float max, Action<float> apply = null, Func<float, string> formatValue = null)
    {
        return Register(ModOptionDefinition.Float(
            MakeId(ns, customCategory, key), LabelText(ns, customCategory, key),
            DescriptionText(ns, customCategory, key),
            customCategory, defaultValue, min, max, apply, formatValue));
    }

    public static bool RegisterInt(string ns, string key, string customCategory,
        int defaultValue, int min, int max, Action<int> apply = null)
    {
        return Register(ModOptionDefinition.Int(
            MakeId(ns, customCategory, key), LabelText(ns, customCategory, key),
            DescriptionText(ns, customCategory, key),
            customCategory, defaultValue, min, max, apply));
    }

    public static bool RegisterBool(string ns, string key, string customCategory,
        bool defaultValue, Action<bool> apply = null)
    {
        return Register(ModOptionDefinition.Bool(
            MakeId(ns, customCategory, key), LabelText(ns, customCategory, key),
            DescriptionText(ns, customCategory, key),
            customCategory, defaultValue, apply));
    }

    public static bool RegisterDropdown(string ns, string key, string customCategory,
        int defaultValue, ModDropdownChoice[] choices, Action<int> apply = null)
    {
        return Register(ModOptionDefinition.Dropdown(
            MakeId(ns, customCategory, key), LabelText(ns, customCategory, key),
            DescriptionText(ns, customCategory, key),
            customCategory, defaultValue, choices, apply));
    }

    public static bool RegisterKeybind(string ns, string key, string customCategory,
        KeyCode defaultValue, Action<KeyCode> apply = null)
    {
        return Register(ModOptionDefinition.Keybind(
            MakeId(ns, customCategory, key), LabelText(ns, customCategory, key),
            DescriptionText(ns, customCategory, key),
            customCategory, defaultValue, apply));
    }
    
    internal static void AppendRegisteredOptions(List<Setting> settings)
    {
        if (settings == null) return;

        foreach (var option in from option in RegisteredOptions
                 let option1 = option
                 where option != null && !settings.Any(setting => setting != null && setting.name == option1.Id)
                 select option)
            settings.Add(option.CreateSetting());
    }

    internal static List<ModOptionCategoryEntry> GetCustomCategories()
    {
        return CustomCategories.ToList();
    }

    private static void MergeIntoLoadedSettings(ModOptionDefinition option)
    {
        if (Settings.settings == null ||
            Settings.settings.Any(setting => setting != null && setting.name == option.Id)) return;

        var createdSetting = option.CreateSetting();
        Settings.settings.Add(createdSetting);
        createdSetting.Apply();
    }

    private static void RegisterLocale(ModOptionDefinition option)
    {
        LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Option, option.Id, option.Label);
        if (!string.IsNullOrWhiteSpace(option.Description))
            LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Option, option.Id + "dsc",
                option.Description);
        if (option.Kind != ModOptionKind.Dropdown || option.Choices == null) return;

        foreach (var choice in option.Choices)
            LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Option, option.Id + choice.Key,
                choice.Label);
    }

    internal static JObject CaptureNetworkSnapshot()
    {
        var root = new JObject();
        foreach (var option in RegisteredOptions)
        {
            if (option == null) continue;

            var value = CaptureOptionValue(option);
            if (value == null) continue;

            root[option.Id] = new JObject
            {
                ["kind"] = option.Kind.ToString(),
                ["value"] = value is string v
                    ? new JValue(v)
                    : JToken.FromObject(value)
            };
        }

        return root;
    }

    internal static void ApplyNetworkSnapshot(JObject snapshot)
    {
        if (snapshot == null) return;

        foreach (var property in snapshot.Properties())
        {
            var option = RegisteredOptions.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.Id, property.Name, StringComparison.Ordinal));
            if (option == null) continue;

            ApplyOptionValue(option, property.Value as JObject);
        }
    }

    private static string Validate(ModOptionDefinition option)
    {
        if (option == null) return "definition was null.";

        if (string.IsNullOrWhiteSpace(option.Id)) return "definition ID was empty.";

        if (option.Id != option.Id.Trim()) return $"option ID '{option.Id}' cannot begin or end with whitespace.";

        if (option.Id.IndexOf('.') < 1 || option.Id.EndsWith(".", StringComparison.Ordinal))
            return
                $"option '{option.Id}' must use a namespaced ID like 'modid.setting'. Might be annoying, but needed.";

        if (string.IsNullOrWhiteSpace(option.Label)) return $"option '{option.Id}' must have a label.";

        if (option.UsesCustomCategory && string.IsNullOrWhiteSpace(option.CustomCategory))
            return $"option '{option.Id}' custom category was empty.";

        if (option.Kind is ModOptionKind.Float or ModOptionKind.Int && option.Min > option.Max)
            return $"option '{option.Id}' has min > max.";

        if (option.Kind != ModOptionKind.Dropdown) return null;

        if (option.Choices == null || option.Choices.Length == 0)
            return $"dropdown option '{option.Id}' must have at least one choice.";

        var choiceKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < option.Choices.Length; i++)
        {
            var choice = option.Choices[i];
            if (choice == null || string.IsNullOrWhiteSpace(choice.Key) || string.IsNullOrWhiteSpace(choice.Label))
                return $"dropdown option '{option.Id}' has an invalid choice at index {i}.";

            if (!choiceKeys.Add(choice.Key))
                return $"dropdown option '{option.Id}' has duplicate choice key '{choice.Key}'.";
        }

        if (option.IntDefault < 0 || option.IntDefault >= option.Choices.Length)
            return $"dropdown option '{option.Id}' default index is outside the choice range.";

        return null;
    }

    private static object CaptureOptionValue(ModOptionDefinition option)
    {
        switch (option.Kind)
        {
            case ModOptionKind.Float:
                var floatSetting = Settings.Get<SettingFloat>(option.Id);
                return floatSetting != null ? (object)floatSetting.value : option.FloatDefault;
            case ModOptionKind.Int:
                var intSetting = Settings.Get<SettingInt>(option.Id);
                return intSetting != null ? (object)intSetting.value : option.IntDefault;
            case ModOptionKind.Bool:
                var boolSetting = Settings.Get<SettingBool>(option.Id);
                return boolSetting != null ? (object)boolSetting.value : option.BoolDefault;
            case ModOptionKind.Dropdown:
                var dropdownSetting = Settings.Get<SettingDropdown>(option.Id);
                return dropdownSetting != null ? (object)dropdownSetting.value : option.IntDefault;
            case ModOptionKind.Keybind:
                var keybindSetting = Settings.Get<SettingKeybind>(option.Id);
                return keybindSetting != null
                    ? (object)keybindSetting.value.ToString()
                    : option.KeyDefault.ToString();
            default:
                return null;
        }
    }

    private static void ApplyOptionValue(ModOptionDefinition option, JObject payload)
    {
        var valueToken = payload?["value"];
        if (valueToken == null) return;

        switch (option.Kind)
        {
            case ModOptionKind.Float:
            {
                var setting = Settings.Get<SettingFloat>(option.Id);
                if (setting != null && valueToken.Type != JTokenType.Null)
                {
                    setting.value = valueToken.Value<float>();
                    setting.Apply();
                }

                break;
            }
            case ModOptionKind.Int:
            {
                var setting = Settings.Get<SettingInt>(option.Id);
                if (setting != null && valueToken.Type != JTokenType.Null)
                {
                    setting.value = valueToken.Value<int>();
                    setting.Apply();
                }

                break;
            }
            case ModOptionKind.Bool:
            {
                var setting = Settings.Get<SettingBool>(option.Id);
                if (setting != null && valueToken.Type != JTokenType.Null)
                {
                    setting.value = valueToken.Value<bool>();
                    setting.Apply();
                }

                break;
            }
            case ModOptionKind.Dropdown:
            {
                var setting = Settings.Get<SettingDropdown>(option.Id);
                if (setting != null && valueToken.Type != JTokenType.Null)
                {
                    setting.value = valueToken.Value<int>();
                    setting.Apply();
                }

                break;
            }
            case ModOptionKind.Keybind:
            {
                var setting = Settings.Get<SettingKeybind>(option.Id);
                if (setting != null && valueToken.Type != JTokenType.Null &&
                    Enum.TryParse(valueToken.Value<string>(), out KeyCode keyCode))
                {
                    setting.value = keyCode;
                    setting.Apply();
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ResolveCategory(ModOptionDefinition option)
    {
        if (!option.UsesCustomCategory) return;

        var normalizedKey = NormalizeCategoryKey(option.CustomCategory);
        if (!CustomCategoriesByKey.TryGetValue(normalizedKey, out var entry))
        {
            entry = new ModOptionCategoryEntry(option.CustomCategory.Trim(),
                CustomCategoryBaseIndex + CustomCategories.Count);
            CustomCategories.Add(entry);
            CustomCategoriesByKey.Add(normalizedKey, entry);
        }

        option.SetResolvedCategory((Setting.SettingCategory)entry.CategoryIndex);
    }

    private static string NormalizeCategoryKey(string category)
    {
        return (category ?? string.Empty).Trim().ToLowerInvariant();
    }
}

internal sealed class ModOptionCategoryEntry(string displayName, int categoryIndex)
{
    public string DisplayName { get; } = displayName;
    public int CategoryIndex { get; } = categoryIndex;
}