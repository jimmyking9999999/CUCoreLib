using System.Collections.Generic;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    /// <summary>
    ///     The game's SettingsMenu hardcodes "gameset" prefix when looking up
    ///     setting locale strings (e.g. Locale.GetOther("gameset" + setting.name)).
    ///     This patch intercepts Locale.GetOther so that keys starting with
    ///     "gameset" first try the clean (unprefixed) key in Language.other.
    ///     If found there, it returns immediately; otherwise the original
    ///     method runs, preserving built-in game settings.
    /// </summary>
    [HarmonyPatch(typeof(Locale), nameof(Locale.GetOther), typeof(string))]
    internal static class LocalePatch
    {
        [HarmonyPrefix]
        private static bool HateGameset(string __0, ref string __result)
        {
            if (string.IsNullOrEmpty(__0) || !__0.StartsWith("gameset"))
                return true;

            var language = Locale.currentLang;
            if (language?.other == null)
                return true;

            var cleanKey = __0.Substring("gameset".Length);
            if (!language.other.TryGetValue(cleanKey, out var value)
                || string.IsNullOrWhiteSpace(value)) return true;
            __result = value;
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
        }
    }

    [HarmonyPatch(typeof(SettingsMenu), "Start")]
    internal static class SettingsMenuStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SettingsMenu __instance)
        {
            SettingsMenuCategoryExtender.EnsureAttached(__instance);
            var helper = __instance.GetComponent<SettingsMenuCategoryExtender>();
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
            var helper = __instance.GetComponent<SettingsMenuCategoryExtender>();
            helper?.OnTabSelected(category);

            if (__instance && __instance.content && helper)
                helper.FixDropdownsInContent(__instance.content);
        }
    }
}