using System;
using CUCoreLib.Saving;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(SaveSystem))]
    [HarmonyAfter("CasualtiesMP")]
    internal static class SaveSystemPatches
    {
        [HarmonyPatch("SaveGame")]
        [HarmonyPostfix]
        private static void SaveGame_Postfix()
        {
            SaveCoordinator.EmbedIntoSaveFile();
        }

        [HarmonyPatch("TryLoadGame")]
        [HarmonyPrefix]
        private static void TryLoadGame_Prefix(out SaveCoordinator.LoadState __state)
        {
            __state = SaveCoordinator.PrepareRestoreFromSaveFile();
        }

        [HarmonyPatch("TryLoadGame")]
        [HarmonyPostfix]
        private static void TryLoadGame_Postfix(SaveCoordinator.LoadState __state)
        {
            SaveCoordinator.ApplyRestore(__state);
        }

        [HarmonyPatch("TryLoadGame")]
        [HarmonyFinalizer]
        private static Exception TryLoadGame_Finalizer(Exception __exception)
        {
            if (__exception == null) return null;

            CUCoreLibPlugin.Log?.LogError("Save loading failed inside SaveSystem.TryLoadGame... Continuing so the world can (hopefully!) still generate.\n" + __exception);

            var console = ConsoleScript.instance;
            if (console != null)
                console.Alert("Error occured during loading the save.\n" + __exception.Message + "\n" + __exception.StackTrace + "");

            return null;
        }
    }
}
