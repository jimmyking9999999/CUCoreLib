using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx;
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
            ContentReloadResult result = new ContentReloadResult
            {
                ModGuid = report != null ? report.ModGuid : null,
                ModName = report != null ? report.ModName : null,
                SourcePath = report != null ? report.SelectedPath : null,
                SourceHash = report != null ? report.SelectedHash : null,
                UnsupportedReason = report != null ? report.UnsupportedReason : "Compatibility report was null."
            };

            if (report == null)
            {
                result.AddError("Compatibility report was null.");
                return result;
            }

            for (int i = 0; i < report.RecognizedMethods.Count; i++)
            {
                result.AddRecognizedMethod(report.RecognizedMethods[i]);
            }

            if (!report.IsSupported)
            {
                if (!string.IsNullOrWhiteSpace(report.UnsupportedReason))
                {
                    result.AddError(report.UnsupportedReason);
                }

                return result;
            }

            byte[] bytes = File.ReadAllBytes(report.SelectedPath);
            Assembly assembly = Assembly.Load(bytes);
            Type pluginType = assembly.GetType(report.PluginTypeFullName, throwOnError: false);
            if (pluginType == null)
            {
                result.AddError("Reloaded assembly did not contain plugin type '" + report.PluginTypeFullName + "'.");
                return result;
            }

            MethodInfo[] methods = report.RecognizedMethods
                .Select(methodName => pluginType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method => method != null && method.GetParameters().Length == 0)
                .ToArray();

            if (methods.Length == 0)
            {
                result.AddError("No invokable content methods were resolved from the reloaded assembly.");
                return result;
            }

            object pluginInstance = methods.Any(method => !method.IsStatic)
                ? CreatePluginReplayInstance(pluginType, report)
                : null;

            ContentOwnerSnapshot existingContent = CaptureExistingContent(report.ModGuid);
            ClearExistingContent(report.ModGuid, result);

            using (ContentReloadSession.Begin(report.ModGuid, assembly, report.SelectedPath, ContentReloadSurface.AllAllowed))
            using (ItemRegistry.BeginOwnerRegistration(report.ModGuid))
            using (LiquidRegistry.BeginOwnerRegistration(report.ModGuid))
            using (RecipeRegistry.BeginOwnerRegistration(report.ModGuid))
            using (LocaleRegistry.BeginOwnerRegistration(report.ModGuid))
            {
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    try
                    {
                        method.Invoke(method.IsStatic ? null : pluginInstance, null);
                        result.AddInfo("Ran " + method.Name + "().");
                    }
                    catch (TargetInvocationException ex)
                    {
                        Exception inner = ex.InnerException ?? ex;
                        Rollback(report.ModGuid, existingContent, result);
                        result.AddError("Method '" + method.Name + "' failed: " + inner.Message);
                        CUCoreLibPlugin.Log?.LogWarning("CUCoreLib strict content reload failed while running '" + method.Name + "' for '" + report.ModGuid + "'.\n" + inner);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Rollback(report.ModGuid, existingContent, result);
                        result.AddError("Method '" + method.Name + "' failed: " + ex.Message);
                        CUCoreLibPlugin.Log?.LogWarning("CUCoreLib strict content reload failed while running '" + method.Name + "' for '" + report.ModGuid + "'.\n" + ex);
                        return result;
                    }
                }
            }

            if (Recipes.recipes != null)
            {
                LiquidRegistry.InjectRegisteredLiquids(logSummary: false);
                RecipeRegistry.InjectRegisteredRecipes();
            }

            ConsolePatch.RefreshRuntimeAutofill();
            RecipeRegistryPatches.RefreshCraftingUi();

            result.AddInfo("Strict content reload completed.");
            return result;
        }

        private static ContentOwnerSnapshot CaptureExistingContent(string modGuid)
        {
            return new ContentOwnerSnapshot
            {
                Items = ItemRegistry.CaptureOwnerEntries(modGuid),
                Liquids = LiquidRegistry.CaptureOwnerEntries(modGuid),
                Recipes = RecipeRegistry.CaptureOwnerEntries(modGuid),
                Locales = LocaleRegistry.CaptureOwnerEntries(modGuid)
            };
        }

        private static void ClearExistingContent(string modGuid, ContentReloadResult result)
        {
            ItemRegistry.ClearOwnerEntries(modGuid, result);
            LiquidRegistry.ClearOwnerEntries(modGuid, result);
            RecipeRegistry.ClearOwnerEntries(modGuid, result);
            LocaleRegistry.ClearOwnerEntries(modGuid, result);
        }

        private static void Rollback(string modGuid, ContentOwnerSnapshot snapshot, ContentReloadResult result)
        {
            ClearExistingContent(modGuid, null);

            using (ItemRegistry.BeginOwnerRegistration(modGuid))
            using (LiquidRegistry.BeginOwnerRegistration(modGuid))
            using (RecipeRegistry.BeginOwnerRegistration(modGuid))
            using (LocaleRegistry.BeginOwnerRegistration(modGuid))
            {
                ItemRegistry.RestoreOwnerEntries(modGuid, snapshot.Items);
                LiquidRegistry.RestoreOwnerEntries(modGuid, snapshot.Liquids);
                RecipeRegistry.RestoreOwnerEntries(modGuid, snapshot.Recipes);
                LocaleRegistry.RestoreOwnerEntries(modGuid, snapshot.Locales);
            }

            if (Recipes.recipes != null)
            {
                LiquidRegistry.InjectRegisteredLiquids(logSummary: false);
                RecipeRegistry.InjectRegisteredRecipes();
            }

            ConsolePatch.RefreshRuntimeAutofill();
            RecipeRegistryPatches.RefreshCraftingUi();

            result.AddSkipped("Reload failed. Restored the previous successful content state for '" + modGuid + "'.");
        }

        private static object CreatePluginReplayInstance(Type pluginType, ContentCompatibilityReport report)
        {
            object instance = FormatterServices.GetUninitializedObject(pluginType);
            TryAssignPluginInfo(pluginType, instance, report);
            TryAssignLoggerField(pluginType, instance);
            return instance;
        }

        private static void TryAssignPluginInfo(Type pluginType, object instance, ContentCompatibilityReport report)
        {
            try
            {
                PropertyInfo infoProperty = GetProperty(pluginType, "Info");
                FieldInfo infoField = GetField(pluginType, "<Info>k__BackingField");
                if (infoProperty == null && infoField == null)
                {
                    return;
                }

                PluginInfo currentInfo = Chainloader.PluginInfos.TryGetValue(report.ModGuid, out var loadedInfo) ? loadedInfo : null;
                if (currentInfo == null)
                {
                    return;
                }

                if (infoField != null)
                {
                    infoField.SetValue(instance, currentInfo);
                }
                else if (infoProperty != null && infoProperty.CanWrite)
                {
                    infoProperty.SetValue(instance, currentInfo, null);
                }
            }
            catch
            {
            }
        }

        private static void TryAssignLoggerField(Type pluginType, object instance)
        {
            FieldInfo[] fields = pluginType.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType != typeof(ManualLogSource))
                {
                    continue;
                }

                if (field.Name.IndexOf("logger", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                try
                {
                    field.SetValue(field.IsStatic ? null : instance, CUCoreLibPlugin.Log);
                }
                catch
                {
                }
            }
        }

        private static PropertyInfo GetProperty(Type type, string propertyName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static FieldInfo GetField(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private sealed class ContentOwnerSnapshot
        {
            public IDictionary<string, CustomItemInfo> Items;
            public IDictionary<string, CustomLiquidInfo> Liquids;
            public IEnumerable<Recipe> Recipes;
            public IDictionary<int, Dictionary<string, string>> Locales;
        }
    }
}
