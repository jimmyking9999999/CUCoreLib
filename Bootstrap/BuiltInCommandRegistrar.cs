using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using CUCoreLib.BugReporting;
using CUCoreLib.ContentReload;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;

namespace CUCoreLib.Bootstrap
{
    internal static class BuiltInCommandRegistrar
    {
        internal static void Register()
        {
            ConsoleCommandRegistry.Register("createLocale",
                "Writes or updates CUCoreLib generated locale data. WARNING: Overrides generated keys in the target file",
                delegate(string[] args)
                {
                    string modGuid = null;
                    string path = null;

                    if (args.Length > 1)
                    {
                        if (LooksLikeLocalePath(args[1])) path = args[1];
                        else modGuid = args[1];
                    }

                    if (args.Length > 2) path = args[2];

                    var writtenPath = LocaleRegistry.WriteLocaleFile(path, modGuid);
                    var message = !string.IsNullOrWhiteSpace(writtenPath)
                        ? $"created locale at {writtenPath}"
                        : !string.IsNullOrWhiteSpace(modGuid)
                            ? $"no locale entries were registered by '{modGuid.Trim()}'! Nothing was written :("
                            : "no locale entries were registered in your mods. Nothing was written as such :(";
                    CUCoreLibPlugin.Log.LogInfo(message);
                    CUCoreUtils.ConsoleLog(ConsoleScript.instance, message);
                },
                new Dictionary<int, List<string>>
                {
                    [0] = ContentReloadManager.GetLoadedModGuids().ToList()
                },
                ("modGuid",
                    "Optional (you should really have it though) BepInEx plugin GUID. When given, writes only that mod's entries to EN-<modGuid>.json."),
                ("path",
                    "Optional output file or folder. Defaults to BepInEx/config/CUCoreLib/Locales/EN-<modGuid>.json"));

            ConsoleCommandRegistry.Register("modlist",
                "Prints the loaded BepInEx plugin list to the in-game console and Unity log.",
                delegate
                {
                    var loadedPlugins = Chainloader.PluginInfos.Values
                        .OrderBy(plugin => plugin.Metadata?.Name ?? plugin.Metadata?.GUID ?? string.Empty)
                        .Select(plugin =>
                        {
                            var name = plugin.Metadata?.Name ?? plugin.Metadata?.GUID ?? "Unknown Plugin";
                            var version = plugin.Metadata?.Version?.ToString() ?? "unknown";
                            var guid = plugin.Metadata?.GUID ?? "unknown.guid";
                            return $"  {name} v{version} ({guid})";
                        }).ToList();

                    var summary = $"Loaded mods ({loadedPlugins.Count}):";
                    CUCoreLibPlugin.Log.LogInfo(summary);
                    foreach (var line in loadedPlugins) CUCoreLibPlugin.Log.LogInfo(line);

                    var console = ConsoleScript.instance;
                    if (console == null) return;
                    CUCoreUtils.ConsoleLog(console, summary);
                    foreach (var line in loadedPlugins) CUCoreUtils.ConsoleLog(console, line);
                });

            ConsoleCommandRegistry.Register("bug-report",
                "Sends a diagnostic bug report with debug logs. Thanks!",
                BugReportService.RunCommand,
                new Dictionary<int, List<string>>
                {
                    [2] = new List<string> { "low", "medium", "high", "critical" } 
                },
                ("description", "Optional description in quotation marks \"\". Optional, but highly recommended."),
                ("bool screenshot", "True/false. Captures the current game screen if true. Optional."),
                ("severity", "low/medium/high/critical. Optional."));

            ConsoleCommandRegistry.Register("reloadcontent",
                "Strictly reloads item/liquid/recipe/locale content from a rebuilt mod DLL.",
                delegate(string[] args)
                {
                    if (args.Length < 2) throw new Exception("Usage: reloadcontent [modGuid]");
                    var result = ContentReloadManager.Reload(args[1]);
                    ContentReloadManager.WriteReloadSummaryToConsole(ConsoleScript.instance, result);
                }, new Dictionary<int, List<string>>
                {
                    [0] = ContentReloadManager.GetLoadedModGuids().ToList()
                }, ("modGuid", "BepInEx plugin GUID to strictly reload from a rebuilt DLL."));

            ConsoleCommandRegistry.Register("autohotreload",
                "Enables automatic hot reloading after detecting a loaded mod DLL file change.",
                delegate(string[] args)
                {
                    if (args.Length < 3) throw new Exception("Usage: autohotreload [modGuid] [enable]");
                    if (!bool.TryParse(args[2], out var enabled))
                        throw new Exception("Enable must be 'true' or 'false'.");

                    var success = ContentReloadManager.ConfigureAutoHotRefresh(args[1], enabled, out var message);
                    if (!success) throw new Exception(message);
                    CUCoreUtils.ConsoleLog(ConsoleScript.instance, message);
                }, null,
                ("modGuid", "BepInEx plugin GUID that previously called ContentReloadManager.EnableHotReload(GUID)."),
                ("enable", "true to enable watch mode for that DLL, false to disable it."));

            ConsoleCommandRegistry.Register("debugwatch",
                "Manages a live top-right overlay of watched static fields for runtime debugging.",
                delegate(string[] args) { DebugWatchConsoleCommands.Run(ConsoleScript.instance, args); }, null,
                ("action", "add, remove, list, clear, show, or hide."),
                ("Type.member", "Reflected static field to watch, such as Namespace.Plugin.healthRate."));
        }

        private static bool LooksLikeLocalePath(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return false;

            var trimmed = arg.Trim();
            return trimmed.IndexOf('/') >= 0 || trimmed.IndexOf('\\') >= 0 ||
                   trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
