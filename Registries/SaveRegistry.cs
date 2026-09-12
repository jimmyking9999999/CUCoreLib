using System;
using System.Collections.Generic;
using CUCoreLib.ContentReload;
using CUCoreLib.Saving;

namespace CUCoreLib.Registries
{
    public static class SaveRegistry
    {
        private static readonly Dictionary<string, ICustomSaveProvider> GlobalProviderMap =
            new Dictionary<string, ICustomSaveProvider>(StringComparer.Ordinal);

        private static readonly Dictionary<string, IItemSaveProvider> ItemProviderMap =
            new Dictionary<string, IItemSaveProvider>(StringComparer.Ordinal);

        private static readonly Dictionary<string, IBodySaveProvider> BodyProviderMap =
            new Dictionary<string, IBodySaveProvider>(StringComparer.Ordinal);

        private static readonly Dictionary<string, ILimbSaveProvider> LimbProviderMap =
            new Dictionary<string, ILimbSaveProvider>(StringComparer.Ordinal);

        private static readonly Dictionary<string, IWorldSaveProvider> WorldProviderMap =
            new Dictionary<string, IWorldSaveProvider>(StringComparer.Ordinal);

        private static bool _builtInsRegistered;

        internal static IReadOnlyDictionary<string, ICustomSaveProvider> GlobalProviders => GlobalProviderMap;
        internal static IReadOnlyDictionary<string, IItemSaveProvider> ItemProviders => ItemProviderMap;
        internal static IReadOnlyDictionary<string, IBodySaveProvider> BodyProviders => BodyProviderMap;
        internal static IReadOnlyDictionary<string, ILimbSaveProvider> LimbProviders => LimbProviderMap;
        internal static IReadOnlyDictionary<string, IWorldSaveProvider> WorldProviders => WorldProviderMap;

        internal static IEnumerable<string> GlobalProviderKeys => GlobalProviderMap.Keys;
        internal static IEnumerable<string> ItemProviderKeys => ItemProviderMap.Keys;
        internal static IEnumerable<string> BodyProviderKeys => BodyProviderMap.Keys;
        internal static IEnumerable<string> LimbProviderKeys => LimbProviderMap.Keys;
        internal static IEnumerable<string> WorldProviderKeys => WorldProviderMap.Keys;

        public static void RegisterGlobalProvider(string key, ICustomSaveProvider provider)
        {
            RegisterProvider(GlobalProviderMap, "global", key, provider);
        }

        public static void RegisterItemProvider(string key, IItemSaveProvider provider)
        {
            RegisterProvider(ItemProviderMap, "item", key, provider);
        }

        public static void RegisterBodyProvider(string key, IBodySaveProvider provider)
        {
            RegisterProvider(BodyProviderMap, "body", key, provider);
        }

        public static void RegisterLimbProvider(string key, ILimbSaveProvider provider)
        {
            RegisterProvider(LimbProviderMap, "limb", key, provider);
        }

        public static void RegisterWorldProvider(string key, IWorldSaveProvider provider)
        {
            RegisterProvider(WorldProviderMap, "world", key, provider);
        }

        internal static void RegisterBuiltIns()
        {
            if (_builtInsRegistered) return;

            _builtInsRegistered = true;
            RegisterItemProvider("cucorelib.itemRuntime", new BuiltInItemRuntimeSaveProvider());
            RegisterBodyProvider("cucorelib.bodyStatuses", new BuiltInBodyStatusSaveProvider());
            RegisterLimbProvider("cucorelib.limbStatuses", new BuiltInLimbStatusSaveProvider());
            RegisterWorldProvider("cucorelib.buildings", new BuiltInBuildingEntitySaveProvider());
        }

        private static void RegisterProvider<T>(Dictionary<string, T> map, string scope, string key, T provider)
            where T : class
        {
            var scopeLabel = string.IsNullOrWhiteSpace(scope)
                ? "Provider"
                : char.ToUpperInvariant(scope[0]) + scope.Substring(1) + "Provider";
            ContentReloadSession.AssertNotActive("SaveRegistry.Register" + scopeLabel + "()",
                "Save providers are excluded from strict content reload.");

            if (string.IsNullOrWhiteSpace(key))
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Save: Ignored " + scope +
                                                " save provider registration with no key.");
                return;
            }

            if (provider == null)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Save: Ignored " + scope + " save provider '" + key +
                                                "' because the provider was null.");
                return;
            }

            key = key.Trim();
            var replacing = map.ContainsKey(key);
            map[key] = provider;

            if (replacing)
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Save: Replaced existing " + scope + " save provider '" +
                                                key + "'.");
        }
    }
}