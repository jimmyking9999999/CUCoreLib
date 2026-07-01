using UnityEngine;

namespace CUCoreLib.Util;

public static class MathExtensions
{
    public static bool IsFinite(this float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static bool IsFinite(this Vector2 value)
    {
        return value.x.IsFinite() && value.y.IsFinite();
    }
}
