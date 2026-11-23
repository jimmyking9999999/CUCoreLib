using System;
using System.Linq;
using UnityEngine;

namespace CUCoreLib.Data
{
    public abstract class ModOptionDefinition
    {
        internal ModOptionDefinition(string id, ModOptionKind kind)
        {
            Id = id;
            Kind = kind;
            Category = kind == ModOptionKind.Keybind ? Setting.SettingCategory.Input : Setting.SettingCategory.Game;
        }

        internal ModOptionKind Kind { get; private set; }
        public string Id { get; private set; }
        public string Label { get; private set; }
        public string Description { get; private set; }
        internal ModOptionCategory CustomTab { get; set; }
        public Setting.SettingCategory Category { get; private set; }
        public string CustomCategory => CustomTab?.Key == null ? CustomTab?.Fallback : null;
        internal float FloatDefault { get; private set; }
        internal int IntDefault { get; private set; }
        internal bool BoolDefault { get; private set; }
        internal KeyCode KeyDefault { get; private set; }
        internal float Min { get; private set; }
        internal float Max { get; private set; }
        public ModDropdownChoice[] Choices { get; private set; }
        internal Func<float, string> FloatFormatter { get; private set; }
        internal string StringDefault { get; private set; }
        internal string Placeholder { get; private set; }
        public bool IsLocal { get; private set; }

        internal string LabelKeyOverride { get; set; }
        internal string DescriptionKeyOverride { get; set; }
        internal bool UsesLegacyText { get; private set; }
        internal bool IsRegistered { get; set; }

        internal void EnsureCanConfigure()
        {
            if (IsRegistered) throw new InvalidOperationException($"Configure option '{Id}' before registering it.");
        }

        internal ModOptionDefinition SetCustomCategory(ModOptionCategory category)
        {
            EnsureCanConfigure();
            CustomTab = category ?? throw new ArgumentNullException(nameof(category));
            return this;
        }

        public ModOptionDefinition LocalOnly()
        {
            IsLocal = true;
            return this;
        }

        internal void SetResolvedCategory(Setting.SettingCategory category)
        {
            Category = category;
        }

        public static ModOptionDefinition<float> Float(
            string id,
            float defaultValue,
            float min,
            float max,
            Func<float, string> formatValue = null)
        {
            return new ModOptionDefinition<float>(id, ModOptionKind.Float, Settings.Get<SettingFloat>)
            {
                FloatDefault = defaultValue,
                Min = min,
                Max = max,
                FloatFormatter = formatValue
            };
        }

        public static ModOptionDefinition<int> Int(
            string id,
            int defaultValue,
            int min,
            int max)
        {
            return new ModOptionDefinition<int>(id, ModOptionKind.Int, Settings.Get<SettingInt>)
            {
                IntDefault = defaultValue,
                Min = min,
                Max = max
            };
        }

        public static ModOptionDefinition<bool> Bool(
            string id,
            bool defaultValue)
        {
            return new ModOptionDefinition<bool>(id, ModOptionKind.Bool, Settings.Get<SettingBool>)
            {
                BoolDefault = defaultValue
            };
        }

        public static ModOptionDefinition<int> Dropdown(
            string id,
            int defaultValue,
            ModDropdownChoice[] choices)
        {
            return new ModOptionDefinition<int>(id, ModOptionKind.Dropdown, Settings.Get<SettingDropdown>)
            {
                IntDefault = defaultValue,
                Choices = choices?.ToArray()
            };
        }

        public static ModOptionDefinition<KeyCode> Keybind(
            string id,
            KeyCode defaultValue)
        {
            return new ModOptionDefinition<KeyCode>(id, ModOptionKind.Keybind, Settings.Get<SettingKeybind>)
            {
                KeyDefault = defaultValue,
                IsLocal = true
            };
        }

        public static ModOptionDefinition<string> Text(
            string id,
            string defaultValue,
            string placeholder = null)
        {
            return new ModOptionDefinition<string>(id, ModOptionKind.Text, Settings.Get<SettingString>)
            {
                StringDefault = defaultValue ?? string.Empty,
                Placeholder = placeholder
            };
        }

        private const string LegacyFactoryMessage =
            "Use the shorter factory and .OnApply(callback). Put the label and description in the language file under id and id + \"dsc\".";

        internal ModOptionDefinition WithLegacyText(string label, string description)
        {
            EnsureCanConfigure();
            Label = label;
            Description = description;
            UsesLegacyText = true;
            return this;
        }

        private ModOptionDefinition WithLegacyCategory(string title)
        {
            SetResolvedCategory(default(Setting.SettingCategory));
            return string.IsNullOrWhiteSpace(title) ? this : SetCustomCategory(ModOptionCategory.Named(title));
        }

