using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using CUCoreLib.Networking;
using CUCoreLib.Util;
using Mono.Cecil;
using UnityEngine;

namespace CUCoreLib.ContentReload;

public static class ContentReloadManager
{
    private const string AutoHotReloadPathKeyPrefix = "CUCoreLib.AutoHotReload.Path.";
    private const string AutoHotReloadEnabledKeyPrefix = "CUCoreLib.AutoHotReload.Enabled.";

    private static readonly Dictionary<string, ContentReloadState> StateByModGuid =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool initialized;
    private static readonly ContentReloadConfig config = new();

    internal static void Initialize()
    {
        if (initialized) return;

        initialized = true;
        RestorePersistedAutoHotReloadSettings();
        ContentWatchService.Initialize();
    }

    public static ContentReloadResult Reload(string modGuid)
    {
        Initialize();

        var result = new ContentReloadResult
        {
            ModGuid = (modGuid ?? string.Empty).Trim()
        };

        if (string.IsNullOrWhiteSpace(modGuid))
        {
            result.AddError("Mod GUID was empty.");
            return result;
        }

        if (IsMultiplayerActive())
        {
            result.AddError("Strict content DLL reload is singleplayer-only.");
            return result;
        }

        var state = GetOrCreateState(modGuid);
        var candidate = ContentAssemblyResolver.ResolveCandidate(modGuid, config, state);
        var report = ContentCompatibilityScanner.Scan(candidate);
        state.LastReport = report;

        result = ContentReplayExecutor.Execute(report);
        state.LastResult = result;

        if (!result.Succeeded) return result;
        state.LastSuccessfulHash = result.SourceHash;
        state.LastSuccessfulSourcePath = result.SourcePath;
        state.PendingHash = null;
        state.PendingSourcePath = null;
        state.PendingSinceUtc = DateTime.MinValue;

        return result;
    }

    public static string[] GetLoadedModGuids()
    {
        return Chainloader.PluginInfos.Keys
            .Where(guid => !string.Equals(guid, CUCoreLibPlugin.GUID, StringComparison.OrdinalIgnoreCase))
            .OrderBy(guid => guid, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static int GetPollIntervalSeconds()
    {
        Initialize();
        return config is { PollIntervalSeconds: > 0 } ? config.PollIntervalSeconds : 2;
    }

    public static bool ConfigureAutoHotRefresh(string dllPath, bool enabled, out string message)
    {
        Initialize();

        var normalizedPath = NormalizeExistingOrTargetPath(dllPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            message = "DLL path was invalid.";
            return false;
        }

        if (!File.Exists(normalizedPath))
        {
            message = "DLL path does not exist: " + normalizedPath;
            return false;
        }

        if (!TryResolvePluginGuidFromDll(normalizedPath, out var modGuid, out var modName, out var reason))
        {
            message = reason;
            return false;
        }

        config.Mods ??= new Dictionary<string, ContentReloadModConfig>(StringComparer.OrdinalIgnoreCase);

        if (!config.Mods.TryGetValue(modGuid, out var modConfig) || modConfig == null)
        {
            modConfig = new ContentReloadModConfig();
            config.Mods[modGuid] = modConfig;
        }

        modConfig.OverrideDllPath = normalizedPath;
        modConfig.WatchEnabled = enabled;
        PersistAutoHotReloadSetting(modGuid, normalizedPath, enabled);

        var label = string.IsNullOrWhiteSpace(modName) ? modGuid : modName + " (" + modGuid + ")";
        message = (enabled ? "Enabled" : "Disabled") + " automatic hot reload for " + label + " using " +
                  normalizedPath + ".";
        return true;
    }

    internal static void PollWatchers()
    {
        if (!initialized || IsMultiplayerActive()) return;

        var now = DateTime.UtcNow;
        foreach (var modGuid in GetLoadedModGuids())
        {
            if (!ContentAssemblyResolver.IsWatchEnabled(config, modGuid)) continue;

            var state = GetOrCreateState(modGuid);
            var candidate = ContentAssemblyResolver.ResolveCandidate(modGuid, config, state);
            if (string.IsNullOrWhiteSpace(candidate.SelectedPath) ||
                string.IsNullOrWhiteSpace(candidate.SelectedHash)) continue;

            if (string.Equals(candidate.SelectedHash, state.LastSuccessfulHash, StringComparison.OrdinalIgnoreCase))
            {
                state.PendingHash = null;
                state.PendingSourcePath = null;
                state.PendingSinceUtc = DateTime.MinValue;
                continue;
            }

            if (!string.Equals(state.PendingHash, candidate.SelectedHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(state.PendingSourcePath, candidate.SelectedPath, StringComparison.OrdinalIgnoreCase))
            {
                state.PendingHash = candidate.SelectedHash;
                state.PendingSourcePath = candidate.SelectedPath;
                state.PendingSinceUtc = now;
                continue;
            }

            var debounceMilliseconds = config is { DebounceMilliseconds: > 0 }
                ? config.DebounceMilliseconds
                : 1200;
            if ((now - state.PendingSinceUtc).TotalMilliseconds < debounceMilliseconds) continue;

            var result = Reload(modGuid);
            if (!result.Succeeded) state.PendingSinceUtc = now;
        }
    }

    public static void WriteReloadSummaryToConsole(ConsoleScript console, ContentReloadResult result)
    {
        if (result is { Succeeded: true })
        {
            var reloadLabel = GetReloadedFileName(result.SourcePath);
            if (console != null) ConsoleUtils.LogToConsole("Reloaded " + reloadLabel + "!");

            return;
        }

        WriteMessages(console, result != null ? result.Errors.ToArray() : []);
        if (result != null && !string.IsNullOrWhiteSpace(result.UnsupportedReason))
            WriteMessages(console, [result.UnsupportedReason]);
    }

    private static ContentReloadState GetOrCreateState(string modGuid)
    {
        var normalizedModGuid = (modGuid ?? string.Empty).Trim();
        if (StateByModGuid.TryGetValue(normalizedModGuid, out var state)) return state;
        state = new ContentReloadState();
        StateByModGuid[normalizedModGuid] = state;

        return state;
    }

    private static void RestorePersistedAutoHotReloadSettings()
    {
        foreach (var modGuid in GetLoadedModGuids())
        {
            var normalizedModGuid = (modGuid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedModGuid)) continue;

            var persistedPath = PlayerPrefsUtils.GetString(GetAutoHotReloadPathKey(normalizedModGuid), string.Empty);
            var watchEnabled = PlayerPrefsUtils.GetBool(GetAutoHotReloadEnabledKey(normalizedModGuid));
            if (string.IsNullOrWhiteSpace(persistedPath) && !watchEnabled) continue;

            config.Mods ??= new Dictionary<string, ContentReloadModConfig>(StringComparer.OrdinalIgnoreCase);

            if (!config.Mods.TryGetValue(normalizedModGuid, out var modConfig) || modConfig == null)
            {
                modConfig = new ContentReloadModConfig();
                config.Mods[normalizedModGuid] = modConfig;
            }

            modConfig.OverrideDllPath = NormalizeExistingOrTargetPath(persistedPath);
            modConfig.WatchEnabled = watchEnabled;
        }
    }

    private static void PersistAutoHotReloadSetting(string modGuid, string dllPath, bool enabled)
    {
        var normalizedModGuid = (modGuid ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedModGuid)) return;

        PlayerPrefsUtils.SetString(GetAutoHotReloadPathKey(normalizedModGuid), dllPath ?? string.Empty);
        PlayerPrefsUtils.SetBool(GetAutoHotReloadEnabledKey(normalizedModGuid), enabled);
        PlayerPrefs.Save();
    }

    private static string GetAutoHotReloadPathKey(string modGuid)
    {
        return AutoHotReloadPathKeyPrefix + modGuid;
    }

    private static string GetAutoHotReloadEnabledKey(string modGuid)
    {
        return AutoHotReloadEnabledKeyPrefix + modGuid;
    }

    private static bool IsMultiplayerActive()
    {
        return MultiplayerBridge.IsAvailable && MultiplayerBridge.IsRunning;
    }

    private static string NormalizeExistingOrTargetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            return Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch
        {
            return null;
        }
    }

