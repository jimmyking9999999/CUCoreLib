using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CUCoreLib.ContentReload;

internal static class ContentCompatibilityScanner
{
    private const string ContentReloadEntryAttributeFullName = "CUCoreLib.Data.ContentReloadEntryAttribute";

    private static readonly string[] AllowedBuildingDefinitionMembers =
    [
        "ID",
        "Name",
        "Description",
        "Sprite",
        "SpriteAnimationId",
        "SortingOrder",
        "UseGlowPlantMaterial",
        "Scale",
        "ColliderSize",
        "ColliderOffset",
        "ColliderIsTrigger",
        "Layer",
        "AddRigidbody2D",
        "RigidbodyBodyType",
        "RigidbodyGravityScale",
        "Health",
        "RequireGround",
        "Metallic",
        "CantHit",
        "Animal",
        "IgnoreBodyOptimize",
        "DropChanceMultiplier",
        "ItemsDropOnDestroy",
        "AlwaysDrop",
        "ItemCategoriesToAdd",
        "GuaranteedDropAmount",
        "Placement",
        "GenerationStyle",
        "SpawnMinPerChunk",
        "SpawnMaxPerChunk",
        "SurfaceOffset",
        "RandomFlip",
        "SpawnInGround",
        "HitSoundReferenceId",
        "HitSound",
        "BlockFootstepSoundId",
        "RenderReferenceId",
        "CopyGlowPlantLayer",
        "HeatRadius",
        "HeatPerSecond",
        "MaxHeatBodyTemperature"
    ];

    internal static ContentCompatibilityReport Scan(ContentReloadCandidate candidate)
    {
        var report = new ContentCompatibilityReport
        {
            ModGuid = candidate?.ModGuid,
            ModName = candidate?.ModName,
            LoadedPluginPath = candidate?.LoadedPluginPath,
            OverridePath = candidate?.OverridePath,
            SelectedPath = candidate?.SelectedPath,
            SelectedHash = candidate?.SelectedHash,
            SelectedSourceLabel = candidate?.SelectedSourceLabel
        };

        if (candidate == null || string.IsNullOrWhiteSpace(candidate.SelectedPath))
        {
            report.UnsupportedReason =
                "No rebuilt DLL source path was found. The loaded plugin path and runtime override path were both unavailable.";
            return report;
        }

        if (!File.Exists(candidate.SelectedPath))
        {
            report.UnsupportedReason = "The selected rebuilt DLL path does not exist: " + candidate.SelectedPath;
            return report;
        }

        try
        {
            using var assembly =
                AssemblyDefinition.ReadAssembly(candidate.SelectedPath, CreateReaderParameters(candidate));
            var pluginType = FindPluginType(assembly, candidate.ModGuid);
            if (pluginType == null)
            {
                report.UnsupportedReason = "No [BepInPlugin] type matching '" + candidate.ModGuid +
                                           "' was found in the rebuilt DLL.";
                return report;
            }

            report.PluginTypeFullName = pluginType.FullName;

            var selectedMethods = SelectSupportedMethods(pluginType, report);
            if (!string.IsNullOrWhiteSpace(report.UnsupportedReason)) return report;

            AddIgnoredMethodNotes(pluginType, report);

            foreach (var selected in selectedMethods)
            {
                var forbiddenCall =
                    FindForbiddenCall(selected.Method, new HashSet<string>(StringComparer.Ordinal));
                if (!string.IsNullOrWhiteSpace(forbiddenCall))
                {
                    report.UnsupportedReason = "Method '" + selected.Method.Name +
                                               "' is not strict content-only: " + forbiddenCall;
                    return report;
                }

                report.RecognizedMethods.Add(selected.Method.Name);
            }

            if (report.RecognizedMethods.Count == 0)
                report.UnsupportedReason =
                    "No supported content reload entry methods were found. " +
                    "Add one or more [ContentReloadEntry(...)] parameterless methods.";
        }
        catch (Exception ex)
        {
            report.UnsupportedReason = "Compatibility scan failed: " + ex.Message;
        }

        return report;
    }

