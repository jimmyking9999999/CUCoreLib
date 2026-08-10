using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class LiquidRegistryPatches
    {
        private static readonly FieldInfo LiquidsRegistryField =
            AccessTools.Field(typeof(Liquids), nameof(Liquids.Registry));

        private static readonly MethodInfo GetMiniBarrelLiquidsMethod =
            AccessTools.Method(typeof(LiquidRegistry), nameof(LiquidRegistry.GetMiniBarrelLiquids));

        [HarmonyPatch(typeof(Liquids), nameof(Liquids.LiquidExists))]
        [HarmonyPostfix]
        private static void IncludeCustomLiquids(string id, ref bool __result)
        {
            if (__result) return;
            __result = LiquidRegistry.EnsureLiquidInjected(id);
        }

        [HarmonyPatch(typeof(WorldGeneration), "DistributeMiniBarrels")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ExcludeUnobtainableLiquidsFromMiniBarrels(
            IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (LiquidsRegistryField != null && GetMiniBarrelLiquidsMethod != null &&
                    instruction.LoadsField(LiquidsRegistryField))
                {
                    yield return new CodeInstruction(OpCodes.Call, GetMiniBarrelLiquidsMethod)
                    {
                        labels = instruction.labels,
                        blocks = instruction.blocks
                    };
                    continue;
                }

                yield return instruction;
            }
        }
    }
}
