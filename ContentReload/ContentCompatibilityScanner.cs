using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CUCoreLib.ContentReload
{
    internal static class ContentCompatibilityScanner
    {
        private const string ContentReloadEntryAttributeFullName = "CUCoreLib.Data.ContentReloadEntryAttribute";

        internal static ContentCompatibilityReport Scan(ContentReloadCandidate candidate)
        {
            ContentCompatibilityReport report = new ContentCompatibilityReport
            {
                ModGuid = candidate != null ? candidate.ModGuid : null,
                ModName = candidate != null ? candidate.ModName : null,
                LoadedPluginPath = candidate != null ? candidate.LoadedPluginPath : null,
                OverridePath = candidate != null ? candidate.OverridePath : null,
                SelectedPath = candidate != null ? candidate.SelectedPath : null,
                SelectedHash = candidate != null ? candidate.SelectedHash : null,
                SelectedSourceLabel = candidate != null ? candidate.SelectedSourceLabel : null
            };

            if (candidate == null || string.IsNullOrWhiteSpace(candidate.SelectedPath))
            {
                report.UnsupportedReason = "No rebuilt DLL source path was found. The loaded plugin path and configured override path were both unavailable.";
                return report;
            }

            if (!File.Exists(candidate.SelectedPath))
            {
                report.UnsupportedReason = "The selected rebuilt DLL path does not exist: " + candidate.SelectedPath;
                return report;
            }

            try
            {
                using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(candidate.SelectedPath, CreateReaderParameters(candidate)))
                {
                    TypeDefinition pluginType = FindPluginType(assembly, candidate.ModGuid);
                    if (pluginType == null)
                    {
                        report.UnsupportedReason = "No [BepInPlugin] type matching '" + candidate.ModGuid + "' was found in the rebuilt DLL.";
                        return report;
                    }

                    report.PluginTypeFullName = pluginType.FullName;

                    List<SelectedReloadMethod> selectedMethods = SelectSupportedMethods(pluginType, report);
                    if (!string.IsNullOrWhiteSpace(report.UnsupportedReason))
                    {
                        return report;
                    }

                    AddIgnoredMethodNotes(pluginType, report);

                    for (int i = 0; i < selectedMethods.Count; i++)
                    {
                        SelectedReloadMethod selected = selectedMethods[i];
                        string forbiddenCall = FindForbiddenCall(selected.Method, new HashSet<string>(StringComparer.Ordinal));
                        if (!string.IsNullOrWhiteSpace(forbiddenCall))
                        {
                            report.UnsupportedReason = "Method '" + selected.Method.Name + "' is not strict content-only: " + forbiddenCall;
                            return report;
                        }

                        report.RecognizedMethods.Add(selected.Method.Name);
                    }

                    if (report.RecognizedMethods.Count == 0)
                    {
                        report.UnsupportedReason =
                            "No supported content reload entry methods were found. " +
                            "Add one or more [ContentReloadEntry(...)] parameterless methods.";
                    }
                }
            }
            catch (Exception ex)
            {
                report.UnsupportedReason = "Compatibility scan failed: " + ex.Message;
            }

            return report;
        }

        private static ReaderParameters CreateReaderParameters(ContentReloadCandidate candidate)
        {
            DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
            foreach (string directory in BuildResolverSearchDirectories(candidate))
            {
                try
                {
                    resolver.AddSearchDirectory(directory);
                }
                catch
                {
                }
            }

            return new ReaderParameters
            {
                AssemblyResolver = resolver,
                InMemory = true,
                ReadWrite = false,
                ReadingMode = ReadingMode.Deferred
            };
        }

        private static IEnumerable<string> BuildResolverSearchDirectories(ContentReloadCandidate candidate)
        {
            HashSet<string> seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] seedPaths =
            {
                candidate != null ? candidate.SelectedPath : null,
                candidate != null ? candidate.LoadedPluginPath : null,
                candidate != null ? candidate.OverridePath : null,
                typeof(CUCoreLibPlugin).Assembly.Location,
                System.Reflection.Assembly.GetExecutingAssembly().Location
            };

            foreach (string path in seedPaths)
            {
                string directory = GetExistingDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && seenDirectories.Add(directory))
                {
                    yield return directory;
                }
            }

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string directory = null;
                try
                {
                    directory = GetExistingDirectoryName(assembly.Location);
                }
                catch
                {
                    directory = null;
                }

                if (!string.IsNullOrWhiteSpace(directory) && seenDirectories.Add(directory))
                {
                    yield return directory;
                }
            }
        }

        private static string GetExistingDirectoryName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            try
            {
                string fullPath = Path.GetFullPath(filePath);
                if (!File.Exists(fullPath))
                {
                    return null;
                }

                string directory = Path.GetDirectoryName(fullPath);
                return Directory.Exists(directory) ? directory : null;
            }
            catch
            {
                return null;
            }
        }

        private static List<SelectedReloadMethod> SelectSupportedMethods(TypeDefinition pluginType, ContentCompatibilityReport report)
        {
            List<SelectedReloadMethod> selectedMethods = new List<SelectedReloadMethod>();
            HashSet<string> seenMethods = new HashSet<string>(StringComparer.Ordinal);
            int discoveryIndex = 0;

            foreach (MethodDefinition method in pluginType.Methods)
            {
                CustomAttribute attribute = method.CustomAttributes.FirstOrDefault(entry =>
                    string.Equals(entry.AttributeType.FullName, ContentReloadEntryAttributeFullName, StringComparison.Ordinal));
                if (attribute == null)
                {
                    continue;
                }

                if (method.HasParameters)
                {
                    report.UnsupportedReason =
                        "Method '" + method.Name + "' uses [ContentReloadEntry] but is not parameterless. " +
                        "Strict content reload entry methods must not take parameters.";
                    return selectedMethods;
                }

                if (!TryReadAttributeStage(attribute, out int stageOrder))
                {
                    report.UnsupportedReason =
                        "Method '" + method.Name + "' uses [ContentReloadEntry] with an unsupported stage value.";
                    return selectedMethods;
                }

                selectedMethods.Add(new SelectedReloadMethod(
                    method,
                    stageOrder,
                    ReadAttributeOrder(attribute),
                    discoveryIndex++));
                seenMethods.Add(method.FullName ?? method.Name);
            }

            return selectedMethods
                .OrderBy(entry => entry.StageOrder)
                .ThenBy(entry => entry.Order)
                .ThenBy(entry => entry.DiscoveryIndex)
                .ToList();
        }

        private static bool TryReadAttributeStage(CustomAttribute attribute, out int stageOrder)
        {
            stageOrder = -1;
            if (attribute == null || attribute.ConstructorArguments.Count < 1)
            {
                return false;
            }

            object rawValue = attribute.ConstructorArguments[0].Value;
            if (rawValue == null)
            {
                return false;
            }

            try
            {
                stageOrder = Convert.ToInt32(rawValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadAttributeOrder(CustomAttribute attribute)
        {
            if (attribute == null)
            {
                return 0;
            }

            for (int i = 0; i < attribute.Properties.Count; i++)
            {
                CustomAttributeNamedArgument property = attribute.Properties[i];
                if (!string.Equals(property.Name, "Order", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    return Convert.ToInt32(property.Argument.Value);
                }
                catch
                {
                    return 0;
                }
            }

            return 0;
        }

        private static TypeDefinition FindPluginType(AssemblyDefinition assembly, string modGuid)
        {
            if (assembly == null)
            {
                return null;
            }

            foreach (TypeDefinition type in EnumerateTypes(assembly.MainModule.Types))
            {
                if (!type.HasCustomAttributes)
                {
                    continue;
                }

                foreach (CustomAttribute attribute in type.CustomAttributes)
                {
                    if (!string.Equals(attribute.AttributeType.FullName, "BepInEx.BepInPlugin", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (attribute.ConstructorArguments.Count > 0 &&
                        string.Equals(attribute.ConstructorArguments[0].Value as string, modGuid, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<TypeDefinition> EnumerateTypes(IEnumerable<TypeDefinition> roots)
        {
            if (roots == null)
            {
                yield break;
            }

            foreach (TypeDefinition type in roots)
            {
                if (type == null)
                {
                    continue;
                }

                yield return type;
                foreach (TypeDefinition nested in EnumerateTypes(type.NestedTypes))
                {
                    yield return nested;
                }
            }
        }

        private static void AddIgnoredMethodNotes(TypeDefinition pluginType, ContentCompatibilityReport report)
        {
            string[] ignoredMethodNames =
            {
                "RegisterBuildings",
                "RegisterBuildingEntities",
                "RegisterStatuses",
                "RegisterMoodles",
                "RegisterOptions",
                "RegisterSettings",
                "RegisterTiles",
                "RegisterSave",
                "RegisterSaveProviders"
            };

            for (int i = 0; i < ignoredMethodNames.Length; i++)
            {
                if (pluginType.Methods.Any(method => string.Equals(method.Name, ignoredMethodNames[i], StringComparison.Ordinal)))
                {
                    report.Notes.Add("Ignored unsupported method '" + ignoredMethodNames[i] + "' during strict content scan.");
                }
            }
        }

        private static string FindForbiddenCall(MethodDefinition method, HashSet<string> visitedMethods)
        {
            if (method == null || !method.HasBody)
            {
                return null;
            }

            string methodKey = method.FullName ?? method.Name;
            if (!visitedMethods.Add(methodKey))
            {
                return null;
            }

            foreach (Instruction instruction in method.Body.Instructions)
            {
                MethodReference calledMethod = instruction.Operand as MethodReference;
                if (calledMethod == null)
                {
                    continue;
                }

                string declaringType = calledMethod.DeclaringType != null ? calledMethod.DeclaringType.FullName : string.Empty;
                string methodName = calledMethod.Name ?? string.Empty;

                if (string.Equals(declaringType, "CUCoreLib.Registries.BuildingEntityRegistry", StringComparison.Ordinal))
                {
                    return "it calls BuildingEntityRegistry." + methodName + "(). Buildings are excluded from strict content reload.";
                }

                if (string.Equals(declaringType, "CUCoreLib.Registries.ModOptionsRegistry", StringComparison.Ordinal))
                {
                    return "it calls ModOptionsRegistry." + methodName + "(). Mod options are excluded from strict content reload.";
                }

                if (string.Equals(declaringType, "CUCoreLib.Registries.SaveRegistry", StringComparison.Ordinal))
                {
                    return "it calls SaveRegistry." + methodName + "(). Save providers are excluded from strict content reload.";
                }

                if (string.Equals(declaringType, "CUCoreLib.Registries.MoodleRegistry", StringComparison.Ordinal) ||
                    string.Equals(declaringType, "CUCoreLib.Registries.StatusRegistry", StringComparison.Ordinal))
                {
                    return "it calls " + calledMethod.DeclaringType.Name + "." + methodName + "(). Status and moodle registration are excluded from strict content reload.";
                }

                if (string.Equals(declaringType, "CUCoreLib.Networking.MultiplayerApi", StringComparison.Ordinal) ||
                    string.Equals(declaringType, "CUCoreLib.Networking.MultiplayerBridge", StringComparison.Ordinal) ||
                    string.Equals(declaringType, "CUCoreLib.Networking.MultiplayerSyncRegistry", StringComparison.Ordinal))
                {
                    return "it calls multiplayer registration/setup code. Multiplayer hooks are excluded from strict content reload.";
                }

                if (string.Equals(declaringType, "CUCoreLib.Registries.TileRegistry", StringComparison.Ordinal))
                {
                    return "it calls TileRegistry." + methodName + "(). Tile registration is excluded from strict content reload.";
                }

                if (string.Equals(declaringType, "CUCoreLib.Registries.ConsoleCommandRegistry", StringComparison.Ordinal))
                {
                    return "it calls ConsoleCommandRegistry." + methodName + "(). Console command registration is excluded from strict content reload.";
                }

                if (string.Equals(declaringType, "HarmonyLib.Harmony", StringComparison.Ordinal) ||
                    string.Equals(declaringType, "HarmonyLib.HarmonyMethod", StringComparison.Ordinal))
                {
                    return "it performs Harmony setup. Patch registration is excluded from strict content reload.";
                }

                MethodDefinition nestedMethod;
                try
                {
                    nestedMethod = calledMethod.Resolve();
                }
                catch
                {
                    nestedMethod = null;
                }

                if (nestedMethod == null || nestedMethod.Module != method.Module)
                {
                    continue;
                }

                string nestedForbiddenCall = FindForbiddenCall(nestedMethod, visitedMethods);
                if (!string.IsNullOrWhiteSpace(nestedForbiddenCall))
                {
                    return nestedForbiddenCall;
                }
            }

            return null;
        }

        private sealed class SelectedReloadMethod
        {
            public MethodDefinition Method { get; }
            public int StageOrder { get; }
            public int Order { get; }
            public int DiscoveryIndex { get; }

            public SelectedReloadMethod(MethodDefinition method, int stageOrder, int order, int discoveryIndex)
            {
                Method = method;
                StageOrder = stageOrder;
                Order = order;
                DiscoveryIndex = discoveryIndex;
            }
        }
    }
}
