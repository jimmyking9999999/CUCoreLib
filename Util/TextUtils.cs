using UnityEngine;

namespace CUCoreLib.Util;

public static class TextUtils
{
    public static string Color(string text, string color)
    {
        return string.IsNullOrEmpty(text) ? text :
            string.IsNullOrEmpty(color) ? text : $"<color={color}>{text}</color>";
    }

    public static string Color(string text, Color color)
    {
        return Color(text, ColorUtility.ToHtmlStringRGB(color));
    }

    public static string Hex(string text, string hex)
    {
        if (string.IsNullOrEmpty(hex)) return text;
        if (!hex.StartsWith("#")) hex = "#" + hex;
        return Color(text, hex);
    }

    public static string Alpha(string text, string alpha)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(alpha)) return text;
        if (!alpha.StartsWith("#")) alpha = "#" + alpha;
        return $"<alpha={alpha}>{text}<alpha=#FF>";
    }

    public static string Alpha(string text, float alpha)
    {
        return Alpha(text, ((int)(Mathf.Clamp01(alpha) * 255)).ToString("X2"));
    }

    public static string Alpha(string text, byte alpha)
    {
        return Alpha(text, alpha.ToString("X2"));
    }

    public static string Bold(string text)
    {
        return string.IsNullOrEmpty(text) ? text : $"<b>{text}</b>";
    }

    public static string Italic(string text)
    {
        return string.IsNullOrEmpty(text) ? text : $"<i>{text}</i>";
    }

    public static string Unline(string text)
    {
        return string.IsNullOrEmpty(text) ? text : $"<u>{text}</u>";
    }

    public static string Delete(string text)
    {
        return string.IsNullOrEmpty(text) ? text : $"<s>{text}</s>";
    }

    public static string Size(string text, int size)
    {
        return string.IsNullOrEmpty(text) ? text : $"<size={size}>{text}</size>";
    }

    public static string Blue(string text)
    {
        return Color(text, "blue");
    }

    public static string Red(string text)
    {
        return Color(text, "red");
    }

    public static string Green(string text)
    {
        return Color(text, "green");
    }

    public static string Yellow(string text)
    {
        return Color(text, "yellow");
    }

    public static string White(string text)
    {
        return Color(text, "white");
    }

    public static string Black(string text)
    {
        return Color(text, "black");
    }

    public static string Cyan(string text)
    {
        return Color(text, "cyan");
    }

    public static string Magenta(string text)
    {
        return Color(text, "magenta");
    }

    public static string Gray(string text)
    {
        return Color(text, "gray");
    }

    public static string Orange(string text)
    {
        return Color(text, "orange");
    }

    public static string Purple(string text)
    {
        return Color(text, "purple");
    }

    public static string Pink(string text)
    {
        return Color(text, "pink");
    }

    public static string Brown(string text)
    {
        return Color(text, "brown");
    }
}
