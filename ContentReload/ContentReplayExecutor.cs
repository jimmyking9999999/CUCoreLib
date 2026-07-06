using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Patches;
using CUCoreLib.Registries;

namespace CUCoreLib.ContentReload
{
    internal static class ContentReplayExecutor
    {
        internal static ContentReloadResult Execute(ContentCompatibilityReport report)
        {
            RecipeRegistry.ResetHotReloadInjectionSummary();

            var result = new ContentReloadResult
            {
                ModGuid = report?.ModGuid,
                ModName = report?.ModName,
                SourcePath = report?.SelectedPath,
                SourceHash = report?.SelectedHash,
                UnsupportedReason = report != null ? report.UnsupportedReason : "Compatibility report was null."
            };

            if (report == null)
            {
                result.AddError("Compatibility report was null.");
                return result;
            }

            foreach (var recognizedMethod in report.RecognizedMethods)
                result.AddRecognizedMethod(recognizedMethod);

            foreach (var skippedMethod in report.SkippedMethods)
                result.AddSkipped(skippedMethod.DisplayName + ": " + skippedMethod.Reason);

            if (!report.IsSupported)
            {
                if (!string.IsNullOrWhiteSpace(report.UnsupportedReason)) result.AddError(report.UnsupportedReason);

                return result;
            }

            var bytes = File.ReadAllBytes(report.SelectedPath);
            var assembly = Assembly.Load(bytes);
            var pluginType = assembly.GetType(report.PluginTypeFullName, false);
            if (pluginType == null)
            {
                result.AddError("Reloaded assembly did not contain plugin type '" + report.PluginTypeFullName + "'.");
                return result;
            }

            var invocations = ResolveInvocations(assembly, pluginType, report, result);
            if (invocations.Count == 0)
            {
                result.UnsupportedReason = "No invokable content methods were resolved from the reloaded assembly.";
                result.AddError(result.UnsupportedReason);
                return result;
            }

            var existingContent = CaptureExistingContent(report.ModGuid);
            AssetLoader.InvalidateEmbeddedCachesForModGuid(report.ModGuid);
            AssetLoader.InvalidateBundlesForModGuid(report.ModGuid, unregister: true);
            ClearExistingContent(report.ModGuid, result);
            var reloadMode = ContentReloadManager.GetReloadMode(report.ModGuid);

            using (ContentReloadSession.Begin(report.ModGuid, assembly, report.SelectedPath,
                       ContentReloadSurface.AllAllowed, reloadMode))
            using (ItemRegistry.BeginOwnerRegistration(report.ModGuid))
            using (LiquidRegistry.BeginOwnerRegistration(report.ModGuid))
            using (RecipeRegistry.BeginOwnerRegistration(report.ModGuid))
            using (LocaleRegistry.BeginOwnerRegistration(report.ModGuid))
            using (BuildingEntityRegistry.BeginOwnerRegistration(report.ModGuid))
            {
                foreach (var invocation in invocations)
                {
                    try
                    {
                        invocation.Method.Invoke(invocation.Method.IsStatic ? null : invocation.Target, null);
                        result.AddRanMethod(invocation.DisplayName);
                        result.AddInfo("Ran " + invocation.DisplayName + "().");
                    }
                    catch (TargetInvocationException ex)
                    {
                        var inner = ex.InnerException ?? ex;
                        Rollback(report.ModGuid, existingContent, result);
                        result.AddError("Method '" + invocation.DisplayName + "' failed: " + inner.Message);
                        CUCoreLibPlugin.Log?.LogWarning("CUCoreLib strict content reload failed while running '" +
                                                        invocation.DisplayName + "' for '" + report.ModGuid + "'.\n" +
                                                        inner);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Rollback(report.ModGuid, existingContent, result);
                        result.AddError("Method '" + invocation.DisplayName + "' failed: " + ex.Message);
                        CUCoreLibPlugin.Log?.LogWarning("CUCoreLib strict content reload failed while running '" +
                                                        invocation.DisplayName + "' for '" + report.ModGuid + "'.\n" +
                                                        ex);
                        return result;
                    }
                }
            }

            FinalizeRuntimeRefresh(existingContent.Buildings?.Keys);

            result.AddInfo("Strict content reload completed.");
            return result;
        }

