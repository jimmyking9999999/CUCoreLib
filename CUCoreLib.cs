using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using CUCoreLib.Patches;
using CUCoreLib.Registries;

namespace CUCoreLib
{
    [BepInPlugin(GUID, MODNAME, VERSION)]
    [BepInDependency("KrokoshaCasualtiesMP", BepInDependency.DependencyFlags.SoftDependency)]
    public class CUCoreLibPlugin : BaseUnityPlugin
    {
        public const string GUID = "net.cucorelib";
        public const string MODNAME = "CUCoreLib";
        public const string VERSION = "1.0.0";
        // Alllright. Let's get this party rolling.


        public static CUCoreLibPlugin Instance { get; private set; }
        internal static ManualLogSource Log;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // Logger.LogInfo($"Starting up {MODNAME} v{VERSION}...");

            // Initialize Helpers
            AssetLoader.Initialize(Logger);
            FileLoader.Initialize(Logger);
            LocaleLoader.Initialize(Logger);
            SaveRegistry.RegisterBuiltIns();
            MultiplayerApi.RegisterBuiltIns();
            RegisterBuiltInCommands();

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
            ConsoleCommandRegistry.Register("createLocale", "Writes or updates CUCoreLib generated locale data.",
            delegate (string[] args)
            {
                string path = args.Length > 1 ? args[1] : null;
                string writtenPath = LocaleRegistry.WriteLocaleFile(path);
                Log.LogInfo($"Locale file written to {writtenPath}");
            }, null, ("path", "Optional output path. Defaults to BepInEx/config/CUCoreLib/Locales/EN.json."));

            ConsoleCommandRegistry.Register("modlist", "Prints the loaded BepInEx plugin list to the in-game console and Unity log.",
            delegate (string[] args)
            {
                var loadedPlugins = Chainloader.PluginInfos.Values
                    .OrderBy(plugin => plugin.Metadata?.Name ?? plugin.Metadata?.GUID ?? string.Empty)
                    .Select(plugin =>
                    {
                        string name = plugin.Metadata?.Name ?? plugin.Metadata?.GUID ?? "Unknown Plugin";
                        string version = plugin.Metadata?.Version?.ToString() ?? "unknown";
                        string guid = plugin.Metadata?.GUID ?? "unknown.guid";
                        return $"{name} v{version} ({guid})";
                    })
                    .ToList();

                string summary = $"Loaded mods ({loadedPlugins.Count}):";
                Log.LogInfo(summary);
                foreach (string line in loadedPlugins)
                {
                    Log.LogInfo(line);
                }

                ConsoleScript console = ConsoleScript.instance;
                if (console != null)
                {
                    CUCoreUtils.ConsoleLog(console, summary);
                    foreach (string line in loadedPlugins)
                    {
                        CUCoreUtils.ConsoleLog(console, line);
                    }
                }
            });
        }
    }
}
