using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using CUCoreLib.Helpers;

namespace CUCoreLib.ContentReload
{
    internal static class ContentReloadSession
    {
        [ThreadStatic] private static SessionState current;
        [ThreadStatic] private static HashSet<string> warningKeys;

        internal static bool IsActive => current != null;

        internal static Assembly GetSourceAssemblyOverride()
        {
            return current?.SourceAssembly;
        }

        internal static string ResolveAmbientOwnerId()
        {
            if (current != null && !string.IsNullOrWhiteSpace(current.ModGuid)) return current.ModGuid;

            try
            {
                var trace = new StackTrace();
                foreach (var frame in trace.GetFrames() ?? Array.Empty<StackFrame>())
                {
                    var method = frame.GetMethod();
                    var declaringType = method != null ? method.DeclaringType : null;
                    var assembly = declaringType?.Assembly;
                    if (assembly == null || assembly == typeof(CUCoreLibPlugin).Assembly) continue;

                    string location;
                    try
                    {
                        location = assembly.Location;
                    }
                    catch
                    {
                        location = null;
                    }

                    if (string.IsNullOrWhiteSpace(location)) continue;

                    foreach (var pluginInfo in Chainloader.PluginInfos.Values
                                 .Where(pluginInfo => pluginInfo?.Metadata != null)
                                 .Where(pluginInfo => string.Equals(pluginInfo.Location, location, StringComparison.OrdinalIgnoreCase)))
                    {
                        return pluginInfo.Metadata.GUID;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        internal static string GetPluginDirectoryOverride()
        {
            if (current == null || string.IsNullOrWhiteSpace(current.SourceDllPath)) return null;

            return Path.GetDirectoryName(current.SourceDllPath);
        }

        internal static HotReloadMode CurrentMode => current?.Mode ?? HotReloadMode.Strict;

        internal static IDisposable Begin(string modGuid, Assembly sourceAssembly, string sourceDllPath,
            ContentReloadSurface allowedSurfaces, HotReloadMode mode)
        {
            var previous = current;
            var previousWarnings = warningKeys;
            current = new SessionState
            {
                ModGuid = modGuid,
                SourceAssembly = sourceAssembly,
                SourceDllPath = sourceDllPath,
                AllowedSurfaces = allowedSurfaces,
                Mode = mode
            };
            warningKeys = new HashSet<string>(StringComparer.Ordinal);

            return new Scope(previous, previousWarnings);
        }

        internal static void AssertAllowed(ContentReloadSurface surface, string apiName, string guidance = null)
        {
            if (current == null) return;

            if ((current.AllowedSurfaces & surface) != 0) return;

            if (CurrentMode == HotReloadMode.FlexibleGuarded)
            {
                WarnBlocked(apiName, guidance);
                return;
            }

            throw new InvalidOperationException(BuildDisallowedMessage(apiName, guidance));
        }

        internal static void AssertNotActive(string apiName, string guidance = null)
        {
            if (current == null) return;

            if (CurrentMode == HotReloadMode.FlexibleGuarded)
            {
                WarnBlocked(apiName, guidance);
                return;
            }

            throw new InvalidOperationException(BuildDisallowedMessage(apiName, guidance));
        }

        private static void WarnBlocked(string apiName, string guidance)
        {
            if (current == null || string.IsNullOrWhiteSpace(apiName)) return;

            var trace = new StackTrace();
            var replayRoot = "<unknown>";
            foreach (var frame in trace.GetFrames() ?? Array.Empty<StackFrame>())
            {
                var method = frame.GetMethod();
                var declaringType = method?.DeclaringType;
                if (declaringType == null) continue;
                if (declaringType.Assembly == typeof(CUCoreLibPlugin).Assembly) continue;

                replayRoot = declaringType.FullName + "." + method.Name;
                break;
            }

            var warningKey = replayRoot + "|" + apiName;
            if (warningKeys != null && !warningKeys.Add(warningKey)) return;

            var message = "Blocked during flexible hot reload: " + replayRoot + " called " + apiName + ".";
            if (!string.IsNullOrWhiteSpace(guidance)) message += " " + guidance;

            CUCoreLibPlugin.Log?.LogWarning(message);
            if (ConsoleScript.instance != null) CUCoreUtils.ConsoleLog(ConsoleScript.instance, message);
        }

        private static string BuildDisallowedMessage(string apiName, string guidance)
        {
            var message = "Strict content reload for '" + current.ModGuid + "' does not allow " + apiName +
                          ". Only item, liquid, recipe, locale/text, and basic building registration are supported.";

            if (!string.IsNullOrWhiteSpace(guidance)) message += " " + guidance;

            return message;
        }

        private sealed class SessionState
        {
            public ContentReloadSurface AllowedSurfaces;
            public string ModGuid;
            public HotReloadMode Mode;
            public Assembly SourceAssembly;
            public string SourceDllPath;
        }

        private sealed class Scope : IDisposable
        {
            private readonly SessionState previous;
            private readonly HashSet<string> previousWarnings;

            public Scope(SessionState previousState, HashSet<string> previousWarningState)
            {
                previous = previousState;
                previousWarnings = previousWarningState;
            }

            public void Dispose()
            {
                current = previous;
                warningKeys = previousWarnings;
            }
        }
    }
}
