using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CUCoreLib.Data
{
    /// <summary>
    /// Extends the vanilla item definition with CUCoreLib-specific fields for custom items.
    /// </summary>
    public class CustomItemInfo : LiquidItemInfo
    {
        private readonly HashSet<CustomItemExplicitField> explicitlySetFields =
            new HashSet<CustomItemExplicitField>();

        /// <summary>
        /// Optional bandage behavior added to the spawned item.
        /// </summary>
        public BandageProperties Bandage;

        /// <summary>
        /// Optional battery behavior added to the spawned item.
        /// </summary>
        public BatteryProperties Battery;

        /// <summary>
        /// Optional container behavior added to the spawned item.
        /// </summary>
        public ContainerProperties Container;

        /// <summary>
        /// Registration-time metadata for mod-defined item behavior.
        /// </summary>
        public Dictionary<string, object> CustomData = new Dictionary<string, object>();

        /// <summary>
        /// Local position offset applied to the held-item sprite.
        /// </summary>
        public Vector2 HeldSpriteOffset;

        /// <summary>
        /// Inventory/world icon sprite used for the item.
        /// </summary>
        public Sprite Icon;

        /// <summary>
        /// Additional multiplier applied only to inventory icon rendering.
        /// </summary>
        public float InventoryIconScale = 1.0f;

        /// <summary>
        /// Optional registered sprite animation id applied to <see cref="Icon"/>.
        /// </summary>
        public string IconAnimationId;

        /// <summary>
        /// Stable id. Not usually needed, as ItemRegistry.Register already asks for and generates the ID.
        /// </summary>
        public string ID;

        /// <summary>
        /// Optional fixed loot-source overrides. Leave null to use category fallback only.
        /// </summary>
        public DropPool? DropPool;

        /// <summary>
        /// Optional loose worldgen spawns per chunk.
        /// </summary>
        public float? WorldSpawnPerChunk;

        /// <summary>
        /// Optional light behavior added to the spawned item.
        /// </summary>
        public LightProperties Light;

        /// <summary>
        /// Optional mask sprite used to render contained liquids on the item sprite.
        /// </summary>
        public Sprite LiquidMask;

        /// <summary>
        /// Component type names resolved and attached when the item spawns.
        /// </summary>
        public List<string> SpawnComponents = new List<string>();

        /// <summary>
        /// Spawn weighting for traders and lootpools. 2 = double chance of seeing it.
        /// </summary>
        public int SpawnFrequency = 1;

        /// <summary>
        /// Legacy sprite scale multiplier applied to the held/inventory visuals. Don't use this, use <see cref="SpriteScaleDimensions"/> instead.
        /// </summary>
        public float SpriteScale = 1.0f;

        /// <summary>
        /// Target-dimension scaling helper for the item sprite.
        /// </summary>
        public SpriteScaleDimensions SpriteScaleDimensions;

        /// <summary>
        /// Optional syringe behavior added to the spawned item.
        /// </summary>
        public SyringeProperties Syringe;

        /// <summary>
        /// Optional melee/tool behavior added to the spawned item.
        /// </summary>
        public ToolProperties Tool;

        /// <summary>
        /// Optional extra worn sprites keyed by vanilla limb name. These are spawned as additive wearable visuals while the item is worn.
        /// </summary>
        public Dictionary<string, Sprite> MultiWornSprites = new Dictionary<string, Sprite>();

        /// <summary>
        /// Optional per-limb local offsets for <see cref="MultiWornSprites"/>.
        /// </summary>
        public Dictionary<string, Vector2> MultiWornSpriteOffsets = new Dictionary<string, Vector2>();

        /// <summary>
        /// Sprite shown when the item is worn on a body. Spawns at the desiredWearLimb position initally with <see cref="WornSpriteOffset"/> applied.
        /// </summary>
        public Sprite WornSprite;

        /// <summary>
        /// Optional worn-sprite sorting-order override applied to the primary and secondary worn sprite renderers. Higher values draw on top of lower values.
        /// </summary>
        public int? WearableSortingOrder;

        /// <summary>
        /// Optional registered sprite animation id applied to <see cref="WornSprite"/>.
        /// </summary>
        public string WornSpriteAnimationId;

        /// <summary>
        /// Local position offset applied to the worn sprite.
        /// </summary>
        public Vector2 WornSpriteOffset;

        /// <summary>
        /// Sets the local offset for a multi-worn sprite by limb name.
        /// </summary>
        public CustomItemInfo SetMultiWornSpriteOffset(string desiredWearLimb, Vector2 offset)
        {
            if (string.IsNullOrWhiteSpace(desiredWearLimb)) return this;

            MultiWornSpriteOffsets[desiredWearLimb] = offset;
            return this;
        }

        /// <summary>
        /// Sets the local offset for a uniquely-mapped multi-worn sprite.
        /// </summary>
        public CustomItemInfo SetMultiWornSpriteOffset(Vector2 offset, Sprite wornSprite)
        {
            if (wornSprite == null)
            {
                CUCoreLib.CUCoreLibPlugin.Log?.LogWarning(
                    "SetMultiWornSpriteOffset skipped because no worn sprite was provided. Use the limb-key overload or pass a sprite from MultiWornSprites.");
                return this;
            }

            var matches = MultiWornSprites
                .Where(entry => entry.Value == wornSprite && !string.IsNullOrWhiteSpace(entry.Key))
                .Select(entry => entry.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (matches.Length != 1)
            {
                CUCoreLib.CUCoreLibPlugin.Log?.LogWarning(
                    "SetMultiWornSpriteOffset could not resolve a unique limb for multi-worn sprite '" +
                    wornSprite.name + "'. Configure the offset by limb key instead.");
                return this;
            }

            MultiWornSpriteOffsets[matches[0]] = offset;
            return this;
        }

        /// <summary>
        /// Adds a spawn-time MonoBehaviour attachment using the runtime type of <typeparamref name="T"/>.
        /// </summary>
        public CustomItemInfo AddSpawnComponent<T>() where T : MonoBehaviour
        {
            return AddSpawnComponent(typeof(T));
        }

        /// <summary>
        /// Adds a spawn-time MonoBehaviour attachment using a runtime-resolvable assembly-qualified type name.
        /// </summary>
        public CustomItemInfo AddSpawnComponent(Type componentType)
        {
            if (componentType == null)
            {
                CUCoreLib.CUCoreLibPlugin.Log?.LogWarning(
                    "AddSpawnComponent skipped because no component type was provided.");
                return this;
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(componentType))
            {
                CUCoreLib.CUCoreLibPlugin.Log?.LogWarning(
                    "AddSpawnComponent skipped because type '" + componentType.FullName +
                    "' is not a MonoBehaviour.");
                return this;
            }

            var assemblyQualifiedName = componentType.AssemblyQualifiedName;
            if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
            {
                CUCoreLib.CUCoreLibPlugin.Log?.LogWarning(
                    "AddSpawnComponent skipped because type '" + componentType.FullName +
                    "' does not have an assembly-qualified name.");
                return this;
            }

            if (!SpawnComponents.Contains(assemblyQualifiedName, StringComparer.Ordinal))
                SpawnComponents.Add(assemblyQualifiedName);

            return this;
        }

        /// <summary>
        /// Adds multiple spawn-time MonoBehaviour attachments using runtime-resolvable assembly-qualified type names.
        /// </summary>
        public CustomItemInfo AddSpawnComponents(params Type[] componentTypes)
        {
            if (componentTypes == null || componentTypes.Length == 0) return this;

            foreach (var componentType in componentTypes)
                AddSpawnComponent(componentType);

            return this;
        }

        /// <summary>
        /// Tracks an explicit assignment to the vanilla usable flag so later defaulting logic does not overwrite it.
        /// </summary>
        public new bool usable
        {
            get => base.usable;
            set
            {
                base.usable = value;
                explicitlySetFields.Add(CustomItemExplicitField.Usable);
            }
        }
        /// <summary>
        /// Tracks an explicit assignment to the vanilla limb-usable flag so later defaulting logic does not overwrite it.
        /// </summary>
        public new bool usableOnLimb
        {
            get => base.usableOnLimb;
            set
            {
                base.usableOnLimb = value;
                explicitlySetFields.Add(CustomItemExplicitField.UsableOnLimb);
            }
        }
        /// <summary>
        /// Tracks an explicit assignment to the vanilla left-click usable flag so later defaulting logic does not overwrite it.
        /// </summary>
        public new bool usableWithLMB
        {
            get => base.usableWithLMB;
            set
            {
                base.usableWithLMB = value;
                explicitlySetFields.Add(CustomItemExplicitField.UsableWithLmb);
            }
        }
        /// <summary>
        /// Tracks an explicit assignment to the vanilla zero-condition destruction flag so later defaulting logic does not overwrite it.
        /// </summary>
        public new bool destroyAtZeroCondition
        {
            get => base.destroyAtZeroCondition;
            set
            {
                base.destroyAtZeroCondition = value;
                explicitlySetFields.Add(CustomItemExplicitField.DestroyAtZeroCondition);
            }
        }

        internal bool WasExplicitlySet(CustomItemExplicitField field)
        {
            return explicitlySetFields.Contains(field);
        }

        internal void SetDefault(CustomItemExplicitField field, bool value)
        {
            switch (field)
            {
                case CustomItemExplicitField.Usable:
                    base.usable = value;
                    break;
                case CustomItemExplicitField.UsableOnLimb:
                    base.usableOnLimb = value;
                    break;
                case CustomItemExplicitField.UsableWithLmb:
                    base.usableWithLMB = value;
                    break;
                case CustomItemExplicitField.DestroyAtZeroCondition:
                    base.destroyAtZeroCondition = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field), field, null);
            }
        }
    }

    internal enum CustomItemExplicitField
    {
        Usable,
        UsableOnLimb,
        UsableWithLmb,
        DestroyAtZeroCondition
    }

    /// <summary>
    /// Target sprite dimensions used to resize the item's sprite.
    /// </summary>
    [Serializable]
    public struct SpriteScaleDimensions
    {
        /// <summary>
        /// Desired sprite width in pixels.
        /// </summary>
        public float Width;

        /// <summary>
        /// Desired sprite height in pixels.
        /// </summary>
        public float Height;

        /// <summary>
        /// When true, scale up until the first width or height target is met instead of requiring both. 
        /// </summary>
        public bool ExpandToFirstMetCondition;

        /// <summary>
        /// Creates a dimension-based scaling rule for the item sprite.
        /// </summary>
        public SpriteScaleDimensions(float width, float height, bool expandToFirstMetCondition = false)
        {
            Width = width;
            Height = height;
            ExpandToFirstMetCondition = expandToFirstMetCondition;
        }

        /// <summary>
        /// Returns true when both width and height have positive configured values.
        /// </summary>
        public bool IsConfigured => Width > 0f && Height > 0f;

        /// <summary>
        /// Creates a configured scale target from a width and height tuple.
        /// </summary>
        public static implicit operator SpriteScaleDimensions((float width, float height) value)
        {
            return new SpriteScaleDimensions(value.width, value.height);
        }

        /// <summary>
        /// Creates a configured scale target from a width, height, and expansion-mode tuple.
        /// </summary>
        public static implicit operator SpriteScaleDimensions(
            (float width, float height, bool expandToFirstMetCondition) value)
        {
            return new SpriteScaleDimensions(value.width, value.height, value.expandToFirstMetCondition);
        }
    }

    /// <summary>
    /// Container capacity and encumbrance behavior applied to an item.
    /// </summary>
    [Serializable]
    public class ContainerProperties
    {
        /// <summary>
        /// Maximum total stored weight of container.
        /// </summary>
        public float Capacity = 10f;

        /// <summary>
        /// Maximum weight allowed for a single item.
        /// </summary>
        public float MaxWeightPerItem = 5f;

        /// <summary>
        /// Encumbrance multiplier applied to contained items. 1.0 = normal, 0.5 = half, 2.0 = double.
        /// </summary>
        public float EncumbranceReduction = 1.0f;

        /// <summary>
        /// Whether contained items remain visually visible while inside the container.
        /// </summary>
        public bool ItemsVisible = false;

        /// <summary>
        /// Optional item-tag restriction list for what the container accepts.
        /// </summary>
        public string[] TagRestriction = new string[0];
    }

    /// <summary>
    /// Battery defaults applied to battery-backed items.
    /// </summary>
    [Serializable]
    public class BatteryProperties
    {
        /// <summary>
        /// Legacy compatibility field kept so older mods do not break. Ignored at runtime; battery capacity is now derived from <see cref="Preset"/>.
        /// </summary>
        public float MaxCharge = 100f;

        /// <summary>
        /// Initial battery charge when the item spawns. Values from 0 to 1 are treated as a percentage of the preset max charge; higher values are treated as absolute charge. Leave below zero to use the preset's full default charge.
        /// </summary>
        public float StartCharge = -1f;

        /// <summary>
        /// Battery preset used to configure the item.
        /// </summary>
        public BatteryItem.BatteryPreset Preset = BatteryItem.BatteryPreset.Medium;

        /// <summary>
        /// Legacy compatibility field kept so older mods do not break. Ignored at runtime; the inserted battery type is now derived from <see cref="Preset"/>.
        /// </summary>
        public string BatteryType = "mediumbattery";

        /// <summary>
        /// Whether the item should spawn with a battery inserted.
        /// </summary>
        public bool SpawnWithBattery = true;
    }

    /// <summary>
    /// Light component settings applied to custom light-emitting items.
    /// </summary>
    [Serializable]
    public class LightProperties
    {
        /// <summary>
        /// Light intensity.
        /// </summary>
        public float Intensity = 0.75f;

        /// <summary>
        /// Light color tint.
        /// </summary>
        public Color Color = Color.white;

        /// <summary>
        /// Outer radius for point/2D light falloff.
        /// </summary>
        public float PointLightOuterRadius = 7.5f;

        /// <summary>
        /// Inner radius for point/2D light falloff.
        /// </summary>
        public float PointLightInnerRadius;

        /// <summary>
        /// Outer cone angle for point lights.
        /// </summary>
        public float PointLightOuterAngle = 360f;

        /// <summary>
        /// Inner cone angle for point lights.
        /// </summary>
        public float PointLightInnerAngle = 360f;

        /// <summary>
        /// Underlying Unity light shape used for the spawned light.
        /// </summary>
        public CustomLightType LightType = CustomLightType.Point;

        /// <summary>
        /// Local offset applied to the spawned light.
        /// </summary>
        public Vector2 Offset = Vector2.zero;

        /// <summary>
        /// Whether a light item helper component should be added automatically.
        /// </summary>
        public bool AddLightItem = true;
    }

    /// <summary>
    /// Supported Unity 2D light shapes for <see cref="LightProperties"/>.
    /// </summary>
    public enum CustomLightType
    {
        /// <summary>
        /// Uses a parametric 2D light shape.
        /// </summary>
        Parametric = 0,
        /// <summary>
        /// Uses a freeform 2D light shape.
        /// </summary>
        Freeform = 1,
        /// <summary>
        /// Uses a sprite-shaped 2D light.
        /// </summary>
        Sprite = 2,
        /// <summary>
        /// Uses a point light.
        /// </summary>
        Point = 3,
        /// <summary>
        /// Uses a global light.
        /// </summary>
        Global = 4
    }

    /// <summary>
    /// Melee/tool behavior applied to a custom item.
    /// </summary>
    [Serializable]
    public class ToolProperties
    {
        /// <summary>
        /// Damage dealt to enemies and traders.
        /// </summary>
        public float Damage = 25f;

        /// <summary>
        /// Damage dealt to structures and tiles.
        /// </summary>
        public float StructuralDamage = 25f;

        /// <summary>
        /// Multiplier applied to the vanilla attack cooldown.
        /// </summary>
        public float AttackCooldownMultiplier = 0.66f;

        /// <summary>
        /// Maximum hit distance.
        /// </summary>
        public float Distance = 2.5f;

        /// <summary>
        /// Knockback force applied on hit.
        /// </summary>
        public float KnockBack = 270f;

        /// <summary>
        /// Base cooldown between uses.
        /// </summary>
        public float Cooldown = 0.35f;

        /// <summary>
        /// Animator trigger or state name used for attacks. Not recommended to change unless you know what you're doing.
        /// </summary>
        public string AttackAnimation = "SwingAnim";

        /// <summary>
        /// Stamina consumed per attack.
        /// </summary>
        public float StaminaUse = 0.5f;

        /// <summary>
        /// Enables piercing hits.
        /// </summary>
        public bool Piercing;

        /// <summary>
        /// Swing sounds randomly used when attacking.
        /// </summary>
        public string[] SwingSounds = { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" };

        /// <summary>
        /// Playback volume for swing sounds.
        /// </summary>
        public float Volume = 0.5f;

        /// <summary>
        /// Visual swing rotation amount.
        /// </summary>
        public float RotateAmount = 15.5f;

        /// <summary>
        /// Enables physical swing hit logic. (Set false for drills and other non-physical swinging tools).
        /// </summary>
        public bool PhysicalSwing = true;

        /// <summary>
        /// Plays the attack animation when attacking.
        /// </summary>
        public bool DoAttackAnimation = true;

        /// <summary>
        /// Enables the vanilla extra-damage-vs-metal behavior.
        /// </summary>
        public bool MetalMoreDamage;

        /// <summary>
        /// Tool condition/battery lost per successful hit.
        /// </summary>
        public float ConditionLossOnHit = 0.02f;
    }

    /// <summary>
    /// Bandage minigame behavior applied to a custom item.
    /// </summary>
    [Serializable]
    public class BandageProperties
    {
        /// <summary>
        /// Overall effectiveness score used by bandage logic. Higher = more healing with less rotation needed. Vanilla bandages go up to 12f.
        /// </summary>
        public float Effectiveness = 8f;

        /// <summary>
        /// Skin healing applied by the bandage. Flat.
        /// </summary>
        public float SkinHealAmount = 8f;

        /// <summary>
        /// Bleed amount healing applied by the bandage, over a few seconds. Flat.
        /// </summary>
        public float BandageSlowAmount = 18f;

        /// <summary>
        /// Pain reduction applied by the bandage. 
        /// </summary>
        public float PainReduction = 40f;

        /// <summary>
        /// Fracture timer reduction applied by the bandage. Flat.
        /// </summary>
        public float BoneHealTimerReduction = 5f;

        /// <summary>
        /// Dislocation timer reduction applied by the bandage. Flat.
        /// </summary>
        public float DislocationTimerReduction = 5f;

        /// <summary>
        /// Minigame color used by the bandaging UI's bandage.
        /// </summary>
        public Color MinigameColor = new Color(0.9f, 0.9f, 0.9f);

        /// <summary>
        /// Whether to auto-create the wrapped bandage sprite variant.
        /// </summary>
        public bool CreateWrapSprite = true;

        /// <summary>
        /// Resource path for the wrap sprite. If just changing a color, use <see cref="WrapSpriteColor"/> instead of creating/using a new sprite.
        /// </summary>
        public string WrapSpritePath = "Special/bandageWrap";

        /// <summary>
        /// Tint applied to the wrap sprite.
        /// </summary>
        public Color WrapSpriteColor = Color.white;
    }

    /// <summary>
    /// Syringe and liquid-container behavior applied to a custom item.
    /// </summary>
    [Serializable]
    public class SyringeProperties
    {
        /// <summary>
        /// Maximum amount of liquid the container can hold.
        /// </summary>
        public float Capacity = 100f;

        /// <summary>
        /// Automatically fills the syringe from the environment when supported.
        /// </summary>
        public bool AutoFill;

        /// <summary>
        /// Amount consumed by a full use.
        /// </summary>
        public float AmountPerFullUse = 100f;

        /// <summary>
        /// Uses the average contained liquid color for syringe visuals.
        /// </summary>
        public bool UseAverageColor = true;

        /// <summary>
        /// Color used by syringe minigame UI for the liquid in the syringe.
        /// </summary>
        public Color MinigameColor = Color.white;

        /// <summary>
        /// Starting liquid contents added when the item spawns.
        /// </summary>
        public List<LiquidStack> DefaultContents = new List<LiquidStack>();
    }
}
