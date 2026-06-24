using System;
using System.Linq;
using UnityEngine;

namespace CUCoreLib.Data
{
    public sealed class ModOptionDefinition
    {
        private ModOptionDefinition()
        {
        }

        internal ModOptionKind Kind { get; private set; }
        public string Id { get; private set; }
        public string Label { get; private set; }
        public string Description { get; private set; }
        public Setting.SettingCategory Category { get; private set; }
        public string CustomCategory { get; private set; }
        internal float FloatDefault { get; private set; }
        internal int IntDefault { get; private set; }
        internal bool BoolDefault { get; private set; }
        internal KeyCode KeyDefault { get; private set; }
        internal float Min { get; private set; }
        internal float Max { get; private set; }
        public ModDropdownChoice[] Choices { get; private set; }
        internal Func<float, string> FloatFormatter { get; private set; }
        internal Action<float> FloatApply { get; private set; }
        internal Action<int> IntApply { get; private set; }
        internal Action<int> DropdownApply { get; private set; }
        internal Action<bool> BoolApply { get; private set; }
        internal Action<KeyCode> KeybindApply { get; private set; }

        internal bool UsesCustomCategory => !string.IsNullOrWhiteSpace(CustomCategory);

        internal void SetResolvedCategory(Setting.SettingCategory category)
        {
            Category = category;
        }

        public static ModOptionDefinition Float(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            float defaultValue,
            float min,
            float max,
            Action<float> apply = null,
            Func<float, string> formatValue = null)
        {
            return CreateFloat(id, label, description, category, null, defaultValue, min, max, apply, formatValue);
        }

        public static ModOptionDefinition Float(
            string id,
            string label,
            string description,
            string category,
            float defaultValue,
            float min,
            float max,
            Action<float> apply = null,
            Func<float, string> formatValue = null)
        {
            return CreateFloat(id, label, description, default, category, defaultValue, min, max, apply, formatValue);
        }

        public static ModOptionDefinition Int(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            int defaultValue,
            int min,
            int max,
            Action<int> apply = null)
        {
            return CreateInt(id, label, description, category, null, defaultValue, min, max, apply);
        }

        public static ModOptionDefinition Int(
            string id,
            string label,
            string description,
            string category,
            int defaultValue,
            int min,
            int max,
            Action<int> apply = null)
        {
            return CreateInt(id, label, description, default, category, defaultValue, min, max, apply);
        }

        public static ModOptionDefinition Bool(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            bool defaultValue,
            Action<bool> apply = null)
        {
            return CreateBool(id, label, description, category, null, defaultValue, apply);
        }

        public static ModOptionDefinition Bool(
            string id,
            string label,
            string description,
            string category,
            bool defaultValue,
            Action<bool> apply = null)
        {
            return CreateBool(id, label, description, default, category, defaultValue, apply);
        }

        public static ModOptionDefinition Dropdown(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            int defaultValue,
            ModDropdownChoice[] choices,
            Action<int> apply = null)
        {
            return CreateDropdown(id, label, description, category, null, defaultValue, choices, apply);
        }

        public static ModOptionDefinition Dropdown(
            string id,
            string label,
            string description,
            string category,
            int defaultValue,
            ModDropdownChoice[] choices,
            Action<int> apply = null)
        {
            return CreateDropdown(id, label, description, default, category, defaultValue, choices, apply);
        }

        public static ModOptionDefinition Keybind(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            KeyCode defaultValue,
            Action<KeyCode> apply = null)
        {
            return CreateKeybind(id, label, description, category, null, defaultValue, apply);
        }

        public static ModOptionDefinition Keybind(
            string id,
            string label,
            string description,
            string category,
            KeyCode defaultValue,
            Action<KeyCode> apply = null)
        {
            return CreateKeybind(id, label, description, default, category, defaultValue, apply);
        }

