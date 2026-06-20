using System.Collections.Generic;
using HarmonyLib;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(Locale), "GetString")]
    internal static class LocalePatches
    {
        [HarmonyPrefix]
        private static bool InterceptLocale(string str, int type, ref string __result)
        {
            if (Locale.currentLang != null)
            {
                Dictionary<string, string> section = type == 0 ? Locale.currentLang.main :
                    type == 1 ? Locale.currentLang.buildings :
                    type == 2 ? Locale.currentLang.moodles :
                    Locale.currentLang.other;

                if (section != null && section.TryGetValue(str, out string localizedText) && !string.IsNullOrWhiteSpace(localizedText))
                {
                    __result = localizedText;
                    return false;
                }
            }

            if (LocaleRegistry.CustomLocales.TryGetValue(type, out var dict) &&
                dict.TryGetValue(str, out string fallbackText) &&
                !string.IsNullOrWhiteSpace(fallbackText))
            {
                __result = fallbackText;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Locale), "LoadLanguage")]
    internal static class LocaleLoadPatches
    {
        [HarmonyPostfix]
        private static void ApplyLocaleOverlays()
        {
            LocaleLoader.ApplyActiveLocaleOverlay();
        }
    }
}
