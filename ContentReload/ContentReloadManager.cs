using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using UnityEngine;

namespace CUCoreLib.ContentReload
{
    public static class ContentReloadManager
    {
        private const string AutoHotReloadEnabledKeyPrefix = "CUCoreLib.AutoHotReload.Enabled.";
        private const string ConfigSectionName = "Hot Reload";
        private static readonly Dictionary<string, ContentReloadState> StateByModGuid =
            new Dictionary<string, ContentReloadState>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> EnabledModGuids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, BepInEx.Configuration.ConfigEntry<string>> OverridePathEntriesByModGuid =
            new Dictionary<string, BepInEx.Configuration.ConfigEntry<string>>(StringComparer.OrdinalIgnoreCase);

        private static bool initialized;
        private static readonly ContentReloadConfig config = new ContentReloadConfig();
        private static BepInEx.Configuration.ConfigFile configFile;

        internal static void Initialize()
        {
            if (initialized) return;

            initialized = true;
            configFile = CUCoreLibPlugin.GetOrCreateSharedConfig();
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

            if (!IsEnabled(modGuid))
            {
                result.AddError("Hot reload is not enabled for '" + modGuid.Trim() +
                                "'. Call ContentReloadManager.EnableHotReload(GUID) from Awake() first.");
                return result;
            }

            EnsureModConfigBound(modGuid);

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

        public static void EnableHotReload(string modGuid)
        {
            EnableHotReload(modGuid, null);
        }

        public static void EnableHotReload(string modGuid, HotReloadOptions options)
        {
            Initialize();

            var normalizedModGuid = (modGuid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedModGuid))
                throw new ArgumentException("Mod GUID was empty.", nameof(modGuid));

            if (!TryFindCallingPluginType(normalizedModGuid, out var pluginType, out var reason))
                throw new InvalidOperationException(reason);

            EnabledModGuids.Add(normalizedModGuid);
            EnsureModConfigBound(normalizedModGuid);
            var normalizedOptions = options ?? new HotReloadOptions();
            GetOrCreateState(normalizedModGuid).Mode = normalizedOptions.Mode;
            PersistAutoHotReloadSetting(normalizedModGuid, true);
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
            return config != null && config.PollIntervalSeconds > 0 ? config.PollIntervalSeconds : 2;
        }

        public static bool ConfigureAutoHotRefresh(string modGuid, bool enabled, out string message)
        {
            Initialize();

            var normalizedModGuid = (modGuid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedModGuid))
            {
                message = "Mod GUID was invalid.";
                return false;
            }

            if (!Chainloader.PluginInfos.TryGetValue(normalizedModGuid, out var pluginInfo) || pluginInfo == null)
            {
                message = "Loaded plugin GUID was not found: " + normalizedModGuid;
                return false;
            }

            if (!EnabledModGuids.Contains(normalizedModGuid))
            {
                message = "Hot reload is not enabled for '" + normalizedModGuid +
                          "'. Call ContentReloadManager.EnableHotReload(GUID) from Awake() first.";
                return false;
            }

            EnsureModConfigBound(normalizedModGuid);

            if (config.Mods == null)
                config.Mods = new Dictionary<string, ContentReloadModConfig>(StringComparer.OrdinalIgnoreCase);

            if (!config.Mods.TryGetValue(normalizedModGuid, out var modConfig) || modConfig == null)
            {
                modConfig = new ContentReloadModConfig();
                config.Mods[normalizedModGuid] = modConfig;
            }

            modConfig.WatchEnabled = enabled;
            PersistAutoHotReloadSetting(normalizedModGuid, enabled);

            var modName = pluginInfo.Metadata != null && !string.IsNullOrWhiteSpace(pluginInfo.Metadata.Name)
                ? pluginInfo.Metadata.Name
                : normalizedModGuid;
            var label = string.IsNullOrWhiteSpace(modName) ? modGuid : modName + " (" + modGuid + ")";
            message = (enabled ? "Enabled" : "Disabled") + " automatic hot reload for " + label + ".";
            return true;
        }

        internal static void PollWatchers()
        {
            if (!initialized || IsMultiplayerActive()) return;

            var now = DateTime.UtcNow;
            foreach (var modGuid in GetLoadedModGuids())
            {
                if (!ContentAssemblyResolver.IsWatchEnabled(config, modGuid)) continue;

                EnsureModConfigBound(modGuid);
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

                var debounceMilliseconds = config != null && config.DebounceMilliseconds > 0
                    ? config.DebounceMilliseconds
                    : 1200;
                if ((now - state.PendingSinceUtc).TotalMilliseconds < debounceMilliseconds) continue;

                var result = Reload(modGuid);
                if (!result.Succeeded) state.PendingSinceUtc = now;
            }
        }

