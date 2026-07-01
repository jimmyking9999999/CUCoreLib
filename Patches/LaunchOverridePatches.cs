using CUCoreLib.Helpers;
using HarmonyLib;

namespace CUCoreLib.Patches;

[HarmonyPatch(typeof(PreRunScript))]
internal static class LaunchOverridePatches
{
    [HarmonyPatch("Start")]
    [HarmonyPostfix]
    private static void PreRunScript_Start_Postfix(PreRunScript __instance)
    {
        LaunchOverrideManager.TryConsumeMenuLaunchOverride(__instance);
    }

    [HarmonyPatch(typeof(TutorialHandler), "Start")]
    [HarmonyPostfix]
    private static void TutorialHandler_Start_Postfix(TutorialHandler __instance)
    {
        LaunchOverrideManager.TryConsumePendingSandboxCourse(__instance);
    }
}