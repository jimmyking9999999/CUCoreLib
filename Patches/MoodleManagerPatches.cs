using HarmonyLib;
using CUCoreLib.Registries;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;
using CUCoreLib.Helpers;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(MoodleManager), "AddAllMoodles")]
    internal static class MoodleManagerPatches
    {
        [HarmonyPrefix]
        private static void AddAllMoodles_Prefix(MoodleManager __instance)
        {
            Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (__instance == null || body == null)
            {
                return;
            }

            MoodleRegistry.AddQueuedMoodles(__instance, important: true);
            MoodleRegistry.AddBodyMoodles(__instance, body, important: true);

            Limb[] limbs = body.limbs;
            if (limbs == null)
            {
                return;
            }

            foreach (Limb limb in limbs)
            {
                if (limb == null)
                {
                    continue;
                }

                MoodleRegistry.AddLimbMoodles(__instance, limb, important: true);
            }
        }

        // Sadly needed to ensure custom moodles can be placed in both the important and nonimportant moodles
        // PRs welcome :)
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> AddAllMoodles_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                CodeInstruction instruction = codes[i];
                yield return instruction;

                if (instruction.opcode == OpCodes.Ldc_I4_1 && i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Stfld && codes[i + 1].operand is FieldInfo field && field.Name == "sideMoodles")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.Call(typeof(MoodleManagerPatches), nameof(AddSideCustomMoodles));
                }
            }
        }

        private static void AddSideCustomMoodles(MoodleManager manager)
        {
            Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (manager == null || body == null)
            {
                return;
            }

            MoodleRegistry.AddQueuedMoodles(manager, important: false);
            MoodleRegistry.AddBodyMoodles(manager, body, important: false);

            Limb[] limbs = body.limbs;
            if (limbs == null)
            {
                return;
            }

            foreach (Limb limb in limbs)
            {
                if (limb == null)
                {
                    continue;
                }

                MoodleRegistry.AddLimbMoodles(manager, limb, important: false);
            }
        }

        [HarmonyPatch(typeof(Moodle), "Start")]
        [HarmonyPostfix]
        private static void ApplyMoodleAnimation(Moodle __instance)
        {
            if (__instance == null || string.IsNullOrWhiteSpace(__instance.type))
            {
                return;
            }

            string iconKey = __instance.type;
            for (int i = iconKey.Length - 1; i >= 0; i--)
            {
                if (!char.IsDigit(iconKey[i]))
                {
                    iconKey = iconKey.Substring(0, i + 1);
                    break;
                }
            }

            if (!MoodleRegistry.TryGetAnimationId(iconKey, out string animationId) || string.IsNullOrWhiteSpace(animationId))
            {
                return;
            }

            if (__instance.transform.childCount == 0)
            {
                return;
            }

            Image image = __instance.transform.GetChild(0).GetComponent<Image>();
            AssetLoader.TryApplyAnimation(image, animationId);
        }
    }
}
