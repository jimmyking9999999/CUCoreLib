using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CUCoreLib.Data;
using CUCoreLib.Patches;
using CUCoreLib.Registries;

namespace CUCoreLib.ContentReload;

internal static class ContentReplayExecutor
{
    internal static ContentReloadResult Execute(ContentCompatibilityReport report)
    {
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

        var methods = report.RecognizedMethods
            .Select(methodName => pluginType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(method => method != null && method.GetParameters().Length == 0)
            .ToArray();

        if (methods.Length == 0)
        {
            result.AddError("No invokable content methods were resolved from the reloaded assembly.");
            return result;
        }

        var pluginInstance = methods.Any(method => !method.IsStatic)
            ? CreatePluginReplayInstance(pluginType, report)
            : null;

        var existingContent = CaptureExistingContent(report.ModGuid);
        ClearExistingContent(report.ModGuid, result);

        using (ContentReloadSession.Begin(report.ModGuid, assembly, report.SelectedPath,
                   ContentReloadSurface.AllAllowed))
        using (ItemRegistry.BeginOwnerRegistration(report.ModGuid))
        using (LiquidRegistry.BeginOwnerRegistration(report.ModGuid))
        using (RecipeRegistry.BeginOwnerRegistration(report.ModGuid))
        using (LocaleRegistry.BeginOwnerRegistration(report.ModGuid))
        using (BuildingEntityRegistry.BeginOwnerRegistration(report.ModGuid))
        {
            foreach (var method in methods)
                try
                {
                    method.Invoke(method.IsStatic ? null : pluginInstance, null);
                    result.AddInfo("Ran " + method.Name + "().");
                }
                catch (TargetInvocationException ex)
                {
                    var inner = ex.InnerException ?? ex;
                    Rollback(report.ModGuid, existingContent, result);
                    result.AddError("Method '" + method.Name + "' failed: " + inner.Message);
                    CUCoreLibPlugin.Log?.LogWarning("CUCoreLib strict content reload failed while running '" +
                                                    method.Name + "' for '" + report.ModGuid + "'.\n" + inner);
                    return result;
                }
                catch (Exception ex)
                {
                    Rollback(report.ModGuid, existingContent, result);
                    result.AddError("Method '" + method.Name + "' failed: " + ex.Message);
                    CUCoreLibPlugin.Log?.LogWarning("CUCoreLib strict content reload failed while running '" +
                                                    method.Name + "' for '" + report.ModGuid + "'.\n" + ex);
                    return result;
                }
        }

        FinalizeRuntimeRefresh(existingContent.Buildings?.Keys);

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
            else if (infoProperty.CanWrite)
                infoProperty.SetValue(instance, currentInfo, null);
        }
        catch
        {
            // ignored
        }
    }

    private static void TryAssignLoggerField(Type pluginType, object instance)
    {
        var fields = pluginType.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
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
}