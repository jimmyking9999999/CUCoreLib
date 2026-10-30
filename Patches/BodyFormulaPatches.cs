using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using HarmonyLib;
using UnityEngine;
using static System.Reflection.Emit.OpCodes;
using static HarmonyLib.CodeInstruction;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class BodyFormulaPatches
    {
        private static readonly Dictionary<string, (Type, string)> PeriodicReplacements =
            new Dictionary<string, (Type, string)>
            {
                ["maxEncumberance"] = (typeof(BodyFormulaData),
                    nameof(BodyFormulaData.MaxEncumberance)),
                ["totalEncumberance"] = (typeof(BodyFormulaData),
                    nameof(BodyFormulaData.TotalEncumberance)),
                ["immunity"] = (typeof(BodyFormulaData), nameof(BodyFormulaData.Immunity))
            };

        private static readonly MethodInfo FloatLerpMethod =
            AccessTools.Method(typeof(Mathf), nameof(Mathf.Lerp),
                new[] { typeof(float), typeof(float), typeof(float) });

        private static readonly MethodInfo FloatMoveTowardsMethod =
            AccessTools.Method(typeof(Mathf), nameof(Mathf.MoveTowards),
                new[] { typeof(float), typeof(float), typeof(float) });

        [HarmonyPatch(typeof(Body), "HandleCirculation")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> HandleCirculation_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                var instruction = codes[i];
                if (instruction.Calls(FloatMoveTowardsMethod)
                    && TryFindStoredBodyField(codes, i + 1, out var fieldName)
                    && string.Equals(fieldName, "respiratoryRate",
                        StringComparison.Ordinal))
                {
                    yield return new CodeInstruction(Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(BodyFormulaPatches),
                            nameof(AdjustRespiratoryRateTarget)));
                    continue;
                }

                if (instruction.Calls(FloatLerpMethod)
                    && TryFindStoredBodyField(codes, i + 1, out fieldName))
                {
                    if (string.Equals(fieldName, "heartRate",
                        StringComparison.Ordinal))
                    {
                        yield return new CodeInstruction(Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call,
                            AccessTools.Method(typeof(BodyFormulaPatches),
                                nameof(AdjustHeartRateTarget)));
                        continue;
                    }

                    if (string.Equals(fieldName, "bloodPressure",
                        StringComparison.Ordinal))
                    {
                        yield return new CodeInstruction(Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call,
                            AccessTools.Method(typeof(BodyFormulaPatches),
                                nameof(AdjustBloodPressureTarget)));
                        continue;
                    }
                }

                yield return instruction;
            }
        }

        [HarmonyPatch(typeof(Body), "HandleBody")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> AveragePainAdd(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var typefuncarr =
                new Dictionary<string, (Type, string)>(PeriodicReplacements);
            var averagePain =
                AccessTools.Field(typeof(Body), nameof(Body.averagePain));
            for (var i = 0; i < codes.Count; i++)
                if (codes[i].opcode == Ldarg_0
                    && codes[i + 1].opcode == Ldarg_0
                    && codes[i + 2].LoadsField(averagePain))
                {
                    var callfunct = new List<CodeInstruction>
                    {
                        new CodeInstruction(Ldarg_0),
                        Call(typeof(StatusExtensions),
                            nameof(StatusExtensions.GetBodyFormulaData)),
                        LoadField(typeof(BodyFormulaData),
                            nameof(BodyFormulaData.AveragePain)),
                        Call(typeof(BodyFormulaData), nameof(BodyFormulaData.Sum)),
                        new CodeInstruction(Add)
                    };

                    codes.InsertRange(i + 3, callfunct);
                    break;
                }

            return codes;
        }

        [HarmonyPatch(typeof(Body), "HandlePeriodicChecks")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> HandlePeriodicChecks_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var typefuncarr =
                new Dictionary<string, (Type, string)>(PeriodicReplacements);
            var callfunct = new List<CodeInstruction>
            {
                new CodeInstruction(Ldarg_0),
                Call(typeof(StatusExtensions),
                    nameof(StatusExtensions.GetBodyFormulaData)),
                null,
                Call(typeof(BodyFormulaData), nameof(BodyFormulaData.Sum)),
                new CodeInstruction(Add)
            };

            for (var i = codes.Count - 1; i >= 0; i--)
                if (codes[i].opcode == Stfld)
                {
                    var field = (FieldInfo)codes[i].operand;
                    if (field.DeclaringType == typeof(Body)
                        && typefuncarr.TryGetValue(field.Name,
                            out (Type type, string name) typefunc))
                    {
                        callfunct[2] = LoadField(typefunc.type, typefunc.name);
                        codes.InsertRange(i, callfunct);
                        typefuncarr.Remove(field.Name);
                    }
                }

            return codes;
        }

        private static IEnumerable<CodeInstruction> ReplaceBodyFieldStores(
            IEnumerable<CodeInstruction> instructions,
            IReadOnlyDictionary<string, MethodInfo> replacements)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == Stfld
                    && instruction.operand is FieldInfo field
                    && field.DeclaringType == typeof(Body)
                    && replacements.TryGetValue(field.Name, out var setter))
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

        private static float AdjustRespiratoryRateTarget(float current, float target,
            float maxDelta, Body body)
        {
            if (body == null) return Mathf.MoveTowards(current, target, maxDelta);

            var data = body.GetBodyFormulaData();
            return Mathf.MoveTowards(current,
                target + BodyFormulaData.Sum(data.RespiratoryRate), maxDelta);
        }

        private static float AdjustHeartRateTarget(float current, float target, float t,
            Body body)
        {
            if (body == null) return Mathf.Lerp(current, target, t);

            var data = body.GetBodyFormulaData();
            return Mathf.Lerp(current, target + BodyFormulaData.Sum(data.HeartRate), t);
        }

        private static float AdjustBloodPressureTarget(float current, float target,
            float t, Body body)
        {
            if (body == null) return Mathf.Lerp(current, target, t);

            var data = body.GetBodyFormulaData();
            return Mathf.Lerp(current, target + BodyFormulaData.Sum(data.BloodPressure),
                t);
        }

        [HarmonyPatch(typeof(Body), "get_" + nameof(Body.actualMaxSpeed))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MaxSpeedAdd(
            IEnumerable<CodeInstruction> instructions, ILGenerator ILGen)
        {
            var fieldlist = new List<int>();
            var codes = new List<CodeInstruction>(instructions);
            var BodymaxSpeed =
                AccessTools.Field(typeof(Body), nameof(Body.maxSpeed));
            for (var i = codes.Count - 1; i >= 0; i--)
                if (codes[i].LoadsField(BodymaxSpeed))
                    fieldlist.Add(i);

            if (fieldlist.Count != 0)
            {
                var maxSpeedvar = ILGen.DeclareLocal(typeof(float));
                var callfunct = new List<CodeInstruction>
                {
                    new CodeInstruction(Ldarg_0),
                    LoadField(typeof(Body), nameof(Body.maxSpeed)),
                    new CodeInstruction(Ldarg_0),
                    Call(typeof(StatusExtensions),
                        nameof(StatusExtensions.GetBodyFormulaData)),
                    LoadField(typeof(BodyFormulaData), nameof(BodyFormulaData.MaxSpeed)),
                    Call(typeof(BodyFormulaData), nameof(BodyFormulaData.Sum)),
                    new CodeInstruction(Add),
                    new CodeInstruction(Stloc, maxSpeedvar)
                };

                foreach (var index in fieldlist)
                {
                    codes[index] = new CodeInstruction(Ldloc, maxSpeedvar);
                    codes.RemoveAt(index - 1);
                }

                codes.InsertRange(0, callfunct);
            }

            return codes;
        }

        [HarmonyPatch(typeof(Body), "get_" + nameof(Body.actualJumpSpeed))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> JumpSpeedAdd(
            IEnumerable<CodeInstruction> instructions)
        {
            var methodidx = -1;
            var codes = new List<CodeInstruction>(instructions);
            var BodyjumpSpeed =
                AccessTools.Field(typeof(Body), nameof(Body.jumpSpeed));
            for (var i = 0; i < codes.Count; i++)
                if (codes[i].LoadsField(BodyjumpSpeed))
                {
                    methodidx = i;
                    break;
                }

            if (methodidx != -1)
            {
                var callfunct = new List<CodeInstruction>
                {
                    new CodeInstruction(Ldarg_0),
                    Call(typeof(StatusExtensions),
                        nameof(StatusExtensions.GetBodyFormulaData)),
                    LoadField(typeof(BodyFormulaData), nameof(BodyFormulaData.JumpSpeed)),
                    Call(typeof(BodyFormulaData), nameof(BodyFormulaData.Sum)),
                    new CodeInstruction(Add)
                };
                codes.InsertRange(methodidx + 1, callfunct);
            }

            return codes;
        }

        private static bool TryFindStoredBodyField(
            IReadOnlyList<CodeInstruction> instructions, int startIndex,
            out string fieldName)
        {
            fieldName = null;
            for (var i = startIndex; i < instructions.Count && i < startIndex + 6; i++)
            {
                var instruction = instructions[i];
                if (instruction.opcode != Stfld
                    || !(instruction.operand is FieldInfo field)
                    || field.DeclaringType != typeof(Body))
                    continue;

                fieldName = field.Name;
                return true;
            }

            return false;
        }

        private static bool IsZeroFloatLoad(CodeInstruction instruction)
        {
            return instruction.opcode == Ldc_R4
                && instruction.operand is float value
                && Mathf.Approximately(value, 0f);
        }
    }
}
