using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Bootstrap;

namespace CUCoreLib.ContentReload
{
    internal static class ContentAssemblyResolver
    {
        internal static ContentReloadCandidate ResolveCandidate(string modGuid, ContentReloadConfig config,
            ContentReloadState state)
        {
            var normalizedModGuid = (modGuid ?? string.Empty).Trim();
            var candidate = new ContentReloadCandidate
            {
                ModGuid = normalizedModGuid,
                ModName = normalizedModGuid
            };

            if (Chainloader.PluginInfos.TryGetValue(normalizedModGuid, out var pluginInfo) && pluginInfo != null)
            {
                candidate.ModName = pluginInfo.Metadata != null && !string.IsNullOrWhiteSpace(pluginInfo.Metadata.Name)
                    ? pluginInfo.Metadata.Name
                    : normalizedModGuid;
                candidate.LoadedPluginPath = NormalizeExistingPath(pluginInfo.Location);
            }

            var modConfig = GetModConfig(config, normalizedModGuid);
            candidate.OverridePath = NormalizeExistingPath(modConfig?.OverridePath);
            var overrideHash = GetFileHash(candidate.OverridePath, state);
            var overrideChanged = !string.IsNullOrWhiteSpace(overrideHash) &&
                                  !string.Equals(overrideHash, state?.LastSuccessfulHash,
                                      StringComparison.OrdinalIgnoreCase);
            var loadedHash = GetFileHash(candidate.LoadedPluginPath, state);
            var loadedChanged = !string.IsNullOrWhiteSpace(loadedHash) &&
                                !string.Equals(loadedHash, state?.LastSuccessfulHash,
                                    StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(candidate.OverridePath) &&
                overrideChanged)
            {
                candidate.SelectedPath = candidate.OverridePath;
                candidate.SelectedHash = overrideHash;
                candidate.SelectedSourceLabel = "override path";
                return candidate;
            }

            if (!string.IsNullOrWhiteSpace(candidate.LoadedPluginPath) &&
                loadedChanged)
            {
                candidate.SelectedPath = candidate.LoadedPluginPath;
                candidate.SelectedHash = loadedHash;
                candidate.SelectedSourceLabel = "loaded plugin";
                return candidate;
            }

            if (!string.IsNullOrWhiteSpace(candidate.OverridePath))
            {
                candidate.SelectedPath = candidate.OverridePath;
                candidate.SelectedHash = overrideHash;
                candidate.SelectedSourceLabel = "override path";
                return candidate;
            }

            if (!string.IsNullOrWhiteSpace(candidate.LoadedPluginPath))
            {
                candidate.SelectedPath = candidate.LoadedPluginPath;
                candidate.SelectedHash = loadedHash;
                candidate.SelectedSourceLabel = "loaded plugin";
                return candidate;
            }

            return candidate;
        }

        internal static string GetFileHash(string path, ContentReloadState state)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            if (state == null) return ComputeHash(path);

            if (!state.ObservedFiles.TryGetValue(path, out var observed))
            {
                observed = new ContentObservedFileState();
                state.ObservedFiles[path] = observed;
            }

            var info = new FileInfo(path);
            var length = info.Length;
            var writeTicks = info.LastWriteTimeUtc.Ticks;
            if (observed.Length == length &&
                observed.LastWriteUtcTicks == writeTicks &&
                !string.IsNullOrWhiteSpace(observed.Hash))
                return observed.Hash;

            observed.Length = length;
            observed.LastWriteUtcTicks = writeTicks;
            observed.Hash = ComputeHash(path);
            return observed.Hash;
        }

        internal static bool IsWatchEnabled(ContentReloadConfig config, string modGuid)
        {
            var modConfig = GetModConfig(config, modGuid);
            return modConfig != null && modConfig.WatchEnabled;
        }

        internal static string[] GetCandidatePaths(ContentReloadCandidate candidate)
        {
            return new[] { candidate.OverridePath, candidate.LoadedPluginPath }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static ContentReloadModConfig GetModConfig(ContentReloadConfig config, string modGuid)
        {
            if (config?.Mods == null || string.IsNullOrWhiteSpace(modGuid)) return null;

            config.Mods.TryGetValue(modGuid.Trim(), out var modConfig);
            return modConfig;
        }

        private static string NormalizeExistingPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            try
            {
                var fullPath = Path.GetFullPath(path);
                return File.Exists(fullPath) ? fullPath : null;
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeHash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var t in hash)
                    builder.Append(t.ToString("x2"));

                return builder.ToString();
            }
        }
    }
}
