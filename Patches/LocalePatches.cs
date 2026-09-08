using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

    [HarmonyPatch(typeof(Locale), nameof(Locale.GetCharacter), typeof(string), typeof(int))]
    internal static class LocaleCharacterDialoguePatch
    {
        private sealed class DialogueSource
        {
            internal string Id;
        }

        private static readonly ConditionalWeakTable<List<string>, DialogueSource> DialogueSources =
            new ConditionalWeakTable<List<string>, DialogueSource>();

        [HarmonyPostfix]
        private static void Postfix(string str, List<string> __result)
        {
            if (__result == null || string.IsNullOrWhiteSpace(str)) return;

            DialogueSources.Remove(__result);
            DialogueSources.Add(__result, new DialogueSource { Id = str });
        }

        internal static string GetDialogueId(List<string> lines)
        {
            return lines != null && DialogueSources.TryGetValue(lines, out var source) ? source.Id : null;
        }
    }

    [HarmonyPatch(typeof(Talker), nameof(Talker.Talk), typeof(List<string>), typeof(Limb), typeof(bool), typeof(bool))]
    internal static class TalkerDialoguePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Talker __instance, bool force, List<string> lines, out bool __state)
        {
            __state = __instance != null && lines != null && lines.Count > 0;
            if (!__state) return;

            var body = __instance.body;
            if (body != null &&
                (!body.conscious || !(body.brainHealth > 50f) || body.mindWipe != null ||
                 ((body.totalHappiness < -75f || body.traumaAmount > 80f) && !force)))
                __state = false;
        }

        [HarmonyPostfix]
        private static void Postfix(Talker __instance, List<string> lines, bool __state, string ___currentString)
        {
            if (!__state || __instance == null || string.IsNullOrEmpty(___currentString)) return;

            CUCoreUtils.SetLastDialogue(LocaleCharacterDialoguePatch.GetDialogueId(lines), ___currentString);
        }
    }
}
