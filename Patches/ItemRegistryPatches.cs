using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CUCoreLib.Patches;

[HarmonyPatch(typeof(Item))]
internal static class ItemRegistryPatches
{
    private static readonly FieldInfo NotSpawnWithBatteryField =
        AccessTools.Field(typeof(BatteryItem), "notSpawnWithBattery");

    private static readonly Dictionary<int, int> NextLightLookupFrameByInstance = new();

    private static readonly HashSet<string> WarnedInvalidDecayConfigurations = [];

    // Startup injection
    [HarmonyPatch("SetupItems")]
    [HarmonyPostfix]
    public static void InjectItems()
    {
        if (Item.GlobalItems == null) return;

        foreach (var kvp in ItemRegistry.RegisteredItems) ItemRegistry.InjectSingleItem(kvp.Key, kvp.Value);

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

    [HarmonyPatch(typeof(WaterContainerItem), "Start")]
    [HarmonyPrefix]
    private static void ApplyCustomLiquidMaskBeforeWaterContainerStart(WaterContainerItem __instance)
    {
        if (__instance == null) return;

        var item = __instance.GetComponent<Item>();
        if (!ItemRegistry.TryGetCustomInfo(item, out var def)) return;

        __instance.fillSprite = def.LiquidMask;
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
            var animation = AssetLoader.GetCachedSpriteAnimation(def.IconAnimationId);
            if (animation is { Frames.Length: > 0 })
                return animation.Frames[0];
        }

        if (def != null && def.Icon != null) return def.Icon;

        var sr = item != null ? item.GetComponent<SpriteRenderer>() : null;
        return sr != null ? sr.sprite : null;
    }

    private static void ApplyCustomItemVisuals(Item item, CustomItemInfo def, bool preferWornSprite)
    {
        var sprite = preferWornSprite && def.WornSprite != null ? def.WornSprite : def.Icon;
        var sr = item.GetComponent<SpriteRenderer>();
        if (sr != null && sprite != null)
        {
            sr.sprite = sprite;
            CustomInstantiate.ApplySpriteCollision(item.gameObject, sprite);
        }

        var animationId = preferWornSprite && !string.IsNullOrWhiteSpace(def.WornSpriteAnimationId)
            ? def.WornSpriteAnimationId
            : def.IconAnimationId;
        if (sr != null && !string.IsNullOrWhiteSpace(animationId)) AssetLoader.TryApplyAnimation(sr, animationId);

        ApplyCustomScale(item, def);
    }

    // Brittle. Please use SpriteScaleDimensions for proper resizing instead! (or better yet, aseprite haha)
    internal static void ApplyCustomScale(Item item, CustomItemInfo def)
    {
        if (item == null || def == null) return;

        var resolvedScale = ResolveSpriteScale(item, def);

        var slot = item.transform.parent != null ? item.transform.parent.GetComponent<InventorySlot>() : null;
        if (slot != null && slot.limb != null)
        {
            item.transform.localScale = new Vector3(resolvedScale / slot.limb.transform.localScale.x, resolvedScale,
                resolvedScale);
            return;
        }

        item.transform.localScale = Vector3.one * resolvedScale;
    }

    internal static float ResolveSpriteScale(Item item, CustomItemInfo def)
    {
        if (item == null || def == null) return 1f;

        if (TryResolveSpriteScaleFromDimensions(item, def, out var scaledByDimensions)) return scaledByDimensions;

        return def.SpriteScale > 0f
            ? def.SpriteScale
            : 1f;
    }

    private static bool TryResolveSpriteScaleFromDimensions(Item item, CustomItemInfo def, out float scale)
    {
        scale = 1f;
        if (item == null || def == null || !def.SpriteScaleDimensions.IsConfigured) return false;

        var renderer = item.GetComponent<SpriteRenderer>();
        var sprite = renderer != null ? renderer.sprite : GetInventorySprite(item, def);
        if (sprite == null) return false;

        var spritePixelSize = sprite.rect.size;
        if (spritePixelSize.x <= 0f || spritePixelSize.y <= 0f) return false;

        var widthScale = def.SpriteScaleDimensions.Width / spritePixelSize.x;
        var heightScale = def.SpriteScaleDimensions.Height / spritePixelSize.y;
        var chosenScale = def.SpriteScaleDimensions.ExpandToFirstMetCondition
            ? Mathf.Min(widthScale, heightScale)
            : Mathf.Max(widthScale, heightScale);

        if (chosenScale <= 0f || float.IsNaN(chosenScale) || float.IsInfinity(chosenScale)) return false;

        scale = chosenScale;
        return true;
    }

