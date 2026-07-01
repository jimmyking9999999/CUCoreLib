namespace CUCoreLib.Util;

public static class MathExtensions
{
    public static bool IsFinite(this float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}