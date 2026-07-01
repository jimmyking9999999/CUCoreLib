using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches;

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

        if (!LocaleRegistry.CustomLocales.TryGetValue(type, out var dict) ||
            !dict.TryGetValue(str, out var fallbackText) ||
            string.IsNullOrWhiteSpace(fallbackText)) return true;
        __result = fallbackText;
        return false;
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