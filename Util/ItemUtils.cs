using System.Collections.Generic;
using System.Linq;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using UnityEngine;

namespace CUCoreLib.Util;

public static class ItemUtils
{
    public static List<Item> FindNearby(Vector2 center, float radius, bool includeContained = false)
    {
        var result = new List<Item>();
        if (radius <= 0f) return result;
        var sqr = radius * radius;
        result.AddRange(Object.FindObjectsOfType<Item>()
            .Where(item => includeContained
                           || item.ParentContainer() == null)
            .Where(item => ((Vector2)item.transform.position - center).sqrMagnitude <= sqr));

        return result;
    }

    public static Item FindClosest(Vector2 center, float maxRadius = float.MaxValue, bool includeContained = false)
    {
        Item best = null;
        var bestSqr = maxRadius * maxRadius;
        foreach (var item in Object.FindObjectsOfType<Item>())
        {
            if (item == null) continue;
            if (!includeContained
                && item.ParentContainer() != null) continue;
            var d = ((Vector2)item.transform.position - center).sqrMagnitude;
            if (!(d < bestSqr)) continue;
            bestSqr = d;
            best = item;
        }

        return best;
    }

    public static void SetCondition(Item item, float condition)
    {
        item?.SetCondition(Mathf.Clamp01(condition));
    }

    public static void Repair(Item item)
    {
        SetCondition(item, 1f);
    }

    public static void SetFavourited(Item item, bool favourited)
    {
        if (item != null) item.favourited = favourited;
    }

    public static void ToggleFavourited(Item item)
    {
        if (item != null) SetFavourited(item, !item.favourited);
    }

    public static bool TryGetCustomItemInfo(string id, out CustomItemInfo info)
    {
        return ItemRegistry.TryGetCustomInfo(id, out info);
    }
}