    private static ReaderParameters CreateReaderParameters(ContentReloadCandidate candidate)
    {
        var resolver = new DefaultAssemblyResolver();
        foreach (var directory in BuildResolverSearchDirectories(candidate))
            try
            {
                resolver.AddSearchDirectory(directory);
            }
            catch
            {
                // ignored
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
        var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] seedPaths =
        [
            candidate?.SelectedPath,
            candidate?.LoadedPluginPath,
            candidate?.OverridePath,
            typeof(CUCoreLibPlugin).Assembly.Location,
            Assembly.GetExecutingAssembly().Location
        ];

        foreach (var path in seedPaths)
        {
            var directory = GetExistingDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && seenDirectories.Add(directory)) yield return directory;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string directory;
            try
            {
                directory = GetExistingDirectoryName(assembly.Location);
            }
            catch
            {
                directory = null;
            }

            if (!string.IsNullOrWhiteSpace(directory) && seenDirectories.Add(directory)) yield return directory;
        }
    }

    private static string GetExistingDirectoryName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath)) return null;

            var directory = Path.GetDirectoryName(fullPath);
            return Directory.Exists(directory) ? directory : null;
        }
        catch
        {
            return null;
        }
    }

    private static List<SelectedReloadMethod> SelectSupportedMethods(TypeDefinition pluginType,
        ContentCompatibilityReport report)
    {
        var selectedMethods = new List<SelectedReloadMethod>();
        // not use seenMethods
        var seenMethods = new HashSet<string>(StringComparer.Ordinal);
        var discoveryIndex = 0;

        foreach (var method in pluginType.Methods)
        {
            var attribute = method.CustomAttributes.FirstOrDefault(entry =>
                string.Equals(entry.AttributeType.FullName, ContentReloadEntryAttributeFullName,
                    StringComparison.Ordinal));
            if (attribute == null) continue;

            if (method.HasParameters)
            {
                report.UnsupportedReason =
                    "Method '" + method.Name + "' uses [ContentReloadEntry] but is not parameterless. " +
                    "Strict content reload entry methods must not take parameters.";
                return selectedMethods;
            }

            if (!TryReadAttributeStage(attribute, out var stageOrder))
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
        if (attribute == null || attribute.ConstructorArguments.Count < 1) return false;

        var rawValue = attribute.ConstructorArguments[0].Value;
        if (rawValue == null) return false;

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
        if (attribute == null) return 0;

        foreach (var property in attribute.Properties)
        {
            if (!string.Equals(property.Name, "Order", StringComparison.Ordinal)) continue;

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
        if (assembly == null) return null;

        foreach (var type in EnumerateTypes(assembly.MainModule.Types))
        {
            if (!type.HasCustomAttributes) continue;

            foreach (var attribute in type.CustomAttributes)
            {
                if (!string.Equals(attribute.AttributeType.FullName, "BepInEx.BepInPlugin",
                        StringComparison.Ordinal)) continue;

                if (attribute.ConstructorArguments.Count > 0 &&
                    string.Equals(attribute.ConstructorArguments[0].Value as string, modGuid,
                        StringComparison.Ordinal))
                    return type;
            }
        }

        return null;
    }

    private static IEnumerable<TypeDefinition> EnumerateTypes(IEnumerable<TypeDefinition> roots)
    {
        if (roots == null) yield break;

        foreach (var type in roots)
        {
            if (type == null) continue;

            yield return type;
            foreach (var nested in EnumerateTypes(type.NestedTypes)) yield return nested;
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

        foreach (var ignoredMethodName in ignoredMethodNames)
            if (pluginType.Methods.Any(method =>
                    string.Equals(method.Name, ignoredMethodName, StringComparison.Ordinal)))
                report.Notes.Add("Ignored unsupported method '" + ignoredMethodName +
                                 "' during strict content scan.");
    }

    private static string FindForbiddenCall(MethodDefinition method, HashSet<string> visitedMethods)
    {
        if (method == null || !method.HasBody) return null;

        var methodKey = method.FullName ?? method.Name;
        if (!visitedMethods.Add(methodKey)) return null;

        foreach (var instruction in method.Body.Instructions)
        {
            var calledMethod = instruction.Operand as MethodReference;
            if (calledMethod == null) continue;

            var declaringType = calledMethod.DeclaringType != null
                ? calledMethod.DeclaringType.FullName
                : string.Empty;
            var methodName = calledMethod.Name ?? string.Empty;

            if (string.Equals(declaringType, "CUCoreLib.Registries.BuildingEntityRegistry",
                    StringComparison.Ordinal))
            {
                if (string.Equals(methodName, "AddDrop", StringComparison.Ordinal)) continue;

                if (!string.Equals(methodName, "Register", StringComparison.Ordinal))
                    return "it calls BuildingEntityRegistry." + methodName +
                           "(). Only basic building registration is supported during strict content reload.";

                var buildingDefinitionIssue = FindUnsupportedBuildingDefinitionUsage(method, calledMethod);
                if (!string.IsNullOrWhiteSpace(buildingDefinitionIssue)) return buildingDefinitionIssue;
            }

            if (string.Equals(declaringType, "CUCoreLib.Registries.ModOptionsRegistry", StringComparison.Ordinal))
                return "it calls ModOptionsRegistry." + methodName +
                       "(). Mod options are excluded from strict content reload.";

            if (string.Equals(declaringType, "CUCoreLib.Registries.SaveRegistry", StringComparison.Ordinal))
                return "it calls SaveRegistry." + methodName +
                       "(). Save providers are excluded from strict content reload.";

            if (string.Equals(declaringType, "CUCoreLib.Registries.MoodleRegistry", StringComparison.Ordinal) ||
                string.Equals(declaringType, "CUCoreLib.Registries.StatusRegistry", StringComparison.Ordinal))
                return "it calls " + calledMethod.DeclaringType?.Name + "." + methodName +
                       "(). Status and moodle registration are excluded from strict content reload.";

            if (string.Equals(declaringType, "CUCoreLib.Networking.MultiplayerApi", StringComparison.Ordinal) ||
                string.Equals(declaringType, "CUCoreLib.Networking.MultiplayerBridge", StringComparison.Ordinal) ||
                string.Equals(declaringType, "CUCoreLib.Networking.MultiplayerSyncRegistry",
                    StringComparison.Ordinal))
                return
                    "it calls multiplayer registration/setup code. Multiplayer hooks are excluded from strict content reload.";

            if (string.Equals(declaringType, "CUCoreLib.Registries.TileRegistry", StringComparison.Ordinal))
                return "it calls TileRegistry." + methodName +
                       "(). Tile registration is excluded from strict content reload.";

            if (string.Equals(declaringType, "CUCoreLib.Registries.ConsoleCommandRegistry",
                    StringComparison.Ordinal))
                return "it calls ConsoleCommandRegistry." + methodName +
                       "(). Console command registration is excluded from strict content reload.";

            if (string.Equals(declaringType, "HarmonyLib.Harmony", StringComparison.Ordinal) ||
                string.Equals(declaringType, "HarmonyLib.HarmonyMethod", StringComparison.Ordinal))
                return "it performs Harmony setup. Patch registration is excluded from strict content reload.";

            MethodDefinition nestedMethod;
            try
            {
                nestedMethod = calledMethod.Resolve();
            }
            catch
            {
                nestedMethod = null;
            }

            if (nestedMethod == null || nestedMethod.Module != method.Module) continue;

            var nestedForbiddenCall = FindForbiddenCall(nestedMethod, visitedMethods);
            if (!string.IsNullOrWhiteSpace(nestedForbiddenCall)) return nestedForbiddenCall;
        }

        return null;
    }

    private static string FindUnsupportedBuildingDefinitionUsage(MethodDefinition callingMethod,
        MethodReference calledMethod)
    {
        if (callingMethod == null || !callingMethod.HasBody || calledMethod == null) return null;

        IList<Instruction> instructions = callingMethod.Body.Instructions;
        for (var i = 0; i < instructions.Count; i++)
        {
            if (!ReferenceEquals(instructions[i].Operand, calledMethod)) continue;

            for (var scanIndex = i - 1; scanIndex >= 0; scanIndex--)
            {
                var scan = instructions[scanIndex];
                var ctorReference = scan.Operand as MethodReference;
                if (scan.OpCode != OpCodes.Newobj || ctorReference == null) continue;

                var ctorDeclaringType = ctorReference.DeclaringType;
                if (ctorDeclaringType == null ||
                    !string.Equals(ctorDeclaringType.FullName, "CUCoreLib.Data.CustomBuildingEntityDefinition",
                        StringComparison.Ordinal))
                    break;

                return ValidateBuildingDefinitionInitialization(instructions, scanIndex, i);
            }

            return null;
        }

        return null;
    }

    private static string ValidateBuildingDefinitionInitialization(IList<Instruction> instructions, int startIndex,
        int endIndex)
    {
        var allowedMembers = new HashSet<string>(AllowedBuildingDefinitionMembers, StringComparer.Ordinal);
        for (var i = startIndex + 1; i < endIndex; i++)
        {
            var instruction = instructions[i];
            var member = instruction.Operand as MemberReference;
            var declaringType = member?.DeclaringType;
            if (member == null ||
                declaringType == null ||
                !string.Equals(declaringType.FullName, "CUCoreLib.Data.CustomBuildingEntityDefinition",
                    StringComparison.Ordinal))
                continue;

            var memberName = member.Name ?? string.Empty;
            if (string.Equals(memberName, "ConfigurePrefab", StringComparison.Ordinal) ||
                string.Equals(memberName, "ConfigureInstance", StringComparison.Ordinal) ||
                string.Equals(memberName, "PlaceCheck", StringComparison.Ordinal) ||
                string.Equals(memberName, "Components", StringComparison.Ordinal) ||
                string.Equals(memberName, "SpawnComponents", StringComparison.Ordinal) ||
                (instruction.OpCode == OpCodes.Stfld && !allowedMembers.Contains(memberName)))
                return "it registers a building definition using unsupported member '" + memberName +
                       "'. Only basic/scriptless building definitions can be hot reloaded.";
        }

        return null;
    }

    private sealed class SelectedReloadMethod
    {
        public SelectedReloadMethod(MethodDefinition method, int stageOrder, int order, int discoveryIndex)
        {
            Method = method;
            StageOrder = stageOrder;
            Order = order;
            DiscoveryIndex = discoveryIndex;
        }

        public MethodDefinition Method { get; }
        public int StageOrder { get; }
        public int Order { get; }
        public int DiscoveryIndex { get; }
    }
}