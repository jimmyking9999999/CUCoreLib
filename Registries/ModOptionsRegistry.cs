using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Registries
{
    public static class ModOptionsRegistry
    {
        private const int CustomCategoryBaseIndex = 100;
        internal static readonly List<ModOptionDefinition> RegisteredOptions = new List<ModOptionDefinition>();
        private static readonly HashSet<string> RegisteredIds = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<ModOptionCategoryEntry> CustomCategories = new List<ModOptionCategoryEntry>();
        private static readonly Dictionary<string, ModOptionCategoryEntry> CustomCategoriesByKey = new Dictionary<string, ModOptionCategoryEntry>(StringComparer.Ordinal);

        public static bool Register(ModOptionDefinition option)
        {
            ContentReloadSession.AssertNotActive("ModOptionsRegistry.Register()", "Mod options are excluded from strict content reload.");

            string error = Validate(option);
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

        internal static void AppendRegisteredOptions(List<Setting> settings)
        {
            if (settings == null)
            {
                return;
            }

            for (int i = 0; i < RegisteredOptions.Count; i++)
            {
                ModOptionDefinition option = RegisteredOptions[i];
                if (option == null || settings.Any(setting => setting != null && setting.name == option.Id))
                {
                    continue;
                }

                settings.Add(option.CreateSetting());
            }
        }

        internal static List<ModOptionCategoryEntry> GetCustomCategories()
        {
            return CustomCategories.ToList();
        }

        private static void MergeIntoLoadedSettings(ModOptionDefinition option)
        {
            if (Settings.settings == null || Settings.settings.Any(setting => setting != null && setting.name == option.Id))
            {
                return;
            }

            Setting createdSetting = option.CreateSetting();
            Settings.settings.Add(createdSetting);
            createdSetting.Apply();
        }

        private static void RegisterLocale(ModOptionDefinition option)
        {
            LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, "gameset" + option.Id, option.Label);
            if (!string.IsNullOrWhiteSpace(option.Description))
            {
                LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, "gameset" + option.Id + "dsc", option.Description);
                // todo I really need to figure this out
                // man this is kinda ass ngl
            }

            if (option.Kind != ModOptionKind.Dropdown || option.Choices == null)
            {
                return;
            }

            for (int i = 0; i < option.Choices.Length; i++)
            {
                ModDropdownChoice choice = option.Choices[i];
                LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, "gameset" + option.Id + choice.Key, choice.Label);
            }
        }

        internal static JObject CaptureNetworkSnapshot()
        {
            JObject root = new JObject();
            foreach (ModOptionDefinition option in RegisteredOptions)
            {
                if (option == null)
                {
                    continue;
                }

                object value = CaptureOptionValue(option);
                if (value == null)
                {
                    continue;
                }

                root[option.Id] = new JObject
                {
                    ["kind"] = option.Kind.ToString(),
                    ["value"] = value is string ? (JToken)new JValue((string)value) : JToken.FromObject(value)
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
                ModOptionDefinition option = RegisteredOptions.FirstOrDefault(entry => entry != null && string.Equals(entry.Id, property.Name, StringComparison.Ordinal));
                if (option == null)
                {
                    continue;
                }

                ApplyOptionValue(option, property.Value as JObject);
            }
        }

        private static string Validate(ModOptionDefinition option)
        {
            if (option == null)
            {
                return "definition was null.";
            }

            if (string.IsNullOrWhiteSpace(option.Id))
            {
                return "definition ID was empty.";
            }

            if (option.Id != option.Id.Trim())
            {
                return $"option ID '{option.Id}' cannot begin or end with whitespace.";
            }

            if (option.Id.IndexOf('.') < 1 || option.Id.EndsWith(".", StringComparison.Ordinal))
            {
                return $"option '{option.Id}' must use a namespaced ID like 'modid.setting'. Might be annoying, but needed.";
            }

            if (string.IsNullOrWhiteSpace(option.Label))
            {
                return $"option '{option.Id}' must have a label.";
            }

            if (option.UsesCustomCategory && string.IsNullOrWhiteSpace(option.CustomCategory))
            {
                return $"option '{option.Id}' custom category was empty.";
            }

            if ((option.Kind == ModOptionKind.Float || option.Kind == ModOptionKind.Int) && option.Min > option.Max)
            {
                return $"option '{option.Id}' has min > max.";
            }

            if (option.Kind != ModOptionKind.Dropdown)
            {
                return null;
            }

            if (option.Choices == null || option.Choices.Length == 0)
            {
                return $"dropdown option '{option.Id}' must have at least one choice.";
            }

            HashSet<string> choiceKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < option.Choices.Length; i++)
            {
                ModDropdownChoice choice = option.Choices[i];
                if (choice == null || string.IsNullOrWhiteSpace(choice.Key) || string.IsNullOrWhiteSpace(choice.Label))
                {
                    return $"dropdown option '{option.Id}' has an invalid choice at index {i}.";
                }

                if (!choiceKeys.Add(choice.Key))
                {
                    return $"dropdown option '{option.Id}' has duplicate choice key '{choice.Key}'.";
                }
            }

            if (option.IntDefault < 0 || option.IntDefault >= option.Choices.Length)
            {
                return $"dropdown option '{option.Id}' default index is outside the choice range.";
            }

            return null;
        }

        private static object CaptureOptionValue(ModOptionDefinition option)
        {
            switch (option.Kind)
            {
                case ModOptionKind.Float:
                    SettingFloat floatSetting = Settings.Get<SettingFloat>(option.Id);
                    return floatSetting != null ? (object)floatSetting.value : option.FloatDefault;
                case ModOptionKind.Int:
                    SettingInt intSetting = Settings.Get<SettingInt>(option.Id);
                    return intSetting != null ? (object)intSetting.value : option.IntDefault;
                case ModOptionKind.Bool:
                    SettingBool boolSetting = Settings.Get<SettingBool>(option.Id);
                    return boolSetting != null ? (object)boolSetting.value : option.BoolDefault;
                case ModOptionKind.Dropdown:
                    SettingDropdown dropdownSetting = Settings.Get<SettingDropdown>(option.Id);
                    return dropdownSetting != null ? (object)dropdownSetting.value : option.IntDefault;
                case ModOptionKind.Keybind:
                    SettingKeybind keybindSetting = Settings.Get<SettingKeybind>(option.Id);
                    return keybindSetting != null ? (object)keybindSetting.value.ToString() : option.KeyDefault.ToString();
                default:
                    return null;
            }
        }

        private static void ApplyOptionValue(ModOptionDefinition option, JObject payload)
        {
            if (payload == null)
            {
                return;
            }

            JToken valueToken = payload["value"];
            if (valueToken == null)
            {
                return;
            }

            switch (option.Kind)
            {
                case ModOptionKind.Float:
                {
                    SettingFloat setting = Settings.Get<SettingFloat>(option.Id);
                    if (setting != null && valueToken.Type != JTokenType.Null)
                    {
                        setting.value = valueToken.Value<float>();
                        setting.Apply();
                    }

                    break;
                }
                case ModOptionKind.Int:
                {
                    SettingInt setting = Settings.Get<SettingInt>(option.Id);
                    if (setting != null && valueToken.Type != JTokenType.Null)
                    {
                        setting.value = valueToken.Value<int>();
                        setting.Apply();
                    }

                    break;
                }
                case ModOptionKind.Bool:
                {
                    SettingBool setting = Settings.Get<SettingBool>(option.Id);
                    if (setting != null && valueToken.Type != JTokenType.Null)
                    {
                        setting.value = valueToken.Value<bool>();
                        setting.Apply();
                    }

                    break;
                }
                case ModOptionKind.Dropdown:
                {
                    SettingDropdown setting = Settings.Get<SettingDropdown>(option.Id);
                    if (setting != null && valueToken.Type != JTokenType.Null)
                    {
                        setting.value = valueToken.Value<int>();
                        setting.Apply();
                    }

                    break;
                }
                case ModOptionKind.Keybind:
                {
                    SettingKeybind setting = Settings.Get<SettingKeybind>(option.Id);
                    if (setting != null && valueToken.Type != JTokenType.Null && Enum.TryParse(valueToken.Value<string>(), out KeyCode keyCode))
                    {
                        setting.value = keyCode;
                        setting.Apply();
                    }

                    break;
                }
            }
        }

        private static void ResolveCategory(ModOptionDefinition option)
        {
            if (!option.UsesCustomCategory)
            {
                return;
            }

            string normalizedKey = NormalizeCategoryKey(option.CustomCategory);
            if (!CustomCategoriesByKey.TryGetValue(normalizedKey, out ModOptionCategoryEntry entry))
            {
                entry = new ModOptionCategoryEntry(option.CustomCategory.Trim(), CustomCategoryBaseIndex + CustomCategories.Count);
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

    internal sealed class ModOptionCategoryEntry
    {
        public string DisplayName { get; }
        public int CategoryIndex { get; }

        public ModOptionCategoryEntry(string displayName, int categoryIndex)
        {
            DisplayName = displayName;
            CategoryIndex = categoryIndex;
        }
    }
}
