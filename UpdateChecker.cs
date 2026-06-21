using System;
using System.Collections;
using BepInEx.Bootstrap;
using BepInEx.Logging;
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
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _logger = logger;

            if (Chainloader.PluginInfos.TryGetValue(CUCoreLibPlugin.GUID, out var pluginInfo) &&
                pluginInfo?.Metadata?.Version != null)
            {
                _currentVersion = "v" + pluginInfo.Metadata.Version;
            }
            else
            {
                _currentVersion = "v" + CUCoreLibPlugin.VERSION;
            }

            GameObject go = new GameObject("CUCoreLib_UpdateChecker");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<UpdateChecker>();
            _instance.StartCoroutine(_instance.CheckForUpdates());
        }

        private IEnumerator CheckForUpdates()
        {
            if (_hasChecked)
            {
                yield break;
            }

            _hasChecked = true;

            using (UnityWebRequest request = UnityWebRequest.Get(ApiUrl))
            {
                request.SetRequestHeader("User-Agent", UserAgent);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    yield return Notify("CUCoreLib could not check for updates.");
                    yield break;
                }

                string latestTag = TryExtractTagName(request.downloadHandler.text);
                if (string.IsNullOrWhiteSpace(latestTag))
                {
                    yield return Notify("CUCoreLib could not read the latest release version.");
                    yield break;
                }

                if (IsNewer(_currentVersion, latestTag))
                {
                    yield return Notify($"CUCoreLib update available! {_currentVersion} -> {latestTag}", warning: true);
                    yield break;
                }

                yield return Notify($"CUCoreLib is up to date! Current: {_currentVersion}, Latest: {latestTag}");
            }
        }

        private static string TryExtractTagName(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            const string tagSearch = "\"tag_name\":\"";
            int startIndex = json.IndexOf(tagSearch, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return null;
            }

            startIndex += tagSearch.Length;
            int endIndex = json.IndexOf('"', startIndex);
            if (endIndex < 0 || endIndex <= startIndex)
            {
                return null;
            }

            return json.Substring(startIndex, endIndex - startIndex);
        }

        private static bool IsNewer(string current, string latest)
        {
            string normalizedCurrent = NormalizeVersion(current);
            string normalizedLatest = NormalizeVersion(latest);

            if (Version.TryParse(normalizedCurrent, out Version currentVersion) &&
                Version.TryParse(normalizedLatest, out Version latestVersion))
            {
                return latestVersion > currentVersion;
            }

            return false;
        }

        private static string NormalizeVersion(string version)
        {
            return (version ?? string.Empty).Trim().TrimStart('v', 'V');
        }

        private IEnumerator Notify(string message, bool warning = false)
        {
            if (warning)
            {
                _logger?.LogWarning(message);
            }
            else
            {
                _logger?.LogInfo(message);
            }

            ConsoleScript console = null;
            int attempts = 0;

            while (console == null && attempts < 50)
            {
                console = ConsoleScript.instance != null
                    ? ConsoleScript.instance
                    : FindObjectOfType<ConsoleScript>();

                if (console == null)
                {
                    attempts++;
                    yield return new WaitForSecondsRealtime(0.2f);
                }
            }

            if (console != null)
            {
                string consoleMessage = warning
                    ? "<color=#FFA500>" + message + "</color>"
                    : message;
                Helpers.CUCoreUtils.ConsoleLog(console, consoleMessage);
            }
        }
    }
}