    internal static void ApplyCustomHeldOffset(Item item, CustomItemInfo def)
    {
        if (item == null || def == null) return;

        var slot = item.transform.parent != null ? item.transform.parent.GetComponent<InventorySlot>() : null;
        if (slot == null || !slot.isHand) return;

        item.transform.localPosition = new Vector3(def.HeldSpriteOffset.x, def.HeldSpriteOffset.y,
            item.transform.localPosition.z);
    }

    internal static void ResetCustomHeldOffset(Item item)
    {
        if (item == null) return;

        var slot = item.transform.parent != null ? item.transform.parent.GetComponent<InventorySlot>() : null;
        if (slot == null || !slot.isHand) return;

        item.transform.localPosition = new Vector3(0f, 0f, item.transform.localPosition.z);
    }

    private static void ApplyCustomItemComponents(Item item, CustomItemInfo def)
    {
        // Ideally I want to have this be unity-esque but we don't have the luxury of adding components at item definition time :(
        // So instead we have to add components at runtime and copy values over, which is less efficient but necessary for flexibility

        // Containers
        if (def.Container != null)
        {
            var cont = item.GetComponent<Container>();
            if (cont == null) cont = item.gameObject.AddComponent<Container>();
            cont.maxWeight = def.Container.Capacity;
            cont.maxWeightPerItem = def.Container.MaxWeightPerItem;
            cont.encumberanceMult = def.Container.EncumbranceReduction;
        }

        // Batteries
        if (def.Battery != null)
        {
            var bat = item.GetComponent<BatteryItem>();
            var createdBattery = bat == null;
            if (bat == null) bat = item.gameObject.AddComponent<BatteryItem>();

            var initializeBatteryState = createdBattery || ConsumePendingBatteryInitialization(item.gameObject);
            ApplyBatteryProperties(item, bat, def, initializeBatteryState,
                createdBattery || initializeBatteryState);

            if (def.decayInfo == 0) def.decayInfo = (byte)ItemInfo.DecayType.BatteryDecay;
        }

        if (def.Light != null) ApplyLight(item, def.Light);

        if (IsLiquidContainer(def))
        {
            var wat = item.GetComponent<WaterContainerItem>();
            var createdWaterContainer = wat == null;
            if (wat == null) wat = item.gameObject.AddComponent<WaterContainerItem>();

            wat.fillSprite = def.LiquidMask;

            if (createdWaterContainer && (wat.stack == null || wat.stack.Count == 0))
                wat.stack = CopyLiquidStacks(def.defaultContents);

            if (def.capacity > 0f)
                item.condition = Mathf.Clamp01(wat.stack.Sum(liquid => liquid.amount) / def.capacity);
        }

        // Injectables (Syringes)
        if (def.Syringe == null) return;
        {
            var wat = item.GetComponent<WaterContainerItem>();
            var createdWaterContainer = wat == null;
            if (wat == null) wat = item.gameObject.AddComponent<WaterContainerItem>();

            wat.fillSprite = def.LiquidMask;

            if (!createdWaterContainer || (wat.stack != null && wat.stack.Count != 0)) return;
            wat.stack = [];
            if (def.Syringe.DefaultContents != null)
                foreach (var liquid in def.Syringe.DefaultContents)
                    wat.stack.Add(new LiquidStack(liquid.liquidId, liquid.amount));

            if (def.Syringe.Capacity > 0f)
                item.condition = Mathf.Clamp01(wat.stack.Sum(liquid => liquid.amount) / def.Syringe.Capacity);
        }
    }

    private static bool IsLiquidContainer(CustomItemInfo def)
    {
        return def != null && (def.capacity > 0f || def.defaultContents is { Count: > 0 });
    }

    private static List<LiquidStack> CopyLiquidStacks(List<LiquidStack> source)
    {
        var copy = new List<LiquidStack>();
        if (source == null) return copy;

        copy.AddRange(from liquid in source
            where liquid != null
            select new LiquidStack(liquid.liquidId, liquid.amount));

        return copy;
    }

