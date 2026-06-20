using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using CUCoreLib.Patches;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Registries
{
    public static class ItemRegistry
    {
        internal static Dictionary<string, CustomItemInfo> RegisteredItems =
            new Dictionary<string, CustomItemInfo>(StringComparer.OrdinalIgnoreCase);

        // In-game decals are manually blacklisted. Which is probably really bad to do, but it's not too dangerous if it fails after an update
        private static readonly HashSet<string> IgnoredMissingIconIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "climbingropeextended",
                "grabberplant",
                "grabbershroom",
                "defibrack",
                "holidaytree",
                "marbleBackground",
                "mushroomrope",
                "mushroomropeend",
                "sandvinehook",
                "sandvinerope"
            };

        public static void Register(string id, ItemInfo info, Sprite icon, int spawnFrequency = 1)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                CUCoreLibPlugin.Log.LogWarning("Ignored custom item registration with no ID.");
                return;
            }

            var customInfo = ToCustomItemInfo(info);
            customInfo.Icon = icon;
            customInfo.SpawnFrequency = spawnFrequency;

            Register(id, customInfo);
        }

        public static void Register(string id, CustomItemInfo info, Sprite icon = null)
        {
            if (info != null && icon != null) info.Icon = icon;

            Register(id, info);
        }

        public static void Register(string id, CustomItemInfo info)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                CUCoreLibPlugin.Log.LogWarning("Ignored custom item registration with no ID.");
                return;
            }

            if (info == null) info = new CustomItemInfo();

            id = id.Trim();
            info.ID = id;

            if (string.IsNullOrWhiteSpace(info.category)) info.category = "nospawn";

            ApplySmartDefaults(info);
            ApplyMedicalActions(info);

            if (!string.IsNullOrEmpty(info.fullName)) info.fullName = LocaleRegistry.Get("item", id, info.fullName);

            if (!string.IsNullOrEmpty(info.description))
                info.description = LocaleRegistry.Get("item", id + "dsc", info.description);

            // Store or replace the registry entry, apply defaults and inject into runtime tables.
            var replacingExisting = RegisteredItems.ContainsKey(id);
            RegisteredItems[id] = info;

            if (Item.GlobalItems != null) InjectSingleItem(id, info, replacingExisting);

            if (ItemLootPool.pool != null) ItemLootPoolPatch.EnsureItemInLootPool(id, info);

            MultiplayerSyncRegistry.QueueHostSnapshotBroadcast();
        }

        public static IEnumerable<string> GetRegisteredItemIds()
        {
            return RegisteredItems.Keys.ToArray();
        }

        // Serialize customitemfields for mp sync
        internal static JObject CaptureNetworkSnapshot()
        {
            var root = new JObject();
            foreach (var entry in RegisteredItems)
            {
                var info = entry.Value;
                if (info == null) continue;

                var item = new JObject
                {
                    ["fullName"] = info.fullName ?? string.Empty,
                    ["description"] = info.description ?? string.Empty,
                    ["category"] = info.category ?? string.Empty,
                    ["slotRotation"] = info.slotRotation,
                    ["usable"] = info.usable,
                    ["usableOnLimb"] = info.usableOnLimb,
                    ["rotSpeed"] = info.rotSpeed,
                    ["destroyAtZeroCondition"] = info.destroyAtZeroCondition,
                    ["weight"] = info.weight,
                    ["scaleWeightWithCondition"] = info.scaleWeightWithCondition,
                    ["onlyHoldInHands"] = info.onlyHoldInHands,
                    ["autoAttack"] = info.autoAttack,
                    ["usableWithLMB"] = info.usableWithLMB,
                    ["wearable"] = info.wearable,
                    ["wearableCanBeHeld"] = info.wearableCanBeHeld,
                    ["desiredWearLimb"] = info.desiredWearLimb ?? string.Empty,
                    ["wearSlotId"] = info.wearSlotId ?? string.Empty,
                    ["wearableArmor"] = info.wearableArmor,
                    ["wearableIsolation"] = info.wearableIsolation,
                    ["wearableHitDurabilityLossMultiplier"] = info.wearableHitDurabilityLossMultiplier,
                    ["jumpHeightMultChange"] = info.jumpHeightMultChange,
                    ["combineable"] = info.combineable,
                    ["ignoreDepression"] = info.ignoreDepression,
                    ["value"] = info.value,
                    ["wearableVisualOffset"] = info.wearableVisualOffset,
                    ["tags"] = info.tags ?? string.Empty,
                    ["decayInfo"] = info.decayInfo,
                    ["decayMinutes"] = info.decayMinutes,
                    ["spawnFrequency"] = info.SpawnFrequency,
                    ["recognitionMin"] = info.rec?.min ?? 0,
                    ["capacity"] = info.capacity,
                    ["autoFill"] = info.autoFill,
                    ["defaultContents"] = NetworkSnapshotSerialization.WriteLiquidStacks(info.defaultContents),
                    ["icon"] = NetworkSnapshotSerialization.WriteSprite(info.Icon),
                    ["wornSprite"] = NetworkSnapshotSerialization.WriteSprite(info.WornSprite),
                    ["heldSpriteOffsetX"] = info.HeldSpriteOffset.x,
                    ["heldSpriteOffsetY"] = info.HeldSpriteOffset.y,
                    ["wornSpriteOffsetX"] = info.WornSpriteOffset.x,
                    ["wornSpriteOffsetY"] = info.WornSpriteOffset.y,
                    ["spriteScale"] = info.SpriteScale,
                    ["spriteScaleWidth"] = info.SpriteScaleDimensions.Width,
                    ["spriteScaleHeight"] = info.SpriteScaleDimensions.Height,
                    ["spriteScaleExpandToFirstMetCondition"] = info.SpriteScaleDimensions.ExpandToFirstMetCondition,
                    ["spawnComponents"] = info.SpawnComponents != null
                        ? JArray.FromObject(info.SpawnComponents)
                        : new JArray(),
                    ["customData"] = info.CustomData != null ? JObject.FromObject(info.CustomData) : new JObject()
                };

                if (info.Container != null) item["container"] = JObject.FromObject(info.Container);

                if (info.Battery != null) item["battery"] = JObject.FromObject(info.Battery);

                if (info.Light != null)
                {
                    var light = new JObject
                    {
                        ["intensity"] = info.Light.Intensity,
                        ["color"] = NetworkSnapshotSerialization.WriteColor(info.Light.Color),
                        ["pointLightOuterRadius"] = info.Light.PointLightOuterRadius,
                        ["pointLightInnerRadius"] = info.Light.PointLightInnerRadius,
                        ["lightType"] = (int)info.Light.LightType,
                        ["offsetX"] = info.Light.Offset.x,
                        ["offsetY"] = info.Light.Offset.y,
                        ["addLightItem"] = info.Light.AddLightItem
                    };

                    item["light"] = light;
                }

                if (info.Bandage != null) item["bandage"] = JObject.FromObject(info.Bandage);

                if (info.Syringe != null)
                {
                    var syringe = new JObject
                    {
                        ["capacity"] = info.Syringe.Capacity,
                        ["autoFill"] = info.Syringe.AutoFill,
                        ["amountPerFullUse"] = info.Syringe.AmountPerFullUse,
                        ["useAverageColor"] = info.Syringe.UseAverageColor,
                        ["minigameColor"] = NetworkSnapshotSerialization.WriteColor(info.Syringe.MinigameColor),
                        ["defaultContents"] =
                            NetworkSnapshotSerialization.WriteLiquidStacks(info.Syringe.DefaultContents)
                    };

                    item["syringe"] = syringe;
                }

                if (info.Tool != null) item["tool"] = JObject.FromObject(info.Tool);

                if (info.qualities != null)
                    item["qualities"] = NetworkSnapshotSerialization.WriteCraftingQualities(info.qualities);

                root[entry.Key] = item;
            }

            return root;
        }

        internal static void ApplyNetworkSnapshot(JObject snapshot)
        {
            if (snapshot == null) return;

            foreach (var property in snapshot.Properties())
            {
                var id = property.Name;
                var obj = property.Value as JObject;
                if (string.IsNullOrWhiteSpace(id) || obj == null) continue;

                var info = new CustomItemInfo
                {
                    fullName = obj.Value<string>("fullName"),
                    description = obj.Value<string>("description"),
                    category = obj.Value<string>("category"),
                    slotRotation = obj.Value<float?>("slotRotation") ?? 0f,
                    usable = obj.Value<bool?>("usable") ?? false,
                    usableOnLimb = obj.Value<bool?>("usableOnLimb") ?? false,
                    rotSpeed = obj.Value<float?>("rotSpeed") ?? 0f,
                    destroyAtZeroCondition = obj.Value<bool?>("destroyAtZeroCondition") ?? false,
                    weight = obj.Value<float?>("weight") ?? 0f,
                    scaleWeightWithCondition = obj.Value<bool?>("scaleWeightWithCondition") ?? false,
                    onlyHoldInHands = obj.Value<bool?>("onlyHoldInHands") ?? false,
                    autoAttack = obj.Value<bool?>("autoAttack") ?? false,
                    usableWithLMB = obj.Value<bool?>("usableWithLMB") ?? false,
                    wearable = obj.Value<bool?>("wearable") ?? false,
                    wearableCanBeHeld = obj.Value<bool?>("wearableCanBeHeld") ?? false,
                    desiredWearLimb = obj.Value<string>("desiredWearLimb"),
                    wearSlotId = obj.Value<string>("wearSlotId"),
                    wearableArmor = obj.Value<float?>("wearableArmor") ?? 0f,
                    wearableIsolation = obj.Value<float?>("wearableIsolation") ?? 0f,
                    wearableHitDurabilityLossMultiplier =
                        obj.Value<float?>("wearableHitDurabilityLossMultiplier") ?? 0f,
                    jumpHeightMultChange = obj.Value<float?>("jumpHeightMultChange") ?? 0f,
                    combineable = obj.Value<bool?>("combineable") ?? false,
                    ignoreDepression = obj.Value<bool?>("ignoreDepression") ?? false,
                    value = obj.Value<int?>("value") ?? 0,
                    wearableVisualOffset = obj.Value<int?>("wearableVisualOffset") ?? 0,
                    tags = obj.Value<string>("tags") ?? string.Empty,
                    decayInfo = obj.Value<byte?>("decayInfo") ?? 0,
                    decayMinutes = obj.Value<float?>("decayMinutes") ?? 0f,
                    SpawnFrequency = obj.Value<int?>("spawnFrequency") ?? 1,
                    rec = new Recognition(obj.Value<int?>("recognitionMin") ?? 0),
                    capacity = obj.Value<float?>("capacity") ?? 0f,
                    autoFill = obj.Value<bool?>("autoFill") ?? true,
                    defaultContents = NetworkSnapshotSerialization.ReadLiquidStacks(obj["defaultContents"]),
                    Icon = NetworkSnapshotSerialization.ReadSprite(obj["icon"]),
                    WornSprite = NetworkSnapshotSerialization.ReadSprite(obj["wornSprite"]),
                    SpriteScale = obj.Value<float?>("spriteScale") ?? 1f,
                    SpriteScaleDimensions = new SpriteScaleDimensions(
                        obj.Value<float?>("spriteScaleWidth") ?? 0f,
                        obj.Value<float?>("spriteScaleHeight") ?? 0f,
                        obj.Value<bool?>("spriteScaleExpandToFirstMetCondition") ?? false),
                    HeldSpriteOffset = new Vector2(
                        obj.Value<float?>("heldSpriteOffsetX") ?? 0f,
                        obj.Value<float?>("heldSpriteOffsetY") ?? 0f),
                    WornSpriteOffset = new Vector2(
                        obj.Value<float?>("wornSpriteOffsetX") ?? 0f,
                        obj.Value<float?>("wornSpriteOffsetY") ?? 0f)
                };

                if (obj["container"] is JObject container) info.Container = container.ToObject<ContainerProperties>();
                if (obj["battery"] is JObject battery) info.Battery = battery.ToObject<BatteryProperties>();
                if (obj["light"] is JObject light)
                    info.Light = new LightProperties
                    {
                        Intensity = light.Value<float?>("intensity") ?? 0.75f,
                        Color = NetworkSnapshotSerialization.ReadColor(light["color"], Color.white),
                        PointLightOuterRadius = light.Value<float?>("pointLightOuterRadius") ?? 0f,
                        PointLightInnerRadius = light.Value<float?>("pointLightInnerRadius") ?? 0f,
                        LightType = (CustomLightType)(light.Value<int?>("lightType") ?? 3),
                        Offset =
                            new Vector2(light.Value<float?>("offsetX") ?? 0f, light.Value<float?>("offsetY") ?? 0f),
                        AddLightItem = light.Value<bool?>("addLightItem") ?? true
                    };

                if (obj["bandage"] is JObject bandage) info.Bandage = bandage.ToObject<BandageProperties>();
                if (obj["syringe"] is JObject syringe)
                    info.Syringe = new SyringeProperties
                    {
                        Capacity = syringe.Value<float?>("capacity") ?? 0f,
                        AutoFill = syringe.Value<bool?>("autoFill") ?? true,
                        AmountPerFullUse = syringe.Value<float?>("amountPerFullUse") ?? 0f,
                        UseAverageColor = syringe.Value<bool?>("useAverageColor") ?? true,
                        MinigameColor = NetworkSnapshotSerialization.ReadColor(syringe["minigameColor"], Color.white),
                        DefaultContents = NetworkSnapshotSerialization.ReadLiquidStacks(syringe["defaultContents"])
                    };

                if (obj["tool"] is JObject tool) info.Tool = tool.ToObject<ToolProperties>();

                var qualities = obj["qualities"];
                if (qualities != null) info.qualities = NetworkSnapshotSerialization.ReadCraftingQualities(qualities);

                if (obj["customData"] is JObject customData)
                    info.CustomData = customData.ToObject<Dictionary<string, object>>() ??
                                      new Dictionary<string, object>();

                var spawnComponents = obj["spawnComponents"];
                if (spawnComponents is JArray spawnComponentArray)
                    info.SpawnComponents = spawnComponentArray.ToObject<List<string>>() ?? new List<string>();

                // Recreate registry entries from the net request
                Register(id, info);
            }
        }

        public static bool TryGetCustomInfo(string id, out CustomItemInfo info)
        {
            info = null;
            return !string.IsNullOrWhiteSpace(id) 
                   && RegisteredItems.TryGetValue(SpawnIdHelpers.NormalizeSpawnId(id), out info);
        }

        public static bool TryGetCustomInfo(Item item, out CustomItemInfo info)
        {
            info = null;
            return item != null
                   && TryGetCustomInfo(item.id, out info);
        }

        public static bool TryGetCustomInfo(ItemInfo stats, out CustomItemInfo info)
        {
            info = null;
            if (stats == null) return false;

            info = stats as CustomItemInfo ?? ExtensionData.Get<ItemInfo, CustomItemInfo>(stats);
            return info != null;
        }

        public static bool TryGetCustomData<T>(Item item, string key, out T value)
        {
            value = default;
            if (item == null || string.IsNullOrWhiteSpace(key)) return false;

            if (!TryGetCustomInfo(item.Stats, out var info)) return false;
            if (info.CustomData == null || !info.CustomData.TryGetValue(key, out var rawValue)) return false;
            if (!(rawValue is T typedValue)) return false;

            value = typedValue;
            return true;
        }

        public static bool TryGetItemInfo(string id, out ItemInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(id);
            if (Item.GlobalItems != null && Item.GlobalItems.TryGetValue(normalizedId, out info)) return true;

            if (RegisteredItems.TryGetValue(normalizedId, out var customInfo))
            {
                if (Item.GlobalItems != null)
                {
                    InjectSingleItem(normalizedId, customInfo);
                    if (Item.GlobalItems.TryGetValue(normalizedId, out info)) return true;
                }

                info = customInfo;
                return true;
            }

            CUCoreLibPlugin.Log?.LogWarning("No item info was found for '" + normalizedId + "'.");
            return false;
        }

        public static bool TryGetIcon(string id, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(id);
            if (IgnoredMissingIconIds.Contains(normalizedId)) return false;

            // explicit registry icon -> cached sprite -> building definition -> prefab sprite
            if (RegisteredItems.TryGetValue(normalizedId, out var info) && info.Icon != null)
            {
                sprite = info.Icon;
                return true;
            }

            sprite = AssetLoader.GetCachedSprite(normalizedId);
            if (sprite != null) return true;

            if (BuildingEntityRegistry.TryGetDefinition(normalizedId, out var buildingDefinition) &&
                buildingDefinition != null && buildingDefinition.Sprite != null)
            {
                sprite = buildingDefinition.Sprite;
                AssetLoader.CacheSprite(normalizedId, sprite);
                return true;
            }

            var prefab = Resources.Load<GameObject>(normalizedId);
            if (prefab != null && prefab.TryGetComponent<SpriteRenderer>(out var renderer) && renderer != null &&
                renderer.sprite != null)
            {
                sprite = renderer.sprite;
                AssetLoader.CacheSprite(normalizedId, sprite);
                return true;
            }

            CUCoreLibPlugin.Log?.LogWarning("No item icon was found for '" + normalizedId + "'.");
            return false;
        }

        internal static void InjectSingleItem(string id, CustomItemInfo info, bool replaceExisting = false)
        {
            if (string.IsNullOrWhiteSpace(id) || info == null || Item.GlobalItems == null) return;
            if (Item.GlobalItems.ContainsKey(id) && !replaceExisting) return;

            info.ID = id;
            info.SetTags();
            if (!string.IsNullOrEmpty(info.fullName)) info.fullName = LocaleRegistry.Get("item", id, info.fullName);

            if (!string.IsNullOrEmpty(info.description))
                info.description = LocaleRegistry.Get("item", id + "dsc", info.description);

            if (info.decayMinutes > 0f) info.rotSpeed = 1.666f / info.decayMinutes;

            ExtensionData.Set<ItemInfo, CustomItemInfo>(info, info);

            Item.GlobalItems[id] = info;

            if (info.Icon != null) AssetLoader.CacheSprite(id, info.Icon);
            if (info.WornSprite != null) AssetLoader.CacheSprite(id + "_worn", info.WornSprite);
        }

        internal static void InjectSingleItem(CustomItemInfo info, bool replaceExisting = false)
        {
            if (info == null) return;
            InjectSingleItem(info.ID, info, replaceExisting);
        }

        private static CustomItemInfo ToCustomItemInfo(ItemInfo info)
        {
            if (info is CustomItemInfo customInfo) return customInfo;

            var clone = new CustomItemInfo();
            if (info == null) return clone;

            // Shallow-copy all fields
            foreach (var field in GetPublicInstanceFields(info.GetType())) field.SetValue(clone, field.GetValue(info));

            return clone;
        }

        private static IEnumerable<FieldInfo> GetPublicInstanceFields(Type type)
        {
            var seen = new HashSet<string>();
            for (var current = type;
                 current != null && typeof(ItemInfo).IsAssignableFrom(current);
                 current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Public | BindingFlags.Instance |
                                                        BindingFlags.DeclaredOnly))
                    if (seen.Add(field.Name))
                        yield return field;
        }

        private static void ApplySmartDefaults(ItemInfo info)
        {
            if (info.destroyAtZeroCondition) return;

            if (info.decayMinutes > 0)
                info.destroyAtZeroCondition = true;
            else if (info.usable && !info.autoAttack && info.category != "tool" && info.category != "weapon")
                info.destroyAtZeroCondition = true;
            else if (info.category == "trash") info.destroyAtZeroCondition = true;
        }

        private static void ApplyMedicalActions(CustomItemInfo info)
        {
            EnsureQualitiesForTags(info);

            if (info.Bandage != null)
            {
                info.usableOnLimb = true;
                info.useLimbAction = (limb, item) =>
                {
                    var bandage = info.Bandage;
                    var effectiveness = Mathf.Max(0.001f, bandage.Effectiveness);
                    MinigameBase.main.StartMinigame(new BandageMinigame(normalAngle =>
                    {
                        var useAmount = normalAngle / effectiveness;
                        item.condition -= useAmount;
                        limb.skinHealAmount += useAmount * bandage.SkinHealAmount;
                        limb.bandageSlowAmount += useAmount * bandage.BandageSlowAmount;
                        limb.pain -= useAmount * bandage.PainReduction;
                        limb.boneHealTimer -= useAmount * bandage.BoneHealTimerReduction;
                        limb.dislocationTimer -= useAmount * bandage.DislocationTimerReduction;
                    }, bandage.MinigameColor, limb), item);

                    if (bandage.CreateWrapSprite && !string.IsNullOrWhiteSpace(bandage.WrapSpritePath))
                        limb.CreateTemporarySprite(Resources.Load<Sprite>(bandage.WrapSpritePath), 0f,
                            bandage.WrapSpriteColor, true);
                };
            }

            if (info.Syringe != null)
            {
                info.usableOnLimb = true;
                info.useLimbAction = (limb, item) =>
                {
                    var wat = item.GetComponent<WaterContainerItem>();
                    if (wat == null) wat = item.gameObject.AddComponent<WaterContainerItem>();

                    var syringe = info.Syringe;
                    var color = syringe.UseAverageColor ? wat.AverageColor() : syringe.MinigameColor;
                    MinigameBase.main.StartMinigame(
                        new SyringeMinigame(mult => { wat.Inject(limb, mult * syringe.AmountPerFullUse); }, limb,
                            color), item);
                };
            }

            if (info.Tool != null)
            {
                info.usable = true;
                info.usableWithLMB = true;
                info.autoAttack = true;
                info.destroyAtZeroCondition = true;
                info.useAction = (body, item) =>
                {
                    if (body == null || item == null) return;

                    var tool = info.Tool;
                    var attack = new AttackInfo
                    {
                        damage = tool.Damage,
                        structuralDamage = tool.StructuralDamage,
                        attackCooldownMult = tool.AttackCooldownMultiplier,
                        distance = tool.Distance,
                        knockBack = tool.KnockBack,
                        cooldown = tool.Cooldown,
                        attackAnim = string.IsNullOrWhiteSpace(tool.AttackAnimation)
                            ? null
                            : Resources.Load<GameObject>(tool.AttackAnimation),
                        staminaUse = tool.StaminaUse,
                        piercing = tool.Piercing,
                        swingSounds = tool.SwingSounds != null && tool.SwingSounds.Length > 0
                            ? tool.SwingSounds
                            : new[] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
                        volume = tool.Volume,
                        rotateAmount = tool.RotateAmount,
                        physicalSwing = tool.PhysicalSwing,
                        doAttackAnim = tool.DoAttackAnimation,
                        metalMoreDamage = tool.MetalMoreDamage
                    };

                    if (body.Attack(attack, 0)) item.condition -= tool.ConditionLossOnHit;
                };
            }
        }

        private static void EnsureQualitiesForTags(ItemInfo info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.tags)) return;
            if (info.qualities == null) info.qualities = new List<CraftingQuality>();

            AddQualityForTag(info, "dressing");
            AddQualityForTag(info, "hammering");
            AddQualityForTag(info, "cutting");
            AddQualityForTag(info, "rippable");
            AddQualityForTag(info, "produce");
            AddQualityForTag(info, "meat");
            AddQualityForTag(info, "foliage");
            AddQualityForTag(info, "heatsource");
            AddQualityForTag(info, "firestarter");
            AddQualityForTag(info, "flammable");
            AddQualityForTag(info, "nails");
            // I don't like hardcoding these. TODO make this more dynamic
        }

        private static void AddQualityForTag(ItemInfo info, string tag)
        {
            var tags = info.tags.Split(',');
            if (tags.All(t => t.Trim() != tag)) return;
            if (info.qualities.Any(q => q.id == tag)) return;

            info.qualities.Add(new CraftingQuality(tag));
        }
    }
}