using System;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace CUCoreLib.Bootstrap
{
    internal static class BepInExConfigurationGuard
    {
        private const string HideManagerGameObjectKey = "HideManagerGameObject";
        private const string HideManagerNoticeGivenKey = "HideManagerGameObjectNoticeGiven";
        private static bool _noticeHandled;

        internal static void EnsureManagerIsHidden(string configPath, ManualLogSource logger)
        {
            var noticeGiven = CUCoreLibPlugin.BindSharedConfig(
                "Notices",
                HideManagerNoticeGivenKey,
                false,
                "Marks that the HideManagerGameObject startup notice has already been shown.");

            if (_noticeHandled || noticeGiven.Value)
            {
                _noticeHandled = true;
                return;
            }

            if (!File.Exists(configPath))
            {
                logger.LogWarning($"Could not find BepInEx core config at '{configPath}'. CUCoreLib could not auto-enable {HideManagerGameObjectKey}.");
                SuppressNotice(noticeGiven);
                return;
            }

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

                logger.LogWarning(
                    $"Enabled BepInEx [Chainloader] {HideManagerGameObjectKey} = true in '{configPath}'. Restart the game so Unity stops destroying plugin game objects. If downloaded mods still do not load, confirm they are in BepInEx/plugins and then relaunch once more.");
                SuppressNotice(noticeGiven);
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Failed to auto-enable BepInEx [Chainloader] {HideManagerGameObjectKey}: {ex}");
                SuppressNotice(noticeGiven);
            }
        }

        private static void SuppressNotice(ConfigEntry<bool> noticeGiven)
        {
            if (noticeGiven.Value) return;

            try
            {
                CUCoreLibPlugin.SetSharedConfigValue(noticeGiven, true);
            }
            catch
            {
                // Ignore any failures while suppressing notice state.
            }

            _noticeHandled = true;
        }
    }
}
