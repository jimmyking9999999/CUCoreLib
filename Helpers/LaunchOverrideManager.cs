using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using CUCoreLib.Networking;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace CUCoreLib.Helpers
{
    internal static class LaunchOverrideManager
    {
        private const string SectionName = "Launch Overrides";
        private const string QuickTestStateDirectoryName = "CUCoreLib";
        private const string QuickTestStateFileName = "multiplayerQuickTest.state";
        private const int QuickTestStateTtlSeconds = 180;
        private const string DefaultQuickTestAddress = "localhost:7790";
        private const string HostUserName = "Host";
        private const string ClientUserName = "Client";

        private static ConfigFile _configFile;
        private static ConfigEntry<bool> _launchInSandbox;
        private static ConfigEntry<bool> _launchInDebugWorld;
        private static ConfigEntry<bool> _multiplayerQuickTest;
        private static ConfigEntry<string> _multiplayerQuickTestAddress;
        private static bool _launchOverrideConsumed;
        private static bool _pendingSandboxCourse;
        private static bool _multiplayerQuickTestConsumed;
        private static string _quickTestStatePath;
        private static QuickTestRole _activeQuickTestRole;

        internal static void Initialize()
        {
            if (_configFile != null) return;

            _quickTestStatePath = Path.Combine(Paths.ConfigPath, QuickTestStateDirectoryName, QuickTestStateFileName);
            _configFile = CUCoreLibPlugin.GetOrCreateSharedConfig();
            _launchInSandbox = CUCoreLibPlugin.BindSharedConfig(
                SectionName,
                "launchInSandbox",
                false,
                "When true, the next game launch skips the menu and opens the tutorial sandbox course.");
            _launchInDebugWorld = CUCoreLibPlugin.BindSharedConfig(
                SectionName,
                "launchInDebugWorld",
                false,
                "When true, the next game launch skips the menu and starts a normal run with debugworld enabled.");
            _multiplayerQuickTest = CUCoreLibPlugin.BindSharedConfig(
                SectionName,
                "multiplayerQuickTest",
                false,
                "When true, the first local game instance becomes localhost Host and the second becomes localhost Client for quick KrokMP testing.");
            _multiplayerQuickTestAddress = CUCoreLibPlugin.BindSharedConfig(
                SectionName,
                "multiplayerQuickTestAddress",
                DefaultQuickTestAddress,
                "The localhost KrokMP address used by multiplayerQuickTest.");
        }

        internal static bool TryConsumeMenuLaunchOverride(PreRunScript menu)
        {
            if (_launchOverrideConsumed || menu == null) return false;

            _launchOverrideConsumed = true;

            if (TryConsumeMultiplayerQuickTest(menu)) return true;

            if (_launchInSandbox != null && _launchInSandbox.Value)
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

        private static bool TryConsumeMultiplayerQuickTest(PreRunScript menu)
        {
            if (_multiplayerQuickTestConsumed || _multiplayerQuickTest == null || !_multiplayerQuickTest.Value)
                return false;

            _multiplayerQuickTestConsumed = true;
            if (!MultiplayerBridge.IsAvailable)
            {
                CUCoreLibPlugin.Log?.LogInfo("CUCoreLib multiplayerQuickTest skipped because KrokMP is not available.");
                return false;
            }

            var role = ClaimQuickTestRole();
            if (role == QuickTestRole.None)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayerQuickTest could not determine a quick-test role.");
                return false;
            }

            var address = NormalizeQuickTestAddress(_multiplayerQuickTestAddress?.Value);
            var username = role == QuickTestRole.Host ? HostUserName : ClientUserName;
            if (!MultiplayerBridge.TryConfigureLocalIdentity(username, address))
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayerQuickTest failed to configure KrokMP localhost identity.");
                return false;
            }

            var started = role == QuickTestRole.Host
                ? MultiplayerBridge.TryStartLocalQuickTestHost(address)
                : MultiplayerBridge.TryStartLocalQuickTestClient(address);

            if (!started)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayerQuickTest failed to start localhost " + role + ".");
                return false;
            }

            _activeQuickTestRole = role;
            if (role == QuickTestRole.Host)
            {
                CUCoreUtils.StartCoroutine(HostQuickTestStartRoutine(menu));
                CUCoreLibPlugin.Log?.LogInfo("CUCoreLib multiplayerQuickTest: starting localhost host as 'Host'.");
                return true;
            }

            CUCoreUtils.StartCoroutine(ClientQuickTestStartRoutine());
            CUCoreLibPlugin.Log?.LogInfo("CUCoreLib multiplayerQuickTest: starting localhost client as 'Client'.");
            return false;
        }

        private static System.Collections.IEnumerator HostQuickTestStartRoutine(PreRunScript menu)
        {
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(3f);

            TrySkipWarningScreen();
            ScrollableText.ForceClose();

            var liveMenu = menu != null ? menu : UnityEngine.Object.FindObjectOfType<PreRunScript>();
            if (liveMenu == null)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayerQuickTest host start could not find PreRunScript.");
                yield break;
            }

            SaveSystem.loadedRun = false;
            WorldGeneration.runSettings = liveMenu.runSettings ??
                                          new Dictionary<string, object>(RunSettings.GetPreset("normal").presetValues);
            liveMenu.StartRun();

            CUCoreUtils.CallWhen(() => WorldGeneration.world != null, TryAnnounceGameStart, 0.1f);
        }

        private static System.Collections.IEnumerator ClientQuickTestStartRoutine()
        {
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(1f);
            TrySkipWarningScreen();
        }

        private static void TryAnnounceGameStart()
        {
            if (_activeQuickTestRole != QuickTestRole.Host) return;
            if (!MultiplayerBridge.IsServer) return;

            if (MultiplayerBridge.TryAnnounceGameStart())
                CUCoreLibPlugin.Log?.LogInfo("CUCoreLib multiplayerQuickTest: announced game start to localhost clients.");
        }

        private static void TrySkipWarningScreen()
        {
            var warning = GameObject.Find("Canvas/Warning");
            if (warning != null && warning.activeSelf) warning.SetActive(false);
        }

        private static string NormalizeQuickTestAddress(string address)
        {
            return string.IsNullOrWhiteSpace(address) ? DefaultQuickTestAddress : address.Trim();
        }

        private static QuickTestRole ClaimQuickTestRole()
        {
            try
            {
                var directory = Path.GetDirectoryName(_quickTestStatePath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

                var state = LoadQuickTestState().Normalize();
                var processId = Process.GetCurrentProcess().Id;
                var now = DateTime.UtcNow;

                if (state.HostProcessId == processId) return QuickTestRole.Host;
                if (state.ClientProcessId == processId) return QuickTestRole.Client;

                if (!state.HasLiveHost)
                {
                    state.HostProcessId = processId;
                    state.HostHeartbeatUtc = now;
                    state.ClientProcessId = 0;
                    state.ClientHeartbeatUtc = DateTime.MinValue;
                    SaveQuickTestState(state);
                    return QuickTestRole.Host;
                }

                state.ClientProcessId = processId;
                state.ClientHeartbeatUtc = now;
                SaveQuickTestState(state);
                return QuickTestRole.Client;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayerQuickTest failed to claim host/client role.\n" + ex);
                return QuickTestRole.None;
            }
        }

        private static QuickTestState LoadQuickTestState()
        {
            if (!File.Exists(_quickTestStatePath)) return new QuickTestState();

            try
            {
                var contents = File.ReadAllText(_quickTestStatePath, Encoding.UTF8);
                var parts = contents.Split('|');
                if (parts.Length < 4) return new QuickTestState();

                return new QuickTestState
                {
                    HostProcessId = ParseInt(parts[0]),
                    HostHeartbeatUtc = ParseUtc(parts[1]),
                    ClientProcessId = ParseInt(parts[2]),
                    ClientHeartbeatUtc = ParseUtc(parts[3])
                };
            }
            catch
            {
                return new QuickTestState();
            }
        }

        private static void SaveQuickTestState(QuickTestState state)
        {
            var normalized = state.Normalize();
            var serialized = string.Join("|",
                normalized.HostProcessId.ToString(CultureInfo.InvariantCulture),
                normalized.HostHeartbeatUtc.ToString("O", CultureInfo.InvariantCulture),
                normalized.ClientProcessId.ToString(CultureInfo.InvariantCulture),
                normalized.ClientHeartbeatUtc.ToString("O", CultureInfo.InvariantCulture));
            File.WriteAllText(_quickTestStatePath, serialized, Encoding.UTF8);
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        private static DateTime ParseUtc(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTime.MinValue;
        }

        private static bool IsProcessSlotAlive(int processId, DateTime heartbeatUtc)
        {
            if (processId <= 0) return false;
            if (heartbeatUtc == DateTime.MinValue) return false;
            if ((DateTime.UtcNow - heartbeatUtc).TotalSeconds > QuickTestStateTtlSeconds) return false;

            try
            {
                return !Process.GetProcessById(processId).HasExited;
            }
            catch
            {
                return false;
            }
        }

        private enum QuickTestRole
        {
            None,
            Host,
            Client
        }

        private sealed class QuickTestState
        {
            public int HostProcessId;
            public DateTime HostHeartbeatUtc;
            public int ClientProcessId;
            public DateTime ClientHeartbeatUtc;

            public bool HasLiveHost => IsProcessSlotAlive(HostProcessId, HostHeartbeatUtc);
            public bool HasLiveClient => IsProcessSlotAlive(ClientProcessId, ClientHeartbeatUtc);

            public QuickTestState Normalize()
            {
                if (!HasLiveHost)
                {
                    HostProcessId = 0;
                    HostHeartbeatUtc = DateTime.MinValue;
                }

                if (!HasLiveClient)
                {
                    ClientProcessId = 0;
                    ClientHeartbeatUtc = DateTime.MinValue;
                }

                return this;
            }
        }
    }
}
