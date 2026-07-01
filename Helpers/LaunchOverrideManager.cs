using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace CUCoreLib.Helpers;

internal static class LaunchOverrideManager
{
    private const string SectionName = "Launch Overrides";

    private static ConfigFile _configFile;
    private static ConfigEntry<bool> _launchInSandbox;
    private static ConfigEntry<bool> _launchInDebugWorld;
    private static bool _launchOverrideConsumed;
    private static bool _pendingSandboxCourse;

    internal static void Initialize()
    {
        if (_configFile != null) return;

        var configPath = Path.Combine(Paths.ConfigPath, "CUCoreLib.cfg");
        _configFile = new ConfigFile(configPath, true);
        _launchInSandbox = _configFile.Bind(
            SectionName,
            "launchInSandbox",
            false,
            "When true, the next game launch skips the menu and opens the tutorial sandbox course.");
        _launchInDebugWorld = _configFile.Bind(
            SectionName,
            "launchInDebugWorld",
            false,
            "When true, the next game launch skips the menu and starts a normal run with debugworld enabled.");
    }

    internal static bool TryConsumeMenuLaunchOverride(PreRunScript menu)
    {
        if (_launchOverrideConsumed || menu == null) return false;

        _launchOverrideConsumed = true;

        if (_launchInSandbox is { Value: true })
        {
            _pendingSandboxCourse = true;
            SaveSystem.loadedRun = false;
            PlayerPrefs.SetInt("tutorial", 1);
            PlayerPrefs.SetInt("radlinedisable", 0);
            WorldGeneration.runSettings = null;
            menu.StartCoroutine(menu.WaitLoad());
            CUCoreLibPlugin.Log?.LogInfo("CUCoreLib launch override: starting tutorial sandbox.");
            return true;
        }

        if (_launchInDebugWorld == null || !_launchInDebugWorld.Value) return false;
        var runSettings = new Dictionary<string, object>(RunSettings.GetPreset("normal").presetValues)
        {
            ["debugworld"] = true
        };

        SaveSystem.loadedRun = false;
        WorldGeneration.runSettings = runSettings;
        menu.StartCoroutine(menu.WaitLoad());
        CUCoreLibPlugin.Log?.LogInfo("CUCoreLib launch override: starting normal debug world.");
        return true;
    }

    internal static bool TryConsumePendingSandboxCourse(TutorialHandler tutorialHandler)
    {
        if (!_pendingSandboxCourse || tutorialHandler == null) return false;

        _pendingSandboxCourse = false;
        tutorialHandler.StartCourse(typeof(SandboxCourse));
        if (tutorialHandler.courseSelectScreen != null) tutorialHandler.courseSelectScreen.SetActive(false);

        CUCoreLibPlugin.Log?.LogInfo("CUCoreLib launch override: entered sandbox course.");
        return true;
    }
}