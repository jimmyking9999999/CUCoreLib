using System;
using System.Collections.Generic;
using System.Linq;

namespace CUCoreLib.Data
{
    // Vanilla order
    public enum WorldSettingCategory
    {
        RunWide,
        Layer,
        Player,
        Trader,
        Other,
        Challenges
    }

    // Vanilla run presets (RunSettings.presets)
    public enum WorldSettingPreset
    {
        Normal,
        Relaxed,
        Paradise,
        Desolate,
        Unchipped,
        Endless
    }

    // Per row
    public sealed class WorldSettingDefinition
    {
        private WorldSettingDefinition()
        {
        }

        internal WorldSettingKind Kind { get; private set; }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public WorldSettingCategory Category { get; private set; }
        public string CustomCategory { get; private set; }
        public object DefaultValue { get; private set; }
        internal float Min { get; private set; }
        internal float Max { get; private set; }
        public ModDropdownChoice[] Choices { get; private set; }
        internal string Placeholder { get; private set; }
        internal Func<float, string> FloatFormatter { get; private set; }
        internal Action<object> Apply { get; private set; }
        internal Action OnClick { get; private set; }

        internal bool UsesCustomCategory => !string.IsNullOrWhiteSpace(CustomCategory);
        internal bool Persists => Kind != WorldSettingKind.Button;

