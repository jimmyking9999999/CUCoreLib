using System.Collections.Generic;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class RecipeRegistryPatches
    {
        private static int selectedRecipeRefreshCount;

        internal static void RefreshCraftingUi()
        {
            var camera = PlayerCamera.main;
            if (camera == null || Recipes.recipes == null) return;

            // Vanilla's RefreshCurrentlySelectedRecipe dereferences craftingPanel unguarded, and
            // other mods' prefixes run on the same path. The panel only exists/visible while the
            // menu is open; OpenCraftScreen() rebuilds the list on every open, so a closed menu
            // needs no refresh at all.
            if (camera.craftingPanel == null || !camera.craftingPanel.activeInHierarchy) return;

            var recipeCount = Recipes.recipes.Count;
            camera.selectedRecipe = recipeCount <= 0
                ? 0
                : Mathf.Clamp(camera.selectedRecipe, 0, recipeCount - 1);

            try
            {
                var refreshCount = selectedRecipeRefreshCount;
                camera.RefreshRecipeList();
                if (selectedRecipeRefreshCount == refreshCount)
                    camera.RefreshCurrentlySelectedRecipe();
            }
            catch (System.Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning($"Crafting UI refresh skipped after recipe edit: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(Recipes), "SetUpRecipes")]
        [HarmonyPostfix]
        private static void InjectRecipes()
        {
            LiquidRegistry.InjectRegisteredLiquids(true);
            RecipeRegistry.ApplyVanillaRecipeEdits();
            RecipeRegistry.InjectRegisteredRecipes();
        }

        // TODO. There's an issue with invalid sprites causing scrolling to not fully function
        [HarmonyPatch(typeof(Recipe), "resultSprite", MethodType.Getter)]
        [HarmonyPrefix]
        private static bool FixRecipeIcon(Recipe __instance, ref (Sprite, Color) __result)
        {
            if (__instance?.result == null || string.IsNullOrWhiteSpace(__instance.result.id))
            {
                __result = (ItemRegistry.GetMissingItemIcon(), Color.white);
                return false;
            }

            if (__instance.result.isLiquid)
            {
                LiquidRegistry.EnsureLiquidInjected(__instance.result.id);
                if (!RecipeRegistry.IsKnownRecipeResult(__instance.result))
                {
                    __result = (ItemRegistry.GetMissingItemIcon(), Color.white);
                    return false;
                }

                return true;
            }

            if (!RecipeRegistry.IsKnownRecipeResult(__instance.result))
            {
                __result = (ItemRegistry.GetMissingItemIcon(), Color.white);
                return false;
            }

            var customSprite = AssetLoader.GetCachedSprite(__instance.result.id);
            if (!ItemRegistry.IsValidIcon(customSprite)) ItemRegistry.TryGetIcon(__instance.result.id, out customSprite);

            if (customSprite != null)
            {
                __result = (customSprite, Color.white);
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(PlayerCamera), "RefreshRecipeList")]
        [HarmonyPostfix]
        private static void ApplyRecipeListAnimations(PlayerCamera __instance)
        {
            if (__instance == null || __instance.recipeListContent == null) return;

            for (var i = 0; i < __instance.recipeListContent.childCount; i++)
            {
                var recipeBox = __instance.recipeListContent.GetChild(i);
                if (recipeBox.childCount < 3) continue;

                if (i < 0 || i >= Recipes.recipes.Count) continue;

                var recipe = Recipes.recipes[i];
                var animationId = recipe?.result?.id;
                if (string.IsNullOrWhiteSpace(animationId)) continue;

                var image = recipeBox.GetChild(2).GetComponent<Image>();
                AssetLoader.TryApplyAnimation(image, animationId);
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), "RefreshCurrentlySelectedRecipe")]
        [HarmonyPostfix]
        private static void TrackSelectedRecipeRefresh()
        {
            selectedRecipeRefreshCount++;
        }

        [HarmonyPatch(typeof(PlayerCamera), "RefreshCurrentlySelectedRecipe")]
        [HarmonyPostfix]
        private static void ApplySelectedRecipeAnimation(PlayerCamera __instance)
        {
            if (__instance == null || __instance.craftingPanel == null || Recipes.recipes == null) return;

            var selectedRecipe = __instance.selectedRecipe;
            if (selectedRecipe < 0 || selectedRecipe >= Recipes.recipes.Count) return;

            var recipe = Recipes.recipes[selectedRecipe];
            if (recipe?.result == null || string.IsNullOrWhiteSpace(recipe.result.id)) return;

            var image = __instance.craftingPanel.transform.GetChild(6).GetComponent<Image>();
            AssetLoader.TryApplyAnimation(image, recipe.result.id);
        }

        [HarmonyPatch(typeof(RecipeResult), "SpawnResult")]
        [HarmonyPrefix]
        private static bool SpawnCustomResult(RecipeResult __instance, int recipeInt)
        {
            // I don't like rewriting the entire method, but it was this or ILCode.. 
            // Here's hoping it doesn't break other mods :p
            if (__instance == null || __instance.isLiquid ||
                !ItemRegistry.RegisteredItems.ContainsKey(__instance.id)) return true;

            // Vanilla fail
            var skillDiff = PlayerCamera.main.body.skills.INT - recipeInt;
            var conditionMult = 1f;
            if (skillDiff < 0 && Random.value < 0.5f) conditionMult = Random.Range(0.2f, 0.9f);

            for (var j = 0; j < __instance.amount; j++)
            {
                var resultObj = CustomInstantiate.InstantiateReturn(__instance.id,
                    PlayerCamera.main.body.transform.position, Quaternion.identity);

                if (resultObj != null)
                {
                    var component = resultObj.GetComponent<Item>();
                    if (component == null) continue;
                    component.condition = __instance.resultCondition * conditionMult;
                    PlayerCamera.main.body.AutoPickUpItem(component);

                    if (!__instance.dontDrainResultLiquid &&
                        component.TryGetComponent<WaterContainerItem>(out var wat))
                        wat.stack = new List<LiquidStack>();
                }
                else
                {
                    CUCoreLibPlugin.Log.LogError($"Failed to craftspawn '{__instance.id}'!");
                }
            }

            return false;
        }
    }
}