        private static List<ResolvedInvocation> ResolveInvocations(Assembly assembly, Type pluginType,
            ContentCompatibilityReport report, ContentReloadResult result)
        {
            var invocations = new List<ResolvedInvocation>();
            object pluginInstance = null;
            var hostInstances = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var discoveredMethod in report.Methods)
            {
                var declaringType = assembly.GetType(discoveredMethod.DeclaringTypeFullName, false);
                if (declaringType == null)
                {
                    result.AddSkipped(discoveredMethod.DisplayName +
                                      ": Reloaded assembly did not contain declaring type '" +
                                      discoveredMethod.DeclaringTypeFullName + "'.");
                    continue;
                }

                var method = declaringType.GetMethod(discoveredMethod.MethodName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null || method.GetParameters().Length != 0)
                {
                    result.AddSkipped(discoveredMethod.DisplayName +
                                      ": Reloaded assembly did not contain an invokable parameterless method.");
                    continue;
                }

                object target = null;
                if (!method.IsStatic)
                {
                    if (discoveredMethod.IsPluginMethod)
                    {
                        if (pluginInstance == null) pluginInstance = CreatePluginReplayInstance(pluginType, report);
                        target = pluginInstance;
                    }
                    else
                    {
                        if (!hostInstances.TryGetValue(discoveredMethod.DeclaringTypeFullName, out target))
                        {
                            target = CreateHostReplayInstance(declaringType, report, out var reason);
                            if (target == null)
                            {
                                result.AddSkipped(discoveredMethod.DisplayName + ": " + reason);
                                continue;
                            }

                            hostInstances[discoveredMethod.DeclaringTypeFullName] = target;
                        }
                    }
                }

                invocations.Add(new ResolvedInvocation
                {
                    DisplayName = discoveredMethod.DisplayName,
                    Method = method,
                    Target = target
                });
            }

            return invocations;
        }

        private static ContentOwnerSnapshot CaptureExistingContent(string modGuid)
        {
            return new ContentOwnerSnapshot
            {
                Items = ItemRegistry.CaptureOwnerEntries(modGuid),
                Liquids = LiquidRegistry.CaptureOwnerEntries(modGuid),
                Recipes = RecipeRegistry.CaptureOwnerEntries(modGuid),
                Locales = LocaleRegistry.CaptureOwnerEntries(modGuid),
                Buildings = BuildingEntityRegistry.CaptureOwnerEntries(modGuid)
            };
        }

        private static void ClearExistingContent(string modGuid, ContentReloadResult result)
        {
            ItemRegistry.ClearOwnerEntries(modGuid, result);
            LiquidRegistry.ClearOwnerEntries(modGuid, result);
            RecipeRegistry.ClearOwnerEntries(modGuid, result);
            LocaleRegistry.ClearOwnerEntries(modGuid, result);
            BuildingEntityRegistry.ClearOwnerEntries(modGuid, result);
        }

        private static void Rollback(string modGuid, ContentOwnerSnapshot snapshot, ContentReloadResult result)
        {
            RecipeRegistry.ResetHotReloadInjectionSummary();
            ClearExistingContent(modGuid, null);

            using (ItemRegistry.BeginOwnerRegistration(modGuid))
            using (LiquidRegistry.BeginOwnerRegistration(modGuid))
            using (RecipeRegistry.BeginOwnerRegistration(modGuid))
            using (LocaleRegistry.BeginOwnerRegistration(modGuid))
            using (BuildingEntityRegistry.BeginOwnerRegistration(modGuid))
            {
                ItemRegistry.RestoreOwnerEntries(modGuid, snapshot.Items);
                LiquidRegistry.RestoreOwnerEntries(modGuid, snapshot.Liquids);
                RecipeRegistry.RestoreOwnerEntries(modGuid, snapshot.Recipes);
                LocaleRegistry.RestoreOwnerEntries(modGuid, snapshot.Locales);
                BuildingEntityRegistry.RestoreOwnerEntries(modGuid, snapshot.Buildings);
            }

            FinalizeRuntimeRefresh(snapshot.Buildings?.Keys);

            result.AddSkipped("Reload failed. Restored the previous successful content state for '" + modGuid + "'.");
        }

