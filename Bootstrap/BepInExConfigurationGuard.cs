using System;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace CUCoreLib.Bootstrap
{
    internal static class BepInExConfigurationGuard
    {
        private const string HideManagerGameObjectKey = "HideManagerGameObject";

        internal static void EnsureManagerIsHidden(string configPath, ManualLogSource logger)
        {
            if (!File.Exists(configPath)) return;

            try
            {
                var wasEnabled = false;
                CUCoreLibPlugin.RunWithConfigFileLock(() =>
                {
                    var config = new ConfigFile(configPath, true)
                    {
                        SaveOnConfigSet = false
                    };
                    var entry = config.Bind("Chainloader", HideManagerGameObjectKey, false,
                        "If enabled, hides BepInEx Manager GameObject from Unity.");
                    if (entry.Value) return;

                    entry.Value = true;
                    config.Save();
                    wasEnabled = true;
                });
                if (!wasEnabled) return;
            }
            catch (Exception ex)
            {
                // ignored
            }
        }
    }
}