using System;
using System.Collections.Generic;

namespace CUCoreLib.ContentReload
{
    [Flags]
    internal enum ContentReloadSurface
    {
        None = 0,
        Items = 1 << 0,
        Liquids = 1 << 1,
        Recipes = 1 << 2,
        Locale = 1 << 3,
        AllAllowed = Items | Liquids | Recipes | Locale
    }

    public sealed class ContentReloadResult
    {
        private readonly List<string> info = new List<string>();
        private readonly List<string> skipped = new List<string>();
        private readonly List<string> errors = new List<string>();
        private readonly List<string> recognizedMethods = new List<string>();

        public string ModGuid { get; internal set; }
        public string ModName { get; internal set; }
        public string SourcePath { get; internal set; }
        public string SourceHash { get; internal set; }
        public string UnsupportedReason { get; internal set; }

        public IReadOnlyList<string> Info => info;
        public IReadOnlyList<string> Skipped => skipped;
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> RecognizedMethods => recognizedMethods;
        public bool Succeeded => string.IsNullOrWhiteSpace(UnsupportedReason) && errors.Count == 0;

        internal void AddInfo(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                info.Add(message);
            }
        }

        internal void AddSkipped(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                skipped.Add(message);
            }
        }

        internal void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                errors.Add(message);
            }
        }

        internal void AddRecognizedMethod(string methodName)
        {
            if (!string.IsNullOrWhiteSpace(methodName) && !recognizedMethods.Contains(methodName))
            {
                recognizedMethods.Add(methodName);
            }
        }
    }

    internal sealed class ContentReloadConfig
    {
        public int PollIntervalSeconds { get; set; } = 2;
        public int DebounceMilliseconds { get; set; } = 1200;
        public Dictionary<string, ContentReloadModConfig> Mods { get; set; } =
            new Dictionary<string, ContentReloadModConfig>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class ContentReloadModConfig
    {
        public string OverrideDllPath { get; set; }
        public bool WatchEnabled { get; set; }
    }

    internal sealed class ContentReloadCandidate
    {
        public string ModGuid { get; set; }
        public string ModName { get; set; }
        public string LoadedPluginPath { get; set; }
        public string OverridePath { get; set; }
        public string SelectedPath { get; set; }
        public string SelectedHash { get; set; }
        public string SelectedSourceLabel { get; set; }
    }

    internal sealed class ContentCompatibilityReport
    {
        public string ModGuid { get; set; }
        public string ModName { get; set; }
        public string LoadedPluginPath { get; set; }
        public string OverridePath { get; set; }
        public string SelectedPath { get; set; }
        public string SelectedHash { get; set; }
        public string SelectedSourceLabel { get; set; }
        public string PluginTypeFullName { get; set; }
        public string UnsupportedReason { get; set; }
        public List<string> RecognizedMethods { get; } = new List<string>();
        public List<string> Notes { get; } = new List<string>();

        public bool IsSupported
        {
            get
            {
                return string.IsNullOrWhiteSpace(UnsupportedReason) &&
                    !string.IsNullOrWhiteSpace(PluginTypeFullName) &&
                    RecognizedMethods.Count > 0 &&
                    !string.IsNullOrWhiteSpace(SelectedPath);
            }
        }
    }

    internal sealed class ContentObservedFileState
    {
        public long Length { get; set; } = -1;
        public long LastWriteUtcTicks { get; set; } = -1;
        public string Hash { get; set; }
    }

    internal sealed class ContentReloadState
    {
        public string LastSuccessfulHash { get; set; }
        public string LastSuccessfulSourcePath { get; set; }
        public ContentReloadResult LastResult { get; set; }
        public ContentCompatibilityReport LastReport { get; set; }
        public string PendingHash { get; set; }
        public string PendingSourcePath { get; set; }
        public DateTime PendingSinceUtc { get; set; } = DateTime.MinValue;
        public Dictionary<string, ContentObservedFileState> ObservedFiles { get; } =
            new Dictionary<string, ContentObservedFileState>(StringComparer.OrdinalIgnoreCase);
    }
}
