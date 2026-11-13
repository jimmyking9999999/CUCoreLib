using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;

namespace CUCoreLib.Patches
{
    [AppliesToBuild(GameBuild.Full)]
    [HarmonyPatch(typeof(Item), nameof(Item.GetRandomItem))]
    internal static class MetamorphicCrystalPatch
    {
        private const int MaxRerollAttempts = 64;

        [HarmonyPostfix]
        private static void FilterMetamorphicDrop(ref string __result)
        {
            if (!IsExcluded(__result)) return;

            var globals = Item.GlobalItems;
            if (globals == null || globals.Count == 0) return;

            // Randomly weighted
            for (var attempt = 0; attempt < MaxRerollAttempts; attempt++)
            {
                var pick = globals.PickRandom();
                if (pick.Value == null || pick.Value.secret) continue;
                if (IsExcluded(pick.Key)) continue;

                __result = pick.Key;
                return;
            }
        }

        private static bool IsExcluded(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || ItemRegistry.RegisteredItems == null) return false;
            if (!ItemRegistry.RegisteredItems.TryGetValue(itemId, out var def)) return false;

            return !DropPoolRegistry.AllowsMetamorphicDrop(def);
        }
    }
}