using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CUCoreLib.Patches
{
    [HarmonyPatch(typeof(BuildingEntity), "Start")]
    internal static class BuildingEntityPatches
    {
        [HarmonyPostfix]
        private static void PreserveRegisteredBuildingLocale(BuildingEntity __instance)
        {
            if (__instance == null ||
                !BuildingEntityRegistry.TryGetDefinition(__instance.id, out var definition)) return;

            if (!string.IsNullOrEmpty(definition.Name)) __instance.fullName = definition.Name;

            if (!__instance.skipDescriptionSet && !string.IsNullOrEmpty(definition.Description))
                __instance.description = definition.Description;
        }
    }

    // BuildingEntity's vanilla destruction path loads each drop directly from Resources.
    // CUCoreLib items are runtime templates, so they need the same fallback used by save loading.
    [HarmonyPatch(typeof(BuildingEntity), "Update")]
    internal static class BuildingEntityCustomDropResolutionPatch
    {
        private static readonly MethodInfo ResourcesLoadMethod = typeof(Resources)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(method =>
                method.Name == nameof(Resources.Load) &&
                !method.IsGenericMethod &&
                method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType == typeof(string));

        private static readonly MethodInfo ResolveSavedResourceMethod =
            AccessTools.Method(typeof(CustomInstantiate), nameof(CustomInstantiate.ResolveSavedResource));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ResolveRuntimeCustomDrops(
            IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(ResourcesLoadMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, ResolveSavedResourceMethod);
                    continue;
                }

                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(BuildingEntity), "Update")]
    internal static class BuildingEntityDropPoolPatches
    {
        [HarmonyPrefix]
        private static bool HandleBuiltInDropPools(BuildingEntity __instance)
        {
            if (__instance == null || __instance.health >= 0.5f) return true;
            if (!TryResolveBuiltInSource(__instance.id, out var source)) return true;

            TryGetComponent<SpriteRenderer>(__instance, out var spriteRenderer);
            if (spriteRenderer != null)
            {
                var particle = Object.Instantiate(Resources.Load("BuildingBreakParticle"),
                    __instance.transform.position, __instance.transform.rotation) as GameObject;
                if (particle != null)
                {
                    var shape = particle.GetComponent<ParticleSystem>().shape;
                    shape.texture = spriteRenderer.sprite.texture;
                    shape.sprite = spriteRenderer.sprite;
                    particle.GetComponent<ParticleSystem>().Play();
                }
            }

            Object.Instantiate(Resources.Load<GameObject>("DustBig"), __instance.transform.position, Quaternion.identity);
            if (__instance.animal) __instance.gameObject.SendMessage("AnimalDeath");

            Sound.Play("footstep/Rock/11", __instance.transform.position);

            var isNearPlayer = PlayerCamera.main != null &&
                               PlayerCamera.main.body != null &&
                               Vector2.Distance(__instance.transform.position,
                                   PlayerCamera.main.body.transform.position) < 8f;

            SpawnDropArray(__instance, __instance.itemsDropOnDestroy, __instance.dropChanceMultiplier, isNearPlayer, true);
            SpawnDropPoolEntries(__instance, source, isNearPlayer);
            SpawnDropArray(__instance, __instance.alwaysDrop, 1f, isNearPlayer, false);

            Object.Destroy(__instance.gameObject);
            return false;
        }

        private static bool TryResolveBuiltInSource(string id, out DropPool source)
        {
            source = DropPool.None;
            var normalizedId = SpawnIdHelpers.NormalizeSpawnId(id ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedId)) return false;

            switch (normalizedId.ToLowerInvariant())
            {
                case "medcrate":
                    source = DropPool.MedicalCrate;
                    return true;
                case "foodbox":
                    source = DropPool.FoodCrate;
                    return true;
                case "containercrate":
                    source = DropPool.ContainerCrate;
                    return true;
                case "lifepodchest":
                    source = DropPool.CapsuleContainer;
                    return true;
                case "dropcapsule":
                    source = DropPool.DropCapsule;
                    return true;
                default:
                    return false;
            }
        }

        private static void SpawnDropPoolEntries(BuildingEntity building, DropPool source, bool isNearPlayer)
        {
            if (building == null || building.guaranteedDropAmount <= 0) return;

            for (var i = 0; i < building.guaranteedDropAmount; i++)
            {
                var fallbackCategory = building.itemCategoriesToAdd != null && building.itemCategoriesToAdd.Length > 0
                    ? building.itemCategoriesToAdd[UnityEngine.Random.Range(0, building.itemCategoriesToAdd.Length)]
                    : null;

                if (!DropPoolRegistry.TryGetRandomItemId(source, fallbackCategory, out var itemId)) continue;

                SpawnSingleDrop(building.transform.position, itemId, 1f, 1f, isNearPlayer);
            }
        }

        private static void SpawnDropArray(BuildingEntity building, ItemDrop[] drops, float multiplier, bool isNearPlayer,
            bool useChance)
        {
            if (building == null || drops == null || drops.Length == 0) return;

            foreach (var drop in drops)
            {
                if (drop == null || string.IsNullOrWhiteSpace(drop.id)) continue;
                if (useChance && UnityEngine.Random.Range(0f, 1f) >= drop.chance * multiplier) continue;

                SpawnSingleDrop(building.transform.position, drop.id, drop.conditionMin, drop.conditionMax, isNearPlayer);
            }
        }

        private static void SpawnSingleDrop(Vector3 position, string itemId, float conditionMin, float conditionMax,
            bool isNearPlayer)
        {
            var instance = CustomInstantiate.InstantiateReturn(
                itemId,
                position,
                Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)),
                UnityEngine.Random.Range(conditionMin, conditionMax));
            if (instance == null) return;

            if (instance.TryGetComponent<Rigidbody2D>(out var body))
                body.velocity = new Vector2(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-7f, 7f));

            if (instance.TryGetComponent<Item>(out var item)) item.SetCondition(UnityEngine.Random.Range(conditionMin, conditionMax));

            if (isNearPlayer && instance.GetComponent<Rigidbody2D>() != null && instance.GetComponent<SpriteRenderer>() != null)
                instance.AddComponent<FreshItemDrop>();
        }

        private static bool TryGetComponent<T>(Component component, out T value) where T : Component
        {
            value = component != null ? component.GetComponent<T>() : null;
            return value != null;
        }
    }
}