    private static void ApplyLight(Item item, LightProperties properties)
    {
        if (item == null || properties == null) return;

        LightItem lightItem = null;
        if (properties.AddLightItem)
        {
            lightItem = item.GetComponent<LightItem>();
            if (lightItem == null) lightItem = item.gameObject.AddComponent<LightItem>();
        }

        var light = item.GetComponentInChildren<Light2D>();
        if (light == null)
        {
            var lightObject = new GameObject("CustomLight", typeof(Light2D));
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

        if (lightItem == null) return;
        lightItem.light = light;
        lightItem.shouldEnable = true;
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
            case BatteryItem.BatteryPreset.Medium:
            default:
                return "mediumbattery";
        }
    }

    internal static void MarkPendingBatteryInitialization(GameObject obj)
    {
        if (obj == null || obj.GetComponent<PendingBatteryInitializationMarker>() != null) return;

        obj.AddComponent<PendingBatteryInitializationMarker>();
    }

    private static bool ConsumePendingBatteryInitialization(GameObject obj)
    {
        if (obj == null) return false;

        var marker = obj.GetComponent<PendingBatteryInitializationMarker>();
        if (marker == null) return false;

        Object.Destroy(marker);
        return true;
    }

    internal static void ApplyBatteryProperties(Item item, BatteryItem bat, CustomItemInfo def,
        bool initializeState, bool forceBatteryType)
    {
        if (item == null || bat == null || def?.Battery == null) return;

        bat.preset = def.Battery.Preset;
        bat.maxCharge = def.Battery.MaxCharge;
        bat.maxAllowedCharge = def.Battery.MaxCharge;
        if (NotSpawnWithBatteryField != null) NotSpawnWithBatteryField.SetValue(bat, !def.Battery.SpawnWithBattery);

        if (def.Battery.SpawnWithBattery)
        {
            var configuredBatteryType = string.IsNullOrWhiteSpace(def.Battery.BatteryType)
                ? PresetToBatteryId(def.Battery.Preset)
                : def.Battery.BatteryType;
            if (forceBatteryType || string.IsNullOrEmpty(bat.batteryType)) bat.batteryType = configuredBatteryType;
        }

        if (!initializeState) return;

        if (!def.Battery.SpawnWithBattery)
        {
            item.condition = 0f;
            return;
        }

        if (def.Battery.StartCharge > 0f)
            item.condition = Mathf.Clamp01(def.Battery.StartCharge / Mathf.Max(1f, def.Battery.MaxCharge));
    }

    [HarmonyPatch(typeof(Body), "PickUpItem")]
    [HarmonyPostfix]
    private static void ApplyCustomScaleAfterPickup(Item item)
    {
        ApplyCustomItemRuntime(item);
    }

    [HarmonyPatch(typeof(Body), "DropItem", typeof(Item))]
    [HarmonyPrefix]
    private static void ResetCustomHeldOffsetBeforeDrop(Item item)
    {
        ResetCustomHeldOffset(item);
    }

    [HarmonyPatch(typeof(Body), "DropItem", typeof(Item))]
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

