using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using CUCoreLib.DevTools.HotReload;
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
            Other = 3,
            Log = 4,
            Command = 5,
            Option = 6,
            Liquid = 7,
            Tile = 8,
            Ui = 9
        }

        internal static Dictionary<int, Dictionary<string, string>> CustomLocales =
            new Dictionary<int, Dictionary<string, string>>();

        private static readonly Dictionary<int, HashSet<string>> RequiredLocales =
            new Dictionary<int, HashSet<string>>();

        private static readonly Dictionary<int, Dictionary<string, string>> LocaleOwners =
            new Dictionary<int, Dictionary<string, string>>();

        private static string ActiveOwnerId;

        private static readonly Dictionary<string, int> LocaleRegistrationCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, int> LocaleLookupCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex PlaceholderPattern = new Regex(@"\{(\d+)\}", RegexOptions.Compiled);

        /// <summary>
        ///     Registers a localized string
        /// </summary>
        /// <param name="type">0=Item, 1=Building, 2=Moodle, 3=Other, 4=Log, 5=Command, 6=Option</param>
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

            if (!LocaleOwners.ContainsKey(type))
                LocaleOwners[type] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var value = text ?? string.Empty;
            if (CustomLocales[type].TryGetValue(key, out var existing)
                && !string.IsNullOrWhiteSpace(existing)
                && string.IsNullOrWhiteSpace(value)) return;

            CustomLocales[type][key] = value;
            var ownerId = !string.IsNullOrWhiteSpace(ActiveOwnerId)
                ? ActiveOwnerId
                : ContentReloadSession.ResolveAmbientOwnerId();
            if (!string.IsNullOrWhiteSpace(ownerId)) LocaleOwners[type][key] = ownerId;

            RecordRegistration(TypeToCategory(type), key);
        }

        /// <summary>
        ///     Helper to register using string types.
        /// </summary>
        /// <param name="category">"item", "building", "moodle", or "other"</param>
        /// <param name="key">Locale key within the selected category.</param>
        /// <param name="text">Localized text value to register.</param>
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
            // Methods with optional parameters are overloaded and hidden
            string optionalFallbackIfLocaleValueNullOrWhitespace = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return optionalFallbackIfLocaleValueNullOrWhitespace ?? string.Empty;

            var normalizedKey = key.Trim();
            if (string.IsNullOrWhiteSpace(optionalFallbackIfLocaleValueNullOrWhitespace))
            {
                Require(category, normalizedKey);
                RecordLookup(category, normalizedKey);
                var runtimeValue = LocaleLoader.GetLocalizedText(category, normalizedKey);
                return string.IsNullOrWhiteSpace(runtimeValue) ? normalizedKey : runtimeValue;
            }

            Register(category, normalizedKey, optionalFallbackIfLocaleValueNullOrWhitespace);
            RecordLookup(category, normalizedKey);
            var value = LocaleLoader.GetLocalizedText(category, normalizedKey,
                optionalFallbackIfLocaleValueNullOrWhitespace);
            return string.IsNullOrWhiteSpace(value) ? optionalFallbackIfLocaleValueNullOrWhitespace : value;
        }

        /// <summary>
        ///     Gets a localized string and replaces {0}, {1}, ... placeholders with the supplied arguments.
        /// </summary>
        /// <param name="category">Locale category such as "item", "building", "log", or "other".</param>
        /// <param name="key">Locale key within the selected category.</param>
        /// <param name="args">
        ///     Values injected into {0}, {1}, ... placeholders. Out-of-range placeholders are preserved verbatim.
        /// </param>
        /// <returns>
        ///     The formatted localized text. Falls back to the formatted raw key when no translation exists,
        ///     so the result is never null.
        /// </returns>
        public static string GetFormatted(string category, string key, params object[] args)
        {
            return Format(Get(category, key, null), key, args);
        }

        /// <summary>
        ///     Gets a localized string from the "other" category and replaces {0}, {1}, ... placeholders.
        /// </summary>
        /// <param name="key">Locale key within the "other" category.</param>
        /// <param name="args">Values injected into {0}, {1}, ... placeholders.</param>
        /// <returns>The formatted localized text; never null.</returns>
        public static string GetFormatted(string key, params object[] args)
        {
            return Format(Get("other", key, null), key, args);
        }

        /// <summary>
        ///     Gets a localized string and replaces {0}, {1}, ... placeholders, using an explicit fallback.
        /// </summary>
        /// <param name="category">Locale category such as "item", "building", "log", or "other".</param>
        /// <param name="key">Locale key within the selected category.</param>
        /// <param name="fallback">Fallback text used when no translation exists.</param>
        /// <param name="args">
        ///     Values injected into {0}, {1}, ... placeholders. Out-of-range placeholders are preserved verbatim.
        /// </param>
        /// <returns>The formatted localized text; never null.</returns>
        public static string GetFormattedWithFallback(
            string category, 
            string key,
            string fallback,
            params object[] args)
        {
            return Format(Get(category, key, fallback), key, args);
        }

        internal static string FormatText(string text, string key, object[] args)
        {
            return Format(text, key, args);
        }

        private static string Format(string text, string key, object[] args)
        {
            if (args == null || args.Length == 0) return text ?? string.Empty;
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return PlaceholderPattern.Replace(text, match =>
            {
                if (!int.TryParse(match.Groups[1].Value, out var index)) return match.Value;
                if (index >= 0 && index < args.Length) return args[index]?.ToString() ?? string.Empty;

                CUCoreLibPlugin.Log.LogWarning(
                    $"Locale placeholder {{{index}}} is out of range for key '{key}' (args: {args.Length}).");
                return match.Value;
            });
        }

        public static void RegisterCraftingQuality(string id, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var normalizedId = id.Trim();
            var key = "cq" + normalizedId;
            var fallbackName = string.IsNullOrWhiteSpace(displayName)
                ? HumanizeCraftingQualityId(normalizedId)
                : displayName.Trim();

            if (CustomLocales.TryGetValue((int)LocaleCategory.Other, out var otherLocales) &&
                otherLocales.TryGetValue(key, out var existingValue) &&
                !string.IsNullOrWhiteSpace(existingValue))
                return;

            Register("other", key, fallbackName);
        }

        public static void RegisterCraftingQualities(IEnumerable<CraftingQuality> qualities)
        {
            if (qualities == null) return;

            foreach (var quality in qualities)
            {
                if (quality == null || string.IsNullOrWhiteSpace(quality.id)) continue;
                RegisterCraftingQuality(quality.id);
            }
        }

        public static JObject BuildLocaleJson(JObject existing = null)
        {
            var root = existing != null ? (JObject)existing.DeepClone() : new JObject();

            for (var type = 0; type <= 8; type++)
            {
                var category = TypeToCategory(type);
                if (!(root[category] is JObject categoryObject))
                {
                    categoryObject = new JObject();
                    root[category] = categoryObject;
                }

                if (CustomLocales.TryGetValue(type, out var generated))
                    foreach (var entry in generated)
                        categoryObject[entry.Key] = entry.Value ?? string.Empty;

                if (!RequiredLocales.TryGetValue(type, out var requiredKeys)) continue;
                foreach (var key in requiredKeys.Where(key => categoryObject[key] == null))
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

            // Serialize a plain CLR graph so locale generation does not depend on
            // JToken.WriteTo, which can fail when another mod loads an older Newtonsoft.Json first.
            var json = JsonConvert.SerializeObject(ConvertTokenToPlainObject(output), Formatting.Indented);

            File.WriteAllText(path, json);
            return path;
        }

        public static string GetDefaultLocalePath()
        {
            return Path.Combine(Paths.ConfigPath, "CUCoreLib", "Locales", "EN.json");
        }

        /// <summary>
        ///     Returns how many times a locale key was registered through <see cref="Register(string,string,string)" />.
        /// </summary>
        /// <param name="category">Locale category to inspect.</param>
        /// <param name="key">Locale key within the category.</param>
        /// <returns>The registration count, or 0 when the key was never registered.</returns>
        public static int GetRegistrationCount(string category, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return 0;

            return LocaleRegistrationCounts.TryGetValue(BuildDiagnosticKey(category, key), out var count) 
                ? count
                : 0;
        }

        /// <summary>
        ///     Returns how many times a locale key was queried through <see cref="Get(string,string,string)" />.
        /// </summary>
        /// <param name="category">Locale category to inspect.</param>
        /// <param name="key">Locale key within the category.</param>
        /// <returns>The lookup count, or 0 when the key was never queried.</returns>
        public static int GetLookupCount(string category, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return 0;

            return LocaleLookupCounts.TryGetValue(BuildDiagnosticKey(category, key), out var count) 
                ? count
                : 0;
        }

        /// <summary>
        ///     Returns a snapshot of every locale key that was queried at least once, mapped to its lookup count.
        /// </summary>
        /// <returns>A new dictionary keyed by "{category}.{key}"; never null.</returns>
        public static Dictionary<string, int> GetLookupCounts()
        {
            return new Dictionary<string, int>(LocaleLookupCounts, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Returns a snapshot of every locale key that was registered at least once, mapped to its registration count.
        /// </summary>
        /// <returns>A new dictionary keyed by "{category}.{key}"; never null.</returns>
        public static Dictionary<string, int> GetRegistrationCounts()
        {
            return new Dictionary<string, int>(LocaleRegistrationCounts, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Clears both the registration and lookup diagnostics without touching registered translations.
        /// </summary>
        public static void ResetDiagnostics()
        {
            LocaleRegistrationCounts.Clear();
            LocaleLookupCounts.Clear();
        }

        private static void RecordRegistration(string category, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var diagnosticKey = BuildDiagnosticKey(category, key);
            LocaleRegistrationCounts[diagnosticKey] =
                LocaleRegistrationCounts.TryGetValue(diagnosticKey, out var count) ? count + 1 : 1;
        }

        private static void RecordLookup(string category, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var diagnosticKey = BuildDiagnosticKey(category, key);
            LocaleLookupCounts[diagnosticKey] =
                LocaleLookupCounts.TryGetValue(diagnosticKey, out var count) ? count + 1 : 1;
        }

        private static string BuildDiagnosticKey(string category, string key)
        {
            var normalizedCategory = (category ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedCategory.Length == 0) normalizedCategory = "other";

            return normalizedCategory + "." + key.Trim();
        }

        public static IDisposable BeginOwnerRegistration(string ownerId)
        {
            return new OwnerScope(ownerId);
        }

        internal static Dictionary<int, Dictionary<string, string>> CaptureOwnerEntries(string ownerId)
        {
            var snapshot = new Dictionary<int, Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(ownerId)) return snapshot;

            var normalizedOwnerId = ownerId.Trim();
            foreach (var entry in LocaleOwners)
            {
                var ownedEntries = entry.Value
                    .Where(pair => string.Equals(pair.Value, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key)
                    .Where(key => CustomLocales.TryGetValue(entry.Key, out var locales) && locales.ContainsKey(key))
                    .ToDictionary(
                        key => key,
                        key => CustomLocales[entry.Key][key],
                        StringComparer.OrdinalIgnoreCase);

                if (ownedEntries.Count > 0) snapshot[entry.Key] = ownedEntries;
            }

            return snapshot;
        }

        internal static void RestoreOwnerEntries(string ownerId, IDictionary<int, Dictionary<string, string>> snapshot)
        {
            if (snapshot == null || snapshot.Count == 0) return;

            foreach (var category in snapshot)
            foreach (var entry in category.Value)
                Register(category.Key, entry.Key, entry.Value);
        }

        internal static void ClearOwnerEntries(string ownerId, ContentReloadResult result)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return;

            var normalizedOwnerId = ownerId.Trim();
            var removed = 0;

            foreach (var entry in LocaleOwners.ToArray())
            {
                var keys = entry.Value
                    .Where(pair => string.Equals(pair.Value, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key)
                    .ToArray();

                foreach (var key in keys)
                {
                    entry.Value.Remove(key);
                    if (CustomLocales.TryGetValue(entry.Key, out var locales)) locales.Remove(key);

                    removed++;
                }
            }

            if (removed > 0)
                result?.AddInfo("Cleared " + removed + " locale entries owned by '" + normalizedOwnerId + "'.");
        }

        private static int CategoryToType(string category)
        {
            var normalizedCategory = (category ?? string.Empty).Trim().ToLowerInvariant();
            return (int)(normalizedCategory == "item" ? LocaleCategory.Item :
                normalizedCategory == "building" ? LocaleCategory.Building :
                normalizedCategory == "moodle" ? LocaleCategory.Moodle :
                normalizedCategory == "log" ? LocaleCategory.Log :
                normalizedCategory == "command" ? LocaleCategory.Command :
                normalizedCategory == "option" ? LocaleCategory.Option :
                normalizedCategory == "liquid" ? LocaleCategory.Liquid :
                normalizedCategory == "tile" ? LocaleCategory.Tile :
                normalizedCategory == "ui" ? LocaleCategory.Ui :
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
                case LocaleCategory.Log:
                    return "log";
                case LocaleCategory.Command:
                    return "command";
                case LocaleCategory.Option:
                    return "option";
                case LocaleCategory.Liquid:
                    return "liquid";
                case LocaleCategory.Tile:
                    return "tile";
                case LocaleCategory.Ui:
                    return "ui";
                case LocaleCategory.Other:
                default:
                    return "other";
            }
        }

        private static object ConvertTokenToPlainObject(JToken token)
        {
            while (true)
            {
                if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined) return null;

                switch (token)
                {
                    case JObject obj:
                    {
                        var result = new Dictionary<string, object>(StringComparer.Ordinal);
                        foreach (var property in obj.Properties())
                            result[property.Name] = ConvertTokenToPlainObject(property.Value);

                        return result;
                    }
                    case JArray array:
                    {
                        return array.Select(ConvertTokenToPlainObject).ToList();
                    }
                    case JProperty propertyToken:
                        token = propertyToken.Value;
                        continue;
                    case JValue value:
                        return value.Value;
                    case JContainer container:
                    {
                        return container.Children().Select(ConvertTokenToPlainObject).ToList();
                    }
                    default:
                        return null;
                }
            }
        }

        private static string HumanizeCraftingQualityId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;

            var trimmed = id.Trim().Replace('_', ' ').Replace('-', ' ');
            var builder = new StringBuilder(trimmed.Length + 4);

            for (var i = 0; i < trimmed.Length; i++)
            {
                var current = trimmed[i];
                if (i > 0 && char.IsUpper(current))
                {
                    var previous = trimmed[i - 1];
                    var hasNext = i + 1 < trimmed.Length;
                    var next = hasNext ? trimmed[i + 1] : '\0';
                    if (char.IsLower(previous) || char.IsDigit(previous) ||
                        (char.IsUpper(previous) && hasNext && char.IsLower(next)))
                        builder.Append(' ');
                }

                builder.Append(current);
            }

            var words = builder.ToString()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1))
                .ToArray();

            return words.Length == 0 ? trimmed : string.Join(" ", words);
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