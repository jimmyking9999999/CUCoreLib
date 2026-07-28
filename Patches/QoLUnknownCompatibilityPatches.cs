using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CUCoreLib.Helpers;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    /// <summary>
    /// Optional integration for QoL Unknown's multiplayer client-inventory restore.
    /// QoL stores vanilla save JSON, but its restore path used Resources.Load directly; that cannot find
    /// CUCoreLib's runtime custom-item templates and silently drops those entries.
    /// </summary>
    internal static class QoLUnknownCompatibilityPatches
    {
        private const string QoLMultiplayerSaveBundleTypeName = "QoL_Unknown.MultiplayerSaveBundle";
        private static bool _installed;
        private static bool _retryScheduled;

        internal static void Install(Harmony harmony)
        {
            if (harmony == null || _installed) return;

            var bundleType = ResolveLoadedType(QoLMultiplayerSaveBundleTypeName);
            var applySavedItems = AccessTools.Method(bundleType, "ApplySavedItems");
            if (applySavedItems != null)
            {
                harmony.Patch(applySavedItems,
                    transpiler: new HarmonyMethod(typeof(QoLUnknownCompatibilityPatches),
                        nameof(ApplySavedItems_Transpiler)));
                _installed = true;
                CUCoreLibPlugin.Log?.LogInfo("CUCoreLib installed QoL Unknown custom-item save compatibility.");
                return;
            }

            ScheduleRetry(harmony);
        }

        private static IEnumerable<CodeInstruction> ApplySavedItems_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var resourcesLoad = typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == nameof(Resources.Load) && !method.IsGenericMethod &&
                                          method.GetParameters().Length == 1 &&
                                          method.GetParameters()[0].ParameterType == typeof(string));
            var resolveSavedResource = AccessTools.Method(typeof(CustomInstantiate),
                nameof(CustomInstantiate.ResolveSavedResource));

            foreach (var instruction in instructions)
            {
                if (resourcesLoad != null && resolveSavedResource != null && instruction.Calls(resourcesLoad))
                {
                    yield return new CodeInstruction(OpCodes.Call, resolveSavedResource);
                    continue;
                }

                yield return instruction;
            }
        }

        private static void ScheduleRetry(Harmony harmony)
        {
            if (_retryScheduled) return;

            _retryScheduled = true;
            CUCoreUtils.DelayCall(1f, () =>
            {
                _retryScheduled = false;
                Install(harmony);
            });
        }

        private static Type ResolveLoadedType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
    }
}
