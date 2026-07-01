using System;
using System.Collections;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CUCoreLib.Util;
using UnityEngine;
using UnityEngine.Networking;

namespace CUCoreLib;

public class UpdateChecker : MonoBehaviour
{
    private const string ApiUrl = "https://api.github.com/repos/jimmyking9999999/CUCoreLib/releases/latest";
    private const string UserAgent = "CUCoreLib";

    private static ManualLogSource _logger;
    private static UpdateChecker _instance;
    private static bool _initialized;
    private static bool _hasChecked;
    private static string _currentVersion;
    private static ConsoleScript _cachedConsole;

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

        using var request = UnityWebRequest.Get(ApiUrl);
        request.SetRequestHeader("User-Agent", UserAgent);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Notify("CUCoreLib could not check for updates.");
            yield break;
        }

        var latestTag = TryExtractTagName(request.downloadHandler.text);
        if (string.IsNullOrWhiteSpace(latestTag))
        {
            Notify("CUCoreLib could not read the latest release version.");
            yield break;
        }

        if (IsNewer(_currentVersion, latestTag))
        {
            Notify($"CUCoreLib update available! {_currentVersion} -> {latestTag}", true);
            yield break;
        }

        Notify($"CUCoreLib is up to date! Current: {_currentVersion}, Latest: {latestTag}");
    }

    private static string TryExtractTagName(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        const string tagSearch = "\"tag_name\":\"";
        var startIndex = json.IndexOf(tagSearch, StringComparison.Ordinal);
        if (startIndex < 0) return null;

        startIndex += tagSearch.Length;
        var endIndex = json.IndexOf('"', startIndex);
        if (endIndex < 0 || endIndex <= startIndex) return null;

        return json.Substring(startIndex, endIndex - startIndex);
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

    private static void Notify(string message, bool warning = false)
    {
        if (warning)
            _logger?.LogWarning(message);
        else
            _logger?.LogInfo(message);

        if (_instance)
            _instance.StartCoroutine(NotifyRoutine(message, warning));
    }

    private static IEnumerator NotifyRoutine(string message, bool warning)
    {
        if (!_cachedConsole)
        {
            _cachedConsole = ConsoleScript.instance;
            if (!_cachedConsole)
                _cachedConsole = FindObjectOfType<ConsoleScript>();
        }

        if (!_cachedConsole) yield break;
        var consoleMessage = warning
            ? "<color=#FFA500>" + message + "</color>"
            : message;
        ConsoleUtils.LogToConsole(consoleMessage);
    }
}
