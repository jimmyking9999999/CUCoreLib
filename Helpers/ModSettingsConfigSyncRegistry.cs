using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using CUCoreLib.Data;
using UnityEngine;

namespace CUCoreLib.Helpers
{
    internal static class ModSettingsConfigSyncRegistry
    {
        private sealed class SyncState
        {
            public ModOptionDefinition Option;
            public Setting Setting;
            public ConfigFile ConfigFile;
            public ConfigEntryBase ConfigEntry;
            public bool ApplyingFromConfig;
            public bool ApplyingFromSetting;
            public bool ApplyWrapped;
        }

        private static readonly Dictionary<string, SyncState> StatesById =
            new Dictionary<string, SyncState>(StringComparer.Ordinal);

        private static readonly Dictionary<ConfigFile, Dictionary<string, ConfigEntryBase>> ConfigEntriesByFile =
            new Dictionary<ConfigFile, Dictionary<string, ConfigEntryBase>>();

        private static readonly HashSet<ConfigFile> HookedFiles = new HashSet<ConfigFile>();

        internal static void RegisterOption(ModOptionDefinition option)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.Id)) return;

            var state = GetOrCreateState(option.Id);
            state.Option = option;
            RefreshLoadedConfigEntries();
        }

        internal static void RegisterSetting(ModOptionDefinition option, Setting setting)
        {
            if (option == null || setting == null || string.IsNullOrWhiteSpace(option.Id)) return;

            var state = GetOrCreateState(option.Id);
            state.Option = option;
            state.Setting = setting;
            WrapSettingApply(state);
            RefreshLoadedConfigEntries();
        }

        internal static void RegisterConfigEntry(ConfigFile configFile, ConfigEntryBase entry)
        {
            if (configFile == null || entry == null) return;

            TrackConfigEntry(configFile, entry);

            var state = FindStateForDefinition(entry.Definition);
            if (state == null) return;

            state.ConfigFile = configFile;
            state.ConfigEntry = entry;
            TryLinkState(state);
        }

        private static SyncState GetOrCreateState(string optionId)
        {
            if (StatesById.TryGetValue(optionId, out var state)) return state;

            state = new SyncState();
            StatesById[optionId] = state;
            return state;
        }

        private static SyncState FindStateForDefinition(ConfigDefinition definition)
        {
            if (definition == null) return null;

            SyncState exactFullMatch = null;
            SyncState exactKeyMatch = null;
            SyncState suffixMatch = null;
            var suffix = "." + definition.Key;
            var fullKey = BuildFullKey(definition);

            foreach (var state in StatesById.Values)
            {
                var optionId = state?.Option?.Id;
                if (string.IsNullOrWhiteSpace(optionId)) continue;

                if (string.Equals(optionId, fullKey, StringComparison.Ordinal))
                {
                    exactFullMatch = state;
                    break;
                }

                if (string.Equals(optionId, definition.Key, StringComparison.Ordinal))
                {
                    exactKeyMatch = state;
                    continue;
                }

                if (optionId.EndsWith(suffix, StringComparison.Ordinal))
                {
                    if (suffixMatch != null)
                    {
                        CUCoreLibPlugin.Log?.LogError(
                            $"CUCoreLib settings sync found multiple mod options for config '{definition.Section}.{definition.Key}'.");
                        return null;
                    }

                    suffixMatch = state;
                }
            }

            return exactFullMatch ?? exactKeyMatch ?? suffixMatch;
        }

        private static void TryLinkState(SyncState state)
        {
            if (state == null) return;

            if (state.Option == null || state.Setting == null || state.ConfigEntry == null) return;

            TrySyncSettingToConfig(state, "linked setting");
        }

        private static void WrapSettingApply(SyncState state)
        {
            if (state == null || state.Setting == null || state.ApplyWrapped) return;

            var originalApply = state.Setting.apply;
            state.Setting.apply = () =>
            {
                originalApply?.Invoke();
                NotifySettingApplied(state);
            };
            state.ApplyWrapped = true;
        }

        private static void NotifySettingApplied(SyncState state)
        {
            if (state == null || state.ConfigEntry == null || state.ApplyingFromConfig) return;

            TrySyncSettingToConfig(state, "game setting applied");
        }

        private static void TrySyncConfigToSetting(SyncState state, ConfigEntryBase entry, string reason)
        {
            if (state == null || state.Option == null || state.Setting == null || entry == null) return;
            if (state.ApplyingFromSetting) return;

            if (!TryConvertConfigValueToSetting(state.Option, entry.BoxedValue, out var convertedValue, out var error))
            {
                CUCoreLibPlugin.Log?.LogError(
                    $"CUCoreLib settings sync could not apply config '{BuildConfigLabel(entry.Definition)}' to '{state.Option.Id}' ({reason}): {error}");
                RevertConfigEntry(state);
                return;
            }

            try
            {
                state.ApplyingFromConfig = true;
                SetSettingValue(state.Setting, state.Option.Kind, convertedValue);
                state.Setting.Apply();
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogError(
                    $"CUCoreLib settings sync failed while applying config '{BuildConfigLabel(entry.Definition)}' to '{state.Option.Id}' ({reason}).\n{ex}");
            }
            finally
            {
                state.ApplyingFromConfig = false;
            }
        }

        private static void TrySyncSettingToConfig(SyncState state, string reason)
        {
            if (state == null || state.Option == null || state.Setting == null || state.ConfigEntry == null) return;
            if (state.ApplyingFromConfig) return;

            var currentValue = GetSettingValue(state.Setting, state.Option.Kind);
            if (!TryConvertSettingValueToConfig(state.Option, currentValue, state.ConfigEntry.SettingType,
                    out var convertedValue, out var error))
            {
                CUCoreLibPlugin.Log?.LogError(
                    $"CUCoreLib settings sync could not mirror '{state.Option.Id}' to config ({reason}): {error}");
                return;
            }

            try
            {
                state.ApplyingFromSetting = true;
                state.ConfigEntry.BoxedValue = convertedValue;
                state.ConfigFile?.Save();
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogError(
                    $"CUCoreLib settings sync failed while mirroring '{state.Option.Id}' to config ({reason}).\n{ex}");
            }
            finally
            {
                state.ApplyingFromSetting = false;
            }
        }

        private static void RevertConfigEntry(SyncState state)
        {
            if (state == null || state.ConfigEntry == null || state.Option == null || state.Setting == null) return;

            var currentValue = GetSettingValue(state.Setting, state.Option.Kind);
            if (!TryConvertSettingValueToConfig(state.Option, currentValue, state.ConfigEntry.SettingType,
                    out var revertedValue, out _))
                return;

            try
            {
                state.ApplyingFromSetting = true;
                state.ConfigEntry.BoxedValue = revertedValue;
                state.ConfigFile?.Save();
            }
            catch
            {
                // Best effort only.
            }
            finally
            {
                state.ApplyingFromSetting = false;
            }
        }

        private static void EnsureConfigFileHooked(ConfigFile configFile)
        {
            if (configFile == null || HookedFiles.Contains(configFile)) return;

            HookedFiles.Add(configFile);
            configFile.SettingChanged += OnConfigSettingChanged;
            configFile.ConfigReloaded += OnConfigReloaded;
        }

        private static void OnConfigSettingChanged(object sender, SettingChangedEventArgs e)
        {
            var configFile = sender as ConfigFile;
            var entry = e?.ChangedSetting;
            if (configFile == null || entry == null) return;

            var state = FindStateForDefinition(entry.Definition);
            if (state == null || state.ApplyingFromSetting) return;

            state.ConfigFile = configFile;
            state.ConfigEntry = entry;
            TrySyncConfigToSetting(state, entry, "config setting changed");
        }

        private static void OnConfigReloaded(object sender, EventArgs e)
        {
            var configFile = sender as ConfigFile;
            if (configFile == null) return;

            if (!ConfigEntriesByFile.TryGetValue(configFile, out var entries)) return;

            foreach (var entry in entries.Values)
            {
                if (entry == null) continue;

                var state = FindStateForDefinition(entry.Definition);
                if (state == null || state.ApplyingFromSetting) continue;

                state.ConfigFile = configFile;
                state.ConfigEntry = entry;
                TrySyncConfigToSetting(state, entry, "config reloaded");
            }
        }

        private static void TryAttachTrackedConfigEntry(SyncState state)
        {
            if (state?.Option == null || state.ConfigEntry != null) return;

            foreach (var pair in ConfigEntriesByFile)
            {
                foreach (var entry in pair.Value.Values)
                {
                    if (entry == null) continue;

                    var matchedState = FindStateForDefinition(entry.Definition);
                    if (!ReferenceEquals(matchedState, state)) continue;

                    state.ConfigFile = pair.Key;
                    state.ConfigEntry = entry;
                    return;
                }
            }
        }

        private static string BuildDefinitionKey(ConfigDefinition definition)
        {
            return (definition?.Section ?? string.Empty) + "\u001F" + (definition?.Key ?? string.Empty);
        }

        private static string BuildFullKey(ConfigDefinition definition)
        {
            if (definition == null) return string.Empty;
            if (string.IsNullOrWhiteSpace(definition.Section)) return definition.Key ?? string.Empty;
            return definition.Section + "." + definition.Key;
        }

        private static string BuildConfigLabel(ConfigDefinition definition)
        {
            if (definition == null) return "<unknown>";
            if (string.IsNullOrWhiteSpace(definition.Section)) return definition.Key ?? "<unknown>";
            return definition.Section + "." + definition.Key;
        }

        internal static void TrackConfigEntry(ConfigFile configFile, ConfigEntryBase entry)
        {
            if (configFile == null || entry == null) return;

            EnsureConfigFileHooked(configFile);

            if (!ConfigEntriesByFile.TryGetValue(configFile, out var entries))
            {
                entries = new Dictionary<string, ConfigEntryBase>(StringComparer.Ordinal);
                ConfigEntriesByFile[configFile] = entries;
            }

            entries[BuildDefinitionKey(entry.Definition)] = entry;
        }

        private static void DiscoverLoadedConfigEntries()
        {
            try
            {
                // ConfigFile.Add is a throw-only compatibility method in BepInEx 5.4.20 and is unsafe to detour.
                // Inspect the public config contents instead when options are registered or the settings menu loads.
                foreach (var plugin in Resources.FindObjectsOfTypeAll<BaseUnityPlugin>())
                {
                    var configFile = plugin?.Config;
                    if (configFile == null) continue;

                    foreach (var entry in ((IDictionary<ConfigDefinition, ConfigEntryBase>)configFile).Values)
                        TrackConfigEntry(configFile, entry);
                }
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogDebug($"CUCoreLib settings sync could not inspect loaded plugin configs: {ex.Message}");
            }
        }

        internal static void RefreshLoadedConfigEntries()
        {
            DiscoverLoadedConfigEntries();

            foreach (var state in StatesById.Values)
            {
                TryAttachTrackedConfigEntry(state);
                TryLinkState(state);
            }
        }

        private static bool TryConvertConfigValueToSetting(
            ModOptionDefinition option,
            object rawValue,
            out object convertedValue,
            out string error)
        {
            convertedValue = null;
            error = null;

            switch (option.Kind)
            {
                case ModOptionKind.Bool:
                    if (TryConvertToBool(rawValue, out var boolValue))
                    {
                        convertedValue = boolValue;
                        return true;
                    }
                    error = DescribeExpectedValue("bool", rawValue);
                    return false;
                case ModOptionKind.Int:
                    if (TryConvertToInt(rawValue, out var intValue))
                    {
                        convertedValue = Mathf.Clamp(intValue, Mathf.RoundToInt(option.Min), Mathf.RoundToInt(option.Max));
                        return true;
                    }
                    error = DescribeExpectedValue("int", rawValue);
                    return false;
                case ModOptionKind.Float:
                    if (TryConvertToFloat(rawValue, out var floatValue))
                    {
                        convertedValue = Mathf.Clamp(floatValue, option.Min, option.Max);
                        return true;
                    }
                    error = DescribeExpectedValue("float", rawValue);
                    return false;
                case ModOptionKind.Dropdown:
                    if (TryConvertToDropdownIndex(option, rawValue, out var index, out error))
                    {
                        convertedValue = index;
                        return true;
                    }
                    return false;
                case ModOptionKind.Keybind:
                    if (TryConvertToKeyCode(rawValue, out var keyCode))
                    {
                        convertedValue = keyCode;
                        return true;
                    }
                    error = DescribeExpectedValue("keybind", rawValue);
                    return false;
                default:
                    error = $"unsupported option kind '{option.Kind}'.";
                    return false;
            }
        }

        private static bool TryConvertSettingValueToConfig(
            ModOptionDefinition option,
            object rawValue,
            Type targetType,
            out object convertedValue,
            out string error)
        {
            convertedValue = null;
            error = null;
            if (targetType == null) targetType = typeof(object);

            switch (option.Kind)
            {
                case ModOptionKind.Bool:
                    if (!TryConvertToBool(rawValue, out var boolValue))
                    {
                        error = DescribeExpectedValue("bool", rawValue);
                        return false;
                    }

                    if (targetType == typeof(string))
                        convertedValue = boolValue.ToString();
                    else if (targetType == typeof(bool) || targetType == typeof(object))
                        convertedValue = boolValue;
                    else
                        convertedValue = Convert.ChangeType(boolValue, targetType, CultureInfo.InvariantCulture);
                    return true;
                case ModOptionKind.Int:
                    if (!TryConvertToInt(rawValue, out var intValue))
                    {
                        error = DescribeExpectedValue("int", rawValue);
                        return false;
                    }

                    if (targetType == typeof(string))
                        convertedValue = intValue.ToString(CultureInfo.InvariantCulture);
                    else if (targetType == typeof(int) || targetType == typeof(object))
                        convertedValue = intValue;
                    else if (targetType == typeof(float))
                        convertedValue = (float)intValue;
                    else
                        convertedValue = Convert.ChangeType(intValue, targetType, CultureInfo.InvariantCulture);
                    return true;
                case ModOptionKind.Float:
                    if (!TryConvertToFloat(rawValue, out var floatValue))
                    {
                        error = DescribeExpectedValue("float", rawValue);
                        return false;
                    }

                    if (targetType == typeof(string))
                        convertedValue = floatValue.ToString(CultureInfo.InvariantCulture);
                    else if (targetType == typeof(float) || targetType == typeof(object))
                        convertedValue = floatValue;
                    else if (targetType == typeof(int))
                        convertedValue = Mathf.RoundToInt(floatValue);
                    else
                        convertedValue = Convert.ChangeType(floatValue, targetType, CultureInfo.InvariantCulture);
                    return true;
                case ModOptionKind.Dropdown:
                    if (!TryConvertToInt(rawValue, out var dropdownIndex))
                    {
                        error = DescribeExpectedValue("dropdown index", rawValue);
                        return false;
                    }

                    if (dropdownIndex < 0 || option.Choices == null || dropdownIndex >= option.Choices.Length)
                    {
                        error = $"dropdown index {dropdownIndex} was outside the available choice range.";
                        return false;
                    }

                    if (targetType == typeof(string))
                        convertedValue = option.Choices[dropdownIndex].Key;
                    else if (targetType == typeof(int) || targetType == typeof(object))
                        convertedValue = dropdownIndex;
                    else
                        convertedValue = Convert.ChangeType(dropdownIndex, targetType, CultureInfo.InvariantCulture);
                    return true;
                case ModOptionKind.Keybind:
                    if (!TryConvertToKeyCode(rawValue, out var keyCode))
                    {
                        error = DescribeExpectedValue("keybind", rawValue);
                        return false;
                    }

                    if (targetType == typeof(string))
                        convertedValue = keyCode.ToString();
                    else if (targetType == typeof(int))
                        convertedValue = (int)keyCode;
                    else if (targetType == typeof(KeyCode) || targetType == typeof(object))
                        convertedValue = keyCode;
                    else
                        convertedValue = Convert.ChangeType((int)keyCode, targetType, CultureInfo.InvariantCulture);
                    return true;
                default:
                    error = $"unsupported option kind '{option.Kind}'.";
                    return false;
            }
        }

        private static bool TryConvertToBool(object rawValue, out bool value)
        {
            switch (rawValue)
            {
                case bool boolValue:
                    value = boolValue;
                    return true;
                case string text when bool.TryParse(text, out value):
                    return true;
                case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt):
                    value = parsedInt != 0;
                    return true;
                case byte byteValue:
                    value = byteValue != 0;
                    return true;
                case sbyte sbyteValue:
                    value = sbyteValue != 0;
                    return true;
                case short shortValue:
                    value = shortValue != 0;
                    return true;
                case ushort ushortValue:
                    value = ushortValue != 0;
                    return true;
                case int intValue:
                    value = intValue != 0;
                    return true;
                case uint uintValue:
                    value = uintValue != 0;
                    return true;
                case long longValue:
                    value = longValue != 0;
                    return true;
                case ulong ulongValue:
                    value = ulongValue != 0;
                    return true;
                case float floatValue:
                    value = Mathf.Abs(floatValue) > float.Epsilon;
                    return true;
                case double doubleValue:
                    value = Math.Abs(doubleValue) > double.Epsilon;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private static bool TryConvertToInt(object rawValue, out int value)
        {
            switch (rawValue)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case float floatValue:
                    value = Mathf.RoundToInt(floatValue);
                    return true;
                case double doubleValue:
                    value = Convert.ToInt32(Math.Round(doubleValue, MidpointRounding.AwayFromZero));
                    return true;
                case bool boolValue:
                    value = boolValue ? 1 : 0;
                    return true;
                case KeyCode keyCode:
                    value = (int)keyCode;
                    return true;
                case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value):
                    return true;
                case string text when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFloat):
                    value = Mathf.RoundToInt(parsedFloat);
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }

        private static bool TryConvertToFloat(object rawValue, out float value)
        {
            switch (rawValue)
            {
                case float floatValue:
                    value = floatValue;
                    return true;
                case double doubleValue:
                    value = (float)doubleValue;
                    return true;
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue:
                    value = longValue;
                    return true;
                case bool boolValue:
                    value = boolValue ? 1f : 0f;
                    return true;
                case string text when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value):
                    return true;
                case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt):
                    value = parsedInt;
                    return true;
                default:
                    value = 0f;
                    return false;
            }
        }

        private static bool TryConvertToKeyCode(object rawValue, out KeyCode value)
        {
            switch (rawValue)
            {
                case KeyCode keyCode:
                    value = keyCode;
                    return true;
                case int intValue:
                    value = (KeyCode)intValue;
                    return true;
                case string text when Enum.TryParse(text, true, out KeyCode parsedKeyCode):
                    value = parsedKeyCode;
                    return true;
                case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt):
                    value = (KeyCode)parsedInt;
                    return true;
                default:
                    value = KeyCode.None;
                    return false;
            }
        }

        private static bool TryConvertToDropdownIndex(
            ModOptionDefinition option,
            object rawValue,
            out int value,
            out string error)
        {
            error = null;

            if (TryConvertToInt(rawValue, out value))
            {
                if (option.Choices != null && value >= 0 && value < option.Choices.Length) return true;
                error = $"dropdown index {value} was outside the available choice range.";
                return false;
            }

            if (option.Choices != null && rawValue is string text)
            {
                for (var i = 0; i < option.Choices.Length; i++)
                {
                    if (string.Equals(option.Choices[i].Key, text, StringComparison.OrdinalIgnoreCase))
                    {
                        value = i;
                        return true;
                    }
                }
            }

            error = DescribeExpectedValue("dropdown index or choice key", rawValue);
            value = 0;
            return false;
        }

        private static object GetSettingValue(Setting setting, ModOptionKind kind)
        {
            switch (kind)
            {
                case ModOptionKind.Float:
                    return setting is SettingFloat floatSetting ? (object)floatSetting.value : null;
                case ModOptionKind.Int:
                    return setting is SettingInt intSetting ? (object)intSetting.value : null;
                case ModOptionKind.Bool:
                    return setting is SettingBool boolSetting ? (object)boolSetting.value : null;
                case ModOptionKind.Dropdown:
                    return setting is SettingDropdown dropdownSetting ? (object)dropdownSetting.value : null;
                case ModOptionKind.Keybind:
                    return setting is SettingKeybind keybindSetting ? (object)keybindSetting.value : null;
                default:
                    return null;
            }
        }

        private static void SetSettingValue(Setting setting, ModOptionKind kind, object value)
        {
            switch (kind)
            {
                case ModOptionKind.Float:
                    if (setting is SettingFloat floatSetting)
                        floatSetting.value = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                    break;
                case ModOptionKind.Int:
                    if (setting is SettingInt intSetting)
                        intSetting.value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    break;
                case ModOptionKind.Bool:
                    if (setting is SettingBool boolSetting)
                        boolSetting.value = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                    break;
                case ModOptionKind.Dropdown:
                    if (setting is SettingDropdown dropdownSetting)
                        dropdownSetting.value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    break;
                case ModOptionKind.Keybind:
                    if (setting is SettingKeybind keybindSetting)
                    {
                        if (value is KeyCode keyCode)
                            keybindSetting.value = keyCode;
                        else
                            keybindSetting.value = (KeyCode)Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    }

                    break;
            }
        }

        private static string DescribeExpectedValue(string expected, object rawValue)
        {
            if (rawValue == null) return $"expected {expected}, but value was null.";
            return $"expected {expected}, but got '{rawValue}' ({rawValue.GetType().Name}).";
        }
    }
}
