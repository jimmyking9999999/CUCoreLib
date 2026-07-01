using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine.UI;

namespace CUCoreLib.Patches;

[HarmonyPatch(typeof(MoodleManager))]
internal static class MoodleManagerPatches
{
    [HarmonyPatch("AddAllMoodles")]
    [HarmonyPrefix]
    private static void AddAllMoodles_Prefix(MoodleManager __instance)
    {
        var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
        if (__instance == null || body == null) return;

        MoodleRegistry.AddQueuedMoodles(__instance, true);
        MoodleRegistry.AddBodyMoodles(__instance, body, true);

        var limbs = body.limbs;
        if (limbs == null) return;

        foreach (var limb in limbs)
        {
            if (limb == null) continue;

            MoodleRegistry.AddLimbMoodles(__instance, limb, true);
        }
    }

    // Sadly needed to ensure custom moodles can be placed in both the important and nonimportant moodles
    // PRs welcome :)
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AddAllMoodles_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        for (var i = 0; i < codes.Count; i++)
        {
            var instruction = codes[i];
            yield return instruction;

            if (instruction.opcode != OpCodes.Ldc_I4_1
                || i + 1 >= codes.Count
                || codes[i + 1].opcode != OpCodes.Stfld 
                || codes[i + 1].operand is not FieldInfo { Name: "sideMoodles" }) continue;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return CodeInstruction.Call(typeof(MoodleManagerPatches), nameof(AddSideCustomMoodles));
        }
    }

    private static void AddSideCustomMoodles(MoodleManager manager)
    {
        var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
        if (manager == null || body == null) return;

        MoodleRegistry.AddQueuedMoodles(manager, false);
        MoodleRegistry.AddBodyMoodles(manager, body, false);

        var limbs = body.limbs;
        if (limbs == null) return;

        foreach (var limb in limbs)
        {
            if (limb == null) continue;

            MoodleRegistry.AddLimbMoodles(manager, limb, false);
        }
    }

    [HarmonyPatch(typeof(Moodle), "Start")]
    [HarmonyPostfix]
    private static void ApplyMoodleAnimation(Moodle __instance)
    {
        if (__instance == null || string.IsNullOrWhiteSpace(__instance.type)) return;

        var iconKey = __instance.type;
        for (var i = iconKey.Length - 1; i >= 0; i--)
            if (!char.IsDigit(iconKey[i]))
            {
                iconKey = iconKey.Substring(0, i + 1);
                break;
            }

        if (!MoodleRegistry.TryGetAnimationId(iconKey, out var animationId) ||
            string.IsNullOrWhiteSpace(animationId)) return;

        if (__instance.transform.childCount == 0) return;

        var image = __instance.transform.GetChild(0).GetComponent<Image>();
        AssetLoader.TryApplyAnimation(image, animationId);
    }
}