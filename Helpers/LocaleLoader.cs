using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
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
                    Logger?.LogWarning($"Failed to load locale overlay '{path}': {ex.Message}");
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

            var pluginRoot = Path.Combine(Path.GetDirectoryName(Paths.ConfigPath) ?? string.Empty, "plugins");
            if (!Directory.Exists(pluginRoot))
                return results
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            {
                var pluginMatches = Directory.EnumerateFiles(pluginRoot, fileName, SearchOption.AllDirectories)
                    .Where(path =>
                        path.IndexOf(Path.DirectorySeparatorChar + "Locales" + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf(Path.AltDirectorySeparatorChar + "Locales" + Path.AltDirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase) >= 0);

                results.AddRange(pluginMatches);
            }

            return results
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
    }
}