using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CUCoreLib.ContentReload;
using CUCoreLib.Registries.Infrastructure;
using UnityEngine;

namespace CUCoreLib.Registries
{
    public static class RecipeRegistry
    {
        internal static List<Recipe> RegisteredRecipes = new List<Recipe>();

        private static readonly HashSet<string> RegisteredRecipeKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly RegistrationOwnershipIndex<string> RecipeOwners =
            new RegistrationOwnershipIndex<string>(StringComparer.OrdinalIgnoreCase);

        private static List<Recipe> LastRecipeList;

        private static readonly HashSet<string> InjectedRecipeKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> WarnedInvalidRecipeIngredientKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static int PendingHotReloadInjectedRecipeCount;

        public static void Register(Recipe recipe)
        {
            if (recipe?.result == null || string.IsNullOrWhiteSpace(recipe.result.id))
            {
                CUCoreLibPlugin.Log.LogError("Recipe registration failed: Result ID is missing.");
                return;
            }

            NormalizeRecipeIngredients(recipe);
            ValidateRecipeReferences(recipe);

            var key = BuildRecipeKey(recipe);
            if (!RegisteredRecipeKeys.Add(key))
            {
                CUCoreLibPlugin.Log.LogWarning(
                    $"Recipe registration ignored duplicate recipe for '{recipe.result.id}'.");
                return;
            }

            RegisteredRecipes.Add(recipe);
            RecipeOwners.Assign(key, ContentReloadSession.ResolveAmbientOwnerId());

            if (Recipes.recipes != null) InjectRegisteredRecipes();
        }

        public static IDisposable BeginOwnerRegistration(string ownerId)
        {
            return RecipeOwners.BeginScope(ownerId);
        }

        internal static string BuildRecipeKey(Recipe recipe)
        {
            if (recipe?.result == null) return string.Empty;

            var ingredientKey = BuildIngredientKey(recipe.items);
            return
                $"{recipe.result.id}|{recipe.result.amount}|{recipe.result.resultCondition}|{recipe.result.isLiquid}|{ingredientKey}";
        }

        internal static bool InjectSingleRecipe(Recipe recipe)
        {
            if (Recipes.recipes == null || recipe?.result == null) return false;
            EnsureCurrentRecipeList();
            NormalizeRecipeIngredients(recipe);
            ValidateRecipeReferences(recipe, deferVanillaValidation: false);

            var recipeKey = BuildRecipeKey(recipe);
            if (InjectedRecipeKeys.Contains(recipeKey)) return false;

            if (Recipes.recipes.Any(existing =>
                    BuildRecipeKey(existing).Equals(recipeKey, StringComparison.OrdinalIgnoreCase)))
            {
                InjectedRecipeKeys.Add(recipeKey);
                return false;
            }

            if (recipe.items != null)
                foreach (var item in recipe.items)
                {
                    if (!string.IsNullOrEmpty(item.specificId))
                        item.specific = true;

                    item.ignoredId = recipe.isRepair ? string.Empty : recipe.result.id;
                }

            recipe.index = Recipes.recipes.Count;
            Recipes.recipes.Add(recipe);
            InjectedRecipeKeys.Add(recipeKey);
            return true;
        }

        internal static int InjectRegisteredRecipes()
        {
            if (Recipes.recipes == null) return 0;

            EnsureCurrentRecipeList();

            var added = RegisteredRecipes.Count(InjectSingleRecipe);

            if (added <= 0) return 0;

            if (ContentReloadSession.IsActive)
            {
                PendingHotReloadInjectedRecipeCount += added;
                return added;
            }

            CUCoreLibPlugin.Log.LogInfo($"Recipes: Added {added} recipes.");

            return added;
        }

        internal static void FlushHotReloadInjectionSummary()
        {
            if (PendingHotReloadInjectedRecipeCount <= 0) return;

            CUCoreLibPlugin.Log.LogInfo($"Recipes: Added {PendingHotReloadInjectedRecipeCount} recipes.");
            PendingHotReloadInjectedRecipeCount = 0;
        }

        internal static void ResetHotReloadInjectionSummary()
        {
            PendingHotReloadInjectedRecipeCount = 0;
        }

        internal static List<Recipe> CaptureOwnerEntries(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return new List<Recipe>();

            var normalizedOwnerId = ownerId.Trim();
            return RegisteredRecipes
                .Where(recipe =>
                {
                    var key = BuildRecipeKey(recipe);
                    return RecipeOwners.IsOwnedBy(key, normalizedOwnerId);
                })
                .ToList();
        }

        internal static void RestoreOwnerEntries(string ownerId, IEnumerable<Recipe> recipes)
        {
            if (recipes == null) return;

            foreach (var recipe in recipes) Register(recipe);
        }

