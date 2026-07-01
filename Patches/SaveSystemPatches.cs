using CUCoreLib.Saving;
using HarmonyLib;

namespace CUCoreLib.Patches;

[HarmonyPatch(typeof(SaveSystem))]
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
    private static void TryLoadGame_Prefix()
    {
        if (!SaveSystem.loadedRun || !SaveSystem.HasSave())
        {
            SaveCoordinator.ClearPendingRestore();
            return;
        }

        SaveCoordinator.PrepareRestoreFromSaveFile();
    }

    [HarmonyPatch("TryLoadGame")]
    [HarmonyPostfix]
    private static void TryLoadGame_Postfix()
    {
        if (!SaveSystem.loadedRun)
        {
            SaveCoordinator.ClearPendingRestore();
            return;
        }

        SaveCoordinator.ApplyPendingRestore();
    }
}