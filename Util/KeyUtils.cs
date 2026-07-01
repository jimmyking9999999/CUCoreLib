using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CUCoreLib.Util;

public static class KeyUtils
{
    private static readonly Dictionary<KeyCode, Sprite> KeySpriteCache = new();

    private static readonly Dictionary<KeyCode, string> FriendlyKeyNames = new()
    {
        { KeyCode.Mouse0, "Left Click" },
        { KeyCode.Mouse1, "Right Click" },
        { KeyCode.Mouse2, "Middle Click" },
        { KeyCode.Alpha0, "0" },
        { KeyCode.Alpha1, "1" },
        { KeyCode.Alpha2, "2" },
        { KeyCode.Alpha3, "3" },
        { KeyCode.Alpha4, "4" },
        { KeyCode.Alpha5, "5" },
        { KeyCode.Alpha6, "6" },
        { KeyCode.Alpha7, "7" },
        { KeyCode.Alpha8, "8" },
        { KeyCode.Alpha9, "9" },
        { KeyCode.Return, "Enter" },
        { KeyCode.Escape, "Esc" },
        { KeyCode.BackQuote, "~" }
    };

    public static Sprite GetKeySprite(KeyCode key, string spritePrefix = "Key_")
    {
        if (KeySpriteCache.TryGetValue(key, out var cached)) return cached;

        var spriteName = spritePrefix + key;
        var found = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == spriteName);
        if (found != null) KeySpriteCache[key] = found;

        return found;
    }

    public static string GetFriendlyKeyName(KeyCode key)
    {
        return FriendlyKeyNames.TryGetValue(key, out var friendly) ? friendly : key.ToString();
    }

    public static void SetFriendlyKeyName(KeyCode key, string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            FriendlyKeyNames.Remove(key);
            return;
        }

        FriendlyKeyNames[key] = displayName;
    }
}