using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CUCoreLib.ContentReload;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using CUCoreLib.Patches;
using CUCoreLib.Registries;
using CUCoreLib.Util;
using HarmonyLib;

namespace CUCoreLib;

[BepInPlugin(GUID, MODNAME, VERSION)]
[BepInDependency("KrokoshaCasualtiesMP", BepInDependency.DependencyFlags.SoftDependency)]
public class CUCoreLibPlugin : BaseUnityPlugin
{
    public const string GUID = "net.cucorelib";
    public const string MODNAME = "CUCoreLib";
    public const string VERSION = "1.0.1";

    internal static ManualLogSource Log;
    // All right. Let's get this party rolling.


    public static CUCoreLibPlugin Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        // Logger.LogInfo($"Starting up {MODNAME} v{VERSION}...");

        // Initialize Helpers
        AssetLoader.Initialize(Logger);
        FileLoader.Initialize(Logger);
        LocaleLoader.Initialize(Logger);
        LaunchOverrideManager.Initialize();
        ContentReloadManager.Initialize();
        SaveRegistry.RegisterBuiltIns();
        MultiplayerApi.RegisterBuiltIns();
        RegisterBuiltInCommands();
        UpdateChecker.Initialize(Logger);

        // Patches
        var harmony = new Harmony(GUID);
        harmony.PatchAll();
        KrokMpCompatibilityPatches.Install(harmony);

        MultiplayerBridge.Initialize();
        MultiplayerSyncRegistry.ScheduleInitialSnapshot();

        Logger.LogInfo("CUCoreLib is ready to sit in the background.");
    }

    private static void RegisterBuiltInCommands() // SetTile also added, but not here
    {
        ConsoleCommandRegistry.Register("createLocale",
            "Writes or updates CUCoreLib generated locale data. WARNING: Overrides EN.json",
            delegate(string[] args)
            {
                var path = args.Length > 1 ? args[1] : null;
                var writtenPath = LocaleRegistry.WriteLocaleFile(path);
                var message = $"created locale at {writtenPath}";
                Log.LogInfo(message);
                ConsoleUtils.LogToConsole(message);
            }, null, ("path", "Optional output path. Defaults to BepInEx/config/CUCoreLib/Locales/EN.json."));

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
                    })
                    .ToList();

                var summary = $"Loaded mods ({loadedPlugins.Count}):";
                Log.LogInfo(summary);
                foreach (var line in loadedPlugins) Log.LogInfo(line);

                var console = ConsoleScript.instance;
                if (console == null) return;
                {
                    ConsoleUtils.LogToConsole(summary);
                    foreach (var line in loadedPlugins) ConsoleUtils.LogToConsole(line);
                }
            });

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
            "Enables automatic hot reloading after detecting a .dll file change.",
            delegate(string[] args)
            {
                if (args.Length < 3) throw new Exception("Usage: autohotreload [pathToDllFile] [enable]");

                var dllPath = args[1];
                if (!bool.TryParse(args[2], out var enabled))
                    throw new Exception("Enable must be 'true' or 'false'.");

                var success = ContentReloadManager.ConfigureAutoHotRefresh(dllPath, enabled, out var message);
                if (!success) throw new Exception(message);

                ConsoleUtils.LogToConsole(message);
            }, null,
            ("pathToDllFile", "Path to the rebuilt DLL that should be watched."),
            ("enable", "true to enable watch mode for that DLL, false to disable it."));
    }
}