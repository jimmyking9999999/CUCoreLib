using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using BepInEx;
using BepInEx.Bootstrap;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;

namespace CUCoreLib.ContentReload
{
    public static class ContentReloadManager
    {
        private const string ConfigFileName = "ContentReload.json";

        private static readonly Dictionary<string, ContentReloadState> StateByModGuid =
            new Dictionary<string, ContentReloadState>(StringComparer.OrdinalIgnoreCase);

        private static bool initialized;
        private static ContentReloadConfig config;

        internal static string ConfigDirectoryPath { get; private set; }

        internal static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            ConfigDirectoryPath = Path.Combine(Paths.ConfigPath, "CUCoreLib");
            config = LoadConfig();
            ContentWatchService.Initialize();
        }

        public static ContentReloadResult Reload(string modGuid)
        {
            Initialize();

            ContentReloadResult result = new ContentReloadResult
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

            ContentReloadState state = GetOrCreateState(modGuid);
            ContentReloadCandidate candidate = ContentAssemblyResolver.ResolveCandidate(modGuid, config, state);
            ContentCompatibilityReport report = ContentCompatibilityScanner.Scan(candidate);
            state.LastReport = report;

            result = ContentReplayExecutor.Execute(report);
            state.LastResult = result;

            if (result.Succeeded)
            {
                state.LastSuccessfulHash = result.SourceHash;
                state.LastSuccessfulSourcePath = result.SourcePath;
                state.PendingHash = null;
                state.PendingSourcePath = null;
                state.PendingSinceUtc = DateTime.MinValue;
            }

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
            return config != null && config.PollIntervalSeconds > 0 ? config.PollIntervalSeconds : 2;
        }

        internal static void PollWatchers()
        {
            if (!initialized || IsMultiplayerActive())
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            foreach (string modGuid in GetLoadedModGuids())
            {
                if (!ContentAssemblyResolver.IsWatchEnabled(config, modGuid))
                {
                    continue;
                }

                ContentReloadState state = GetOrCreateState(modGuid);
                ContentReloadCandidate candidate = ContentAssemblyResolver.ResolveCandidate(modGuid, config, state);
                if (string.IsNullOrWhiteSpace(candidate.SelectedPath) || string.IsNullOrWhiteSpace(candidate.SelectedHash))
                {
                    continue;
                }

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

                int debounceMilliseconds = config != null && config.DebounceMilliseconds > 0 ? config.DebounceMilliseconds : 1200;
                if ((now - state.PendingSinceUtc).TotalMilliseconds < debounceMilliseconds)
                {
                    continue;
                }

                ContentReloadResult result = Reload(modGuid);
                if (!result.Succeeded)
                {
                    state.PendingSinceUtc = now;
                }
            }
        }

        public static void WriteReloadSummaryToConsole(ConsoleScript console, ContentReloadResult result)
        {
            string headline = BuildResultHeadline(result);
            if (result != null && !result.Succeeded)
            {
                CUCoreLibPlugin.Log?.LogWarning(headline);
            }
            else
            {
                CUCoreLibPlugin.Log?.LogInfo(headline);
            }
            if (console != null)
            {
                CUCoreUtils.ConsoleLog(console, headline);
            }

            WriteMessages(console, result != null ? result.RecognizedMethods.Select(method => "Recognized method: " + method).ToArray() : Array.Empty<string>());
            WriteMessages(console, result != null ? result.Info.ToArray() : Array.Empty<string>());
            WriteMessages(console, result != null ? result.Skipped.ToArray() : Array.Empty<string>());
            WriteMessages(console, result != null ? result.Errors.ToArray() : Array.Empty<string>());
            if (result != null && !string.IsNullOrWhiteSpace(result.UnsupportedReason))
            {
                WriteMessages(console, new[] { result.UnsupportedReason });
            }
        }

        private static ContentReloadState GetOrCreateState(string modGuid)
        {
            string normalizedModGuid = (modGuid ?? string.Empty).Trim();
            if (!StateByModGuid.TryGetValue(normalizedModGuid, out ContentReloadState state))
            {
                state = new ContentReloadState();
                StateByModGuid[normalizedModGuid] = state;
            }

            return state;
        }

        private static ContentReloadConfig LoadConfig()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectoryPath);
                string configPath = Path.Combine(ConfigDirectoryPath, ConfigFileName);
                if (!File.Exists(configPath))
                {
                    ContentReloadConfig created = new ContentReloadConfig();
                    File.WriteAllText(configPath, JsonConvert.SerializeObject(created, Formatting.Indented));
                    return created;
                }

                ContentReloadConfig loaded = JsonConvert.DeserializeObject<ContentReloadConfig>(File.ReadAllText(configPath));
                if (loaded == null)
                {
                    return new ContentReloadConfig();
                }

                if (loaded.Mods == null)
                {
                    loaded.Mods = new Dictionary<string, ContentReloadModConfig>(StringComparer.OrdinalIgnoreCase);
                }

                if (loaded.PollIntervalSeconds <= 0)
                {
                    loaded.PollIntervalSeconds = 2;
                }

                if (loaded.DebounceMilliseconds <= 0)
                {
                    loaded.DebounceMilliseconds = 1200;
                }

                return loaded;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("Failed to load strict content reload config.\n" + ex);
                return new ContentReloadConfig();
            }
        }

        private static bool IsMultiplayerActive()
        {
            return MultiplayerBridge.IsAvailable && MultiplayerBridge.IsRunning;
        }

        private static string BuildResultHeadline(ContentReloadResult result)
        {
            if (result == null)
            {
                return "Strict content reload result was null.";
            }

            string label = string.IsNullOrWhiteSpace(result.ModName) ? result.ModGuid : result.ModName + " (" + result.ModGuid + ")";
            string suffix = string.IsNullOrWhiteSpace(result.SourceHash)
                ? string.Empty
                : " hash=" + result.SourceHash + ".";
            return "Strict content reload for " + label + ": " +
                (result.Succeeded ? "success" : "failed") +
                ", " + result.Info.Count + " info, " +
                result.Skipped.Count + " skipped, " +
                result.Errors.Count + " errors." + suffix;
        }

        private static void WriteMessages(ConsoleScript console, IEnumerable<string> messages)
        {
            if (messages == null)
            {
                return;
            }

            foreach (string message in messages)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                CUCoreLibPlugin.Log?.LogInfo(message);
                if (console != null)
                {
                    CUCoreUtils.ConsoleLog(console, message);
                }
            }
        }
    }
}
