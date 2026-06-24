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
        internal static ContentReloadCandidate ResolveCandidate(string modGuid, ContentReloadConfig config, ContentReloadState state)
        {
            string normalizedModGuid = (modGuid ?? string.Empty).Trim();
            ContentReloadCandidate candidate = new ContentReloadCandidate
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

            ContentReloadModConfig modConfig = GetModConfig(config, normalizedModGuid);
            candidate.OverridePath = NormalizeExistingPath(ResolveConfiguredPath(modConfig != null ? modConfig.OverrideDllPath : null));

            string loadedHash = GetFileHash(candidate.LoadedPluginPath, state);
            string overrideHash = GetFileHash(candidate.OverridePath, state);

            bool loadedChanged = !string.IsNullOrWhiteSpace(loadedHash) &&
                !string.Equals(loadedHash, state != null ? state.LastSuccessfulHash : null, StringComparison.OrdinalIgnoreCase);
            bool overrideChanged = !string.IsNullOrWhiteSpace(overrideHash) &&
                !string.Equals(overrideHash, state != null ? state.LastSuccessfulHash : null, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(candidate.LoadedPluginPath) && (loadedChanged || string.IsNullOrWhiteSpace(candidate.OverridePath)))
            {
                candidate.SelectedPath = candidate.LoadedPluginPath;
                candidate.SelectedHash = loadedHash;
                candidate.SelectedSourceLabel = "loaded plugin";
                return candidate;
            }

            if (!string.IsNullOrWhiteSpace(candidate.OverridePath) && overrideChanged)
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

            if (!string.IsNullOrWhiteSpace(candidate.OverridePath))
            {
                candidate.SelectedPath = candidate.OverridePath;
                candidate.SelectedHash = overrideHash;
                candidate.SelectedSourceLabel = "override path";
            }

            return candidate;
        }

        internal static string GetFileHash(string path, ContentReloadState state)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            if (state == null)
            {
                return ComputeHash(path);
            }

            if (!state.ObservedFiles.TryGetValue(path, out ContentObservedFileState observed))
            {
                observed = new ContentObservedFileState();
                state.ObservedFiles[path] = observed;
            }

            FileInfo info = new FileInfo(path);
            long length = info.Length;
            long writeTicks = info.LastWriteTimeUtc.Ticks;
            if (observed.Length == length &&
                observed.LastWriteUtcTicks == writeTicks &&
                !string.IsNullOrWhiteSpace(observed.Hash))
            {
                return observed.Hash;
            }

            observed.Length = length;
            observed.LastWriteUtcTicks = writeTicks;
            observed.Hash = ComputeHash(path);
            return observed.Hash;
        }

        internal static bool IsWatchEnabled(ContentReloadConfig config, string modGuid)
        {
            ContentReloadModConfig modConfig = GetModConfig(config, modGuid);
            return modConfig != null && modConfig.WatchEnabled;
        }

        internal static string[] GetCandidatePaths(ContentReloadCandidate candidate)
        {
            return new[] { candidate.LoadedPluginPath, candidate.OverridePath }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static ContentReloadModConfig GetModConfig(ContentReloadConfig config, string modGuid)
        {
            if (config == null || config.Mods == null || string.IsNullOrWhiteSpace(modGuid))
            {
                return null;
            }

            config.Mods.TryGetValue(modGuid.Trim(), out ContentReloadModConfig modConfig);
            return modConfig;
        }

        private static string ResolveConfiguredPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            string baseDirectory = ContentReloadManager.ConfigDirectoryPath;
            return string.IsNullOrWhiteSpace(baseDirectory) ? path : Path.Combine(baseDirectory, path);
        }

        private static string NormalizeExistingPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                return File.Exists(fullPath) ? fullPath : null;
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
