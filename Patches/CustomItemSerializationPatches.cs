using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CUCoreLib.Helpers;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    internal static class CustomItemSerializationHelpers
    {
        internal static Object LoadSavedItemResource(string id)
        {
            var vanilla = Resources.Load(id);
            if (vanilla != null) return vanilla;

            return CustomInstantiate.GetOrCreateTemplate(id);
        }

        internal static Object InstantiateSavedItem(Object original, Vector3 position, Quaternion rotation)
        {
            if (original == null) return null;

            var clone = Object.Instantiate(original, position, rotation);
            if (clone is GameObject obj) obj.SetActive(true);

            return clone;
        }
    }

    [HarmonyPatch(typeof(SaveSystem), "TryLoadGame")]
    internal static class SaveSystemCustomItemLoadPatch
    {
        private static readonly MethodInfo ResourcesLoadMethod = typeof(Resources)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(method =>
                method.Name == nameof(Resources.Load) &&
                !method.IsGenericMethod &&
                method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType == typeof(string));

        private static readonly MethodInfo LoadSavedItemResourceMethod =
            AccessTools.Method(typeof(CustomItemSerializationHelpers),
                nameof(CustomItemSerializationHelpers.LoadSavedItemResource));

        private static readonly MethodInfo ObjectInstantiateMethod = typeof(Object)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(method =>
                method.Name == nameof(Object.Instantiate) &&
                !method.IsGenericMethod &&
                method.GetParameters().Length == 3 &&
                method.GetParameters()[0].ParameterType == typeof(Object) &&
                method.GetParameters()[1].ParameterType == typeof(Vector3) &&
                method.GetParameters()[2].ParameterType == typeof(Quaternion));

        private static readonly MethodInfo InstantiateSavedItemMethod =
            AccessTools.Method(typeof(CustomItemSerializationHelpers),
                nameof(CustomItemSerializationHelpers.InstantiateSavedItem));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(ResourcesLoadMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, LoadSavedItemResourceMethod);
                    continue;
                }

                if (instruction.Calls(ObjectInstantiateMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, InstantiateSavedItemMethod);
                    continue;
                }

                yield return instruction;
            }
        }
    }
}