        // Preset name (as in RunSettings.presets) -> default used when the player picks that preset.
        internal Dictionary<string, object> PresetDefaults { get; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Optional: a different default when the player picks a vanilla run preset (e.g. Desolate).
        // Presets without one use the normal default. Chainable: .WithPresetDefault(...).WithPresetDefault(...)
        public WorldSettingDefinition WithPresetDefault(WorldSettingPreset preset, object value)
        {
            return WithPresetDefault(preset.ToString().ToLowerInvariant(), value);
        }

        // String overload for presets added by other mods.
        public WorldSettingDefinition WithPresetDefault(string presetName, object value)
        {
            if (!string.IsNullOrWhiteSpace(presetName) && value != null) PresetDefaults[presetName.Trim()] = value;
            return this;
        }

        // Vanilla divider name, as in RunSettings.settingTypes.
        internal string GameSection => Category.ToString().ToLowerInvariant();

        public static WorldSettingDefinition Bool(string id, string name, string description,
            WorldSettingCategory category, bool defaultValue, Action<bool> apply = null)
        {
            return CreateBool(id, name, description, category, null, defaultValue, apply);
        }

        public static WorldSettingDefinition Bool(string id, string name, string description,
            string category, bool defaultValue, Action<bool> apply = null)
        {
            return CreateBool(id, name, description, default, category, defaultValue, apply);
        }

        public static WorldSettingDefinition Float(string id, string name, string description,
            WorldSettingCategory category, float defaultValue, float min, float max,
            Action<float> apply = null, Func<float, string> formatValue = null)
        {
            return CreateFloat(id, name, description, category, null, defaultValue, min, max, apply, formatValue);
        }

        public static WorldSettingDefinition Float(string id, string name, string description,
            string category, float defaultValue, float min, float max,
            Action<float> apply = null, Func<float, string> formatValue = null)
        {
            return CreateFloat(id, name, description, default, category, defaultValue, min, max, apply, formatValue);
        }

        public static WorldSettingDefinition Int(string id, string name, string description,
            WorldSettingCategory category, int defaultValue, int min, int max,
            Action<int> apply = null, string placeholder = null)
        {
            return CreateInt(id, name, description, category, null, defaultValue, min, max, apply, placeholder);
        }

        public static WorldSettingDefinition Int(string id, string name, string description,
            string category, int defaultValue, int min, int max,
            Action<int> apply = null, string placeholder = null)
        {
            return CreateInt(id, name, description, default, category, defaultValue, min, max, apply, placeholder);
        }

        public static WorldSettingDefinition Dropdown(string id, string name, string description,
            WorldSettingCategory category, int defaultValue, ModDropdownChoice[] choices, Action<int> apply = null)
        {
            return CreateDropdown(id, name, description, category, null, defaultValue, choices, apply);
        }

        public static WorldSettingDefinition Dropdown(string id, string name, string description,
            string category, int defaultValue, ModDropdownChoice[] choices, Action<int> apply = null)
        {
            return CreateDropdown(id, name, description, default, category, defaultValue, choices, apply);
        }

        public static WorldSettingDefinition Text(string id, string name, string description,
            WorldSettingCategory category, string defaultValue, Action<string> apply = null, string placeholder = null)
        {
            return CreateText(id, name, description, category, null, defaultValue, apply, placeholder);
        }

        public static WorldSettingDefinition Text(string id, string name, string description,
            string category, string defaultValue, Action<string> apply = null, string placeholder = null)
        {
            return CreateText(id, name, description, default, category, defaultValue, apply, placeholder);
        }

        public static WorldSettingDefinition Button(string id, string name, string description,
            WorldSettingCategory category, Action onClick, string buttonText = null)
        {
            return CreateButton(id, name, description, category, null, onClick, buttonText);
        }

        public static WorldSettingDefinition Button(string id, string name, string description,
            string category, Action onClick, string buttonText = null)
        {
            return CreateButton(id, name, description, default, category, onClick, buttonText);
        }

        private static WorldSettingDefinition CreateBase(string id, string name, string description,
            WorldSettingCategory category, string customCategory, WorldSettingKind kind)
        {
            return new WorldSettingDefinition
            {
                Id = id,
                Name = name,
                Description = description,
                Category = category,
                CustomCategory = string.IsNullOrWhiteSpace(customCategory) ? null : customCategory.Trim(),
                Kind = kind
            };
        }

        private static WorldSettingDefinition CreateBool(string id, string name, string description,
            WorldSettingCategory category, string customCategory, bool defaultValue, Action<bool> apply)
        {
            var definition = CreateBase(id, name, description, category, customCategory, WorldSettingKind.Bool);
            definition.DefaultValue = defaultValue;
            if (apply != null) definition.Apply = value => apply((bool)value);
            return definition;
        }

        private static WorldSettingDefinition CreateFloat(string id, string name, string description,
            WorldSettingCategory category, string customCategory, float defaultValue, float min, float max,
            Action<float> apply, Func<float, string> formatValue)
        {
            var definition = CreateBase(id, name, description, category, customCategory, WorldSettingKind.Float);
            definition.DefaultValue = defaultValue;
            definition.Min = min;
            definition.Max = max;
            definition.FloatFormatter = formatValue;
            if (apply != null) definition.Apply = value => apply((float)value);
            return definition;
        }

        private static WorldSettingDefinition CreateInt(string id, string name, string description,
            WorldSettingCategory category, string customCategory, int defaultValue, int min, int max,
            Action<int> apply, string placeholder)
        {
            var definition = CreateBase(id, name, description, category, customCategory, WorldSettingKind.Int);
            definition.DefaultValue = defaultValue;
            definition.Min = min;
            definition.Max = max;
            definition.Placeholder = placeholder;
            if (apply != null) definition.Apply = value => apply((int)value);
            return definition;
        }

        private static WorldSettingDefinition CreateDropdown(string id, string name, string description,
            WorldSettingCategory category, string customCategory, int defaultValue, ModDropdownChoice[] choices,
            Action<int> apply)
        {
            var definition = CreateBase(id, name, description, category, customCategory, WorldSettingKind.Dropdown);
            definition.DefaultValue = defaultValue;
            definition.Choices = choices?.ToArray();
            if (apply != null) definition.Apply = value => apply((int)value);
            return definition;
        }

        private static WorldSettingDefinition CreateText(string id, string name, string description,
            WorldSettingCategory category, string customCategory, string defaultValue, Action<string> apply,
            string placeholder)
        {
            var definition = CreateBase(id, name, description, category, customCategory, WorldSettingKind.Text);
            definition.DefaultValue = defaultValue ?? string.Empty;
            definition.Placeholder = placeholder;
            if (apply != null) definition.Apply = value => apply((string)value);
            return definition;
        }

        private static WorldSettingDefinition CreateButton(string id, string name, string description,
            WorldSettingCategory category, string customCategory, Action onClick, string buttonText)
        {
            var definition = CreateBase(id, name, description, category, customCategory, WorldSettingKind.Button);
            definition.OnClick = onClick;
            definition.Placeholder = buttonText;
            return definition;
        }
    }

    internal enum WorldSettingKind
    {
        Bool,
        Float,
        Int,
        Dropdown,
        Text,
        Button
    }
}
