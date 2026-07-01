using System.Runtime.CompilerServices;

namespace CUCoreLib.Helpers;

internal static class ExtensionData
{
    // Attach data to game objects
    public static void Set<TKey, TValue>(TKey target, TValue value)
        where TKey : class
        where TValue : class
    {
        // Ensure update in the worst possible way
        TableHolder<TKey, TValue>.Table.Remove(target);
        TableHolder<TKey, TValue>.Table.Add(target, value);
    }

    public static TValue Get<TKey, TValue>(TKey target)
        where TKey : class
        where TValue : class
    {
        return TableHolder<TKey, TValue>.Table.TryGetValue(target, out var value) ? value : null;
    }

    // Generic storage
    private static class TableHolder<TKey, TValue> where TKey : class where TValue : class
    {
        public static readonly ConditionalWeakTable<TKey, TValue> Table = new();
    }
}