        foreach (var slot in __instance.slots)
        {
            if (slot == null || slot.transform.childCount == 0) continue;

            var item = slot.transform.GetChild(0).GetComponent<Item>();
            if (!ItemRegistry.TryGetCustomInfo(item, out var def)) continue;
            ApplyCustomScale(item, def);
            ApplyCustomHeldOffset(item, def);
        }
    }

    [HarmonyPatch(typeof(InvButton), "UpdateGraphic")]
    [HarmonyPostfix]
    private static void UseCustomInventoryIcon(InvButton __instance)
    {
        if (__instance == null || __instance.itemImg == null) return;

        var item = __instance.GetItem();
        if (!ItemRegistry.TryGetCustomInfo(item, out var def)) return;

        var sprite = GetInventorySprite(item, def);
        if (sprite == null) return;

        __instance.itemImg.sprite = sprite;
        __instance.itemImg.rectTransform.sizeDelta =
            PlayerCamera.ImageSizeDelta(sprite.texture, 3f, __instance.maxImageSize) *
            Mathf.Max(0.01f, ResolveSpriteScale(item, def));
    }

    [HarmonyPatch(typeof(LightItem), "Start")]
    [HarmonyPostfix]
    private static void FindCustomLightAfterStart(LightItem __instance)
    {
        if (__instance == null || __instance.light != null) return;

        __instance.light = __instance.GetComponentInChildren<Light2D>();
        if (__instance.light != null) NextLightLookupFrameByInstance.Remove(__instance.GetInstanceID());
    }

    private static void ApplyCustomSpawnComponents(Item item, CustomItemInfo def)
    {
        if (item == null || def?.SpawnComponents == null || def.SpawnComponents.Count == 0) return;

        foreach (var componentType in from componentName in def.SpawnComponents
                 where !string.IsNullOrWhiteSpace(componentName)
                 select Type.GetType(componentName, false)
                 into componentType
                 where componentType != null
                 where typeof(MonoBehaviour).IsAssignableFrom(componentType)
                 where item.GetComponent(componentType) == null
                 select componentType)
            item.gameObject.AddComponent(componentType);
    }

    [HarmonyPatch(typeof(LightItem), "Update")]
    [HarmonyPrefix]
    private static bool SkipNullCustomLightUpdate(LightItem __instance)
    {
        if (__instance == null) return false;
        if (__instance.light != null) return __instance.light != null;
        var instanceId = __instance.GetInstanceID();
        if (NextLightLookupFrameByInstance.TryGetValue(instanceId, out var nextFrame) &&
            Time.frameCount < nextFrame) return false;

        NextLightLookupFrameByInstance[instanceId] = Time.frameCount + 30;
        __instance.light = __instance.GetComponentInChildren<Light2D>();
        if (__instance.light != null) NextLightLookupFrameByInstance.Remove(instanceId);

        return __instance.light != null;
    }

    [HarmonyPatch(typeof(Item), "HandleDecay")]
    [HarmonyPrefix]
    private static bool GuardInvalidDecayConfiguration(Item __instance)
    {
        if (__instance == null) return false;

        var stats = __instance.Stats;
        if (stats == null) return false;

        if ((stats.decayInfo & 1) != 0 && __instance.container == null)
        {
            WarnInvalidDecayConfiguration(__instance,
                "uses NoDecayWithoutContainerItem but has no Container component; skipping decay update");
            return false;
        }

        if ((stats.decayInfo & 0x10) == 0 || __instance.battery != null) return true;
        WarnInvalidDecayConfiguration(__instance,
            "uses BatteryDecay but has no BatteryItem component; skipping decay update");
        return false;
    }

    private static void WarnInvalidDecayConfiguration(Item item, string issue)
    {
        var itemId = string.IsNullOrWhiteSpace(item != null ? item.id : null) ? "<unknown>" : item.id;
        var warningKey = itemId + "|" + issue;
        if (!WarnedInvalidDecayConfigurations.Add(warningKey)) return;

        CUCoreLibPlugin.Log?.LogWarning("Item '" + itemId +
                                        "' has an invalid decay configuration and would have thrown in Item.HandleDecay(): " +
                                        issue + ".");
    }

    [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.Capacity), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CustomWaterCapacity(WaterContainerItem __instance, ref float __result)
    {
        if (__result > 0f || __instance == null) return;

        var item = __instance.GetComponent<Item>();
        if (ItemRegistry.TryGetCustomInfo(item, out var info) && info.Syringe != null)
            __result = info.Syringe.Capacity;
    }

    [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.AutoFill), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CustomWaterAutoFill(WaterContainerItem __instance, ref bool __result)
    {
        if (__result || __instance == null) return;

        var item = __instance.GetComponent<Item>();
        if (ItemRegistry.TryGetCustomInfo(item, out var info) && info.Syringe != null)
            __result = info.Syringe.AutoFill;
    }

    // GetItem Patch
    [HarmonyPatch("GetItem")]
    [HarmonyPatch([typeof(string)])]
    [HarmonyPrefix]
    public static bool GetItem_Prefix(string id, ref ItemInfo __result)
    {
        if (Item.GlobalItems == null || string.IsNullOrWhiteSpace(id)) return true;
        if (Item.GlobalItems.ContainsKey(id)) return true;

        if (!ItemRegistry.RegisteredItems.TryGetValue(id, out var def)) return true;
        ItemRegistry.InjectSingleItem(id, def);
        if (!Item.GlobalItems.TryGetValue(id, out var info)) return true;
        __result = info;
        return false;
    }

    private sealed class PendingBatteryInitializationMarker : MonoBehaviour
    {
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
            if (__instance == null || Item.GlobalItems == null || string.IsNullOrWhiteSpace(__instance.id))
                return true;
            if (Item.GlobalItems.ContainsKey(__instance.id)) return true;

            if (!ItemRegistry.RegisteredItems.TryGetValue(__instance.id, out var def)) return true;
            ItemRegistry.InjectSingleItem(__instance.id, def);
            if (!Item.GlobalItems.TryGetValue(__instance.id, out var info)) return true;
            __result = info;
            return false;
        }
    }
}