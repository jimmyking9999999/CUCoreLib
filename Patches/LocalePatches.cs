using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches;

[HarmonyPatch(typeof(Locale))]
internal static class LocalePatches
{
    [HarmonyPatch("GetString")]
    [HarmonyPrefix]
    private static bool InterceptLocale(string str, int type, ref string __result)
    {
        if (Locale.currentLang != null)
        {
            var section = type switch
            {
                0 => Locale.currentLang.main,
                1 => Locale.currentLang.buildings,
                2 => Locale.currentLang.moodles,
                _ => Locale.currentLang.other
            };

            if (section != null && section.TryGetValue(str, out var localizedText) &&
                !string.IsNullOrWhiteSpace(localizedText))
            {
                __result = localizedText;
                return false;
            }
        }

        if (!LocaleRegistry.CustomLocales.TryGetValue(type, out var dict) ||
            !dict.TryGetValue(str, out var fallbackText) ||
            string.IsNullOrWhiteSpace(fallbackText)) return true;
        __result = fallbackText;
        return false;
    }
    
    [HarmonyPatch("LoadLanguage")]
    [HarmonyPostfix]
    private static void ApplyLocaleOverlays()
    {
        LocaleLoader.ApplyActiveLocaleOverlay();
    }
}