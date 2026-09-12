using System.Collections.Generic;
using System.Linq;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    internal static class LocalePatch
    {
        [HarmonyPatch(typeof(Locale), nameof(Locale.GetOther), typeof(string))]
        [HarmonyPrefix]
        private static bool HateGameset(string __0, ref string __result)
        {
            if (!ModOptionsRegistry.TryGetLocalizedText(__0, out var localizedText))
                return true;

            __result = localizedText;
            return false;
        }
    }

    internal static class ModOptionsPatches
    {
        [HarmonyPatch(typeof(Settings), nameof(Settings.DefaultSettings))]
        [HarmonyPostfix]
        private static void AppendRegisteredOptions(List<Setting> __result)
        {
            ModOptionsRegistry.AppendRegisteredOptions(__result);
            ModSettingsConfigSyncRegistry.RefreshLoadedConfigEntries();
        }
    }

    internal static class SettingsMenuStartPatch
    {
        [HarmonyPatch(typeof(SettingsMenu), "Start")]
        [HarmonyPostfix]
        private static void Postfix(SettingsMenu __instance)
        {
            ModSettingsConfigSyncRegistry.RefreshLoadedConfigEntries();
            SettingsMenuCategoryExtender.EnsureAttached(__instance);
            var helper = __instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.OnTabSelected(Setting.SettingCategory.Video);
        }
    }

    internal static class SettingsMenuSelectTabPatch
    {
        [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.SelectTab), typeof(Setting.SettingCategory))]
        [HarmonyPostfix]
        private static void Postfix(SettingsMenu __instance, Setting.SettingCategory category)
        {
            SettingsMenuCategoryExtender.EnsureAttached(__instance);
            var helper = __instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.OnTabSelected(category);

            if (__instance && __instance.content && helper)
                helper.FixDropdownsInContent(__instance.content);

            RestoreRegisteredKeybindTooltips(__instance, category);
        }

        private static void RestoreRegisteredKeybindTooltips(SettingsMenu menu, Setting.SettingCategory category)
        {
            if (!menu || !menu.content) return;

            var displayedSettingIndex = 0;
            foreach (var setting in Settings.GetAllSettings().Where(setting => setting != null && setting.category == category))
            {
                if (displayedSettingIndex >= menu.content.childCount) return;

                if (setting is SettingKeybind)
                {
                    var option = ModOptionsRegistry.RegisteredOptions.Find(candidate =>
                        candidate != null && candidate.Id == setting.name && candidate.Kind == ModOptionKind.Keybind);
                    if (option != null && !string.IsNullOrWhiteSpace(option.Description))
                    {
                        var tooltipTarget = menu.content.GetChild(displayedSettingIndex).GetChild(0);
                        var tooltip = tooltipTarget.GetComponent<UITooltip>();
                        if (tooltip != null)
                            tooltip.tipDesc = option.Description;
                    }
                }

                displayedSettingIndex++;
            }
        }
    }

    internal static class KeyBindsGetBindNamePatch
    {
        [HarmonyPatch(typeof(KeyBinds), nameof(KeyBinds.GetBindName), typeof(string))]
        [HarmonyPrefix]
        private static bool Prefix(string action, ref string __result)
        {
            if (!CUCoreUtils.TryGetFriendlyBindDisplayName(action, out __result))
                return true;

            return false;
        }
    }

    internal static class KeyBindsGetBindPatch
    {
        [HarmonyPatch(typeof(KeyBinds), nameof(KeyBinds.GetBind), typeof(string))]
        [HarmonyPrefix]
        private static bool Prefix(string action, ref KeyCode __result)
        {
            if (!CUCoreUtils.TryGetFriendlyBindKeyCode(action, out __result))
                return true;

            return false;
        }
    }
}