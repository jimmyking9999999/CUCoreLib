using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class CustomWearablePatches
    {
        [HarmonyPatch(typeof(Body), "WearWearable")]
        [HarmonyPrefix]
        private static void ApplyWornSpriteBeforeWear(Item item, out bool __state)
        {
            __state = false;
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def)) return;

            EnsureWearableComponent(item, def);
            if (def.WornSprite == null) return;

            ApplySprite(item, def.WornSprite);
            __state = true;
        }

        [HarmonyPatch(typeof(Body), "WearWearable")]
        [HarmonyPostfix]
        private static void ApplyWornSpriteOffsetAfterWear(Item item, bool __state)
        {
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def))
                return;

            if (!IsWorn(item))
            {
                if (__state && def.Icon != null)
                    ApplySprite(item, def.Icon);

                ItemRegistryPatches.ApplyCustomItemRuntime(item);
                return;
            }

            if (__state)
                ApplyPrimaryWornVisualState(item, def);

            ItemRegistryPatches.ApplyCustomItemRuntime(item, true);
        }

        [HarmonyPatch(typeof(Body), "DropWearable")]
        [HarmonyPrefix]
        private static void ResetWornSpriteOffsetBeforeDrop(Item item)
        {
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def)) return;
            if (def.WornSprite == null && (def.MultiWornSprites == null || def.MultiWornSprites.Count == 0)) return;

            item.transform.localPosition = new Vector3(0f, 0f, item.transform.localPosition.z);
        }

        [HarmonyPatch(typeof(Body), "DropWearable")]
        [HarmonyPostfix]
        private static void RestoreIconAfterDropWearable(Item item)
        {
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def) || def.Icon == null) return;

            ApplySprite(item, def.Icon);
            ItemRegistryPatches.ApplyCustomItemRuntime(item);
        }

        [HarmonyPatch(typeof(Body), "DropItem", typeof(Item))]
        [HarmonyPrefix]
        private static void ClearCustomWearableVisualsBeforeGenericDrop(Item item, out bool __state)
        {
            __state = false;
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def)) return;
            if (!IsWorn(item)) return;
            if (!item.TryGetComponent<Wearable>(out var wearable)) return;
            if (def.WornSprite == null && (def.MultiWornSprites == null || def.MultiWornSprites.Count == 0)) return;

            wearable.ClearSprites();
            item.transform.localPosition = new Vector3(0f, 0f, item.transform.localPosition.z);
            __state = true;
        }

        [HarmonyPatch(typeof(Body), "DropItem", typeof(Item))]
        [HarmonyPostfix]
        private static void RestoreCustomWearableVisualsAfterGenericDrop(Item item, bool __state)
        {
            if (!__state) return;
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def)) return;

            if (def.Icon != null)
                ApplySprite(item, def.Icon);

            ItemRegistryPatches.ApplyCustomItemRuntime(item);
        }

        [HarmonyPatch(typeof(Wearable), "CreateSprites")]
        [HarmonyPrefix]
        private static void ConfigureSecondarySpritesForCustomWearables(Wearable __instance, Body body)
        {
            var item = __instance != null ? __instance.GetComponent<Item>() : null;
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def)) return;

            ConfigureSecondarySprites(__instance, body, item, def);
        }

        [HarmonyPatch(typeof(Wearable), "CreateSprites")]
        [HarmonyPostfix]
        private static void ApplySecondarySpriteOffsetsAfterCreateSprites(Wearable __instance)
        {
            var item = __instance != null ? __instance.GetComponent<Item>() : null;
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out var def)) return;

            ApplySecondarySpriteOffsets(__instance, def);
            ApplySecondarySpriteSortingOrder(__instance, def);
        }

        private static void ApplySprite(Item item, Sprite sprite)
        {
            var sr = item.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            sr.sprite = sprite;

            if (ItemRegistry.TryGetCustomInfo(item, out var def) &&
                !string.IsNullOrWhiteSpace(def.WornSpriteAnimationId))
                AssetLoader.TryApplyAnimation(sr, def.WornSpriteAnimationId);
        }

        private static bool IsWorn(Item item)
        {
            var parent = item != null ? item.transform.parent : null;
            return parent != null && parent.GetComponent<Limb>() != null;
        }

        private static void ConfigureSecondarySprites(Wearable wearable, Body body, Item item, CustomItemInfo def)
        {
            if (wearable == null) return;
            if (def.WornSprite == null && (def.MultiWornSprites == null || def.MultiWornSprites.Count == 0)) return;

            var configuredSprites = new List<KeyValuePair<string, Sprite>>();
            if (def.MultiWornSprites != null)
                foreach (var entry in def.MultiWornSprites)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) continue;

                    if (body != null && body.LimbByName(entry.Key) == null)
                    {
                        CUCoreLibPlugin.Log?.LogWarning(
                            "Skipping multi-worn sprite for item '" + item.id + "' because limb '" + entry.Key +
                            "' does not exist on the target body.");
                        continue;
                    }

                    configuredSprites.Add(new KeyValuePair<string, Sprite>(entry.Key, entry.Value));
                }

            wearable.secondaryLimbs = configuredSprites.Select(entry => entry.Key).ToArray();
            wearable.secondaryLimbSprites = configuredSprites.Select(entry => entry.Value).ToArray();
            wearable.secondaryObjects = new GameObject[configuredSprites.Count];
        }

        private static void ApplySecondarySpriteOffsets(Wearable wearable, CustomItemInfo def)
        {
            if (wearable == null || def?.MultiWornSpriteOffsets == null || def.MultiWornSpriteOffsets.Count == 0)
                return;
            if (wearable.secondaryObjects == null || wearable.secondaryLimbs == null) return;

            var count = Math.Min(wearable.secondaryObjects.Length, wearable.secondaryLimbs.Length);
            for (var i = 0; i < count; i++)
            {
                var obj = wearable.secondaryObjects[i];
                var limb = wearable.secondaryLimbs[i];
                if (obj == null || string.IsNullOrWhiteSpace(limb)) continue;
                if (!def.MultiWornSpriteOffsets.TryGetValue(limb, out var offset)) continue;

                obj.transform.localPosition = new Vector3(offset.x, offset.y, obj.transform.localPosition.z);
            }
        }

        private static void ApplySortingOrder(Item item, CustomItemInfo def)
        {
            if (item == null || def == null || !def.WearableSortingOrder.HasValue) return;

            var renderer = item.GetComponent<SpriteRenderer>();
            if (renderer == null) return;

            renderer.sortingOrder = def.WearableSortingOrder.Value;
        }

        private static void ApplySecondarySpriteSortingOrder(Wearable wearable, CustomItemInfo def)
        {
            if (wearable == null || def == null || !def.WearableSortingOrder.HasValue) return;
            if (wearable.secondaryObjects == null) return;

            foreach (var obj in wearable.secondaryObjects)
            {
                if (obj == null) continue;

                var renderer = obj.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;

                renderer.sortingOrder = def.WearableSortingOrder.Value;
            }
        }

        internal static void ApplyPrimaryWornVisualState(Item item, CustomItemInfo def)
        {
            if (item == null || def == null) return;

            item.transform.localPosition = def.WornSprite != null
                ? new Vector3(def.WornSpriteOffset.x, def.WornSpriteOffset.y, item.transform.localPosition.z)
                : new Vector3(0f, 0f, item.transform.localPosition.z);

            ApplySortingOrder(item, def);
        }

        private static void EnsureWearableComponent(Item item, CustomItemInfo def)
        {
            if (item == null || def == null || !def.wearable) return;
            if (item.GetComponent<Wearable>() != null) return;

            item.gameObject.AddComponent<Wearable>();
        }
    }
}
