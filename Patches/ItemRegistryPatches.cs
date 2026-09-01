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

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(Item))]
    internal static class ItemRegistryPatches
    {
        private static readonly MethodInfo WearableCreateSpritesMethod =
            AccessTools.Method(typeof(Wearable), "CreateSprites", new[] { typeof(Body) }) ??
            AccessTools.Method(typeof(Wearable), "CreateSprites");

        private static readonly FieldInfo NotSpawnWithBatteryField =
            AccessTools.Field(typeof(BatteryItem), "notSpawnWithBattery");

        private static readonly MethodInfo MarkLightForUpdateMethod =
            AccessTools.Method(typeof(Light2D), "MarkForUpdate");

        private static readonly Dictionary<int, int> NextLightLookupFrameByInstance =
            new Dictionary<int, int>();

        private static readonly HashSet<string> WarnedInvalidDecayConfigurations =
            new HashSet<string>();

        private static readonly HashSet<string> WarnedInvalidSpawnComponents =
            new HashSet<string>();

        internal static void ClearWorldState()
        {
            NextLightLookupFrameByInstance.Clear();
        }

        [ThreadStatic]
        private static LiquidRegistry.HealthUseMode previousLiquidApplyMode;

        [ThreadStatic]
        private static LiquidRegistry.HealthUseMode previousLiquidInjectMode;

        // Startup injection
        [HarmonyPatch("SetupItems")]
        [HarmonyPostfix]
        public static void InjectItems()
        {
            if (Item.GlobalItems == null) return;

            ItemRegistry.ApplyVanillaItemEdits();
            ItemLootPool.InitializePool();

            foreach (var kvp in ItemRegistry.RegisteredItems.ToArray())
                try
                {
                    ItemRegistry.InjectSingleItem(kvp.Key, kvp.Value);
                }
                catch
                {
                }

            CUCoreLibPlugin.Log.LogInfo($"Bulk injected {ItemRegistry.RegisteredItems.Count} items.");
        }


        // Visuals & logic 
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void ApplyItemLogic(Item __instance)
        {
            ApplyCustomItemRuntime(__instance);
        }

        [HarmonyPatch(typeof(GunScript), nameof(GunScript.Fire))]
        [HarmonyPrefix]
        private static bool PreventCustomGunSelfHarm(GunScript __instance, bool suicide)
        {
            if (!suicide || __instance == null) return true;

            var item = __instance.GetComponent<Item>();
            return !ItemRegistry.TryGetCustomInfo(item, out var def) || def?.Gun == null;
        }

        [HarmonyPatch(nameof(Item.totalWeight), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool OverrideCustomScaledWeight(Item __instance, ref float __result)
        {
            if (__instance == null || !ItemRegistry.TryGetCustomInfo(__instance, out var def))
                return true;

            var stats = __instance.Stats;
            if (stats == null || !stats.scaleWeightWithCondition)
                return true;

            __result = Mathf.Max(0f, Mathf.Lerp(def.scaleConditionToward, stats.weight, Mathf.Clamp01(__instance.condition)));
            return false;
        }

        [HarmonyPatch(typeof(WaterContainerItem), "Start")]
        [HarmonyPrefix]
        private static void ApplyCustomLiquidMaskBeforeWaterContainerStart(WaterContainerItem __instance)
        {
            if (__instance == null) return;

            var item = __instance.GetComponent<Item>();
            if (!ItemRegistry.TryGetCustomInfo(item, out var def)) return;

            ApplyLiquidMask(__instance, def);
        }

        [HarmonyPatch(typeof(WaterContainerItem), "Start")]
        [HarmonyPostfix]
        private static void ApplyCustomLiquidMaskAnimationAfterWaterContainerStart(WaterContainerItem __instance)
        {
            if (__instance == null) return;

            var item = __instance.GetComponent<Item>();
            if (!ItemRegistry.TryGetCustomInfo(item, out var def)) return;

            ApplyLiquidMaskAnimation(__instance, def);
        }

        [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.ApplyToLimb))]
        [HarmonyPrefix]
        private static void BeginLiquidApplyToLimb()
        {
            previousLiquidApplyMode = LiquidRegistry.PushHealthUseMode(LiquidRegistry.HealthUseMode.ApplyToLimb);
        }

        [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.ApplyToLimb))]
        [HarmonyFinalizer]
        private static Exception EndLiquidApplyToLimb(Exception __exception)
        {
            LiquidRegistry.PopHealthUseMode(previousLiquidApplyMode);
            return __exception;
        }

        [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.Inject))]
        [HarmonyPrefix]
        private static void BeginLiquidInject()
        {
            previousLiquidInjectMode = LiquidRegistry.PushHealthUseMode(LiquidRegistry.HealthUseMode.Inject);
        }

        [HarmonyPatch(typeof(WaterContainerItem), nameof(WaterContainerItem.Inject))]
        [HarmonyFinalizer]
        private static Exception EndLiquidInject(Exception __exception)
        {
            LiquidRegistry.PopHealthUseMode(previousLiquidInjectMode);
            return __exception;
        }

        internal static void ApplyCustomItemRuntime(Item item, bool preferWornSprite = false)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                return;

            if (!ItemRegistry.RegisteredItems.TryGetValue(item.id, out var def))
                return;

            var shouldPreferWornSprite = preferWornSprite || IsCurrentlyWornWearable(item, def);

            ItemRegistry.EnsureRuntimeCustomDataState(item, out _);
            ApplyCustomItemVisuals(item, def, shouldPreferWornSprite);
            ApplyCustomItemComponents(item, def);
            ApplyCustomSpawnComponents(item, def);
            ApplyCustomHeldOffset(item, def);
        }

        internal static void RefreshLiveInstances(IEnumerable<string> itemIds = null)
        {
            if (Item.allItems == null || Item.allItems.Count == 0) return;

            HashSet<string> filteredIds = null;
            if (itemIds != null)
            {
                filteredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var rawId in itemIds)
                    if (!string.IsNullOrWhiteSpace(rawId))
                        filteredIds.Add(rawId.Trim());
            }

            foreach (var item in Item.allItems.ToArray())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id)) continue;
                if (filteredIds != null && !filteredIds.Contains(item.id)) continue;
                if (!ItemRegistry.TryGetCustomInfo(item, out var def) || def == null) continue;

                var preferWornSprite = IsCurrentlyWornWearable(item, def);
                ApplyCustomItemRuntime(item, preferWornSprite);

                if (preferWornSprite && item.TryGetComponent<Wearable>(out var wearable))
                {
                    var body = item.transform.parent != null ? item.transform.parent.GetComponentInParent<Body>() : null;
                    if (body != null)
                    {
                        wearable.ClearSprites();
                        InvokeWearableCreateSprites(wearable, body);
                    }
                }

                if (preferWornSprite)
                    CustomWearablePatches.ApplyPrimaryWornVisualState(item, def);
            }
        }

        private static void InvokeWearableCreateSprites(Wearable wearable, Body body)
        {
            if (wearable == null || body == null || WearableCreateSpritesMethod == null)
                return;

            var parameters = WearableCreateSpritesMethod.GetParameters();
            if (parameters.Length == 0)
            {
                WearableCreateSpritesMethod.Invoke(wearable, null);
                return;
            }

            WearableCreateSpritesMethod.Invoke(wearable, new object[] { body });
        }

        internal static Sprite GetInventorySprite(Item item, CustomItemInfo def)
        {
            if (TryGetRuntimeIconOverride(item, out var runtimeOverride) && ItemRegistry.IsValidIcon(runtimeOverride))
                return runtimeOverride;

            if (def != null && !string.IsNullOrWhiteSpace(def.IconAnimationId))
            {
                var animation = AssetLoader.GetCachedSpriteAnimation(def.IconAnimationId);
                if (animation != null && animation.Frames != null && animation.Frames.Length > 0 &&
                    ItemRegistry.IsValidIcon(animation.Frames[0]))
                    return animation.Frames[0];
            }

            var icon = ItemRegistry.GetIcon(def);
            if (icon != null) return icon;

            var sr = item != null ? item.GetComponent<SpriteRenderer>() : null;
            return sr != null ? sr.sprite : null;
        }

        private static void ApplyCustomItemVisuals(Item item, CustomItemInfo def, bool preferWornSprite)
        {
            var sprite = preferWornSprite && def.WornSprite != null
                ? def.WornSprite
                : GetInventorySprite(item, def);
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

            var baseScale = def.SpriteScale > 0f
                ? def.SpriteScale
                : 1f;

            if (!TryResolveSpriteScaleFromDimensions(item, def, out var scaledByDimensions)) return baseScale;

            return scaledByDimensions * baseScale;
        }

        internal static float ResolveInventoryIconScale(Item item, CustomItemInfo def)
        {
            if (def == null) return 1f;

            return Mathf.Max(0.01f, ResolveSpriteScale(item, def)) * Mathf.Max(0.01f, def.InventoryIconScale);
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

            var offset = def.VisualOffset;
            if (offset == default(Vector2))
                offset = def.HeldSpriteOffset;
            item.transform.localPosition = new Vector3(offset.x, offset.y,
                item.transform.localPosition.z);
        }

        internal static void ResetCustomHeldOffset(Item item)
        {
            if (item == null) return;

            var slot = item.transform.parent != null ? item.transform.parent.GetComponent<InventorySlot>() : null;
            if (slot == null || !slot.isHand) return;

            item.transform.localPosition = new Vector3(0f, 0f, item.transform.localPosition.z);
        }

        private static bool IsCurrentlyWornWearable(Item item, CustomItemInfo def)
        {
            if (item == null || def == null) return false;
            if (def.WornSprite == null && (def.MultiWornSprites == null || def.MultiWornSprites.Count == 0)) return false;
            if (!item.TryGetComponent<Wearable>(out _)) return false;

            var parent = item.transform.parent;
            return parent != null && parent.GetComponent<Limb>() != null;
        }

        internal static bool TryGetRuntimeIconOverride(Item item, out Sprite sprite)
        {
            sprite = null;
            if (item == null) return false;

            var overrideState = item.GetComponent<RuntimeItemIconOverride>();
            if (overrideState == null || overrideState.Sprite == null) return false;

            sprite = overrideState.Sprite;
            return true;
        }

        internal static void SetRuntimeIconOverride(Item item, Sprite sprite)
        {
            if (item == null) return;

            var overrideState = item.GetComponent<RuntimeItemIconOverride>();
            if (sprite == null)
            {
                if (overrideState != null)
                    Object.Destroy(overrideState);

                ApplyCustomItemRuntime(item);
                return;
            }

            if (overrideState == null)
                overrideState = item.gameObject.AddComponent<RuntimeItemIconOverride>();

            overrideState.Sprite = sprite;
            ApplyCustomItemRuntime(item);
        }

        private static void ApplyCustomItemComponents(Item item, CustomItemInfo def)
        {
            // Ideally I want to have this be unity-esque but we don't have the luxury of adding components at item definition time :(
            // So instead we have to add components at runtime and copy values over, which is less efficient but necessary for flexibility

            // Containers
            ApplyContainerProperties(item, def);

            // Batteries
            if (def.Battery != null)
            {
                var bat = item.GetComponent<BatteryItem>();
                var createdBattery = bat == null;
                if (bat == null) bat = item.gameObject.AddComponent<BatteryItem>();

                var initializeBatteryState = createdBattery || ConsumePendingBatteryInitialization(item.gameObject);
                ApplyBatteryProperties(item, bat, def, initializeBatteryState);

                def.decayInfo |= (byte)ItemInfo.DecayType.BatteryDecay;
            }

            if (def.Gun != null)
                ApplyGunProperties(item, def);

            if (def.Light != null) ApplyLight(item, def.Light);

            if (IsLiquidContainer(def))
            {
                var wat = item.GetComponent<WaterContainerItem>();
                var createdWaterContainer = wat == null;
                if (wat == null) wat = item.gameObject.AddComponent<WaterContainerItem>();

                ApplyLiquidMask(wat, def);

                if (createdWaterContainer && (wat.stack == null || wat.stack.Count == 0))
                    wat.stack = CopyLiquidStacks(def.defaultContents);

                if (def.capacity > 0f)
                {
                    var totalAmount = 0f;
                    for (var si = 0; si < wat.stack.Count; si++)
                        if (wat.stack[si] != null)
                            totalAmount += wat.stack[si].amount;
                    item.condition = Mathf.Clamp01(totalAmount / def.capacity);
                }
            }

            // Injectables (Syringes)
            if (def.Syringe == null) return;
            {
                var wat = item.GetComponent<WaterContainerItem>();
                var createdWaterContainer = wat == null;
                if (wat == null) wat = item.gameObject.AddComponent<WaterContainerItem>();

                ApplyLiquidMask(wat, def);

                if (!createdWaterContainer || (wat.stack != null && wat.stack.Count != 0)) return;
                wat.stack = new List<LiquidStack>();
                if (def.Syringe.DefaultContents != null)
                    foreach (var liquid in def.Syringe.DefaultContents)
                        wat.stack.Add(new LiquidStack(liquid.liquidId, liquid.amount));

                if (def.Syringe.Capacity > 0f)
                {
                    var totalAmount = 0f;
                    for (var si = 0; si < wat.stack.Count; si++)
                        if (wat.stack[si] != null)
                            totalAmount += wat.stack[si].amount;
                    item.condition = Mathf.Clamp01(totalAmount / def.Syringe.Capacity);
                }
            }
        }

        internal static void ApplyLiquidMask(WaterContainerItem waterContainer, CustomItemInfo def)
        {
            if (waterContainer == null || def == null) return;

            var animation = string.IsNullOrWhiteSpace(def.LiquidMaskAnimationId)
                ? null
                : AssetLoader.GetCachedSpriteAnimation(def.LiquidMaskAnimationId);
            var fillSprite = animation?.Frames != null && animation.Frames.Length > 0
                ? animation.Frames[0]
                : def.LiquidMask;
            waterContainer.fillSprite = fillSprite;

            var fillRenderer = FindLiquidFillRenderer(waterContainer);
            if (fillRenderer == null) return;

            if (animation != null)
            {
                AssetLoader.TryApplyAnimation(fillRenderer, def.LiquidMaskAnimationId);
                return;
            }

            fillRenderer.sprite = fillSprite;
            var player = fillRenderer.GetComponent<AnimatedSpriteRenderer>();
            if (player != null) Object.Destroy(player);
        }

        private static void ApplyLiquidMaskAnimation(WaterContainerItem waterContainer, CustomItemInfo def)
        {
            if (waterContainer == null || def == null || string.IsNullOrWhiteSpace(def.LiquidMaskAnimationId)) return;

            var animation = AssetLoader.GetCachedSpriteAnimation(def.LiquidMaskAnimationId);
            if (animation?.Frames == null || animation.Frames.Length == 0) return;

            var fillRenderer = FindLiquidFillRenderer(waterContainer);
            if (fillRenderer != null) AssetLoader.TryApplyAnimation(fillRenderer, def.LiquidMaskAnimationId);
        }

        private static SpriteRenderer FindLiquidFillRenderer(WaterContainerItem waterContainer)
        {
            if (waterContainer == null) return null;

            var renderers = waterContainer.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer != null && renderer.transform.parent == waterContainer.transform &&
                    string.Equals(renderer.gameObject.name, "LiquidFill", StringComparison.Ordinal))
                    return renderer;
            }
            return null;
        }

        internal static void ApplyContainerProperties(Item item, CustomItemInfo def)
        {
            if (item == null || def?.Container == null) return;

            var cont = item.GetComponent<Container>();
            if (cont == null) cont = item.gameObject.AddComponent<Container>();
            cont.maxWeight = def.Container.Capacity;
            cont.maxWeightPerItem = def.Container.MaxWeightPerItem;
            cont.encumberanceMult = def.Container.EncumbranceReduction;
            cont.itemsVisible = def.Container.ItemsVisible;
            cont.tagRestriction = def.Container.TagRestriction ?? new string[0];
        }

        private static bool IsLiquidContainer(CustomItemInfo def)
        {
            return def != null && (def.capacity > 0f || (def.defaultContents != null && def.defaultContents.Count > 0));
        }

        private static List<LiquidStack> CopyLiquidStacks(List<LiquidStack> source)
        {
            var copy = new List<LiquidStack>();
            if (source == null) return copy;

            for (var i = 0; i < source.Count; i++)
            {
                var liquid = source[i];
                if (liquid != null)
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

            ApplyLightProperties(light, properties);

            if (lightItem == null) return;
            lightItem.light = light;
            lightItem.shouldEnable = true;
        }

        /// <summary>
        /// Applies the complete Light2D state from one shared seam.  Point/cone
        /// lights are explicitly marked after all shape fields are assigned so
        /// URP consumes the new cone state on its next LateUpdate.
        /// </summary>
        internal static void ApplyLightProperties(Light2D light, LightProperties properties)
        {
            if (light == null || properties == null) return;

            light.transform.localPosition = properties.Offset;
            light.transform.localRotation = Quaternion.Euler(0f, 0f, properties.Rotation);
            light.lightType = ToLight2DType(properties.LightType);
            light.intensity = properties.Intensity;
            light.color = properties.Color;
            light.falloffIntensity = properties.FalloffIntensity;
            light.pointLightOuterRadius = properties.PointLightOuterRadius;
            light.pointLightInnerRadius = properties.PointLightInnerRadius;
            light.pointLightOuterAngle = properties.PointLightOuterAngle;
            light.pointLightInnerAngle = properties.PointLightInnerAngle;

            if (properties.LightType == CustomLightType.Point &&
                (properties.PointLightInnerAngle < 360f || properties.PointLightOuterAngle < 360f))
            {
                try
                {
                    MarkLightForUpdateMethod?.Invoke(light, null);
                }
                catch
                {
                    // Older URP versions may not expose the internal refresh hook.
                }
            }
        }

        internal static void ApplyGunProperties(Item item, CustomItemInfo def)
        {
            if (item == null || def?.Gun == null) return;

            var properties = def.Gun;
            var gun = item.GetComponent<GunScript>();
            if (gun == null) gun = item.gameObject.AddComponent<GunScript>();

            if (properties.AmmoType.HasValue) gun.ammoType = properties.AmmoType.Value;
            if (properties.FiringMode.HasValue) gun.firingMode = properties.FiringMode.Value;
            if (properties.FeedType.HasValue) gun.feedType = properties.FeedType.Value;
            if (properties.MagCapacity.HasValue) gun.magCapacity = Mathf.Max(0, properties.MagCapacity.Value);
            if (properties.KnockBack.HasValue) gun.knockBack = properties.KnockBack.Value;
            if (properties.StructureDamage.HasValue) gun.structureDamage = properties.StructureDamage.Value;
            if (properties.AnimalDamage.HasValue) gun.animalDamage = properties.AnimalDamage.Value;
            if (properties.Loudness.HasValue) gun.loudness = properties.Loudness.Value;
            if (properties.DesiredGasTime.HasValue) gun.desiredGasTime = properties.DesiredGasTime.Value;
            if (properties.ShotsPerFire.HasValue) gun.shotsPerFire = Mathf.Max(1, properties.ShotsPerFire.Value);
            if (properties.VerticalSpread.HasValue) gun.verticalSpread = properties.VerticalSpread.Value;
            if (properties.ConditionLossPerShot.HasValue)
                gun.conditionLossPerShot = properties.ConditionLossPerShot.Value;

            if (properties.FireSound != null) gun.fireSound = properties.FireSound;
            if (properties.CustomRack != null) gun.customRack = properties.CustomRack;
            if (properties.CustomUnrack != null) gun.customUnrack = properties.CustomUnrack;

            var icon = ItemRegistry.GetIcon(def);
            var baseSprite = properties.NormalSprite ?? icon;
            if (icon != null)
            {
                gun.normalSprite = properties.NormalSprite ?? icon;
                gun.rackedSprite = properties.RackedSprite ?? icon;
                gun.normalSpriteNoMag = properties.NormalSpriteNoMag ?? icon;
                gun.rackedSpriteNoMag = properties.RackedSpriteNoMag ?? icon;
            }
            else
            {
                gun.normalSprite = properties.NormalSprite ?? gun.normalSprite;
                gun.rackedSprite = properties.RackedSprite ?? gun.rackedSprite;
                gun.normalSpriteNoMag = properties.NormalSpriteNoMag ?? gun.normalSpriteNoMag;
                gun.rackedSpriteNoMag = properties.RackedSpriteNoMag ?? gun.rackedSpriteNoMag;
            }

            ApplyGunTransformOffset(gun.barrel, properties.BarrelOffset);
            ApplyGunTransformOffset(gun.muzzleParticle != null ? gun.muzzleParticle.transform : null,
                properties.MuzzleOffset);
        }

        private static void ApplyGunTransformOffset(Transform target, Vector2? requestedOffset)
        {
            if (target == null) return;

            var offset = requestedOffset;
            if (!offset.HasValue) return;

            target.localPosition = offset.Value;
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

        private static float PresetToMaxCharge(BatteryItem.BatteryPreset preset)
        {
            switch (preset)
            {
                case BatteryItem.BatteryPreset.Small:
                    return 50f;
                case BatteryItem.BatteryPreset.Large:
                    return 300f;
                case BatteryItem.BatteryPreset.Medium:
                default:
                    return 100f;
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
            bool initializeState)
        {
            if (item == null || bat == null || def?.Battery == null) return;

            var maxCharge = PresetToMaxCharge(def.Battery.Preset);
            bat.preset = def.Battery.Preset;
            bat.maxAllowedCharge = maxCharge;
            if (NotSpawnWithBatteryField != null) NotSpawnWithBatteryField.SetValue(bat, !def.Battery.SpawnWithBattery);

            if (!initializeState) return;

            if (!def.Battery.SpawnWithBattery)
            {
                bat.batteryType = string.Empty;
                bat.batteryWasFavourited = false;
                bat.maxCharge = 0f;
                item.condition = 0f;
                return;
            }

            bat.batteryType = PresetToBatteryId(def.Battery.Preset);
            bat.maxCharge = maxCharge;

            var startCharge = maxCharge;
            if (def.Battery.StartCharge >= 0f)
            {
                startCharge = def.Battery.StartCharge <= 1f
                    ? maxCharge * def.Battery.StartCharge
                    : Mathf.Min(def.Battery.StartCharge, maxCharge);
            }

            item.condition = Mathf.Clamp01(startCharge / Mathf.Max(1f, maxCharge));
        }

        [HarmonyPatch(typeof(Body), "PickUpItem")]
        [HarmonyPostfix]
        private static void ApplyCustomScaleAfterPickup(Item item)
        {
            ApplyCustomItemRuntime(item);
        }

        [HarmonyPatch(typeof(Body), "AutoPickUpItem")]
        [HarmonyPrefix]
        private static void ApplyCustomRuntimeBeforeAutoPickup(Item item)
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
                ResolveInventoryIconScale(item, def);
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

            foreach (var componentName in def.SpawnComponents)
            {
                if (string.IsNullOrWhiteSpace(componentName))
                {
                    WarnInvalidSpawnComponent(item, componentName, "entry is blank");
                    continue;
                }

                var componentType = Type.GetType(componentName, false);
                if (componentType == null)
                {
                    WarnInvalidSpawnComponent(item, componentName, "type could not be resolved");
                    continue;
                }

                if (!typeof(MonoBehaviour).IsAssignableFrom(componentType))
                {
                    WarnInvalidSpawnComponent(item, componentName, "resolved type is not a MonoBehaviour");
                    continue;
                }

                if (item.GetComponent(componentType) != null) continue;
                item.gameObject.AddComponent(componentType);
            }
        }

        private static void WarnInvalidSpawnComponent(Item item, string rawEntry, string issue)
        {
            var itemId = string.IsNullOrWhiteSpace(item != null ? item.id : null) ? "<unknown>" : item.id;
            var entry = rawEntry ?? "<null>";
            var warningKey = itemId + "|" + entry + "|" + issue;
            if (!WarnedInvalidSpawnComponents.Add(warningKey)) return;

            CUCoreLibPlugin.Log?.LogWarning(
                "Item '" + itemId + "' has an invalid SpawnComponents entry '" + entry + "': " + issue + ".");
        }

        [HarmonyPatch(typeof(LightItem), "Update")]
        [HarmonyPrefix]
        private static bool PrepareCustomLightUpdate(LightItem __instance)
        {
            if (__instance == null) return false;

            if (ItemRegistry.TryGetCustomInfo(__instance.GetComponent<Item>(), out var def) && def?.Light != null)
            {
                var battery = __instance.GetComponent<BatteryItem>();
                if (battery != null) __instance.shouldEnable = battery.hasCharge;
            }

            if (__instance.light == null)
            {
                var instanceId = __instance.GetInstanceID();
                if (NextLightLookupFrameByInstance.TryGetValue(instanceId, out var nextFrame) &&
                    Time.frameCount < nextFrame) return false;

                NextLightLookupFrameByInstance[instanceId] = Time.frameCount + 30;
                __instance.light = __instance.GetComponentInChildren<Light2D>();
                if (__instance.light != null) NextLightLookupFrameByInstance.Remove(instanceId);
            }

            if (__instance.light == null) return false;
            return true;
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
        [HarmonyPatch(new[] { typeof(string) })]
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

        private sealed class RuntimeItemIconOverride : MonoBehaviour
        {
            public Sprite Sprite;
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
}
