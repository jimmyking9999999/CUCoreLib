using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;

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
                var section = type == 0 ? Locale.currentLang.main :
                    type == 1 ? Locale.currentLang.buildings :
                    type == 2 ? Locale.currentLang.moodles :
                    Locale.currentLang.other;

                if (section != null && section.TryGetValue(str, out var localizedText) &&
                    !string.IsNullOrWhiteSpace(localizedText))
                {
                    __result = localizedText;
                    return false;
                }
            }

            if (TryGetCustomLocaleText(type, str, out var fallbackText))
            {
                __result = fallbackText;
                return false;
            }

            return true;
        }

        private static bool TryGetCustomLocaleText(int type, string key, out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            if (TryGetCustomLocaleTextForType(type, key, out text)) return true;

            // Some vanilla surfaces resolve custom content through Locale.GetOther(...)
            // even though CUCoreLib exports those keys into dedicated sections.
            if (type != (int)LocaleRegistry.LocaleCategory.Other) return false;

            return TryGetCustomLocaleTextForType((int)LocaleRegistry.LocaleCategory.Liquid, key, out text) ||
                   TryGetCustomLocaleTextForType((int)LocaleRegistry.LocaleCategory.Title, key, out text);
        }

        private static bool TryGetCustomLocaleTextForType(int type, string key, out string text)
        {
            text = null;

            if (!LocaleRegistry.CustomLocales.TryGetValue(type, out var dict) ||
                !dict.TryGetValue(key, out var fallbackText) ||
                string.IsNullOrWhiteSpace(fallbackText)) return false;

            text = fallbackText;
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