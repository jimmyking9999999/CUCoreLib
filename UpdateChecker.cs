using System;
using System.Collections;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CUCoreLib
{
    public class UpdateChecker : MonoBehaviour
    {
        private const string ApiUrl = "https://api.github.com/repos/jimmyking9999999/CUCoreLib/releases/latest";
        private const string UserAgent = "CUCoreLib";

        private static ManualLogSource _logger;
        private static UpdateChecker _instance;
        private static bool _initialized;
        private static bool _hasChecked;
        private static string _currentVersion;

        public static void Initialize(ManualLogSource logger)
        {
            if (_initialized) return;

            _initialized = true;
            _logger = logger;

            if (Chainloader.PluginInfos.TryGetValue(CUCoreLibPlugin.GUID, out var pluginInfo) &&
                pluginInfo?.Metadata?.Version != null)
                _currentVersion = "v" + pluginInfo.Metadata.Version;
            else
                _currentVersion = "v" + CUCoreLibPlugin.VERSION;

            var go = new GameObject("CUCoreLib_UpdateChecker");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<UpdateChecker>();
            _instance.StartCoroutine(CheckForUpdates());
        }

        private static IEnumerator CheckForUpdates()
        {
            if (_hasChecked) yield break;

            _hasChecked = true;

            using (var request = UnityWebRequest.Get(ApiUrl))
            {
                request.SetRequestHeader("User-Agent", UserAgent);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    yield return Notify(LocaleRegistry.GetFormatted("other", "updatechecker.no_connection"));
                    yield break;
                }

                var latestTag = TryExtractTagName(request.downloadHandler.text);
                if (string.IsNullOrWhiteSpace(latestTag))
                {
                    yield return Notify(LocaleRegistry.GetFormatted("other", "updatechecker.no_version"));
                    yield break;
                }

                if (IsNewer(_currentVersion, latestTag))
                {
                    yield return Notify(LocaleRegistry.GetFormatted("other", "updatechecker.update_available", _currentVersion, latestTag), true);
                    yield break;
                }

                yield return Notify(LocaleRegistry.GetFormatted("other", "updatechecker.up_to_date", _currentVersion, latestTag));
            }
        }

        private static string TryExtractTagName(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                var obj = JObject.Parse(json);
                return obj["tag_name"]?.Value<string>();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsNewer(string current, string latest)
        {
            var normalizedCurrent = NormalizeVersion(current);
            var normalizedLatest = NormalizeVersion(latest);

            if (Version.TryParse(normalizedCurrent, out var currentVersion) &&
                Version.TryParse(normalizedLatest, out var latestVersion))
                return latestVersion > currentVersion;

            return false;
        }

        private static string NormalizeVersion(string version)
        {
            return (version ?? string.Empty).Trim().TrimStart('v', 'V');
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private static IEnumerator Notify(string message, bool warning = false)
        {
            if (warning)
                _logger?.LogWarning(message);
            else
                _logger?.LogInfo(message);

            ConsoleScript console = null;
            var attempts = 0;

            while (!console && attempts < 50)
            {
                console = ConsoleScript.instance
                    ? ConsoleScript.instance
                    : FindObjectOfType<ConsoleScript>();

                if (!console)
                {
                    attempts++;
                    yield return new WaitForSecondsRealtime(0.2f);
                }
            }

            if (console)
            {
                var consoleMessage = warning
                    ? "<color=#FFA500>" + message + "</color>"
                    : message;
                CUCoreUtils.ConsoleLog(console, consoleMessage);
            }
        }
    }
}