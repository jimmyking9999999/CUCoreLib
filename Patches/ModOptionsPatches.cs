using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(Locale), nameof(Locale.GetOther), typeof(string))]
    internal static class LocalePatch
    {
        [HarmonyPrefix]
        private static bool HateGameset(string __0, ref string __result)
        {
            if (!ModOptionsRegistry.TryGetLocalizedText(__0, out var localizedText))
                return true;

            __result = localizedText;
            return false;
        }
    }

    [HarmonyPatch(typeof(Settings), nameof(Settings.DefaultSettings))]
    internal static class ModOptionsPatches
    {
        [HarmonyPostfix]
        private static void AppendRegisteredOptions(List<Setting> __result)
        {
            ModOptionsRegistry.AppendRegisteredOptions(__result);
            ModSettingsConfigSyncRegistry.RefreshLoadedConfigEntries();
        }
    }

    [HarmonyPatch(typeof(SettingsMenu), "Start")]
    internal static class SettingsMenuStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SettingsMenu __instance)
        {
            BepInExConfigAutoRegistrar.RunOnce();
            ModSettingsConfigSyncRegistry.RefreshLoadedConfigEntries();
            SettingsMenuCategoryExtender.EnsureAttached(__instance);
            var helper = __instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.OnTabSelected(Setting.SettingCategory.Video);
        }
    }

    [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.SelectTab),
        typeof(Setting.SettingCategory))]
    internal static class SettingsMenuSelectTabPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SettingsMenu __instance,
            Setting.SettingCategory category,
            List<GameObject> ___spawnedSettings)
        {
            SettingsMenuCategoryExtender.EnsureAttached(__instance);
            var helper = __instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.OnTabSelected(category);

            if (__instance && __instance.content && helper)
                helper.FixDropdownsInContent(__instance.content);

            RestoreRegisteredKeybindTooltips(___spawnedSettings, category);
        }

        private static void RestoreRegisteredKeybindTooltips(
            List<GameObject> spawnedSettings, Setting.SettingCategory category)
        {
            if (spawnedSettings == null) return;

            // Content still contains the previous tab's rows until Unity finishes deferred destruction.
            var displayedSettingIndex = 0;
            foreach (var setting in Settings.GetAllSettings().Where(setting =>
                setting != null && setting.category == category))
            {
                if (displayedSettingIndex >= spawnedSettings.Count) return;

                if (setting is SettingKeybind)
                {
                    var option = ModOptionsRegistry.RegisteredOptions.Find(candidate =>
                        candidate != null
                        && candidate.Id == setting.name
                        && candidate.Kind == ModOptionKind.Keybind);
                    if (option != null && !string.IsNullOrWhiteSpace(option.Description))
                    {
                        var row = spawnedSettings[displayedSettingIndex];
                        if (!row || row.transform.childCount == 0)
                        {
                            displayedSettingIndex++;
                            continue;
                        }

                        var tooltipTarget = row.transform.GetChild(0);
                        var tooltip = tooltipTarget.GetComponent<UITooltip>();
                        if (tooltip != null)
                            tooltip.tipDesc =
                                Locale.GetOther("gameset" + setting.name + "dsc");
                    }
                }

                displayedSettingIndex++;
            }
        }
    }

    [HarmonyPatch(typeof(KeyBinds), nameof(KeyBinds.GetBindName), typeof(string))]
    internal static class KeyBindsGetBindNamePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(string action, ref string __result)
        {
            if (!CUCoreUtils.TryGetFriendlyBindDisplayName(action, out __result))
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(KeyBinds), nameof(KeyBinds.GetBind), typeof(string))]
    internal static class KeyBindsGetBindPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(string action, ref KeyCode __result)
        {
            if (!CUCoreUtils.TryGetFriendlyBindKeyCode(action, out __result))
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.SelectTab),
        typeof(Setting.SettingCategory))]
    internal static class SettingsMenuSelectTabStringPatch
    {
        private static readonly List<SettingString> DeferredStringSettings =
            new List<SettingString>();

        [HarmonyPrefix]
        private static void Prefix(Setting.SettingCategory category)
        {
            DeferredStringSettings.Clear();
            if (Settings.settings == null) return;

            for (var i = Settings.settings.Count - 1; i >= 0; i--)
                if (Settings.settings[i] is SettingString stringSetting
                    && stringSetting.category == category)
                {
                    DeferredStringSettings.Add(stringSetting);
                    Settings.settings.RemoveAt(i);
                }

            DeferredStringSettings.Reverse();
        }

        [HarmonyPostfix]
        private static void Postfix(
            SettingsMenu __instance,
            Setting.SettingCategory category,
            List<GameObject> ___spawnedSettings)
        {
            if (DeferredStringSettings.Count == 0
                || __instance == null
                || !__instance.content
                || ___spawnedSettings == null) return;

            var num = ___spawnedSettings.Where(spawned => spawned)
                .Select(spawned => spawned.GetComponent<RectTransform>())
                .Where(rect => rect).Sum(rect => rect.sizeDelta.y);

            foreach (var stringSetting in DeferredStringSettings)
            {
                var g = Utils.Create("Special/GameSettingInt", __instance.content);
                var inputField = g.transform.GetChild(1).GetComponent<TMP_InputField>();
                if (inputField)
                {
                    inputField.SetTextWithoutNotify(stringSetting.value ?? string.Empty);
                    inputField.contentType = TMP_InputField.ContentType.Standard;
                    inputField.onEndEdit.RemoveAllListeners();
                    inputField.onEndEdit.AddListener(delegate(string text)
                    {
                        stringSetting.value = text ?? string.Empty;
                        stringSetting.Apply();
                    });

                    var option = ModOptionsRegistry.RegisteredOptions.Find(candidate =>
                        candidate != null
                        && candidate.Id == stringSetting.name
                        && candidate.Kind == ModOptionKind.Text);
                    if (option != null
                        && !string.IsNullOrWhiteSpace(option.Placeholder)
                        && inputField.placeholder)
                    {
                        var placeholderText =
                            inputField.placeholder.GetComponent<TMP_Text>();
                        if (placeholderText) placeholderText.text = option.Placeholder;
                    }
                }

                g.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text =
                    Locale.GetOther("gameset" + stringSetting.name);
                var tooltip = g.transform.GetChild(0).gameObject
                    .AddComponent<UITooltip>();
                tooltip.skipLocale = true;
                tooltip.tipName = Locale.GetOther("gameset" + stringSetting.name);
                tooltip.tipDesc = Locale.GetOther("gameset" + stringSetting.name + "dsc");

                g.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(0f,
                        0f - num - g.GetComponent<RectTransform>().sizeDelta.y * 0.5f);
                num += g.GetComponent<RectTransform>().sizeDelta.y;
                ___spawnedSettings.Add(g);
            }

            __instance.content.sizeDelta =
                new Vector2(__instance.content.sizeDelta.x, num);

            foreach (var stringSetting in DeferredStringSettings.Where(stringSetting =>
                !Settings.settings.Contains(stringSetting)))
                Settings.settings.Add(stringSetting);

            DeferredStringSettings.Clear();
        }
    }

    [HarmonyPatch(typeof(Settings), nameof(Settings.LoadSettings))]
    internal static class SettingsLoadStringPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            var path = Application.persistentDataPath + "/settings.json";
            if (!File.Exists(path)) return;

            try
            {
                var savedData =
                    JsonConvert.DeserializeObject<List<SettingSaveData>>(
                        File.ReadAllText(path));
                if (savedData == null) return;

                foreach (var data in savedData)
                {
                    if (data?.name == null) continue;

                    var setting =
                        Settings.settings?.Find(s => s != null && s.name == data.name);
                    if (setting is SettingString stringSetting)
                        stringSetting.value = data.value?.ToString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogError(
                    $"Failed to load string settings: {ex.Message}");
            }
        }
    }
}
