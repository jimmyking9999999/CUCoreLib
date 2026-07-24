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
        private static readonly Dictionary<string, ModOptionCategoryEntry> CustomCategoriesByOptionId =
            new Dictionary<string, ModOptionCategoryEntry>(StringComparer.Ordinal);

        private static readonly Dictionary<string, ModOptionCategoryEntry> CustomCategoriesByKey =
            new Dictionary<string, ModOptionCategoryEntry>(StringComparer.Ordinal);

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
            ModSettingsConfigSyncRegistry.RegisterOption(option);
            RegisterLocale(option);
            MergeIntoLoadedSettings(option);
            SettingsMenuCategoryExtender.RefreshLiveMenu();
            return true;
        }

        internal static void AppendRegisteredOptions(List<Setting> settings)
        {
            if (settings == null) return;

            ReconcileCustomCategoryOwnership(settings);

            foreach (var option in from option in RegisteredOptions
                     let option1 = option
                     where option != null && !settings.Any(setting => setting != null && setting.name == option1.Id)
                     select option)
            {
                var createdSetting = option.CreateSetting();
                settings.Add(createdSetting);
                ModSettingsConfigSyncRegistry.RegisterSetting(option, createdSetting);
            }
        }

        internal static List<ModOptionCategoryEntry> GetCustomCategories()
        {
            return CustomCategories.ToList();
        }

        internal static bool TryGetOwnedCustomCategory(Setting.SettingCategory category, out ModOptionCategoryEntry entry)
        {
            entry = CustomCategories.FirstOrDefault(candidate => candidate != null && candidate.Category == category);
            return entry != null;
        }

        internal static bool TryGetOwnedCustomCategory(string normalizedKey, out ModOptionCategoryEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(normalizedKey)) return false;

            return CustomCategoriesByKey.TryGetValue(normalizedKey, out entry) && entry != null;
        }

        internal static string NormalizeCustomCategoryKey(string category)
        {
            return NormalizeCategoryKey(category);
        }

        internal static bool IsOwnedCustomCategory(Setting.SettingCategory category)
        {
            return CustomCategories.Any(entry => entry != null && entry.Category == category);
        }

        internal static void ReconcileCustomCategoryOwnership(IEnumerable<Setting> settings = null)
        {
            if (CustomCategories.Count == 0) return;

            var occupiedIndices = CollectForeignCustomCategoryIndices(settings);
            var assignedIndices = new HashSet<int>();
            var changed = false;

            foreach (var entry in CustomCategories)
            {
                if (entry == null) continue;

                var currentIndex = (int)entry.Category;
                var needsReassignment = currentIndex < CustomCategoryBaseIndex ||
                                        occupiedIndices.Contains(currentIndex) ||
                                        !assignedIndices.Add(currentIndex);

                if (!needsReassignment) continue;

                var nextIndex = FindNextAvailableCategoryIndex(occupiedIndices, assignedIndices);
                if (currentIndex == nextIndex) continue;

                entry.SetCategory((Setting.SettingCategory)nextIndex);
                assignedIndices.Add(nextIndex);
                changed = true;
            }

            if (changed) ApplyResolvedCategoriesToOwnedOptions(settings);
        }

        internal static bool TryGetLocalizedText(string localeKey, out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(localeKey) ||
                !localeKey.StartsWith("gameset", StringComparison.Ordinal)) return false;

            if (TryGetLoadedLocaleText(localeKey, out text)) return true;

            var cleanKey = localeKey.Substring("gameset".Length);
            if (TryGetLoadedLocaleText(cleanKey, out text)) return true;

            if (TryGetRegisteredLocaleText(LocaleRegistry.LocaleCategory.Other, localeKey, out text)) return true;
            if (TryGetRegisteredLocaleText(LocaleRegistry.LocaleCategory.Other, cleanKey, out text)) return true;

            return TryGetRegisteredLocaleText(LocaleRegistry.LocaleCategory.Option, cleanKey, out text);
        }

        private static void MergeIntoLoadedSettings(ModOptionDefinition option)
        {
            if (Settings.settings == null ||
                Settings.settings.Any(setting => setting != null && setting.name == option.Id)) return;

            ReconcileCustomCategoryOwnership(Settings.settings);
            var createdSetting = option.CreateSetting();
            Settings.settings.Add(createdSetting);
            ModSettingsConfigSyncRegistry.RegisterSetting(option, createdSetting);
            createdSetting.Apply();
        }

        private static void RegisterLocale(ModOptionDefinition option)
        {
            LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Option, option.Id, option.Label);
            LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, "gameset" + option.Id, option.Label);
            if (!string.IsNullOrWhiteSpace(option.Description))
            {
                LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Option, option.Id + "dsc",
                    option.Description);
                LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, "gameset" + option.Id + "dsc",
                    option.Description);
            }
            // todo I really need to figure this out
            // man this is kinda ass ngl
            if (option.Kind != ModOptionKind.Dropdown || option.Choices == null) return;

            foreach (var choice in option.Choices)
            {
                LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Option, option.Id + choice.Key,
                    choice.Label);
                LocaleRegistry.Register(LocaleRegistry.LocaleCategory.Other, "gameset" + option.Id + choice.Key,
                    choice.Label);
            }
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

            if ((option.Kind == ModOptionKind.Float || option.Kind == ModOptionKind.Int) && option.Min > option.Max)
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
                    (Setting.SettingCategory)FindNextAvailableCategoryIndex(CollectForeignCustomCategoryIndices(null),
                        new HashSet<int>(CustomCategories.Select(category => (int)category.Category))));
                CustomCategories.Add(entry);
                CustomCategoriesByKey.Add(normalizedKey, entry);
            }

            entry.RegisterOption(option.Id);
            CustomCategoriesByOptionId[option.Id] = entry;
            option.SetResolvedCategory(entry.Category);
        }

        private static string NormalizeCategoryKey(string category)
        {
            return (category ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static HashSet<int> CollectForeignCustomCategoryIndices(IEnumerable<Setting> settings)
        {
            var occupiedIndices = new HashSet<int>();
            var source = settings ?? Settings.settings;
            if (source == null) return occupiedIndices;

            foreach (var setting in source)
            {
                if (setting == null) continue;
                var categoryIndex = (int)setting.category;
                if (categoryIndex < CustomCategoryBaseIndex) continue;
                if (RegisteredIds.Contains(setting.name)) continue;
                occupiedIndices.Add(categoryIndex);
            }

            return occupiedIndices;
        }

        private static int FindNextAvailableCategoryIndex(ISet<int> occupiedIndices, ISet<int> assignedIndices)
        {
            var nextIndex = CustomCategoryBaseIndex;
            while ((occupiedIndices != null && occupiedIndices.Contains(nextIndex)) ||
                   (assignedIndices != null && assignedIndices.Contains(nextIndex)))
                nextIndex++;

            return nextIndex;
        }

        private static void ApplyResolvedCategoriesToOwnedOptions(IEnumerable<Setting> settings)
        {
            foreach (var option in RegisteredOptions)
            {
                if (option == null || !CustomCategoriesByOptionId.TryGetValue(option.Id, out var entry) || entry == null) continue;
                option.SetResolvedCategory(entry.Category);
            }

            var targetSettings = settings ?? Settings.settings;
            if (targetSettings == null) return;

            foreach (var setting in targetSettings)
            {
                if (setting == null || !CustomCategoriesByOptionId.TryGetValue(setting.name, out var entry) || entry == null)
                    continue;

                setting.category = entry.Category;
            }
        }

        private static bool TryGetLoadedLocaleText(string key, out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            var language = Locale.currentLang;
            if (language?.other == null ||
                !language.other.TryGetValue(key, out var localizedText) ||
                string.IsNullOrWhiteSpace(localizedText)) return false;

            text = localizedText;
            return true;
        }

        private static bool TryGetRegisteredLocaleText(LocaleRegistry.LocaleCategory category, string key, out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            if (!LocaleRegistry.CustomLocales.TryGetValue((int)category, out var locales) ||
                !locales.TryGetValue(key, out var localizedText) ||
                string.IsNullOrWhiteSpace(localizedText)) return false;

            text = localizedText;
            return true;
        }
    }

    internal sealed class ModOptionCategoryEntry
    {
        private readonly HashSet<string> ownedOptionIds = new HashSet<string>(StringComparer.Ordinal);

        public ModOptionCategoryEntry(string displayName, Setting.SettingCategory category)
        {
            DisplayName = displayName;
            Category = category;
        }

        public void RegisterOption(string optionId)
        {
            if (!string.IsNullOrWhiteSpace(optionId))
                ownedOptionIds.Add(optionId);
        }

        public void SetCategory(Setting.SettingCategory category)
        {
            Category = category;
        }

        public string DisplayName { get; }
        public Setting.SettingCategory Category { get; private set; }
        public int CategoryIndex => (int)Category;
    }
}
