using System.Collections.Generic;
using System.Reflection;

namespace CUCoreLib.Util;

public static class ReflectionUtils
{
    private static readonly Dictionary<string, MethodInfo> MethodCache = new();

    public static MethodInfo GetMethod(object target, string methodName)
    {
        if (target == null || string.IsNullOrEmpty(methodName)) return null;

        return target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.FlattenHierarchy
        );
    }

    public static void InvokeMethod(object target, string methodName, params object[] args)
    {
        var method = GetCachedMethod(target, methodName);
        if (method == null) return;

        method.Invoke(target, args);
    }

    private static MethodInfo GetCachedMethod(object target, string methodName)
    {
        if (target == null || string.IsNullOrEmpty(methodName)) return null;

        var targetType = target.GetType();
        var cacheKey = targetType.FullName + "::" + methodName;
        if (MethodCache.TryGetValue(cacheKey, out var cached)) return cached;

        var method = targetType.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.FlattenHierarchy
        );

        MethodCache[cacheKey] = method;
        return method;
    }
}