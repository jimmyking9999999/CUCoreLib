using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using CUCoreLib.Patches;
using CUCoreLib.Registries.Infrastructure;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Registries
{
    public static class ItemRegistry
    {
        private const string MissingItemIconResourcePath = "Data.MissingItem.png";

        internal static Dictionary<string, CustomItemInfo> RegisteredItems =
            new Dictionary<string, CustomItemInfo>(StringComparer.OrdinalIgnoreCase);

        private static Sprite missingItemIcon;

        private static readonly Dictionary<string, List<Action<ItemInfo>>> VanillaItemEdits =
            new Dictionary<string, List<Action<ItemInfo>>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, ItemInfo> AppliedVanillaItemEditTables =
            new Dictionary<string, ItemInfo>(StringComparer.OrdinalIgnoreCase);

        private static readonly RegistrationOwnershipIndex<string> ItemOwners =
            new RegistrationOwnershipIndex<string>(StringComparer.OrdinalIgnoreCase);

        // In-game decals are manually blacklisted. Which is probably really bad to do, but it's not too dangerous if it fails after an update
        private static readonly HashSet<string> IgnoredMissingIconIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Awful terrible hack :>
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

        private static bool NetworkSpawnComponentsWarningLogged;

        private static readonly HashSet<string> WarnedMissingIconIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> WarnedMissingCustomIconIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> WarnedInvalidLiquidStackKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConditionalWeakTable<Item, ItemCustomDataState> ItemCustomDataStates =
            new ConditionalWeakTable<Item, ItemCustomDataState>();

        private static readonly Dictionary<Type, FieldInfo[]> PublicInstanceFieldCache =
            new Dictionary<Type, FieldInfo[]>();

        public static void Register(string id, ItemInfo info, Sprite icon, int spawnFrequency = 1)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                CUCoreLibPlugin.Log?.LogWarning("Ignored custom item registration with no ID.");
                return;
            }

            var customInfo = ToCustomItemInfo(info);
            customInfo.Icon = icon;
            customInfo.SpawnFrequency = spawnFrequency;

            Register(id, customInfo);
        }

        internal static void QueueVanillaItemEdit(string id, Action<ItemInfo> edit)
        {
            if (string.IsNullOrWhiteSpace(id) || edit == null) return;

            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(id);
            if (string.IsNullOrWhiteSpace(normalizedId)) return;

            if (!VanillaItemEdits.TryGetValue(normalizedId, out var edits))
            {
                edits = new List<Action<ItemInfo>>();
                VanillaItemEdits[normalizedId] = edits;
            }

            edits.Add(edit);

            if (Item.GlobalItems != null && Item.GlobalItems.TryGetValue(normalizedId, out var info))
                ApplyVanillaItemEdits(normalizedId, info, new[] { edit });
        }

        internal static void ApplyVanillaItemEdits()
        {
            if (Item.GlobalItems == null || VanillaItemEdits.Count == 0) return;

            foreach (var entry in VanillaItemEdits.ToArray())
                if (Item.GlobalItems.TryGetValue(entry.Key, out var info))
                {
                    if (AppliedVanillaItemEditTables.TryGetValue(entry.Key, out var appliedInfo) &&
                        ReferenceEquals(appliedInfo, info))
                        continue;

                    ApplyVanillaItemEdits(entry.Key, info, entry.Value);
                }
        }

        private static void ApplyVanillaItemEdits(string id, ItemInfo info, IEnumerable<Action<ItemInfo>> edits)
        {
            if (info == null || edits == null) return;

            foreach (var edit in edits.ToArray())
                TryRun(() => edit(info));

            info.tags = info.tags ?? string.Empty;
            TryRun(info.SetTags);
            AppliedVanillaItemEditTables[id] = info;
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
                CUCoreLibPlugin.Log?.LogWarning("Ignored custom item registration with no ID.");
                return;
            }

            if (info == null) info = new CustomItemInfo();

            id = id.Trim();
            info.ID = id;
            info.tags = info.tags ?? string.Empty;
            NormalizeIcon(info);
            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(id);
            WarnedMissingIconIds.Remove(normalizedId);
            WarnedMissingCustomIconIds.Remove(normalizedId);

            if (string.IsNullOrWhiteSpace(info.category)) info.category = "nospawn";

            TryRun(() => ApplyMedicalActions(info));
            TryRun(() => ApplyDefaultOverrides(info));
            TryRun(() => LocaleRegistry.RegisterCraftingQualities(info.qualities));
            TryRun(() => ValidateLiquidReferences(id, info));

            if (!string.IsNullOrEmpty(info.fullName))
                TryRun(() => info.fullName = LocaleRegistry.Get("item", id, info.fullName));

            if (!string.IsNullOrEmpty(info.description))
                TryRun(() => info.description = LocaleRegistry.Get("item", id + "dsc", info.description));

            TryRun(() => WarnMissingCustomIcon(id, info));

            // Store or replace the registry entry, apply defaults and inject into runtime tables.
            var replacingExisting = RegisteredItems.ContainsKey(id);
            RegisteredItems[id] = info;
            
            if (replacingExisting)
                CustomInstantiate.ClearTemplateCache(id);
            
            TryRun(() => ItemOwners.Assign(id, ContentReloadSession.ResolveAmbientOwnerId()));

            TryRun(() => DropPoolRegistry.RegisterItem(id, info));

            if (Item.GlobalItems != null) TryRun(() => InjectSingleItem(id, info, replacingExisting));

            if (ItemLootPool.pool != null) TryRun(() => ItemLootPoolPatch.EnsureItemInLootPool(id, info));

            TryRun(MultiplayerSyncRegistry.QueueHostSnapshotBroadcast);
        }

        public static IDisposable BeginOwnerRegistration(string ownerId)
        {
            return ItemOwners.BeginScope(ownerId);
        }

        public static IEnumerable<string> GetRegisteredItemIds()
        {
            return RegisteredItems.Keys.ToArray();
        }

        public static bool TryGetOwnerModGuid(string id, out string modGuid)
        {
            modGuid = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(id);
            return ItemOwners.TryGetOwner(normalizedId, out modGuid) && !string.IsNullOrWhiteSpace(modGuid);
        }

        internal static IEnumerable<string> GetRegisteredItemIdsForOwner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return Array.Empty<string>();

            return ItemOwners.GetKeys(ownerId);
        }

        internal static Dictionary<string, CustomItemInfo> CaptureOwnerEntries(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return new Dictionary<string, CustomItemInfo>(StringComparer.OrdinalIgnoreCase);

            return ItemOwners.GetKeys(ownerId)
                .Where(id => RegisteredItems.TryGetValue(id, out _))
                .ToDictionary(id => id, id => RegisteredItems[id], StringComparer.OrdinalIgnoreCase);
        }

        internal static void RestoreOwnerEntries(string ownerId, IDictionary<string, CustomItemInfo> entries)
        {
            if (entries == null || entries.Count == 0) return;

            foreach (var entry in entries) Register(entry.Key, entry.Value);
        }

        internal static void ClearOwnerEntries(string ownerId, ContentReloadResult result)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return;

            var normalizedOwnerId = ownerId.Trim();
            var ids = ItemOwners.GetKeys(normalizedOwnerId);

            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                RegisteredItems.Remove(id);
                ItemOwners.Remove(id);
                Item.GlobalItems?.Remove(id);
                RemoveLootPoolEntries(id);
            }

            if (ids.Length > 0)
                result?.AddInfo("Cleared " + ids.Length + " item registrations owned by '" + normalizedOwnerId + "'.");
        }

        // Serialize customitemfields for mp sync
        internal static JObject CaptureNetworkSnapshot()
        {
            var root = new JObject();
            foreach (var entry in RegisteredItems.ToArray())
            {
                try
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
                        ["scaleConditionToward"] = info.scaleConditionToward,
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
                        ["dropPool"] = info.DropPool.HasValue
                            ? new JValue((ushort)info.DropPool.Value)
                            : JValue.CreateNull(),
                        ["worldSpawnPerChunk"] = info.WorldSpawnPerChunk.HasValue
                            ? new JValue(info.WorldSpawnPerChunk.Value)
                            : JValue.CreateNull(),
                        ["recognitionMin"] = info.rec != null ? info.rec.min : 0,
                        ["capacity"] = info.capacity,
                        ["autoFill"] = info.autoFill,
                        ["defaultContents"] = NetworkSnapshotSerialization.WriteLiquidStacks(info.defaultContents),
                        ["icon"] = NetworkSnapshotSerialization.WriteSprite(GetIcon(info)),
                        ["inventoryIconScale"] = info.InventoryIconScale,
                        ["wornSprite"] = NetworkSnapshotSerialization.WriteSprite(info.WornSprite),
                        ["wearableSortingOrder"] = info.WearableSortingOrder.HasValue
                            ? new JValue(info.WearableSortingOrder.Value)
                            : JValue.CreateNull(),
                        ["multiWornSprites"] = NetworkSnapshotSerialization.WriteSpriteDictionary(info.MultiWornSprites),
                        ["liquidMask"] = NetworkSnapshotSerialization.WriteSprite(info.LiquidMask),
                        ["liquidMaskAnimationId"] = info.LiquidMaskAnimationId ?? string.Empty,
                        ["visualOffsetX"] = info.VisualOffset.x,
                        ["visualOffsetY"] = info.VisualOffset.y,
                        ["heldSpriteOffsetX"] = info.HeldSpriteOffset.x,
                        ["heldSpriteOffsetY"] = info.HeldSpriteOffset.y,
                        ["wornSpriteOffsetX"] = info.WornSpriteOffset.x,
                        ["wornSpriteOffsetY"] = info.WornSpriteOffset.y,
                        ["multiWornSpriteOffsets"] =
                            NetworkSnapshotSerialization.WriteVector2Dictionary(info.MultiWornSpriteOffsets),
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
                            ["falloffIntensity"] = info.Light.FalloffIntensity,
                            ["pointLightOuterRadius"] = info.Light.PointLightOuterRadius,
                            ["pointLightInnerRadius"] = info.Light.PointLightInnerRadius,
                            ["pointLightOuterAngle"] = info.Light.PointLightOuterAngle,
                            ["pointLightInnerAngle"] = info.Light.PointLightInnerAngle,
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

                    if (info.Gun != null) item["gun"] = CaptureGunProperties(info.Gun);

                    if (info.qualities != null)
                        item["qualities"] = NetworkSnapshotSerialization.WriteCraftingQualities(info.qualities);

                    root[entry.Key] = item;
                }
                catch
                {
                }
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

                try
                {
                    // Audio clips are intentionally omitted from snapshots. Preserve clips from
                    // the client's local registration when the host sends the static gun data.
                    GunProperties localGun = null;
                    if (RegisteredItems.TryGetValue(id, out var localInfo)) localGun = localInfo?.Gun;

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
                    scaleConditionToward = obj.Value<float?>("scaleConditionToward") ?? 0f,
                    onlyHoldInHands = obj.Value<bool?>("onlyHoldInHands") ?? false,
                    autoAttack = obj.Value<bool?>("autoAttack") ?? false,
                    usableWithLMB = obj.Value<bool?>("usableWithLMB") ?? false,
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
                    DropPool = obj["dropPool"]?.Type == JTokenType.Null
                        ? (DropPool?)null
                        : (DropPool?)obj.Value<ushort?>("dropPool"),
                    WorldSpawnPerChunk = obj["worldSpawnPerChunk"]?.Type == JTokenType.Null
                        ? (float?)null
                        : obj.Value<float?>("worldSpawnPerChunk"),
                    rec = new Recognition(obj.Value<int?>("recognitionMin") ?? 0),
                    capacity = obj.Value<float?>("capacity") ?? 0f,
                    autoFill = obj.Value<bool?>("autoFill") ?? true,
                    defaultContents = NetworkSnapshotSerialization.ReadLiquidStacks(obj["defaultContents"]),
                    Icon = NetworkSnapshotSerialization.ReadSprite(obj["icon"]),
                    InventoryIconScale = obj.Value<float?>("inventoryIconScale") ?? 1f,
                    WornSprite = NetworkSnapshotSerialization.ReadSprite(obj["wornSprite"]),
                    WearableSortingOrder = obj["wearableSortingOrder"]?.Type == JTokenType.Null
                        ? (int?)null
                        : obj.Value<int?>("wearableSortingOrder"),
                    MultiWornSprites = NetworkSnapshotSerialization.ReadSpriteDictionary(obj["multiWornSprites"]),
                    LiquidMask = NetworkSnapshotSerialization.ReadSprite(obj["liquidMask"]),
                    LiquidMaskAnimationId = obj.Value<string>("liquidMaskAnimationId"),
                    SpriteScale = obj.Value<float?>("spriteScale") ?? 1f,
                    SpriteScaleDimensions = new SpriteScaleDimensions(
                        obj.Value<float?>("spriteScaleWidth") ?? 0f,
                        obj.Value<float?>("spriteScaleHeight") ?? 0f,
                        obj.Value<bool?>("spriteScaleExpandToFirstMetCondition") ?? false),
                    VisualOffset = new Vector2(
                        obj.Value<float?>("visualOffsetX") ?? 0f,
                        obj.Value<float?>("visualOffsetY") ?? 0f),
                    HeldSpriteOffset = new Vector2(
                        obj.Value<float?>("heldSpriteOffsetX") ?? 0f,
                        obj.Value<float?>("heldSpriteOffsetY") ?? 0f),
                    WornSpriteOffset = new Vector2(
                        obj.Value<float?>("wornSpriteOffsetX") ?? 0f,
                        obj.Value<float?>("wornSpriteOffsetY") ?? 0f),
                    MultiWornSpriteOffsets =
                        NetworkSnapshotSerialization.ReadVector2Dictionary(obj["multiWornSpriteOffsets"])
                };

                if (obj["wearable"] != null)
                    info.wearable = obj.Value<bool?>("wearable") ?? false;

                var container = obj["container"] as JObject;
                if (container != null) info.Container = container.ToObject<ContainerProperties>();

                var battery = obj["battery"] as JObject;
                if (battery != null) info.Battery = battery.ToObject<BatteryProperties>();

                var light = obj["light"] as JObject;
                if (light != null)
                    info.Light = new LightProperties
                    {
                        Intensity = light.Value<float?>("intensity") ?? 0.75f,
                        Color = NetworkSnapshotSerialization.ReadColor(light["color"], Color.white),
                        FalloffIntensity = light.Value<float?>("falloffIntensity") ?? 0.5f,
                        PointLightOuterRadius = light.Value<float?>("pointLightOuterRadius") ?? 0f,
                        PointLightInnerRadius = light.Value<float?>("pointLightInnerRadius") ?? 0f,
                        PointLightOuterAngle = light.Value<float?>("pointLightOuterAngle") ?? 360f,
                        PointLightInnerAngle = light.Value<float?>("pointLightInnerAngle") ?? 360f,
                        LightType = (CustomLightType)(light.Value<int?>("lightType") ?? 3),
                        Offset =
                            new Vector2(light.Value<float?>("offsetX") ?? 0f, light.Value<float?>("offsetY") ?? 0f),
                        AddLightItem = light.Value<bool?>("addLightItem") ?? true
                    };

                var bandage = obj["bandage"] as JObject;
                if (bandage != null) info.Bandage = bandage.ToObject<BandageProperties>();

                var syringe = obj["syringe"] as JObject;
                if (syringe != null)
                    info.Syringe = new SyringeProperties
                    {
                        Capacity = syringe.Value<float?>("capacity") ?? 0f,
                        AutoFill = syringe.Value<bool?>("autoFill") ?? true,
                        AmountPerFullUse = syringe.Value<float?>("amountPerFullUse") ?? 0f,
                        UseAverageColor = syringe.Value<bool?>("useAverageColor") ?? true,
                        MinigameColor = NetworkSnapshotSerialization.ReadColor(syringe["minigameColor"], Color.white),
                        DefaultContents = NetworkSnapshotSerialization.ReadLiquidStacks(syringe["defaultContents"])
                    };

                var tool = obj["tool"] as JObject;
                if (tool != null) info.Tool = tool.ToObject<ToolProperties>();

                var gun = obj["gun"] as JObject;
                if (gun != null)
                {
                    info.Gun = RestoreGunProperties(gun);
                    if (info.Gun != null && localGun != null)
                    {
                        info.Gun.FireSound = localGun.FireSound;
                        info.Gun.CustomRack = localGun.CustomRack;
                        info.Gun.CustomUnrack = localGun.CustomUnrack;
                    }
                }

                var qualities = obj["qualities"];
                if (qualities != null) info.qualities = NetworkSnapshotSerialization.ReadCraftingQualities(qualities);

                if (obj["customData"] is JObject customData)
                    info.CustomData = customData.ToObject<Dictionary<string, object>>() ??
                                      new Dictionary<string, object>();

                if (obj["spawnComponents"] is JArray)
                    WarnIgnoredNetworkSpawnComponents();

                // Recreate registry entries from the net request
                    Register(id, info);
                }
                catch
                {
                }
            }
        }

        private static void WarnIgnoredNetworkSpawnComponents()
        {
            if (NetworkSpawnComponentsWarningLogged) return;

            NetworkSpawnComponentsWarningLogged = true;
            CUCoreLibPlugin.Log?.LogWarning(
                "CUCoreLib Items: Ignoring network snapshot 'spawnComponents'. SpawnComponents are only honored from local registration.");
        }

        private static void ValidateLiquidReferences(string itemId, CustomItemInfo info)
        {
            if (info == null) return;

            ValidateLiquidStacks(itemId, "defaultContents", info.defaultContents);
            ValidateLiquidStacks(itemId, "Syringe.DefaultContents", info.Syringe?.DefaultContents);
        }

        private static void ValidateLiquidStacks(string itemId, string sourceName, IList<LiquidStack> stacks)
        {
            if (stacks == null) return;

            for (var i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if (stack == null)
                {
                    var nullKey = BuildInvalidLiquidStackWarningKey(itemId, sourceName, i, "<null>");
                    if (!WarnedInvalidLiquidStackKeys.Add(nullKey)) continue;

                    CUCoreLibPlugin.Log?.LogWarning(
                        $"Item '{itemId}' has a null liquid stack at {sourceName}[{i}].");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(stack.liquidId))
                {
                    var emptyKey = BuildInvalidLiquidStackWarningKey(itemId, sourceName, i, "<empty>");
                    if (!WarnedInvalidLiquidStackKeys.Add(emptyKey)) continue;

                    CUCoreLibPlugin.Log?.LogWarning(
                        $"Item '{itemId}' has a liquid stack with no liquid ID at {sourceName}[{i}].");
                    continue;
                }

                var normalizedId = stack.liquidId.Trim();
                if (LiquidRegistry.TryGetCustomInfo(normalizedId, out _) ||
                    (Liquids.Registry != null && Liquids.Registry.ContainsKey(normalizedId))) continue;

                var warningKey = BuildInvalidLiquidStackWarningKey(itemId, sourceName, i, normalizedId);
                if (!WarnedInvalidLiquidStackKeys.Add(warningKey)) continue;

                CUCoreLibPlugin.Log?.LogWarning(
                    $"Item '{itemId}' references unknown liquid '{normalizedId}' at {sourceName}[{i}].");
            }
        }

        private static string BuildInvalidLiquidStackWarningKey(string itemId, string sourceName, int index, string liquidId)
        {
            return string.Concat(itemId ?? string.Empty, "|", sourceName, "|", index.ToString(), "|", liquidId);
        }

        public static bool TryGetCustomInfo(string id, out CustomItemInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            return RegisteredItems.TryGetValue(SpawnIdHelpers.NormalizeSpawnId(id), out info);
        }

        public static bool TryGetCustomInfo(Item item, out CustomItemInfo info)
        {
            info = null;
            if (item == null) return false;

            return TryGetCustomInfo(item.id, out info);
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

            if (!TryGetRuntimeCustomDataState(item, out var state)) return false;
            if (!state.TryGetValue(key, out var rawValue)) return false;
            if (!TryConvertCustomDataValue(rawValue, out value)) return false;

            return true;
        }

        public static bool HasCustomData(Item item, string key)
        {
            if (item == null || string.IsNullOrWhiteSpace(key)) return false;

            return TryGetRuntimeCustomDataState(item, out var state) && state.ContainsKey(key);
        }

        public static T GetCustomData<T>(Item item, string key, T fallback = default)
        {
            return TryGetCustomData<T>(item, key, out var value) ? value : fallback;
        }

        public static void SetCustomData(Item item, string key, object value)
        {
            if (item == null || string.IsNullOrWhiteSpace(key)) return;
            if (!EnsureRuntimeCustomDataState(item, out var state)) return;

            state.SetValue(key, value);
            MultiplayerSyncRegistry.QueueHostSnapshotBroadcast();
        }

        public static bool RemoveCustomData(Item item, string key)
        {
            if (item == null || string.IsNullOrWhiteSpace(key)) return false;
            if (!TryGetRuntimeCustomDataState(item, out var state)) return false;

            var removed = state.RemoveValue(key);
            if (removed) MultiplayerSyncRegistry.QueueHostSnapshotBroadcast();
            return removed;
        }

        public static IReadOnlyDictionary<string, object> GetAllCustomData(Item item)
        {
            if (!TryGetRuntimeCustomDataState(item, out var state))
                return new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(StringComparer.Ordinal));

            return state.CreateSnapshot();
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

            // explicit registry icon -> cached sprite -> building definition -> prefab sprite
            if (RegisteredItems.TryGetValue(normalizedId, out var info))
            {
                sprite = GetIcon(info);
                if (sprite == null) return false;
                return true;
            }

            if (IgnoredMissingIconIds.Contains(normalizedId)) return false;

            sprite = AssetLoader.GetCachedSprite(normalizedId);
            if (sprite != null) return true;

            if (BuildingEntityRegistry.TryGetDefinition(normalizedId, out var buildingDefinition) &&
                buildingDefinition != null && buildingDefinition.Sprite != null)
            {
                sprite = buildingDefinition.Sprite;
                AssetLoader.CacheSprite(normalizedId, sprite);
                return true;
            }

            WarnMissingCustomIcon(normalizedId, info);

            var customTemplate = CustomInstantiate.GetOrCreateTemplate(normalizedId);
            if (customTemplate != null && customTemplate.TryGetComponent<SpriteRenderer>(out var customRenderer) &&
                customRenderer != null && customRenderer.sprite != null)
            {
                sprite = customRenderer.sprite;
                AssetLoader.CacheSprite(normalizedId, sprite);
                WarnedMissingIconIds.Remove(normalizedId);
                return true;
            }

            var prefab = Resources.Load<GameObject>(normalizedId);
            if (prefab != null && prefab.TryGetComponent<SpriteRenderer>(out var renderer) && renderer != null &&
                renderer.sprite != null)
            {
                sprite = renderer.sprite;
                AssetLoader.CacheSprite(normalizedId, sprite);
                WarnedMissingIconIds.Remove(normalizedId);
                return true;
            }

            if (WarnedMissingIconIds.Add(normalizedId))
                CUCoreLibPlugin.Log?.LogWarning("No item icon was found for '" + normalizedId + "'.");
            return false;
        }

        internal static Sprite GetIcon(CustomItemInfo info)
        {
            if (info == null) return null;
            if (info.Icon != null && IsValidIcon(info.Icon)) return info.Icon;

            NormalizeIcon(info);
            return info.Icon;
        }

        private static void NormalizeIcon(CustomItemInfo info)
        {
            if (info == null || IsValidIcon(info.Icon)) return;

            info.Icon = GetMissingItemIcon();
        }

        internal static bool IsValidIcon(Sprite sprite)
        {
            if (sprite == null) return false;

            try
            {
                var texture = sprite.texture;
                return texture != null && texture.width > 0 && texture.height > 0 &&
                       sprite.rect.width > 0f && sprite.rect.height > 0f;
            }
            catch
            {
                return false;
            }
        }

        internal static Sprite GetMissingItemIcon()
        {
            if (IsValidIcon(missingItemIcon)) return missingItemIcon;

            missingItemIcon = AssetLoader.LoadEmbeddedSprite(MissingItemIconResourcePath, AssetLoader.PPU_WORLD,
                typeof(ItemRegistry).Assembly);
            return missingItemIcon;
        }

        internal static void InjectSingleItem(string id, CustomItemInfo info, bool replaceExisting = false)
        {
            if (string.IsNullOrWhiteSpace(id) || info == null || Item.GlobalItems == null) return;
            if (Item.GlobalItems.ContainsKey(id) && !replaceExisting) return;

            info.ID = id;
            info.tags = info.tags ?? string.Empty;
            NormalizeIcon(info);
            TryRun(info.SetTags);
            if (!string.IsNullOrEmpty(info.fullName))
                TryRun(() => info.fullName = LocaleRegistry.Get("item", id, info.fullName));

            if (!string.IsNullOrEmpty(info.description))
                TryRun(() => info.description = LocaleRegistry.Get("item", id + "dsc", info.description));

            if (info.decayMinutes > 0f) info.rotSpeed = 1.666f / info.decayMinutes;

            TryRun(() => ExtensionData.Set<ItemInfo, CustomItemInfo>(info, info));

            Item.GlobalItems[id] = info;

            if (GetIcon(info) != null) TryRun(() => AssetLoader.CacheSprite(id, info.Icon));
            if (info.WornSprite != null) TryRun(() => AssetLoader.CacheSprite(id + "_worn", info.WornSprite));
            if (info.MultiWornSprites != null)
                foreach (var entry in info.MultiWornSprites)
                    if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value != null)
                        TryRun(() => AssetLoader.CacheSprite(id + "_worn_" + entry.Key, entry.Value));

            TryRun(() => WarnMissingCustomIcon(id, info));
        }

        internal static void InjectSingleItem(CustomItemInfo info, bool replaceExisting = false)
        {
            if (info == null) return;
            InjectSingleItem(info.ID, info, replaceExisting);
        }

        /// <summary>
        /// Returns a <see cref="CustomItemInfo"/> view of an item definition.
        /// Existing custom definitions are returned unchanged; vanilla <see cref="ItemInfo"/> instances are shallow-copied.
        /// </summary>
        /// <param name="info">The item definition to convert. May be <c>null</c>.</param>
        /// <returns>A custom item definition containing the source's public fields, or an empty definition for <c>null</c>.</returns>
        public static CustomItemInfo ToCustomItemInfo(ItemInfo info)
        {
            if (info is CustomItemInfo customInfo) return customInfo;

            var clone = new CustomItemInfo();
            if (info == null) return clone;

            // Shallow-copy all fields. A field added by another mod should not prevent the rest from registering.
            foreach (var field in GetPublicInstanceFields(info.GetType()))
                TryRun(() => field.SetValue(clone, field.GetValue(info)));

            return clone;
        }

        private static void TryRun(Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch
            {
            }
        }

        internal static bool EnsureRuntimeCustomDataState(Item item, out ItemCustomDataState state)
        {
            state = null;
            if (item == null || !TryGetCustomInfo(item, out var info)) return false;

            state = ItemCustomDataStates.GetValue(item, _ => new ItemCustomDataState());
            state.Initialize(info.CustomData);
            return true;
        }

        internal static bool TryGetRuntimeCustomDataState(Item item, out ItemCustomDataState state)
        {
            state = null;
            if (item == null || !TryGetCustomInfo(item, out var info)) return false;
            if (!ItemCustomDataStates.TryGetValue(item, out state)) return false;

            state.Initialize(info.CustomData);
            return true;
        }

        internal static JObject CaptureRuntimeCustomData(Item item)
        {
            if (!EnsureRuntimeCustomDataState(item, out var state)) return null;
            return state.Capture(item.id);
        }

        internal static void RestoreRuntimeCustomData(Item item, JObject payload)
        {
            if (item == null || payload == null) return;
            if (!EnsureRuntimeCustomDataState(item, out var state)) return;

            state.Restore(payload);
        }

        private static bool TryConvertCustomDataValue<T>(object rawValue, out T value)
        {
            value = default;
            if (rawValue == null)
            {
                if (default(T) == null) return true;
                return false;
            }

            if (rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            try
            {
                if (rawValue is JToken token)
                {
                    value = token.ToObject<T>();
                    return true;
                }

                var targetType = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (underlyingType.IsEnum)
                {
                    if (rawValue is string enumString)
                    {
                        value = (T)Enum.Parse(underlyingType, enumString, true);
                        return true;
                    }

                    value = (T)Enum.ToObject(underlyingType, rawValue);
                    return true;
                }

                if (underlyingType == typeof(Guid) && rawValue is string guidString)
                {
                    value = (T)(object)Guid.Parse(guidString);
                    return true;
                }

                if (rawValue is IConvertible && typeof(IConvertible).IsAssignableFrom(underlyingType))
                {
                    value = (T)Convert.ChangeType(rawValue, underlyingType);
                    return true;
                }

                value = JToken.FromObject(rawValue).ToObject<T>();
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        private static void WarnMissingCustomIcon(string id, CustomItemInfo info)
        {
            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(id);
            if (string.IsNullOrWhiteSpace(normalizedId) || info == null || info.Icon != null) return;
            if (IgnoredMissingIconIds.Contains(normalizedId)) return;
            if (!WarnedMissingCustomIconIds.Add(normalizedId)) return;

            CUCoreLibPlugin.Log?.LogWarning(
                "Custom item '" + normalizedId +
                "' has no valid icon sprite. Runtime will use CUCoreLib's missing-item fallback until you assign one.");
        }

        private static FieldInfo[] GetPublicInstanceFields(Type type)
        {
            if (PublicInstanceFieldCache.TryGetValue(type, out var cached))
                return cached;

            var seen = new HashSet<string>();
            var fields = new List<FieldInfo>();
            for (var current = type;
                 current != null && typeof(ItemInfo).IsAssignableFrom(current);
                 current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Public | BindingFlags.Instance |
                                                        BindingFlags.DeclaredOnly))
                    if (seen.Add(field.Name))
                        fields.Add(field);

            var result = fields.ToArray();
            PublicInstanceFieldCache[type] = result;
            return result;
        }

        private static void ApplyDefaultOverrides(CustomItemInfo info)
        {
            if (info == null) return;

            ApplyDestroyAtZeroConditionDefault(info);
            ApplyWearableDefaults(info);
            ApplyUsableDefaults(info);
        }

        private static void ApplyDestroyAtZeroConditionDefault(CustomItemInfo info)
        {
            if (info == null || info.WasExplicitlySet(CustomItemExplicitField.DestroyAtZeroCondition)) return;

            if (info.Battery != null)
            {
                info.SetDefault(CustomItemExplicitField.DestroyAtZeroCondition, false);
                return;
            }

            if (IsStandardLiquidContainer(info)) info.SetDefault(CustomItemExplicitField.DestroyAtZeroCondition, true);
        }

        private static void ApplyUsableDefaults(CustomItemInfo info)
        {
            if (info == null) return;

            var shouldDefaultUsable =
                info.useAction != null ||
                info.useLimbAction != null ||
                info.wearable;

            if (shouldDefaultUsable && !info.WasExplicitlySet(CustomItemExplicitField.Usable))
                info.SetDefault(CustomItemExplicitField.Usable, true);

            if (info.useLimbAction != null && !info.WasExplicitlySet(CustomItemExplicitField.UsableOnLimb))
                info.SetDefault(CustomItemExplicitField.UsableOnLimb, true);

            if (info.Tool != null && !info.WasExplicitlySet(CustomItemExplicitField.UsableWithLmb))
                info.SetDefault(CustomItemExplicitField.UsableWithLmb, true);
        }

        private static void ApplyWearableDefaults(CustomItemInfo info)
        {
            if (info == null) return;

            if (!info.WasExplicitlySet(CustomItemExplicitField.Wearable) &&
                !string.IsNullOrWhiteSpace(info.desiredWearLimb) &&
                !string.IsNullOrWhiteSpace(info.wearSlotId))
                info.SetDefault(CustomItemExplicitField.Wearable, true);

            if (!info.wearable || info.useAction != null) return;

            info.useAction = (body, item) =>
            {
                if (body == null || item == null) return;
                body.WearWearable(item);
            };
        }

        private static bool IsStandardLiquidContainer(ItemInfo info)
        {
            if (!(info is LiquidItemInfo liquidInfo)) return false;

            return liquidInfo.capacity > 0f ||
                   (liquidInfo.defaultContents != null && liquidInfo.defaultContents.Count > 0) ||
                   liquidInfo.autoFill;
        }

        private static void ApplyMedicalActions(CustomItemInfo info)
        {
            EnsureQualitiesForTags(info);

            if (info.Bandage != null)
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

            if (info.Syringe != null)
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

            if (info.Tool != null)
            {
                info.autoAttack = true;
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

            if (info.Gun != null)
            {
                info.autoAttack = true;
                info.usable = true;
                info.usableWithLMB = true;
                info.tags = AddTag(info.tags, "gun");
                info.useAction = (body, item) =>
                {
                    if (item == null) return;
                    var gun = item.GetComponent<GunScript>();
                    if (gun != null) gun.triggerPressed = true;
                };
            }
        }

        private static JObject CaptureGunProperties(GunProperties gun)
        {
            var result = new JObject
            {
                ["ammoType"] = gun.AmmoType.HasValue ? (JToken)new JValue((int)gun.AmmoType.Value) : JValue.CreateNull(),
                ["firingMode"] = gun.FiringMode.HasValue ? (JToken)new JValue((int)gun.FiringMode.Value) : JValue.CreateNull(),
                ["feedType"] = gun.FeedType.HasValue ? (JToken)new JValue((int)gun.FeedType.Value) : JValue.CreateNull(),
                ["magCapacity"] = gun.MagCapacity.HasValue ? (JToken)new JValue(gun.MagCapacity.Value) : JValue.CreateNull(),
                ["knockBack"] = gun.KnockBack.HasValue ? (JToken)new JValue(gun.KnockBack.Value) : JValue.CreateNull(),
                ["structureDamage"] = gun.StructureDamage.HasValue ? (JToken)new JValue(gun.StructureDamage.Value) : JValue.CreateNull(),
                ["animalDamage"] = gun.AnimalDamage.HasValue ? (JToken)new JValue(gun.AnimalDamage.Value) : JValue.CreateNull(),
                ["loudness"] = gun.Loudness.HasValue ? (JToken)new JValue(gun.Loudness.Value) : JValue.CreateNull(),
                ["desiredGasTime"] = gun.DesiredGasTime.HasValue ? (JToken)new JValue(gun.DesiredGasTime.Value) : JValue.CreateNull(),
                ["shotsPerFire"] = gun.ShotsPerFire.HasValue ? (JToken)new JValue(gun.ShotsPerFire.Value) : JValue.CreateNull(),
                ["verticalSpread"] = gun.VerticalSpread.HasValue ? (JToken)new JValue(gun.VerticalSpread.Value) : JValue.CreateNull(),
                ["conditionLossPerShot"] = gun.ConditionLossPerShot.HasValue ? (JToken)new JValue(gun.ConditionLossPerShot.Value) : JValue.CreateNull(),
                ["normalSprite"] = NetworkSnapshotSerialization.WriteSprite(gun.NormalSprite),
                ["rackedSprite"] = NetworkSnapshotSerialization.WriteSprite(gun.RackedSprite),
                ["normalSpriteNoMag"] = NetworkSnapshotSerialization.WriteSprite(gun.NormalSpriteNoMag),
                ["rackedSpriteNoMag"] = NetworkSnapshotSerialization.WriteSprite(gun.RackedSpriteNoMag)
            };

            if (gun.BarrelOffset.HasValue)
                result["barrelOffset"] = new JObject { ["x"] = gun.BarrelOffset.Value.x, ["y"] = gun.BarrelOffset.Value.y };
            if (gun.MuzzleOffset.HasValue)
                result["muzzleOffset"] = new JObject { ["x"] = gun.MuzzleOffset.Value.x, ["y"] = gun.MuzzleOffset.Value.y };

            return result;
        }

        private static GunProperties RestoreGunProperties(JObject obj)
        {
            var gun = new GunProperties
            {
                AmmoType = ReadNullableEnum<GunScript.AmmoType>(obj, "ammoType"),
                FiringMode = ReadNullableEnum<GunScript.FiringMode>(obj, "firingMode"),
                FeedType = ReadNullableEnum<GunScript.FeedType>(obj, "feedType"),
                MagCapacity = ReadNullableInt(obj, "magCapacity"),
                KnockBack = ReadNullableFloat(obj, "knockBack"),
                StructureDamage = ReadNullableFloat(obj, "structureDamage"),
                AnimalDamage = ReadNullableFloat(obj, "animalDamage"),
                Loudness = ReadNullableFloat(obj, "loudness"),
                DesiredGasTime = ReadNullableFloat(obj, "desiredGasTime"),
                ShotsPerFire = ReadNullableInt(obj, "shotsPerFire"),
                VerticalSpread = ReadNullableFloat(obj, "verticalSpread"),
                ConditionLossPerShot = ReadNullableFloat(obj, "conditionLossPerShot"),
                NormalSprite = NetworkSnapshotSerialization.ReadSprite(obj["normalSprite"]),
                RackedSprite = NetworkSnapshotSerialization.ReadSprite(obj["rackedSprite"]),
                NormalSpriteNoMag = NetworkSnapshotSerialization.ReadSprite(obj["normalSpriteNoMag"]),
                RackedSpriteNoMag = NetworkSnapshotSerialization.ReadSprite(obj["rackedSpriteNoMag"])
            };

            gun.BarrelOffset = ReadNullableVector2(obj["barrelOffset"]);
            gun.MuzzleOffset = ReadNullableVector2(obj["muzzleOffset"]);
            return gun;
        }

        private static T? ReadNullableEnum<T>(JObject obj, string key) where T : struct
        {
            if (obj[key] == null || obj[key].Type == JTokenType.Null) return null;
            return (T)Enum.ToObject(typeof(T), obj.Value<int?>(key) ?? 0);
        }

        private static int? ReadNullableInt(JObject obj, string key)
        {
            return obj[key] == null || obj[key].Type == JTokenType.Null ? (int?)null : obj.Value<int?>(key);
        }

        private static float? ReadNullableFloat(JObject obj, string key)
        {
            return obj[key] == null || obj[key].Type == JTokenType.Null ? (float?)null : obj.Value<float?>(key);
        }

        private static Vector2? ReadNullableVector2(JToken token)
        {
            var obj = token as JObject;
            return obj == null ? (Vector2?)null : new Vector2(obj.Value<float?>("x") ?? 0f, obj.Value<float?>("y") ?? 0f);
        }

        private static string AddTag(string tags, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return tags ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tags)) return tag;
            if (tags.Split(',').Any(existing => string.Equals(existing.Trim(), tag,
                    StringComparison.OrdinalIgnoreCase))) return tags;
            return tags.TrimEnd() + "," + tag;
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

        internal sealed class ItemCustomDataState
        {
            private readonly Dictionary<string, object> _values =
                new Dictionary<string, object>(StringComparer.Ordinal);

            private bool _initialized;

            public void Initialize(Dictionary<string, object> defaults)
            {
                if (_initialized) return;

                if (defaults != null)
                    foreach (var entry in defaults)
                        _values[entry.Key] = entry.Value;

                _initialized = true;
            }

            public bool ContainsKey(string key)
            {
                return !string.IsNullOrWhiteSpace(key) && _values.ContainsKey(key);
            }

            public bool TryGetValue(string key, out object value)
            {
                value = null;
                return !string.IsNullOrWhiteSpace(key) && _values.TryGetValue(key, out value);
            }

            public void SetValue(string key, object value)
            {
                if (string.IsNullOrWhiteSpace(key)) return;

                _values[key] = value;
            }

            public bool RemoveValue(string key)
            {
                return !string.IsNullOrWhiteSpace(key) && _values.Remove(key);
            }

            public IReadOnlyDictionary<string, object> CreateSnapshot()
            {
                return new ReadOnlyDictionary<string, object>(
                    new Dictionary<string, object>(_values, StringComparer.Ordinal));
            }

            public JObject Capture(string itemId)
            {
                if (_values.Count == 0) return null;

                var result = new JObject();
                foreach (var entry in _values)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)) continue;

                    try
                    {
                        result[entry.Key] = entry.Value == null
                            ? JValue.CreateNull()
                            : JToken.FromObject(entry.Value);
                    }
                    catch (Exception ex)
                    {
                        CUCoreLibPlugin.Log?.LogWarning(
                            "CUCoreLib ItemRegistry: Skipped custom item data key '" + entry.Key +
                            "' on item '" + (string.IsNullOrWhiteSpace(itemId) ? "<unknown>" : itemId) +
                            "' because the value could not be serialized.\n" + ex);
                    }
                }

                return result.HasValues ? result : null;
            }

            public void Restore(JObject payload)
            {
                if (payload == null) return;

                _values.Clear();
                foreach (var property in payload.Properties())
                    _values[property.Name] = property.Value?.ToObject<object>();

                _initialized = true;
            }
        }

        private static void AddQualityForTag(ItemInfo info, string tag)
        {
            var tags = info.tags.Split(',');
            if (!tags.Any(t => t.Trim() == tag)) return;
            if (info.qualities.Any(q => q != null && q.id == tag)) return;

            info.qualities.Add(new CraftingQuality(tag));
        }

        private static void RemoveLootPoolEntries(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            if (ItemLootPool.pool != null)
                foreach (var poolItems in ItemLootPool.pool.Values)
                    poolItems.RemoveAll(itemId => string.Equals(itemId, id, StringComparison.OrdinalIgnoreCase));

            DropPoolRegistry.RemoveItem(id);
        }

    }
}
