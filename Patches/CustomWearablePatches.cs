using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;
using CUCoreLib.Helpers;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class CustomWearablePatches
    {
        [HarmonyPatch(typeof(Body), "WearWearable")]
        [HarmonyPrefix]
        private static void ApplyWornSpriteBeforeWear(Item item)
        {
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def) || def.WornSprite == null)
            {
                return;
            }

            ApplySprite(item, def.WornSprite);
        }

        [HarmonyPatch(typeof(Body), "WearWearable")]
        [HarmonyPostfix]
        private static void ApplyWornSpriteOffsetAfterWear(Item item)
        {
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def) || def.WornSprite == null)
            {
                return;
            }

            item.transform.localPosition = new Vector3(def.WornSpriteOffset.x, def.WornSpriteOffset.y, item.transform.localPosition.z);
            ItemRegistryPatches.ApplyCustomItemRuntime(item, preferWornSprite: true);
        }

        [HarmonyPatch(typeof(Body), "DropWearable")]
        [HarmonyPrefix]
        private static void ResetWornSpriteOffsetBeforeDrop(Item item)
        {
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def) || def.WornSprite == null)
            {
                return;
            }

            item.transform.localPosition = new Vector3(0f, 0f, item.transform.localPosition.z);
        }

        [HarmonyPatch(typeof(Body), "DropWearable")]
        [HarmonyPostfix]
        private static void RestoreIconAfterDropWearable(Item item)
        {
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def) || def.Icon == null)
            {
                return;
            }

            ApplySprite(item, def.Icon);
            ItemRegistryPatches.ApplyCustomItemRuntime(item);
        }

        [HarmonyPatch(typeof(Wearable), "CreateSprites")]
        [HarmonyPrefix]
        private static void ClearVanillaSecondarySpritesForSingleSpriteCustomWearables(Wearable __instance)
        {
            Item item = __instance != null ? __instance.GetComponent<Item>() : null;
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def) || def.WornSprite == null)
            {
                return;
            }

            __instance.secondaryLimbs = new string[0];
            __instance.secondaryLimbSprites = new Sprite[0];
            __instance.secondaryObjects = new GameObject[0];
        }

        private static void ApplySprite(Item item, Sprite sprite)
        {
            SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = sprite;

                if (ItemRegistry.TryGetCustomInfo(item, out var def) && !string.IsNullOrWhiteSpace(def.WornSpriteAnimationId))
                {
                    AssetLoader.TryApplyAnimation(sr, def.WornSpriteAnimationId);
                }
            }
        }
    }
}
