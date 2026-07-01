using System;
using System.Text.RegularExpressions;
using CUCoreLib.Registries;
using JetBrains.Annotations;

namespace CUCoreLib.Util;

public static class LocaleUtils
{
    public static bool HasKey(string category, string key)
    {
        var text = LocaleRegistry.Get(category, key, key);
        return !string.IsNullOrWhiteSpace(text) && text != key;
    }

    public static bool HasKeyItem(string key)
    {
        return HasKey("item", key);
    }

    public static bool HasKeyBuilding(string key)
    {
        return HasKey("build", key);
    }

    public static bool HasKeyMoodle(string key)
    {
        return HasKey("moodle", key);
    }

    public static bool HasKeyOther(string key)
    {
        return HasKey("other", key);
    }

    public static bool HasKeyLog(string key)
    {
        return HasKey("log", key);
    }

    public static bool HasKeyCommand(string key)
    {
        return HasKey("command", key);
    }

    public static bool HasKeyOption(string key)
    {
        return HasKey("option", key);
    }

    public static bool HasKeyLiquid(string key)
    {
        return HasKey("liquid", key);
    }

    public static bool HasKeyTitle(string key)
    {
        return HasKey("title", key);
    }

    private static string Replace(string key, [NotNull] params object[] args)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        if (args.Length == 0) return key;
        return Regex.Replace(key, @"\{(\d+)\}", match =>
        {
            var index = int.Parse(match.Groups[1].Value);
            return args[index].ToString();
        });
    }

    private static string Get(string category, string key, params object[] args)
    {
        var resolvedKey = Replace(key, args);

        var text = LocaleRegistry.Get(category, resolvedKey, resolvedKey);
        if (!string.IsNullOrWhiteSpace(text) && text != resolvedKey)
            return text;
        return key;
    }

    public static string GetItem(string key, params object[] args)
    {
        return Get("item", key, args);
    }

    public static string GetBuilding(string key, params object[] args)
    {
        return Get("build", key, args);
    }

    public static string GetMoodle(string key, params object[] args)
    {
        return Get("moodle", key, args);
    }

    public static string GetOther(string key, params object[] args)
    {
        return Get("other", key, args);
    }

    public static string GetLog(string key, params object[] args)
    {
        return Get("log", key, args);
    }

    public static string GetCommand(string key, params object[] args)
    {
        return Get("command", key, args);
    }

    public static string GetOption(string key, params object[] args)
    {
        return Get("option", key, args);
    }

    public static string GetLiquid(string key, params object[] args)
    {
        return Get("liquid", key, args);
    }

    public static string GetTitle(string key, params object[] args)
    {
        return Get("title", key, args);
    }
}
