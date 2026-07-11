using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using CUCoreLib.ContentReload;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using CUCoreLib.Patches;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib
{
    [BepInPlugin(GUID, MODNAME, VERSION)]
    [BepInDependency("KrokoshaCasualtiesMP", BepInDependency.DependencyFlags.SoftDependency)]
    public class CUCoreLibPlugin : BaseUnityPlugin
    {
        public const string GUID = "net.cucorelib";
        public const string MODNAME = "CUCoreLib";
        public const string VERSION = "1.0.3";
        private const string BepInExCoreConfigFileName = "BepInEx.cfg";
        private const string HideManagerGameObjectKey = "HideManagerGameObject";

        internal static ManualLogSource Log;
        // Alllright. Let's get this party rolling.


        public static CUCoreLibPlugin Instance { get; private set; }
        internal static ConfigFile SharedConfig { get; private set; }

        internal static ConfigFile GetOrCreateSharedConfig()
        {
            if (SharedConfig != null) return SharedConfig;

            SharedConfig = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, "CUCoreLib.cfg"), true);
            return SharedConfig;
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            SharedConfig = GetOrCreateSharedConfig();
            EnsureBepInExManagerIsHidden();

            // Logger.LogInfo($"Starting up {MODNAME} v{VERSION}...");

            // Initialize Helpers
            AssetLoader.Initialize(Logger);
            FileLoader.Initialize(Logger);
            LocaleLoader.Initialize(Logger);
            LaunchOverrideManager.Initialize();
            DebugWatchService.Initialize();
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

        private void EnsureBepInExManagerIsHidden()
        {
            var bepinExConfigPath = Path.Combine(Paths.ConfigPath, BepInExCoreConfigFileName);
            if (!File.Exists(bepinExConfigPath))
            {
                Logger.LogWarning($"Could not find BepInEx core config at '{bepinExConfigPath}'. CUCoreLib could not auto-enable {HideManagerGameObjectKey}.");
                return;
            }

            try
            {
                var bepinExConfig = new ConfigFile(bepinExConfigPath, true);
                var hideManagerEntry = bepinExConfig.Bind("Chainloader",
                    HideManagerGameObjectKey,
                    false,
                    "If enabled, hides BepInEx Manager GameObject from Unity.");

                if (hideManagerEntry.Value) return;

                hideManagerEntry.Value = true;
                bepinExConfig.Save();

                Logger.LogWarning(
                    $"Enabled BepInEx [Chainloader] {HideManagerGameObjectKey} = true in '{bepinExConfigPath}'. Restart the game so Unity stops destroying plugin game objects. If downloaded mods still do not load, confirm they are in BepInEx/plugins and then relaunch once more.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to auto-enable BepInEx [Chainloader] {HideManagerGameObjectKey}: {ex}");
            }
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
                    CUCoreUtils.ConsoleLog(ConsoleScript.instance, message);
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
                        CUCoreUtils.ConsoleLog(console, summary);
                        foreach (var line in loadedPlugins) CUCoreUtils.ConsoleLog(console, line);
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
                delegate(string[] args)
                {
                    DebugWatchConsoleCommands.Run(ConsoleScript.instance, args);
                }, null,
                ("action", "add, remove, list, clear, show, or hide."),
                ("Type.member", "Reflected static field to watch, such as Namespace.Plugin.healthRate."));
        }
    }
}
