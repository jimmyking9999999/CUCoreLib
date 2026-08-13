using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(Body), nameof(Body.TryLastStand))]
    internal static class PlayerEventPatches
    {
        internal static void NotifyHeal(Body player)
        {
            CUCoreUtils.RaiseOnHeal(player);
        }

        [HarmonyPrefix]
        private static void Prefix(Body __instance, out bool __state)
        {
            __state = __instance != null && __instance.succesfullyRolledLastStand;
        }

        [HarmonyPostfix]
        private static void Postfix(Body __instance, bool __state)
        {
            if (__state || __instance == null || !__instance.succesfullyRolledLastStand) return;

            if (!MultiplayerBridge.IsRunning && (PlayerCamera.main == null || PlayerCamera.main.body != __instance))
                return;

            CUCoreUtils.RaiseOnLastStand(__instance);
        }
    }
}
