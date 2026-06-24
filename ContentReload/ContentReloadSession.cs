using System;
using System.IO;
using System.Reflection;

namespace CUCoreLib.ContentReload
{
    internal static class ContentReloadSession
    {
        [ThreadStatic]
        private static SessionState current;

        internal static bool IsActive => current != null;

        internal static Assembly GetSourceAssemblyOverride()
        {
            return current != null ? current.SourceAssembly : null;
        }

        internal static string GetPluginDirectoryOverride()
        {
            if (current == null || string.IsNullOrWhiteSpace(current.SourceDllPath))
            {
                return null;
            }

            return Path.GetDirectoryName(current.SourceDllPath);
        }

        internal static IDisposable Begin(string modGuid, Assembly sourceAssembly, string sourceDllPath, ContentReloadSurface allowedSurfaces)
        {
            SessionState previous = current;
            current = new SessionState
            {
                ModGuid = modGuid,
                SourceAssembly = sourceAssembly,
                SourceDllPath = sourceDllPath,
                AllowedSurfaces = allowedSurfaces
            };

            return new Scope(previous);
        }

        internal static void AssertAllowed(ContentReloadSurface surface, string apiName, string guidance = null)
        {
            if (current == null)
            {
                return;
            }

            if ((current.AllowedSurfaces & surface) != 0)
            {
                return;
            }

            throw new InvalidOperationException(BuildDisallowedMessage(apiName, guidance));
        }

        internal static void AssertNotActive(string apiName, string guidance = null)
        {
            if (current == null)
            {
                return;
            }

            throw new InvalidOperationException(BuildDisallowedMessage(apiName, guidance));
        }

        private static string BuildDisallowedMessage(string apiName, string guidance)
        {
            string message = "Strict content reload for '" + current.ModGuid + "' does not allow " + apiName +
                ". Only item, liquid, recipe, and locale/text registration are supported.";

            if (!string.IsNullOrWhiteSpace(guidance))
            {
                message += " " + guidance;
            }

            return message;
        }

        private sealed class SessionState
        {
            public string ModGuid;
            public Assembly SourceAssembly;
            public string SourceDllPath;
            public ContentReloadSurface AllowedSurfaces;
        }

        private sealed class Scope : IDisposable
        {
            private readonly SessionState previous;

            public Scope(SessionState previousState)
            {
                previous = previousState;
            }

            public void Dispose()
            {
                current = previous;
            }
        }
    }
}
