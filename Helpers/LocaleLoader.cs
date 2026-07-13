using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Helpers
{
    internal static class LocaleLoader
    {
        private static ManualLogSource Logger;

        public static void Initialize(ManualLogSource logger)
        {
            Logger = logger;
        }

        public static void ApplyActiveLocaleOverlay()
        {
            if (Locale.currentLang == null) return;

            var localeName = Locale.currentLangName;
            if (string.IsNullOrWhiteSpace(localeName)) return;

            var normalizedLocaleName = localeName.Trim();

            var isEnglish = string.Equals(normalizedLocaleName, "EN", StringComparison.OrdinalIgnoreCase);
            if (!isEnglish)
                ApplyLocaleFile("EN");

            ApplyLocaleFile(normalizedLocaleName);
        }

        private static void ApplyLocaleFile(string localeName)
        {
            var embeddedResources = FindEmbeddedOverlayResources(localeName);
            foreach (var resource in embeddedResources)
                try
                {
                    var localeJson = LoadEmbeddedLocaleJson(resource);
                    if (localeJson == null) continue;

                    MergeLocaleJson(Locale.currentLang, localeJson);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(
                        $"Failed to load embedded locale overlay '{resource.DisplayName}' while applying locale '{localeName}': {ex.Message}");
                }

            var overlayFiles = FindOverlayFiles(localeName);
            if (overlayFiles.Count == 0) return;

            foreach (var path in overlayFiles)
                try
                {
                    var localeJson = JObject.Parse(File.ReadAllText(path));
                    MergeLocaleJson(Locale.currentLang, localeJson);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(
                        $"Failed to load locale overlay '{path}' while applying locale '{localeName}'. Owning plugin: {ResolveOverlayOwner(path)}. {ex.Message}");
                }
        }

        public static string GetLocalizedText(string category, string key, string fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback;

            var normalizedKey = key.Trim();
            var normalizedCategory = (category ?? string.Empty).Trim().ToLowerInvariant();

            var value = TryReadValue(Locale.currentLang, normalizedCategory, normalizedKey);
            if (!string.IsNullOrWhiteSpace(value)) return value;

            return !string.IsNullOrWhiteSpace(fallback)
                ? fallback
                : normalizedKey;
        }

        private static List<string> FindOverlayFiles(string localeName)
        {
            var results = new List<string>();
            var fileName = localeName + ".json";

            var configPath = Path.Combine(Paths.ConfigPath, "CUCoreLib", "Locales", fileName);
            if (File.Exists(configPath)) results.Add(configPath);

            results.AddRange(FindPluginOverlayFiles(fileName));

            return results
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> FindPluginOverlayFiles(string fileName)
        {
            var results = new List<string>();
            var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pluginInfo in Chainloader.PluginInfos.Values
                         .Where(info => info != null)
                         .OrderBy(GetPluginSortKey, StringComparer.OrdinalIgnoreCase))
            {
                var pluginLocation = NormalizeExistingPath(pluginInfo.Location);
                if (string.IsNullOrWhiteSpace(pluginLocation)) continue;

                var pluginDirectory = Path.GetDirectoryName(pluginLocation);
                if (string.IsNullOrWhiteSpace(pluginDirectory)) continue;

                AddIfExists(results, visitedPaths, Path.Combine(pluginDirectory, fileName));
                AddIfExists(results, visitedPaths, Path.Combine(pluginDirectory, "Locales", fileName));
                AddRangeIfExists(results, visitedPaths, EnumerateMatchingFiles(pluginDirectory, fileName));
            }

            var pluginRoot = Path.Combine(Path.GetDirectoryName(Paths.ConfigPath) ?? string.Empty, "plugins");
            if (!Directory.Exists(pluginRoot)) return results;

            AddRangeIfExists(results, visitedPaths, EnumerateMatchingFiles(pluginRoot, fileName));

            return results;
        }

        private static List<EmbeddedLocaleResource> FindEmbeddedOverlayResources(string localeName)
        {
            var fileName = localeName + ".json";
            var normalizedFileName = NormalizeResourceName(fileName);
            var results = new List<EmbeddedLocaleResource>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pluginInfo in Chainloader.PluginInfos.Values
                         .Where(info => info != null)
                         .OrderBy(GetPluginSortKey, StringComparer.OrdinalIgnoreCase))
            {
                var assembly = ResolvePluginAssembly(pluginInfo);
                if (assembly == null) continue;

                foreach (var resourceName in assembly.GetManifestResourceNames()
                             .Where(name => ResourceNameMatchesLocale(name, normalizedFileName))
                             .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    var uniqueKey = (assembly.FullName ?? assembly.GetName().Name ?? string.Empty) + "|" + resourceName;
                    if (!visited.Add(uniqueKey)) continue;

                    results.Add(new EmbeddedLocaleResource(assembly, resourceName));
                }
            }

            return results;
        }

        private static JObject LoadEmbeddedLocaleJson(EmbeddedLocaleResource resource)
        {
            if (resource == null || resource.Assembly == null || string.IsNullOrWhiteSpace(resource.ResourceName))
                return null;

            var json = AssetLoader.LoadEmbeddedText(resource.ResourceName, resource.Assembly);
            return string.IsNullOrWhiteSpace(json) ? null : JObject.Parse(json);
        }

        private static void MergeLocaleJson(Language target, JObject source)
        {
            if (target == null || source == null) return;

            MergeSection(target.main, source["item"]);
            MergeSection(target.main, source["main"]);

            MergeSection(target.buildings, source["building"]);
            MergeSection(target.buildings, source["buildings"]);

            MergeSection(target.moodles, source["moodle"]);
            MergeSection(target.moodles, source["moodles"]);

            MergeSection(target.other, source["other"]);

            MergeSection(target.other, source["log"]);
            MergeSection(target.other, source["command"]);
            MergeSection(target.other, source["option"]);
            MergeSection(target.other, source["liquid"]);
            MergeSection(target.other, source["title"]);
        }

        private static void MergeSection(Dictionary<string, string> target, JToken sectionToken)
        {
            if (target == null || sectionToken == null) return;

            if (!(sectionToken is JObject section)) return;

            foreach (var property in section.Properties())
            {
                if (string.IsNullOrWhiteSpace(property.Name)) continue;

                var value = property.Value.Type == JTokenType.String
                    ? property.Value.Value<string>()
                    : property.Value.ToString();
                if (string.IsNullOrWhiteSpace(value)) continue;

                target[property.Name.Trim()] = value;
            }
        }

        private static string TryReadValue(Language language, string category, string key)
        {
            if (language == null || string.IsNullOrWhiteSpace(key)) return string.Empty;

            var section = category == "item" ? language.main :
                category == "building" ? language.buildings :
                category == "moodle" ? language.moodles :
                language.other;

            if (section == null) return string.Empty;

            if (section.TryGetValue(key, out var value)) return value ?? string.Empty;

            return string.Empty;
        }

        private static string GetPluginSortKey(PluginInfo pluginInfo)
        {
            if (pluginInfo?.Metadata != null && !string.IsNullOrWhiteSpace(pluginInfo.Metadata.GUID))
                return pluginInfo.Metadata.GUID.Trim();

            if (!string.IsNullOrWhiteSpace(pluginInfo?.Location))
                return pluginInfo.Location;

            return string.Empty;
        }

        private static Assembly ResolvePluginAssembly(PluginInfo pluginInfo)
        {
            var instanceAssembly = pluginInfo?.Instance != null ? pluginInfo.Instance.GetType().Assembly : null;
            if (instanceAssembly != null) return instanceAssembly;

            var normalizedLocation = NormalizeExistingPath(pluginInfo?.Location);
            if (string.IsNullOrWhiteSpace(normalizedLocation)) return null;

            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly =>
                {
                    try
                    {
                        return string.Equals(NormalizeExistingPath(assembly.Location), normalizedLocation,
                            StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });
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

        private static string NormalizeResourceName(string resourceName)
        {
            return string.IsNullOrWhiteSpace(resourceName)
                ? string.Empty
                : resourceName.Trim().Replace('/', '.').Replace('\\', '.');
        }

        private static bool ResourceNameMatchesLocale(string resourceName, string normalizedFileName)
        {
            if (string.IsNullOrWhiteSpace(resourceName) || string.IsNullOrWhiteSpace(normalizedFileName)) return false;

            var normalizedResourceName = NormalizeResourceName(resourceName);
            return string.Equals(normalizedResourceName, normalizedFileName, StringComparison.OrdinalIgnoreCase) ||
                   normalizedResourceName.EndsWith("." + normalizedFileName, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddIfExists(ICollection<string> results, ISet<string> visitedPaths, string path)
        {
            var normalizedPath = NormalizeExistingPath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath) || !visitedPaths.Add(normalizedPath)) return;

            results.Add(normalizedPath);
        }

        private static void AddRangeIfExists(ICollection<string> results, ISet<string> visitedPaths,
            IEnumerable<string> paths)
        {
            if (paths == null) return;

            foreach (var path in paths)
                AddIfExists(results, visitedPaths, path);
        }

        private static IEnumerable<string> EnumerateMatchingFiles(string rootPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(fileName) ||
                !Directory.Exists(rootPath))
                return Enumerable.Empty<string>();

            try
            {
                return Directory.EnumerateFiles(rootPath, fileName, SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning($"Failed to scan locale overlays under '{rootPath}': {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        private static string ResolveOverlayOwner(string path)
        {
            var normalizedPath = NormalizeExistingPath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath)) return "<unknown>";

            foreach (var pluginInfo in Chainloader.PluginInfos.Values.Where(info => info != null))
            {
                var pluginLocation = NormalizeExistingPath(pluginInfo.Location);
                if (string.IsNullOrWhiteSpace(pluginLocation)) continue;

                var pluginDirectory = Path.GetDirectoryName(pluginLocation);
                if (string.IsNullOrWhiteSpace(pluginDirectory)) continue;

                if (!normalizedPath.StartsWith(pluginDirectory, StringComparison.OrdinalIgnoreCase)) continue;

                if (!string.IsNullOrWhiteSpace(pluginInfo.Metadata?.GUID))
                    return pluginInfo.Metadata.GUID.Trim();

                var assembly = ResolvePluginAssembly(pluginInfo);
                if (assembly != null) return assembly.GetName().Name;

                return pluginDirectory;
            }

            var configLocaleRoot = Path.Combine(Paths.ConfigPath, "CUCoreLib", "Locales");
            if (normalizedPath.StartsWith(configLocaleRoot, StringComparison.OrdinalIgnoreCase))
                return "CUCoreLib config locale folder";

            return Path.GetDirectoryName(normalizedPath) ?? "<unknown>";
        }

        private sealed class EmbeddedLocaleResource
        {
            public EmbeddedLocaleResource(Assembly assembly, string resourceName)
            {
                Assembly = assembly;
                ResourceName = resourceName;
            }

            public Assembly Assembly { get; }
            public string ResourceName { get; }
            public string DisplayName => $"{Assembly.GetName().Name}:{ResourceName}";
        }
    }
}