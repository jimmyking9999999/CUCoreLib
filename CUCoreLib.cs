using System;
using System.IO;
using System.Threading;
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
        private const string ConfigFileMutexName = @"Local\CUCoreLib.ConfigFile";
        private static readonly Mutex ConfigFileMutex = new Mutex(false, ConfigFileMutexName);

        internal static ManualLogSource Log;
        // Alllright. Let's get this party rolling.


        public static CUCoreLibPlugin Instance { get; private set; }
        internal static ConfigFile SharedConfig { get; private set; }

        internal static ConfigFile GetOrCreateSharedConfig()
        {
            if (SharedConfig != null) return SharedConfig;

            var lockTaken = EnterSharedConfigMutex();
            if (!lockTaken)
                throw new IOException("CUCoreLib config is busy.");

            try
            {
                if (SharedConfig != null) return SharedConfig;

                SharedConfig = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, "CUCoreLib.cfg"), false)
                {
                    SaveOnConfigSet = false
                };
                return SharedConfig;
            }
            finally
            {
                ConfigFileMutex.ReleaseMutex();
            }
        }

        internal static ConfigEntry<T> BindSharedConfig<T>(string section, string key, T defaultValue,
            string description)
        {
            var config = GetOrCreateSharedConfig();
            var lockTaken = EnterSharedConfigMutex();
            if (!lockTaken)
            {
                Log?.LogWarning("CUCoreLib config is busy; '" + section + "." + key + "' was not saved.");
                return config.Bind(section, key, defaultValue, description);
            }

            try
            {
                if (File.Exists(config.ConfigFilePath)) config.Reload();
                var entry = config.Bind(section, key, defaultValue, description);
                config.Save();
                return entry;
            }
            catch (IOException ex)
            {
                Log?.LogWarning("CUCoreLib could not save '" + section + "." + key + "': " + ex.Message);
                return config.Bind(section, key, defaultValue, description);
            }
            finally
            {
                ConfigFileMutex.ReleaseMutex();
            }
        }

        internal static void SetSharedConfigValue<T>(ConfigEntry<T> entry, T value)
        {
            if (entry == null) return;

            var lockTaken = EnterSharedConfigMutex();
            if (!lockTaken)
            {
                Log?.LogWarning("CUCoreLib config is busy; its latest changes were not saved.");
                return;
            }

            try
            {
                var config = GetOrCreateSharedConfig();
                if (File.Exists(config.ConfigFilePath)) config.Reload();
                entry.Value = value;
                config.Save();
            }
            catch (IOException ex)
            {
                Log?.LogWarning("CUCoreLib could not save its config: " + ex.Message);
            }
            finally
            {
                ConfigFileMutex.ReleaseMutex();
            }
        }

        private static bool EnterSharedConfigMutex()
        {
            try
            {
                return ConfigFileMutex.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
        }

        internal static void RunWithConfigFileLock(Action action)
        {
            var lockTaken = EnterSharedConfigMutex();
            if (!lockTaken)
                throw new IOException("CUCoreLib config is busy.");

            try
            {
                action();
            }
            finally
            {
                ConfigFileMutex.ReleaseMutex();
            }
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
