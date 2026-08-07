using System;
using System.Collections.Generic;
using CUCoreLib.Data;

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
        Buildings = 1 << 4,
        AllAllowed = Items | Liquids | Recipes | Locale | Buildings
    }

    internal enum ContentReloadEntryStage
    {
        LoadAssets = 0,
        RegisterText = 100,
        RegisterLocale = 200,
        RegisterLiquids = 300,
        RegisterItems = 400,
        RegisterBuildings = 450,
        RegisterRecipes = 500
    }

    public enum HotReloadMode
    {
        FlexibleGuarded = 0,
        Strict = 1
    }

    public sealed class HotReloadOptions
    {
        public HotReloadMode Mode { get; set; } = HotReloadMode.FlexibleGuarded;
    }

    public sealed class ContentReloadResult
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> info = new List<string>();
        private readonly List<string> recognizedMethods = new List<string>();
        private readonly List<string> ranMethods = new List<string>();
        private readonly List<string> skipped = new List<string>();

        public string ModGuid { get; internal set; }
        public string ModName { get; internal set; }
        public string SourcePath { get; internal set; }
        public string SourceHash { get; internal set; }
        public string UnsupportedReason { get; internal set; }

        public IReadOnlyList<string> Info => info;
        public IReadOnlyList<string> Skipped => skipped;
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> RecognizedMethods => recognizedMethods;
        public IReadOnlyList<string> RanMethods => ranMethods;
        public bool Succeeded => string.IsNullOrWhiteSpace(UnsupportedReason) && errors.Count == 0;

        internal void AddInfo(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) info.Add(message);
        }

        internal void AddSkipped(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) skipped.Add(message);
        }

        internal void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) errors.Add(message);
        }

        internal void AddRecognizedMethod(string methodName)
        {
            if (!string.IsNullOrWhiteSpace(methodName) && !recognizedMethods.Contains(methodName))
                recognizedMethods.Add(methodName);
        }

        internal void AddRanMethod(string methodName)
        {
            if (!string.IsNullOrWhiteSpace(methodName) && !ranMethods.Contains(methodName))
                ranMethods.Add(methodName);
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
        public bool WatchEnabled { get; set; }
        public string OverridePath { get; set; }
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
        public bool UsesEnableHotReloadContract { get; set; }
        public List<DiscoveredReloadMethod> Methods { get; } = new List<DiscoveredReloadMethod>();
        public List<SkippedReloadMethod> SkippedMethods { get; } = new List<SkippedReloadMethod>();
        public List<string> Notes { get; } = new List<string>();
        public List<string> RecognizedMethods { get; } = new List<string>();

        public bool IsSupported =>
            string.IsNullOrWhiteSpace(UnsupportedReason) &&
            !string.IsNullOrWhiteSpace(PluginTypeFullName) &&
            Methods.Count > 0 &&
            !string.IsNullOrWhiteSpace(SelectedPath);
    }

    internal sealed class DiscoveredReloadMethod
    {
        public string DisplayName { get; set; }
        public string DeclaringTypeFullName { get; set; }
        public string MethodName { get; set; }
        public bool IsStatic { get; set; }
        public bool IsPluginMethod { get; set; }
        public ContentReloadEntryStage Stage { get; set; }
        public int Order { get; set; }
        public int DiscoveryIndex { get; set; }
    }

    internal sealed class SkippedReloadMethod
    {
        public string DisplayName { get; set; }
        public string Reason { get; set; }
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
        public HotReloadMode Mode { get; set; } = HotReloadMode.FlexibleGuarded;
        public string PendingHash { get; set; }
        public string PendingSourcePath { get; set; }
        public DateTime PendingSinceUtc { get; set; } = DateTime.MinValue;

        public Dictionary<string, ContentObservedFileState> ObservedFiles { get; } =
            new Dictionary<string, ContentObservedFileState>(StringComparer.OrdinalIgnoreCase);
    }
}