        public static void WriteReloadSummaryToConsole(ConsoleScript console, ContentReloadResult result)
        {
            if (result != null && result.Succeeded)
            {
                var reloadLabel = GetReloadedFileName(result.SourcePath);
                WriteMessages(console, new[] { "Reloaded " + reloadLabel + "!" });
                WriteMessages(console, result.Skipped);

                return;
            }

            WriteMessages(console, result != null ? result.Errors.ToArray() : Array.Empty<string>());
            WriteMessages(console, result != null ? result.Skipped.ToArray() : Array.Empty<string>());
            if (result != null && !string.IsNullOrWhiteSpace(result.UnsupportedReason))
                WriteMessages(console, new[] { result.UnsupportedReason });
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

                var watchEnabled = CUCoreUtils.GetBool(GetAutoHotReloadEnabledKey(normalizedModGuid));
                if (!watchEnabled) continue;

                if (config.Mods == null)
                    config.Mods = new Dictionary<string, ContentReloadModConfig>(StringComparer.OrdinalIgnoreCase);

                if (!config.Mods.TryGetValue(normalizedModGuid, out var modConfig) || modConfig == null)
                {
                    modConfig = new ContentReloadModConfig();
                    config.Mods[normalizedModGuid] = modConfig;
                }

                modConfig.WatchEnabled = watchEnabled;
                modConfig.OverridePath = GetConfiguredOverridePath(normalizedModGuid);
            }
        }

        private static void PersistAutoHotReloadSetting(string modGuid, bool enabled)
        {
            var normalizedModGuid = (modGuid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedModGuid)) return;

            CUCoreUtils.SetBool(GetAutoHotReloadEnabledKey(normalizedModGuid), enabled);
            PlayerPrefs.Save();
        }

        private static string GetAutoHotReloadEnabledKey(string modGuid)
        {
            return AutoHotReloadEnabledKeyPrefix + modGuid;
        }

        private static void EnsureModConfigBound(string modGuid)
        {
            var normalizedModGuid = (modGuid ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedModGuid) || configFile == null) return;

            if (!config.Mods.TryGetValue(normalizedModGuid, out var modConfig) || modConfig == null)
            {
                modConfig = new ContentReloadModConfig();
                config.Mods[normalizedModGuid] = modConfig;
            }

            if (!OverridePathEntriesByModGuid.TryGetValue(normalizedModGuid, out var overrideEntry) || overrideEntry == null)
            {
                overrideEntry = CUCoreLibPlugin.BindSharedConfig(
                    ConfigSectionName,
                    normalizedModGuid + ".overridePath",
                    string.Empty,
                    "Optional absolute DLL path for hot reloading mod '" + normalizedModGuid + "'. " +
                    "When set to an existing file, CUCoreLib reloads from that path instead of the deployed plugin DLL.");
                OverridePathEntriesByModGuid[normalizedModGuid] = overrideEntry;
            }

            modConfig.OverridePath = GetConfiguredOverridePath(normalizedModGuid);
        }

        private static string GetConfiguredOverridePath(string modGuid)
        {
            if (string.IsNullOrWhiteSpace(modGuid)) return null;
            if (!OverridePathEntriesByModGuid.TryGetValue(modGuid.Trim(), out var entry) || entry == null) return null;

            var value = entry.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static bool IsMultiplayerActive()
        {
            return MultiplayerBridge.IsAvailable && MultiplayerBridge.IsRunning;
        }

        internal static bool IsEnabled(string modGuid)
        {
            return !string.IsNullOrWhiteSpace(modGuid) && EnabledModGuids.Contains(modGuid.Trim());
        }

        internal static HotReloadMode GetReloadMode(string modGuid)
        {
            if (string.IsNullOrWhiteSpace(modGuid)) return HotReloadMode.FlexibleGuarded;

            return GetOrCreateState(modGuid).Mode;
        }

        private static bool TryFindCallingPluginType(string modGuid, out Type pluginType, out string reason)
        {
            pluginType = null;
            reason = "ContentReloadManager.EnableHotReload() must be called from the owning plugin Awake().";

            var frames = new System.Diagnostics.StackTrace().GetFrames();
            if (frames == null || frames.Length == 0) return false;

            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                var declaringType = method?.DeclaringType;
                if (declaringType == null) continue;

                if (!typeof(BaseUnityPlugin).IsAssignableFrom(declaringType)) continue;
                if (!string.Equals(method.Name, "Awake", StringComparison.Ordinal)) continue;

                var candidateGuid = TryGetPluginGuidFromType(declaringType);
                if (!string.Equals(candidateGuid, modGuid, StringComparison.Ordinal))
                {
                    reason = "EnableHotReload('" + modGuid + "') was called from '" +
                             (candidateGuid ?? declaringType.FullName) +
                             "'. The GUID must match the owning plugin's [BepInPlugin] GUID.";
                    return false;
                }

                pluginType = declaringType;
                return true;
            }

            return false;
        }

        private static string TryGetPluginGuidFromType(Type pluginType)
        {
            if (pluginType == null) return null;

            var attribute = pluginType.GetCustomAttributes(typeof(BepInPlugin), true)
                .OfType<BepInPlugin>()
                .FirstOrDefault();
            return attribute?.GUID;
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

                CUCoreLibPlugin.Log?.LogInfo(message);
                if (console != null) CUCoreUtils.ConsoleLog(console, message);
            }
        }
    }
}
