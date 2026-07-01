using CUCoreLib.Helpers;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches;

[HarmonyPatch(typeof(CorpseScript), "Start")]
internal static class CorpsePatches
{
    // May produce unwanted behaviour with another corpsescript patcher
    private static bool Prefix(CorpseScript __instance)
    {
        if (__instance.animalCorpse) return true;

        if (__instance.categories == null || __instance.categories.Length == 0) return false;

        var amount = 0;
        var roll = Random.Range(0f, 1f);
        if (roll > 0.5f) amount++;

        if (roll > 0.85f) amount++;

        if (roll > 0.95f) amount++;

        var corpseRenderer = __instance.GetComponent<SpriteRenderer>();

        for (var i = 0; i < amount; i++)
        {
            var category = __instance.categories[Random.Range(0, __instance.categories.Length)];
            if (!ItemLootPool.pool.TryGetValue(category, out var poolList) || poolList.Count == 0) continue;

            var randomId = poolList[Random.Range(0, poolList.Count)];
            var spawnPos = __instance.transform.position + new Vector3(Random.Range(-3f, 3f), 3f, 0f);
            var spawnRot = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var obj = CustomInstantiate.InstantiateReturn(randomId, spawnPos, spawnRot, Random.Range(0f, 1f));
            if (obj == null) continue;

            if (obj.TryGetComponent<SpriteRenderer>(out var objRenderer) && corpseRenderer != null)
                objRenderer.sortingOrder = corpseRenderer.sortingOrder + 1;
        }

        return false;
    }
}