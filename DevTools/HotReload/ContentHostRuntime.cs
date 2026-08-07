using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using CUCoreLib.Data;
using CUCoreLib.Helpers;

namespace CUCoreLib.ContentReload
{
    internal static class ContentHostRuntime
    {
        private static readonly Dictionary<string, BaseUnityPlugin> PluginByGuid =
            new Dictionary<string, BaseUnityPlugin>(StringComparer.OrdinalIgnoreCase);

        internal static void RegisterPlugin(BaseUnityPlugin plugin)
        {
            if (plugin == null || plugin.Info == null || plugin.Info.Metadata == null) return;

            var guid = plugin.Info.Metadata.GUID;
            if (string.IsNullOrWhiteSpace(guid)) return;
            PluginByGuid[guid] = plugin;
        }

        internal static BaseUnityPlugin GetPlugin(string modGuid)
        {
            if (string.IsNullOrWhiteSpace(modGuid)) return null;

            return PluginByGuid.TryGetValue(modGuid.Trim(), out var plugin) ? plugin : null;
        }

        internal static object CreateHostInstance(Type hostType, string modGuid, out string reason)
        {
            reason = null;
            if (hostType == null)
            {
                reason = "Host type was null.";
                return null;
            }

            if (hostType.IsAbstract)
            {
                reason = "Host type '" + hostType.FullName + "' is abstract.";
                return null;
            }

            if (hostType.IsSealed && hostType.IsAbstract)
            {
                reason = "Host type '" + hostType.FullName + "' is static and cannot be instantiated.";
                return null;
            }

            var plugin = GetPlugin(modGuid);
            var parameterlessCtor = hostType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (parameterlessCtor != null)
            {
                var instance = parameterlessCtor.Invoke(null);
                AttachPluginIfNeeded(instance, plugin);
                return instance;
            }

            if (plugin != null)
            {
                var pluginCtor = hostType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(BaseUnityPlugin) },
                    null);
                if (pluginCtor != null)
                {
                    var instance = pluginCtor.Invoke(new object[] { plugin });
                    AttachPluginIfNeeded(instance, plugin);
                    return instance;
                }
            }

            if (typeof(CCLBase).IsAssignableFrom(hostType))
            {
                reason = "Host type '" + hostType.FullName +
                         "' must expose a parameterless constructor or a constructor taking BaseUnityPlugin.";
                return null;
            }

            reason = "Host type '" + hostType.FullName + "' must expose a parameterless constructor.";
            return null;
        }

        private static void AttachPluginIfNeeded(object instance, BaseUnityPlugin plugin)
        {
            if (!(instance is CCLBase host) || plugin == null) return;

            host.AttachPlugin(plugin);
        }
    }
}
