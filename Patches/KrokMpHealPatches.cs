using System.Reflection;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Patches
{
    internal static class KrokMpHealPatches // Probably not needed, might rename this to general MP references for certain actions later down
    // I wouldn't patch these functions with harmony if you were thinking about that, thanks ^^
    {
        private const string BodyExtensionsTypeName = "CasualtiesTogetherUtils.Util_BodyExtensions";
        private const string HealChannel = "cucorelib.player.heal";

        private static bool _installed;
        private static bool _retryScheduled;
        private static bool _healCommandWindow;
        private static bool _handlerRegistered;

        internal static bool IsInstalled => _installed;

        internal static void Install(Harmony harmony)
        {
            if (harmony == null) return;

            RegisterHealHandler();

            if (_installed) return;

            var resetHealth = ResolveResetHealth();
            if (resetHealth == null)
            {
                ScheduleRetry(harmony);
                return;
            }

            harmony.Patch(resetHealth,
                postfix: new HarmonyMethod(typeof(KrokMpHealPatches), nameof(ResetHealthPostfix)));
            _installed = true;
        }

        internal static void OpenHealWindow(bool isHealCommand)
        {
            _healCommandWindow = isHealCommand;
        }

        internal static void CloseHealWindow()
        {
            _healCommandWindow = false;
        }

        private static void ResetHealthPostfix(Body __0)
        {
            // Outside a running session, the vanilla command wrapper already reports the healed player.
            if (!_healCommandWindow || !MultiplayerBridge.IsRunning || __0 == null) return;
            if (MultiplayerBridge.IsServer &&
                MultiplayerApi.TryGetClientIdFromBody(__0, out var clientId))
            {
                MultiplayerApi.BroadcastEverywhere(HealChannel, new JObject { ["clientId"] = clientId });
                return;
            }

            PlayerEventPatches.NotifyHeal(__0);
        }
        
        private static void RegisterHealHandler()
        {
            if (_handlerRegistered) return;

            _handlerRegistered = true;
            MultiplayerApi.RegisterClientHandler(HealChannel, payload =>
            {
                var clientId = payload?.Value<uint?>("clientId");
                if (clientId == null) return;

                if (MultiplayerApi.TryGetBodyFromClientId(clientId.Value, out var body) && body != null)
                    PlayerEventPatches.NotifyHeal(body);
            });
        }

        private static MethodInfo ResolveResetHealth()
        {
            var type = MultiplayerApi.ResolveKrokMpType(BodyExtensionsTypeName);
            if (type == null) return null;

            var method = AccessTools.Method(type, "ResetHealth", new[] { typeof(Body), typeof(bool) });
            return method != null && method.IsStatic ? method : null;
        }

        private static void ScheduleRetry(Harmony harmony)
        {
            if (_retryScheduled || MultiplayerApi.KrokMpVersion == null) return;

            _retryScheduled = true;
            CUCoreUtils.DelayCall(1f, () =>
            {
                _retryScheduled = false;
                if (_installed) return;

                Install(harmony);
            });
        }
    }
}
