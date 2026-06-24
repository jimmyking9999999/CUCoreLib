using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;

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
        private static readonly Dictionary<int, Dictionary<string, string>> LocaleOwners =
            new Dictionary<int, Dictionary<string, string>>();
        private static string ActiveOwnerId;

        /// <summary>
        /// Registers a localized string
        /// </summary>
        /// <param name="type">0=Item, 1=Building, 2=Moodle, 3=Other (UI/Fluids)</param>
        /// <param name="key">The ID key (e.g. "sunpear")</param>
        /// <param name="text">The text to display</param>
        public static void Register(int type, string key, string text)
        {
            Register((LocaleCategory)type, key, text);
        }

        /// <summary>
        /// Registers a localized string
        /// </summary>
        /// <param name="category">The locale category to register under.</param>
        /// <param name="key">The ID key (e.g. "sunpear")</param>
        /// <param name="text">The text to display</param>
        public static void Register(LocaleCategory category, string key, string text)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            key = key.Trim();

            int type = (int)category;

            if (!CustomLocales.ContainsKey(type))
            {
                CustomLocales[type] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!LocaleOwners.ContainsKey(type))
            {
                LocaleOwners[type] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            string value = text ?? string.Empty;
            if (CustomLocales[type].TryGetValue(key, out string existing) && !string.IsNullOrWhiteSpace(existing) && string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            CustomLocales[type][key] = value;
            if (!string.IsNullOrWhiteSpace(ActiveOwnerId))
            {
                LocaleOwners[type][key] = ActiveOwnerId;
            }
        }

        /// <summary>
        /// Helper to register using string types.
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

            int type = CategoryToType(category);
            if (!RequiredLocales.ContainsKey(type))
            {
                RequiredLocales[type] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            RequiredLocales[type].Add(key.Trim());
        }

        public static string Get(string key, string optionalFallbackIfLocaleValueNullOrWhitespace = null)
        {
            return Get("other", key, optionalFallbackIfLocaleValueNullOrWhitespace);
        }

        public static string Get(string category, string key, string optionalFallbackIfLocaleValueNullOrWhitespace = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return optionalFallbackIfLocaleValueNullOrWhitespace ?? string.Empty;
            }

            string normalizedKey = key.Trim();
            if (string.IsNullOrWhiteSpace(optionalFallbackIfLocaleValueNullOrWhitespace))
            {
                Require(category, normalizedKey);
                string runtimeValue = LocaleLoader.GetLocalizedText(category, normalizedKey, null);
                return string.IsNullOrWhiteSpace(runtimeValue) ? normalizedKey : runtimeValue;
            }

            string fallback = optionalFallbackIfLocaleValueNullOrWhitespace;
            Register(category, normalizedKey, fallback);
            string value = LocaleLoader.GetLocalizedText(category, normalizedKey, fallback);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public static JObject BuildLocaleJson(JObject existing = null)
        {
            JObject root = existing != null ? (JObject)existing.DeepClone() : new JObject();

            for (int type = 0; type <= 3; type++)
            {
                string category = TypeToCategory(type);
                JObject categoryObject = root[category] as JObject;
                if (categoryObject == null)
                {
                    categoryObject = new JObject();
                    root[category] = categoryObject;
                }

                if (CustomLocales.TryGetValue(type, out var generated))
                {
                    foreach (var entry in generated)
                    {
                        categoryObject[entry.Key] = entry.Value ?? string.Empty;
                    }
                }

                if (RequiredLocales.TryGetValue(type, out var requiredKeys))
                {
                    foreach (string key in requiredKeys)
                    {
                        if (categoryObject[key] == null)
                        {
                            categoryObject[key] = string.Empty;
                        }
                    }
                }
            }

            return root;
        }

        public static string WriteLocaleFile(string path = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = GetDefaultLocalePath();
            }

            JObject existing = null;
            if (File.Exists(path))
            {
                try
                {
                    existing = JObject.Parse(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    CUCoreLibPlugin.Log.LogWarning($"Existing locale file could not be parsed and will be replaced: {ex.Message}");
                }
            }

            JObject output = BuildLocaleJson(existing);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Serialize a plain CLR graph so locale generation does not depend on
            // JToken.WriteTo, which can fail when another mod loads an older Newtonsoft.Json first.
            string json = JsonConvert.SerializeObject(ConvertTokenToPlainObject(output), Formatting.Indented);

            File.WriteAllText(path, json);
            return path;
        }

        public static string GetDefaultLocalePath()
        {
            return Path.Combine(BepInEx.Paths.ConfigPath, "CUCoreLib", "Locales", "EN.json");
        }

        public static IDisposable BeginOwnerRegistration(string ownerId)
        {
            return new OwnerScope(ownerId);
        }

        internal static Dictionary<int, Dictionary<string, string>> CaptureOwnerEntries(string ownerId)
        {
            Dictionary<int, Dictionary<string, string>> snapshot = new Dictionary<int, Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return snapshot;
            }

            string normalizedOwnerId = ownerId.Trim();
            foreach (KeyValuePair<int, Dictionary<string, string>> entry in LocaleOwners)
            {
                Dictionary<string, string> ownedEntries = entry.Value
                    .Where(pair => string.Equals(pair.Value, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key)
                    .Where(key => CustomLocales.TryGetValue(entry.Key, out Dictionary<string, string> locales) && locales.ContainsKey(key))
                    .ToDictionary(
                        key => key,
                        key => CustomLocales[entry.Key][key],
                        StringComparer.OrdinalIgnoreCase);

                if (ownedEntries.Count > 0)
                {
                    snapshot[entry.Key] = ownedEntries;
                }
            }

            return snapshot;
        }

        internal static void RestoreOwnerEntries(string ownerId, IDictionary<int, Dictionary<string, string>> snapshot)
        {
            if (snapshot == null || snapshot.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int, Dictionary<string, string>> category in snapshot)
            {
                foreach (KeyValuePair<string, string> entry in category.Value)
                {
                    Register(category.Key, entry.Key, entry.Value);
                }
            }
        }

        internal static void ClearOwnerEntries(string ownerId, ContentReloadResult result)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return;
            }

            string normalizedOwnerId = ownerId.Trim();
            int removed = 0;

            foreach (KeyValuePair<int, Dictionary<string, string>> entry in LocaleOwners.ToArray())
            {
                string[] keys = entry.Value
                    .Where(pair => string.Equals(pair.Value, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key)
                    .ToArray();

                for (int i = 0; i < keys.Length; i++)
                {
                    string key = keys[i];
                    entry.Value.Remove(key);
                    if (CustomLocales.TryGetValue(entry.Key, out Dictionary<string, string> locales))
                    {
                        locales.Remove(key);
                    }

                    removed++;
                }
            }

            if (removed > 0)
            {
                result?.AddInfo("Cleared " + removed + " locale entries owned by '" + normalizedOwnerId + "'.");
            }
        }

        private static int CategoryToType(string category)
        {
            string normalizedCategory = (category ?? string.Empty).Trim().ToLowerInvariant();
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

        private static object ConvertTokenToPlainObject(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            if (token is JObject obj)
            {
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (JProperty property in obj.Properties())
                {
                    result[property.Name] = ConvertTokenToPlainObject(property.Value);
                }

                return result;
            }

            if (token is JArray array)
            {
                List<object> result = new List<object>();
                foreach (JToken entry in array)
                {
                    result.Add(ConvertTokenToPlainObject(entry));
                }

                return result;
            }

            if (token is JValue value)
            {
                return value.Value;
            }

            return token.ToString(Formatting.None);
        }

        private sealed class OwnerScope : IDisposable
        {
            private readonly string previousOwnerId;

            public OwnerScope(string ownerId)
            {
                previousOwnerId = ActiveOwnerId;
                ActiveOwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId.Trim();
            }

            public void Dispose()
            {
                ActiveOwnerId = previousOwnerId;
            }
        }

    }
}
