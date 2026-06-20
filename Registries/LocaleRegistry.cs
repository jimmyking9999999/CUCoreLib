using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using CUCoreLib.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Registries
{
    public static class LocaleRegistry
    {
        public enum LocaleCategory
        {
            Item = 0,
            Building = 1,
            Moodle = 2,
            Other = 3
        }

        internal static Dictionary<int, Dictionary<string, string>> CustomLocales =
            new Dictionary<int, Dictionary<string, string>>();

        private static readonly Dictionary<int, HashSet<string>> RequiredLocales =
            new Dictionary<int, HashSet<string>>();

        /// <summary>
        ///     Registers a localized string
        /// </summary>
        /// <param name="type">0=Item, 1=Building, 2=Moodle, 3=Other (UI/Fluids)</param>
        /// <param name="key">The ID key (e.g. "sunpear")</param>
        /// <param name="text">The text to display</param>
        public static void Register(int type, string key, string text)
        {
            Register((LocaleCategory)type, key, text);
        }

        /// <summary>
        ///     Registers a localized string
        /// </summary>
        /// <param name="category">The locale category to register under.</param>
        /// <param name="key">The ID key (e.g. "sunpear")</param>
        /// <param name="text">The text to display</param>
        public static void Register(LocaleCategory category, string key, string text)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            key = key.Trim();

            var type = (int)category;

            if (!CustomLocales.ContainsKey(type))
                CustomLocales[type] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var value = text ?? string.Empty;
            if (CustomLocales[type].TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing) &&
                string.IsNullOrWhiteSpace(value)) return;

            CustomLocales[type][key] = value;
        }

        /// <summary>
        ///     Helper to register using string types.
        /// </summary>
        /// <param name="category">"item", "building", "moodle", or "other"</param>
        public static void Register(string category, string key, string text)
        {
            Register(CategoryToType(category), key, text);
        }

        public static void Require(string key)
        {
            Require("other", key);
        }

        public static void Require(string category, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var type = CategoryToType(category);
            if (!RequiredLocales.ContainsKey(type))
                RequiredLocales[type] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            RequiredLocales[type].Add(key.Trim());
        }

        public static string Get(string key, string optionalFallbackIfLocaleValueNullOrWhitespace = null)
        {
            return Get("other", key, optionalFallbackIfLocaleValueNullOrWhitespace);
        }

        public static string Get(string category, string key,
            string optionalFallbackIfLocaleValueNullOrWhitespace = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return optionalFallbackIfLocaleValueNullOrWhitespace ?? string.Empty;

            var normalizedKey = key.Trim();
            if (string.IsNullOrWhiteSpace(optionalFallbackIfLocaleValueNullOrWhitespace))
            {
                Require(category, normalizedKey);
                var runtimeValue = LocaleLoader.GetLocalizedText(category, normalizedKey);
                return string.IsNullOrWhiteSpace(runtimeValue) ? normalizedKey : runtimeValue;
            }

            var fallback = optionalFallbackIfLocaleValueNullOrWhitespace;
            Register(category, normalizedKey, fallback);
            var value = LocaleLoader.GetLocalizedText(category, normalizedKey, fallback);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public static JObject BuildLocaleJson(JObject existing = null)
        {
            var root = existing != null ? (JObject)existing.DeepClone() : new JObject();

            for (var type = 0; type <= 3; type++)
            {
                var category = TypeToCategory(type);
                var categoryObject = root[category] as JObject;
                if (categoryObject == null)
                {
                    categoryObject = new JObject();
                    root[category] = categoryObject;
                }

                if (CustomLocales.TryGetValue(type, out var generated))
                    foreach (var entry in generated)
                        categoryObject[entry.Key] = entry.Value ?? string.Empty;

                if (RequiredLocales.TryGetValue(type, out var requiredKeys))
                    foreach (var key in requiredKeys)
                        if (categoryObject[key] == null)
                            categoryObject[key] = string.Empty;
            }

            return root;
        }

        public static string WriteLocaleFile(string path = null)
        {
            if (string.IsNullOrWhiteSpace(path)) path = GetDefaultLocalePath();

            JObject existing = null;
            if (File.Exists(path))
                try
                {
                    existing = JObject.Parse(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    CUCoreLibPlugin.Log.LogWarning(
                        $"Existing locale file could not be parsed and will be replaced: {ex.Message}");
                }

            var output = BuildLocaleJson(existing);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string json;
            using (var textWriter = new StringWriter())
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;
                output.WriteTo(jsonWriter);
                jsonWriter.Flush();
                json = textWriter.ToString();
            }

            File.WriteAllText(path, json);
            return path;
        }

        public static string GetDefaultLocalePath()
        {
            return Path.Combine(Paths.ConfigPath, "CUCoreLib", "Locales", "EN.json");
        }

        private static int CategoryToType(string category)
        {
            var normalizedCategory = (category ?? string.Empty).Trim().ToLowerInvariant();
            return (int)(normalizedCategory == "item" ? LocaleCategory.Item :
                normalizedCategory == "building" ? LocaleCategory.Building :
                normalizedCategory == "moodle" ? LocaleCategory.Moodle :
                LocaleCategory.Other);
        }

        private static string TypeToCategory(int type)
        {
            switch ((LocaleCategory)type)
            {
                case LocaleCategory.Item:
                    return "item";
                case LocaleCategory.Building:
                    return "building";
                case LocaleCategory.Moodle:
                    return "moodle";
                default:
                    return "other";
            }
        }
    }
}