        private static void FinalizeRuntimeRefresh(IEnumerable<string> buildingIds)
        {
            if (Recipes.recipes != null)
            {
                LiquidRegistry.InjectRegisteredLiquids();
                RecipeRegistry.InjectRegisteredRecipes();
                RecipeRegistry.FlushHotReloadInjectionSummary();
            }

            try
            {
                ItemRegistryPatches.RefreshLiveInstances();
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib strict content reload item refresh failed.\n" + ex);
            }

            try
            {
                BuildingEntityRegistry.RefreshLiveInstances(buildingIds);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib strict content reload building refresh failed.\n" + ex);
            }

            try
            {
                ConsolePatch.RefreshRuntimeAutofill();
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib strict content reload console autofill refresh failed.\n" + ex);
            }

            try
            {
                RecipeRegistryPatches.RefreshCraftingUi();
            }
            catch (Exception ex)
            {
                _ = ex;
            }
        }

        private static object CreatePluginReplayInstance(Type pluginType, ContentCompatibilityReport report)
        {
            var instance = FormatterServices.GetUninitializedObject(pluginType);
            TryAssignPluginInfo(pluginType, instance, report);
            TryAssignLoggerField(pluginType, instance);
            return instance;
        }

        private static object CreateHostReplayInstance(Type hostType, ContentCompatibilityReport report,
            out string reason)
        {
            var instance = ContentHostRuntime.CreateHostInstance(hostType, report.ModGuid, out reason);
            if (instance == null) return null;

            TryAssignLoggerField(hostType, instance);
            return instance;
        }

        private static void TryAssignPluginInfo(Type pluginType, object instance, ContentCompatibilityReport report)
        {
            try
            {
                var infoProperty = GetProperty(pluginType, "Info");
                var infoField = GetField(pluginType, "<Info>k__BackingField");
                if (infoProperty == null && infoField == null) return;

                var currentInfo = Chainloader.PluginInfos.TryGetValue(report.ModGuid, out var loadedInfo)
                    ? loadedInfo
                    : null;
                if (currentInfo == null) return;

                if (infoField != null)
                    infoField.SetValue(instance, currentInfo);
                else if (infoProperty != null && infoProperty.CanWrite)
                    infoProperty.SetValue(instance, currentInfo, null);
            }
            catch
            {
                // ignored
            }
        }

        private static void TryAssignLoggerField(Type type, object instance)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                        BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(ManualLogSource)) continue;

                if (field.Name.IndexOf("logger", StringComparison.OrdinalIgnoreCase) < 0) continue;

                try
                {
                    field.SetValue(field.IsStatic ? null : instance, CUCoreLibPlugin.Log);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static PropertyInfo GetProperty(Type type, string propertyName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var property = current.GetProperty(propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null) return property;
            }

            return null;
        }

        private static FieldInfo GetField(Type type, string fieldName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }

            return null;
        }

        private sealed class ContentOwnerSnapshot
        {
            public IDictionary<string, CustomBuildingEntityDefinition> Buildings;
            public IDictionary<string, CustomItemInfo> Items;
            public IDictionary<string, CustomLiquidInfo> Liquids;
            public IDictionary<int, Dictionary<string, string>> Locales;
            public IEnumerable<Recipe> Recipes;
        }

        private sealed class ResolvedInvocation
        {
            public string DisplayName;
            public MethodInfo Method;
            public object Target;
        }
    }
}
