using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CUCoreLib.Data;

namespace CUCoreLib.ContentReload
{
    internal static class ContentCompatibilityScanner
    {
        private const string ContentReloadManagerTypeFullName = "CUCoreLib.ContentReload.ContentReloadManager";

        private static readonly string[] AllowedBuildingDefinitionMembers =
        {
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
        };

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
                using (var assembly =
                       AssemblyDefinition.ReadAssembly(candidate.SelectedPath, CreateReaderParameters(candidate)))
                {
                    var pluginType = FindPluginType(assembly, candidate.ModGuid);
                    if (pluginType == null)
                    {
                        report.UnsupportedReason = "No [BepInPlugin] type matching '" + candidate.ModGuid +
                                                   "' was found in the rebuilt DLL.";
                        return report;
                    }

                    report.PluginTypeFullName = pluginType.FullName;

                    var discoveryIndex = 0;
                    DiscoverEnableHotReloadMethods(pluginType, report, ref discoveryIndex);

                    report.Methods.Sort((left, right) =>
                    {
                        var stage = left.Stage.CompareTo(right.Stage);
                        if (stage != 0) return stage;

                        var order = left.Order.CompareTo(right.Order);
                        if (order != 0) return order;

                        return left.DiscoveryIndex.CompareTo(right.DiscoveryIndex);
                    });

                    foreach (var discoveredMethod in report.Methods)
                        report.RecognizedMethods.Add(discoveredMethod.DisplayName);

                    if (report.Methods.Count == 0)
                    {
                        if (report.SkippedMethods.Count > 0)
                            report.UnsupportedReason =
                                "No supported content reload entry methods were found. All discovered candidates were skipped.";
                        else
                            report.UnsupportedReason =
                                "No supported content reload entry methods were found after EnableHotReload(GUID). CUCoreLib could not discover any replayable supported content registrations.";
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
            {
                candidate?.SelectedPath,
                candidate?.LoadedPluginPath,
                candidate?.OverridePath,
                typeof(CUCoreLibPlugin).Assembly.Location,
                Assembly.GetExecutingAssembly().Location
            };

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

        private static void DiscoverEnableHotReloadMethods(TypeDefinition pluginType, ContentCompatibilityReport report,
            ref int discoveryIndex)
        {
            if (pluginType == null)
            {
                report.UnsupportedReason = "Plugin type was null.";
                return;
            }

            var replayMode = ContentReloadManager.GetReloadMode(report.ModGuid);

            var awakeMethod = pluginType.Methods.FirstOrDefault(method =>
                string.Equals(method.Name, "Awake", StringComparison.Ordinal) &&
                !method.HasParameters);
            if (awakeMethod == null || !awakeMethod.HasBody)
            {
                report.UnsupportedReason =
                    "Plugin '" + pluginType.FullName +
                    "' must define an Awake() method with a ContentReloadManager.EnableHotReload(GUID) call.";
                return;
            }

            if (!TryFindEnableHotReloadMarker(awakeMethod, report.ModGuid, out var markerIndex, out var markerReason))
            {
                report.UnsupportedReason = markerReason;
                return;
            }

            report.UsesEnableHotReloadContract = true;

            var discoveredAny = false;
            var visitedReplayMethods = new HashSet<string>(StringComparer.Ordinal);
            var afterMarker = awakeMethod.Body.Instructions.Skip(markerIndex + 1).ToList();
            foreach (var instruction in afterMarker)
            {
                if (!IsDirectMethodCall(instruction, out var calledMethod)) continue;
                if (calledMethod == null) continue;

                MethodDefinition resolvedMethod;
                try
                {
                    resolvedMethod = calledMethod.Resolve();
                }
                catch
                {
                    resolvedMethod = null;
                }

                if (resolvedMethod == null || resolvedMethod.Module != pluginType.Module) continue;
                if (!IsEligibleReplayRootMethod(resolvedMethod)) continue;

                if (DiscoverReplayRootsFromMethod(pluginType, resolvedMethod, report, replayMode, ref discoveryIndex,
                        visitedReplayMethods))
                    discoveredAny = true;
            }

            if (!discoveredAny)
                report.UnsupportedReason =
                    "Awake() called EnableHotReload('" + report.ModGuid +
                    "') but CUCoreLib could not discover any replayable supported content registrations after the marker.";
        }

        private static bool TryInferStage(MethodDefinition method, out ContentReloadEntryStage stage, out string reason)
        {
            stage = ContentReloadEntryStage.LoadAssets;
            reason = null;

            if (method == null || !method.HasBody)
            {
                reason = "Method '" + BuildMethodDisplayName(method) +
                         "' has no method body. Strict content reload can only inspect methods with IL bodies.";
                return false;
            }

            var surfaceUsages = AnalyzeMethodSurfaceUsage(method, new HashSet<string>(StringComparer.Ordinal));

            if (!string.IsNullOrWhiteSpace(surfaceUsages.UnsupportedReason))
            {
                reason = surfaceUsages.UnsupportedReason;
                return false;
            }

            if (surfaceUsages.Surfaces.Count == 0)
            {
                if (surfaceUsages.UsesAssetLoader)
                {
                    stage = ContentReloadEntryStage.LoadAssets;
                    return true;
                }

                reason = "Method '" + BuildMethodDisplayName(method) +
                         "' did not call a supported content registration API. Replay roots must directly register supported content or call helper methods that do.";
                return false;
            }

            if (surfaceUsages.Surfaces.Count > 1)
            {
                reason = "Method '" + BuildMethodDisplayName(method) +
                         "' touches multiple supported registration surfaces. CUCoreLib can often recover by replaying deeper helper methods, but this method itself is not a direct replay leaf.";
                return false;
            }

            var surface = surfaceUsages.Surfaces.First();
            stage = SurfaceToStage(surface);
            return true;
        }

        private static bool TryFindEnableHotReloadMarker(MethodDefinition awakeMethod, string modGuid,
            out int markerIndex, out string reason)
        {
            markerIndex = -1;
            reason = "Awake() must call ContentReloadManager.EnableHotReload(\"" + modGuid + "\").";

            if (awakeMethod == null || !awakeMethod.HasBody) return false;

            var instructions = awakeMethod.Body.Instructions;
            for (var i = 0; i < instructions.Count; i++)
            {
                if (!IsDirectMethodCall(instructions[i], out var calledMethod) || calledMethod == null) continue;
                if (!string.Equals(calledMethod.Name, "EnableHotReload", StringComparison.Ordinal)) continue;
                if (!string.Equals(calledMethod.DeclaringType?.FullName, ContentReloadManagerTypeFullName,
                        StringComparison.Ordinal)) continue;

                if (!TryReadPreviousStringArgument(instructions, i, out var argumentGuid))
                {
                    reason = "Awake() must call ContentReloadManager.EnableHotReload(GUID) with a literal GUID argument.";
                    return false;
                }

                if (!string.Equals(argumentGuid, modGuid, StringComparison.Ordinal))
                {
                    reason = "Awake() called ContentReloadManager.EnableHotReload(\"" + argumentGuid +
                             "\") but the plugin GUID is \"" + modGuid + "\".";
                    return false;
                }

                markerIndex = i;
                return true;
            }

            return false;
        }

        private static bool DiscoverReplayRootsFromMethod(TypeDefinition pluginType, MethodDefinition method,
            ContentCompatibilityReport report, HotReloadMode replayMode, ref int discoveryIndex,
            HashSet<string> visitedReplayMethods)
        {
            if (method == null) return false;

            var methodKey = method.FullName ?? method.Name;
            if (!visitedReplayMethods.Add(methodKey)) return false;

            if (ShouldIgnoreStartupOnlyMethod(method))
                return false;

            if (TryInferStage(method, out var stage, out var reason))
            {
                AddRecognizedMethod(report, method, stage, 0, IsPluginMethod(pluginType, method), discoveryIndex++);
                return true;
            }

            if (replayMode == HotReloadMode.FlexibleGuarded && MethodOnlyCallsLocalHelpers(method))
            {
                var discoveredAny = false;
                foreach (var nestedMethod in ResolveLocalCalledMethods(method))
                    if (DiscoverReplayRootsFromMethod(pluginType, nestedMethod, report, replayMode, ref discoveryIndex,
                            visitedReplayMethods))
                        discoveredAny = true;

                if (discoveredAny) return true;
            }

            if (!ShouldSuppressReplaySkip(method, reason))
                AddSkippedMethod(report, method, reason);

            return false;
        }

        private static SurfaceUsageAnalysis AnalyzeMethodSurfaceUsage(MethodDefinition method,
            HashSet<string> visitedMethods)
        {
            var analysis = new SurfaceUsageAnalysis();
            if (method == null || !method.HasBody) return analysis;

            var methodKey = method.FullName ?? method.Name;
            if (!visitedMethods.Add(methodKey)) return analysis;

            foreach (var instruction in method.Body.Instructions)
            {
                var calledMethod = instruction.Operand as MethodReference;
                if (calledMethod == null) continue;

                var surface = ClassifySupportedSurfaceCall(method, calledMethod, out var unsupportedReason);
                if (!string.IsNullOrWhiteSpace(unsupportedReason))
                {
                    analysis.UnsupportedReason = unsupportedReason;
                    return analysis;
                }

                if (surface.HasValue) analysis.Surfaces.Add(surface.Value);

                if (IsAssetLoaderCall(calledMethod)) analysis.UsesAssetLoader = true;

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

                var nestedAnalysis = AnalyzeMethodSurfaceUsage(nestedMethod, visitedMethods);
                if (!string.IsNullOrWhiteSpace(nestedAnalysis.UnsupportedReason))
                {
                    analysis.UnsupportedReason = nestedAnalysis.UnsupportedReason;
                    return analysis;
                }

                analysis.UsesAssetLoader |= nestedAnalysis.UsesAssetLoader;
                foreach (var nestedSurface in nestedAnalysis.Surfaces) analysis.Surfaces.Add(nestedSurface);
            }

            return analysis;
        }

        private static ContentReloadEntryStage SurfaceToStage(ContentReloadSurface surface)
        {
            switch (surface)
            {
                case ContentReloadSurface.Locale:
                    return ContentReloadEntryStage.RegisterLocale;
                case ContentReloadSurface.Liquids:
                    return ContentReloadEntryStage.RegisterLiquids;
                case ContentReloadSurface.Items:
                    return ContentReloadEntryStage.RegisterItems;
                case ContentReloadSurface.Buildings:
                    return ContentReloadEntryStage.RegisterBuildings;
                case ContentReloadSurface.Recipes:
                    return ContentReloadEntryStage.RegisterRecipes;
                default:
                    return ContentReloadEntryStage.LoadAssets;
            }
        }

        private static string ValidateMethodForStage(MethodDefinition method, ContentReloadEntryStage stage,
            bool allowSurfaceMismatch)
        {
            if (stage == ContentReloadEntryStage.RegisterBuildings)
            {
                var buildingIssue = FindBuildingDefinitionIssue(method, new HashSet<string>(StringComparer.Ordinal));
                if (!string.IsNullOrWhiteSpace(buildingIssue)) return buildingIssue;
            }

            var analysis = AnalyzeMethodSurfaceUsage(method, new HashSet<string>(StringComparer.Ordinal));
            if (!string.IsNullOrWhiteSpace(analysis.UnsupportedReason)) return analysis.UnsupportedReason;

            if (allowSurfaceMismatch || analysis.Surfaces.Count == 0) return null;

            if (analysis.Surfaces.Count > 1)
                return "Method '" + BuildMethodDisplayName(method) +
                       "' touches multiple supported registration surfaces. CUCoreLib can often recover by replaying deeper helper methods, but this method itself is not a direct replay leaf.";

            var requiredSurface = StageToSurface(stage);
            if (requiredSurface == ContentReloadSurface.None) return null;

            return analysis.Surfaces.Contains(requiredSurface)
                ? null
                : "Method '" + BuildMethodDisplayName(method) + "' is tagged for stage '" + stage +
                  "' but does not call the matching supported registration API.";
        }

        private static ContentReloadSurface StageToSurface(ContentReloadEntryStage stage)
        {
            switch (stage)
            {
                case ContentReloadEntryStage.RegisterText:
                case ContentReloadEntryStage.RegisterLocale:
                    return ContentReloadSurface.Locale;
                case ContentReloadEntryStage.RegisterLiquids:
                    return ContentReloadSurface.Liquids;
                case ContentReloadEntryStage.RegisterItems:
                    return ContentReloadSurface.Items;
                case ContentReloadEntryStage.RegisterBuildings:
                    return ContentReloadSurface.Buildings;
                case ContentReloadEntryStage.RegisterRecipes:
                    return ContentReloadSurface.Recipes;
                default:
                    return ContentReloadSurface.None;
            }
        }

        private static ContentReloadSurface? ClassifySupportedSurfaceCall(MethodDefinition callingMethod,
            MethodReference calledMethod, out string unsupportedReason)
        {
            unsupportedReason = null;

            var declaringType = calledMethod.DeclaringType != null
                ? calledMethod.DeclaringType.FullName
                : string.Empty;
            var methodName = calledMethod.Name ?? string.Empty;

            if (string.Equals(declaringType, "CUCoreLib.Registries.ItemRegistry", StringComparison.Ordinal) &&
                string.Equals(methodName, "Register", StringComparison.Ordinal))
                return ContentReloadSurface.Items;

            if (string.Equals(declaringType, "CUCoreLib.Registries.LiquidRegistry", StringComparison.Ordinal) &&
                string.Equals(methodName, "Register", StringComparison.Ordinal))
                return ContentReloadSurface.Liquids;

            if (string.Equals(declaringType, "CUCoreLib.Registries.RecipeRegistry", StringComparison.Ordinal) &&
                string.Equals(methodName, "Register", StringComparison.Ordinal))
                return ContentReloadSurface.Recipes;

            if (string.Equals(declaringType, "CUCoreLib.Registries.LocaleRegistry", StringComparison.Ordinal) &&
                methodName.StartsWith("Register", StringComparison.Ordinal))
                return ContentReloadSurface.Locale;

            if (string.Equals(declaringType, "CUCoreLib.Helpers.LocaleLoader", StringComparison.Ordinal))
                return ContentReloadSurface.Locale;

            if (string.Equals(declaringType, "CUCoreLib.Registries.BuildingEntityRegistry",
                    StringComparison.Ordinal))
            {
                if (string.Equals(methodName, "AddDrop", StringComparison.Ordinal)) return null;

                if (string.Equals(methodName, "Register", StringComparison.Ordinal))
                {
                    var buildingDefinitionIssue = FindUnsupportedBuildingDefinitionUsage(callingMethod, calledMethod);
                    if (!string.IsNullOrWhiteSpace(buildingDefinitionIssue))
                    {
                        unsupportedReason = buildingDefinitionIssue;
                        return null;
                    }

                    return ContentReloadSurface.Buildings;
                }

                unsupportedReason = "Method '" + BuildMethodDisplayName(callingMethod) +
                                    "' calls BuildingEntityRegistry." + methodName +
                                    "(). Only basic building registration is supported during strict content reload.";
                return null;
            }

            if (string.Equals(declaringType, "CUCoreLib.Registries.ModOptionsRegistry", StringComparison.Ordinal))
            {
                unsupportedReason = BuildUnsupportedReason(callingMethod, "it calls ModOptionsRegistry." + methodName +
                                                                         "(). Mod options are excluded from strict content reload.");
                return null;
            }

            if (string.Equals(declaringType, "CUCoreLib.Registries.SaveRegistry", StringComparison.Ordinal))
            {
                unsupportedReason = BuildUnsupportedReason(callingMethod, "it calls SaveRegistry." + methodName +
                                                                         "(). Save providers are excluded from strict content reload.");
                return null;
            }

            if (string.Equals(declaringType, "CUCoreLib.Registries.MoodleRegistry", StringComparison.Ordinal) ||
                string.Equals(declaringType, "CUCoreLib.Registries.StatusRegistry", StringComparison.Ordinal))
            {
                unsupportedReason = BuildUnsupportedReason(callingMethod,
                    "it calls " + calledMethod.DeclaringType?.Name + "." + methodName +
                    "(). Status and moodle registration are excluded from strict content reload.");
                return null;
            }

            if (string.Equals(declaringType, "CUCoreLib.Networking.MultiplayerApi", StringComparison.Ordinal) ||
                string.Equals(declaringType, "CUCoreLib.Networking.MultiplayerBridge", StringComparison.Ordinal) ||
                string.Equals(declaringType, "CUCoreLib.Networking.MultiplayerSyncRegistry",
                    StringComparison.Ordinal))
            {
                unsupportedReason = BuildUnsupportedReason(callingMethod,
                    "it calls multiplayer registration/setup code. Multiplayer hooks are excluded from strict content reload.");
                return null;
            }

            if (string.Equals(declaringType, "CUCoreLib.Registries.TileRegistry", StringComparison.Ordinal))
            {
                unsupportedReason = BuildUnsupportedReason(callingMethod, "it calls TileRegistry." + methodName +
                                                                         "(). Tile registration is excluded from strict content reload.");
                return null;
            }

            if (string.Equals(declaringType, "CUCoreLib.Registries.StructureRegistry", StringComparison.Ordinal))
            {
                unsupportedReason = BuildUnsupportedReason(callingMethod, "it calls StructureRegistry." + methodName +
                                                                         "(). Structure registration is excluded from strict content reload.");
                return null;
            }

            if (string.Equals(declaringType, "CUCoreLib.Registries.ConsoleCommandRegistry",
                    StringComparison.Ordinal))
            {
                unsupportedReason = BuildUnsupportedReason(callingMethod,
                    "it calls ConsoleCommandRegistry." + methodName +
                    "(). Console command registration is excluded from strict content reload.");
                return null;
            }

            if (string.Equals(declaringType, "HarmonyLib.Harmony", StringComparison.Ordinal) ||
                string.Equals(declaringType, "HarmonyLib.HarmonyMethod", StringComparison.Ordinal))
            {
                unsupportedReason = BuildUnsupportedReason(callingMethod,
                    "it performs Harmony setup. Patch registration is excluded from strict content reload.");
                return null;
            }

            if (string.Equals(declaringType, "CUCoreLib.Helpers.CustomInstantiate", StringComparison.Ordinal) ||
                string.Equals(declaringType, "Body", StringComparison.Ordinal) &&
                (string.Equals(methodName, "PickUpItem", StringComparison.Ordinal) ||
                 string.Equals(methodName, "DropItem", StringComparison.Ordinal)))
            {
                unsupportedReason = BuildUnsupportedReason(callingMethod,
                    "it mutates the live scene or inventory. Runtime scene side effects are excluded from strict content reload.");
                return null;
            }

            return null;
        }

        private static string BuildUnsupportedReason(MethodDefinition method, string detail)
        {
            return "Method '" + BuildMethodDisplayName(method) + "' is not strict content-only: " + detail;
        }

        private static string FindBuildingDefinitionIssue(MethodDefinition method, HashSet<string> visitedMethods)
        {
            if (method == null || !method.HasBody) return null;

            var methodKey = method.FullName ?? method.Name;
            if (!visitedMethods.Add(methodKey)) return null;

            foreach (var instruction in method.Body.Instructions)
            {
                var calledMethod = instruction.Operand as MethodReference;
                if (calledMethod == null) continue;

                if (string.Equals(calledMethod.DeclaringType?.FullName, "CUCoreLib.Registries.BuildingEntityRegistry",
                        StringComparison.Ordinal) &&
                    string.Equals(calledMethod.Name, "Register", StringComparison.Ordinal))
                {
                    var issue = FindUnsupportedBuildingDefinitionUsage(method, calledMethod);
                    if (!string.IsNullOrWhiteSpace(issue)) return issue;
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

                if (nestedMethod == null || nestedMethod.Module != method.Module) continue;

                var nestedIssue = FindBuildingDefinitionIssue(nestedMethod, visitedMethods);
                if (!string.IsNullOrWhiteSpace(nestedIssue)) return nestedIssue;
            }

            return null;
        }

        private static bool IsAssetLoaderCall(MethodReference calledMethod)
        {
            return string.Equals(calledMethod.DeclaringType?.FullName, "CUCoreLib.Helpers.AssetLoader",
                       StringComparison.Ordinal) ||
                   string.Equals(calledMethod.DeclaringType?.FullName, "CUCoreLib.Helpers.FileLoader",
                       StringComparison.Ordinal);
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

        private static void AddRecognizedMethod(ContentCompatibilityReport report, MethodDefinition method,
            ContentReloadEntryStage stage, int order, bool isPluginMethod, int discoveryIndex)
        {
            var displayName = BuildMethodDisplayName(method);
            if (report.Methods.Any(existing => string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal)))
                return;

            report.Methods.Add(new DiscoveredReloadMethod
            {
                DisplayName = displayName,
                DeclaringTypeFullName = method.DeclaringType.FullName,
                MethodName = method.Name,
                IsStatic = method.IsStatic,
                IsPluginMethod = isPluginMethod,
                Stage = stage,
                Order = order,
                DiscoveryIndex = discoveryIndex
            });
        }

        private static void AddSkippedMethod(ContentCompatibilityReport report, MethodDefinition method, string reason)
        {
            var displayName = BuildMethodDisplayName(method);
            if (report.SkippedMethods.Any(existing =>
                    string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal) &&
                    string.Equals(existing.Reason, reason, StringComparison.Ordinal)))
                return;

            report.SkippedMethods.Add(new SkippedReloadMethod
            {
                DisplayName = displayName,
                Reason = reason
            });
        }

        private static string BuildMethodDisplayName(MethodDefinition method)
        {
            if (method == null) return "<unknown method>";

            var declaringType = method.DeclaringType != null ? method.DeclaringType.FullName : "<unknown type>";
            return declaringType + "." + method.Name;
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

        private static bool IsDirectMethodCall(Instruction instruction, out MethodReference calledMethod)
        {
            calledMethod = instruction?.Operand as MethodReference;
            if (calledMethod == null || instruction == null) return false;

            return instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt;
        }

        private static bool TryReadPreviousStringArgument(IList<Instruction> instructions, int callIndex,
            out string value)
        {
            value = null;
            if (instructions == null || callIndex <= 0) return false;

            for (var i = callIndex - 1; i >= 0; i--)
            {
                var instruction = instructions[i];
                if (instruction.OpCode == OpCodes.Ldstr)
                {
                    value = instruction.Operand as string;
                    return !string.IsNullOrWhiteSpace(value);
                }

                if (instruction.OpCode.FlowControl == FlowControl.Call ||
                    instruction.OpCode.FlowControl == FlowControl.Branch ||
                    instruction.OpCode.FlowControl == FlowControl.Cond_Branch)
                    break;
            }

            return false;
        }

        private static bool IsEligibleReplayRootMethod(MethodDefinition method)
        {
            if (method == null) return false;
            if (method.IsConstructor || method.IsGetter || method.IsSetter || method.IsAddOn || method.IsRemoveOn)
                return false;
            if (method.HasParameters) return false;
            if (method.ReturnType != null && method.ReturnType.FullName != "System.Void") return false;
            if ((method.Attributes & Mono.Cecil.MethodAttributes.SpecialName) != 0) return false;
            if (method.Name.StartsWith("<", StringComparison.Ordinal)) return false;

            return true;
        }

        private static bool ShouldIgnoreStartupOnlyMethod(MethodDefinition method)
        {
            if (method == null) return false;

            var declaringType = method.DeclaringType?.FullName ?? string.Empty;
            if (string.Equals(declaringType, "HarmonyLib.Harmony", StringComparison.Ordinal)) return true;
            if (string.Equals(declaringType, "FantasyMod.FantasyGameplayHooks", StringComparison.Ordinal) &&
                string.Equals(method.Name, "PatchAll", StringComparison.Ordinal)) return true;

            return false;
        }

        private static bool MethodOnlyCallsLocalHelpers(MethodDefinition method)
        {
            if (method == null || !method.HasBody) return false;

            var hasLocalCall = false;
            foreach (var instruction in method.Body.Instructions)
            {
                if (!IsDirectMethodCall(instruction, out var calledMethod) || calledMethod == null) continue;

                MethodDefinition resolvedMethod;
                try
                {
                    resolvedMethod = calledMethod.Resolve();
                }
                catch
                {
                    resolvedMethod = null;
                }

                if (resolvedMethod == null || resolvedMethod.Module != method.Module) return false;
                if (!IsEligibleReplayRootMethod(resolvedMethod)) return false;
                hasLocalCall = true;
            }

            return hasLocalCall;
        }

        private static IEnumerable<MethodDefinition> ResolveLocalCalledMethods(MethodDefinition method)
        {
            if (method == null || !method.HasBody) yield break;

            foreach (var instruction in method.Body.Instructions)
            {
                if (!IsDirectMethodCall(instruction, out var calledMethod) || calledMethod == null) continue;

                MethodDefinition resolvedMethod;
                try
                {
                    resolvedMethod = calledMethod.Resolve();
                }
                catch
                {
                    resolvedMethod = null;
                }

                if (resolvedMethod == null || resolvedMethod.Module != method.Module) continue;
                if (!IsEligibleReplayRootMethod(resolvedMethod)) continue;
                yield return resolvedMethod;
            }
        }

        private static bool ShouldSuppressReplaySkip(MethodDefinition method, string reason)
        {
            if (method == null) return false;
            if (string.IsNullOrWhiteSpace(reason)) return true;

            return reason.IndexOf("did not call a supported content registration API", StringComparison.Ordinal) >= 0 &&
                   MethodOnlyCallsLocalHelpers(method);
        }

        private static bool IsPluginMethod(TypeDefinition pluginType, MethodDefinition method)
        {
            return pluginType != null &&
                   method != null &&
                   string.Equals(pluginType.FullName, method.DeclaringType?.FullName, StringComparison.Ordinal);
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
                    instruction.OpCode == OpCodes.Stfld && !allowedMembers.Contains(memberName))
                    return "Method '" + member.DeclaringType.FullName +
                           "' registers a building definition using unsupported member '" + memberName +
                           "'. Only basic/scriptless building definitions can be hot reloaded.";
            }

            return null;
        }

        private sealed class SurfaceUsageAnalysis
        {
            public HashSet<ContentReloadSurface> Surfaces { get; } = new HashSet<ContentReloadSurface>();
            public bool UsesAssetLoader { get; set; }
            public string UnsupportedReason { get; set; }
        }
    }
}
