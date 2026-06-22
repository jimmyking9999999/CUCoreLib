namespace CUCoreLib.Data
{
using System.Collections.Generic;
using System;
using UnityEngine;

    public class CustomItemInfo : LiquidItemInfo
    {
        private readonly HashSet<CustomItemExplicitField> explicitlySetFields =
            new HashSet<CustomItemExplicitField>();

        public string ID;
        public Sprite Icon;
        public Sprite WornSprite;
        public Sprite LiquidMask;
        public string IconAnimationId;
        public string WornSpriteAnimationId;
        public Vector2 HeldSpriteOffset;
        public Vector2 WornSpriteOffset;
        public float SpriteScale = 1.0f;
        public SpriteScaleDimensions SpriteScaleDimensions;
        public int SpawnFrequency = 1;
        public ContainerProperties Container;
        public BatteryProperties Battery;
        public LightProperties Light;
        public BandageProperties Bandage;
        public SyringeProperties Syringe;
        public ToolProperties Tool;
        public List<string> SpawnComponents = new List<string>();
        public Dictionary<string, object> CustomData = new Dictionary<string, object>();

        public new bool usable
        {
            get => base.usable;
            set
            {
                base.usable = value;
                explicitlySetFields.Add(CustomItemExplicitField.Usable);
            }
        }

        public new bool usableOnLimb
        {
            get => base.usableOnLimb;
            set
            {
                base.usableOnLimb = value;
                explicitlySetFields.Add(CustomItemExplicitField.UsableOnLimb);
            }
        }

        public new bool usableWithLMB
        {
            get => base.usableWithLMB;
            set
            {
                base.usableWithLMB = value;
                explicitlySetFields.Add(CustomItemExplicitField.UsableWithLmb);
            }
        }

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

    [System.Serializable]
    public struct SpriteScaleDimensions
    {
        public float Width;
        public float Height;
        public bool ExpandToFirstMetCondition;

        public SpriteScaleDimensions(float width, float height, bool expandToFirstMetCondition = false)
        {
            Width = width;
            Height = height;
            ExpandToFirstMetCondition = expandToFirstMetCondition;
        }

        public bool IsConfigured
        {
            get
            {
                return Width > 0f && Height > 0f;
            }
        }

        public static implicit operator SpriteScaleDimensions((float width, float height) value)
        {
            return new SpriteScaleDimensions(value.width, value.height);
        }

        public static implicit operator SpriteScaleDimensions((float width, float height, bool expandToFirstMetCondition) value)
        {
            return new SpriteScaleDimensions(value.width, value.height, value.expandToFirstMetCondition);
        }
    }

    [System.Serializable]
    public class ContainerProperties
    {
        public float Capacity = 10f;
        public float MaxWeightPerItem = 5f;
        public float EncumbranceReduction = 1.0f;
    }

    [System.Serializable]
    public class BatteryProperties
    {
        public float MaxCharge = 100f;
        public float StartCharge = 100f;
        public BatteryItem.BatteryPreset Preset = BatteryItem.BatteryPreset.Medium;
        public string BatteryType = "mediumbattery";
        public bool SpawnWithBattery = true;
    }

    [System.Serializable]
    public class LightProperties
    {
        public float Intensity = 0.75f;
        public Color Color = Color.white;
        public float PointLightOuterRadius = 7.5f;
        public float PointLightInnerRadius = 0f;
        public CustomLightType LightType = CustomLightType.Point;
        public Vector2 Offset = Vector2.zero;
        public bool AddLightItem = true;
    }

    public enum CustomLightType
    {
        Parametric = 0,
        Freeform = 1,
        Sprite = 2,
        Point = 3,
        Global = 4
    }

    [System.Serializable]
    public class ToolProperties
    {
        public float Damage = 25f; 
        public float StructuralDamage = 25f;
        public float AttackCooldownMultiplier = 0.66f;
        public float Distance = 5f;
        public float KnockBack = 270f;
        public float Cooldown = 0.35f;
        public string AttackAnimation = "SwingAnim";
        public float StaminaUse = 0.5f;
        public bool Piercing = false;
        public string[] SwingSounds = new string[] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" };
        public float Volume = 0.5f;
        public float RotateAmount = 15.5f;
        public bool PhysicalSwing = true;
        public bool DoAttackAnimation = true;
        public bool MetalMoreDamage = false;
        public float ConditionLossOnHit = 0.02f;
    }

    [System.Serializable]
    public class BandageProperties
    {
        public float Effectiveness = 8f;
        public float SkinHealAmount = 8f;
        public float BandageSlowAmount = 18f;
        public float PainReduction = 40f;
        public float BoneHealTimerReduction = 5f;
        public float DislocationTimerReduction = 5f;
        public Color MinigameColor = new Color(0.9f, 0.9f, 0.9f);
        public bool CreateWrapSprite = true;
        public string WrapSpritePath = "Special/bandageWrap";
        public Color WrapSpriteColor = Color.white;
    }

    [System.Serializable]
    public class SyringeProperties
    {
        public float Capacity = 100f;
        public bool AutoFill = false;
        public float AmountPerFullUse = 100f;
        public bool UseAverageColor = true;
        public Color MinigameColor = Color.white;
        public List<LiquidStack> DefaultContents = new List<LiquidStack>();
    }
}
