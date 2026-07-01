using UnityEngine;

namespace CUCoreLib.Util;

public enum SkillType
{
    Strength = 0,
    Resilience = 1,
    Intelligence = 2
}

public static class PlayerSkillUtils
{
    public static float XpMultiplier
    {
        get => Skills.xpGainMult;
        set
        {
            if (WorldGeneration.runSettings != null) WorldGeneration.runSettings["xpgain"] = Mathf.Max(0f, value);
        }
    }

    private static Skills GetSkills()
    {
        return PlayerUtils.GetBody().skills;
    }

    public static int GetLevel(SkillType skillType)
    {
        return GetSkills() is { } skill
            ? skillType switch { SkillType.Strength => skill.STR, SkillType.Resilience => skill.RES, _ => skill.INT }
            : 0;
    }

    public static float GetExperience(SkillType skillType)
    {
        return GetSkills() is { } skill
            ? skillType switch
            {
                SkillType.Strength => skill.expSTR, SkillType.Resilience => skill.expRES, _ => skill.expINT
            }
            : 0f;
    }

    public static float GetProgress(SkillType skillType)
    {
        return GetSkills() is { } skill
            ? skillType switch
            {
                SkillType.Strength => skill.ToNextNormalized(skill.expSTR, skill.minSTR, skill.maxSTR),
                SkillType.Resilience => skill.ToNextNormalized(skill.expRES, skill.minRES, skill.maxRES),
                _ => skill.ToNextNormalized(skill.expINT, skill.minINT, skill.maxINT)
            }
            : 0f;
    }

    public static float GetExperienceInLevel(SkillType skillType)
    {
        return GetSkills() is { } skill
            ? skillType switch
            {
                SkillType.Strength => skill.expSTR - skill.minSTR, SkillType.Resilience => skill.expRES - skill.minRES,
                _ => skill.expINT - skill.minINT
            }
            : 0f;
    }

    public static float GetExperienceForNextLevel(SkillType skillType)
    {
        return GetSkills() is { } skill
            ? skillType switch
            {
                SkillType.Strength => skill.maxSTR - skill.minSTR, SkillType.Resilience => skill.maxRES - skill.minRES,
                _ => skill.maxINT - skill.minINT
            }
            : 0f;
    }

    public static int GetExperienceForLevel(int targetLevel)
    {
        return Skills.GetExperienceForLevel(targetLevel);
    }

    public static void AddExperience(SkillType skillType, float xp)
    {
        GetSkills()?.AddExp((int)skillType, xp);
    }

    public static void SetLevelRaw(SkillType skillType, int level)
    {
        if (GetSkills() is not { } skill) return;
        level = Mathf.Max(0, level);
        switch (skillType)
        {
            case SkillType.Strength: skill.STR = level; break;
            case SkillType.Resilience: skill.RES = level; break;
            case SkillType.Intelligence:
            default: skill.INT = level; break;
        }

        skill.UpdateExpBoundaries();
        switch (skillType)
        {
            case SkillType.Strength: skill.expSTR = skill.minSTR; break;
            case SkillType.Resilience: skill.expRES = skill.minRES; break;
            case SkillType.Intelligence:
            default: skill.expINT = skill.minINT; break;
        }
    }
}
