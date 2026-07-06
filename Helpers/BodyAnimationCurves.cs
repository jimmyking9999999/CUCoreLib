using System;
using System.Collections.Generic;
using CUCoreLib.Data;
using UnityEngine;

namespace CUCoreLib.Helpers
{
    public enum BodyAnimationCurveField
    {
        StaminaStrength,
        WeightMovementCurve,
        TemperatureMovementCurve,
        FoodMovementCurve,
        HungerLimbHeal,
        DepressionChanceCurve,
        ImmunityInfectionSpeed,
        LastLastChanceHappiness,
        ClawDamageCurve,
        HeartCurveNormal,
        HeartCurveArrythmia,
        ThirstBloodPressureCurve
    }

    public static class BodyAnimationCurves
    {
        public static bool TryApplyCurve(Body body, BodyAnimationCurveField field, AnimationCurve curve)
        {
            if (body == null || curve == null) return false;

            switch (field)
            {
                case BodyAnimationCurveField.StaminaStrength:
                    body.staminaStrength = curve;
                    return true;
                case BodyAnimationCurveField.WeightMovementCurve:
                    body.weightMovementCurve = curve;
                    return true;
                case BodyAnimationCurveField.TemperatureMovementCurve:
                    body.temperatureMovementCurve = curve;
                    return true;
                case BodyAnimationCurveField.FoodMovementCurve:
                    body.foodMovementCurve = curve;
                    return true;
                case BodyAnimationCurveField.HungerLimbHeal:
                    body.hungerLimbHeal = curve;
                    return true;
                case BodyAnimationCurveField.DepressionChanceCurve:
                    body.depressionChanceCurve = curve;
                    return true;
                case BodyAnimationCurveField.ImmunityInfectionSpeed:
                    body.immunityInfectionSpeed = curve;
                    return true;
                case BodyAnimationCurveField.LastLastChanceHappiness:
                    body.lastLastChanceHappiness = curve;
                    return true;
                case BodyAnimationCurveField.ClawDamageCurve:
                    body.clawDamageCurve = curve;
                    return true;
                case BodyAnimationCurveField.HeartCurveNormal:
                    body.heartCurveNormal = curve;
                    return true;
                case BodyAnimationCurveField.HeartCurveArrythmia:
                    body.heartCurveArrythmia = curve;
                    return true;
                case BodyAnimationCurveField.ThirstBloodPressureCurve:
                    body.thirstBloodPressureCurve = curve;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryApplyBundledCurve(Body body, BodyAnimationCurveField field, string bundleId,
            string assetName)
        {
            if (!AssetLoader.TryLoadBundleAnimationCurve(bundleId, assetName, out var curve)) return false;

            return TryApplyCurve(body, field, curve);
        }

        public static bool TryApplyBundledCurves(Body body, string bundleId,
            IEnumerable<BodyAnimationCurveOverride> overrides)
        {
            if (body == null || overrides == null) return false;

            var resolvedOverrides = new List<ResolvedCurveOverride>();
            foreach (var entry in overrides)
            {
                if (string.IsNullOrWhiteSpace(entry.AssetName)) return false;
                if (!AssetLoader.TryLoadBundleAnimationCurve(bundleId, entry.AssetName, out var curve)) return false;

                resolvedOverrides.Add(new ResolvedCurveOverride(entry.Field, curve));
            }

            if (resolvedOverrides.Count == 0) return false;

            var appliedAny = false;
            for (var i = 0; i < resolvedOverrides.Count; i++)
                appliedAny |= TryApplyCurve(body, resolvedOverrides[i].Field, resolvedOverrides[i].Curve);

            return appliedAny;
        }

        public static bool TryApplyBundledCurves(Body body, string bundleId,
            params BodyAnimationCurveOverride[] overrides)
        {
            return overrides != null && TryApplyBundledCurves(body, bundleId, (IEnumerable<BodyAnimationCurveOverride>)overrides);
        }

        public static bool TryApplyBundledProfile(Body body, string bundleId, string assetName)
        {
            if (!AssetLoader.TryLoadBundleAsset(bundleId, assetName, out BodyAnimationCurveProfileAsset profile))
                return false;

            return TryApplyBundledProfile(body, bundleId, profile);
        }

        public static bool TryApplyBundledProfile(Body body, string bundleId, BodyAnimationCurveProfileAsset profile)
        {
            if (profile == null) return false;

            return TryApplyBundledCurves(body, bundleId, profile.Overrides);
        }

        private readonly struct ResolvedCurveOverride
        {
            public ResolvedCurveOverride(BodyAnimationCurveField field, AnimationCurve curve)
            {
                Field = field;
                Curve = curve;
            }

            public BodyAnimationCurveField Field { get; }
            public AnimationCurve Curve { get; }
        }
    }
}