        [Obsolete(LegacyFactoryMessage)]
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
            return Float(id, defaultValue, min, max, formatValue).OnApply(apply)
                .WithCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
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
            return Float(id, defaultValue, min, max, formatValue).OnApply(apply)
                .WithLegacyCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
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
            return Int(id, defaultValue, min, max).OnApply(apply)
                .WithCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
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
            return Int(id, defaultValue, min, max).OnApply(apply)
                .WithLegacyCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
        public static ModOptionDefinition Bool(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            bool defaultValue,
            Action<bool> apply = null)
        {
            return Bool(id, defaultValue).OnApply(apply)
                .WithCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
        public static ModOptionDefinition Bool(
            string id,
            string label,
            string description,
            string category,
            bool defaultValue,
            Action<bool> apply = null)
        {
            return Bool(id, defaultValue).OnApply(apply)
                .WithLegacyCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
        public static ModOptionDefinition Dropdown(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            int defaultValue,
            ModDropdownChoice[] choices,
            Action<int> apply = null)
        {
            return Dropdown(id, defaultValue, choices).OnApply(apply)
                .WithCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
        public static ModOptionDefinition Dropdown(
            string id,
            string label,
            string description,
            string category,
            int defaultValue,
            ModDropdownChoice[] choices,
            Action<int> apply = null)
        {
            return Dropdown(id, defaultValue, choices).OnApply(apply)
                .WithLegacyCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
        public static ModOptionDefinition Keybind(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            KeyCode defaultValue,
            Action<KeyCode> apply = null)
        {
            return Keybind(id, defaultValue).OnApply(apply)
                .WithCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
        public static ModOptionDefinition Keybind(
            string id,
            string label,
            string description,
            string category,
            KeyCode defaultValue,
            Action<KeyCode> apply = null)
        {
            return Keybind(id, defaultValue).OnApply(apply)
                .WithLegacyCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
        public static ModOptionDefinition Text(
            string id,
            string label,
            string description,
            Setting.SettingCategory category,
            string defaultValue,
            Action<string> apply = null,
            string placeholder = null)
        {
            return Text(id, defaultValue, placeholder).OnApply(apply)
                .WithCategory(category).WithLegacyText(label, description);
        }

        [Obsolete(LegacyFactoryMessage)]
        public static ModOptionDefinition Text(
            string id,
            string label,
            string description,
            string category,
            string defaultValue,
            Action<string> apply = null,
            string placeholder = null)
        {
            return Text(id, defaultValue, placeholder).OnApply(apply)
                .WithLegacyCategory(category).WithLegacyText(label, description);
        }

        internal abstract void ApplyCurrentValue();

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
                        apply = ApplyCurrentValue
                    };
                case ModOptionKind.Int:
                    return new SettingInt
                    {
                        name = Id,
                        value = Mathf.Clamp(IntDefault, Mathf.RoundToInt(Min), Mathf.RoundToInt(Max)),
                        min = Mathf.RoundToInt(Min),
                        max = Mathf.RoundToInt(Max),
                        category = Category,
                        apply = ApplyCurrentValue
                    };
                case ModOptionKind.Bool:
                    return new SettingBool
                    {
                        name = Id,
                        value = BoolDefault,
                        category = Category,
                        apply = ApplyCurrentValue
                    };
                case ModOptionKind.Dropdown:
                    return new SettingDropdown
                    {
                        name = Id,
                        value = IntDefault,
                        choices = Choices.Select(choice => choice.Key).ToArray(),
                        category = Category,
                        apply = ApplyCurrentValue
                    };
                case ModOptionKind.Keybind:
                    return new SettingKeybind
                    {
                        name = Id,
                        value = KeyDefault,
                        category = Category,
                        apply = ApplyCurrentValue
                    };
                case ModOptionKind.Text:
                    return new SettingString
                    {
                        name = Id,
                        value = StringDefault ?? string.Empty,
                        category = Category,
                        apply = ApplyCurrentValue
                    };
                default:
                    throw new InvalidOperationException($"Unsupported mod option kind '{Kind}'.");
            }
        }
    }

    public sealed class ModOptionDefinition<T> : ModOptionDefinition
    {
        private readonly Func<string, Setting> getSetting;
        private Action<T> apply;

        internal ModOptionDefinition(string id, ModOptionKind kind, Func<string, Setting> getSetting) : base(id, kind)
        {
            this.getSetting = getSetting;
        }

        /// <summary>Sets the callback invoked with the current value when the game applies this setting.</summary>
        public ModOptionDefinition<T> OnApply(Action<T> callback)
        {
            EnsureCanConfigure();
            apply = callback;
            return this;
        }

        internal override void ApplyCurrentValue()
        {
            if (apply == null) return;
            var setting = getSetting(Id);
            if (setting != null) apply((T)setting.GetValue());
        }

        /// <summary>Uses a locale key for the label. By default the label uses the setting ID.</summary>
        public ModOptionDefinition<T> WithLabelKey(string key)
        {
            LabelKeyOverride = ConfigureLocaleKey(key);
            return this;
        }

        /// <summary>Uses a locale key for the description. By default the description uses the setting ID + "dsc".</summary>
        public ModOptionDefinition<T> WithDescriptionKey(string key)
        {
            DescriptionKeyOverride = ConfigureLocaleKey(key);
            return this;
        }

        private string ConfigureLocaleKey(string key)
        {
            EnsureCanConfigure();
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A locale key is required.", nameof(key));
            return key.Trim();
        }

        public ModOptionDefinition<T> WithCategory(Setting.SettingCategory category)
        {
            EnsureCanConfigure();
            SetResolvedCategory(category);
            CustomTab = null;
            return this;
        }

        public ModOptionDefinition<T> WithCategory(ModOptionCategory category)
        {
            SetCustomCategory(category);
            return this;
        }

        public new ModOptionDefinition<T> LocalOnly()
        {
            base.LocalOnly();
            return this;
        }
    }

    public sealed class ModOptionCategory
    {
        private ModOptionCategory(string key, string fallback)
        {
            Key = key;
            Fallback = fallback;
        }

        public string Key { get; }
        public string Fallback { get; }

        public static ModOptionCategory Localized(string key, string fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A locale key is required.", nameof(key));
            return new ModOptionCategory(key.Trim(), fallback);
        }

        public static ModOptionCategory Named(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A tab title is required.", nameof(title));
            return new ModOptionCategory(null, title.Trim());
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
        Keybind,
        Text
    }
}