        internal Setting CreateSetting()
        {
            switch (Kind)
            {
                case ModOptionKind.Float:
                    return new SettingFloat
                    {
                        name = Id,
                        value = Mathf.Clamp(FloatDefault, Min, Max),
                        min = Min,
                        max = Max,
                        category = Category,
                        formatValue = FloatFormatter,
                        apply = () =>
                        {
                            var setting = Settings.Get<SettingFloat>(Id);
                            if (setting != null) FloatApply?.Invoke(setting.value);
                        }
                    };
                case ModOptionKind.Int:
                    return new SettingInt
                    {
                        name = Id,
                        value = Mathf.Clamp(IntDefault, Mathf.RoundToInt(Min), Mathf.RoundToInt(Max)),
                        min = Mathf.RoundToInt(Min),
                        max = Mathf.RoundToInt(Max),
                        category = Category,
                        apply = () =>
                        {
                            var setting = Settings.Get<SettingInt>(Id);
                            if (setting != null) IntApply?.Invoke(setting.value);
                        }
                    };
                case ModOptionKind.Bool:
                    return new SettingBool
                    {
                        name = Id,
                        value = BoolDefault,
                        category = Category,
                        apply = () =>
                        {
                            var setting = Settings.Get<SettingBool>(Id);
                            if (setting != null) BoolApply?.Invoke(setting.value);
                        }
                    };
                case ModOptionKind.Dropdown:
                    return new SettingDropdown
                    {
                        name = Id,
                        value = IntDefault,
                        choices = Choices.Select(choice => choice.Key).ToArray(),
                        category = Category,
                        apply = () =>
                        {
                            var setting = Settings.Get<SettingDropdown>(Id);
                            if (setting != null) DropdownApply?.Invoke(setting.value);
                        }
                    };
                case ModOptionKind.Keybind:
                    return new SettingKeybind
                    {
                        name = Id,
                        value = KeyDefault,
                        category = Category,
                        apply = () =>
                        {
                            var setting = Settings.Get<SettingKeybind>(Id);
                            if (setting != null) KeybindApply?.Invoke(setting.value);
                        }
                    };
                default:
                    throw new InvalidOperationException($"Unsupported mod option kind '{Kind}'.");
            }
        }

        private static ModOptionDefinition CreateBase(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            string customCategory)
        {
            return new ModOptionDefinition
            {
                Id = id,
                Label = label,
                Description = description,
                Category = category,
                CustomCategory = string.IsNullOrWhiteSpace(customCategory) ? null : customCategory.Trim()
            };
        }

        private static ModOptionDefinition CreateFloat(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            string customCategory,
            float defaultValue,
            float min,
            float max,
            Action<float> apply,
            Func<float, string> formatValue)
        {
            var definition = CreateBase(id, label, description, category, customCategory);
            definition.Kind = ModOptionKind.Float;
            definition.FloatDefault = defaultValue;
            definition.Min = min;
            definition.Max = max;
            definition.FloatApply = apply;
            definition.FloatFormatter = formatValue;
            return definition;
        }

        private static ModOptionDefinition CreateInt(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            string customCategory,
            int defaultValue,
            int min,
            int max,
            Action<int> apply)
        {
            var definition = CreateBase(id, label, description, category, customCategory);
            definition.Kind = ModOptionKind.Int;
            definition.IntDefault = defaultValue;
            definition.Min = min;
            definition.Max = max;
            definition.IntApply = apply;
            return definition;
        }

        private static ModOptionDefinition CreateBool(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            string customCategory,
            bool defaultValue,
            Action<bool> apply)
        {
            var definition = CreateBase(id, label, description, category, customCategory);
            definition.Kind = ModOptionKind.Bool;
            definition.BoolDefault = defaultValue;
            definition.BoolApply = apply;
            return definition;
        }

        private static ModOptionDefinition CreateDropdown(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            string customCategory,
            int defaultValue,
            ModDropdownChoice[] choices,
            Action<int> apply)
        {
            var definition = CreateBase(id, label, description, category, customCategory);
            definition.Kind = ModOptionKind.Dropdown;
            definition.IntDefault = defaultValue;
            definition.Choices = choices?.ToArray();
            definition.DropdownApply = apply;
            return definition;
        }

        private static ModOptionDefinition CreateKeybind(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            string customCategory,
            KeyCode defaultValue,
            Action<KeyCode> apply)
        {
            var definition = CreateBase(id, label, description, category, customCategory);
            definition.Kind = ModOptionKind.Keybind;
            definition.KeyDefault = defaultValue;
            definition.KeybindApply = apply;
            return definition;
        }
    }

    public sealed class ModDropdownChoice
    {
        public ModDropdownChoice(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; }
        public string Label { get; private set; }
    }

    internal enum ModOptionKind
    {
        Float,
        Int,
        Bool,
        Dropdown,
        Keybind
    }
}