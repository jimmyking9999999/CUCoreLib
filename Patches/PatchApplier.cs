using System;
using System.Linq;
using System.Reflection;
using CUCoreLib.Helpers;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    internal static class PatchApplier
    {
        internal static void ApplyAll(Harmony harmony)
        {
            if (harmony == null) return;

            var assembly = typeof(PatchApplier).Assembly;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            var applied = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var type in types)
            {
                if (!HasPatchAttribute(type)) continue;

                var gate = (AppliesToBuildAttribute)Attribute
                    .GetCustomAttribute(type, typeof(AppliesToBuildAttribute));
                if (gate != null && !GameBuildInfo.Matches(gate.Build))
                {
                    skipped++;
                    // CUCoreLibPlugin.Log?.LogInfo($" Skipped {type.Name} ({gate.Build}).");
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception /*ex*/)
                {
                    failed++;
                    // CUCoreLibPlugin.Log?.LogWarning($" Failed to apply {type.FullName}: {ex}");
                }
            }

            // CUCoreLibPlugin.Log?.LogInfo(
            //     $"{applied} patch famil{(applied == 1 ? "y" : "ies")} applied, {skipped} skipped (build gate), {failed} failed.");
        }

        // Discover attributes
        private static bool HasPatchAttribute(Type type)
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0) return true;

            return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.DeclaredOnly)
                .Any(method => method.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0);
        }
    }
}