    private static bool TryResolvePluginGuidFromDll(string dllPath, out string modGuid, out string modName,
        out string reason)
    {
        modGuid = null;
        modName = null;
        reason = null;

        try
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(dllPath))
            {
                foreach (var type in EnumerateTypes(assembly.MainModule.Types))
                {
                    if (type == null || !type.HasCustomAttributes) continue;

                    foreach (var attribute in type.CustomAttributes.Where(attribute =>
                                 string.Equals(attribute.AttributeType.FullName, "BepInEx.BepInPlugin",
                                     StringComparison.Ordinal)))
                    {
                        if (attribute.ConstructorArguments.Count > 0)
                            modGuid = attribute.ConstructorArguments[0].Value as string;

                        if (attribute.ConstructorArguments.Count > 1)
                            modName = attribute.ConstructorArguments[1].Value as string;

                        if (!string.IsNullOrWhiteSpace(modGuid)) return true;
                    }
                }
            }

            reason = "No [BepInPlugin] GUID was found in " + dllPath + ".";
            return false;
        }
        catch (Exception ex)
        {
            reason = "Failed to inspect DLL '" + dllPath + "': " + ex.Message;
            return false;
        }
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

    // not use BuildResultHeadline
    private static string BuildResultHeadline(ContentReloadResult result)
    {
        if (result == null) return "Strict content reload result was null.";

        var label = string.IsNullOrWhiteSpace(result.ModName)
            ? result.ModGuid
            : result.ModName + " (" + result.ModGuid + ")";
        var suffix = string.IsNullOrWhiteSpace(result.SourceHash)
            ? string.Empty
            : " hash=" + result.SourceHash + ".";
        return "Strict content reload for " + label + ": " +
               (result.Succeeded ? "success" : "failed") +
               ", " + result.Info.Count + " info, " +
               result.Skipped.Count + " skipped, " +
               result.Errors.Count + " errors." + suffix;
    }

    private static string GetReloadedFileName(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return "content DLL";

        try
        {
            var fileName = Path.GetFileName(sourcePath);
            return string.IsNullOrWhiteSpace(fileName) ? "content DLL" : fileName;
        }
        catch
        {
            return "content DLL";
        }
    }

    private static void WriteMessages(ConsoleScript console, IEnumerable<string> messages)
    {
        if (messages == null) return;

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message)) continue;

            // CUCoreLibPlugin.Log?.LogInfo(message);
            if (console != null) ConsoleUtils.LogToConsole(message);
        }
    }
}