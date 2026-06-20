using HarmonyLib;
using CUCoreLib.Registries;
using CUCoreLib.Helpers;
using CUCoreLib.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(Item))]
    internal static class ItemRegistryPatches
    {
        private static readonly FieldInfo NotSpawnWithBatteryField =
            AccessTools.Field(typeof(BatteryItem), "notSpawnWithBattery");

        private static readonly Dictionary<int, int> NextLightLookupFrameByInstance =
            new Dictionary<int, int>();

        private static readonly HashSet<string> WarnedInvalidDecayConfigurations =
            new HashSet<string>();

        // Startup injection
        [HarmonyPatch("SetupItems")]
        [HarmonyPostfix]
        public static void InjectItems()
        {
            if (Item.GlobalItems == null) return;

            foreach (var kvp in ItemRegistry.RegisteredItems)
            {
                ItemRegistry.InjectSingleItem(kvp.Key, kvp.Value);
            }

            ItemLootPool.InitializePool();
            CUCoreLibPlugin.Log.LogInfo($"Bulk injected {ItemRegistry.RegisteredItems.Count} items.");
        }


        // Visuals & logic 
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void ApplyItemLogic(Item __instance)
        {
            ApplyCustomItemRuntime(__instance);
        }

        internal static void ApplyCustomItemRuntime(Item item, bool preferWornSprite = false)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                return;

            if (!ItemRegistry.RegisteredItems.TryGetValue(item.id, out var def))
                return;

            ApplyCustomItemVisuals(item, def, preferWornSprite);
            ApplyCustomItemComponents(item, def);
            ApplyCustomSpawnComponents(item, def);
            ApplyCustomHeldOffset(item, def);
        }

        internal static Sprite GetInventorySprite(Item item, CustomItemInfo def)
        {
            if (def != null && !string.IsNullOrWhiteSpace(def.IconAnimationId))
            {
                RegisteredSpriteAnimation animation = AssetLoader.GetCachedSpriteAnimation(def.IconAnimationId);
                if (animation != null && animation.Frames != null && animation.Frames.Length > 0)
                {
                    return animation.Frames[0];
                }
            }

            if (def != null && def.Icon != null)
            {
                return def.Icon;
            }

            SpriteRenderer sr = item != null ? item.GetComponent<SpriteRenderer>() : null;
            return sr != null ? sr.sprite : null;
        }

        private static void ApplyCustomItemVisuals(Item item, CustomItemInfo def, bool preferWornSprite)
        {
            Sprite sprite = preferWornSprite && def.WornSprite != null ? def.WornSprite : def.Icon;
            SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
            if (sr != null && sprite != null)
            {
                sr.sprite = sprite;
                CustomInstantiate.ApplySpriteCollision(item.gameObject, sprite);
            }

            string animationId = preferWornSprite && !string.IsNullOrWhiteSpace(def.WornSpriteAnimationId)
                ? def.WornSpriteAnimationId
                : def.IconAnimationId;
            if (sr != null && !string.IsNullOrWhiteSpace(animationId))
            {
                AssetLoader.TryApplyAnimation(sr, animationId);
            }

            ApplyCustomScale(item, def);
        }

        // Brittle. Please use SpriteScaleDimensions for proper resizing instead! (or better yet, aseprite haha)
        internal static void ApplyCustomScale(Item item, CustomItemInfo def)
        {
            if (item == null || def == null) return;

            float resolvedScale = ResolveSpriteScale(item, def);

            InventorySlot slot = item.transform.parent != null ? item.transform.parent.GetComponent<InventorySlot>() : null;
            if (slot != null && slot.limb != null)
            {
                item.transform.localScale = new Vector3(resolvedScale / slot.limb.transform.localScale.x, resolvedScale, resolvedScale);
                return;
            }

            item.transform.localScale = Vector3.one * resolvedScale;
        }

        internal static float ResolveSpriteScale(Item item, CustomItemInfo def)
        {
            if (item == null || def == null)
            {
                return 1f;
            }

            if (TryResolveSpriteScaleFromDimensions(item, def, out float scaledByDimensions))
            {
                return scaledByDimensions;
            }

            if (def.SpriteScale > 0f)
            {
                return def.SpriteScale;
            }

            return 1f;
        }

        private static bool TryResolveSpriteScaleFromDimensions(Item item, CustomItemInfo def, out float scale)
        {
            scale = 1f;
            if (item == null || def == null || !def.SpriteScaleDimensions.IsConfigured)
            {
                return false;
            }

            SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
            Sprite sprite = renderer != null ? renderer.sprite : GetInventorySprite(item, def);
            if (sprite == null)
            {
                return false;
            }

            Vector2 spritePixelSize = sprite.rect.size;
            if (spritePixelSize.x <= 0f || spritePixelSize.y <= 0f)
            {
                return false;
            }

            float widthScale = def.SpriteScaleDimensions.Width / spritePixelSize.x;
            float heightScale = def.SpriteScaleDimensions.Height / spritePixelSize.y;
            float chosenScale = def.SpriteScaleDimensions.ExpandToFirstMetCondition
                ? Mathf.Min(widthScale, heightScale)
                : Mathf.Max(widthScale, heightScale);

            if (chosenScale <= 0f || float.IsNaN(chosenScale) || float.IsInfinity(chosenScale))
            {
                return false;
            }

            scale = chosenScale;
            return true;
        }

        internal static void ApplyCustomHeldOffset(Item item, CustomItemInfo def)
        {
            if (item == null || def == null)
            {
                return;
            }

            InventorySlot slot = item.transform.parent != null ? item.transform.parent.GetComponent<InventorySlot>() : null;
            if (slot == null || !slot.isHand)
            {
                return;
            }

            item.transform.localPosition = new Vector3(def.HeldSpriteOffset.x, def.HeldSpriteOffset.y, item.transform.localPosition.z);
        }

        internal static void ResetCustomHeldOffset(Item item)
        {
            if (item == null)
            {
                return;
            }

            InventorySlot slot = item.transform.parent != null ? item.transform.parent.GetComponent<InventorySlot>() : null;
            if (slot == null || !slot.isHand)
            {
                return;
            }

            item.transform.localPosition = new Vector3(0f, 0f, item.transform.localPosition.z);
        }

        private static void ApplyCustomItemComponents(Item item, CustomItemInfo def)
        {
            // Ideally I want to have this be unity-esque but we don't have the luxury of adding components at item definition time :(
            // So instead we have to add components at runtime and copy values over, which is less efficient but necessary for flexibility

            // Containers
            if (def.Container != null)
            {
                Container cont = item.GetComponent<Container>();
                if (cont == null) cont = item.gameObject.AddComponent<Container>();
                cont.maxWeight = def.Container.Capacity;
                cont.maxWeightPerItem = def.Container.MaxWeightPerItem;
                cont.encumberanceMult = def.Container.EncumbranceReduction;
            }

            // Batteries
            if (def.Battery != null)
            {
                BatteryItem bat = item.GetComponent<BatteryItem>();
                if (bat == null) bat = item.gameObject.AddComponent<BatteryItem>();

                bat.preset = def.Battery.Preset;
                bat.maxCharge = def.Battery.MaxCharge;
                bat.maxAllowedCharge = def.Battery.MaxCharge;
                if (NotSpawnWithBatteryField != null)
                {
                    NotSpawnWithBatteryField.SetValue(bat, !def.Battery.SpawnWithBattery);
                }
                if (def.Battery.SpawnWithBattery && string.IsNullOrEmpty(bat.batteryType))
                {
                    bat.batteryType = string.IsNullOrWhiteSpace(def.Battery.BatteryType) ? PresetToBatteryId(def.Battery.Preset) : def.Battery.BatteryType;
                }

                if (def.decayInfo == 0)
                {
                    def.decayInfo = (byte)ItemInfo.DecayType.BatteryDecay;
                }

                if (item.condition <= 0f && def.Battery.StartCharge > 0f)
                {
                    item.condition = Mathf.Clamp01(def.Battery.StartCharge / Mathf.Max(1f, def.Battery.MaxCharge));
                }
            }

            if (def.Light != null)
            {
                ApplyLight(item, def.Light);
            }

            if (IsLiquidContainer(def))
            {
                WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
                bool createdWaterContainer = wat == null;
                if (wat == null) wat = item.gameObject.AddComponent<WaterContainerItem>();

                if (createdWaterContainer && (wat.stack == null || wat.stack.Count == 0))
                {
                    wat.stack = CopyLiquidStacks(def.defaultContents);
                }

                if (def.capacity > 0f)
                {
                    item.condition = Mathf.Clamp01(wat.stack.Sum(liquid => liquid.amount) / def.capacity);
                }
            }

            // Injectables (Syringes)
            if (def.Syringe != null)
            {
                WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
                bool createdWaterContainer = wat == null;
                if (wat == null) wat = item.gameObject.AddComponent<WaterContainerItem>();

                if (createdWaterContainer && (wat.stack == null || wat.stack.Count == 0))
                {
                    wat.stack = new List<LiquidStack>();
                    if (def.Syringe.DefaultContents != null)
                    {
                        foreach (LiquidStack liquid in def.Syringe.DefaultContents)
                        {
                            wat.stack.Add(new LiquidStack(liquid.liquidId, liquid.amount));
                        }
                    }

                    if (def.Syringe.Capacity > 0f)
                    {
                        item.condition = Mathf.Clamp01(wat.stack.Sum(liquid => liquid.amount) / def.Syringe.Capacity);
                    }
                }
            }
        }

        private static bool IsLiquidContainer(CustomItemInfo def)
        {
            return def != null && (def.capacity > 0f || (def.defaultContents != null && def.defaultContents.Count > 0));
        }

        private static List<LiquidStack> CopyLiquidStacks(List<LiquidStack> source)
        {
            List<LiquidStack> copy = new List<LiquidStack>();
            if (source == null) return copy;

            foreach (LiquidStack liquid in source)
            {
                if (liquid == null) continue;
                copy.Add(new LiquidStack(liquid.liquidId, liquid.amount));
            }

            return copy;
        }

        private static void ApplyLight(Item item, LightProperties properties)
        {
            if (item == null || properties == null) return;

            LightItem lightItem = null;
            if (properties.AddLightItem)
            {
                lightItem = item.GetComponent<LightItem>();
                if (lightItem == null)
                {
                    lightItem = item.gameObject.AddComponent<LightItem>();
                }
            }

            Light2D light = item.GetComponentInChildren<Light2D>();
            if (light == null)
            {
                GameObject lightObject = new GameObject("CustomLight", typeof(Light2D));
                lightObject.transform.SetParent(item.transform);
                lightObject.transform.localRotation = Quaternion.identity;
                lightObject.transform.localScale = Vector3.one;
                light = lightObject.GetComponent<Light2D>();
            }

            light.transform.localPosition = properties.Offset;
            light.lightType = ToLight2DType(properties.LightType);
            light.intensity = properties.Intensity;
            light.color = properties.Color;
            light.pointLightOuterRadius = properties.PointLightOuterRadius;
            light.pointLightInnerRadius = properties.PointLightInnerRadius;

            if (lightItem != null)
            {
                lightItem.light = light;
                lightItem.shouldEnable = true;
            }
        }

        private static Light2D.LightType ToLight2DType(CustomLightType type)
        {
            return (Light2D.LightType)(int)type;
        }

        private static string PresetToBatteryId(BatteryItem.BatteryPreset preset)
        {
            switch (preset)
            {
                case BatteryItem.BatteryPreset.Small:
                    return "smallbattery";
                case BatteryItem.BatteryPreset.Large:
                    return "largebattery";
                default:
                    return "mediumbattery";
            }
        }

        [HarmonyPatch(typeof(Body), "PickUpItem")]
        [HarmonyPostfix]
        private static void ApplyCustomScaleAfterPickup(Item item)
        {
            ApplyCustomItemRuntime(item);
        }

        [HarmonyPatch(typeof(Body), "DropItem", new[] { typeof(Item) })]
        [HarmonyPrefix]
        private static void ResetCustomHeldOffsetBeforeDrop(Item item)
        {
            ResetCustomHeldOffset(item);
        }

        [HarmonyPatch(typeof(Body), "DropItem", new[] { typeof(Item) })]
        [HarmonyPostfix]
        private static void ApplyCustomScaleAfterDrop(Item item)
        {
            ApplyCustomItemRuntime(item);
        }

        [HarmonyPatch(typeof(Body), "HandlePeriodicChecks")]
        [HarmonyPostfix]
        private static void ReapplyCustomHeldItemScale(Body __instance)
        {
            if (__instance == null || __instance.slots == null) return;

            foreach (InventorySlot slot in __instance.slots)
            {
                if (slot == null || slot.transform.childCount == 0) continue;

                Item item = slot.transform.GetChild(0).GetComponent<Item>();
                if (ItemRegistry.TryGetCustomInfo(item, out var def))
                {
                    ApplyCustomScale(item, def);
                    ApplyCustomHeldOffset(item, def);
                }
            }
        }

        [HarmonyPatch(typeof(InvButton), "UpdateGraphic")]
        [HarmonyPostfix]
        private static void UseCustomInventoryIcon(InvButton __instance)
        {
            if (__instance == null || __instance.itemImg == null) return;

            Item item = __instance.GetItem();
            if (!ItemRegistry.TryGetCustomInfo(item, out var def)) return;

            Sprite sprite = GetInventorySprite(item, def);
            if (sprite == null) return;

            __instance.itemImg.sprite = sprite;
            __instance.itemImg.rectTransform.sizeDelta = PlayerCamera.ImageSizeDelta(sprite.texture, 3f, __instance.maxImageSize) * Mathf.Max(0.01f, ResolveSpriteScale(item, def));
        }

        [HarmonyPatch(typeof(LightItem), "Start")]
        [HarmonyPostfix]
        private static void FindCustomLightAfterStart(LightItem __instance)
        {
            if (__instance == null || __instance.light != null) return;

            __instance.light = __instance.GetComponentInChildren<Light2D>();
            if (__instance.light != null)
            {
                NextLightLookupFrameByInstance.Remove(__instance.GetInstanceID());
            }
        }

        private static void ApplyCustomSpawnComponents(Item item, CustomItemInfo def)
        {
            if (item == null || def == null || def.SpawnComponents == null || def.SpawnComponents.Count == 0)
            {
                return;
            }

            foreach (string componentName in def.SpawnComponents)
            {
                if (string.IsNullOrWhiteSpace(componentName))
                {
                    continue;
                }

                Type componentType = Type.GetType(componentName, false);
                if (componentType == null)
                {
                    continue;
                }

                if (!typeof(MonoBehaviour).IsAssignableFrom(componentType))
                {
                    continue;
                }

                if (item.GetComponent(componentType) == null)
                {
                    item.gameObject.AddComponent(componentType);
                }
            }
        }

        [HarmonyPatch(typeof(LightItem), "Update")]
        [HarmonyPrefix]
        private static bool SkipNullCustomLightUpdate(LightItem __instance)
        {
            if (__instance == null) return false;
            if (__instance.light == null)
            {
                int instanceId = __instance.GetInstanceID();
                if (NextLightLookupFrameByInstance.TryGetValue(instanceId, out int nextFrame) && Time.frameCount < nextFrame)
                {
                    return false;
                }

                NextLightLookupFrameByInstance[instanceId] = Time.frameCount + 30;
                __instance.light = __instance.GetComponentInChildren<Light2D>();
                if (__instance.light != null)
                {
                    NextLightLookupFrameByInstance.Remove(instanceId);
                }
            }

            return __instance.light != null;
        }

        [HarmonyPatch(typeof(Item), "HandleDecay")]
        [HarmonyPrefix]
        private static bool GuardInvalidDecayConfiguration(Item __instance)
        {
            if (__instance == null)
            {
                return false;
            }

            ItemInfo stats = __instance.Stats;
            if (stats == null)
            {
                return false;
            }

            if ((stats.decayInfo & 1) != 0 && __instance.container == null)
            {
                WarnInvalidDecayConfiguration(__instance, "uses NoDecayWithoutContainerItem but has no Container component; skipping decay update");
                return false;
            }

            if ((stats.decayInfo & 0x10) != 0 && __instance.battery == null)
            {
                WarnInvalidDecayConfiguration(__instance, "uses BatteryDecay but has no BatteryItem component; skipping decay update");
                return false;
            }

            return true;
        }

        private static void WarnInvalidDecayConfiguration(Item item, string issue)
        {
            string itemId = string.IsNullOrWhiteSpace(item != null ? item.id : null) ? "<unknown>" : item.id;
            string warningKey = itemId + "|" + issue;
            if (!WarnedInvalidDecayConfigurations.Add(warningKey))
            {
                return;
            }

            CUCoreLibPlugin.Log?.LogWarning("Item '" + itemId + "' has an invalid decay configuration and would have thrown in Item.HandleDecay(): " + issue + ".");
        }

        [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.Capacity), MethodType.Getter)]
        [HarmonyPostfix]
        private static void CustomWaterCapacity(WaterContainerItem __instance, ref float __result)
        {
            if (__result > 0f || __instance == null) return;

            Item item = __instance.GetComponent<Item>();
            if (ItemRegistry.TryGetCustomInfo(item, out var info) && info.Syringe != null)
            {
                __result = info.Syringe.Capacity;
            }
        }

        [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.AutoFill), MethodType.Getter)]
        [HarmonyPostfix]
        private static void CustomWaterAutoFill(WaterContainerItem __instance, ref bool __result)
        {
            if (__result || __instance == null) return;

            Item item = __instance.GetComponent<Item>();
            if (ItemRegistry.TryGetCustomInfo(item, out var info) && info.Syringe != null)
            {
                __result = info.Syringe.AutoFill;
            }
        }

        // GetItem Patch
        [HarmonyPatch("GetItem")]
        [HarmonyPatch(new[] { typeof(string) })]
        [HarmonyPrefix]
        public static bool GetItem_Prefix(string id, ref ItemInfo __result)
        {
            if (Item.GlobalItems == null || string.IsNullOrWhiteSpace(id)) return true;
            if (Item.GlobalItems.ContainsKey(id)) return true;

            if (ItemRegistry.RegisteredItems.TryGetValue(id, out var def))
            {
                ItemRegistry.InjectSingleItem(id, def);
                if (Item.GlobalItems.TryGetValue(id, out var info))
                {
                    __result = info;
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch]
        internal static class ItemStatsPatch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.PropertyGetter(typeof(Item), nameof(Item.Stats));
            }

            public static bool Prefix(Item __instance, ref ItemInfo __result)
            {
                if (__instance == null || Item.GlobalItems == null || string.IsNullOrWhiteSpace(__instance.id)) return true;
                if (Item.GlobalItems.ContainsKey(__instance.id)) return true;

                if (ItemRegistry.RegisteredItems.TryGetValue(__instance.id, out var def))
                {
                    ItemRegistry.InjectSingleItem(__instance.id, def);
                    if (Item.GlobalItems.TryGetValue(__instance.id, out var info))
                    {
                        __result = info;
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
