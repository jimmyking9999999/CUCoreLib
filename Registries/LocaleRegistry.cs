using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using CUCoreLib.ContentReload;
using CUCoreLib.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Registries;

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
        Title = 8
    }

    internal static Dictionary<int, Dictionary<string, string>> CustomLocales = new();

    private static readonly Dictionary<int, HashSet<string>> RequiredLocales = new();

    private static readonly Dictionary<int, Dictionary<string, string>> LocaleOwners = new();

    private static string ActiveOwnerId;

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
        if (CustomLocales[type].TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing) &&
            string.IsNullOrWhiteSpace(value)) return;

        CustomLocales[type][key] = value;
        var ownerId = !string.IsNullOrWhiteSpace(ActiveOwnerId)
            ? ActiveOwnerId
            : ContentReloadSession.ResolveAmbientOwnerId();
        if (!string.IsNullOrWhiteSpace(ownerId)) LocaleOwners[type][key] = ownerId;
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
        // Methods with optional parameters are overloaded and hidden
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

        Register(category, normalizedKey, optionalFallbackIfLocaleValueNullOrWhitespace);
        var value = LocaleLoader.GetLocalizedText(category, normalizedKey,
            optionalFallbackIfLocaleValueNullOrWhitespace);
        return string.IsNullOrWhiteSpace(value) ? optionalFallbackIfLocaleValueNullOrWhitespace : value;
    }

    public static JObject BuildLocaleJson(JObject existing = null)
    {
        var root = existing != null ? (JObject)existing.DeepClone() : new JObject();

        for (var type = 0; type <= 6; type++)
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
            normalizedCategory == "title" ? LocaleCategory.Title :
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
            case LocaleCategory.Title:
                return "title";
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