using CUCoreLib.Saving;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(SaveSystem))]
    [HarmonyAfter("KrokoshaCasualtiesMP")]
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
    }
}
