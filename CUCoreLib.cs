using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CUCoreLib.Bootstrap;
using CUCoreLib.BugReporting;
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
        public const string VERSION = "1.0.4";
        private const string BepInExCoreConfigFileName = "BepInEx.cfg";

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
            BepInExConfigurationGuard.EnsureManagerIsHidden(
                Path.Combine(Paths.ConfigPath, BepInExCoreConfigFileName), Logger);

            // Logger.LogInfo($"Starting up {MODNAME} v{VERSION}...");

            // Initialize Helpers
            AssetLoader.Initialize(Logger);
            FileLoader.Initialize(Logger);
            LocaleLoader.Initialize(Logger);
            LaunchOverrideManager.Initialize();
            DebugWatchService.Initialize();
            ContentReloadManager.Initialize();
            SaveRegistry.RegisterBuiltIns();
            LiquidTileRegistry.RegisterBuiltIns();
            MultiplayerApi.RegisterBuiltIns();
            BuiltInCommandRegistrar.Register();
            UpdateChecker.Initialize(Logger);

            // Patches
            var harmony = new Harmony(GUID);
            harmony.PatchAll();
            KrokMpCompatibilityPatches.Install(harmony);
            QoLUnknownCompatibilityPatches.Install(harmony);

            MultiplayerBridge.Initialize();
            MultiplayerSyncRegistry.ScheduleInitialSnapshot();

            Logger.LogInfo("CUCoreLib is ready to sit in the background.");
        }

    }
}
