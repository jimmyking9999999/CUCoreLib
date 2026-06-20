using System.Collections.Generic;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(Settings), nameof(Settings.DefaultSettings))]
    internal static class ModOptionsPatches
    {
        [HarmonyPostfix]
        private static void AppendRegisteredOptions(List<Setting> __result)
        {
            ModOptionsRegistry.AppendRegisteredOptions(__result);
        }
    }

    [HarmonyPatch(typeof(SettingsMenu), "Start")]
    internal static class SettingsMenuStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SettingsMenu __instance)
        {
            SettingsMenuCategoryExtender.EnsureAttached(__instance);
            SettingsMenuCategoryExtender helper = __instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.OnTabSelected(Setting.SettingCategory.Video);
        }
    }

    [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.SelectTab), typeof(Setting.SettingCategory))]
    internal static class SettingsMenuSelectTabPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SettingsMenu __instance, Setting.SettingCategory category)
        {
            SettingsMenuCategoryExtender.EnsureAttached(__instance);
            SettingsMenuCategoryExtender helper = __instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.OnTabSelected(category);
        }
    }
}
