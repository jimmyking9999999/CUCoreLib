using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class BodyFormulaPatches
    {
        private static readonly Dictionary<string, MethodInfo> PeriodicReplacements =
            new Dictionary<string, MethodInfo>
            {
                ["maxEncumberance"] = AccessTools.Method(typeof(BodyFormulaPatches), nameof(SetMaxEncumberance)),
                ["totalEncumberance"] = AccessTools.Method(typeof(BodyFormulaPatches), nameof(SetTotalEncumberance)),
                ["immunity"] = AccessTools.Method(typeof(BodyFormulaPatches), nameof(SetImmunity))
            };

        private static readonly MethodInfo FloatLerpMethod =
            AccessTools.Method(typeof(Mathf), nameof(Mathf.Lerp), new[] { typeof(float), typeof(float), typeof(float) });

        private static readonly MethodInfo FloatMoveTowardsMethod =
            AccessTools.Method(typeof(Mathf), nameof(Mathf.MoveTowards),
                new[] { typeof(float), typeof(float), typeof(float) });

        [HarmonyPatch(typeof(Body), "HandleCirculation")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> HandleCirculation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                CodeInstruction instruction = codes[i];
                if (instruction.Calls(FloatMoveTowardsMethod) &&
                    TryFindStoredBodyField(codes, i + 1, out string fieldName) &&
                    string.Equals(fieldName, "respiratoryRate", System.StringComparison.Ordinal))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(BodyFormulaPatches), nameof(AdjustRespiratoryRateTarget)));
                    continue;
                }

                if (instruction.Calls(FloatLerpMethod) &&
                    TryFindStoredBodyField(codes, i + 1, out fieldName))
                {
                    if (string.Equals(fieldName, "heartRate", System.StringComparison.Ordinal))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call,
                            AccessTools.Method(typeof(BodyFormulaPatches), nameof(AdjustHeartRateTarget)));
                        continue;
                    }

                    if (string.Equals(fieldName, "bloodPressure", System.StringComparison.Ordinal))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call,
                            AccessTools.Method(typeof(BodyFormulaPatches), nameof(AdjustBloodPressureTarget)));
                        continue;
                    }
                }

                yield return instruction;
            }
        }

        [HarmonyPatch(typeof(Body), "HandlePeriodicChecks")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> HandlePeriodicChecks_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool insertedAveragePainPatch = false;

            for (var i = 0; i < codes.Count; i++)
            {
                if (!insertedAveragePainPatch &&
                    i + 2 < codes.Count &&
                    codes[i].opcode == OpCodes.Ldarg_0 &&
                    IsZeroFloatLoad(codes[i + 1]) &&
                    codes[i + 2].opcode == OpCodes.Stfld &&
                    codes[i + 2].operand is FieldInfo averagePainField &&
                    averagePainField.DeclaringType == typeof(Body) &&
                    averagePainField.Name == "averagePain")
                {
                    insertedAveragePainPatch = true;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(BodyFormulaPatches), nameof(ApplyAveragePainContribution)));
                }

                CodeInstruction instruction = codes[i];
                if (instruction.opcode == OpCodes.Stfld &&
                    instruction.operand is FieldInfo field &&
                    field.DeclaringType == typeof(Body) &&
                    PeriodicReplacements.TryGetValue(field.Name, out MethodInfo setter))
                {
                    yield return new CodeInstruction(OpCodes.Call, setter)
                    {
                        labels = new List<Label>(instruction.labels),
                        blocks = new List<ExceptionBlock>(instruction.blocks)
                    };
                    continue;
                }

                yield return instruction;
            }
        }

        [HarmonyPatch(typeof(Body), "Start")]
        [HarmonyPostfix]
        private static void Start_Postfix(Body __instance)
        {
            ApplyJumpSpeedContribution(__instance);
        }

        [HarmonyPatch(typeof(Body), "Update")]
        [HarmonyPrefix]
        private static void Update_Prefix(Body __instance)
        {
            ApplyJumpSpeedContribution(__instance);
        }

        [HarmonyPatch(typeof(Body), "FixedUpdate")]
        [HarmonyPrefix]
        private static void FixedUpdate_Prefix(Body __instance)
        {
            ApplyJumpSpeedContribution(__instance);
        }

        [HarmonyPatch(typeof(Body), "OnCollisionEnter2D")]
        [HarmonyPrefix]
        private static void OnCollisionEnter2D_Prefix(Body __instance)
        {
            ApplyJumpSpeedContribution(__instance);
        }

        private static IEnumerable<CodeInstruction> ReplaceBodyFieldStores(
            IEnumerable<CodeInstruction> instructions,
            IReadOnlyDictionary<string, MethodInfo> replacements)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Stfld &&
                    instruction.operand is FieldInfo field &&
                    field.DeclaringType == typeof(Body) &&
                    replacements.TryGetValue(field.Name, out MethodInfo setter))
                {
                    yield return new CodeInstruction(OpCodes.Call, setter)
                    {
                        labels = new List<Label>(instruction.labels),
                        blocks = new List<ExceptionBlock>(instruction.blocks)
                    };
                    continue;
                }

                yield return instruction;
            }
        }

        private static float AdjustRespiratoryRateTarget(float current, float target, float maxDelta, Body body)
        {
            if (body == null) return Mathf.MoveTowards(current, target, maxDelta);

            BodyFormulaData data = body.GetBodyFormulaData();
            return Mathf.MoveTowards(current, target + BodyFormulaData.Sum(data.RespiratoryRate), maxDelta);
        }

        private static float AdjustHeartRateTarget(float current, float target, float t, Body body)
        {
            if (body == null) return Mathf.Lerp(current, target, t);

            BodyFormulaData data = body.GetBodyFormulaData();
            return Mathf.Lerp(current, target + BodyFormulaData.Sum(data.HeartRate), t);
        }

        private static float AdjustBloodPressureTarget(float current, float target, float t, Body body)
        {
            if (body == null) return Mathf.Lerp(current, target, t);

            BodyFormulaData data = body.GetBodyFormulaData();
            return Mathf.Lerp(current, target + BodyFormulaData.Sum(data.BloodPressure), t);
        }

        private static void ApplyJumpSpeedContribution(Body body)
        {
            if (body == null)
            {
                return;
            }

            BodyFormulaData data = body.GetBodyFormulaData();
            float contribution = BodyFormulaData.Sum(data.JumpSpeed);
            float previousContribution = data.AppliedJumpSpeedContribution;

            body.jumpSpeed = Mathf.Max(0f, body.jumpSpeed - previousContribution + contribution);
            data.AppliedJumpSpeedContribution = contribution;
        }

        private static void ApplyAveragePainContribution(Body body)
        {
            if (body == null || body.limbs == null)
            {
                return;
            }

            BodyFormulaData data = body.GetBodyFormulaData();
            float contribution = BodyFormulaData.Sum(data.AveragePain);
            float previousContribution = data.AppliedAveragePainContribution;

            foreach (Limb limb in body.limbs)
            {
                if (limb == null || limb.dismembered)
                {
                    continue;
                }

                limb.pain = Mathf.Clamp(limb.pain - previousContribution + contribution, 0f, 100f);
            }

            data.AppliedAveragePainContribution = contribution;
        }

        private static bool TryFindStoredBodyField(IReadOnlyList<CodeInstruction> instructions, int startIndex,
            out string fieldName)
        {
            fieldName = null;
            for (var i = startIndex; i < instructions.Count && i < startIndex + 6; i++)
            {
                CodeInstruction instruction = instructions[i];
                if (instruction.opcode != OpCodes.Stfld || !(instruction.operand is FieldInfo field) ||
                    field.DeclaringType != typeof(Body))
                {
                    continue;
                }

                fieldName = field.Name;
                return true;
            }

            return false;
        }

        private static bool IsZeroFloatLoad(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Ldc_R4 &&
                   instruction.operand is float value &&
                   Mathf.Approximately(value, 0f);
        }

        private static void SetMaxEncumberance(Body body, float value)
        {
            if (body == null)
            {
                return;
            }

            BodyFormulaData data = body.GetBodyFormulaData();
            body.maxEncumberance = Mathf.Max(0f, value + BodyFormulaData.Sum(data.MaxEncumberance));
        }

        private static void SetTotalEncumberance(Body body, float value)
        {
            if (body == null)
            {
                return;
            }

            BodyFormulaData data = body.GetBodyFormulaData();
            body.totalEncumberance = Mathf.Max(0f, value + BodyFormulaData.Sum(data.TotalEncumberance));
        }

        private static void SetImmunity(Body body, float value)
        {
            if (body == null)
            {
                return;
            }

            BodyFormulaData data = body.GetBodyFormulaData();
            body.immunity = value + BodyFormulaData.Sum(data.Immunity);
        }
    }
}
