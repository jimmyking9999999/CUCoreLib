using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CUCoreLib.Util;

public enum LimbSlot
{
    Head = 0,
    Thorax = 1,
    Pelvis = 2
}

public static class PlayerLimbUtils
{
    public static Limb GetLimb(int index)
    {
        if (PlayerUtils.GetBody()?.limbs == null || index < 0 || index >= PlayerUtils.GetBody().limbs.Length) return null;
        return PlayerUtils.GetBody().limbs[index];
    }

    public static Limb GetLimb(LimbSlot slot)
    {
        return GetLimb((int)slot);
    }

    public static Limb GetLimbByName(string name)
    {
        return PlayerUtils.GetBody()?.LimbByName(name);
    }

    public static List<Limb> GetAllLimbs()
    {
        return PlayerUtils.GetBody()?.limbs != null ? [..PlayerUtils.GetBody().limbs] : [];
    }

    public static bool HasBrokenBone()
    {
        if (PlayerUtils.GetBody()?.limbs == null) return false;
        return PlayerUtils.GetBody().limbs.Any(limb => limb is { dismembered: false, broken: true });
    }

    public static bool HasDislocation()
    {
        return PlayerUtils.GetBody()?.limbs != null
               && PlayerUtils.GetBody().limbs.Any(limb => limb is { dismembered: false, dislocated: true });
    }

    public static bool HasInfection()
    {
        if (PlayerUtils.GetBody()?.limbs == null) return false;
        return PlayerUtils.GetBody().limbs.Any(limb => limb is { dismembered: false, infected: true });
    }

    public static bool HasDismemberment()
    {
        return PlayerUtils.GetBody()?.limbs != null
               && PlayerUtils.GetBody().limbs.Any(limb => limb is { dismembered: true });
    }

    public static float GetMaxInfection()
    {
        if (PlayerUtils.GetBody()?.limbs == null) return 0f;
        var max = 0f;
        foreach (var limb in PlayerUtils.GetBody().limbs)
            if (limb is { dismembered: false } && limb.infectionAmount > max)
                max = limb.infectionAmount;
        return max;
    }

    public static float GetAveragePain()
    {
        return PlayerUtils.GetBody()?.averagePain ?? 0f;
    }

    public static float GetTotalBleedSpeed()
    {
        return PlayerUtils.GetBody()?.totalBleedSpeed ?? 0f;
    }

    public static void HealLimb(Limb limb)
    {
        if (limb == null || limb.dismembered) return;
        limb.skinHealth = limb.muscleHealth = 100f;
        limb.bleedAmount = limb.pain = limb.infectionAmount = 0f;
        limb.infected = false;
        limb.shrapnel = 0;
        if (limb.broken) limb.MendBone();
        if (limb.dislocated) limb.UnDislocate();
    }

    public static void HealLimb(int index)
    {
        HealLimb(GetLimb(index));
    }

    public static void DamageSkin(Limb limb, float value)
    {
        if (limb != null) limb.skinHealth = Mathf.Clamp(limb.skinHealth - value, 0f, 100f);
    }

    public static void DamageMuscle(Limb limb, float value)
    {
        if (limb != null) limb.muscleHealth = Mathf.Clamp(limb.muscleHealth - value, 0f, 100f);
    }

    public static void SetSkinHealthRaw(Limb limb, float value)
    {
        if (limb != null) limb.skinHealth = Mathf.Clamp(value, 0f, 100f);
    }

    public static void SetMuscleHealthRaw(Limb limb, float value)
    {
        if (limb != null) limb.muscleHealth = Mathf.Clamp(value, 0f, 100f);
    }

    public static void SetBleedRaw(Limb limb, float value)
    {
        if (limb != null) limb.bleedAmount = Mathf.Clamp(value, 0f, 100f);
    }

    public static void SetPainRaw(Limb limb, float value)
    {
        if (limb != null) limb.pain = Mathf.Clamp(value, 0f, 100f);
    }

    public static void SetInfectionRaw(Limb limb, float value)
    {
        if (limb == null) return;
        limb.infectionAmount = Mathf.Clamp(value, 0f, 100f);
        limb.infected = value > 0f;
    }
}