        internal static void ClearOwnerEntries(string ownerId, ContentReloadResult result)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return;

            var normalizedOwnerId = ownerId.Trim();
            var ownedKeys = RecipeOwners.GetKeys(normalizedOwnerId);

            if (ownedKeys.Length == 0) return;

            var ownedKeySet = new HashSet<string>(ownedKeys, StringComparer.OrdinalIgnoreCase);

            RegisteredRecipes.RemoveAll(recipe => ownedKeySet.Contains(BuildRecipeKey(recipe)));
            Recipes.recipes?.RemoveAll(recipe => ownedKeySet.Contains(BuildRecipeKey(recipe)));

            foreach (var key in ownedKeys)
            {
                RegisteredRecipeKeys.Remove(key);
                RecipeOwners.Remove(key);
                InjectedRecipeKeys.Remove(key);
                WarnedInvalidRecipeIngredientKeys.Remove(key);
            }

            result?.AddInfo("Cleared " + ownedKeys.Length + " recipes owned by '" + normalizedOwnerId + "'.");
        }

        private static void NormalizeRecipeIngredients(Recipe recipe)
        {
            if (recipe?.items == null) return;

            foreach (var item in recipe.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.specificId)) continue;

                item.specific = true;
            }
        }

        private static void EnsureCurrentRecipeList()
        {
            if (ReferenceEquals(LastRecipeList, Recipes.recipes)) return;
            LastRecipeList = Recipes.recipes;
            InjectedRecipeKeys.Clear();
        }

        private static void ValidateRecipeReferences(Recipe recipe, bool deferVanillaValidation = true)
        {
            if (recipe?.items == null) return;

            var recipeKey = BuildRecipeKey(recipe);

            for (var i = 0; i < recipe.items.Count; i++)
            {
                var item = recipe.items[i];
                if (item == null)
                {
                    CUCoreLibPlugin.Log?.LogWarning($"Recipe '{recipe.result.id}' has a null ingredient at index {i}.");
                    continue;
                }

                if (!item.specific) continue;
                if (string.IsNullOrWhiteSpace(item.specificId))
                {
                    CUCoreLibPlugin.Log?.LogWarning(
                        $"Recipe '{recipe.result.id}' has a specific ingredient without a specificId at index {i}.");
                    continue;
                }

                var normalizedId = item.specificId.Trim();
                if (TryResolveRecipeItemId(normalizedId, item.isLiquid, deferVanillaValidation)) continue;

                var warningKey = BuildInvalidIngredientWarningKey(recipeKey, i, item.isLiquid, normalizedId);
                if (!WarnedInvalidRecipeIngredientKeys.Add(warningKey)) continue;

                CUCoreLibPlugin.Log?.LogWarning(
                    $"Recipe '{recipe.result.id}' references unknown {(item.isLiquid ? "liquid" : "item")} '{normalizedId}' at ingredient index {i}.");
            }
        }

        private static bool TryResolveRecipeItemId(string id, bool isLiquid, bool deferVanillaValidation)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            if (isLiquid) return LiquidRegistry.TryGetCustomInfo(id, out _) || Liquids.Registry.ContainsKey(id);

            if (ItemRegistry.TryGetCustomInfo(id, out _)) return true;

            if (Item.GlobalItems == null)
                // Recipe registration commonly runs during plugin Awake, before vanilla item tables exist.
                // Defer vanilla-item validation until runtime instead of warning on valid base-game IDs.
                return deferVanillaValidation;

            return Item.GlobalItems.ContainsKey(id) || Resources.Load<GameObject>(id) != null;
        }

        private static string BuildInvalidIngredientWarningKey(string recipeKey, int index, bool isLiquid, string id)
        {
            return recipeKey + "|" + index + "|" + isLiquid + "|" + id;
        }

        private static string BuildIngredientKey(List<RecipeItem> items)
        {
            if (items == null || items.Count == 0) return string.Empty;

            var builder = new StringBuilder();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (i > 0) builder.Append(';');

                if (item == null)
                {
                    builder.Append("null");
                    continue;
                }

                builder.Append(item.minimumCondition);
                builder.Append('|');
                builder.Append(item.isLiquid ? 'L' : 'I');
                builder.Append('|');
                builder.Append(item.specific ? 'S' : 'A');
                builder.Append('|');
                builder.Append(string.IsNullOrWhiteSpace(item.specificId) ? string.Empty : item.specificId.Trim());
                builder.Append('|');
                builder.Append(item.destroyItem ? 'D' : 'K');
                builder.Append('|');
                builder.Append(item.quality != null ? item.quality.id : string.Empty);
                builder.Append('|');
                builder.Append(item.quality != null
                    ? item.quality.amount.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);
            }

            return builder.ToString();
        }

    }
}
